using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FastBattleSimulator
{
    public class SimUnit
    {
        public int typeID, team, level, x, y;
        public float hp, maxHp, damage;
        public int range;
        public bool hasActedThisTurn;
        public string unitId;
        
        public SimUnit FindNearestEnemy(List<SimUnit> allUnits)
        {
            return allUnits
                .Where(u => u.team != team && u.hp > 0)
                .OrderBy(u => Mathf.Abs(u.x - x) + Mathf.Abs(u.y - y))
                .FirstOrDefault();
        }
    }

    // Статы синхронизированы с реальными классами
    private static float GetBaseHP(int typeID)
    {
        switch(typeID)
        {
            case 1: return 15f;  // Knight
            case 2: return 8f;   // Archer
            case 3: return 6f;   // Mage
            default: return 10f;
        }
    }

    private static float GetBaseDamage(int typeID)
    {
        switch(typeID)
        {
            case 1: return 5f;   // Knight
            case 2: return 4f;   // Archer
            case 3: return 7f;   // Mage
            default: return 5f;
        }
    }

    private static float GetLevelMultiplier(int level)
    {
        switch(level)
        {
            case 1: return 1f;
            case 2: return 2f;
            case 3: return 4.5f;
            default: return 1f;
        }
    }

    private static int GetRange(int typeID)
    {
        switch(typeID)
        {
            case 1: return 1;  // Knight
            case 2: return 3;  // Archer
            case 3: return 4;  // Mage
            default: return 1;
        }
    }

    public static SimUnit CreateUnit(int typeID, int level, int x, int y, int team)
    {
        float mult = GetLevelMultiplier(level);
        string id = $"t{typeID}_l{level}_tm{team}_{x}_{y}";
        
        return new SimUnit
        {
            typeID = typeID,
            level = level,
            team = team,
            x = x, y = y,
            unitId = id,
            maxHp = GetBaseHP(typeID) * mult,
            hp = GetBaseHP(typeID) * mult,
            damage = GetBaseDamage(typeID) * mult,
            range = GetRange(typeID),
            hasActedThisTurn = false
        };
    }

    // ==================== ВЕРСИЯ БЕЗ RL (БЫСТРАЯ) ====================
    public static float RunSimulation(string playerLayout, string enemyLayout)
    {
        var units = new List<SimUnit>();
        ParseAndSpawn(playerLayout, 0, units);
        ParseAndSpawn(enemyLayout, 1, units);

        int maxTurns = 100;
        int turn = 0;

        while (units.Any(u => u.team == 0 && u.hp > 0) && 
               units.Any(u => u.team == 1 && u.hp > 0) && 
               turn < maxTurns)
        {
            turn++;
            foreach (var u in units) u.hasActedThisTurn = false;
            
            var aliveUnits = units.Where(u => u.hp > 0).OrderBy(u => Guid.NewGuid()).ToList();
            
            foreach (var unit in aliveUnits)
            {
                if (unit.hp <= 0 || unit.hasActedThisTurn) continue;
                
                var enemies = units.Where(u => u.team != unit.team && u.hp > 0).ToList();
                if (!enemies.Any()) break;
                
                var target = enemies.OrderBy(e => Mathf.Abs(e.x - unit.x) + Mathf.Abs(e.y - unit.y)).First();
                int dist = Mathf.Abs(target.x - unit.x) + Mathf.Abs(target.y - unit.y);
                
                if (dist <= unit.range)
                {
                    target.hp -= unit.damage;
                    unit.hasActedThisTurn = true;
                }
                else
                {
                    if (unit.x != target.x) unit.x += Math.Sign(target.x - unit.x);
                    else if (unit.y != target.y) unit.y += Math.Sign(target.y - unit.y);
                    unit.hasActedThisTurn = true;
                }
            }
        }
        
        return CalculateFitness(units);
    }

    
    public static float RunSimulationWithRL(string playerLayout, string enemyLayout, 
                                        RLAgent rlAgentPlayer, 
                                        RLAgent rlAgentEnemy, 
                                        bool isTraining = true,
                                        System.Action<string, float, bool> onUnitDeath = null)
    {
        var units = new List<SimUnit>();
        ParseAndSpawn(playerLayout, 0, units);
        ParseAndSpawn(enemyLayout, 1, units);
        
        var totalRewards = new Dictionary<string, float>();
        foreach (var unit in units)
        {
            totalRewards[unit.unitId] = 0f;
        }

        int maxTurns = 100;
        int turn = 0;
        bool battleEnded = false;

        while (!battleEnded && turn < maxTurns)
        {
            turn++;
            foreach (var u in units) u.hasActedThisTurn = false;
            
            var aliveUnits = units.Where(u => u.hp > 0).OrderBy(u => Guid.NewGuid()).ToList();
            
            foreach (var unit in aliveUnits)
            {
                if (unit.hp <= 0 || unit.hasActedThisTurn) continue;
                
                var enemies = units.Where(u => u.team != unit.team && u.hp > 0).ToList();
                if (!enemies.Any()) 
                {
                    battleEnded = true;
                    break;
                }
                
                float[] state = GetUnitState(unit, enemies);
                var targetBefore = unit.FindNearestEnemy(units);
                float enemyHpBefore = targetBefore?.hp ?? 0;
                int distBeforeVal = targetBefore != null ? 
                    Mathf.Abs(targetBefore.x - unit.x) + Mathf.Abs(targetBefore.y - unit.y) : 999;
                bool couldAttackBefore = distBeforeVal <= unit.range;
                float hpBefore = unit.hp;
                int xBefore = unit.x, yBefore = unit.y;
                
                RLAgent selectedAgent = unit.team == 0 ? rlAgentPlayer : rlAgentEnemy;
                int action = selectedAgent.GetAction(state, unit.unitId, isTraining);
                
                ApplyAction(unit, action, enemies);
                
                var targetAfter = unit.FindNearestEnemy(units);
                if (targetAfter != null)
                {
                    int distAfterVal = Mathf.Abs(targetAfter.x - unit.x) + Mathf.Abs(targetAfter.y - unit.y);
                    if (distAfterVal <= unit.range)
                    {
                        targetAfter.hp -= unit.damage;
                        if (targetAfter.hp <= 0 && onUnitDeath != null)
                        {
                            onUnitDeath(targetAfter.unitId, totalRewards.GetValueOrDefault(targetAfter.unitId, 0), false);
                        }
                    }
                }
                
                float[] nextState = GetUnitState(unit, enemies);
                float enemyHpAfter = targetAfter?.hp ?? 0;
                int distAfterNew = targetAfter != null ? 
                    Mathf.Abs(targetAfter.x - unit.x) + Mathf.Abs(targetAfter.y - unit.y) : 999;
                bool canAttackNow = distAfterNew <= unit.range;
                bool isDead = unit.hp <= 0;
                
                float reward = CalculateReward(
                    hpBefore, unit.hp,
                    enemyHpBefore, enemyHpAfter,
                    couldAttackBefore, canAttackNow,
                    xBefore, yBefore, unit.x, unit.y,
                    unit.typeID
                );
                
                totalRewards[unit.unitId] += reward;
                
                selectedAgent.SendExperience(state, action, reward, nextState, isDead, unit.unitId);
                
                if (isDead && onUnitDeath != null)
                {
                    onUnitDeath(unit.unitId, totalRewards[unit.unitId], true);
                }
                
                unit.hasActedThisTurn = true;
            }
            
            bool noPlayers = !units.Any(u => u.team == 0 && u.hp > 0);
            bool noEnemies = !units.Any(u => u.team == 1 && u.hp > 0);
            battleEnded = noPlayers || noEnemies;
        }
        
        float fitness = CalculateFitness(units);
        float avgReward = totalRewards.Values.DefaultIfEmpty(0).Average();
        Debug.Log($"Бой завершён за {turn} ходов. Fitness: {fitness:F2}, Avg RL Reward: {avgReward:F2}");
        
        return fitness;
    }    // ==================== RL ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ====================
    
    private static float[] GetUnitState(SimUnit unit, List<SimUnit> allUnits)
    {
        float[] state = new float[27];
        if (unit == null) return state;
        
        // 0-1: Координаты (нормализованные)
        state[0] = unit.x / 4f;
        state[1] = unit.y / 9f;
        
        // 2: HP (нормализованное)
        state[2] = unit.hp / unit.maxHp;
        
        // 3: Уровень
        state[3] = unit.level / 3f;
        
        // 4: Тип юнита
        state[4] = unit.typeID / 3f;
        
        // 5-8: Ближайший враг
        var enemy = unit.FindNearestEnemy(allUnits);
        if (enemy != null)
        {
            state[5] = enemy.x / 4f;
            state[6] = enemy.y / 9f;
            state[7] = enemy.typeID / 3f;
            int dist = Mathf.Abs(enemy.x - unit.x) + Mathf.Abs(enemy.y - unit.y);
            state[8] = dist <= unit.range ? 1f : 0f;
        }
        else
        {
            state[5] = state[6] = state[7] = state[8] = -1f;
        }
        
        // 9-16: Соседние клетки (радиус 1)
        int[] dx1 = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int[] dy1 = { -1, 0, 1, -1, 1, -1, 0, 1 };
        for (int i = 0; i < 8 && i < dx1.Length; i++)
        {
            int nx = unit.x + dx1[i];
            int ny = unit.y + dy1[i];
            state[9 + i] = GetCellValue(nx, ny, unit, allUnits);
        }
        
        // 17-25: Дальние клетки (радиус атаки)
        int range = unit.range;
        int[] dxR = { -range, -range, -range, 0, 0, range, range, range };
        int[] dyR = { -range, 0, range, -range, range, -range, 0, range };
        for (int i = 0; i < 8 && i < dxR.Length; i++)
        {
            int nx = unit.x + dxR[i];
            int ny = unit.y + dyR[i];
            state[17 + i] = GetCellValue(nx, ny, unit, allUnits);
        }
        
        // 26: За укрытием?
        state[26] = IsBehindCover(unit, allUnits) ? 1f : 0f;
        
        return state;
    }
    
    private static float GetCellValue(int x, int y, SimUnit unit, List<SimUnit> allUnits)
    {
        if (x < 0 || x >= 5 || y < 0 || y >= 10) return -1f; // Стена
        
        var other = allUnits.FirstOrDefault(u => u.x == x && u.y == y && u.hp > 0);
        if (other == null) return 0f; // Пусто
        
        return other.team == unit.team ? 1f : 2f; // Свой или враг
    }
    
    private static bool IsBehindCover(SimUnit unit, List<SimUnit> allUnits)
    {
        if (unit.typeID == 1) return false; // Рыцари не используют укрытия
        
        var enemy = unit.FindNearestEnemy(allUnits);
        if (enemy == null) return false;
        
        int dx = Math.Sign(enemy.x - unit.x);
        int dy = Math.Sign(enemy.y - unit.y);
        
        // Проверяем, есть ли союзник между юнитом и врагом
        int checkX = unit.x + dx;
        int checkY = unit.y + dy;
        
        if (checkX < 0 || checkX >= 5 || checkY < 0 || checkY >= 10) return false;
        
        var blocker = allUnits.FirstOrDefault(u => u.x == checkX && u.y == checkY && u.team == unit.team);
        return blocker != null;
    }
    
    private static void ApplyAction(SimUnit unit, int action, List<SimUnit> allUnits)
    {
        int newX = unit.x;
        int newY = unit.y;
        
        switch (action)
        {
            case 1: newY = Mathf.Min(9, unit.y + 1); break;  // Вверх
            case 2: newY = Mathf.Max(0, unit.y - 1); break;  // Вниз
            case 3: newX = Mathf.Max(0, unit.x - 1); break;  // Влево
            case 4: newX = Mathf.Min(4, unit.x + 1); break;  // Вправо
            default: return; // Стоять
        }
        
        // Проверяем, свободна ли клетка
        bool isOccupied = allUnits.Any(u => u != unit && u.hp > 0 && u.x == newX && u.y == newY);
        if (!isOccupied)
        {
            unit.x = newX;
            unit.y = newY;
        }
    }
    
    private static float CalculateReward(
        float oldHp, float newHp,
        float oldEnemyHp, float newEnemyHp,
        bool couldAttackBefore, bool canAttackNow,
        int oldX, int oldY, int newX, int newY,
        int typeID)
    {
        float reward = -0.01f; // Маленький штраф за каждый шаг (стимул действовать быстро)
        
        // 1. Урон врагу (ГЛАВНОЕ)
        float damageDealt = oldEnemyHp - newEnemyHp;
        if (damageDealt > 0)
        {
            reward += damageDealt * 0.5f;  // +0.5 за каждую единицу урона
            if (newEnemyHp <= 0)
            {
                reward += 15f;  // Бонус за убийство
            }
        }
        
        // 2. Полученный урон
        float hpLost = oldHp - newHp;
        if (hpLost > 0)
        {
            reward -= hpLost * 0.3f;  // -0.3 за каждую единицу потерянного HP
            if (newHp <= 0)
            {
                reward -= 20f;  // Штраф за смерть
            }
        }
        
        // 3. Вход в радиус атаки (важно для ближнего боя)
        if (!couldAttackBefore && canAttackNow)
        {
            reward += 2f;
        }
        
        // 4. Приближение к врагу (для рыцарей)
        if (typeID == 1) // Knight
        {
            var enemy = FindNearestEnemyStatic(oldX, oldY, null, null);
            // Упрощённо: если приблизился - бонус
            int oldDist = Mathf.Abs(oldX - 2) + Mathf.Abs(oldY - 7); // Примерная позиция врага
            int newDist = Mathf.Abs(newX - 2) + Mathf.Abs(newY - 7);
            if (newDist < oldDist) reward += 0.1f;
        }
        
        // 5. Лечение (хилка - будет добавлено позже)
        if (newHp > oldHp && !canAttackNow)
        {
            reward += 5f;
        }
        
        return reward;
    }
    
    private static SimUnit FindNearestEnemyStatic(int x, int y, SimUnit unit, List<SimUnit> allUnits)
    {
        if (allUnits == null) return null;
        return allUnits
            .Where(u => u.team != unit?.team && u.hp > 0)
            .OrderBy(u => Mathf.Abs(u.x - x) + Mathf.Abs(u.y - y))
            .FirstOrDefault();
    }

    // ==================== ПАРСИНГ И ФИТНЕС ====================
    
    private static void ParseAndSpawn(string layout, int team, List<SimUnit> list)
    {
        if (string.IsNullOrEmpty(layout)) return;
        var parts = layout.Trim().Split(',');
        for (int i = 0; i < parts.Length && i < 50; i++)
        {
            if (string.IsNullOrEmpty(parts[i])) continue;
            var sub = parts[i].Split(':');
            if (!int.TryParse(sub[0], out int id) || id == 0) continue;
            
            int level = sub.Length > 1 && int.TryParse(sub[1], out int lvl) ? Mathf.Clamp(lvl, 1, 3) : 1;
            int x = i % 5;
            int y = (i / 5) + (team == 0 ? 0 : 5);
            
            if (x >= 0 && x < 5 && y >= 0 && y < 10)
                list.Add(CreateUnit(id, level, x, y, team));
        }
    }

    private static float CalculateFitness(List<SimUnit> units)
    {
        int killedPlayers = units.Count(u => u.team == 0 && u.hp <= 0);
        float killScore = killedPlayers * 15f;
        
        float totalPlayerHp = units.Where(u => u.team == 0).Sum(u => u.maxHp);
        float currentPlayerHp = units.Where(u => u.team == 0 && u.hp > 0).Sum(u => u.hp);
        float damageScore = totalPlayerHp > 0 ? ((totalPlayerHp - currentPlayerHp) / totalPlayerHp) * 12f : 0;
        
        int survivingEnemies = units.Count(u => u.team == 1 && u.hp > 0);
        float survivalScore = survivingEnemies * 0.5f;
        
        bool playerWiped = !units.Any(u => u.team == 0 && u.hp > 0);
        float wipeBonus = playerWiped ? 25f : 0f;
        
        if (survivingEnemies == 0) return damageScore * 0.5f - 10f;
        
        return killScore + damageScore + survivalScore + wipeBonus;
    }
    
    // Вспомогательный класс для хранения опыта юнита
    private class UnitExperience
    {
        public float[] lastState;
        public int lastAction;
        public float lastReward;
        public bool lastDone;
    }
}
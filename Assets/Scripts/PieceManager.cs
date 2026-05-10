using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PieceManager : MonoBehaviour
{
    public GameObject mPiecePrefab;

    public List<BasePiece> mMyMinis = new List<BasePiece>();
    public List<BasePiece> mEnemyMinis = new List<BasePiece>();

    private Board mBoard;

    [Header("Battle Settings")]
    public float turnDelay = 1.0f;
    private bool mBattleInProgress = false;

    public void Setup(Board board)
    {
        mBoard = board;

        // Игрок (белые)
        SpawnUnit(typeof(Knight), Color.white, new Color32(80, 124, 159, 255), new Vector2Int(2, 0));
        SpawnUnit(typeof(Archer), Color.white, new Color32(80, 200, 100, 255), new Vector2Int(1, 0));
        SpawnUnit(typeof(Mage), Color.white, new Color32(200, 80, 200, 255), new Vector2Int(3, 0));

        // Противник (чёрные)
        SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9));
        SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9));
        SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(4, 9));
    }

    public void SpawnUnit(Type unitType, Color teamColor, Color32 spriteColor, Vector2Int pos)
    {
        GameObject newPieceObject = Instantiate(mPiecePrefab);
        newPieceObject.transform.SetParent(transform);
        newPieceObject.transform.localScale = Vector3.one;

        BasePiece newPiece = (BasePiece)newPieceObject.AddComponent(unitType);
        newPiece.name = $"{unitType.Name}_{teamColor}";

        newPiece.Setup(teamColor, spriteColor, this);
        newPiece.Place(mBoard.mAllCells[pos.x, pos.y]);

        if (teamColor == Color.white)
            mMyMinis.Add(newPiece);
        else
            mEnemyMinis.Add(newPiece);
    }

    public void StartBattle()
    {
        if (!mBattleInProgress)
        {
            mBattleInProgress = true;
            StartCoroutine(BattleLoop());
        }
    }

    private IEnumerator BattleLoop()
    {
        Debug.Log("=== БОЙ НАЧАЛСЯ ===");

        while (mMyMinis.Count > 0 && mEnemyMinis.Count > 0)
        {
            // === ФАЗА 1: ВСЕ ВЫБИРАЮТ ЦЕЛИ ===
            List<BasePiece> allUnits = GetAliveUnits();

            // Словарь: кто кого атакует
            Dictionary<BasePiece, BasePiece> attacks = new Dictionary<BasePiece, BasePiece>();
            // Словарь: кто в какую клетку хочет пойти
            Dictionary<BasePiece, Cell> desiredMoves = new Dictionary<BasePiece, Cell>();

            foreach (BasePiece unit in allUnits)
            {
                BasePiece enemy = unit.FindNearestEnemy();
                if (enemy == null) continue;

                if (unit.CanAttackTarget(enemy))
                {
                    attacks[unit] = enemy;
                }
                else
                {
                    Cell nextCell = unit.GetCellTowardsTarget(enemy);
                    if (nextCell != null)
                    {
                        desiredMoves[unit] = nextCell;
                    }
                }
            }

            // === ФАЗА 2: ДВИЖЕНИЕ (с проверкой конфликтов) ===
            // Сортируем движения по расстоянию до цели (кто ближе — тот первый занимает клетку)
            List<KeyValuePair<BasePiece, Cell>> sortedMoves = new List<KeyValuePair<BasePiece, Cell>>(desiredMoves);
            sortedMoves.Sort((a, b) =>
            {
                BasePiece enemyA = a.Key.FindNearestEnemy();
                BasePiece enemyB = b.Key.FindNearestEnemy();
                if (enemyA == null || enemyB == null) return 0;

                float distA = Vector2Int.Distance(a.Key.mCurrentCell.mBoardPosition, enemyA.mCurrentCell.mBoardPosition);
                float distB = Vector2Int.Distance(b.Key.mCurrentCell.mBoardPosition, enemyB.mCurrentCell.mBoardPosition);
                return distA.CompareTo(distB);
            });

            // Множество занятых клеток (клетки, куда уже кто-то пошёл)
            HashSet<Vector2Int> reservedCells = new HashSet<Vector2Int>();

            foreach (var kvp in sortedMoves)
            {
                BasePiece unit = kvp.Key;
                Cell targetCell = kvp.Value;

                // Проверяем, что:
                // 1. Юнит ещё жив
                // 2. Клетка свободна И никто другой её ещё не занял в этой фазе
                if (unit != null && unit.gameObject.activeSelf &&
                    targetCell.mCurrentPiece == null &&
                    !reservedCells.Contains(targetCell.mBoardPosition))
                {
                    // Резервируем клетку
                    reservedCells.Add(targetCell.mBoardPosition);
                    unit.MoveToCell(targetCell);
                }
            }

            yield return new WaitForSeconds(0.3f);

            // === ФАЗА 3: АТАКА (с одновременным уроном) ===
            // Сначала собираем весь урон, потом применяем (чтобы атаки были истинно одновременными)
            Dictionary<BasePiece, int> damageToApply = new Dictionary<BasePiece, int>();

            foreach (var kvp in attacks)
            {
                BasePiece attacker = kvp.Key;
                BasePiece defender = kvp.Value;

                // Проверяем живучесть и дальность
                if (attacker != null && attacker.gameObject.activeSelf &&
                    defender != null && defender.gameObject.activeSelf &&
                    attacker.CanAttackTarget(defender))
                {
                    if (!damageToApply.ContainsKey(defender))
                        damageToApply[defender] = 0;

                    damageToApply[defender] += attacker.damage;
                    Debug.Log($"{attacker.name} (ID:{attacker.unitID}) атакует {defender.name} (ID:{defender.unitID}) на {attacker.damage} урона");
                }
            }

            // Применяем урон одновременно
            foreach (var kvp in damageToApply)
            {
                BasePiece defender = kvp.Key;
                int totalDamage = kvp.Value;

                if (defender != null && defender.gameObject.activeSelf)
                {
                    defender.TakeDamage(totalDamage);
                }
            }

            yield return new WaitForSeconds(turnDelay);
        }

        // Объявляем победителя
        if (mMyMinis.Count > 0)
            Debug.Log("=== ПОБЕДА ИГРОКА! ===");
        else if (mEnemyMinis.Count > 0)
            Debug.Log("=== ПОБЕДА ВРАГА! ===");
        else
            Debug.Log("=== НИЧЬЯ! ===");

        mBattleInProgress = false;
    }

    private List<BasePiece> GetAliveUnits()
    {
        List<BasePiece> allUnits = new List<BasePiece>();
        allUnits.AddRange(mMyMinis);
        allUnits.AddRange(mEnemyMinis);
        return allUnits;
    }

    public void SwitchSides(Color color)
    {
        Debug.Log("Action by: " + color);
    }

    public List<BasePiece> GetActiveUnits()
    {
        return GetAliveUnits();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

[System.Serializable]
public struct UnitSpawnCommand
{
    public int unitTypeID;
    public int x;
    public int y;
    public int team;
}

public class PieceManager : MonoBehaviour
{
    [Header("Unit Prefabs")]
    public BasePiece knightPrefab;
    public BasePiece archerPrefab;
    public BasePiece magePrefab;
    public GameObject mPiecePrefab;
    public List<BasePiece> mMyMinis = new List<BasePiece>();
    public List<BasePiece> mEnemyMinis = new List<BasePiece>();
    public Board mBoard;

    [Header("Shop Sprites")]
    public Sprite knightShopSprite;
    public Sprite archerShopSprite;
    public Sprite mageShopSprite;

    [Header("Battle Settings")]
    public float turnDelay = 1.0f;
    private bool mBattleInProgress = false;

    [Header("Elixir System")]
    public int maxElixir = 999;
    public int currentElixir;
    public TextMeshProUGUI elixirText;

    [Header("Best of 9")]
    public int playerWins = 0;
    public int enemyWins = 0;
    public int winsToWin = 5;
    public int currentRound = 1;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI scoreText;
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public TextMeshProUGUI finalScoreText1;
    public TextMeshProUGUI finalScoreText2;

    [Header("Draft")]
    public DraftManager draftManager;

    public static bool IsBattleActive { get; private set; }

    private List<SavedUnitData> savedPlayerUnits = new List<SavedUnitData>();

    [Header("Level Sprites")]
    public Sprite[] knightSprites; // 3 спрайта: Knight1, Knight2, Knight3
    public Sprite[] archerSprites; // 3 спрайта: Archer1, Archer2, Archer3
    public Sprite[] mageSprites;   // 3 спрайта: Mage1, Mage2, Mage3
    [Header("Attack Effects")]
    public GameObject arrowPrefab;
    public GameObject slashPrefab;
    public GameObject magicPrefab;
    public float projectileSpeed = 5f;

    public void PlayAttackEffect(BasePiece attacker, BasePiece defender)
    {
        GameObject effect = null;

        if (attacker is Knight)
            effect = Instantiate(slashPrefab, transform);
        else if (attacker is Archer)
            effect = Instantiate(arrowPrefab, transform);
        else if (attacker is Mage)
            effect = Instantiate(magicPrefab, transform);

        if (effect != null)
            StartCoroutine(FlyProjectile(effect, attacker.transform.position, defender.transform.position));
    }

    private IEnumerator FlyProjectile(GameObject projectile, Vector3 from, Vector3 to)
    {
        projectile.transform.position = from;
        float duration = 0.3f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            projectile.transform.position = Vector3.Lerp(from, to, t);

            // Поворачиваем снаряд в сторону цели
            Vector3 dir = (to - from).normalized;
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            projectile.transform.rotation = Quaternion.Euler(0, 0, angle);

            yield return null;
        }

        Destroy(projectile);
    }

    [Header("Enemy Level Sprites")]
    public Sprite[] enemyKnightSprites;
    public Sprite[] enemyArcherSprites;
    public Sprite[] enemyMageSprites;

    [System.Serializable]
    private class SavedUnitData
    {
        public string unitType;
        public Vector2Int position;
        public int currentHP;
        public int maxHP;
        public int level; // ← ДОБАВИТЬ УРОВЕНЬ
    }

    // Стоимость юнитов
    private Dictionary<System.Type, int> unitCosts = new Dictionary<System.Type, int>
    {
        { typeof(Knight), 3 },
        { typeof(Archer), 2 },
        { typeof(Mage), 4 }
    };

    public int GetUnitCost(System.Type type)
    {
        if (unitCosts.ContainsKey(type)) return unitCosts[type];
        return 0;
    }

    void Start()
    {
        currentElixir = maxElixir;
        UpdateElixirUI();
        UpdateScoreUI();
        SetupFirstRound();
    }

    public void Setup(Board board)
    {
        mBoard = board;
    }

    public void SetupFirstRound()
    {
        ClearAllUnits();
        savedPlayerUnits.Clear();
        SpawnEnemiesForRound(1);
        currentElixir = maxElixir;
        UpdateElixirUI();
        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;

        if (draftManager != null) draftManager.RefreshDraft();

        StartTimer(); // ← ЗАПУСК ТАЙМЕРА
    }

    public void SetupNextRound()
    {
        ClearAllUnits();
        RestorePlayerUnits();
        SpawnEnemiesForRound(currentRound);

        int elixirBonus = 4;
        currentElixir += elixirBonus;
        UpdateElixirUI();

        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;

        if (draftManager != null) draftManager.RefreshDraft();

        StartTimer(); // ← ЗАПУСК ТАЙМЕРА
    }

    public void StartTimer()
    {
        mTimer = autoBattleTime;
        mTimerActive = true;
        if (timerText != null) timerText.gameObject.SetActive(true);
    }

    public void StopTimer()
    {
        mTimerActive = false;
        if (timerText != null) timerText.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!mTimerActive) return;

        mTimer -= Time.deltaTime;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(mTimer);
            timerText.text = $"{seconds}";

            // Красный цвет если меньше 5 секунд
            if (seconds <= 5)
                timerText.color = Color.red;
            else
                timerText.color = Color.white;
        }

        if (mTimer <= 0f)
        {
            StopTimer();
            StartBattle();
        }
    }

    private void SavePlayerUnitsBeforeBattle()
    {
        savedPlayerUnits.Clear();
        foreach (var unit in mMyMinis)
        {
            if (unit != null && unit.gameObject.activeSelf && unit.mCurrentCell != null)
            {
                savedPlayerUnits.Add(new SavedUnitData
                {
                    unitType = unit.GetType().Name,
                    position = unit.mCurrentCell.mBoardPosition,
                    currentHP = unit.currentHP,
                    maxHP = unit.maxHP,
                    level = unit.level // ← СОХРАНЯЕМ УРОВЕНЬ
                });
            }
        }
        Debug.Log($"Сохранено перед боем: {savedPlayerUnits.Count} юнитов");
    }

    private void RestorePlayerUnits()
    {
        foreach (var data in savedPlayerUnits)
        {
            Type type = GetTypeByName(data.unitType);
            if (type != null && mBoard.mAllCells[data.position.x, data.position.y].mCurrentPiece == null)
            {
                BasePiece piece = SpawnUnit(type, Color.white, GetColorForType(data.unitType), data.position, true);
                if (piece != null)
                {
                    piece.currentHP = data.currentHP;
                    piece.maxHP = data.maxHP;
                    piece.level = data.level; // ← ВОССТАНАВЛИВАЕМ УРОВЕНЬ
                    piece.ApplyLevelStats();   // ← ПЕРЕСЧИТЫВАЕМ СТАТЫ
                    piece.UpdateLevelAppearance(); // ← ОБНОВЛЯЕМ ВИЗУАЛ
                }
            }
        }
        Debug.Log($"Восстановлено юнитов: {mMyMinis.Count}");
    }

    private Type GetTypeByName(string name)
    {
        switch (name)
        {
            case "Knight": return typeof(Knight);
            case "Archer": return typeof(Archer);
            case "Mage": return typeof(Mage);
            default: return null;
        }
    }

    private Color32 GetColorForType(string type)
    {
        switch (type)
        {
            case "Knight": return new Color32(80, 124, 159, 255);
            case "Archer": return new Color32(80, 200, 100, 255);
            case "Mage": return new Color32(200, 80, 200, 255);
            default: return new Color32(200, 200, 200, 255);
        }
    }

    private void SpawnEnemiesForRound(int round)
    {
        switch (round)
        {
            case 1:
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9), false);
                SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(4, 9), false);
                break;
            case 2:
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9), false);
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(1, 8), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9), false);
                SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(4, 9), false);
                break;
            case 3:
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9), false);
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(1, 8), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(0, 9), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(3, 9), false);
                SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(4, 9), false);
                break;
            default:
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(2, 9), false);
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(0, 8), false);
                SpawnUnit(typeof(Knight), Color.black, new Color32(210, 95, 64, 255), new Vector2Int(4, 8), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(1, 9), false);
                SpawnUnit(typeof(Archer), Color.black, new Color32(200, 50, 50, 255), new Vector2Int(3, 9), false);
                SpawnUnit(typeof(Mage), Color.black, new Color32(180, 50, 180, 255), new Vector2Int(2, 8), false);
                break;
        }
    }

    private void ClearAllUnits()
    {
        foreach (var unit in mMyMinis.ToArray())
            if (unit != null && unit.gameObject != null) Destroy(unit.gameObject);
        foreach (var unit in mEnemyMinis.ToArray())
            if (unit != null && unit.gameObject != null) Destroy(unit.gameObject);
        mMyMinis.Clear();
        mEnemyMinis.Clear();

        if (mBoard != null)
            for (int x = 0; x < 5; x++)
                for (int y = 0; y < 10; y++)
                    if (mBoard.mAllCells[x, y] != null)
                        mBoard.mAllCells[x, y].mCurrentPiece = null;
    }

    public bool CanAfford(int cost) => currentElixir >= cost;

    public bool SpendElixir(int cost)
    {
        if (CanAfford(cost))
        {
            currentElixir -= cost;
            UpdateElixirUI();
            return true;
        }
        return false;
    }

    public void RefundElixir(int cost)
    {
        currentElixir += cost;
        UpdateElixirUI();
    }

    private void UpdateElixirUI()
    {
        if (elixirText != null)
            elixirText.text = $"{currentElixir}";
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null) scoreText.text = $"Вы {playerWins} - {enemyWins} Враг";
        if (roundText != null) roundText.text = $"Раунд {currentRound}/9";
    }
    [Header("Auto Battle Timer")]
    public float autoBattleTime = 20f;
    private float mTimer;
    public TextMeshProUGUI timerText;
    private bool mTimerActive = false;
    public void PurchaseUnit(System.Type unitType)
    {
        if (BasePiece.sBattleStarted) return;

        int cost = GetUnitCost(unitType);
        if (!SpendElixir(cost))
        {
            Debug.Log("Недостаточно эликсира!");
            return;
        }

        Cell freeCell = FindFreeCell(true);
        if (freeCell == null)
        {
            Debug.Log("Нет свободных клеток!");
            RefundElixir(cost);
            return;
        }

        SpawnUnit(unitType, Color.white, GetUnitColor(unitType), freeCell.mBoardPosition, true);
    }

    private Cell FindFreeCell(bool isPlayer)
    {
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < (isPlayer ? 5 : 10); y++)
            {
                if (isPlayer && y >= 5) continue;
                if (!isPlayer && y < 5) continue;
                if (mBoard.mAllCells[x, y].mCurrentPiece == null)
                    return mBoard.mAllCells[x, y];
            }
        }
        return null;
    }

    private Color32 GetUnitColor(System.Type type)
    {
        if (type == typeof(Knight)) return new Color32(80, 124, 159, 255);
        if (type == typeof(Archer)) return new Color32(80, 200, 100, 255);
        return new Color32(200, 80, 200, 255);
    }

    public void StartBattle()
    {
        if (mBattleInProgress) return;
        StopTimer(); // ← ОСТАНАВЛИВАЕМ ТАЙМЕР
        SavePlayerUnitsBeforeBattle();
        mBattleInProgress = true;
        IsBattleActive = true;
        BasePiece.DisableAllDrag();
        if (draftManager != null) draftManager.Hide();
        StartCoroutine(BattleLoop());
    }

    private IEnumerator BattleLoop()
    {
        int maxRounds = 50;
        int roundCount = 0;

        while (mMyMinis.Count > 0 && mEnemyMinis.Count > 0 && roundCount < maxRounds)
        {
            roundCount++;
            CleanDeadUnits();
            List<BasePiece> allUnits = GetAliveUnits();

            Dictionary<BasePiece, BasePiece> attacks = new Dictionary<BasePiece, BasePiece>();
            Dictionary<BasePiece, Cell> desiredMoves = new Dictionary<BasePiece, Cell>();

            foreach (BasePiece unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeSelf) continue;
                BasePiece enemy = unit.FindNearestEnemy();
                if (enemy == null || !enemy.gameObject.activeSelf) continue;

                if (unit.CanAttackTarget(enemy))
                    attacks[unit] = enemy;
                else
                {
                    Cell nextCell = unit.GetCellTowardsTarget(enemy);
                    if (nextCell != null && nextCell.mCurrentPiece == null)
                        desiredMoves[unit] = nextCell;
                }
            }

            foreach (var kvp in desiredMoves)
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf && kvp.Value.mCurrentPiece == null)
                    kvp.Key.MoveToCell(kvp.Value);

            yield return new WaitForSeconds(0.2f);

            foreach (var kvp in attacks)
            {
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf &&
                    kvp.Value != null && kvp.Value.gameObject.activeSelf &&
                    kvp.Key.CanAttackTarget(kvp.Value))
                {
                    // ЭФФЕКТ АТАКИ
                    PlayAttackEffect(kvp.Key, kvp.Value);

                    kvp.Key.AttackTarget(kvp.Value);
                }
            }

            yield return new WaitForSeconds(turnDelay);
        }

        if (mMyMinis.Count > 0 && mEnemyMinis.Count == 0) playerWins++;
        else if (mEnemyMinis.Count > 0 && mMyMinis.Count == 0) enemyWins++;

        UpdateScoreUI();
        mBattleInProgress = false;
        IsBattleActive = false;

        if (playerWins >= winsToWin || enemyWins >= winsToWin)
        {
            EndSeries();
            yield break;
        }

        currentRound++;
        yield return new WaitForSeconds(2f);
        SetupNextRound();
    }

    private void EndSeries()
    {
        if (playerWins >= winsToWin)
        {
            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (finalScoreText1 != null) finalScoreText1.text = $"{playerWins} - {enemyWins}";
        }
        else
        {
            if (defeatPanel != null) defeatPanel.SetActive(true);
            if (finalScoreText1 != null) finalScoreText1.text = $"{playerWins} - {enemyWins}";
        }
        if (playerWins >= winsToWin)
        {
            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (finalScoreText2 != null) finalScoreText2.text = $"{playerWins} - {enemyWins}";
        }
        else
        {
            if (defeatPanel != null) defeatPanel.SetActive(true);
            if (finalScoreText2 != null) finalScoreText2.text = $"{playerWins} - {enemyWins}";
        }
        IsBattleActive = true;
        BasePiece.sBattleStarted = true;
    }

    public void RestartSeries()
    {
        playerWins = 0;
        enemyWins = 0;
        currentRound = 1;
        UpdateScoreUI();
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        SetupFirstRound();
    }

    [Header("Attack Sprites")]
    public Sprite[] knightAttackSprites;
    public Sprite[] archerAttackSprites;
    public Sprite[] mageAttackSprites;
    public Sprite[] enemyKnightAttackSprites;
    public Sprite[] enemyArcherAttackSprites;
    public Sprite[] enemyMageAttackSprites;

    public BasePiece SpawnUnit(Type unitType, Color teamColor, Color32 spriteColor, Vector2Int pos, bool isPlayer)
    {
        Cell targetCell = mBoard.mAllCells[pos.x, pos.y];

        BasePiece template = null;
        if (unitType == typeof(Knight)) template = knightPrefab;
        else if (unitType == typeof(Archer)) template = archerPrefab;
        else if (unitType == typeof(Mage)) template = magePrefab;

        if (template == null)
        {
            Debug.LogError($"Префаб для {unitType.Name} не назначен!");
            return null;
        }

        GameObject newPieceObject = Instantiate(mPiecePrefab, transform);
        newPieceObject.transform.localScale = Vector3.one;
        newPieceObject.name = $"{unitType.Name}_{(isPlayer ? "Player" : "Enemy")}";

        BasePiece newPiece = (BasePiece)newPieceObject.AddComponent(unitType);
        if (unitType == typeof(Knight))
            newPiece.levelSprites = knightSprites;
        else if (unitType == typeof(Archer))
            newPiece.levelSprites = archerSprites;
        else if (unitType == typeof(Mage))
            newPiece.levelSprites = mageSprites;
        newPiece.maxHP = template.maxHP;
        newPiece.currentHP = template.maxHP;
        newPiece.damage = template.damage;
        newPiece.attackRange = template.attackRange;
        newPiece.attackSpeed = template.attackSpeed;
        newPiece.unitID = template.unitID;
        newPiece.cost = GetUnitCost(unitType);

        if (isPlayer)
        {
            if (unitType == typeof(Knight)) newPiece.levelSprites = knightSprites;
            else if (unitType == typeof(Archer)) newPiece.levelSprites = archerSprites;
            else newPiece.levelSprites = mageSprites;
        }
        else
        {
            if (unitType == typeof(Knight)) newPiece.levelSprites = enemyKnightSprites;
            else if (unitType == typeof(Archer)) newPiece.levelSprites = enemyArcherSprites;
            else newPiece.levelSprites = enemyMageSprites;
        }
        if (isPlayer)
        {
            if (unitType == typeof(Knight)) newPiece.attackSprites = knightAttackSprites;
            else if (unitType == typeof(Archer)) newPiece.attackSprites = archerAttackSprites;
            else newPiece.attackSprites = mageAttackSprites;
        }
        else
        {
            if (unitType == typeof(Knight)) newPiece.attackSprites = enemyKnightAttackSprites;
            else if (unitType == typeof(Archer)) newPiece.attackSprites = enemyArcherAttackSprites;
            else newPiece.attackSprites = enemyMageAttackSprites;
        }

        newPiece.Setup(isPlayer, teamColor, spriteColor, this);

        // Убираем наложение цвета — спрайт уже цветной
        Image img = newPiece.GetComponent<Image>();
        if (img != null)
            img.color = Color.white; // Белый = без перекрашивания

        // ВАЖНО: привязываем к клетке
        newPiece.Place(targetCell);

        if (isPlayer) mMyMinis.Add(newPiece);
        else mEnemyMinis.Add(newPiece);
        return newPiece;
    }

    private void CleanDeadUnits()
    {
        mMyMinis.RemoveAll(u => u == null || !u.gameObject.activeSelf);
        mEnemyMinis.RemoveAll(u => u == null || !u.gameObject.activeSelf);
    }

    private List<BasePiece> GetAliveUnits()
    {
        List<BasePiece> all = new List<BasePiece>();
        all.AddRange(mMyMinis.Where(u => u != null && u.gameObject.activeSelf));
        all.AddRange(mEnemyMinis.Where(u => u != null && u.gameObject.activeSelf));
        return all;
    }

    public void SpawnEnemyLayout(string data)
    {
        ClearEnemies();
        string[] values = data.Split(',');

        for (int i = 0; i < values.Length; i++)
        {
            if (i >= 25) break;
            int unitType = int.Parse(values[i]);
            if (unitType == 0) continue;
            int x = i % 5;
            int y = (i / 5) + 5;
            Type t = GetTypeByID(unitType);
            if (t == null) continue;
            Color32 col = GetColorByUnitType(unitType);
            if (mBoard.mAllCells[x, y].mCurrentPiece == null)
                SpawnUnit(t, Color.black, col, new Vector2Int(x, y), false);
        }
    }

    public void ClearEnemies()
    {
        foreach (BasePiece unit in mEnemyMinis)
        {
            if (unit != null)
            {
                if (unit.mCurrentCell != null)
                    unit.mCurrentCell.mCurrentPiece = null;
                Destroy(unit.gameObject);
            }
        }
        mEnemyMinis.Clear();
    }

    private Type GetTypeByID(int id)
    {
        if (id == 1) return typeof(Knight);
        if (id == 2) return typeof(Archer);
        if (id == 3) return typeof(Mage);
        return null;
    }

    private Color32 GetColorByUnitType(int id)
    {
        if (id == 1) return new Color32(210, 95, 64, 255);
        if (id == 2) return new Color32(200, 50, 50, 255);
        if (id == 3) return new Color32(180, 50, 180, 255);
        return Color.white;
    }
    public void PurchaseUnitAtCell(System.Type unitType, Cell cell)
    {
        if (BasePiece.sBattleStarted) return;

        int cost = GetUnitCost(unitType);
        if (!SpendElixir(cost))
        {
            Debug.Log("Недостаточно эликсира!");
            return;
        }

        SpawnUnit(unitType, Color.white, GetUnitColor(unitType), cell.mBoardPosition, true);
    }

    // Слияние двух одинаковых юнитов
    public void FuseUnits(BasePiece target, BasePiece dragged)
    {
        if (target.GetType() != dragged.GetType()) return;
        if (target.level != dragged.level) return;
        if (target.level >= target.maxLevel) return;

        Debug.Log($"⭐ СЛИЯНИЕ: {dragged.name} → {target.name} → уровень {target.level + 1}!");

        // Эффект слияния
        StartCoroutine(FusionEffect(target, dragged));
    }

    private IEnumerator FusionEffect(BasePiece target, BasePiece dragged)
    {
        Image targetImg = target.GetComponent<Image>();
        Image draggedImg = dragged.GetComponent<Image>();

        // Мигание 3 раза
        for (int i = 0; i < 3; i++)
        {
            if (targetImg != null) targetImg.color = Color.yellow;
            if (draggedImg != null) draggedImg.color = Color.yellow;
            yield return new WaitForSeconds(0.1f);

            if (targetImg != null) targetImg.color = Color.white;
            if (draggedImg != null) draggedImg.color = Color.white;
            yield return new WaitForSeconds(0.1f);
        }

        // Улучшаем цель
        target.level++;
        target.ApplyLevelStats();
        target.UpdateLevelAppearance();

        // Удаляем перетащенного
        if (dragged.mCurrentCell != null)
            dragged.mCurrentCell.mCurrentPiece = null;
        mMyMinis.Remove(dragged);
        Destroy(dragged.gameObject);

        // Финальная вспышка
        if (targetImg != null)
        {
            targetImg.color = new Color(1f, 0.8f, 0f);
            yield return new WaitForSeconds(0.2f);
            targetImg.color = Color.white;
        }

        // ЯВНО ОБНОВЛЯЕМ HEALTHBAR (звёзды)
        target.RefreshHealthBar();

        Debug.Log($"Слияние завершено! {target.name}: HP={target.maxHP}, Урон={target.damage}, Уровень={target.level}");
    }

    // Авто-слияние после покупки
    public void CheckFusion()
    {
        var groups = new Dictionary<string, List<BasePiece>>();

        foreach (var unit in mMyMinis)
        {
            if (unit == null || !unit.gameObject.activeSelf) continue;
            if (unit.level >= unit.maxLevel) continue;

            string key = $"{unit.GetType().Name}_{unit.level}";
            if (!groups.ContainsKey(key))
                groups[key] = new List<BasePiece>();
            groups[key].Add(unit);
        }

        foreach (var kvp in groups)
        {
            if (kvp.Value.Count >= 2)
            {
                FuseUnits(kvp.Value[0], kvp.Value[1]);
                CheckFusion(); // рекурсивно проверяем снова
                break;
            }
        }
    }
}
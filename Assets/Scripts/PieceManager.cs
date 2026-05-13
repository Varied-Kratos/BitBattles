using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

[System.Serializable]
public struct UnitSpawnCommand {
    public int unitTypeID; 
    public int x;
    public int y;
    public int team; // 0 для тебя, 1 для противника
}

public class PieceManager : MonoBehaviour
{
    public GameObject mPiecePrefab;
    public List<BasePiece> mMyMinis = new List<BasePiece>();
    public List<BasePiece> mEnemyMinis = new List<BasePiece>();
    public Board mBoard;

    [Header("Battle Settings")]
    public float turnDelay = 1.0f;
    private bool mBattleInProgress = false;

    [Header("Elixir System")]
    public int maxElixir = 10;
    public int currentElixir;
    public TextMeshProUGUI elixirText;

    [Header("Best of 5")]
    public int playerWins = 0;
    public int enemyWins = 0;
    public int winsToWin = 3;
    public int currentRound = 1;
    public TextMeshProUGUI roundText;
    public TextMeshProUGUI scoreText;
    public GameObject victoryPanel;
    public GameObject defeatPanel;
    public TextMeshProUGUI finalScoreText;

    public static bool IsBattleActive { get; private set; }

    // Сохраняем позиции выживших юнитов игрока
    private List<SavedUnitData> savedPlayerUnits = new List<SavedUnitData>();

    [System.Serializable]
    private class SavedUnitData
    {
        public string unitType;
        public Vector2Int position;
        public int currentHP;
        public int maxHP;
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

    // Первый раунд — чистый старт
    public void SetupFirstRound()
    {
        ClearAllUnits();
        savedPlayerUnits.Clear();

        // Базовые враги
        SpawnEnemiesForRound(1);

        currentElixir = maxElixir;
        UpdateElixirUI();

        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;
    }

    // Новый раунд — сохраняем игрока, спавним новых врагов
    // Новый метод: сохранить всех текущих игроков перед боем
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
                    maxHP = unit.maxHP
                });
            }
        }
        Debug.Log($"Сохранено перед боем: {savedPlayerUnits.Count} юнитов");
    }

    public void SetupNextRound()
    {
        // Очищаем поле
        ClearAllUnits();

        // Восстанавливаем сохранённых юнитов
        RestorePlayerUnits();

        // Спавним врагов
        SpawnEnemiesForRound(currentRound);

        // Пополняем эликсир
        int elixirBonus = 4;
        currentElixir += elixirBonus;
        if (currentElixir > maxElixir) currentElixir = maxElixir;
        UpdateElixirUI();

        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;
    }

    // Перед стартом боя сохраняем юнитов
    public void StartBattle()
    {
        if (mBattleInProgress) return;

        // СОХРАНЯЕМ перед боем!
        SavePlayerUnitsBeforeBattle();

        mBattleInProgress = true;
        IsBattleActive = true;
        BasePiece.DisableAllDrag();
        StartCoroutine(BattleLoop());
    }

    private void SavePlayerUnits()
    {
        savedPlayerUnits.Clear();
        foreach (var unit in mMyMinis)
        {
            if (unit != null && unit.gameObject.activeSelf)
            {
                savedPlayerUnits.Add(new SavedUnitData
                {
                    unitType = unit.GetType().Name,
                    position = unit.mCurrentCell.mBoardPosition,
                    currentHP = unit.currentHP,
                    maxHP = unit.maxHP
                });
            }
        }
        Debug.Log($"Сохранено {savedPlayerUnits.Count} юнитов игрока");
    }

    private void RestorePlayerUnits()
    {
        foreach (var data in savedPlayerUnits)
        {
            Type type = null;
            switch (data.unitType)
            {
                case "Knight": type = typeof(Knight); break;
                case "Archer": type = typeof(Archer); break;
                case "Mage": type = typeof(Mage); break;
            }

            if (type != null)
            {
                // Проверяем, что клетка свободна
                if (mBoard.mAllCells[data.position.x, data.position.y].mCurrentPiece == null)
                {
                    SpawnUnit(type, Color.white, GetColorForType(data.unitType), data.position, true);

                    // Восстанавливаем HP
                    BasePiece piece = mMyMinis[mMyMinis.Count - 1];
                    piece.currentHP = data.currentHP;
                }
            }
        }
        Debug.Log($"Восстановлено {mMyMinis.Count} юнитов игрока");
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
        // С каждым раундом врагов больше
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
            default: // 4 и 5 раунды
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
        if (currentElixir > maxElixir) currentElixir = maxElixir;
        UpdateElixirUI();
    }

    private void UpdateElixirUI()
    {
        if (elixirText != null)
            elixirText.text = $"💧 {currentElixir}/{maxElixir}";
    }

    private void UpdateScoreUI()
    {
        if (scoreText != null)
            scoreText.text = $"Игрок {playerWins} - {enemyWins} Враг";
        if (roundText != null)
            roundText.text = $"Раунд {currentRound}/5";
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
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf && kvp.Value != null && kvp.Value.gameObject.activeSelf && kvp.Key.CanAttackTarget(kvp.Value))
                    kvp.Key.AttackTarget(kvp.Value);

            yield return new WaitForSeconds(turnDelay);
        }

        // Определяем победителя раунда
        if (mMyMinis.Count > 0 && mEnemyMinis.Count == 0)
            playerWins++;
        else if (mEnemyMinis.Count > 0 && mMyMinis.Count == 0)
            enemyWins++;

        Debug.Log($"Раунд завершён! Игрок {playerWins} - {enemyWins} Враг");
        UpdateScoreUI();
        mBattleInProgress = false;
        IsBattleActive = false;

        // ПРОВЕРКА НА КОНЕЦ СЕРИИ
        if (playerWins >= winsToWin || enemyWins >= winsToWin)
        {
            Debug.Log($"СЕРИЯ ЗАВЕРШЕНА! Игрок {playerWins} - {enemyWins} Враг");
            EndSeries();
            yield break; // ВЫХОДИМ из корутины полностью
        }

        // Иначе готовим следующий раунд
        currentRound++;
        yield return new WaitForSeconds(2f);
        UpdateScoreUI();
        SetupNextRound();
    }

    private void EndSeries()
    {
        Debug.Log($"EndSeries вызван! playerWins={playerWins}, enemyWins={enemyWins}");

        if (playerWins >= winsToWin)
        {
            Debug.Log("ПОБЕДА ИГРОКА!");
            if (victoryPanel != null) victoryPanel.SetActive(true);
            if (finalScoreText != null) finalScoreText.text = $"{playerWins} - {enemyWins}";
        }
        else
        {
            Debug.Log("ПОРАЖЕНИЕ!");
            if (defeatPanel != null) defeatPanel.SetActive(true);
            if (finalScoreText != null) finalScoreText.text = $"{playerWins} - {enemyWins}";
        }

        // Блокируем кнопку "В бой"
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

    public void SpawnUnit(Type unitType, Color teamColor, Color32 spriteColor, Vector2Int pos, bool isPlayer)
    {
        GameObject newPieceObject = Instantiate(mPiecePrefab);
        newPieceObject.transform.SetParent(transform, false);
        newPieceObject.transform.localScale = Vector3.one;

        CanvasRenderer cr = newPieceObject.GetComponent<CanvasRenderer>();
        if (cr != null) cr.cullTransparentMesh = false;

        BasePiece newPiece = (BasePiece)newPieceObject.AddComponent(unitType);
        newPiece.name = $"{unitType.Name}_{(isPlayer ? "Player" : "Enemy")}";
        newPiece.Setup(teamColor, spriteColor, this);
        newPiece.Place(mBoard.mAllCells[pos.x, pos.y]);

        if (isPlayer) mMyMinis.Add(newPiece);
        else mEnemyMinis.Add(newPiece);
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

    public void ClearBoard() {
        // Чистим всех юнитов на доске
        foreach (var cell in mBoard.mAllCells) {
            if (cell.mCurrentPiece != null) {
                Destroy(cell.mCurrentPiece.gameObject);
                cell.mCurrentPiece = null;
            }
        }
        // Обнуляем списки
        mMyMinis.Clear();
        mEnemyMinis.Clear();
    }
}
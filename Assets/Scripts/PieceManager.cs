using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Random = UnityEngine.Random;

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
    public PythonConnector pythonConnector;

    [Header("Shop Sprites")]
    public Sprite knightShopSprite;
    public Sprite archerShopSprite;
    public Sprite mageShopSprite;

    [Header("Obstacles & Heals")]
    public GameObject stonePrefab;
    public GameObject healPrefab;
    public HashSet<Vector2Int> blockedCells = new HashSet<Vector2Int>();
    public HashSet<Vector2Int> healCells = new HashSet<Vector2Int>();
    public List<GameObject> spawnedObjects = new List<GameObject>();

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

    private void SpawnObstaclesAndHeals(int round)
    {
        ClearObstaclesAndHeals();

        int maxCount = Mathf.Min(round, 3);
        int stoneCount = Random.Range(0, maxCount + 1);
        int healCount = Random.Range(0, maxCount + 1);

        // Соберём все свободные клетки (без юнитов, не заблокированные)
        List<Vector2Int> freeCells = new List<Vector2Int>();
        for (int x = 0; x < 5; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                if (mBoard.mAllCells[x, y].mCurrentPiece == null)
                    freeCells.Add(new Vector2Int(x, y));
            }
        }

        // Камни
        for (int i = 0; i < stoneCount; i++)
        {
            if (freeCells.Count == 0) break;
            int idx = Random.Range(0, freeCells.Count);
            Vector2Int pos = freeCells[idx];
            freeCells.RemoveAt(idx);

            blockedCells.Add(pos);
            GameObject stone = Instantiate(stonePrefab, mBoard.mAllCells[pos.x, pos.y].transform);
            stone.transform.localPosition = Vector3.zero;
            spawnedObjects.Add(stone);
        }

        // Хилки
        for (int i = 0; i < healCount; i++)
        {
            if (freeCells.Count == 0) break;
            int idx = Random.Range(0, freeCells.Count);
            Vector2Int pos = freeCells[idx];
            freeCells.RemoveAt(idx);

            healCells.Add(pos);
            GameObject heal = Instantiate(healPrefab, mBoard.mAllCells[pos.x, pos.y].transform);
            heal.transform.localPosition = Vector3.zero;
            spawnedObjects.Add(heal);
        }
    }

    private void ClearObstaclesAndHeals()
    {
        foreach (var obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();
        blockedCells.Clear();
        healCells.Clear();
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

        // БОЕВАЯ МУЗЫКА СРАЗУ
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic();
    }

    public void Setup(Board board)
    {
        mBoard = board;
    }

    public void SetupFirstRound()
    {
        ClearAllUnits();
        savedPlayerUnits.Clear();

        if (pythonConnector != null) {
            pythonConnector.RequestNextLayout(); 
        } else {
            Debug.LogError("Ссылка на PythonConnector не назначена в инспекторе!");
        }

        currentElixir = maxElixir;
        UpdateElixirUI();
        SpawnObstaclesAndHeals(currentRound);
        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;

        if (draftManager != null) draftManager.RefreshDraft();

        StartTimer(); // ← ЗАПУСК ТАЙМЕРА
    }

    public void SetupNextRound(float fitness = 0)
    {
        // 1. Полная очистка поля от старых юнитов
        ClearAllUnits();

        // 2. Восстановление твоих (игрока) выживших юнитов
        RestorePlayerUnits();

        // 3. Запрос новой расстановки у ИИ через Python
        if (pythonConnector != null) 
        {
            // Передаем накопленный фитнес (результат боя) в коннектор
            pythonConnector.RequestNextLayout(fitness); 
        } 
        else 
        {
            Debug.LogError("Ссылка на PythonConnector не назначена в инспекторе!");
        }

        // 4. Экономика: начисление бонуса эликсира за новый раунд
        int elixirBonus = 4;
        currentElixir += elixirBonus;
        UpdateElixirUI();

        // 5. Сброс игровых состояний для подготовки к расстановке
        BasePiece.sBattleStarted = false;
        IsBattleActive = false;
        mBattleInProgress = false;
        SpawnObstaclesAndHeals(currentRound);
        // 6. Обновление магазина юнитов (драфта)
        if (draftManager != null) 
        {
            draftManager.RefreshDraft();
        }

        // 7. Запуск таймера фазы подготовки
        StartTimer(); 
    }

    public void StartTimer()
    {
        mTimer = autoBattleTime;
        mTimerActive = true;
        mTimerSoundPlayed = false; // ← СБРАСЫВАЕМ ФЛАГ
        if (timerPanel != null)
            timerPanel.SetActive(true);
    }

    public void StopTimer()
    {
        mTimerActive = false;
        if (timerPanel != null)
            timerPanel.SetActive(false);
    }
    private bool mTimerSoundPlayed = false; // чтобы не спамить звук каждый кадр
    private void Update()
    {
        if (!mTimerActive) return;

        mTimer -= Time.deltaTime;

        if (timerText != null)
        {
            int seconds = Mathf.CeilToInt(mTimer);
            timerText.text = $"{seconds}";

            if (seconds <= 5)
            {
                timerText.color = Color.red;

                if (!mTimerSoundPlayed && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayTimerTick();
                    mTimerSoundPlayed = true;
                }
            }
            else
            {
                timerText.color = Color.white;
            }
        }

        // ВОТ ЗДЕСЬ — это конец метода Update:
        if (mTimer <= 0f)
        {
            StopTimer();
            AudioManager.Instance?.StopTimerTick(); // ← ДОБАВЬ ЭТУ СТРОКУ
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
                    // Set saved values directly - DON'T call ApplyLevelStats again
                    piece.level = data.level;
                    piece.maxHP = data.maxHP;
                    piece.currentHP = data.currentHP;
                    piece.damage = Mathf.RoundToInt(piece.damage * GetLevelMultiplier(data.level));
                    piece.UpdateLevelAppearance();
                    piece.RefreshHealthBar();
                }
            }
        }
        Debug.Log($"Восстановлено юнитов: {mMyMinis.Count}");
    }

    // Add this helper method to BasePiece
    public float GetLevelMultiplier(int lvl)
    {
        switch (lvl)
        {
            case 1: return 1f;
            case 2: return 2f;
            case 3: return 4.5f;
            default: return 1f;
        }
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
        ClearObstaclesAndHeals();
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

        AudioManager.Instance?.PlayBuy(); // ← ДОБАВИТЬ ЗВУК
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

        StopTimer();

        // Останавливаем звук таймера
        AudioManager.Instance?.StopTimerTick();

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

            // Будем хранить не сам объект врага (который может умереть), 
            // а позицию, куда должна прилететь стрела/магия в случае смерти
            Dictionary<BasePiece, BasePiece> attacks = new Dictionary<BasePiece, BasePiece>();
            Dictionary<BasePiece, Vector3> deathTargetPositions = new Dictionary<BasePiece, Vector3>();
            Dictionary<BasePiece, Cell> desiredMoves = new Dictionary<BasePiece, Cell>();

            foreach (BasePiece unit in allUnits)
            {
                if (unit == null || !unit.gameObject.activeSelf) continue;

                if (RLManager.Instance != null && RLManager.Instance.useRL)
                {
                    BasePiece targetEnemy = unit.FindNearestEnemy();
                    float enemyHPBefore = (targetEnemy != null) ? targetEnemy.currentHP : 0f;
                    Vector3 lastEnemyPos = (targetEnemy != null) ? targetEnemy.transform.position : Vector3.zero;

                    // Ход ИИ (урон наносится тут же внутри)
                    RLManager.Instance.RLTurn(unit);

                    // Если урон прошел
                    if (targetEnemy != null && targetEnemy.currentHP < enemyHPBefore)
                    {
                        if (targetEnemy.gameObject.activeSelf && targetEnemy.currentHP > 0)
                        {
                            attacks[unit] = targetEnemy;
                        }
                        else
                        {
                            // Враг погиб от этого удара — запоминаем его координаты, чтобы стрела не выдала ошибку
                            deathTargetPositions[unit] = lastEnemyPos;
                        }
                    }
                }
                else
                {
                    // Старое поведение
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
            }

            // Отработка перемещений для ручного режима
            foreach (var kvp in desiredMoves)
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf && kvp.Value.mCurrentPiece == null)
                    kvp.Key.MoveToCell(kvp.Value);

            yield return new WaitForSeconds(0.2f);

            // 1. Визуализируем атаки по живым целям
            foreach (var kvp in attacks)
            {
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf &&
                    kvp.Value != null && kvp.Value.gameObject.activeSelf)
                {
                    PlayAttackEffect(kvp.Key, kvp.Value);
                    
                    if (RLManager.Instance == null || !RLManager.Instance.useRL)
                    {
                        kvp.Key.AttackTarget(kvp.Value);
                    }
                }
            }

            // 2. Визуализируем фатальные атаки (для RL, если врага испарило мгновенным уроном)
            foreach (var kvp in deathTargetPositions)
            {
                if (kvp.Key != null && kvp.Key.gameObject.activeSelf)
                {
                    // Спавним временную пустышку на месте гибели, чтобы FlyProjectile не упал с ошибкой
                    GameObject dummyTarget = new GameObject("DummyTarget");
                    dummyTarget.transform.position = kvp.Value;
                    
                    PlayAttackEffect(kvp.Key, dummyTarget.AddComponent<Knight>()); // Тип не важен, нужен лишь transform
                    Destroy(dummyTarget, 0.4f); // Удаляем пустышку после долета снаряда
                }
            }

            yield return new WaitForSeconds(turnDelay);
        }

        bool playerWon = false;

        if (mMyMinis.Count > 0 && mEnemyMinis.Count == 0)
        {
            playerWins++;
            playerWon = true;
            AudioManager.Instance?.PlayRoundWin();
        }
        else if (mEnemyMinis.Count > 0 && mMyMinis.Count == 0)
        {
            enemyWins++;
            playerWon = false;
            AudioManager.Instance?.PlayRoundLose();
        }
        float currentFitness = CalculateFitness();
        UpdateScoreUI();
        mBattleInProgress = false;
        IsBattleActive = false;

        yield return ShowRoundResult(playerWon);

        if (playerWins >= winsToWin || enemyWins >= winsToWin)
        {
            EndSeries();
            yield break;
        }

        currentRound++;
        UpdateScoreUI();
        SetupNextRound(currentFitness);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic();
    }
    [Header("Auto Battle Timer")]
    public GameObject timerPanel; // ← Ссылка на панель Time
    [Header("Round Result UI")]
    public GameObject roundWinPanel;
    public GameObject roundLosePanel;
    private IEnumerator ShowRoundResult(bool playerWon)
    {
        GameObject panel = playerWon ? roundWinPanel : roundLosePanel;

        if (panel != null)
        {
            panel.SetActive(true);
            yield return new WaitForSeconds(2f);
            panel.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(2f);
        }
    }   
    private void EndSeries()
    {
        StopTimer();

        bool isTotalWin = playerWins >= winsToWin;
        
        // 1. Начисляем кубки и ставим защиту от "минуса"
        if (PlayerDataManager.instance != null)
        {
            // Если проигрыш, вычитаем 10, но в PlayerDataManager 
            // сработает Mathf.Max(0, ...), так что ниже нуля не упадем.
            PlayerDataManager.instance.AddResult(isTotalWin, isTotalWin ? 25 : 10);
        }

        // 2. Показываем нужную панель
        if (isTotalWin)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayVictoryMusic();
            if (victoryPanel != null) victoryPanel.SetActive(true);
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDefeatMusic();
            if (defeatPanel != null) defeatPanel.SetActive(true);
        }

        // 3. Исправляем отображение счета (обновляем оба поля на всякий случай)
        string finalScoreStr = $"{playerWins} - {enemyWins}";
        if (finalScoreText1 != null) finalScoreText1.text = finalScoreStr;
        if (finalScoreText2 != null) finalScoreText2.text = finalScoreStr;

        IsBattleActive = true;
        BasePiece.sBattleStarted = true;
    }
    public void RestartSeries()
    {
        playerWins = 0;
        enemyWins = 0;
        currentRound = 1;
        winsToWin = 5; // ← ДОБАВЬ ЭТО (на случай если менялось)
        UpdateScoreUI();
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (defeatPanel != null) defeatPanel.SetActive(false);
        SetupFirstRound(); 
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayBattleMusic();
    }

    [Header("Attack Sprites")]
    public Sprite[] knightAttackSprites;
    public Sprite[] archerAttackSprites;
    public Sprite[] mageAttackSprites;
    public Sprite[] enemyKnightAttackSprites;
    public Sprite[] enemyArcherAttackSprites;
    public Sprite[] enemyMageAttackSprites;

    
    private void CleanDeadUnits()
    {
        mMyMinis.RemoveAll(u => u == null || !u.gameObject.activeSelf);
        mEnemyMinis.RemoveAll(u => u == null || !u.gameObject.activeSelf);
    }

    public List<BasePiece> GetAliveUnits()
    {
        List<BasePiece> all = new List<BasePiece>();
        all.AddRange(mMyMinis.Where(u => u != null && u.gameObject.activeSelf));
        all.AddRange(mEnemyMinis.Where(u => u != null && u.gameObject.activeSelf));
        return all;
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
    // ЗАМЕНИТЕ эти методы в вашем PieceManager.cs:

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

        // Копируем БАЗОВЫЕ статы (уровень 1)
        newPiece.maxHP = template.maxHP;
        newPiece.currentHP = template.maxHP;
        newPiece.damage = template.damage;
        newPiece.attackRange = template.attackRange;
        newPiece.attackSpeed = template.attackSpeed;
        newPiece.unitID = template.unitID;
        newPiece.cost = GetUnitCost(unitType);

        // Назначаем спрайты
        if (isPlayer)
        {
            if (unitType == typeof(Knight)) { newPiece.levelSprites = knightSprites; newPiece.attackSprites = knightAttackSprites; }
            else if (unitType == typeof(Archer)) { newPiece.levelSprites = archerSprites; newPiece.attackSprites = archerAttackSprites; }
            else if (unitType == typeof(Mage)) { newPiece.levelSprites = mageSprites; newPiece.attackSprites = mageAttackSprites; }
        }
        else
        {
            if (unitType == typeof(Knight)) { newPiece.levelSprites = enemyKnightSprites; newPiece.attackSprites = enemyKnightAttackSprites; }
            else if (unitType == typeof(Archer)) { newPiece.levelSprites = enemyArcherSprites; newPiece.attackSprites = enemyArcherAttackSprites; }
            else if (unitType == typeof(Mage)) { newPiece.levelSprites = enemyMageSprites; newPiece.attackSprites = enemyMageAttackSprites; }
        }

        // Setup БЕЗ применения уровневых статов
        newPiece.Setup(isPlayer, teamColor, spriteColor, this);

        // ВАЖНО: Не вызываем ApplyLevelStats здесь!
        // Уровень будет установлен ВЫЗЫВАЮЩИМ кодом после SpawnUnit

        newPiece.Place(targetCell);

        if (isPlayer) mMyMinis.Add(newPiece);
        else mEnemyMinis.Add(newPiece);

        return newPiece;
    }


    public float CalculateFitness()
    {
        int initialPlayerUnits = savedPlayerUnits.Count;
        int initialEnemyUnits = mEnemyMinis.Count;

        if (initialEnemyUnits == 0) return -10f;
        if (initialPlayerUnits == 0) return 5f; // Игрок без юнитов — враги молодцы

        // 1. Очки за убийство игроков (главная цель)
        int killedPlayerUnits = 0;
        foreach (var saved in savedPlayerUnits)
        {
            bool stillAlive = mMyMinis.Any(u => u != null &&
                              u.mCurrentCell != null &&
                              u.mCurrentCell.mBoardPosition == saved.position &&
                              u.currentHP > 0);
            if (!stillAlive) killedPlayerUnits++;
        }
        float killScore = killedPlayerUnits * 5.0f; // Увеличено с 3 до 5

        // 2. Очки за нанесённый урон (поощряем агрессию)
        float totalPlayerMaxHP = savedPlayerUnits.Sum(u => u.maxHP);
        float totalPlayerCurrentHP = mMyMinis.Where(u => u != null && u.gameObject.activeSelf).Sum(u => u.currentHP);
        float damageDealt = totalPlayerMaxHP - totalPlayerCurrentHP;
        float damageScore = (damageDealt / Mathf.Max(totalPlayerMaxHP, 1f)) * 8.0f; // Увеличено с 5 до 8

        // 3. Выживаемость врагов (важно, но не главное)
        int survivingEnemies = mEnemyMinis.Count(u => u != null && u.gameObject.activeSelf && u.currentHP > 0);
        float survivalRatio = (float)survivingEnemies / Mathf.Max(initialEnemyUnits, 1);
        float survivalScore = survivalRatio * 3.0f; // Увеличено с 0.5 за юнита до 3.0 за всех

        // 4. Бонус за полную победу (вайпаут)
        bool playerWipedOut = mMyMinis.Count == 0 || mMyMinis.All(u => u == null || !u.gameObject.activeSelf || u.currentHP <= 0);
        float wipeoutBonus = playerWipedOut ? 10.0f : 0f;

        // 5. Минимальный фитнес за частичное выживание (защита от -10)
        bool anyEnemyAlive = survivingEnemies > 0;
        float survivalFloor = anyEnemyAlive ? 1.0f : 0f;

        float totalFitness = killScore + damageScore + survivalScore + wipeoutBonus;

        // Если все враги умерли, но нанесли урон — не -10, а пропорционально урону
        if (!anyEnemyAlive)
        {
            totalFitness = Mathf.Max(-5f, damageScore - 5f); // Минимум -5 вместо -10
        }
        else
        {
            totalFitness = Mathf.Max(survivalFloor, totalFitness); // Минимум 1 если выжили
        }

        Debug.Log($"GA Fitness: убито={killedPlayerUnits}/{initialPlayerUnits}, урон={damageDealt:F0}/{totalPlayerMaxHP:F0}, выжило={survivingEnemies}/{initialEnemyUnits}, итог={totalFitness:F2}");

        return totalFitness;
    }
    // Исправленный спавн врагов (уровень применяется ОДИН раз)
    public void SpawnEnemyLayout(string data)
    {
        ClearEnemies();

        string[] units = data.Trim().Split(',');

        for (int i = 0; i < units.Length; i++)
        {
            if (i >= 25) break;

            if (string.IsNullOrEmpty(units[i])) continue;

            string[] parts = units[i].Split(':');
            int unitTypeID = int.Parse(parts[0]);

            if (unitTypeID == 0) continue;

            int unitLevel = (parts.Length > 1) ? int.Parse(parts[1]) : 1;

            int x = i % 5;
            int y = (i / 5) + 5;

            Type t = GetTypeByID(unitTypeID);
            if (t == null) continue;

            // Спавним с БАЗОВЫМИ статами
            BasePiece newEnemy = SpawnUnit(t, Color.black, GetColorByUnitType(unitTypeID), new Vector2Int(x, y), false);

            if (newEnemy != null)
            {
                // Устанавливаем уровень ОДИН раз и применяем статы
                newEnemy.level = unitLevel;
                newEnemy.ApplyLevelStats();      // Применяем множитель уровня
                newEnemy.UpdateLevelAppearance();
                newEnemy.RefreshHealthBar();
            }
        }
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

        AudioManager.Instance?.PlayBuy(); // ← ДОБАВИТЬ ЗВУК
    }

    // Слияние двух одинаковых юнитов
    public void FuseUnits(BasePiece target, BasePiece dragged)
    {
        if (target.GetType() != dragged.GetType()) return;
        if (target.level != dragged.level) return;
        if (target.level >= target.maxLevel) return;

        Debug.Log($"⭐ СЛИЯНИЕ: {dragged.name} → {target.name} → уровень {target.level + 1}!");

        // Эффект слияния
        AudioManager.Instance.PlayFusion(); // ← ЗВУК СЛИЯНИЯ
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
    public void ExitToMenu()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    
    public int CalculateEnemyBudget()
    {
        
        int baseBudget = 10 + (currentRound - 1) * 4; 
        
        
        int survivorBonus = 0;
        foreach (var unit in mEnemyMinis)
        {
            if (unit != null && unit.gameObject.activeSelf && unit.currentHP > 0)
            {
                survivorBonus += GetUnitCost(unit.GetType()) * unit.level;
            }
        }
        
        return baseBudget + survivorBonus;
    }
}
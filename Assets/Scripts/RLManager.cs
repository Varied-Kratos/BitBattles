using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class RLManager : MonoBehaviour
{
    public static RLManager Instance { get; private set; }

    [Header("RL Mode")]
    public bool useRL = false;
    public string pythonHost = "127.0.0.1";
    public int pythonPort = 65432;

    private TcpClient mClient;
    private NetworkStream mStream;
    private bool mConnected = false;

    public PieceManager pieceManager;
    public Board board;

    // Для расчёта наград между шагами
    private Dictionary<string, ExperienceBuffer> unitExperience = new Dictionary<string, ExperienceBuffer>();

    // Буфер для хранения предыдущего опыта
    private class ExperienceBuffer
    {
        public float[] prevState;
        public int prevAction;
        public float prevHP;
        public float prevEnemyHP;
        public Vector2Int prevPos;
        public bool prevCanAttack;
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        if (useRL) ConnectToPython();
    }

    void Update()
    {
        if (useRL && !mConnected) ConnectToPython();
    }

    public void ConnectToPython()
    {
        try
        {
            mClient = new TcpClient();
            mClient.Connect(pythonHost, pythonPort);
            mStream = mClient.GetStream();
            mConnected = true;
            Debug.Log("RLManager подключился к Python серверу");
        }
        catch (Exception e)
        {
            Debug.LogWarning("Не удалось подключиться к Python: " + e.Message);
            mConnected = false;
        }
    }

    // --------------------------------------------------
    // Вектор состояния (27 элементов)
    // --------------------------------------------------
    public float[] GetUnitState(BasePiece unit)
    {
        float[] s = new float[27];
        if (unit == null || unit.mCurrentCell == null)
            return s;

        Vector2Int pos = unit.mCurrentCell.mBoardPosition;

        // 0-1: Свои координаты
        s[0] = pos.x / 4f;
        s[1] = pos.y / 9f;

        // 2: Текущее HP
        s[2] = (float)unit.currentHP / unit.maxHP;

        // 3: Уровень юнита
        s[3] = unit.level / 3f;

        // 4: ID Типа юнита
        s[4] = unit.unitID / 3f;

        // 5-8: Данные о ближайшем противнике
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null && enemy.mCurrentCell != null)
        {
            s[5] = enemy.mCurrentCell.mBoardPosition.x / 4f;
            s[6] = enemy.mCurrentCell.mBoardPosition.y / 9f;
            s[7] = enemy.unitID / 3f;
            s[8] = unit.CanAttackTarget(enemy) ? 1f : 0f;
        }

        // 9-16: Окружение в радиусе 1
        FillRingNeighbors(s, 9, pos, 1, unit);

        // 17-25: Окружение на векторе атаки
        int range = (unit.attackRange <= 1) ? 2 : unit.attackRange;
        FillRingNeighbors(s, 17, pos, range, unit);

        // 26: Флаг нахождения за препятствием
        s[26] = IsBehindCover(unit) ? 1f : 0f;

        return s;
    }

    private void FillRingNeighbors(float[] arr, int start, Vector2Int pos, int radius, BasePiece unit)
    {
        int idx = start;
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

        for (int i = 0; i < 8; i++)
        {
            if (idx >= arr.Length || idx >= start + 8) return;

            int nx = pos.x + dx[i] * radius;
            int ny = pos.y + dy[i] * radius;

            if (nx < 0 || nx >= 5 || ny < 0 || ny >= 10)
            {
                arr[idx++] = -1f;
            }
            else if (pieceManager.blockedCells.Contains(new Vector2Int(nx, ny)))
            {
                arr[idx++] = -2f;
            }
            else if (pieceManager.healCells.Contains(new Vector2Int(nx, ny)))
            {
                arr[idx++] = 3f;
            }
            else
            {
                Cell cell = board.mAllCells[nx, ny];
                if (cell == null || cell.mCurrentPiece == null)
                {
                    arr[idx++] = 0f;
                }
                else
                {
                    BasePiece other = cell.mCurrentPiece;
                    bool sameTeam = unit.name.Contains("Player") == other.name.Contains("Player");
                    arr[idx++] = sameTeam ? 1f : 2f;
                }
            }
        }
    }

    private bool IsBehindCover(BasePiece unit)
    {
        if (unit == null || unit.mCurrentCell == null) return false;
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy == null || enemy.mCurrentCell == null) return false;

        Vector2Int myPos = unit.mCurrentCell.mBoardPosition;
        Vector2Int enemyPos = enemy.mCurrentCell.mBoardPosition;
        Vector2Int dir = myPos - enemyPos;
        if (dir == Vector2Int.zero) return false;

        int dx = Mathf.Clamp(dir.x, -1, 1);
        int dy = Mathf.Clamp(dir.y, -1, 1);
        Vector2Int checkPos = myPos - new Vector2Int(dx, dy);

        if (checkPos.x < 0 || checkPos.x >= 5 || checkPos.y < 0 || checkPos.y >= 10)
            return false;

        return pieceManager.blockedCells.Contains(checkPos);
    }

    // ИСПРАВЛЕНО: Отправляем state и получаем action, потом считаем reward
    private int SendStateAndGetAction(float[] state, string unitId)
    {
        if (!mConnected) return UnityEngine.Random.Range(0, 5);

        float[] safe = new float[27];
        Array.Copy(state, safe, Math.Min(state.Length, 27));

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string numbers = string.Join(",", System.Array.ConvertAll(safe, x => x.ToString(culture)));
        string json = $"{{\"state\":[{numbers}],\"unit_id\":\"{unitId}\"}}";

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            mStream.Write(data, 0, data.Length);

            byte[] buffer = new byte[1024];
            int bytesRead = mStream.Read(buffer, 0, buffer.Length);
            string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            int action = 0;
            if (response.Contains("\"action\":"))
            {
                int start = response.IndexOf("\"action\":") + 9;
                int end = response.IndexOf("}", start);
                if (end < 0) end = response.Length;
                string num = response.Substring(start, end - start).Trim();
                int.TryParse(num, out action);
            }
            return Mathf.Clamp(action, 0, 4);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Ошибка связи с Python: " + e.Message);
            mConnected = false;
            return UnityEngine.Random.Range(0, 5);
        }
    }

    // ИСПРАВЛЕНО: Отправляем опыт (state, action, reward, next_state, done)
    private void SendExperience(float[] state, int action, float reward, float[] nextState, bool done, string unitId)
    {
        if (!mConnected) return;

        float[] safeState = new float[27];
        float[] safeNextState = new float[27];
        Array.Copy(state, safeState, Math.Min(state.Length, 27));
        Array.Copy(nextState, safeNextState, Math.Min(nextState.Length, 27));

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string stateStr = string.Join(",", System.Array.ConvertAll(safeState, x => x.ToString(culture)));
        string nextStateStr = string.Join(",", System.Array.ConvertAll(safeNextState, x => x.ToString(culture)));

        string json = $"{{\"state\":[{stateStr}],\"action\":{action},\"reward\":{reward.ToString(culture)},\"next_state\":[{nextStateStr}],\"done\":{done.ToString().ToLower()},\"unit_id\":\"{unitId}\"}}";

        try
        {
            byte[] data = Encoding.UTF8.GetBytes(json + "\n");
            mStream.Write(data, 0, data.Length);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Ошибка отправки опыта: " + e.Message);
            mConnected = false;
        }
    }

    public void ApplyAction(BasePiece unit, int action)
    {
        if (unit == null || unit.mCurrentCell == null) return;

        Vector2Int pos = unit.mCurrentCell.mBoardPosition;
        Vector2Int newPos = pos;

        switch (action)
        {
            case 1: newPos = pos + new Vector2Int(0, 1); break;
            case 2: newPos = pos + new Vector2Int(0, -1); break;
            case 3: newPos = pos + new Vector2Int(-1, 0); break;
            case 4: newPos = pos + new Vector2Int(1, 0); break;
            default: break;
        }

        if (action >= 1 && action <= 4)
        {
            if (board.ValidateCell(newPos.x, newPos.y, unit) == CellState.Free)
            {
                Cell targetCell = board.mAllCells[newPos.x, newPos.y];
                unit.MoveToCell(targetCell);
            }
        }

        // Выполнение атаки после движения
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null && enemy != unit && unit.CanAttackTarget(enemy))
        {
            bool unitIsPlayer = unit.name.Contains("Player");
            bool enemyIsPlayer = enemy.name.Contains("Player");
            if (unitIsPlayer != enemyIsPlayer)
            {
                unit.AttackTarget(enemy);
            }
        }
    }

    // --------------------------------------------------
    // ИСПРАВЛЕННАЯ логика RL шага
    // --------------------------------------------------
    public void RLTurn(BasePiece unit)
    {
        if (unit == null || unit.mCurrentCell == null) return;

        string id = unit.name;

        // 1. Получаем текущее состояние
        float[] currentState = GetUnitState(unit);

        // 2. Сохраняем данные ДО действия
        BasePiece nearestEnemy = unit.FindNearestEnemy();
        float enemyHPBefore = (nearestEnemy != null && nearestEnemy.gameObject.activeSelf) ? nearestEnemy.currentHP : 0f;
        float myHPBefore = unit.currentHP;
        bool couldAttackBefore = nearestEnemy != null && unit.CanAttackTarget(nearestEnemy);

        // 3. Если есть предыдущий опыт — отправляем его с reward
        if (unitExperience.ContainsKey(id))
        {
            ExperienceBuffer exp = unitExperience[id];

            // Считаем награду за ПРЕДЫДУЩЕЕ действие
            float reward = CalculateReward(unit, exp, myHPBefore, enemyHPBefore, couldAttackBefore);
            bool done = unit.currentHP <= 0;

            // Отправляем опыт
            SendExperience(exp.prevState, exp.prevAction, reward, currentState, done, id);

            if (done)
            {
                unitExperience.Remove(id);
                return; // Юнит умер, не даём новое действие
            }
        }

        // 4. Запрашиваем новое действие
        int action = SendStateAndGetAction(currentState, id);

        // 5. Выполняем действие
        ApplyAction(unit, action);

        // 6. Сохраняем опыт для следующего шага
        unitExperience[id] = new ExperienceBuffer
        {
            prevState = currentState,
            prevAction = action,
            prevHP = myHPBefore,
            prevEnemyHP = enemyHPBefore,
            prevPos = unit.mCurrentCell.mBoardPosition,
            prevCanAttack = couldAttackBefore
        };
    }

    // ИСПРАВЛЕННАЯ функция награды
    private float CalculateReward(BasePiece unit, ExperienceBuffer exp, float currentHP, float currentEnemyHP, bool canAttackNow)
    {
        float reward = 0f;

        // 1. Маленький штраф за существование (стимул действовать)
        reward -= 0.01f;

        // 2. Награда за УРОН врагу (САМОЕ ВАЖНОЕ)
        if (exp.prevEnemyHP > 0)
        {
            float damageDealt = exp.prevEnemyHP - currentEnemyHP;
            if (damageDealt > 0)
            {
                reward += damageDealt * 0.5f; // +0.5 за каждую единицу урона

                // Бонус за убийство
                if (currentEnemyHP <= 0)
                {
                    reward += 15f;
                }
            }
        }

        // 3. Штраф за ПОЛУЧЕННЫЙ урон
        float hpLost = exp.prevHP - currentHP;
        if (hpLost > 0)
        {
            reward -= hpLost * 0.3f; // -0.3 за каждую единицу своего HP

            // Дополнительный штраф за смерть
            if (currentHP <= 0)
            {
                reward -= 20f;
            }
        }

        // 4. Награда за приближение к врагу (только для ближнего боя)
        if (unit is Knight && exp.prevCanAttack == false && canAttackNow)
        {
            reward += 2f; // Бонус за вход в радиус атаки
        }

        // 5. Бонус за укрытие (для дальнего боя)
        if ((unit is Archer || unit is Mage) && IsBehindCover(unit))
        {
            BasePiece enemy = unit.FindNearestEnemy();
            if (enemy != null)
            {
                reward += 0.3f;
            }
        }

        // 6. Бонус за подбор хилки
        if (currentHP > exp.prevHP && !canAttackNow)
        {
            reward += 5f; // Лечение без боя = подобрал хилку
        }

        return reward;
    }

    private float DistanceToNearestEnemy(BasePiece unit)
    {
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy == null || enemy.mCurrentCell == null) return 99f;
        return Vector2.Distance(unit.mCurrentCell.mBoardPosition, enemy.mCurrentCell.mBoardPosition);
    }

    public void ProcessRLTurn()
    {
        List<BasePiece> allUnits = pieceManager.GetAliveUnits();
        foreach (var unit in allUnits)
        {
            if (unit != null && unit.gameObject.activeSelf)
                RLTurn(unit);
        }
    }

    void OnDestroy()
    {
        mStream?.Close();
        mClient?.Close();
    }
}
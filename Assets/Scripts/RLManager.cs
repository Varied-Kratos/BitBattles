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
    private Dictionary<string, float> prevHP = new Dictionary<string, float>();
    private Dictionary<string, float> prevDist = new Dictionary<string, float>();
    private Dictionary<string, bool> killedEnemy = new Dictionary<string, bool>();

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
    // 27-мерное состояние юнита (совместимо с STATE_SIZE=27)
    // --------------------------------------------------
    public float[] GetUnitState(BasePiece unit)
    {
        float[] s = new float[27];
        if (unit == null || unit.mCurrentCell == null)
            return s;

        Vector2Int pos = unit.mCurrentCell.mBoardPosition;

        // 0-1: координаты
        s[0] = pos.x / 4f;
        s[1] = pos.y / 9f;

        // 2: HP
        s[2] = (float)unit.currentHP / unit.maxHP;

        // 3: уровень
        s[3] = unit.level / 3f;

        // 4: тип юнита
        s[4] = unit.unitID / 3f;

        // 5-8: ближайший враг
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null && enemy.mCurrentCell != null)
        {
            s[5] = enemy.mCurrentCell.mBoardPosition.x / 4f;
            s[6] = enemy.mCurrentCell.mBoardPosition.y / 9f;
            s[7] = enemy.unitID / 3f;
            s[8] = unit.CanAttackTarget(enemy) ? 1f : 0f;
        }

        // 9-16: 8 ближних соседей (радиус 1)
        FillNeighbors(s, 9, pos, 1, unit);               // было FillNeighbors(s, 9, pos, 1);
        FillNeighbors(s, 17, pos, unit.attackRange, unit); // было FillNeighbors(s, 17, pos, unit.attackRange);
        // 17-25: дальние клетки (attackRange)

        // 26: флаг укрытия за камнем (только для Archer/Mage)
        s[26] = IsBehindCover(unit) ? 1f : 0f;

        return s;
    }

    // Заполняет массив s начиная с индекса start значениями клеток в радиусе radius
    private void FillNeighbors(float[] arr, int start, Vector2Int pos, int radius, BasePiece unit)
    {
        int idx = start;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Mathf.Abs(dx) <= 1 && Mathf.Abs(dy) <= 1 && radius > 1) continue;

                int nx = pos.x + dx;
                int ny = pos.y + dy;
                if (idx >= arr.Length) return;

                if (nx < 0 || nx >= 5 || ny < 0 || ny >= 10)
                    arr[idx++] = -1f;
                else
                {
                    Cell cell = board.mAllCells[nx, ny];
                    if (pieceManager.blockedCells.Contains(new Vector2Int(nx, ny)))
                        arr[idx++] = -2f;
                    else if (pieceManager.healCells.Contains(new Vector2Int(nx, ny)))
                        arr[idx++] = 3f;
                    else if (cell == null || cell.mCurrentPiece == null)
                        arr[idx++] = 0f;
                    else
                    {
                        BasePiece other = cell.mCurrentPiece;
                        bool sameTeam = unit.name.Contains("Player") == other.name.Contains("Player");
                        arr[idx++] = sameTeam ? 1f : 2f;
                    }
                }
            }
        }
    }

    // Находится ли юнит за камнем относительно ближайшего врага
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

    // --------------------------------------------------
    // Отправка состояния и получение действия
    // --------------------------------------------------
    private int SendStateAndGetAction(float[] state, float reward, bool done, string unitId)
    {
        if (!mConnected) return UnityEngine.Random.Range(0, 5);

        float[] safe = new float[27];
        Array.Copy(state, safe, Math.Min(state.Length, 27));

        var culture = System.Globalization.CultureInfo.InvariantCulture;
        string numbers = string.Join(",", System.Array.ConvertAll(safe, x => x.ToString(culture)));
        string json = $"{{\"state\":[{numbers}],\"reward\":{reward.ToString(culture)},\"done\":{done.ToString().ToLower()},\"unit_id\":\"{unitId}\"}}";

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

    // --------------------------------------------------
    // Применение действия
    // --------------------------------------------------
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

        // Авто-атака (если враг рядом)
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null && enemy != unit && unit.CanAttackTarget(enemy))
        {
            bool unitIsPlayer = unit.name.Contains("Player");
            bool enemyIsPlayer = enemy.name.Contains("Player");
            if (unitIsPlayer != enemyIsPlayer)
            {
                unit.AttackTarget(enemy);
                if (enemy.currentHP <= 0)
                    killedEnemy[unit.name] = true;
            }
        }
    }

    // --------------------------------------------------
    // RL-ход с расчётом награды
    // --------------------------------------------------
    public void RLTurn(BasePiece unit)
    {
        if (unit == null || unit.mCurrentCell == null) return;

        string id = unit.name;
        float reward = 0f;
        bool done = false;
        float hpBefore = unit.currentHP;
        float distBefore = DistanceToNearestEnemy(unit);

        // Награда за предыдущий шаг
        if (prevHP.ContainsKey(id))
        {
            float hpDiff = unit.currentHP - prevHP[id];
            reward += hpDiff * 0.1f;

            if (killedEnemy.ContainsKey(id) && killedEnemy[id])
            {
                reward += 5f;
                killedEnemy[id] = false;
            }

            float newDist = DistanceToNearestEnemy(unit);
            float distDiff = prevDist[id] - newDist;
            reward += distDiff * 0.01f;

            // Бонус за укрытие
            if ((unit is Archer || unit is Mage) && IsBehindCover(unit))
                reward += 0.5f;

            // Подбор хилки (HP выросло без атаки)
            if (unit.currentHP > hpBefore && !unit.CanAttackTarget(unit.FindNearestEnemy()))
                reward += 3f;

            if (unit.currentHP <= 0)
            {
                reward -= 10f;
                done = true;
            }
        }

        float[] state = GetUnitState(unit);
        int action = SendStateAndGetAction(state, reward, done, id);
        ApplyAction(unit, action);

        // Сохраняем данные для следующего шага
        prevHP[id] = unit.currentHP;
        prevDist[id] = DistanceToNearestEnemy(unit);
        if (!killedEnemy.ContainsKey(id)) killedEnemy[id] = false;

        if (done)
        {
            prevHP.Remove(id);
            prevDist.Remove(id);
            killedEnemy.Remove(id);
        }
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
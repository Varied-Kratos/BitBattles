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
    public bool useRL = false;               // включить RL вместо обычного ИИ
    public string pythonHost = "127.0.0.1";
    public int pythonPort = 65432;

    private TcpClient mClient;
    private NetworkStream mStream;
    private bool mConnected = false;

    // Ссылки на менеджеры
    public PieceManager pieceManager;
    public Board board;
    void Start()
    {
        if (useRL)
            ConnectToPython();
    }

    void Update()
    {
        // Повторная попытка подключения, если не подключены
        if (useRL && !mConnected)
            ConnectToPython();
    }
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // Попытка подключиться к Python‑серверу
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

    // === Сбор состояния одного юнита ===
    public float[] GetUnitState(BasePiece unit)
    {
        float[] s = new float[17]; // всегда 17
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
        else
        {
            s[5] = 0f; s[6] = 0f; s[7] = 0f; s[8] = 0f;
        }

        // 9-16: 8 соседних клеток
        int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
        int[] dy = { 1, 1, 0, -1, -1, -1, 0, 1 };
        for (int i = 0; i < 8; i++)
        {
            int nx = pos.x + dx[i];
            int ny = pos.y + dy[i];
            if (nx < 0 || nx >= 5 || ny < 0 || ny >= 10)
            {
                s[9 + i] = -1f;
            }
            else
            {
                Cell cell = board.mAllCells[nx, ny];
                if (cell == null || cell.mCurrentPiece == null)
                    s[9 + i] = 0f;
                else
                {
                    BasePiece other = cell.mCurrentPiece;
                    bool sameTeam = unit.name.Contains("Player") == other.name.Contains("Player");
                    s[9 + i] = sameTeam ? 1f : 2f;
                }
            }
        }

        return s;
    }

    // === Отправка состояния и получение действия ===
    private int SendStateAndGetAction(float[] state, float reward, bool done, string unitId)
    {
        if (!mConnected) return UnityEngine.Random.Range(0, 5);

        // Обрезаем до 17 (на всякий случай)
        float[] safe = new float[17];
        Array.Copy(state, safe, Math.Min(state.Length, 17));

        // Формируем JSON с инвариантной культурой (точка в числах)
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

    // === Применить действие к юниту (RL) ===
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

        // Движение
        if (action >= 1 && action <= 4)
        {
            if (board.ValidateCell(newPos.x, newPos.y, unit) == CellState.Free)
            {
                Cell targetCell = board.mAllCells[newPos.x, newPos.y];
                unit.MoveToCell(targetCell);
            }
        }

        // Атака — ТОЛЬКО если враг существует и ЭТО ДЕЙСТВИТЕЛЬНО ВРАГ
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null && enemy != unit && unit.CanAttackTarget(enemy))
        {
            // Двойная проверка: враг должен быть из противоположной команды
            bool unitIsPlayer = unit.name.Contains("Player");
            bool enemyIsPlayer = enemy.name.Contains("Player");
            if (unitIsPlayer != enemyIsPlayer)
            {
                unit.AttackTarget(enemy);
            }
        }
    }

    // === Замена TakeTurn для RL ===
    public void RLTurn(BasePiece unit)
    {
        float[] state = GetUnitState(unit);
        int action = SendStateAndGetAction(state, 0f, false, unit.name);
        ApplyAction(unit, action);
    }

    // === RL цикл для всех юнитов (вызывается из PieceManager) ===
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
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
        if (unit == null || unit.mCurrentCell == null)
            return new float[0];

        var state = new List<float>();

        // 1. Нормализованные координаты (0..1)
        state.Add(unit.mCurrentCell.mBoardPosition.x / 4f);
        state.Add(unit.mCurrentCell.mBoardPosition.y / 9f);

        // 2. HP (нормализовано)
        state.Add((float)unit.currentHP / unit.maxHP);

        // 3. Уровень (1/3, 2/3, 3/3)
        state.Add(unit.level / 3f);

        // 4. Тип юнита (1=Knight, 2=Archer, 3=Mage)
        state.Add(unit.unitID / 3f);

        // 5. Ближайший враг
        BasePiece enemy = unit.FindNearestEnemy();
        if (enemy != null)
        {
            Vector2Int enemyPos = enemy.mCurrentCell.mBoardPosition;
            state.Add(enemyPos.x / 4f);
            state.Add(enemyPos.y / 9f);
            state.Add(enemy.unitID / 3f);
            state.Add(unit.CanAttackTarget(enemy) ? 1f : 0f);
        }
        else
        {
            state.Add(-1f); state.Add(-1f); state.Add(-1f); state.Add(-1f);
        }

        // 6. 8 соседних клеток (что на них?)
        Vector2Int pos = unit.mCurrentCell.mBoardPosition;
        Vector2Int[] dirs = {
            new Vector2Int(0,1), new Vector2Int(1,1), new Vector2Int(1,0), new Vector2Int(1,-1),
            new Vector2Int(0,-1), new Vector2Int(-1,-1), new Vector2Int(-1,0), new Vector2Int(-1,1)
        };
        foreach (var d in dirs)
        {
            int nx = pos.x + d.x;
            int ny = pos.y + d.y;
            if (nx < 0 || nx >= 5 || ny < 0 || ny >= 10)
                state.Add(-1f); // стена
            else
            {
                Cell cell = board.mAllCells[nx, ny];
                if (cell.mCurrentPiece == null) state.Add(0f); // пусто
                else
                {
                    // свой или враг?
                    BasePiece other = cell.mCurrentPiece;
                    bool sameTeam = (unit.name.Contains("Player") && other.name.Contains("Player")) ||
                                    (!unit.name.Contains("Player") && !other.name.Contains("Player"));
                    state.Add(sameTeam ? 1f : 2f);
                }
            }
        }

        return state.ToArray();
    }

    // === Отправка состояния и получение действия ===
    private int SendStateAndGetAction(float[] state, float reward, bool done, string unitId)
    {
        if (!mConnected) return UnityEngine.Random.Range(0, 5);

        try
        {
            // Формируем JSON с состоянием, наградой и флагом завершения
            string json = $"{{\"state\":[{string.Join(",", state)}],\"reward\":{reward},\"done\":{done.ToString().ToLower()},\"unit_id\":\"{unitId}\"}}";
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
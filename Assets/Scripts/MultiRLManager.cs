using System.Collections.Generic;
using UnityEngine;

public class MultiRLManager : MonoBehaviour
{
    public static MultiRLManager Instance { get; private set; }

    [Header("Multi RL Settings")]
    public bool useRL = false;
    public string pythonHost = "127.0.0.1";
    public int basePort = 65432;
    public int agentCount = 4;
    public PieceManager pieceManager;
    public Board board;

    private RLManager[] rlManagers;
    private int currentAgentIndex = 0;

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
        if (!useRL) return;

        rlManagers = new RLManager[agentCount];
        for (int i = 0; i < agentCount; i++)
        {
            GameObject agentObj = new GameObject($"RL_Agent_{i}");
            agentObj.transform.SetParent(transform);

            RLManager rl = agentObj.AddComponent<RLManager>();
            rl.useRL = true;
            rl.pythonHost = pythonHost;
            rl.pythonPort = basePort + i;
            rl.pieceManager = pieceManager;
            rl.board = board;

            rlManagers[i] = rl;
        }

        foreach (var rl in rlManagers)
            rl.ConnectToPython();
    }

    // Вызывается для каждого юнита отдельно — не блокирует цикл
    public void RLTurnForUnit(BasePiece unit)
    {
        if (!useRL || rlManagers == null || unit == null) return;

        // Распределяем по кругу
        int index = currentAgentIndex % agentCount;
        currentAgentIndex++;

        rlManagers[index].RLTurn(unit);
    }
}
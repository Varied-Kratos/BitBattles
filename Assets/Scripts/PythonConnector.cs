using System;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using UnityEngine;

public class PythonConnector : MonoBehaviour
{
    public string host = "127.0.0.1";
    public int port = 5005;
    public BoardEncoder boardEncoder;
    public PieceManager pieceManager;

    void Awake()
    {
        if (boardEncoder == null) boardEncoder = GetComponent<BoardEncoder>();
        if (pieceManager == null) pieceManager = GetComponent<PieceManager>();
    }

    void Start()
    {
        if (boardEncoder != null && pieceManager != null)
        {
            RequestNextLayout(0);
        }
    }

    public void RequestNextLayout(float lastFitness = 0)
    {
        if (pieceManager == null || boardEncoder == null) return;

        try {
            if (pieceManager.victoryPanel != null) pieceManager.victoryPanel.SetActive(false);
            if (pieceManager.defeatPanel != null) pieceManager.defeatPanel.SetActive(false);
        } catch { }

        try 
        {
            int enemyBudget = pieceManager.CalculateEnemyBudget();
            int playerTrophies = PlayerDataManager.instance != null ? PlayerDataManager.instance.trophies : 0;
            int round = pieceManager.currentRound;

            float[] rawState = boardEncoder.GetFlattenedBoardState();
            if (rawState == null) return;

            string boardState = string.Join(",", rawState.Select(f => ((int)f).ToString()));
            string message = $"FITNESS:{lastFitness.ToString("F1")}|BUDGET:{enemyBudget}|TROPHIES:{playerTrophies}|ROUND:{round}|BOARD:{boardState}"; 

            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                
                if (!success) return;

                client.EndConnect(result);
                using (NetworkStream stream = client.GetStream())
                {
                    stream.Write(dataToSend, 0, dataToSend.Length);
                    byte[] buffer = new byte[2048];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (!string.IsNullOrEmpty(response)) {
                        pieceManager.SpawnEnemyLayout(response);
                    }
                }
            }
        }
        catch (Exception e) {
            Debug.LogWarning(e.Message);
        }
    }
}
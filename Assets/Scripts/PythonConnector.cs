using System;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using UnityEngine;
using System.Globalization;

public class PythonConnector : MonoBehaviour
{
    public string host = "127.0.0.1";
    public int port = 5005;
    public BoardEncoder boardEncoder;
    public PieceManager pieceManager;

    void Awake()
    {
        if (boardEncoder == null) boardEncoder = FindObjectOfType<BoardEncoder>();
        if (pieceManager == null) pieceManager = FindObjectOfType<PieceManager>();
    }

    public void RequestNextLayout(float lastFitness = 0)
    {
        if (pieceManager == null || boardEncoder == null) return;

        // Чистим интерфейс перед новым запросом
        if (pieceManager.victoryPanel != null) pieceManager.victoryPanel.SetActive(false);
        if (pieceManager.defeatPanel != null) pieceManager.defeatPanel.SetActive(false);

        try 
        {
            // Сбор данных
            int enemyBudget = pieceManager.CalculateEnemyBudget();
            int currentRound = pieceManager.currentRound;
            float[] rawBoard = boardEncoder.GetFlattenedBoardState();
            
            if (rawBoard == null) return;

            // Сборка финального вектора 102
            float[] fullState = new float[102];
            Array.Copy(rawBoard, fullState, Math.Min(rawBoard.Length, 100));
            fullState[100] = (float)enemyBudget;
            fullState[101] = (float)currentRound;

            // Кодирование в строку: "Бюджет|Число,Число,Число..."
            string boardString = string.Join(",", fullState.Select(f => f.ToString(CultureInfo.InvariantCulture)));
            string message = $"{enemyBudget}|{boardString}"; 

            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                
                if (!success) return;
                client.EndConnect(result);

                using (NetworkStream stream = client.GetStream())
                {
                    // Отправка данных (Input для нейросети)
                    stream.Write(dataToSend, 0, dataToSend.Length);

                    // Получение ответа (Output нейросети - расстановка)
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (!string.IsNullOrEmpty(response)) {
                        pieceManager.SpawnEnemyLayout(response);
                    }

                    // Отправка результата (Fitness для обучения)
                    string fitnessMsg = lastFitness.ToString("F2", CultureInfo.InvariantCulture);
                    byte[] fitnessData = Encoding.UTF8.GetBytes(fitnessMsg);
                    stream.Write(fitnessData, 0, fitnessData.Length);
                }
            }
        }
        catch (Exception e) {
            Debug.LogWarning("Связь с Python не удалась: " + e.Message);
        }
    }
}
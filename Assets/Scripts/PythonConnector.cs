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

        if (pieceManager.victoryPanel != null) pieceManager.victoryPanel.SetActive(false);
        if (pieceManager.defeatPanel != null) pieceManager.defeatPanel.SetActive(false);

        try 
        {
            int enemyBudget = pieceManager.CalculateEnemyBudget();
            int currentRound = pieceManager.currentRound;
            float[] rawBoard = boardEncoder.GetFlattenedBoardState();
            
            if (rawBoard == null) return;

            // Собираем fullState (100 клеток доски + 2 мета-переменные)
            float[] fullState = new float[102];
            Array.Copy(rawBoard, fullState, Math.Min(rawBoard.Length, 100));
            fullState[100] = (float)enemyBudget;
            fullState[101] = (float)currentRound;

            // Переводим массив доски в строку через запятую
            string boardString = string.Join(",", fullState.Select(f => f.ToString(CultureInfo.InvariantCulture)));
            
            // Форматируем фитнес, чтобы в Python гарантированно прилетела точка в качестве разделителя
            string fitnessStr = lastFitness.ToString("F2", CultureInfo.InvariantCulture);

            // Склеиваем ВСЁ в одно сообщение через разделитель '|'
            // Формат: БЮДЖЕТ | ФИТНЕС | ДОСКА,МЕТА
            string message = $"{enemyBudget}|{fitnessStr}|{boardString}"; 

            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                
                if (!success) {
                    Debug.LogError("Не удалось подключиться к Python-серверу по таймауту.");
                    return;
                }
                client.EndConnect(result);

                using (NetworkStream stream = client.GetStream())
                {
                    // 1. Отправляем ВСЕ данные разом
                    stream.Write(dataToSend, 0, dataToSend.Length);
                    stream.Flush(); 

                    // 2. Ждем ответ от Python с новой расстановкой врага
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (!string.IsNullOrEmpty(response)) {
                        pieceManager.SpawnEnemyLayout(response);
                    }
                    else {
                        Debug.LogWarning("Получен пустой ответ от Python-сервера.");
                    }
                } // Стрим и соединение закроются автоматически и чисто
            }
        }
        catch (Exception e) {
            Debug.LogWarning("Связь с Python не удалась: " + e.Message);
        }
    }
}
using System;
using System.Collections;
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

    /// <summary>
    /// Главный метод для запуска обсчета всего поколения.
    /// Передай сюда массив фитнес-очков (размером 15), и они отправятся последовательно с микро-паузой.
    /// </summary>
    public void RequestWholePopulation(float[] fitnessScores)
    {
        if (fitnessScores == null || fitnessScores.Length == 0)
        {
            Debug.LogError("Массив фитнеса пуст или равен null!");
            return;
        }

        StartCoroutine(SendPopulationCoroutine(fitnessScores));
    }

    private IEnumerator SendPopulationCoroutine(float[] fitnessScores)
    {
        for (int i = 0; i < fitnessScores.Length; i++)
        {
            // Вызываем отправку для текущего бота
            ExecuteSingleRequest(fitnessScores[i], i);

            // КРИТИЧЕСКИ ВАЖНО: Ждем 0.05 секунды перед следующим ботом,
            // чтобы Python успевал вызывать .accept() и сокеты не отваливались
            yield return new WaitForSeconds(0.05f);
        }
    }

    // Внутренний метод, который выполняет один конкретный сетевой запрос
    private void ExecuteSingleRequest(float lastFitness, int botIndex)
    {
        if (pieceManager == null || boardEncoder == null) return;

        if (pieceManager.victoryPanel != null) pieceManager.victoryPanel.SetActive(false);
        if (pieceManager.defeatPanel != null) pieceManager.defeatPanel.SetActive(false);

        try 
        {
            int enemyBudget = pieceManager.CalculateEnemyBudget();
            int currentRound = pieceManager.currentRound;
            float[] rawBoard = boardEncoder.GetGAState();
            
            if (rawBoard == null) return;

            // Собираем fullState (100 клеток доски + 2 мета-переменные)
            float[] fullState = new float[102];
            Array.Copy(rawBoard, fullState, Math.Min(rawBoard.Length, 100));
            fullState[100] = (float)enemyBudget;
            fullState[101] = (float)currentRound;

            // Переводим массив доски в строку через запятую
            string boardString = string.Join(",", fullState.Select(f => f.ToString(CultureInfo.InvariantCulture)));
            
            // Форматируем фитнес с точкой-разделителем
            string fitnessStr = lastFitness.ToString("F2", CultureInfo.InvariantCulture);

            // ИСПРАВЛЕНО: Склеиваем и ДОБАВЛЯЕМ '\n' в самый конец, чтобы Python-сервер четко видел терминатор пакета
            string message = $"{enemyBudget}|{fitnessStr}|{boardString}\n"; 

            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                var result = client.BeginConnect(host, port, null, null);
                // ИСПРАВЛЕНО: Увеличили таймаут до 3 секунд, чтобы боты в очереди ОС не отваливались раньше времени
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3));
                
                if (!success) {
                    Debug.LogError($"Бот {botIndex + 1}: Не удалось подключиться к Python-серверу по таймауту.");
                    return;
                }
                client.EndConnect(result);

                using (NetworkStream stream = client.GetStream())
                {
                    stream.ReadTimeout = 3000; // Таймаут на чтение ответа

                    // 1. Отправляем данные
                    stream.Write(dataToSend, 0, dataToSend.Length);
                    stream.Flush(); 

                    // 2. Ждем ответ от Python
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    Debug.Log($"[DEBUG] Получен ответ: {response}");
                    if (!string.IsNullOrEmpty(response)) {
                        // Передаем ответ в менеджер для спавна конкретного бота
                        pieceManager.SpawnEnemyLayout(response);
                    }
                    else {
                        Debug.LogWarning($"Бот {botIndex + 1}: Получен пустой ответ от Python-сервера.");
                    }
                } 
            }
        }
        catch (Exception e) {
            Debug.LogWarning($"Бот {botIndex + 1}: Связь с Python не удалась: " + e.Message);
        }
    }

    // Оставляем старый метод как обертку для одиночных тестов, если они где-то вызываются
    public void RequestNextLayout(float lastFitness = 0)
    {
        ExecuteSingleRequest(lastFitness, 0);
    }
}
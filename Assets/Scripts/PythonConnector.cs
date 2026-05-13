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
        // 1. Проверяем основы
        if (pieceManager == null || boardEncoder == null) return;

        // 2. БЕЗОПАСНОЕ выключение панелей (если они null, ошибки не будет)
        try {
            if (pieceManager.victoryPanel != null) pieceManager.victoryPanel.SetActive(false);
            if (pieceManager.defeatPanel != null) pieceManager.defeatPanel.SetActive(false);
        } catch { /* игнорируем, если что-то не так с UI */ }

        try 
        {
            // Получаем состояние доски
            float[] rawState = boardEncoder.GetFlattenedBoardState();
            if (rawState == null) return;

            // Конвертируем в строку для сервера
            string boardState = string.Join(",", rawState.Select(f => ((int)f).ToString()));
            string message = $"RESULT:{lastFitness}|{boardState}"; 

            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                // Пытаемся подключиться (таймаут 1 сек)
                var result = client.BeginConnect(host, port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                
                if (!success) {
                    Debug.LogWarning("Python Server не отвечает (таймаут)");
                    return;
                }

                client.EndConnect(result);
                using (NetworkStream stream = client.GetStream())
                {
                    stream.Write(dataToSend, 0, dataToSend.Length);
                    byte[] buffer = new byte[1024];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    if (!string.IsNullOrEmpty(response)) {
                        pieceManager.SpawnEnemyLayout(response);
                    }
                }
            }
        }
        catch (Exception e) {
            Debug.LogWarning("[PythonConnector] Ошибка: " + e.Message);
        }
    }
}
using System;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

public class PythonConnector : MonoBehaviour
{
    public string host = "127.0.0.1";
    public int port = 5005;
    
    // Ссылка на кодировщик
    public BoardEncoder boardEncoder;

    public void SendBoardData()
    {
        try
        {
            // 1. Получаем актуальные данные от твоего кодировщика
            float[] boardData = boardEncoder.GetFlattenedBoardState(); 

            // 2. Превращаем массив в строку формата "0.5,1,-1..."
            // Это быстрее, чем настраивать JSON-библиотеки для простого массива
            string dataString = string.Join(",", boardData);
            byte[] dataToSend = Encoding.UTF8.GetBytes(dataString);

            // 3. Создаем подключение
            using (TcpClient client = new TcpClient(host, port))
            using (NetworkStream stream = client.GetStream())
            {
                // Отправляем
                stream.Write(dataToSend, 0, dataToSend.Length);
                Debug.Log("Данные отправлены в Python!");

                // Ждем ответ (ход ИИ)
                byte[] responseBuffer = new byte[1024];
                int bytesRead = stream.Read(responseBuffer, 0, responseBuffer.Length);
                string response = Encoding.UTF8.GetString(responseBuffer, 0, bytesRead);
                
                Debug.Log("Python прислал решение: " + response);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Ошибка связи: " + e.Message);
        }
    }

    void Update()
    {
        // Для теста: жмем 'G' (Go), чтобы отправить данные
        if (Input.GetKeyDown(KeyCode.G))
        {
            SendBoardData();
        }
    }
}
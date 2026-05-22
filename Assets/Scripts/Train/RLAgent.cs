using UnityEngine;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Generic;
using System.Collections;

public class RLAgent : MonoBehaviour
{
    [Header("Connection Settings")]
    public string host = "127.0.0.1";
    public int port = 65432;
    public float reconnectDelay = 2f;
    
    private TcpClient client;
    private NetworkStream stream;
    private bool connected = false;
    private bool isReconnecting = false;

    [Serializable]
    public class StateActionReward
    {
        public float[] state;
        public int action;
        public float reward;
        public float[] nextState;
        public bool done;
        public string unitId;
    }

    [Serializable]
    public class StateWrapper { public float[] state; public string unitId; public bool train; }
    [Serializable]
    public class ActionWrapper { public int action; }

    void Start() 
    { 
        ConnectToServer(); 
    }

    public void SetPort(int newPort)
    {
        port = newPort;
        if (connected)
        {
            Disconnect();
        }
        ConnectToServer();
    }

    public void ConnectToServer() 
    {
        if (isReconnecting) return;
        
        try {
            if (client != null)
            {
                client.Close();
            }
            
            client = new TcpClient();
            client.BeginConnect(host, port, OnConnect, null);
        }
        catch (Exception e) {
            Debug.LogError($"Ошибка подключения RL к порту {port}: {e.Message}");
            connected = false;
            StartCoroutine(ReconnectRoutine());
        }
    }

    private void OnConnect(IAsyncResult ar)
    {
        try
        {
            client.EndConnect(ar);
            stream = client.GetStream();
            connected = true;
            isReconnecting = false;
            Debug.Log($"RLAgent подключен к Python серверу на порту {port}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Ошибка подключения к порту {port}: {e.Message}");
            connected = false;
            StartCoroutine(ReconnectRoutine());
        }
    }

    private void Disconnect()
    {
        connected = false;
        stream?.Close();
        client?.Close();
    }

    private IEnumerator ReconnectRoutine()
    {
        if (isReconnecting) yield break;
        isReconnecting = true;
        
        while (!connected)
        {
            yield return new WaitForSeconds(reconnectDelay);
            ConnectToServer();
        }
    }

    public int GetAction(float[] state, string unitId, bool isTraining = true)
    {
        if (!connected)
        {
            return UnityEngine.Random.Range(0, 5);
        }

        try {
            var wrapper = new StateWrapper { state = state, unitId = unitId, train = isTraining };
            string json = JsonUtility.ToJson(wrapper) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            
            stream.Write(data, 0, data.Length);
            stream.Flush();

            // Даём время серверу обработать запрос
            System.Threading.Thread.Sleep(10);
            
            // Читаем все доступные данные
            byte[] buffer = new byte[4096];
            int totalBytes = 0;
            int bytes;
            
            while (stream.DataAvailable && totalBytes < buffer.Length)
            {
                bytes = stream.Read(buffer, totalBytes, buffer.Length - totalBytes);
                totalBytes += bytes;
            }
            
            if (totalBytes == 0)
            {
                // Если данных нет, пробуем прочитать с ожиданием
                stream.ReadTimeout = 500;
                bytes = stream.Read(buffer, 0, buffer.Length);
                totalBytes = bytes;
            }
            
            string response = Encoding.UTF8.GetString(buffer, 0, totalBytes).Trim();
            
            // Берём только последнюю строку (после \n)
            string[] lines = response.Split('\n');
            string lastLine = lines[lines.Length - 1];
            
            if (!string.IsNullOrEmpty(lastLine))
            {
                var result = JsonUtility.FromJson<ActionWrapper>(lastLine);
                if (result != null)
                {
                    return result.action;
                }
            }
            
            return UnityEngine.Random.Range(0, 5);
        }
        catch (Exception e) {
            Debug.LogWarning($"RL ошибка (порт {port}): {e.Message}");
            connected = false;
            StartCoroutine(ReconnectRoutine());
            return UnityEngine.Random.Range(0, 5);
        }
    }

    public void SendExperience(float[] state, int action, float reward, float[] nextState, bool done, string unitId)
    {
        if (!connected) return;

        try {
            var exp = new StateActionReward
            {
                state = state,
                action = action,
                reward = reward,
                nextState = nextState,
                done = done,
                unitId = unitId
            };
            
            string json = JsonUtility.ToJson(exp) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            stream.Write(data, 0, data.Length);
        }
        catch (Exception e) {
            Debug.LogWarning($"Ошибка отправки опыта (порт {port}): {e.Message}");
            connected = false;
            StartCoroutine(ReconnectRoutine());
        }
    }

    public bool IsConnected()
    {
        return connected && client != null && client.Connected;
    }

    void OnDestroy()
    {
        Disconnect();
    }

    void OnApplicationQuit()
    {
        Disconnect();
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Linq;
using UnityEngine;
using System.Globalization;

public class EnemyTrainer : MonoBehaviour
{
    [Header("GA Server")]
    public string gaHost = "127.0.0.1";
    public int gaPort = 5005;

    [Header("Training Settings")]
    public int popSize = 15;
    public int totalGenerations = 40;
    public float delayBetweenBots = 0.05f;

    [Header("Debug")]
    public bool verbose = true;

    private RLAgent rlAgent;
    private bool isRunning = false;
    private int currentGeneration = 0;

    private float[] fitnessScores;
    private string[] enemyLayouts;

    void Start()
    {
        rlAgent = GetComponent<RLAgent>();
        if (rlAgent == null)
        {
            rlAgent = gameObject.AddComponent<RLAgent>();
        }
        
        fitnessScores = new float[popSize];
        enemyLayouts = new string[popSize];

        for (int i = 0; i < popSize; i++)
        {
            enemyLayouts[i] = GenerateRandomLayout();
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G) && !isRunning)
        {
            Debug.Log("Запуск обучения врага...");
            StartCoroutine(RunTrainingLoop());
        }
    }

    private IEnumerator RunTrainingLoop()
    {
        isRunning = true;

        for (int gen = 0; gen < totalGenerations; gen++)
        {
            currentGeneration = gen;
            Debug.Log($"=== ПОКОЛЕНИЕ {gen} ===");

            float[] newFitnessScores = new float[popSize];

            for (int i = 0; i < popSize; i++)
            {
                string playerLayout = GenerateRandomLayout();
                
                float fitness = FastBattleSimulator.RunSimulationWithRL(
                    playerLayout, enemyLayouts[i], rlAgent, rlAgent);

                newFitnessScores[i] = fitness;
                if (verbose) Debug.Log($"  Бот {i}: Fitness = {fitness:F2}");

                yield return new WaitForSeconds(delayBetweenBots);
            }

            fitnessScores = newFitnessScores;

            yield return StartCoroutine(RequestLayoutsForEnemy());

            float avgFit = fitnessScores.Average();
            float maxFit = fitnessScores.Max();

            Debug.Log($"Враг - Max: {maxFit:F2}, Avg: {avgFit:F2}");
        }

        Debug.Log("Обучение врага завершено!");
        isRunning = false;
    }

    private IEnumerator RequestLayoutsForEnemy()
    {
        for (int i = 0; i < popSize; i++)
        {
            string layout = ExecuteGASingleRequest(fitnessScores[i], i, enemyLayouts[i]);
            if (!string.IsNullOrEmpty(layout))
            {
                enemyLayouts[i] = layout;
            }
            yield return new WaitForSeconds(0.05f);
        }
    }

    private string ExecuteGASingleRequest(float lastFitness, int botIndex, string currentLayout)
    {
        try 
        {
            int fakeBudget = 15;
            int fakeRound = 1;
            
            float[] fullState = new float[102];
            
            string[] playerCells = currentLayout.Split(',');
            for (int i = 0; i < 25 && i < playerCells.Length; i++)
            {
                if (string.IsNullOrEmpty(playerCells[i])) continue;
                int type = int.Parse(playerCells[i].Split(':')[0]);
                fullState[i] = type > 0 ? 1f : 0f;
            }
            
            for (int i = 25; i < 50; i++) fullState[i] = 0f;
            for (int i = 50; i < 75; i++) fullState[i] = 0f;
            for (int i = 75; i < 100; i++) fullState[i] = 0f;
            
            fullState[100] = fakeBudget;
            fullState[101] = fakeRound;

            string boardString = string.Join(",", fullState.Select(f => f.ToString(CultureInfo.InvariantCulture)));
            string fitnessStr = lastFitness.ToString("F2", CultureInfo.InvariantCulture);

            string message = $"{fakeBudget}|{fitnessStr}|{boardString}\n";
            byte[] dataToSend = Encoding.UTF8.GetBytes(message);

            using (TcpClient client = new TcpClient())
            {
                var result = client.BeginConnect(gaHost, gaPort, null, null);
                if (!result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(3))) return null;
                client.EndConnect(result);

                using (NetworkStream stream = client.GetStream())
                {
                    stream.Write(dataToSend, 0, dataToSend.Length);
                    stream.Flush();

                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();
                    
                    if (verbose) Debug.Log($"Получен ответ от GA: {response}");
                    return response;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GA ошибка бот {botIndex}: {e.Message}");
        }
        return null;
    }

    private string GenerateRandomLayout()
    {
        string[] cells = new string[25];
        for (int i = 0; i < 25; i++) cells[i] = "0:0";
        
        int numUnits = UnityEngine.Random.Range(3, 8);
        for (int u = 0; u < numUnits; u++)
        {
            int idx = UnityEngine.Random.Range(0, 25);
            int type = UnityEngine.Random.Range(1, 4);
            cells[idx] = $"{type}:1";
        }
        
        return string.Join(",", cells);
    }
}
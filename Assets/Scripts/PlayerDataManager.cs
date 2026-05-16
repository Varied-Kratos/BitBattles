using UnityEngine;

public class PlayerDataManager : MonoBehaviour
{
    public static PlayerDataManager instance;

    [Header("Player Stats")]
    public string playerName = "Alan";
    public int trophies = 0;
    public int wins = 0;
    public int losses = 0;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadData(); // Загружаем данные при старте
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SaveData()
    {
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("Trophies", trophies);
        PlayerPrefs.SetInt("Wins", wins);
        PlayerPrefs.SetInt("Losses", losses);
        PlayerPrefs.Save();
    }

    public void LoadData()
    {
        playerName = PlayerPrefs.GetString("PlayerName", "Alan");
        trophies = PlayerPrefs.GetInt("Trophies", 0);
        wins = PlayerPrefs.GetInt("Wins", 0);
        losses = PlayerPrefs.GetInt("Losses", 0);
    }

    public void ResetData()
    {
        
        PlayerPrefs.DeleteAll(); 
        
        
        playerName = "Alan";
        trophies = 0;
        wins = 0;
        losses = 0;

        
        SaveData();
        
        Debug.Log("Данные игрока полностью сброшены!");
    }
}
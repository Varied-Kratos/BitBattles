using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public GameObject profilePanel;
    public GameObject settingsPanel;

    [Header("UI Text References")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI trophyText;
    public TextMeshProUGUI winsText;
    public TextMeshProUGUI lossesText;

    [Header("Editing Name")]
    public TMP_InputField nameInputField;

    void Start()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayMenuMusic();
            AudioManager.Instance.UpdateMusicVolume();
        }

        // Находим панели, если ссылки потерялись
        if (settingsPanel == null)
            settingsPanel = GameObject.Find("SettingsPanel");
        if (profilePanel == null)
            profilePanel = GameObject.Find("ProfilePanel");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (profilePanel != null)
            profilePanel.SetActive(false);
    }

    public void ToggleSettings()
    {
        // Если ссылка потерялась — ищем
        if (settingsPanel == null)
            settingsPanel = GameObject.Find("SettingsPanel");

        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }

    public void CloseSettings()
    {
        if (settingsPanel == null)
            settingsPanel = GameObject.Find("SettingsPanel");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void OpenProfile()
    {
        if (profilePanel == null)
            profilePanel = GameObject.Find("ProfilePanel");

        UpdateProfileUI();
        if (profilePanel != null) profilePanel.SetActive(true);
    }

    public void CloseProfile()
    {
        if (profilePanel == null)
            profilePanel = GameObject.Find("ProfilePanel");

        if (profilePanel != null) profilePanel.SetActive(false);
    }

    public void ClickChangeName()
    {
        nameInputField.gameObject.SetActive(true);
        nameInputField.text = PlayerDataManager.instance.playerName;
        nameInputField.ActivateInputField();
    }

    public void OnEndEditName(string newName)
    {
        if (!string.IsNullOrEmpty(newName))
        {
            PlayerDataManager.instance.playerName = newName;
            PlayerDataManager.instance.SaveData();
            UpdateProfileUI();
        }
        nameInputField.gameObject.SetActive(false);
    }

    public void UpdateProfileUI()
    {
        if (PlayerDataManager.instance != null)
        {
            var data = PlayerDataManager.instance;
            nameText.text = data.playerName;
            trophyText.text = data.trophies.ToString();
            winsText.text = data.wins.ToString();
            lossesText.text = data.losses.ToString();
        }
    }

    public void PlayGame()
    {
        AudioManager.Instance.PlayButton();
        SceneManager.LoadScene("BattleScene");
    }

    public void QuitGame()
    {
        Debug.Log("Выход из игры");
        Application.Quit();
    }

    public void ClickNewPlayer()
    {
        if (PlayerDataManager.instance != null)
        {
            PlayerDataManager.instance.ResetData();
            UpdateProfileUI();
        }
    }
}
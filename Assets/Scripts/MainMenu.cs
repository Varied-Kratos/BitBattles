using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenu : MonoBehaviour
{
    public GameObject profilePanel;

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
            AudioManager.Instance.PlayMenuMusic();
    }
    public void ClickChangeName()
    {
        nameInputField.gameObject.SetActive(true); // Показываем поле
        nameInputField.text = PlayerDataManager.instance.playerName; // Вставляем текущее имя
        nameInputField.ActivateInputField(); // Сразу ставим курсор для печати
    }

    public void OnEndEditName(string newName)
    {
        if (!string.IsNullOrEmpty(newName))
        {
            PlayerDataManager.instance.playerName = newName;
            PlayerDataManager.instance.SaveData(); // Сохраняем в память
            UpdateProfileUI(); // Обновляем текст в профиле
        }
        nameInputField.gameObject.SetActive(false); // Прячем поле обратно
    }

    public void UpdateProfileUI()
    {
        // Проверяем, существует ли менеджер данных
        if (PlayerDataManager.instance != null)
        {
            var data = PlayerDataManager.instance;

            nameText.text = data.playerName;
            trophyText.text = data.trophies.ToString();
            winsText.text = data.wins.ToString();
            lossesText.text = data.losses.ToString();
        }
    }

    public void OpenProfile()
    {
        UpdateProfileUI(); // Обновляем текст каждый раз перед открытием
        if (profilePanel != null) profilePanel.SetActive(true);
    }

    public void CloseProfile()
    {
        if (profilePanel != null) profilePanel.SetActive(false);
    }
    public void PlayGame()
    {
        AudioManager.Instance.PlayButton(); // ← ЗВУК КНОПКИ
        SceneManager.LoadScene("BattleScene");
    }


    public void QuitGame()
    {
        Debug.Log("����� �� ����");
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
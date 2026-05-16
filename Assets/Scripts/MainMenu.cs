using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public GameObject profilePanel;
    public void PlayGame()
    {
        SceneManager.LoadScene("BattleScene");
    }

    public void OpenProfile()
    {
        if (profilePanel != null)
        {
            profilePanel.SetActive(true);
        }
    }

    // Метод для кнопки "НАЗАД" в самом профиле
    public void CloseProfile()
    {
        if (profilePanel != null)
        {
            profilePanel.SetActive(false);
        }
    }

    public void QuitGame()
    {
        Debug.Log("����� �� ����");
        Application.Quit();
    }
}
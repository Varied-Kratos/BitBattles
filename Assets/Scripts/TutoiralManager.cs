using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class TutorialSceneManager : MonoBehaviour
{
    public GameObject loadingScreen;      // Один фон загрузки
    public Slider loadingBar;             // Слайдер на нём
    public GameObject[] tutorialSlides;   // 4 обучалки
    public Button nextButton;
    public Button skipButton;

    private int currentSlide = -1;
    private bool canSwitch = true;

    void Start()
    {
        // Скрываем обучающие слайды
        for (int i = 0; i < tutorialSlides.Length; i++)
        {
            tutorialSlides[i].SetActive(false);
            CanvasGroup cg = tutorialSlides[i].GetComponent<CanvasGroup>();
            if (cg == null) cg = tutorialSlides[i].AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }

        // Кнопки скрыты
        if (nextButton != null) nextButton.gameObject.SetActive(false);
        if (skipButton != null) skipButton.gameObject.SetActive(false);

        // Показываем загрузку
        if (loadingScreen != null) loadingScreen.SetActive(true);
        if (loadingBar != null) loadingBar.value = 0f;

        StartCoroutine(LoadingSequence());
    }

    IEnumerator LoadingSequence()
    {
        float totalDuration = 5f;
        float elapsed = 0f;
        int lastPercent = 0;

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;
            int currentPercent = Mathf.FloorToInt((elapsed / totalDuration) * 100);

            // Округляем вниз до ближайших 5%
            currentPercent = (currentPercent / 5) * 5;

            // Если процент изменился — обновляем слайдер
            if (currentPercent != lastPercent && currentPercent <= 100)
            {
                lastPercent = currentPercent;
                if (loadingBar != null)
                    loadingBar.value = currentPercent / 100f;
            }

            yield return null;
        }

        // 100% в конце
        if (loadingBar != null) loadingBar.value = 1f;
        yield return new WaitForSeconds(0.5f);

        // Скрываем загрузку
        if (loadingScreen != null) loadingScreen.SetActive(false);

        // Кнопки
        if (nextButton != null) nextButton.gameObject.SetActive(true);
        if (skipButton != null) skipButton.gameObject.SetActive(true);

        if (nextButton != null) nextButton.onClick.AddListener(NextSlide);
        if (skipButton != null) skipButton.onClick.AddListener(SkipTutorial);

        ShowSlide(0);
    }

    void ShowSlide(int index)
    {
        if (currentSlide >= 0 && currentSlide < tutorialSlides.Length && tutorialSlides[currentSlide] != null)
            tutorialSlides[currentSlide].SetActive(false);

        currentSlide = index;

        if (tutorialSlides[currentSlide] != null)
        {
            tutorialSlides[currentSlide].SetActive(true);
            StartCoroutine(FadeIn(tutorialSlides[currentSlide]));
        }

        if (nextButton != null)
        {
            var tmp = nextButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = (index == tutorialSlides.Length - 1) ? "В БОЙ" : "ДАЛЕЕ";
        }
    }

    IEnumerator FadeIn(GameObject slide)
    {
        CanvasGroup cg = slide.GetComponent<CanvasGroup>();
        if (cg == null) yield break;
        cg.alpha = 0f;
        float t = 0f;
        while (t < 0.4f) { t += Time.deltaTime; cg.alpha = t / 0.4f; yield return null; }
        cg.alpha = 1f;
    }

    public void NextSlide()
    {
        if (!canSwitch) return;
        if (currentSlide < tutorialSlides.Length - 1)
            StartCoroutine(SwitchWithDelay());
        else
            PlayGame();
    }

    IEnumerator SwitchWithDelay()
    {
        canSwitch = false;
        yield return new WaitForSeconds(0.3f);
        ShowSlide(currentSlide + 1);
        yield return new WaitForSeconds(0.2f);
        canSwitch = true;
    }

    public void SkipTutorial() { PlayGame(); }

    void PlayGame()
    {
        string playerName = PlayerDataManager.instance?.playerName ?? "Player";
        PlayerPrefs.SetInt("TutorialSeen_" + playerName, 1);
        PlayerPrefs.Save();
        SceneManager.LoadScene("BattleScene");
    }
}
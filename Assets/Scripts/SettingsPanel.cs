using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
    public static SettingsPanel Instance { get; private set; }

    public Slider musicSlider;
    public Slider sfxSlider;
    public GameObject panel;
    public AudioManager audioManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (panel != null)
            panel.SetActive(false);

        if (musicSlider != null)
        {
            musicSlider.value = PlayerPrefs.GetFloat("MusicVolume", 0.3f);
            musicSlider.onValueChanged.AddListener(v =>
            {
                if (audioManager != null)
                {
                    audioManager.musicVolume = v;
                    audioManager.UpdateMusicVolume();
                }
                PlayerPrefs.SetFloat("MusicVolume", v);
            });
        }

        if (sfxSlider != null)
        {
            sfxSlider.value = PlayerPrefs.GetFloat("SFXVolume", 0.7f);
            sfxSlider.onValueChanged.AddListener(v =>
            {
                if (audioManager != null)
                {
                    audioManager.uiVolume = v;
                    audioManager.battleVolume = v;
                    audioManager.gameVolume = v;
                }
                PlayerPrefs.SetFloat("SFXVolume", v);
            });
        }
    }

    public void Toggle()
    {
        if (panel != null)
            panel.SetActive(!panel.activeSelf);
    }

    public void Close()
    {
        if (panel != null)
            panel.SetActive(false);
    }
}
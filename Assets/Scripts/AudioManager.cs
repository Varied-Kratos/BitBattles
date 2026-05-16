using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Volume Settings")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float uiVolume = 0.7f;
    [Range(0f, 1f)] public float battleVolume = 0.5f;
    [Range(0f, 1f)] public float gameVolume = 0.6f;

    [Header("UI Sounds")]
    public AudioClip buttonClick;
    public AudioClip unitPlace;
    public AudioClip unitSell;
    public AudioClip unitBuy;
    public AudioClip coinEarn;

    [Header("Battle Sounds")]
    public AudioClip knightAttack;
    public AudioClip archerAttack;
    public AudioClip mageAttack;
    public AudioClip damageTaken;
    public AudioClip unitDeath;
    public AudioClip unitFusion;

    [Header("Game Sounds")]
    public AudioClip victory;
    public AudioClip defeat;
    public AudioClip roundStart;

    private AudioSource mAudioSource;

    [Header("Music")]
    public AudioClip menuMusic;
    public AudioClip battleMusic;
    public AudioClip victoryMusic;
    public AudioClip defeatMusic;
    [Range(0f, 1f)] public float musicVolume = 0.3f;

    [Header("Round Sounds")]
    public AudioClip roundWin;
    public AudioClip roundLose;
    [Range(0f, 1f)] public float roundVolume = 0.8f;
    public void PlayRoundWin() => PlaySound(roundWin, gameVolume);
    public void PlayRoundLose() => PlaySound(roundLose, gameVolume);

    private AudioSource mMusicSource; // Отдельный источник для музыки
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

        // Для звуков
        mAudioSource = gameObject.AddComponent<AudioSource>();
        mAudioSource.playOnAwake = false;

        // Для музыки (отдельный источник)
        mMusicSource = gameObject.AddComponent<AudioSource>();
        mMusicSource.loop = true; // Зацикливаем
        mMusicSource.playOnAwake = false;
        mMusicSource.volume = musicVolume;
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip != null)
            mAudioSource.PlayOneShot(clip, volume * masterVolume);
    }

    // UI
    public void PlayButton() => PlaySound(buttonClick, uiVolume);
    public void PlayPlace() => PlaySound(unitPlace, uiVolume);
    public void PlaySell() => PlaySound(unitSell, uiVolume);
    public void PlayBuy() => PlaySound(unitBuy, uiVolume);
    public void PlayCoin() => PlaySound(coinEarn, uiVolume);

    // Battle
    public void PlayKnightAttack() => PlaySound(knightAttack, battleVolume);
    public void PlayArcherAttack() => PlaySound(archerAttack, battleVolume);
    public void PlayMageAttack() => PlaySound(mageAttack, battleVolume);
    public void PlayDamage() => PlaySound(damageTaken, battleVolume);
    public void PlayDeath() => PlaySound(unitDeath, battleVolume);
    public void PlayFusion() => PlaySound(unitFusion, battleVolume);

    // Game
    public void PlayVictory() => PlaySound(victory, gameVolume);
    public void PlayDefeat() => PlaySound(defeat, gameVolume);
    public void PlayRoundStart() => PlaySound(roundStart, gameVolume);

    public void PlayMenuMusic()
    {
        PlayMusic(menuMusic);
    }

    public void PlayBattleMusic()
    {
        PlayMusic(battleMusic);
    }

    public void PlayVictoryMusic()
    {
        PlayMusicOnce(victoryMusic);
    }

    public void PlayDefeatMusic()
    {
        PlayMusicOnce(defeatMusic);
    }

    private void PlayMusic(AudioClip clip)
    {
        if (mMusicSource.clip == clip) return; // Уже играет

        mMusicSource.clip = clip;
        mMusicSource.volume = musicVolume * masterVolume;
        mMusicSource.loop = true;
        mMusicSource.Play();
    }

    private void PlayMusicOnce(AudioClip clip)
    {
        mMusicSource.loop = false;
        mMusicSource.clip = clip;
        mMusicSource.volume = musicVolume * masterVolume;
        mMusicSource.Play();
    }

    public void StopMusic()
    {
        mMusicSource.Stop();
    }
}
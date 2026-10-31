using UnityEngine;

/// <summary>
/// Музика й загальні звуки: спіймали, рівень пройдено, не вистачає енергії, взаємодія.
/// Будь-який скрипт може програти звук через GameAudio.Play(clip).
/// chaseMusic - шар погоні тієї ж довжини й темпу, що й music: грає синхронно і плавно
/// з'являється, коли хтось із ворогів женеться за привидом.
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [SerializeField] AudioClip music;
    [Tooltip("Шар погоні: та сама довжина й темп, що й music")]
    [SerializeField] AudioClip chaseMusic;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] float chaseVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] float sfxVolume = 0.8f;

    [SerializeField] AudioClip caughtClip;
    [SerializeField] AudioClip levelCompleteClip;
    [SerializeField] AudioClip noEnergyClip;
    [SerializeField] AudioClip interactClip;

    AudioSource musicSource;
    AudioSource chaseSource;
    AudioSource sfxSource;
    GuardAI[] enemies = new GuardAI[0];
    float chaseLevel;
    bool ended;
    PlayerInteractor interactor;

    float baseMusic, baseChase, baseSfx;

    void Awake()
    {
        Instance = this;
        baseMusic = musicVolume;
        baseChase = chaseVolume;
        baseSfx = sfxVolume;
        // гучність з меню «Налаштування»
        musicVolume = baseMusic * GameSettings.MusicVolume;
        chaseVolume = baseChase * GameSettings.MusicVolume;
        sfxVolume = baseSfx * GameSettings.SfxVolume;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
        musicSource.clip = music;
        if (music != null) musicSource.Play();

        chaseSource = gameObject.AddComponent<AudioSource>();
        chaseSource.loop = true;
        chaseSource.playOnAwake = false;
        chaseSource.volume = 0f;
        chaseSource.clip = chaseMusic;
        if (chaseMusic != null) chaseSource.Play();

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        sfxSource.volume = sfxVolume;
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver += HandleGameOver;
            GameManager.Instance.OnLevelComplete += HandleLevelComplete;
        }

        enemies = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            interactor.OnNotEnoughEnergy += HandleNoEnergy;
            interactor.OnInteracted += HandleInteracted;
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnLevelComplete -= HandleLevelComplete;
        }
        if (interactor != null)
        {
            interactor.OnNotEnoughEnergy -= HandleNoEnergy;
            interactor.OnInteracted -= HandleInteracted;
        }
    }

    void Update()
    {
        if (chaseMusic == null) return;

        bool chasing = false;
        if (!ended)
            foreach (var e in enemies)
                if (e != null && e.IsChasing) { chasing = true; break; }

        // швидко наростає, повільно стихає
        chaseLevel = Mathf.MoveTowards(chaseLevel, chasing ? 1f : 0f, Time.deltaTime / (chasing ? 0.4f : 2.5f));
        chaseSource.volume = chaseVolume * chaseLevel;

        // тримаємо шари в такт
        if (music != null && chaseSource.clip.samples == music.samples &&
            Mathf.Abs(chaseSource.timeSamples - musicSource.timeSamples) > 2048)
            chaseSource.timeSamples = musicSource.timeSamples;
    }

    /// <summary>Застосувати гучність з налаштувань (викликає пауза, коли крутять повзунки).</summary>
    public static void RefreshVolumes()
    {
        var a = Instance;
        if (a == null) return;
        a.musicVolume = a.baseMusic * GameSettings.MusicVolume;
        a.chaseVolume = a.baseChase * GameSettings.MusicVolume;
        a.sfxVolume = a.baseSfx * GameSettings.SfxVolume;
        if (!a.ended) a.musicSource.volume = a.musicVolume;
        a.sfxSource.volume = a.sfxVolume;
    }

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (Instance != null && clip != null) Instance.sfxSource.PlayOneShot(clip, volume);
    }

    void HandleGameOver()
    {
        ended = true;
        musicSource.volume = musicVolume * 0.3f;
        Play(caughtClip);
    }

    void HandleLevelComplete()
    {
        ended = true;
        musicSource.volume = musicVolume * 0.3f;
        Play(levelCompleteClip);
    }

    void HandleNoEnergy(Interactable _) => Play(noEnergyClip, 0.8f);
    void HandleInteracted(Interactable _) => Play(interactClip, 0.6f);
}

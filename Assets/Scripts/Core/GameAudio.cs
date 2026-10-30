using UnityEngine;

/// <summary>
/// Музика й загальні звуки: спіймали, рівень пройдено, не вистачає енергії, взаємодія.
/// Будь-який скрипт може програти звук через GameAudio.Play(clip).
/// </summary>
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance { get; private set; }

    [SerializeField] AudioClip music;
    [Range(0f, 1f)] [SerializeField] float musicVolume = 0.35f;
    [Range(0f, 1f)] [SerializeField] float sfxVolume = 0.8f;

    [SerializeField] AudioClip caughtClip;
    [SerializeField] AudioClip levelCompleteClip;
    [SerializeField] AudioClip noEnergyClip;
    [SerializeField] AudioClip interactClip;

    AudioSource musicSource;
    AudioSource sfxSource;
    PlayerInteractor interactor;

    void Awake()
    {
        Instance = this;

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = musicVolume;
        musicSource.clip = music;
        if (music != null) musicSource.Play();

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

    public static void Play(AudioClip clip, float volume = 1f)
    {
        if (Instance != null && clip != null) Instance.sfxSource.PlayOneShot(clip, volume);
    }

    void HandleGameOver()
    {
        musicSource.volume = musicVolume * 0.3f;
        Play(caughtClip);
    }

    void HandleLevelComplete()
    {
        musicSource.volume = musicVolume * 0.3f;
        Play(levelCompleteClip);
    }

    void HandleNoEnergy(Interactable _) => Play(noEnergyClip, 0.8f);
    void HandleInteracted(Interactable _) => Play(interactClip, 0.6f);
}

using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Керує станом рівня: програш (привида спіймали) і перемога (вилетів у відчинені двері).
/// Один об'єкт "GameManager" з цим скриптом лежить у кожній сцені рівня.
///
/// Ворог при спійманні викликає:       GameManager.Instance.GameOver();
/// Вихід із рівня (LevelExit) викликає: GameManager.Instance.CompleteLevel();
/// R - почати рівень заново, Esc - пауза (PauseMenu).
/// Між рівнями - LevelTransition: привид летить тунелем, поки вантажиться наступна сцена.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Рівень")]
    [SerializeField] int levelNumber;
    [SerializeField] string levelTitle = "";
    [TextArea] [SerializeField] string levelHint = "";

    [Header("Наступна сцена (заповнює збирач гри)")]
    [SerializeField] string nextTitle = "";
    [SerializeField] string nextName = "";

    [Header("Переходи")]
    [Tooltip("Скільки секунд показувати \"тебе спіймали\" перед перезапуском")]
    [SerializeField] float caughtDelay = 1.8f;
    [Tooltip("Для тестових сцен: після виходу перезапускати цю ж сцену, а не вантажити наступну")]
    [SerializeField] bool restartInsteadOfNext;

    public bool IsLevelOver { get; private set; }
    public float LevelTime { get; private set; }
    public int LevelNumber => levelNumber;
    public string LevelTitle => levelTitle;
    public string LevelHint => levelHint;

    public event Action OnGameOver;
    public event Action OnLevelComplete;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (PauseMenu.IsPaused || LevelTransition.Busy) return;

        if (!IsLevelOver)
        {
            LevelTime += Time.deltaTime;
            GameProgress.RunTime += Time.deltaTime;
        }

        if (GameInput.RestartPressed && !IsLevelOver) RestartLevel();
    }

    public void GameOver()
    {
        if (IsLevelOver) return;
        IsLevelOver = true;
        GameProgress.TimesCaught++;
        OnGameOver?.Invoke();
        Invoke(nameof(RestartLevel), caughtDelay);
    }

    public void CompleteLevel()
    {
        if (IsLevelOver) return;
        IsLevelOver = true;
        FreezePlayer();
        OnLevelComplete?.Invoke();
        Invoke(restartInsteadOfNext ? nameof(RestartLevel) : nameof(LoadNextLevel), 0.7f);
    }

    /// <summary>Привид завмирає у дверях, щоб не пролітати крізь стіну, поки йде перехід.</summary>
    void FreezePlayer()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm == null) return;
        pm.enabled = false;
        var fpm = pm.GetComponent<FirstPersonMovement>();
        if (fpm != null) fpm.Freeze();
        var interactor = pm.GetComponent<PlayerInteractor>();
        if (interactor != null) interactor.enabled = false;
        var rb = pm.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector2.zero;
#else
            rb.velocity = Vector2.zero;
#endif
            rb.simulated = false;
        }
    }

    public void RestartLevel()
    {
        CancelInvoke();
        LevelTransition.Go(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Наступна сцена за списком у File > Build Profiles (наступний рівень або фінальна сцена).
    /// Якщо далі нічого немає - назад у меню з екраном перемоги.
    /// </summary>
    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings)
        {
            // Сюжетний поворот має з'явитися до назви й тунелю п'ятого рівня.
            if (SceneUtility.GetScenePathByBuildIndex(next).EndsWith("/Twist.unity", StringComparison.OrdinalIgnoreCase))
            {
                LevelTransition.Go(next);
                return;
            }
            var frames = GhostSkin.Current != null ? GhostSkin.Current.MoveFrames : null;
            LevelTransition.Go(next, nextTitle, nextName, frames);
            return;
        }
        GameProgress.JustWon = true;
        LevelTransition.Go(0);
    }
}

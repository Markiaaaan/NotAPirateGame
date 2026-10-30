using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Керує станом рівня: програш (привида спіймали) і перемога (дійшов до виходу).
/// Один об'єкт "GameManager" з цим скриптом лежить у кожній сцені рівня.
///
/// Сторож при спійманні викликає:      GameManager.Instance.GameOver();
/// Вихід із рівня (LevelExit) викликає: GameManager.Instance.CompleteLevel();
/// R - почати рівень заново, Esc - вийти в меню.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Рівень")]
    [SerializeField] string levelTitle = "";
    [TextArea] [SerializeField] string levelHint = "";

    [Header("Переходи")]
    [Tooltip("Пауза перед перезапуском або наступним рівнем, щоб гравець побачив, що сталося")]
    [SerializeField] float transitionDelay = 1.5f;
    [Tooltip("Для тестових сцен: після виходу перезапускати цю ж сцену, а не вантажити наступну")]
    [SerializeField] bool restartInsteadOfNext;

    public bool IsLevelOver { get; private set; }
    public float LevelTime { get; private set; }
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
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (!IsLevelOver)
        {
            LevelTime += Time.deltaTime;
            GameProgress.RunTime += Time.deltaTime;
        }

        if (GameInput.RestartPressed && !IsLevelOver) RestartLevel();
        if (GameInput.BackPressed) SceneManager.LoadScene(0);
    }

    public void GameOver()
    {
        if (IsLevelOver) return;
        IsLevelOver = true;
        GameProgress.TimesCaught++;
        OnGameOver?.Invoke();
        Invoke(nameof(RestartLevel), transitionDelay);
    }

    public void CompleteLevel()
    {
        if (IsLevelOver) return;
        IsLevelOver = true;
        OnLevelComplete?.Invoke();
        Invoke(restartInsteadOfNext ? nameof(RestartLevel) : nameof(LoadNextLevel), transitionDelay);
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// Наступна сцена за списком у File > Build Profiles. Якщо наступної сцени-рівня (Level_...) немає,
    /// гру пройдено: повертаємось у меню (сцена 0) з екраном перемоги.
    /// </summary>
    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings && IsLevelScene(next))
        {
            SceneManager.LoadScene(next);
            return;
        }
        GameProgress.JustWon = true;
        SceneManager.LoadScene(0);
    }

    static bool IsLevelScene(int buildIndex)
    {
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        return Path.GetFileNameWithoutExtension(path).StartsWith("Level");
    }
}

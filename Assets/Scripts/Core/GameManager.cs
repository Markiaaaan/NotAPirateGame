using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Керує станом рівня: програш (привида спіймали) і перемога (дійшов до виходу).
/// Поклади один порожній об'єкт "GameManager" з цим скриптом у КОЖНУ сцену рівня.
///
/// Охоронець при спійманні викликає:   GameManager.Instance.GameOver();
/// Вихід із рівня (LevelExit) викликає: GameManager.Instance.CompleteLevel();
/// UI може підписатися на OnGameOver / OnLevelComplete, щоб показати екран.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Tooltip("Пауза перед перезапуском або наступним рівнем, щоб гравець побачив, що сталося")]
    [SerializeField] float transitionDelay = 1.5f;
    [Tooltip("Для тестових сцен: після виходу перезапускати цю ж сцену, а не вантажити наступну")]
    [SerializeField] bool restartInsteadOfNext;

    public bool IsLevelOver { get; private set; }

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

    public void GameOver()
    {
        if (IsLevelOver) return;
        IsLevelOver = true;
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

    /// <summary>Наступна сцена за списком у File > Build Settings. Після останньої повертає на першу (меню).</summary>
    public void LoadNextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        SceneManager.LoadScene(next < SceneManager.sceneCountInBuildSettings ? next : 0);
    }
}

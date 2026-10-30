using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Тимчасовий інтерфейс для тестування: енергія, видимість, підказка "[E] ..." і журнал подій.
/// Малюється через OnGUI, тому не потребує Canvas. Потім замінимо справжнім UI.
/// </summary>
public class DebugHUD : MonoBehaviour
{
    [SerializeField] int fontSize = 18;
    [SerializeField] int maxLogLines = 6;

    PlayerInteractor interactor;
    PlayerEnergy energy;
    GhostVisibility ghost;
    readonly List<string> log = new List<string>();
    GUIStyle label;

    void OnEnable()
    {
        LureEvents.OnLure += HandleLure;
    }

    void OnDisable()
    {
        LureEvents.OnLure -= HandleLure;
        if (interactor != null) interactor.OnNotEnoughEnergy -= HandleNoEnergy;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelComplete -= HandleLevelComplete;
            GameManager.Instance.OnGameOver -= HandleGameOver;
        }
    }

    void Start()
    {
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            energy = interactor.GetComponent<PlayerEnergy>();
            ghost = interactor.GetComponent<GhostVisibility>();
            interactor.OnNotEnoughEnergy += HandleNoEnergy;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelComplete += HandleLevelComplete;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }
    }

    void Update()
    {
        if (GameInput.DebugCaughtPressed && GameManager.Instance != null) GameManager.Instance.GameOver();
    }

    void HandleLure(LureSignal s)
    {
        string who = s.Source != null ? s.Source.name : "?";
        Add($"{who}: радіус {s.Loudness:0.#}, триває {s.Duration:0.#} с, підозра +{s.Suspicion:0.##}");
    }

    void HandleNoEnergy(Interactable target) => Add($"Не вистачає енергії: {target.Prompt}");
    void HandleLevelComplete() => Add("Рівень пройдено! Перезапуск...");
    void HandleGameOver() => Add("Тебе спіймали!");

    void Add(string message)
    {
        Debug.Log(message);
        log.Add(message);
        if (log.Count > maxLogLines) log.RemoveAt(0);
    }

    void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = fontSize };
            label.normal.textColor = Color.white;
        }

        GUILayout.BeginArea(new Rect(12, 12, 640, 420));
        GUILayout.Label("WASD - рух,  E - взаємодія,  K - тест: тебе спіймали", label);

        if (energy != null)
        {
            GUILayout.Label($"Енергія: {energy.Current:0} / {energy.Max:0}", label);
            DrawBar(energy.Normalized, new Color(0.4f, 0.9f, 1f));
        }

        if (ghost != null)
        {
            string seen = ghost.IsVisible ? "   ТЕБЕ ВИДНО!" : "";
            GUILayout.Label($"Видимість: {ghost.Visibility:0.00}{seen}", label);
            DrawBar(ghost.Visibility, new Color(1f, 0.85f, 0.3f));
        }

        GUILayout.Space(10);
        foreach (var line in log) GUILayout.Label(line, label);
        GUILayout.EndArea();

        if (interactor != null && interactor.Current != null)
        {
            var target = interactor.Current;
            string text = $"[E] {target.Prompt}   ({target.EnergyCost:0} енергії)";
            Vector2 size = label.CalcSize(new GUIContent(text));
            GUI.Label(new Rect((Screen.width - size.x) / 2f, Screen.height - size.y - 40f, size.x, size.y), text, label);
        }
    }

    void DrawBar(float value, Color color)
    {
        Rect r = GUILayoutUtility.GetRect(260, 14, GUILayout.Width(260));
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = color;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

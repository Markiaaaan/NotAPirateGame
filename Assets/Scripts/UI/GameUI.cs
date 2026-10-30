using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Інтерфейс рівня: енергія, підозра охорони, підказка "[E] ...", назва рівня на старті,
/// час і великі написи "ТЕБЕ СПІЙМАЛИ" / "ВТІК!". Будується кодом при старті сцени.
/// </summary>
public class GameUI : MonoBehaviour
{
    [SerializeField] Sprite energyIcon;
    [SerializeField] Sprite eyeIcon;
    [SerializeField] float titleShowTime = 4.5f;

    static readonly Color EnergyColor = new Color(0.45f, 0.92f, 1f);
    static readonly Color CalmColor = new Color(0.55f, 0.85f, 0.45f);
    static readonly Color AlarmColor = new Color(1f, 0.3f, 0.3f);

    RectTransform energyFill, suspicionFill;
    Image suspicionImage;
    Text promptText, centerText, titleText, hintText, timerText, alertText, energyLabel;
    float titleTimer, centerTimer, alertBlink, noEnergyFlash;

    PlayerInteractor interactor;
    PlayerEnergy energy;

    void Awake()
    {
        Build();
    }

    void Start()
    {
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            energy = interactor.Energy;
            interactor.OnNotEnoughEnergy += HandleNoEnergy;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnGameOver += HandleGameOver;
            gm.OnLevelComplete += HandleLevelComplete;
            titleText.text = gm.LevelTitle;
            hintText.text = gm.LevelHint;
        }
        titleTimer = titleShowTime;

        if (SuspicionMeter.Instance != null) SuspicionMeter.Instance.OnAlert += HandleAlert;
    }

    void OnDestroy()
    {
        if (interactor != null) interactor.OnNotEnoughEnergy -= HandleNoEnergy;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnLevelComplete -= HandleLevelComplete;
        }
        if (SuspicionMeter.Instance != null) SuspicionMeter.Instance.OnAlert -= HandleAlert;
    }

    void Build()
    {
        var canvas = UIFactory.CreateCanvas(transform, "HUD");
        var root = canvas.transform;
        Vector2 topLeft = new Vector2(0f, 1f), top = new Vector2(0.5f, 1f), topRight = new Vector2(1f, 1f);
        Vector2 center = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f), bottomRight = new Vector2(1f, 0f);

        // Енергія
        UIFactory.MakeImage(root, "EnergyIcon", topLeft, new Vector2(30f, -24f), new Vector2(56f, 56f), Color.white, energyIcon);
        energyFill = UIFactory.MakeBar(root, "EnergyBar", topLeft, new Vector2(98f, -36f), new Vector2(340f, 30f), EnergyColor, out _);
        energyLabel = UIFactory.MakeText(root, "EnergyLabel", "ЕНЕРГІЯ", 20, topLeft, new Vector2(104f, -68f),
            new Vector2(340f, 30f), TextAnchor.UpperLeft, new Color(0.8f, 0.95f, 1f));

        // Підозра
        UIFactory.MakeImage(root, "EyeIcon", topLeft, new Vector2(30f, -104f), new Vector2(56f, 56f), Color.white, eyeIcon);
        suspicionFill = UIFactory.MakeBar(root, "SuspicionBar", topLeft, new Vector2(98f, -116f), new Vector2(340f, 30f), CalmColor, out suspicionImage);
        UIFactory.MakeText(root, "SuspicionLabel", "ПІДОЗРА ОХОРОНИ", 20, topLeft, new Vector2(104f, -148f),
            new Vector2(340f, 30f), TextAnchor.UpperLeft, new Color(1f, 0.9f, 0.9f));
        alertText = UIFactory.MakeText(root, "Alert", "ТРИВОГА!", 34, topLeft, new Vector2(456f, -110f),
            new Vector2(300f, 50f), TextAnchor.UpperLeft, AlarmColor);
        alertText.enabled = false;

        // Час і керування
        timerText = UIFactory.MakeText(root, "Timer", "0:00", 34, topRight, new Vector2(-36f, -28f),
            new Vector2(300f, 50f), TextAnchor.UpperRight, Color.white);
        UIFactory.MakeText(root, "Controls", "WASD - летіти    E - приманка    R - заново    Esc - меню", 20, bottomRight,
            new Vector2(-30f, 20f), new Vector2(900f, 30f), TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.6f));

        // Підказка взаємодії
        promptText = UIFactory.MakeText(root, "Prompt", "", 34, bottom, new Vector2(0f, 90f),
            new Vector2(1200f, 60f), TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.6f));

        // Назва рівня
        titleText = UIFactory.MakeText(root, "Title", "", 64, top, new Vector2(0f, -170f),
            new Vector2(1600f, 90f), TextAnchor.MiddleCenter, Color.white);
        hintText = UIFactory.MakeText(root, "Hint", "", 30, top, new Vector2(0f, -260f),
            new Vector2(1300f, 100f), TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 1f));

        // Великий напис посередині
        centerText = UIFactory.MakeText(root, "Center", "", 96, center, Vector2.zero,
            new Vector2(1600f, 200f), TextAnchor.MiddleCenter, Color.white);
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (energy != null)
        {
            UIFactory.SetBar(energyFill, energy.Normalized);
            noEnergyFlash = Mathf.Max(0f, noEnergyFlash - dt);
            energyLabel.color = noEnergyFlash > 0f && Mathf.Repeat(noEnergyFlash, 0.2f) > 0.1f
                ? AlarmColor : new Color(0.8f, 0.95f, 1f);
        }

        float s = SuspicionMeter.Level;
        UIFactory.SetBar(suspicionFill, s);
        suspicionImage.color = Color.Lerp(CalmColor, AlarmColor, s);
        bool alarm = SuspicionMeter.Instance != null && SuspicionMeter.Instance.IsAlert;
        alertBlink += dt;
        alertText.enabled = alarm && Mathf.Repeat(alertBlink, 0.6f) < 0.4f;

        if (interactor != null && interactor.Current != null)
        {
            var c = interactor.Current;
            promptText.text = $"[E]  {c.Prompt}   ({c.EnergyCost:0} енергії)";
        }
        else promptText.text = "";

        if (GameManager.Instance != null) timerText.text = GameProgress.FormatTime(GameManager.Instance.LevelTime);

        titleTimer -= dt;
        float titleAlpha = Mathf.Clamp01(titleTimer);
        SetAlpha(titleText, titleAlpha);
        SetAlpha(hintText, titleAlpha);

        if (centerTimer > 0f)
        {
            centerTimer -= dt;
            centerText.transform.localScale = Vector3.one * (1f + 0.04f * Mathf.Sin(Time.time * 8f));
        }
    }

    static void SetAlpha(Text t, float a)
    {
        var c = t.color;
        c.a = a;
        t.color = c;
    }

    void ShowCenter(string text, Color color)
    {
        centerText.text = text;
        centerText.color = color;
        centerTimer = 10f;
        titleTimer = 0f;
    }

    void HandleGameOver() => ShowCenter("ТЕБЕ СПІЙМАЛИ!", AlarmColor);
    void HandleLevelComplete() => ShowCenter("ВТІК!", new Color(0.5f, 1f, 0.6f));
    void HandleNoEnergy(Interactable _) => noEnergyFlash = 0.8f;
    void HandleAlert() => alertBlink = 0f;
}

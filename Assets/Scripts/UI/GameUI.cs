using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Інтерфейс рівня (будується кодом при старті):
///   ліворуч угорі   - номер і назва рівня, час;
///   праворуч угорі  - завдання (під мінікартою в 3D): знайти важіль / вихід відчинено;
///   ліворуч унизу   - привид у вибраному скіні (у 3D) і око: СХОВАНИЙ / ТЕБЕ ВИДНО / ПОГОНЯ;
///   внизу посередині - підказка "[E] Розбити глечик";
///   по краях екрана - віньєтка: блакитна, коли тебе видно, червона, коли женуться.
/// </summary>
public class GameUI : MonoBehaviour
{
    [SerializeField] float titleShowTime = 4.5f;

    static readonly Color Gold = new Color(1f, 0.8f, 0.4f);
    static readonly Color Calm = new Color(0.7f, 0.72f, 0.8f);
    static readonly Color SeenColor = new Color(0.55f, 0.9f, 1f);
    static readonly Color AlarmColor = new Color(1f, 0.32f, 0.28f);
    static readonly Color OkColor = new Color(0.5f, 1f, 0.6f);
    static readonly Color SpiritColor = new Color(0.82f, 0.6f, 1f);

    RectTransform canvasRt, objectivePanel, promptPanel, statusPanel, noticePanel;
    Image vignette, portrait, eyeOpen, eyeClosed, objectiveIcon, noticeImage, promptImage;
    Text levelText, timerText, objectiveText, statusText, promptText, promptKey, noticeText, centerText,
        titleNumber, titleName, titleHint;
    Image centerBand;
    CanvasGroup objectiveGroup;

    GhostVisibility ghost;
    Transform ghostBody;
    Vector3 lastGhostPos;
    float ghostSpeed;
    SpriteRenderer ghostSprite;
    PlayerInteractor interactor;
    GuardAI[] enemies = new GuardAI[0];
    float titleTimer, centerTimer, noticeTimer, promptShow, objectiveFlash, time;
    string lastObjective;
    Color vignetteColor = new Color(0f, 0f, 0f, 0f);
    bool firstPerson;

    void Awake()
    {
        if (GetComponent<Minimap>() == null) gameObject.AddComponent<Minimap>();
        firstPerson = GameSettings.FirstPerson;
        Build();
    }

    void Start()
    {
        interactor = FindAnyObjectByType<PlayerInteractor>();
        if (interactor != null)
        {
            ghost = interactor.GetComponent<GhostVisibility>();
            ghostSprite = interactor.GetComponent<SpriteRenderer>();
            ghostBody = interactor.transform;
            lastGhostPos = ghostBody.position;
        }
        enemies = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OnGameOver += HandleGameOver;
            gm.OnLevelComplete += HandleLevelComplete;
            levelText.text = gm.LevelNumber > 0 ? $"РІВЕНЬ {gm.LevelNumber} · {gm.LevelTitle.ToUpper()}" : gm.LevelTitle.ToUpper();
            titleNumber.text = gm.LevelNumber > 0 ? $"РІВЕНЬ {gm.LevelNumber}" : "";
            titleName.text = gm.LevelTitle;
            titleHint.text = gm.LevelHint;
        }
        titleTimer = titleShowTime;
        LevelExit.Message += ShowNotice;
        if (!firstPerson) Cursor.visible = false;   // у 2D миша не потрібна (у 3D курсор ховає FirstPersonMovement)
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver -= HandleGameOver;
            GameManager.Instance.OnLevelComplete -= HandleLevelComplete;
        }
        LevelExit.Message -= ShowNotice;
    }

    // ================================================================ побудова

    void Build()
    {
        var canvas = UIFactory.CreateCanvas(transform, "HUD");
        canvasRt = (RectTransform)canvas.transform;
        var root = canvas.transform;
        Vector2 topLeft = new Vector2(0f, 1f), top = new Vector2(0.5f, 1f), topRight = new Vector2(1f, 1f);
        Vector2 center = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f), bottomLeft = new Vector2(0f, 0f), bottomRight = new Vector2(1f, 0f);

        // віньєтка на весь екран
        vignette = UIFactory.MakeImage(root, "Vignette", center, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0f), UIFactory.VignetteSprite());
        vignette.preserveAspect = false;
        UIFactory.Stretch(vignette.rectTransform);

        // рівень і час
        levelText = UIFactory.MakeText(root, "Level", "", 32, topLeft, new Vector2(28f, -22f), new Vector2(1000f, 40f),
            TextAnchor.UpperLeft, new Color(0.92f, 0.86f, 0.72f));
        timerText = UIFactory.MakeText(root, "Timer", "0:00", 32, topLeft, new Vector2(28f, -62f), new Vector2(300f, 40f),
            TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.7f));

        // завдання
        objectivePanel = UIFactory.MakePanel(root, "Objective", topRight, new Vector2(-24f, -24f), new Vector2(560f, 76f)).rectTransform;
        objectiveIcon = UIFactory.MakeIcon(objectivePanel, "Icon", "icon_lever", new Vector2(0f, 0.5f), new Vector2(14f, 0f), 48f);
        objectiveText = UIFactory.MakeText(objectivePanel, "Text", "", 32, new Vector2(0f, 0.5f), new Vector2(72f, 0f),
            new Vector2(470f, 70f), TextAnchor.MiddleLeft, Color.white);
        objectiveGroup = objectivePanel.gameObject.AddComponent<CanvasGroup>();   // з'являється, коли зникне назва рівня
        objectiveGroup.alpha = 0f;

        // стан помітності
        if (firstPerson)
        {
            statusPanel = UIFactory.MakePanel(root, "Status", bottomLeft, new Vector2(20f, 20f), new Vector2(276f, 280f)).rectTransform;
            portrait = UIFactory.MakeImage(statusPanel, "Ghost", new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(192f, 192f), Color.white);
            portrait.enabled = false;
            eyeOpen = UIFactory.MakeIcon(statusPanel, "EyeOpen", "icon_eye_open", new Vector2(0f, 0f), new Vector2(14f, 14f), 48f);
            eyeClosed = UIFactory.MakeIcon(statusPanel, "EyeClosed", "icon_eye_closed", new Vector2(0f, 0f), new Vector2(14f, 14f), 48f);
            statusText = UIFactory.MakeText(statusPanel, "State", "", 32, new Vector2(0f, 0f), new Vector2(66f, 18f),
                new Vector2(200f, 40f), TextAnchor.LowerLeft, Calm);
        }
        else
        {
            statusPanel = UIFactory.MakePanel(root, "Status", bottomLeft, new Vector2(20f, 20f), new Vector2(300f, 80f)).rectTransform;
            eyeOpen = UIFactory.MakeIcon(statusPanel, "EyeOpen", "icon_eye_open", new Vector2(0f, 0.5f), new Vector2(14f, 0f), 48f);
            eyeClosed = UIFactory.MakeIcon(statusPanel, "EyeClosed", "icon_eye_closed", new Vector2(0f, 0.5f), new Vector2(14f, 0f), 48f);
            statusText = UIFactory.MakeText(statusPanel, "State", "", 32, new Vector2(0f, 0.5f), new Vector2(72f, 0f),
                new Vector2(220f, 40f), TextAnchor.MiddleLeft, Calm);
        }

        // підказка взаємодії
        promptImage = UIFactory.MakePanel(root, "Prompt", bottom, new Vector2(0f, 40f), new Vector2(760f, 76f), true);
        promptPanel = promptImage.rectTransform;
        var keyBox = UIFactory.MakePanel(promptPanel, "Key", new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(56f, 56f));
        promptKey = UIFactory.MakeText(keyBox.rectTransform, "E", "E", 32, new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(56f, 56f),
            TextAnchor.MiddleCenter, Gold);
        promptText = UIFactory.MakeText(promptPanel, "Text", "", 32, new Vector2(0f, 0.5f), new Vector2(84f, 0f), new Vector2(660f, 70f),
            TextAnchor.MiddleLeft, Color.white);

        // повідомлення (важіль, замкнено)
        noticeImage = UIFactory.MakePanel(root, "Notice", top, new Vector2(0f, -110f), new Vector2(1100f, 76f), true);
        noticePanel = noticeImage.rectTransform;
        noticeText = UIFactory.MakeText(noticePanel, "Text", "", 32, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 70f),
            TextAnchor.MiddleCenter, Gold);

        // назва рівня на старті
        titleNumber = UIFactory.MakeText(root, "TitleNumber", "", 48, top, new Vector2(0f, -200f), new Vector2(1600f, 60f),
            TextAnchor.MiddleCenter, Gold);
        titleName = UIFactory.MakeText(root, "TitleName", "", 80, top, new Vector2(0f, -262f), new Vector2(1600f, 100f),
            TextAnchor.MiddleCenter, Color.white);
        titleHint = UIFactory.MakeText(root, "TitleHint", "", 32, top, new Vector2(0f, -378f), new Vector2(1300f, 100f),
            TextAnchor.UpperCenter, new Color(0.85f, 0.9f, 1f));

        // великий напис посередині
        centerBand = UIFactory.MakeImage(root, "Band", center, Vector2.zero, new Vector2(4000f, 170f), new Color(0f, 0f, 0f, 0f));
        centerBand.preserveAspect = false;
        centerText = UIFactory.MakeText(root, "Center", "", 96, center, Vector2.zero, new Vector2(1800f, 140f),
            TextAnchor.MiddleCenter, Color.white);

        UIFactory.MakeText(root, "Pause", "Esc - пауза", 32, bottomRight, new Vector2(-28f, 24f), new Vector2(400f, 40f),
            TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.45f));
    }

    // ================================================================ кадр

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        time += dt;
        if (PauseMenu.IsPaused) return;

        bool chased = false, spiritNear = false, spiritAlerted = false;
        foreach (var e in enemies)
        {
            if (e == null) continue;
            if (e.IsChasing) chased = true;
            if (e.GhostInSense) spiritNear = true;
            if (e.SpiritHasNoticed) spiritAlerted = true;
        }
        if (ghostBody != null && Time.deltaTime > 0f)
        {
            float sp = Vector3.Distance(ghostBody.position, lastGhostPos) / Time.deltaTime;
            ghostSpeed = Mathf.Lerp(ghostSpeed, sp, 1f - Mathf.Exp(-Time.deltaTime * 12f));
            lastGhostPos = ghostBody.position;
        }
        bool moving = ghostSpeed > 0.35f;
        bool over = GameManager.Instance != null && GameManager.Instance.IsLevelOver;
        float vis = ghost != null ? ghost.Visibility : 0f;
        bool seen = ghost != null && ghost.IsVisible;

        UpdateStatus(chased, seen, vis, spiritNear, moving, spiritAlerted);
        UpdateVignette(chased, seen || spiritNear, dt, spiritNear);
        UpdateObjective(dt);
        UpdatePrompt(dt, over);

        if (GameManager.Instance != null) timerText.text = GameProgress.FormatTime(GameManager.Instance.LevelTime);

        // назва рівня
        titleTimer -= dt;
        float ta = Mathf.Clamp01(titleTimer);
        UIFactory.SetAlpha(titleNumber, ta);
        UIFactory.SetAlpha(titleName, ta);
        UIFactory.SetAlpha(titleHint, ta);

        // повідомлення
        noticeTimer -= dt;
        float na = Mathf.Clamp01(noticeTimer * 2f);
        noticePanel.gameObject.SetActive(na > 0.01f);
        UIFactory.SetAlpha(noticeImage, na);
        UIFactory.SetAlpha(noticeText, na);
        noticePanel.anchoredPosition = new Vector2(0f, -110f + (1f - na) * 30f);

        if (centerTimer > 0f)
        {
            centerTimer -= dt;
            centerText.transform.localScale = Vector3.one * (1f + 0.03f * Mathf.Sin(time * 8f));
            UIFactory.SetAlpha(centerBand, Mathf.Min(0.6f, centerTimer));
        }
    }

    void UpdateStatus(bool chased, bool seen, float vis, bool spiritNear, bool moving, bool spiritAlerted)
    {
        string label; Color col;
        if (chased) { label = "ПОГОНЯ!"; col = AlarmColor; }
        else if (spiritAlerted) { label = "ТІКАЙ!"; col = AlarmColor; }
        // злий дух поруч: він відчуває рух, тож треба завмерти
        else if (spiritNear) { label = moving ? "ЗАВМРИ!" : "ДУХ ПОРУЧ"; col = moving && Mathf.Repeat(time, 0.4f) > 0.2f ? Color.white : SpiritColor; }
        else if (seen) { label = "ТЕБЕ ВИДНО"; col = SeenColor; }
        else { label = "СХОВАНИЙ"; col = Calm; }
        statusText.text = label;
        statusText.color = chased && Mathf.Repeat(time, 0.5f) > 0.3f ? Color.white : col;
        bool open = chased || seen || spiritNear;
        eyeOpen.enabled = open && eyeOpen.sprite != null;
        eyeClosed.enabled = !open && eyeClosed.sprite != null;
        if (open) eyeOpen.color = chased ? new Color(1f, 0.6f, 0.6f) : spiritNear ? new Color(0.85f, 0.65f, 1f) : Color.white;

        if (portrait != null && ghostSprite != null && ghostSprite.sprite != null)
        {
            portrait.enabled = true;
            portrait.sprite = ghostSprite.sprite;
            portrait.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.3f, 1f, vis));
            portrait.rectTransform.anchoredPosition = new Vector2(0f, -18f + Mathf.Sin(time * 2f) * 5f);
        }
    }

    void UpdateVignette(bool chased, bool seen, float dt, bool spiritNear)
    {
        Color target;
        if (chased) target = new Color(0.8f, 0.05f, 0.05f, 0.45f + 0.15f * Mathf.Sin(time * 6f));
        else if (spiritNear) target = new Color(0.6f, 0.2f, 0.9f, 0.32f + 0.08f * Mathf.Sin(time * 4f));
        else if (seen) target = new Color(0.55f, 0.85f, 1f, 0.28f);
        else if (SuspicionMeter.Instance != null && SuspicionMeter.Instance.IsAlert) target = new Color(0.9f, 0.45f, 0.1f, 0.25f);
        else target = new Color(0f, 0f, 0f, firstPerson ? 0.25f : 0.45f);
        vignetteColor = Color.Lerp(vignetteColor, target, dt * 5f);
        vignette.color = vignetteColor;
    }

    void UpdateObjective(float dt)
    {
        var exit = LevelExit.Instance;
        string text; string icon;
        if (exit != null && exit.HasLock && exit.IsLocked)
        {
            if (exit.RequiresKey)
            {
                var boss = BossJailer.Instance;
                text = boss != null && boss.IsDefeated ? "Забери ключ Ключника"
                    : boss != null && boss.HitsNeeded > 1 ? $"Скинь вантаж на Ключника: {boss.Hits} з {boss.HitsNeeded}"
                    : "Заманюй Ключника під вантаж";
                icon = "icon_exit";
            }
            else
            {
                text = exit.LeversNeeded > 1
                    ? $"Потягни всі важелі: {exit.LeversPulled} з {exit.LeversNeeded} (E)"
                    : "Знайди важіль і потягни його (E)";
                icon = "icon_lever";
            }
        }
        else if (exit != null && exit.HasLock) { text = "Вихід відчинено - лети до дверей!"; icon = "icon_check"; }
        else { text = "Знайди вихід"; icon = "icon_exit"; }

        if (text != lastObjective)
        {
            if (lastObjective != null) objectiveFlash = 1.5f;
            lastObjective = text;
            objectiveText.text = text;
            objectiveIcon.sprite = UIFactory.UISprite(icon);
            objectiveIcon.enabled = objectiveIcon.sprite != null;
            FitWidth(objectivePanel, objectiveText, 72f);
        }
        objectiveGroup.alpha = Mathf.MoveTowards(objectiveGroup.alpha, titleTimer > 0.8f ? 0f : 1f, dt * 3f);
        objectiveFlash = Mathf.Max(0f, objectiveFlash - dt);
        objectiveText.color = objectiveFlash > 0f && Mathf.Repeat(objectiveFlash, 0.3f) > 0.15f ? OkColor : Color.white;

        // під мінікартою
        float y = -24f;
        if (Minimap.BottomUnits > 0f) y = -(Minimap.BottomUnits + 12f);
        objectivePanel.anchoredPosition = new Vector2(-24f, y);
    }

    void UpdatePrompt(float dt, bool over)
    {
        var target = interactor != null && !over ? interactor.Current : null;
        if (target != null && promptText.text != target.Prompt)
        {
            promptText.text = target.Prompt;
            FitWidth(promptPanel, promptText, 84f);
        }
        promptShow = Mathf.MoveTowards(promptShow, target != null ? 1f : 0f, dt * 6f);
        promptPanel.gameObject.SetActive(promptShow > 0.01f);
        promptPanel.anchoredPosition = new Vector2(0f, 40f - (1f - promptShow) * 24f);
        UIFactory.SetAlpha(promptImage, promptShow);
        UIFactory.SetAlpha(promptText, promptShow);
        UIFactory.SetAlpha(promptKey, promptShow);
    }

    /// <summary>Рамка підганяється під довжину тексту (текст в один рядок, не вилазить за край).</summary>
    static void FitWidth(RectTransform panel, Text text, float textLeft)
    {
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        float w = Mathf.Max(260f, textLeft + text.preferredWidth + 30f);
        panel.sizeDelta = new Vector2(Mathf.Round(w / 4f) * 4f, panel.sizeDelta.y);
    }

    // ================================================================ події

    void ShowNotice(string text)
    {
        noticeText.text = text;
        noticeText.horizontalOverflow = HorizontalWrapMode.Overflow;
        noticePanel.sizeDelta = new Vector2(Mathf.Round(Mathf.Max(420f, noticeText.preferredWidth + 84f) / 4f) * 4f, noticePanel.sizeDelta.y);
        noticeTimer = 3.5f;
        titleTimer = Mathf.Min(titleTimer, 0.3f);
    }

    void ShowCenter(string text, Color color)
    {
        centerText.text = text;
        centerText.color = color;
        centerTimer = 10f;
        titleTimer = 0f;
        noticeTimer = 0f;
    }

    void HandleGameOver() => ShowCenter("ТЕБЕ СПІЙМАЛИ!", AlarmColor);
    void HandleLevelComplete() => ShowCenter("ВИБРАВСЯ!", OkColor);
}

using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Підказки-навчання на першому рівні: спливаюче віконце внизу екрана, кроки йдуть самі,
/// коли гравець щось зробив (полетів, використав приманку, потягнув важіль) або минув час.
/// Прогрес пам'ятається до кінця запуску гри, тож після "спіймали" підказки не повторюються.
/// Enter / Tab - сховати поточну підказку.
/// </summary>
public class TutorialHints : MonoBehaviour
{
    [SerializeField] AudioClip popClip;
    [SerializeField] float startDelay = 2f;

    class Step
    {
        public string text3D, text2D;
        public float minTime, maxTime;
        public Func<bool> done;
        public Func<bool> showWhen;
    }

    static int savedStep;
    static bool savedDone;

    Step[] steps;
    int step;
    float stepTime, delay, show;
    bool visible, lureUsed;
    Vector3 startPos;
    Transform player;
    RectTransform panel;
    GameObject canvasRoot;
    Text header, body;
    Image panelImage;
    AudioSource sfx;

    bool Unlocked => LevelExit.Instance != null && !LevelExit.Instance.IsLocked;

    void Awake()
    {
        steps = new[]
        {
            new Step { text3D = "Ти - прозорий привид. WASD - летіти, миша - роззиратися.",
                       text2D = "Ти - прозорий привид. WASD - летіти.",
                       minTime = 2.5f, maxTime = 10f, done = () => Moved() },
            new Step { text3D = "У темряві тебе не видно. Світло смолоскипів і ліхтарів тебе виказує - на мапі воно позначене жовтими колами.",
                       text2D = "У темряві тебе не видно. Світло смолоскипів і ліхтарів тебе виказує - на мапі воно позначене жовтими колами.",
                       minTime = 8f, maxTime = 8f },
            new Step { text3D = "Скелети ходять з ліхтарями. Якщо над ворогом «!» - тебе помітили: тікай у темряву, і він загубить слід.",
                       text2D = "Скелети ходять з ліхтарями. Якщо над ворогом «!» - тебе помітили: тікай у темряву, і він загубить слід.",
                       minTime = 8f, maxTime = 8f },
            new Step { text3D = "Підлети до глечика, патефона чи дзвону й натисни E. Шум приманить нежить - так можна звільнити прохід.",
                       text2D = "Підлети до глечика, патефона чи дзвону й натисни E. Шум приманить нежить - так можна звільнити прохід.",
                       minTime = 3f, maxTime = 15f, done = () => lureUsed },
            new Step { text3D = "Вихід замкнено. Знайди важіль (жовта точка на мапі) і натисни E. Але він гуркоче на все підземелля!",
                       text2D = "Вихід замкнено. Знайди важіль (жовта точка на мапі) і натисни E. Але він гуркоче на все підземелля!",
                       minTime = 3f, maxTime = 14f, done = () => Unlocked },
            new Step { text3D = "Вихід відчинено! Лети до дверей, поки нежить біжить до важеля.",
                       text2D = "Вихід відчинено! Лети до дверей, поки нежить біжить до важеля.",
                       minTime = 6f, maxTime = 6f, showWhen = () => Unlocked },
        };

        sfx = gameObject.AddComponent<AudioSource>();
        Build();
        step = savedStep;
        delay = startDelay;
        if (savedDone) enabled = false;
        SetVisible(0f);
        canvasRoot.SetActive(GameSettings.TutorialHintsEnabled);
    }

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) { player = pm.transform; startPos = player.position; }
        LureEvents.OnLure += HandleLure;
    }

    void OnDestroy() => LureEvents.OnLure -= HandleLure;

    void HandleLure(LureSignal s)
    {
        if (s.Source == null || s.Source.GetComponent<LeverLure>() == null) lureUsed = true;
    }

    bool Moved() => player != null && Vector2.Distance(player.position, startPos) > 2.5f;

    void Build()
    {
        var root = UIFactory.CreateCanvas(transform, "Tutorial", 150).transform;
        canvasRoot = root.gameObject;
        panelImage = UIFactory.MakePanel(root, "Popup", new Vector2(0.5f, 0f), new Vector2(0f, 180f), new Vector2(1180f, 156f), true);
        panel = panelImage.rectTransform;
        header = UIFactory.MakeText(panel, "Header", "", 32, new Vector2(0f, 1f), new Vector2(36f, -18f), new Vector2(900f, 40f),
            TextAnchor.UpperLeft, new Color(1f, 0.8f, 0.4f));
        body = UIFactory.MakeText(panel, "Body", "", 32, new Vector2(0f, 1f), new Vector2(36f, -60f), new Vector2(1110f, 90f),
            TextAnchor.UpperLeft, Color.white);
        UIFactory.MakeText(panel, "Skip", "Enter - сховати", 16, new Vector2(1f, 1f), new Vector2(-28f, -22f), new Vector2(300f, 24f),
            TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.5f));
    }

    void Update()
    {
        if (!GameSettings.TutorialHintsEnabled)
        {
            // Не витрачаємо кроки й таймери, поки навчання вимкнене.
            visible = false;
            show = 0f;
            SetVisible(0f);
            canvasRoot.SetActive(false);
            if (sfx.isPlaying) sfx.Stop();
            return;
        }
        // під час паузи віконце ховається, щоб не просвічувало крізь меню паузи
        if (canvasRoot != null && canvasRoot.activeSelf == PauseMenu.IsPaused) canvasRoot.SetActive(!PauseMenu.IsPaused);
        if (PauseMenu.IsPaused || LevelTransition.Busy) return;
        if (GameManager.Instance != null && GameManager.Instance.IsLevelOver) { Fade(false); return; }
        float dt = Time.deltaTime;

        if (delay > 0f) { delay -= dt; Fade(false); return; }
        if (step >= steps.Length)
        {
            savedDone = true;
            Fade(false);
            if (show <= 0f) enabled = false;
            return;
        }

        var s = steps[step];
        if (!visible)
        {
            if (s.showWhen != null && !s.showWhen()) { Fade(false); return; }
            Open(s);
        }

        stepTime += dt;
        bool skip = GameInput.StartPressed || TabPressed();
        bool ready = stepTime >= s.minTime && (s.done == null ? stepTime >= s.maxTime : s.done() || stepTime >= s.maxTime);
        if (ready || skip) Next();
        Fade(visible);
    }

    void Open(Step s)
    {
        visible = true;
        stepTime = 0f;
        bool fp = GameSettings.FirstPerson;
        header.text = $"ПІДКАЗКА {step + 1}/{steps.Length}";
        body.text = fp ? s.text3D : s.text2D;
        if (popClip != null) sfx.PlayOneShot(popClip, 0.6f * GameSettings.SfxVolume);
    }

    void Next()
    {
        visible = false;
        step++;
        savedStep = step;
        delay = 0.6f;
    }

    void Fade(bool on)
    {
        show = Mathf.MoveTowards(show, on ? 1f : 0f, Time.deltaTime * 4f);
        SetVisible(show);
    }

    void SetVisible(float k)
    {
        float e = 1f - (1f - k) * (1f - k);
        panel.anchoredPosition = new Vector2(0f, 180f - (1f - e) * 60f);
        UIFactory.SetAlpha(panelImage, e);
        UIFactory.SetAlpha(header, e);
        UIFactory.SetAlpha(body, e);
        foreach (var t in panel.GetComponentsInChildren<Text>()) if (t != header && t != body) UIFactory.SetAlpha(t, 0.5f * e);
        panel.gameObject.SetActive(e > 0.01f);
    }

    static bool TabPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Tab);
#endif
    }
}

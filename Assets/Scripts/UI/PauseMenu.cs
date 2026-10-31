using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Пауза на рівні (Esc): продовжити, почати заново, гучність, чутливість миші, вийти в меню.
/// Поки пауза, Time.timeScale = 0, а курсор вільний. Інші скрипти перевіряють PauseMenu.IsPaused.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    [SerializeField] AudioClip moveClip;
    [SerializeField] AudioClip selectClip;

    class Row
    {
        public RectTransform rt;
        public Image bar;
        public Text label, value;
        public Action click;
        public Action<int> change;
        public Func<string> show;
    }

    static readonly Color Normal = new Color(0.82f, 0.86f, 0.95f);
    static readonly Color Selected = new Color(1f, 0.82f, 0.4f);

    readonly List<Row> rows = new List<Row>();
    GameObject root;
    Text arrow;
    int selected;
    Vector2 lastMouse;
    AudioSource sfx;

    void Awake()
    {
        IsPaused = false;
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.ignoreListenerPause = true;
        Build();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }

    void Build()
    {
        var canvas = UIFactory.CreateCanvas(transform, "Pause", 500);
        root = canvas.gameObject;
        var c = new Vector2(0.5f, 0.5f);

        var dim = UIFactory.MakeImage(root.transform, "Dim", c, Vector2.zero, Vector2.zero, new Color(0.02f, 0.01f, 0.04f, 0.7f));
        UIFactory.Stretch(dim.rectTransform);
        UIFactory.MakePanel(root.transform, "Panel", c, new Vector2(0f, 10f), new Vector2(980f, 700f), true);
        UIFactory.MakeText(root.transform, "Header", "ПАУЗА", 80, c, new Vector2(0f, 270f), new Vector2(900f, 100f),
            TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f));

        float y = 160f;
        AddRow("ПРОДОВЖИТИ", ref y, Resume, null, null);
        AddRow("ПОЧАТИ ЗАНОВО", ref y, Restart, null, null);
        AddRow("МУЗИКА", ref y, null,
            d => { GameSettings.MusicVolume = Step(GameSettings.MusicVolume, d, 0.1f, 0f, 1f); GameAudio.RefreshVolumes(); },
            () => Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%");
        AddRow("ЗВУКИ", ref y, null,
            d => { GameSettings.SfxVolume = Step(GameSettings.SfxVolume, d, 0.1f, 0f, 1f); GameAudio.RefreshVolumes(); },
            () => Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%");
        AddRow("ЧУТЛИВІСТЬ МИШІ", ref y, null,
            d => GameSettings.MouseSensitivity = Step(GameSettings.MouseSensitivity, d, 0.25f, 0.25f, 3f),
            () => "x" + GameSettings.MouseSensitivity.ToString("0.00"));
        AddRow("ГОЛОВНЕ МЕНЮ", ref y, ToMenu, null, null);

        arrow = UIFactory.MakeText(root.transform, "Arrow", "►", 48, c, new Vector2(-420f, 160f), new Vector2(48f, 70f),
            TextAnchor.MiddleCenter, Selected);
        UIFactory.MakeText(root.transform, "Hint", "↑↓ - вибір    ← → - змінити    Enter - OK    Esc - продовжити", 32, c,
            new Vector2(0f, -300f), new Vector2(940f, 40f), TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
    }

    void AddRow(string text, ref float y, Action click, Action<int> change, Func<string> show)
    {
        var c = new Vector2(0.5f, 0.5f);
        var bar = UIFactory.MakeImage(root.transform, "Row_" + text, c, new Vector2(0f, y), new Vector2(820f, 72f), new Color(0f, 0f, 0f, 0f));
        bar.preserveAspect = false;
        var label = UIFactory.MakeText(bar.transform, "Label", text, 48, c, Vector2.zero, new Vector2(820f, 72f),
            change == null ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, Normal);
        UIFactory.Stretch(label.rectTransform);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        if (change != null) label.rectTransform.offsetMin = new Vector2(40f, 0f);
        Text value = null;
        if (change != null)
            value = UIFactory.MakeText(bar.transform, "Value", "", 48, new Vector2(1f, 0.5f), new Vector2(-20f, 0f),
                new Vector2(330f, 72f), TextAnchor.MiddleCenter, Normal);
        rows.Add(new Row { rt = bar.rectTransform, bar = bar, label = label, value = value, click = click, change = change, show = show });
        y -= 76f;
    }

    void Update()
    {
        if (LevelTransition.Busy) return;
        if (GameInput.BackPressed)
        {
            if (IsPaused) Resume();
            else if (GameManager.Instance == null || !GameManager.Instance.IsLevelOver) Pause();
            return;
        }
        if (!IsPaused) return;

        Vector2 mp = GameInput.MousePosition;
        bool moved = (mp - lastMouse).sqrMagnitude > 4f;
        lastMouse = mp;
        int hover = -1;
        for (int i = 0; i < rows.Count; i++)
            if (RectTransformUtility.RectangleContainsScreenPoint(rows[i].rt, mp, null)) hover = i;
        if (moved && hover >= 0 && hover != selected) Select(hover);

        if (GameInput.MouseClicked && hover >= 0)
        {
            var r = rows[hover];
            if (r.change != null)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(r.value.rectTransform, mp, null, out var local);
                Rect rect = r.value.rectTransform.rect;
                float t = (local.x - rect.xMin) / rect.width;
                Change(r, t >= 0f && t < 0.5f ? -1 : 1);
            }
            else Click(r);
            return;
        }

        if (GameInput.UpPressed) Select((selected - 1 + rows.Count) % rows.Count);
        else if (GameInput.DownPressed) Select((selected + 1) % rows.Count);
        else if (GameInput.LeftPressed && rows[selected].change != null) Change(rows[selected], -1);
        else if (GameInput.RightPressed && rows[selected].change != null) Change(rows[selected], 1);
        else if (GameInput.StartPressed)
        {
            var r = rows[selected];
            if (r.click != null) Click(r);
            else Change(r, 1);
        }
        arrow.rectTransform.anchoredPosition = new Vector2(-420f + Mathf.Sin(Time.unscaledTime * 6f) * 4f, rows[selected].rt.anchoredPosition.y);
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        root.SetActive(true);
        selected = 0;
        lastMouse = GameInput.MousePosition;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Refresh();
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        root.SetActive(false);
        var fpm = FindAnyObjectByType<FirstPersonMovement>();
        if (fpm != null && fpm.isActiveAndEnabled)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else Cursor.visible = false;   // 2D: курсор лише заважає
    }

    void Restart()
    {
        IsPaused = false;
        LevelTransition.Go(SceneManager.GetActiveScene().buildIndex);
    }

    void ToMenu()
    {
        IsPaused = false;
        LevelTransition.Go(0);
    }

    void Click(Row r)
    {
        if (selectClip != null) sfx.PlayOneShot(selectClip, 0.8f * GameSettings.SfxVolume);
        r.click?.Invoke();
    }

    void Select(int i)
    {
        selected = i;
        if (moveClip != null) sfx.PlayOneShot(moveClip, 0.6f * GameSettings.SfxVolume);
        Refresh();
    }

    void Change(Row r, int dir)
    {
        r.change(dir);
        if (moveClip != null) sfx.PlayOneShot(moveClip, 0.6f * GameSettings.SfxVolume);
        Refresh();
    }

    void Refresh()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            bool sel = i == selected;
            r.bar.color = sel ? new Color(1f, 0.6f, 0.2f, 0.18f) : new Color(0f, 0f, 0f, 0f);
            r.label.color = sel ? Selected : Normal;
            if (r.value != null)
            {
                r.value.text = "◄  " + r.show() + "  ►";
                r.value.color = sel ? Selected : Normal;
            }
        }
        arrow.rectTransform.anchoredPosition = new Vector2(-420f, rows[selected].rt.anchoredPosition.y);
    }

    static float Step(float v, int dir, float step, float min, float max) =>
        Mathf.Clamp(Mathf.Round(v / step + dir) * step, min, max);
}

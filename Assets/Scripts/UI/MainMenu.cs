using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Головне меню (сцена 0): Грати / Скіни / Налаштування / Вийти.
/// Керування: ↑↓ або W/S - вибір, ← → або A/D - змінити значення, Enter - OK, Esc - назад.
/// Мишею теж можна: наведення вибирає пункт, клік натискає, колесо змінює значення.
/// Усе будується кодом при старті, у сцені лише цей компонент.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] string gameTitle = "ПРИВИД ПІДЗЕМЕЛЛЯ";
    [Tooltip("Картинка на весь екран за меню (необов'язково)")]
    [SerializeField] Sprite background;
    [Tooltip("Кадри всіх скінів підряд, по 14 на скін (idle 0-3, move 0-3, caught 0-5), як у GhostSkin")]
    [SerializeField] Sprite[] skinFrames = new Sprite[0];
    [SerializeField] string[] skinNames = new string[0];
    [SerializeField] AudioClip music;
    [SerializeField] AudioClip startClip;
    [SerializeField] AudioClip moveClip;
    [SerializeField] AudioClip selectClip;

    enum Page { Main, Skins, Settings }

    class Item
    {
        public RectTransform rt;
        public Image bar;
        public Text label;
        public Text value;
        public string text;
        public string help;
        public Action click;
        public Action<int> change;
        public Func<string> show;
    }

    static readonly Color Normal = new Color(0.82f, 0.86f, 0.95f);
    static readonly Color Selected = new Color(1f, 0.82f, 0.4f);
    static readonly Color BarColor = new Color(1f, 0.6f, 0.2f, 0.2f);
    const float MusicBase = 0.35f;
    static bool screenApplied;

    readonly Dictionary<Page, List<Item>> items = new Dictionary<Page, List<Item>>();
    readonly Dictionary<Page, GameObject> roots = new Dictionary<Page, GameObject>();
    readonly List<Image> thumbs = new List<Image>();
    readonly List<Image> thumbFrames = new List<Image>();

    Page page = Page.Main;
    int selected;
    int previewSkin;
    float time;
    bool starting;
    Vector2 lastMouse;

    AudioSource musicSource, sfxSource;
    Image fade, ghostImage, previewImage;
    Text titleText, headerText, helpText, ghostCaption, skinNameText, arrowText;
    RectTransform skinPrev, skinNext;

    int SkinCount => skinFrames.Length / GhostSkin.FramesPerSkin;

    void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (!screenApplied)
        {
            screenApplied = true;
            GameSettings.ApplyScreen();
        }

        EnsureAudioSources();

#if UNITY_EDITOR
        // гру забули перезібрати - беремо кадри прямо з папок
        if (skinFrames.Length < GhostSkin.FramesPerSkin)
        {
            skinFrames = GhostSkinLibrary.LoadAllInEditor();
            skinNames = GhostSkinLibrary.Names;
        }
#endif
        if (SkinCount > 0 && GameSettings.Skin >= SkinCount) GameSettings.Skin = 0;

        Build();
        ShowPage(Page.Main, 0);
        lastMouse = GameInput.MousePosition;
    }

    void OnEnable()
    {
        // Після перекомпіляції Awake не повторюється, а словники й делегати не зберігаються.
        if (Application.isPlaying && !items.ContainsKey(Page.Main)) RebuildMenu();
    }

    void EnsureAudioSources()
    {
        var sources = GetComponents<AudioSource>();
        if (musicSource == null) musicSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();
        if (sfxSource == null) sfxSource = sources.Length > 1 ? sources[1] : gameObject.AddComponent<AudioSource>();
        musicSource.loop = true;
        musicSource.volume = MusicBase * GameSettings.MusicVolume;
        musicSource.clip = music;
        if (music != null && !musicSource.isPlaying) musicSource.Play();
    }

    void RebuildMenu()
    {
        Page previousPage = page;
        int previousSelection = selected;
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (child.name != "Menu") continue;
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
        items.Clear();
        roots.Clear();
        thumbs.Clear();
        thumbFrames.Clear();
        EnsureAudioSources();
        Build();
        ShowPage(previousPage, previousSelection);
        Animate(0f);
        lastMouse = GameInput.MousePosition;
    }

    // ================================================================ побудова

    void Build()
    {
        var root = UIFactory.CreateCanvas(transform, "Menu").transform;
        Vector2 c = new Vector2(0.5f, 0.5f), top = new Vector2(0.5f, 1f), bottom = new Vector2(0.5f, 0f);

        if (background != null)
        {
            var bg = UIFactory.MakeImage(root, "Background", c, Vector2.zero, Vector2.zero, Color.white, background);
            bg.preserveAspect = false;
            Stretch(bg.rectTransform);
        }
        var shade = UIFactory.MakeImage(root, "Shade", c, Vector2.zero, Vector2.zero, new Color(0f, 0f, 0f, 0.3f));
        Stretch(shade.rectTransform);

        titleText = UIFactory.MakeText(root, "Title", gameTitle, 128, top, new Vector2(0f, -56f), new Vector2(1880f, 160f),
            TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f));
        headerText = UIFactory.MakeText(root, "Header", "", 80, top, new Vector2(0f, -60f), new Vector2(1600f, 110f),
            TextAnchor.MiddleCenter, new Color(0.85f, 0.95f, 1f));

        // привид у вибраному скіні праворуч
        ghostImage = UIFactory.MakeImage(root, "Ghost", c, new Vector2(520f, -40f), new Vector2(330f, 330f), Color.white, Frame(0, 0));
        ghostCaption = UIFactory.MakeText(root, "GhostCaption", "", 32, c, new Vector2(520f, -250f), new Vector2(600f, 40f),
            TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.75f));

        BuildMain(root);
        BuildSkins(root);
        BuildSettings(root);

        helpText = UIFactory.MakeText(root, "Help", "", 32, bottom, new Vector2(0f, 70f), new Vector2(1600f, 40f),
            TextAnchor.MiddleCenter, new Color(0.85f, 0.88f, 1f, 0.9f));

        fade = UIFactory.MakeImage(root, "Fade", c, Vector2.zero, Vector2.zero, Color.black);
        Stretch(fade.rectTransform);
    }

    GameObject PageRoot(Transform parent, Page p)
    {
        var rt = UIFactory.MakeRect(parent, p.ToString(), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(rt);
        roots[p] = rt.gameObject;
        items[p] = new List<Item>();
        return rt.gameObject;
    }

    void BuildMain(Transform parent)
    {
        var root = PageRoot(parent, Page.Main).transform;
        var left = new Vector2(0f, 0.5f);

        UIFactory.MakePanel(root, "Panel", left, new Vector2(110f, -60f), new Vector2(640f, 470f), false, 4f, 0.92f);

        string[] names = { "ГРАТИ", "СКІНИ", "НАЛАШТУВАННЯ", "ВИЙТИ" };
        Action[] actions = { StartGame, () => ShowPage(Page.Skins, 0), () => ShowPage(Page.Settings, 0), Quit };
        for (int i = 0; i < names.Length; i++)
        {
            var it = AddItem(root, Page.Main, names[i], left, new Vector2(140f, 110f - i * 105f), new Vector2(580f, 90f), 64,
                TextAnchor.MiddleLeft, actions[i]);
            it.help = "";
            it.label.rectTransform.offsetMin = new Vector2(104f, 0f);   // місце для стрілки
        }
        // стрілка навпроти вибраного пункту (окремо від тексту, щоб той не зсувався)
        arrowText = UIFactory.MakeText(root, "Arrow", "►", 64, left, new Vector2(ArrowX, 110f), new Vector2(64f, 90f),
            TextAnchor.MiddleCenter, Selected);

        if (GameProgress.JustWon)
        {
            int caught = GameProgress.TimesCaught;
            string times = caught % 10 == 1 && caught % 100 != 11 ? "раз"
                : caught % 10 >= 2 && caught % 10 <= 4 && (caught % 100 < 12 || caught % 100 > 14) ? "рази" : "разів";
            string info = $"ТИ ВИБРАВСЯ З ПІДЗЕМЕЛЛЯ!\nЧас: {GameProgress.FormatTime(GameProgress.RunTime)}    Спіймали: {caught} {times}";
            UIFactory.MakePanel(root, "WinPanel", new Vector2(0.5f, 0.5f), new Vector2(520f, -330f), new Vector2(820f, 120f), true);
            UIFactory.MakeText(root, "Win", info, 32, new Vector2(0.5f, 0.5f), new Vector2(520f, -330f), new Vector2(780f, 100f),
                TextAnchor.MiddleCenter, new Color(0.5f, 1f, 0.6f));
        }
    }

    void BuildSkins(Transform parent)
    {
        var root = PageRoot(parent, Page.Skins).transform;
        var c = new Vector2(0.5f, 0.5f);

        // панель -380..350 по висоті: підказка внизу екрана (-440..-400) її вже не перекриває
        UIFactory.MakePanel(root, "Panel", c, new Vector2(0f, -15f), new Vector2(1300f, 730f), false, 4f, 0.95f);
        UIFactory.MakePanel(root, "PreviewBack", c, new Vector2(0f, 135f), new Vector2(340f, 340f), true);
        previewImage = UIFactory.MakeImage(root, "Preview", c, new Vector2(0f, 135f), new Vector2(300f, 300f), Color.white, Frame(0, 0));
        previewImage.enabled = previewImage.sprite != null;

        var it = AddItem(root, Page.Skins, "", c, new Vector2(0f, -85f), new Vector2(700f, 76f), 64, TextAnchor.MiddleCenter, null);
        it.change = ChangeSkin;
        it.click = ConfirmSkin;
        it.show = () => SkinName(previewSkin);
        it.help = "Натисни на скін, щоб вибрати. ← → — переглянути. Enter — вибрати.";
        skinNameText = it.label;
        skinPrev = UIFactory.MakeText(root, "Prev", "◄", 96, c, new Vector2(-430f, -85f), new Vector2(130f, 110f), TextAnchor.MiddleCenter, Selected).rectTransform;
        skinNext = UIFactory.MakeText(root, "Next", "►", 96, c, new Vector2(430f, -85f), new Vector2(130f, 110f), TextAnchor.MiddleCenter, Selected).rectTransform;

        int n = Mathf.Max(1, SkinCount);
        float step = 156f;
        for (int i = 0; i < n; i++)
        {
            var pos = new Vector2((i - (n - 1) * 0.5f) * step, -205f);
            var frame = UIFactory.MakePanel(root, $"Thumb{i}", c, pos, new Vector2(132f, 132f));
            var img = UIFactory.MakeImage(frame.transform, "Img", c, Vector2.zero, new Vector2(112f, 112f), Color.white, Frame(i, 0));
            img.enabled = img.sprite != null;
            thumbFrames.Add(frame);
            thumbs.Add(img);
        }

        var back = AddItem(root, Page.Skins, "НАЗАД", c, new Vector2(0f, -326f), new Vector2(360f, 64f), 48, TextAnchor.MiddleCenter,
            () => ShowPage(Page.Main, 1));
        back.help = "Повернутися без зміни скіна.";
    }

    void BuildSettings(Transform parent)
    {
        var root = PageRoot(parent, Page.Settings).transform;
        var c = new Vector2(0.5f, 0.5f);

        UIFactory.MakePanel(root, "Panel", c, new Vector2(0f, -10f), new Vector2(1300f, 720f), false, 4f, 0.95f);

        float y = 270f;
        AddSetting(root, "Музика", ref y, "Гучність музики",
            d => { GameSettings.MusicVolume = Step(GameSettings.MusicVolume, d, 0.1f, 0f, 1f); musicSource.volume = MusicBase * GameSettings.MusicVolume; },
            () => Percent(GameSettings.MusicVolume));
        AddSetting(root, "Звуки", ref y, "Гучність звуків: приманки, скелети, духи",
            d => GameSettings.SfxVolume = Step(GameSettings.SfxVolume, d, 0.1f, 0f, 1f),
            () => Percent(GameSettings.SfxVolume));
        AddSetting(root, "Чутливість миші", ref y, "Як швидко миша повертає погляд у 3D",
            d => GameSettings.MouseSensitivity = Step(GameSettings.MouseSensitivity, d, 0.25f, 0.25f, 3f),
            () => "x" + GameSettings.MouseSensitivity.ToString("0.00"));
        AddSetting(root, "Вигляд", ref y, "3D - ретро-вигляд від першої особи. 2D - вид зверху.",
            d => GameSettings.FirstPerson = !GameSettings.FirstPerson,
            () => GameSettings.FirstPerson ? "3D від першої особи" : "2D зверху");
        AddSetting(root, "Складність", ref y, "",
            d => GameSettings.Difficulty = (GameSettings.Difficulty + d + 3) % 3,
            () => GameSettings.DifficultyNames[GameSettings.Difficulty]);
        AddSetting(root, "Навчальні підказки", ref y, "Показувати підказки про керування та правила гри",
            d => GameSettings.TutorialHintsEnabled = !GameSettings.TutorialHintsEnabled,
            () => GameSettings.TutorialHintsEnabled ? "Увімкнено" : "Вимкнено");
        AddSetting(root, "Повний екран", ref y, "Грати на весь екран або в окремому вікні",
            d => { GameSettings.Fullscreen = !GameSettings.Fullscreen; GameSettings.ApplyScreen(); },
            () => GameSettings.Fullscreen ? "Так" : "Ні");

        var back = AddItem(root, Page.Settings, "НАЗАД", c, new Vector2(0f, y - 30f), new Vector2(360f, 70f), 48, TextAnchor.MiddleCenter,
            () => ShowPage(Page.Main, 2));
        back.help = "";
    }

    void AddSetting(Transform root, string label, ref float y, string help, Action<int> change, Func<string> show)
    {
        var c = new Vector2(0.5f, 0.5f);
        var it = AddItem(root, Page.Settings, label, c, new Vector2(0f, y), new Vector2(1160f, 74f), 48, TextAnchor.MiddleLeft, null);
        it.label.rectTransform.offsetMin = new Vector2(40f, 0f);
        it.help = help;
        it.change = change;
        it.show = show;
        it.value = UIFactory.MakeText(it.rt, "Value", "", 48, new Vector2(1f, 0.5f), new Vector2(-30f, 0f), new Vector2(560f, 74f),
            TextAnchor.MiddleCenter, Normal);
        y -= 80f;
    }

    Item AddItem(Transform parent, Page p, string text, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize,
        TextAnchor align, Action click)
    {
        var bar = UIFactory.MakeImage(parent, "Item_" + text, anchor, pos, size, new Color(0f, 0f, 0f, 0f));
        bar.preserveAspect = false;
        var label = UIFactory.MakeText(bar.transform, "Label", text, fontSize, new Vector2(0.5f, 0.5f), Vector2.zero, size, align, Normal);
        Stretch(label.rectTransform);
        label.horizontalOverflow = HorizontalWrapMode.Overflow;   // довгий текст не переноситься на другий рядок
        if (align == TextAnchor.MiddleLeft) label.rectTransform.offsetMin = new Vector2(30f, 0f);
        var it = new Item { rt = bar.rectTransform, bar = bar, label = label, text = text, click = click };
        items[p].Add(it);
        return it;
    }

    // ================================================================ логіка

    void ShowPage(Page p, int select)
    {
        if (p == Page.Skins && page != Page.Skins)
            previewSkin = Mathf.Clamp(GameSettings.Skin, 0, Mathf.Max(0, SkinCount - 1));
        page = p;
        foreach (var kv in roots) kv.Value.SetActive(kv.Key == p);
        bool main = p == Page.Main;
        titleText.enabled = main;
        headerText.enabled = !main;
        headerText.text = p == Page.Skins ? "СКІНИ ПРИВИДА" : p == Page.Settings ? "НАЛАШТУВАННЯ" : "";
        headerText.fontSize = 64;
        headerText.rectTransform.anchoredPosition = new Vector2(0f, -32f);
        ghostImage.enabled = main && ghostImage.sprite != null;
        ghostCaption.enabled = main;
        selected = Mathf.Clamp(select, 0, items[p].Count - 1);
        Refresh();
    }

    void Update()
    {
        if (!items.TryGetValue(page, out var list))
        {
            RebuildMenu();
            return;
        }
        float dt = Time.deltaTime;
        time += dt;
        Animate(dt);

        if (starting) return;

        Vector2 mp = GameInput.MousePosition;
        bool mouseMoved = (mp - lastMouse).sqrMagnitude > 4f;
        lastMouse = mp;

        int hover = -1;
        for (int i = 0; i < list.Count; i++)
            if (list[i].rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(list[i].rt, mp, null))
                hover = i;
        if (mouseMoved && hover >= 0 && hover != selected) Select(hover);

        if (GameInput.MouseClicked)
        {
            if (hover >= 0)
            {
                Select(hover, false);
                var it = list[hover];
                if (it.change != null && it.value != null) Change(it, MouseSide(it, mp));
                else if (it.change != null && page == Page.Skins) Change(it, 1);   // клік по назві - наступний скін
                else if (it.click != null) { PlaySelect(); it.click(); }
            }
            else if (page == Page.Skins) ClickSkins(mp);
        }
        if (starting || list != items[page]) return;   // клік відкрив іншу сторінку чи почав гру

        int scroll = GameInput.MouseScroll;
        if (scroll != 0 && hover >= 0 && list[hover].change != null) Change(list[hover], scroll);

        if (GameInput.UpPressed) Select((selected - 1 + list.Count) % list.Count);
        else if (GameInput.DownPressed) Select((selected + 1) % list.Count);
        else if (GameInput.LeftPressed && list[selected].change != null) Change(list[selected], -1);
        else if (GameInput.RightPressed && list[selected].change != null) Change(list[selected], 1);
        else if (GameInput.StartPressed)
        {
            var it = list[selected];
            if (it.click != null) { PlaySelect(); it.click(); }
            else if (it.change != null) Change(it, 1);
        }
        else if (GameInput.BackPressed)
        {
            if (page == Page.Main) Select(list.Count - 1);   // на "ВИЙТИ", щоб не вийти випадково
            else ShowPage(Page.Main, page == Page.Skins ? 1 : 2);
        }
    }

    void Select(int i, bool sound = true)
    {
        if (i == selected) return;
        selected = i;
        if (sound) PlayMove();
        Refresh();
    }

    void Change(Item it, int dir)
    {
        it.change(dir);
        PlayMove();
        Refresh();
    }

    /// <summary>Клік на ліву половину значення (◄) - зменшити, на праву (►) чи на назву - збільшити.</summary>
    static int MouseSide(Item it, Vector2 mp)
    {
        var rt = it.value.rectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, mp, null, out var local);
        Rect r = rt.rect;
        float t = (local.x - r.xMin) / r.width;
        return t >= 0f && t < 0.5f ? -1 : 1;
    }

    void ClickSkins(Vector2 mp)
    {
        if (RectTransformUtility.RectangleContainsScreenPoint(previewImage.rectTransform, mp, null))
        {
            PlaySelect();
            ConfirmSkin();
            return;
        }
        for (int i = 0; i < thumbFrames.Count; i++)
        {
            if (!RectTransformUtility.RectangleContainsScreenPoint(thumbFrames[i].rectTransform, mp, null)) continue;
            if (i < SkinCount)
            {
                previewSkin = i;
                PlaySelect();
                ConfirmSkin();
            }
            return;
        }
        if (RectTransformUtility.RectangleContainsScreenPoint(skinPrev, mp, null)) Change(items[Page.Skins][0], -1);
        else if (RectTransformUtility.RectangleContainsScreenPoint(skinNext, mp, null)) Change(items[Page.Skins][0], 1);
    }

    void ChangeSkin(int dir)
    {
        if (SkinCount == 0) return;
        previewSkin = (previewSkin + dir + SkinCount) % SkinCount;
    }

    void ConfirmSkin()
    {
        if (SkinCount > 0) GameSettings.Skin = previewSkin;
        ShowPage(Page.Main, 1);
    }

    void Refresh()
    {
        var list = items[page];
        for (int i = 0; i < list.Count; i++)
        {
            var it = list[i];
            bool sel = i == selected;
            it.bar.color = sel ? BarColor : new Color(0f, 0f, 0f, 0f);
            it.label.color = sel ? Selected : Normal;
            string shown = it.show != null && it.value == null ? it.show() : it.text;
            it.label.text = shown;
            if (it.value != null)
            {
                it.value.text = "◄   " + it.show() + "   ►";
                it.value.color = sel ? Selected : Normal;
            }
        }

        if (page == Page.Main && list.Count > 0) arrowY = list[selected].rt.anchoredPosition.y;

        string help = list.Count > 0 ? list[selected].help : "";
        if (page == Page.Settings && list[selected].text == "Складність")
            help = DifficultyHelp(GameSettings.Difficulty);
        helpText.text = help;

        int skin = page == Page.Skins ? previewSkin : GameSettings.Skin;
        for (int i = 0; i < thumbFrames.Count; i++)
            thumbFrames[i].color = i == skin ? new Color(1f, 0.85f, 0.45f, 1f) : new Color(0.55f, 0.55f, 0.65f, 0.9f);
        ghostCaption.text = SkinCount > 1 ? SkinName(skin) : "";
    }

    static string DifficultyHelp(int d)
    {
        switch (d)
        {
            case 0: return "Вороги повільніші й довше придивляються";
            case 2: return "Вороги швидші, бачать далі й помічають майже миттєво";
            default: return "Як задумано";
        }
    }

    float arrowY = 110f;
    const float ArrowX = 156f;

    void Animate(float dt)
    {
        if (arrowText != null)
            arrowText.rectTransform.anchoredPosition = new Vector2(ArrowX + Mathf.Sin(time * 6f) * 4f, arrowY);
        int skin = page == Page.Skins ? previewSkin : GameSettings.Skin;
        if (ghostImage.enabled)
        {
            var s = GhostSkinLibrary.AnimatedFrame(skin, "idle", time) ?? Frame(skin, (int)(time * 5f) % 4);
            if (s != null) ghostImage.sprite = s;
            ghostImage.rectTransform.anchoredPosition = new Vector2(520f, -40f + Mathf.Sin(time * 2f) * 14f);
            ghostImage.color = new Color(1f, 1f, 1f, 0.6f + 0.3f * Mathf.Sin(time * 1.3f));
        }
        if (page == Page.Skins)
        {
            // прев'ю: 4 кадри стоїть, 4 кадри летить
            float cycle = Mathf.Repeat(time, 1.75f);
            bool flying = cycle >= 1f;
            var s = GhostSkinLibrary.AnimatedFrame(skin, flying ? "move" : "idle", flying ? cycle - 1f : cycle)
                ?? Frame(skin, (int)(time * 6f) % 8);
            if (s != null) previewImage.sprite = s;
            previewImage.rectTransform.anchoredPosition = new Vector2(0f, 135f + Mathf.Sin(time * 2.2f) * 8f);
            float pulse = 1f + 0.08f * Mathf.Sin(time * 5f);
            skinPrev.localScale = skinNext.localScale = Vector3.one * pulse;
        }

        // затемнення на старті й перед грою
        float a = 1f - Mathf.Clamp01(time / 0.7f);
        fade.color = new Color(0f, 0f, 0f, a);
        fade.enabled = a > 0.001f;
    }

    Sprite Frame(int skin, int i)
    {
        if (GhostSkinLibrary.HasExtendedAnimations(skin))
        {
            string state = i < 4 ? "idle" : i < 8 ? "move" : "dissolve";
            int frame = i < 4 ? i : i < 8 ? i - 4 : i - 8;
            var animated = GhostSkinLibrary.AnimatedFrame(skin, state, frame / GhostSkinLibrary.FrameRate(state));
            if (animated != null) return animated;
        }
        int k = skin * GhostSkin.FramesPerSkin + i;
        if (k >= 0 && k < skinFrames.Length && skinFrames[k] != null) return skinFrames[k];
        return i < skinFrames.Length ? skinFrames[i] : null;
    }

    string SkinName(int i) => i >= 0 && i < skinNames.Length ? skinNames[i] : $"Скін {i + 1}";

    static float Step(float v, int dir, float step, float min, float max)
    {
        float n = Mathf.Round(v / step + dir) * step;
        return Mathf.Clamp(n, min, max);
    }

    static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    void PlayMove()
    {
        if (moveClip != null) sfxSource.PlayOneShot(moveClip, 0.6f * GameSettings.SfxVolume);
    }

    void PlaySelect()
    {
        if (selectClip != null) sfxSource.PlayOneShot(selectClip, 0.8f * GameSettings.SfxVolume);
    }

    void StartGame()
    {
        starting = true;
        GameProgress.ResetRun();
        if (startClip != null) sfxSource.PlayOneShot(startClip, GameSettings.SfxVolume);
        if (SceneManager.sceneCountInBuildSettings > 1) LevelTransition.Go(1);
    }

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

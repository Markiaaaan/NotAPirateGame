using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Слайд-шоу: передісторія перед першим рівнем і фінал після останнього.
/// Кадри-ілюстрації з повільним наїздом камери, текст, що друкується по літері, плавні переходи.
/// Enter / Space / E - далі (або показати весь текст одразу), Esc - пропустити все.
/// Після останнього слайда - сцена nextSceneIndex (або наступна за списком), через LevelTransition.
/// </summary>
public class IntroCutscene : MonoBehaviour
{
    [SerializeField] Sprite[] slides = new Sprite[0];
    [TextArea] [SerializeField] string[] lines = new string[0];
    [SerializeField] float charsPerSecond = 30f;
    [Tooltip("Скільки секунд чекати після тексту, перш ніж самому перейти далі")]
    [SerializeField] float autoAdvanceDelay = 4f;
    [SerializeField] float fadeTime = 0.6f;
    [SerializeField] AudioClip music;
    [SerializeField] AudioClip typeClip;

    [Header("Куди далі")]
    [Tooltip("-1 = наступна сцена за списком")]
    [SerializeField] int nextSceneIndex = -1;
    [Tooltip("Фінал гри: позначити перемогу (меню покаже статистику)")]
    [SerializeField] bool markWin;
    [SerializeField] string nextTitle = "";
    [SerializeField] string nextName = "";
    [Tooltip("Кадри всіх скінів підряд (для привида в тунелі на екрані переходу)")]
    [SerializeField] Sprite[] skinFrames = new Sprite[0];

    [Header("Привид поверх слайдів (фінал)")]
    [Tooltip("Для кожного слайда: звідки й куди летить привид (x0, y0, x1, y1; 0..1 від лівого нижнього кута). Порожньо - без привида")]
    [SerializeField] Vector4[] ghostPaths = new Vector4[0];
    [Tooltip("Розмір привида для кожного слайда (частка висоти екрана; 0 - не показувати)")]
    [SerializeField] float[] ghostSizes = new float[0];
    [SerializeField] float ghostTravelTime = 4f;
    [Tooltip("На останньому слайді показати великий напис КІНЕЦЬ і статистику проходження")]
    [SerializeField] bool showStats;

    Image picture, fade, ghostImage;
    Text text, counter, endTitle, endStats;
    RectTransform pictureRt;
    AudioSource musicSource, sfxSource;

    int index = -1;
    float time;
    float fadeOut = -1f;
    int typed;
    bool finished;
    Vector2 panDir;

    void Awake()
    {
        Cursor.visible = false;   // у кат-сцені курсор не потрібен (меню знову його покаже)
        var root = UIFactory.CreateCanvas(transform, "Intro").transform;
        Vector2 c = new Vector2(0.5f, 0.5f), bottom = new Vector2(0.5f, 0f);

        var bg = UIFactory.MakeImage(root, "Black", c, Vector2.zero, Vector2.zero, Color.black);
        Stretch(bg.rectTransform);

        picture = UIFactory.MakeImage(root, "Picture", c, Vector2.zero, Vector2.zero, Color.white);
        picture.preserveAspect = false;
        pictureRt = picture.rectTransform;
        Stretch(pictureRt);

        // привид у вибраному скіні (летить поверх ілюстрації)
        ghostImage = UIFactory.MakeImage(root, "Ghost", c, Vector2.zero, new Vector2(200f, 200f), Color.white);
        ghostImage.preserveAspect = true;
        ghostImage.enabled = false;

        endTitle = UIFactory.MakeText(root, "EndTitle", "", 128, new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(1600f, 160f),
            TextAnchor.MiddleCenter, new Color(1f, 0.84f, 0.45f));
        endStats = UIFactory.MakeText(root, "EndStats", "", 48, new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1600f, 70f),
            TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));

        var panel = UIFactory.MakeImage(root, "TextPanel", bottom, Vector2.zero, new Vector2(1920f, 230f), new Color(0f, 0f, 0f, 0.7f));
        panel.rectTransform.anchorMin = new Vector2(0f, 0f);
        panel.rectTransform.anchorMax = new Vector2(1f, 0f);
        panel.rectTransform.sizeDelta = new Vector2(0f, 230f);

        text = UIFactory.MakeText(root, "Text", "", 48, bottom, new Vector2(0f, 46f), new Vector2(1640f, 170f),
            TextAnchor.UpperLeft, new Color(0.95f, 0.95f, 1f));
        counter = UIFactory.MakeText(root, "Hint", "", 32, new Vector2(1f, 0f), new Vector2(-30f, 10f), new Vector2(1100f, 40f),
            TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.5f));

        fade = UIFactory.MakeImage(root, "Fade", c, Vector2.zero, Vector2.zero, Color.black);
        Stretch(fade.rectTransform);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.clip = music;
        musicSource.loop = true;
        musicSource.volume = 0.35f * GameSettings.MusicVolume;
        if (music != null) musicSource.Play();
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.volume = 0.25f * GameSettings.SfxVolume;

        NextSlide();
    }

    void NextSlide()
    {
        index++;
        if (index >= slides.Length)
        {
            Finish();
            return;
        }

        picture.sprite = slides[index];
        time = 0f;
        typed = 0;
        fadeOut = -1f;
        float a = Random.Range(0f, Mathf.PI * 2f);
        panDir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        counter.text = $"{index + 1} / {slides.Length}      Enter - далі      Esc - пропустити";

        bool last = showStats && index == slides.Length - 1;
        endTitle.text = last ? "КІНЕЦЬ" : "";
        endStats.text = last ? StatsLine() : "";
    }

    static string StatsLine()
    {
        int caught = GameProgress.TimesCaught;
        string times = caught % 10 == 1 && caught % 100 != 11 ? "раз"
            : caught % 10 >= 2 && caught % 10 <= 4 && (caught % 100 < 12 || caught % 100 > 14) ? "рази" : "разів";
        return $"Час: {GameProgress.FormatTime(GameProgress.RunTime)}     Спіймали: {caught} {times}";
    }

    /// <summary>Привид летить по шляху слайда: махає кадрами польоту, а долетівши - просто висить і погойдується.</summary>
    void UpdateGhost()
    {
        bool has = index >= 0 && index < ghostPaths.Length && index < ghostSizes.Length && ghostSizes[index] > 0f;
        ghostImage.enabled = has;
        if (!has) return;

        Vector4 p = ghostPaths[index];
        float t = Mathf.Clamp01((time - 0.3f) / ghostTravelTime);
        float e = t * t * (3f - 2f * t);
        Vector2 pos = Vector2.Lerp(new Vector2(p.x, p.y), new Vector2(p.z, p.w), e);
        pos.y += Mathf.Sin(time * 2.2f) * 0.012f;
        var rt = ghostImage.rectTransform;
        rt.anchorMin = rt.anchorMax = pos;
        rt.anchoredPosition = Vector2.zero;
        float size = ghostSizes[index] * 1080f;
        rt.sizeDelta = new Vector2(size, size);

        bool flying = t < 1f && (p.z != p.x || p.w != p.y);
        int frame = (int)(time * (flying ? 8f : 5f)) % 4 + (flying ? 4 : 0);
        int skin = GameSettings.Skin;
        int k = skin * GhostSkin.FramesPerSkin + frame;
        Sprite s = k < skinFrames.Length && skinFrames[k] != null ? skinFrames[k] : frame < skinFrames.Length ? skinFrames[frame] : null;
        s = GhostSkinLibrary.AnimatedFrame(skin, flying ? "move" : "idle", time) ?? s;
        if (s != null) ghostImage.sprite = s;
        else ghostImage.enabled = false;
        rt.localScale = new Vector3(p.z < p.x ? -1f : 1f, 1f, 1f);
        ghostImage.color = new Color(1f, 1f, 1f, 0.9f + 0.1f * Mathf.Sin(time * 3f));
    }

    string CurrentLine => index >= 0 && index < lines.Length ? lines[index] : "";

    void Update()
    {
        if (finished || index >= slides.Length) return;
        float dt = Time.deltaTime;
        time += dt;

        // повільний наїзд камери
        pictureRt.localScale = Vector3.one * (1.04f + time * 0.012f);
        pictureRt.anchoredPosition = panDir * time * 6f;
        UpdateGhost();

        // друк тексту
        string line = CurrentLine;
        int n = Mathf.Min(line.Length, (int)(Mathf.Max(0f, time - fadeTime * 0.5f) * charsPerSecond));
        if (n > typed)
        {
            if (typeClip != null && n / 2 != typed / 2 && line[n - 1] != ' ') sfxSource.PlayOneShot(typeClip);
            typed = n;
            text.text = line.Substring(0, n);
        }
        bool fullyTyped = typed >= line.Length;

        // Esc пропускає все одразу, навіть посеред затемнення між слайдами
        if (GameInput.BackPressed)
        {
            Finish();
            return;
        }

        // затемнення
        if (fadeOut >= 0f)
        {
            fadeOut += dt;
            SetFade(Mathf.Clamp01(fadeOut / (fadeTime * 0.6f)));
            if (fadeOut >= fadeTime * 0.6f) NextSlide();
            return;
        }
        SetFade(1f - Mathf.Clamp01(time / fadeTime));

        bool next = GameInput.StartPressed || GameInput.InteractPressed;
        if (next && !fullyTyped)
        {
            typed = line.Length;
            text.text = line;
            time = Mathf.Max(time, fadeTime * 0.5f + line.Length / charsPerSecond);
        }
        else if ((next && time > 0.3f) || (fullyTyped && time > fadeTime * 0.5f + line.Length / charsPerSecond + autoAdvanceDelay))
        {
            fadeOut = 0f;
        }
    }

    void SetFade(float a)
    {
        var col = fade.color;
        col.a = a;
        fade.color = col;
    }

    void Finish()
    {
        if (finished) return;
        finished = true;
        if (markWin) GameProgress.JustWon = true;
        int next = nextSceneIndex >= 0 ? nextSceneIndex : SceneManager.GetActiveScene().buildIndex + 1;
        if (next >= SceneManager.sceneCountInBuildSettings) next = 0;
        if (!string.IsNullOrEmpty(nextName))
            LevelTransition.Go(next, nextTitle, nextName, GhostSkinLibrary.MoveFrames(skinFrames, GameSettings.Skin));
        else
            LevelTransition.Go(next);
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

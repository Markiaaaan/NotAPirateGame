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

    Image picture, fade;
    Text text, counter;
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

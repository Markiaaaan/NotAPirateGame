using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Переходи між сценами. Створюється сам при першому виклику й живе між сценами.
///   LevelTransition.Go(index)                          - просто затемнення.
///   LevelTransition.Go(index, "РІВЕНЬ 2", "Катакомби", кадри) - привид летить тунелем,
///   поки у фоні вантажиться наступна сцена (замість того, щоб пролітати крізь текстуру дверей).
/// Фон тунелю - Assets/Resources/Transition/passage_back.png і passage_front.png (тайляться по горизонталі).
/// </summary>
public class LevelTransition : MonoBehaviour
{
    static LevelTransition instance;

    /// <summary>Зараз іде перехід (ввід краще ігнорувати).</summary>
    public static bool Busy => instance != null && instance.busy;

    const float FadeOut = 0.35f, FadeIn = 0.5f, MinPassage = 2.6f;

    GameObject passageRoot;
    Image black, ghost, shadow;
    RawImage back, front;
    Text title, subtitle, loading;
    Sprite[] frames;
    bool busy;
    float animTime, ghostX = 0.42f;

    static LevelTransition Get()
    {
        if (instance == null)
        {
            var go = new GameObject("LevelTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LevelTransition>();
            instance.Build();
        }
        return instance;
    }

    public static void Go(int buildIndex, string title = "", string subtitle = "", Sprite[] passageFrames = null)
    {
        var t = Get();
        if (t.busy) return;
        t.StartCoroutine(t.Run(buildIndex, title, subtitle, passageFrames));
    }

    void Build()
    {
        var root = UIFactory.CreateCanvas(transform, "Transition", 1000).transform;

        passageRoot = UIFactory.MakeRect(root, "Passage", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
        UIFactory.Stretch((RectTransform)passageRoot.transform);
        var bgBlack = UIFactory.MakeImage(passageRoot.transform, "Back", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.black);
        UIFactory.Stretch(bgBlack.rectTransform);

        back = MakeRaw(passageRoot.transform, "Tunnel", Resources.Load<Texture2D>("Transition/passage_back"));
        shadow = UIFactory.MakeImage(passageRoot.transform, "Shadow", new Vector2(0.42f, 0.2f), Vector2.zero, new Vector2(200f, 44f),
            new Color(1f, 1f, 1f, 0.8f), UIFactory.UISprite("ui_shadow"));
        shadow.preserveAspect = false;
        shadow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        ghost = UIFactory.MakeImage(passageRoot.transform, "Ghost", new Vector2(0.42f, 0.45f), Vector2.zero, new Vector2(256f, 256f), Color.white);
        ghost.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        front = MakeRaw(passageRoot.transform, "Pillars", Resources.Load<Texture2D>("Transition/passage_front"));

        title = UIFactory.MakeText(passageRoot.transform, "Title", "", 48, new Vector2(0.5f, 1f), new Vector2(0f, -48f),
            new Vector2(1600f, 60f), TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.4f));
        subtitle = UIFactory.MakeText(passageRoot.transform, "Name", "", 80, new Vector2(0.5f, 1f), new Vector2(0f, -112f),
            new Vector2(1800f, 100f), TextAnchor.MiddleCenter, Color.white);
        loading = UIFactory.MakeText(passageRoot.transform, "Loading", "", 32, new Vector2(1f, 0f), new Vector2(-40f, 28f),
            new Vector2(600f, 40f), TextAnchor.LowerRight, new Color(1f, 1f, 1f, 0.7f));
        passageRoot.SetActive(false);

        black = UIFactory.MakeImage(root, "Black", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Color.black);
        UIFactory.Stretch(black.rectTransform);
        black.raycastTarget = true;
        SetBlack(0f);
    }

    static RawImage MakeRaw(Transform parent, string name, Texture2D tex)
    {
        var rt = UIFactory.MakeRect(parent, name, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        UIFactory.Stretch(rt);
        var raw = rt.gameObject.AddComponent<RawImage>();
        raw.texture = tex;
        raw.raycastTarget = false;
        raw.enabled = tex != null;
        if (tex != null)
        {
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;
        }
        return raw;
    }

    void SetBlack(float a)
    {
        black.color = new Color(0f, 0f, 0f, a);
        black.enabled = a > 0.001f;
    }

    IEnumerator Fade(float from, float to, float time, bool animate)
    {
        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            SetBlack(Mathf.Lerp(from, to, t / time));
            if (animate) Animate(Time.unscaledDeltaTime);
            yield return null;
        }
        SetBlack(to);
    }

    IEnumerator Run(int buildIndex, string titleText, string nameText, Sprite[] passageFrames)
    {
        busy = true;
        Time.timeScale = 1f;
        AudioListener.pause = false;

        yield return Fade(0f, 1f, FadeOut, false);

        frames = passageFrames;
        bool passage = frames != null && frames.Length > 0 && back.texture != null;
        var op = SceneManager.LoadSceneAsync(buildIndex);
        op.allowSceneActivation = false;

        if (passage)
        {
            passageRoot.SetActive(true);
            title.text = titleText;
            subtitle.text = nameText;
            animTime = 0f;
            ghostX = -0.15f;
            float start = Time.unscaledTime;
            yield return Fade(1f, 0f, 0.4f, true);
            while (op.progress < 0.9f || Time.unscaledTime - start < MinPassage)
            {
                Animate(Time.unscaledDeltaTime);
                yield return null;
            }
            // привид відлітає далі тунелем, екран гасне
            for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
            {
                ghostX += Time.unscaledDeltaTime * 1.2f;
                Animate(Time.unscaledDeltaTime);
                yield return null;
            }
            yield return Fade(0f, 1f, 0.3f, true);
            passageRoot.SetActive(false);
        }
        else
        {
            while (op.progress < 0.9f) yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone) yield return null;
        yield return null;
        yield return Fade(1f, 0f, FadeIn, false);
        busy = false;
    }

    void Animate(float dt)
    {
        animTime += dt;
        if (back.texture != null) back.uvRect = new Rect(animTime * 0.11f, 0f, 1f, 1f);
        if (front.texture != null) front.uvRect = new Rect(animTime * 0.3f, 0f, 1f, 1f);

        // привид підлітає до свого місця й погойдується
        ghostX = Mathf.MoveTowards(ghostX, Mathf.Max(ghostX, 0.42f), dt * 0.9f);
        float bob = Mathf.Sin(animTime * 3f);
        var rt = ghost.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(ghostX, 0.45f);
        rt.anchoredPosition = new Vector2(0f, bob * 14f);
        rt.localRotation = Quaternion.Euler(0f, 0f, -4f + bob * 2f);
        if (frames != null && frames.Length > 0) ghost.sprite = frames[(int)(animTime * 8f) % frames.Length];
        ghost.color = new Color(1f, 1f, 1f, 0.82f + 0.12f * Mathf.Sin(animTime * 5f));

        var srt = shadow.rectTransform;
        srt.anchorMin = srt.anchorMax = new Vector2(ghostX, 0.2f);
        srt.localScale = new Vector3(1f - bob * 0.1f, 1f - bob * 0.1f, 1f);

        int dots = (int)(animTime * 3f) % 4;
        loading.text = "ЗАВАНТАЖЕННЯ" + new string('.', dots) + new string(' ', 3 - dots);
    }
}

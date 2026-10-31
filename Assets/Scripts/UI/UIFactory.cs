using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Маленькі помічники, щоб будувати інтерфейс кодом (без ручного налаштування Canvas у сцені).
/// Шрифт - піксельний Assets/Resources/Fonts/PryvydPixel.ttf (16 px). Найчіткіший при розмірах,
/// кратних 16: 16, 32, 48, 64, 80, 96...
/// Рамки й іконки лежать у Assets/Resources/UI (16x16, рамка ріжеться як 9-slice по 4 px).
/// </summary>
public static class UIFactory
{
    static Font font;
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.Load<Font>("Fonts/PryvydPixel");
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    /// <summary>Спрайт з Assets/Resources/UI (null, якщо немає).</summary>
    public static Sprite UISprite(string name)
    {
        if (!sprites.TryGetValue(name, out var s) || s == null)
        {
            s = Resources.Load<Sprite>("UI/" + name);
            sprites[name] = s;
        }
        return s;
    }

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder = 100)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        canvas.pixelPerfect = true;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    /// <summary>anchor: (0,1) лівий верх, (0.5,0.5) центр, (1,0) правий низ і т.д.</summary>
    public static RectTransform MakeRect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        return rt;
    }

    public static Text MakeText(Transform parent, string name, string text, int size, Vector2 anchor, Vector2 position,
        Vector2 box, TextAnchor align, Color color)
    {
        var rt = MakeRect(parent, name, anchor, position, box);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = DefaultFont;
        t.text = text;
        t.fontSize = size;
        t.alignment = align;
        t.color = color;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        t.lineSpacing = 1.1f;
        // піксельна тінь: на 1 "піксель" шрифту вниз-управо + обвідка, щоб читалось на будь-якому фоні
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.04f, 0.02f, 0.06f, 0.9f);
        outline.effectDistance = new Vector2(2f, -2f);
        var shadow = rt.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        shadow.effectDistance = new Vector2(Mathf.Max(2f, size / 16f * 2f), -Mathf.Max(2f, size / 16f * 2f));
        return t;
    }

    public static Image MakeImage(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
        Color color, Sprite sprite = null)
    {
        var rt = MakeRect(parent, name, anchor, position, size);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.preserveAspect = sprite != null;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Панель з піксельною рамкою (9-slice). pixel - скільки екранних пікселів на 1 піксель рамки.
    /// Якщо рамки немає в Resources - просто темний прямокутник.
    /// </summary>
    public static Image MakePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
        bool gold = false, float pixel = 4f, float alpha = 1f)
    {
        var frame = UISprite(gold ? "ui_frame_gold" : "ui_frame");
        var img = MakeImage(parent, name, anchor, position, size, new Color(1f, 1f, 1f, alpha), frame);
        img.preserveAspect = false;
        if (frame != null)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 100f / (frame.pixelsPerUnit * pixel);
        }
        else img.color = new Color(0.05f, 0.04f, 0.08f, 0.85f * alpha);
        return img;
    }

    /// <summary>Іконка з Resources/UI (піксельна, без розмиття).</summary>
    public static Image MakeIcon(Transform parent, string name, string icon, Vector2 anchor, Vector2 position, float size)
    {
        var img = MakeImage(parent, name, anchor, position, new Vector2(size, size), Color.white, UISprite(icon));
        img.enabled = img.sprite != null;
        return img;
    }

    /// <summary>Розтягнути на весь батьківський прямокутник.</summary>
    public static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    /// <summary>Смужка: темний фон + заповнення, ширину якого задає SetBar.</summary>
    public static RectTransform MakeBar(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
        Color fill, out Image fillImage)
    {
        var bg = MakeImage(parent, name, anchor, position, size, new Color(0f, 0f, 0f, 0.6f));
        var go = new GameObject("Fill", typeof(RectTransform));
        go.transform.SetParent(bg.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(3f, 3f);
        rt.offsetMax = new Vector2(-3f, -3f);
        fillImage = go.AddComponent<Image>();
        fillImage.color = fill;
        fillImage.raycastTarget = false;
        return rt;
    }

    public static void SetBar(RectTransform fill, float value)
    {
        fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
    }

    public static void SetAlpha(Graphic g, float a)
    {
        var c = g.color;
        c.a = a;
        g.color = c;
    }

    /// <summary>М'яка віньєтка (затемнення по краях), створюється в пам'яті.</summary>
    public static Sprite VignetteSprite()
    {
        if (sprites.TryGetValue("__vignette", out var s) && s != null) return s;
        const int w = 128, h = 72;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float dx = (x + 0.5f) / w * 2f - 1f, dy = (y + 0.5f) / h * 2f - 1f;
            float d = Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 1.1f);
            float a = Mathf.Clamp01((d - 0.55f) / 0.75f);
            a = a * a * (3f - 2f * a);
            px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }
        tex.SetPixels32(px);
        tex.Apply();
        s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprites["__vignette"] = s;
        return s;
    }
}

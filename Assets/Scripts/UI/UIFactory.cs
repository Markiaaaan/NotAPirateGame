using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Маленькі помічники, щоб будувати інтерфейс кодом (без ручного налаштування Canvas у сцені).
/// </summary>
public static class UIFactory
{
    static Font font;

    public static Font DefaultFont
    {
        get
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font;
        }
    }

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder = 100)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
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
        var outline = rt.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);
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
}

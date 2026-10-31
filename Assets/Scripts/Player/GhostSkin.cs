using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скіни привида. Animator завжди програє класичні кадри, а цей компонент щокадру (у LateUpdate,
/// тобто вже після Animator) підміняє їх відповідними кадрами вибраного скіна (GameSettings.Skin).
/// Кадри кожного скіна йдуть підряд у тому ж порядку, що й classicFrames:
/// idle 0-3, move 0-3, caught 0-5 (14 штук). Скін 0 = класичний.
/// Список скінів - у GhostSkinLibrary. Масиви заповнює GameBuilder; якщо вони порожні,
/// у редакторі кадри підвантажуються прямо з папок.
/// </summary>
public class GhostSkin : MonoBehaviour
{
    public const int FramesPerSkin = 14;

    [SerializeField] Sprite[] classicFrames = new Sprite[0];
    [Tooltip("Усі скіни підряд, по 14 кадрів на скін")]
    [SerializeField] Sprite[] skinFrames = new Sprite[0];
    [SerializeField] string[] skinNames = new string[0];

    SpriteRenderer spriteRenderer;
    readonly Dictionary<Sprite, int> index = new Dictionary<Sprite, int>();
    int skin;

    /// <summary>Скін привида в поточній сцені (для інтерфейсу).</summary>
    public static GhostSkin Current { get; private set; }

    public int SkinCount => skinFrames.Length / FramesPerSkin;
    public int CurrentSkin => skin;

    /// <summary>Перший кадр вибраного скіна (іконка в інтерфейсі).</summary>
    public Sprite Portrait => Frame(skin, 0);

    /// <summary>Кадри польоту вибраного скіна (для екрана переходу між рівнями).</summary>
    public Sprite[] MoveFrames => GhostSkinLibrary.MoveFrames(skinFrames, skin);

    void Awake()
    {
        Current = this;
        spriteRenderer = GetComponent<SpriteRenderer>();

#if UNITY_EDITOR
        if (skinFrames.Length < FramesPerSkin || classicFrames.Length == 0)
        {
            skinFrames = GhostSkinLibrary.LoadAllInEditor();
            classicFrames = new Sprite[FramesPerSkin];
            System.Array.Copy(skinFrames, classicFrames, FramesPerSkin);
            skinNames = GhostSkinLibrary.Names;
        }
#endif

        for (int i = 0; i < classicFrames.Length; i++)
            if (classicFrames[i] != null && !index.ContainsKey(classicFrames[i])) index[classicFrames[i]] = i;

        // У 3D камера нічого не малює, тож Animator не має "засинати", коли привида не видно
        var animator = GetComponent<Animator>();
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        SetSkin(GameSettings.Skin);
    }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    public void SetSkin(int s)
    {
        skin = SkinCount > 0 ? Mathf.Clamp(s, 0, SkinCount - 1) : 0;
        Apply();
    }

    Sprite Frame(int s, int i)
    {
        int k = s * FramesPerSkin + i;
        if (k >= 0 && k < skinFrames.Length && skinFrames[k] != null) return skinFrames[k];
        return i < classicFrames.Length ? classicFrames[i] : null;
    }

    void LateUpdate() => Apply();

    void Apply()
    {
        if (skin == 0 || spriteRenderer == null || spriteRenderer.sprite == null) return;
        if (index.TryGetValue(spriteRenderer.sprite, out int i))
        {
            var s = Frame(skin, i);
            if (s != null) spriteRenderer.sprite = s;
        }
    }

    // Якщо на привиді зі старого префаба немає GhostSkin - додати автоматично.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachMissing();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AttachMissing();

    static void AttachMissing()
    {
        foreach (var ghost in FindObjectsByType<GhostAnimator>(FindObjectsSortMode.None))
            if (ghost.GetComponent<GhostSkin>() == null) ghost.gameObject.AddComponent<GhostSkin>();
    }
}

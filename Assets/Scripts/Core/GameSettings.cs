using UnityEngine;

/// <summary>
/// Налаштування, що зберігаються між запусками (PlayerPrefs). Змінюються в меню «Налаштування».
/// Тут же множники складності, які читають скелети, духи й енергія привида.
/// </summary>
public static class GameSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void SelectDefaultSkinAtStartup() => Skin = 0;

    public static readonly string[] DifficultyNames = { "Легко", "Нормально", "Важко" };

    static int GetInt(string key, int def) => PlayerPrefs.GetInt(key, def);
    static void SetInt(string key, int v) { PlayerPrefs.SetInt(key, v); PlayerPrefs.Save(); }
    static float GetFloat(string key, float def) => PlayerPrefs.GetFloat(key, def);
    static void SetFloat(string key, float v) { PlayerPrefs.SetFloat(key, v); PlayerPrefs.Save(); }

    /// <summary>true - вигляд від першої особи (3D), false - зверху (2D).</summary>
    public static bool FirstPerson
    {
        get => GetInt("FirstPerson", 1) == 1;
        set => SetInt("FirstPerson", value ? 1 : 0);
    }

    /// <summary>Гучність музики 0..1.</summary>
    public static float MusicVolume
    {
        get => GetFloat("MusicVolume", 0.8f);
        set => SetFloat("MusicVolume", Mathf.Clamp01(value));
    }

    /// <summary>Гучність звуків 0..1.</summary>
    public static float SfxVolume
    {
        get => GetFloat("SfxVolume", 1f);
        set => SetFloat("SfxVolume", Mathf.Clamp01(value));
    }

    /// <summary>Множник чутливості миші 0.25..3.</summary>
    public static float MouseSensitivity
    {
        get => GetFloat("MouseSensitivity", 1f);
        set => SetFloat("MouseSensitivity", Mathf.Clamp(value, 0.25f, 3f));
    }

    /// <summary>0 - легко, 1 - нормально, 2 - важко.</summary>
    public static int Difficulty
    {
        get => Mathf.Clamp(GetInt("Difficulty", 1), 0, 2);
        set => SetInt("Difficulty", Mathf.Clamp(value, 0, 2));
    }

    /// <summary>Номер вибраного скіна привида.</summary>
    public static int Skin
    {
        get => Mathf.Max(0, GetInt("Skin", 0));
        set => SetInt("Skin", Mathf.Max(0, value));
    }

    public static bool Fullscreen
    {
        get => GetInt("Fullscreen", 1) == 1;
        set => SetInt("Fullscreen", value ? 1 : 0);
    }

    /// <summary>Шість навчальних вікон першого рівня. За замовчуванням увімкнені.</summary>
    public static bool TutorialHintsEnabled
    {
        get => GetInt("TutorialHintsEnabled", 1) == 1;
        set => SetInt("TutorialHintsEnabled", value ? 1 : 0);
    }

    // ---- множники складності ----
    static readonly float[] speedMul = { 0.85f, 1f, 1.15f };
    static readonly float[] noticeMul = { 1.5f, 1f, 0.7f };
    static readonly float[] viewMul = { 0.85f, 1f, 1.15f };
    static readonly float[] regenMul = { 1.3f, 1f, 0.75f };

    /// <summary>Швидкість ворогів.</summary>
    public static float EnemySpeedMul => speedMul[Difficulty];
    /// <summary>Скільки часу ворогу треба, щоб помітити привида (більше = легше).</summary>
    public static float NoticeTimeMul => noticeMul[Difficulty];
    /// <summary>Дальність зору ворогів.</summary>
    public static float ViewMul => viewMul[Difficulty];
    /// <summary>Як швидко відновлюється енергія привида.</summary>
    public static float EnergyRegenMul => regenMul[Difficulty];

    /// <summary>Застосувати повноекранний режим (у збірці; в редакторі нічого не робить).</summary>
    public static void ApplyScreen()
    {
        if (Application.isEditor) return;
        if (Fullscreen)
            Screen.SetResolution(Display.main.systemWidth, Display.main.systemHeight, FullScreenMode.FullScreenWindow);
        else
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
    }
}

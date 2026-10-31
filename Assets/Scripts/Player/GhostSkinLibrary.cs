using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Список скінів привида в одному місці: назва і підпапка в Assets/Sprites/Characters/Ghost.
/// Кожен скін - 14 кадрів: ghost_idle_0..3, ghost_move_0..3, ghost_caught_0..5.
/// Новий скін: намалюй кадри в нову підпапку й додай рядок сюди, потім «Зібрати всю гру».
/// </summary>
public static class GhostSkinLibrary
{
    public static readonly string[] Names = { "Класичний", "Кучерявий", "Рокер", "Розвідник", "Стиляга" };
    public static readonly string[] Folders = { "", "curly/", "rocker/", "scout/", "hanna/" };
    static readonly Dictionary<string, Sprite[]> animationCache = new Dictionary<string, Sprite[]>();
    static readonly Sprite[] noFrames = new Sprite[0];
    public const float AnimationFps = 8f;
    public static float FrameRate(string state) => state == "idle" ? 6f : AnimationFps;

    public static bool HasExtendedAnimations(int skin)
        => skin >= 0 && skin < Folders.Length && Folders[skin] == "hanna/";

    public static Sprite[] AnimationFrames(int skin, string state)
    {
        if (!HasExtendedAnimations(skin)) return noFrames;
        if (animationCache.TryGetValue(state, out var cached)) return cached;
        var texture = Resources.Load<Texture2D>("Skins/Hanna/ghost_" + state + "_strip");
        if (texture == null || texture.height != 32 || texture.width % 32 != 0)
        {
            animationCache[state] = noFrames;
            return noFrames;
        }
        var frames = new Sprite[texture.width / 32];
        for (int i = 0; i < frames.Length; i++)
        {
            frames[i] = Sprite.Create(texture, new Rect(i * 32f, 0f, 32f, 32f),
                new Vector2(0.5f, 0.5f), 32f, 0, SpriteMeshType.FullRect);
            frames[i].name = "hanna_" + state + "_" + i;
        }
        animationCache[state] = frames;
        return frames;
    }

    public static Sprite AnimatedFrame(int skin, string state, float time, bool loop = true)
    {
        var frames = AnimationFrames(skin, state);
        if (frames.Length == 0) return null;
        int index = Mathf.Max(0, Mathf.FloorToInt(time * FrameRate(state)));
        return frames[loop ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
    }

    /// <summary>Старі скіни, які прибрали з гри (GameBuilder видаляє їхні папки).</summary>
    public static readonly string[] RemovedFolders = { "swamp/", "phantom/" };

    /// <summary>4 кадри польоту скіна з масиву "усі скіни підряд" (запасний варіант - класичний).</summary>
    public static Sprite[] MoveFrames(Sprite[] all, int skin)
    {
        var extended = AnimationFrames(skin, "move");
        if (extended.Length > 0) return extended;
        if (all == null || all.Length < 8) return new Sprite[0];
        int start = skin * 14 + 4;
        if (start + 4 > all.Length || all[start] == null) start = 4;
        var r = new Sprite[4];
        System.Array.Copy(all, start, r, 0, 4);
        return r;
    }

    public const string Root = "Assets/Sprites/Characters/Ghost/";

    /// <summary>Шляхи 14 кадрів скіна в тому самому порядку, що й у GhostSkin.</summary>
    public static List<string> FramePaths(string folder)
    {
        // Слоти сумісності для старих масивів. Повні анімації завантажуються з Resources.
        if (folder == "hanna/")
        {
            var singleFrame = new List<string>();
            for (int i = 0; i < 14; i++) singleFrame.Add(Root + folder + "Hannas_skin.png");
            return singleFrame;
        }
        string p = Root + folder + "ghost_";
        var list = new List<string>();
        for (int i = 0; i < 4; i++) list.Add($"{p}idle_{i}.png");
        for (int i = 0; i < 4; i++) list.Add($"{p}move_{i}.png");
        for (int i = 0; i < 6; i++) list.Add($"{p}caught_{i}.png");
        return list;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Лише в редакторі: кадри всіх скінів прямо з папок. Запасний варіант, якщо гру
    /// забули перезібрати і в префабі чи меню порожні масиви.
    /// </summary>
    public static Sprite[] LoadAllInEditor()
    {
        var result = new List<Sprite>();
        foreach (string folder in Folders)
            foreach (string path in FramePaths(folder))
                result.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path));
        return result.ToArray();
    }
#endif
}

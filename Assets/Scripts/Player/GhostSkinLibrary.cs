using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Список скінів привида в одному місці: назва і підпапка в Assets/Sprites/Characters/Ghost.
/// Кожен скін - 14 кадрів: ghost_idle_0..3, ghost_move_0..3, ghost_caught_0..5.
/// Новий скін: намалюй кадри в нову підпапку й додай рядок сюди, потім «Зібрати всю гру».
/// </summary>
public static class GhostSkinLibrary
{
    public static readonly string[] Names = { "Класичний", "Кучерявий", "Рокер", "Розвідник" };
    public static readonly string[] Folders = { "", "curly/", "rocker/", "scout/" };

    /// <summary>Старі скіни, які прибрали з гри (GameBuilder видаляє їхні папки).</summary>
    public static readonly string[] RemovedFolders = { "swamp/", "phantom/" };

    /// <summary>4 кадри польоту скіна з масиву "усі скіни підряд" (запасний варіант - класичний).</summary>
    public static Sprite[] MoveFrames(Sprite[] all, int skin)
    {
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Tools > Ghost > Зібрати всю гру
///
/// 1. Створює префаби (Player, Skeleton, Spirit, Vase, TV, Phone, Mannequin, Lamp, Exit, Lever) в Assets/Prefabs,
///    якщо їх ще немає. Існуючі префаби НЕ чіпає, тож їх можна спокійно налаштовувати руками.
/// 2. Будує сцени: меню, передісторію, по сцені на кожен файл Assets/Levels/*.txt (Level_01, ...) і фінал.
/// 3. Записує сцени в Build Profiles у правильному порядку, ставить назву гри та іконку.
///
/// Tools > Ghost > Зібрати Windows-версію - те саме + готова гра в Build/PryvydPidzemellya і архів Build/PryvydPidzemellya-Windows.zip.
/// Рівні можна редагувати прямо в .txt і перезбирати. Легенда символів - у README_GAME.md.
/// </summary>
public static class GameBuilder
{
    const string LevelsFolder = "Assets/Levels";
    const string ScenesFolder = "Assets/Scenes";
    const string PrefabsFolder = "Assets/Prefabs";
    const string MenuScenePath = ScenesFolder + "/MainMenu.unity";
    const string IntroScenePath = ScenesFolder + "/Intro.unity";
    const string TwistScenePath = ScenesFolder + "/Twist.unity";
    const string EndingScenePath = ScenesFolder + "/Ending.unity";
    const string ProductName = "Привид Підземелля";

    static readonly string[] WallVariantPaths =
    {
        "Environment/wall_front.png", "Environment/wall_poster_sale.png", "Environment/wall_poster_ghost.png",
        "Environment/wall_clock.png", "Environment/wall_window.png", "Environment/wall_garland.png",
    };

    static readonly string[] IntroLines =
    {
        "Далеко під землею, серед забутих тунелів і стародавніх руїн, ховається підземелля, про яке не знають навіть найсміливіші шукачі пригод.",
        "Однієї місячної ночі маленький привид випадково потрапив у пастку темних сил. Важкі кам'яні ворота зачинилися за ним, і місячне світло лишилося десь високо нагорі.",
        "Тепер він опинився у світі, де кожен коридор ховає небезпеку, кожна тінь може виявитися ворогом, а за кожними дверима чекає нове випробування.",
        "Щоб повернутися на волю, привид повинен пройти всі рівні підземелля, подолати пастки, розгадати таємниці та знайти шлях до виходу.",
        "Але пам'ятай: підземелля не відпускає тих, хто здається. Допоможи привиду вибратися й знову побачити місяць. Твоя пригода починається зараз!",
    };

    static readonly string[] TwistLines =
    {
        "За склепом знайшлися сходи нагору. У прочиненій брамі сяяв місяць. Ще кілька кроків — і привид нарешті буде вільний.",
        "Та в брамі виріс велетенський дух. «Ти справді думав, що я тебе відпущу?» Світло згасло. Шлях до волі перекрив Сторож.",
        "Привид кинувся назад, але крик Сторожа скував його політ. Старий міст тріснув. Герой зірвався в темряву разом з уламками.",
        "Привид упав у залу Ключника. Вихід замкнений, а ключ носить велетенський скелет. Силою його не здолати. Треба перехитрити вартового.",
    };

    static readonly string[] EndingLines =
    {
        "Ключ повернувся в замку — і кам'яні ворота з гуркотом поповзли вгору. Згори повіяло свіжим повітрям. Цього разу вихід справжній.",
        "Сходинка за сходинкою... і ось воно, старе місто! Маленький привид вилітає з підземелля посеред ночі.",
        "Над дахами сяє місяць. Темні сили лишилися глибоко внизу, і жодні ворота більше його не втримають.",
        "Дякуємо, що грали!",
    };


    const string Spr = "Assets/Sprites/";
    const string Sfx = "Assets/Audio/SFX/";
    const string MusicPath = "Assets/Audio/Music/ambient_loop.wav";
    /// <summary>Тема меню й передісторії (музична скринька). Якщо файлу немає - грає гул підземелля.</summary>
    const string MenuMusicPath = "Assets/Audio/Music/menu_theme.wav";

    const string LevelMusicPath = "Assets/Audio/Music/level_theme.wav";
    const string ChaseMusicPath = "Assets/Audio/Music/level_chase.wav";

    static AudioClip LevelMusic() =>
        AssetDatabase.LoadAssetAtPath<AudioClip>(LevelMusicPath) ?? AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);

    static AudioClip MenuMusic() =>
        AssetDatabase.LoadAssetAtPath<AudioClip>(MenuMusicPath) ?? AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
    const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    const int LightPoint = 3;
    const int LightGlobal = 4;

    /// <summary>
    /// Кольорові теми рівнів (рядок "theme:" у файлі рівня). Текстури теми лежать у
    /// Sprites/Environment/Themes/&lt;тема&gt;/ (чого там немає - береться звичайне). cellar - звичайні текстури.
    /// light - колір загального світла (і в 2D, і в 3D), fog - колір туману в 3D.
    /// </summary>
    static readonly Dictionary<string, (Color light, float intensity, Color fog)> Themes =
        new Dictionary<string, (Color, float, Color)>
        {
            ["cellar"] = (new Color(0.74f, 0.56f, 0.46f), 0.42f, new Color(0.03f, 0.018f, 0.012f)),
            ["catacombs"] = (new Color(0.64f, 0.64f, 0.5f), 0.42f, new Color(0.022f, 0.024f, 0.016f)),
            ["river"] = (new Color(0.46f, 0.6f, 0.8f), 0.46f, new Color(0.01f, 0.02f, 0.036f)),
            ["crypt"] = (new Color(0.62f, 0.48f, 0.78f), 0.42f, new Color(0.022f, 0.012f, 0.032f)),
            ["gate"] = (new Color(0.5f, 0.7f, 0.56f), 0.4f, new Color(0.01f, 0.026f, 0.018f)),
        };

    class PrefabSet
    {
        public GameObject player, skeleton, spirit, vase, tv, phone, mannequin, lamp, exit, key, lever;
    }

    // ===================================================================== меню

    /// <summary>Збільшується, коли префаби треба перезібрати (нова тема, нові тексти приманок).</summary>
    const int PrefabVersion = 6;
    const string PrefabVersionKey = "GhostGame.PrefabVersion";

    [MenuItem("Tools/Ghost/Зібрати всю гру", priority = 0)]
    public static void BuildAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        bool outdated = EditorPrefs.GetInt(PrefabVersionKey, 1) < PrefabVersion;
        BuildAll(outdated);
        EditorPrefs.SetInt(PrefabVersionKey, PrefabVersion);
    }

    [MenuItem("Tools/Ghost/Перезібрати префаби й гру", priority = 1)]
    public static void RebuildPrefabsMenu()
    {
        if (!EditorUtility.DisplayDialog("Перезібрати префаби",
                "Префаби в Assets/Prefabs буде створено заново, ручні зміни в них зникнуть. Продовжити?", "Так", "Ні"))
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAll(true);
    }

    static void BuildAll(bool forcePrefabs)
    {
        try
        {
            string[] levelFiles = Directory.Exists(LevelsFolder)
                ? Directory.GetFiles(LevelsFolder, "*.txt").OrderBy(f => f).ToArray()
                : new string[0];
            if (levelFiles.Length == 0)
            {
                EditorUtility.DisplayDialog("Ghost", "Не знайшов рівнів у Assets/Levels/*.txt", "OK");
                return;
            }

            EditorUtility.DisplayProgressBar("Ghost", "Текстури...", 0.05f);
            RemoveOldSkins();
            PrepareResources();
            MakeSpritesReadable();
            ConfigurePlayer();
            var levels = levelFiles.Select(f => LevelData.Parse(File.ReadAllText(f, Encoding.UTF8))).ToList();

            EditorUtility.DisplayProgressBar("Ghost", "Префаби...", 0.1f);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);
            var prefabs = EnsurePrefabs(forcePrefabs);
            RefreshPlayerSkins(prefabs.player);
            // старий сторож більше не потрібен (його замінили скелет і дух)
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/Guard.prefab") != null)
                AssetDatabase.DeleteAsset(PrefabsFolder + "/Guard.prefab");

            EditorUtility.DisplayProgressBar("Ghost", "Меню...", 0.2f);
            var scenes = new List<string>();
            BuildMenuScene();
            scenes.Add(MenuScenePath);
            BuildIntroScene(levels[0].name);
            scenes.Add(IntroScenePath);

            for (int i = 0; i < levels.Count; i++)
            {
                EditorUtility.DisplayProgressBar("Ghost", $"Рівень {i + 1}...", 0.3f + 0.6f * i / levels.Count);
                string path = $"{ScenesFolder}/Level_{i + 1:00}.unity";
                bool last = i == levels.Count - 1;
                BuildLevelScene(levels[i], i + 1, prefabs, path,
                    last ? "ФІНАЛ" : $"РІВЕНЬ {i + 2}", last ? "Нагору, до світанку" : levels[i + 1].name);
                scenes.Add(path);
                if (i == 3 && levels.Count > 4)
                {
                    BuildTwistScene(levels[4].name);
                    scenes.Add(TwistScenePath);
                }
            }
            BuildEndingScene();
            scenes.Add(EndingScenePath);

            SetBuildScenes(scenes);
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Debug.Log($"Гру зібрано: меню + рівнів: {levelFiles.Length}. Тисни Play.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    /// <summary>
    /// Усім картинкам у Assets/Sprites: Read/Write (3D-вигляд читає пікселі) і правильний розмір
    /// (32 px = клітинка, квадратні 64 px теж = клітинка).
    /// </summary>
    static void MakeSpritesReadable()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Sprites" }))
            TestSceneBuilder.LoadPixelSprite(AssetDatabase.GUIDToAssetPath(guid));
    }

    /// <summary>Скіни, які прибрали з гри (зелений і фіолетовий): їхні папки більше не потрібні.</summary>
    static void RemoveOldSkins()
    {
        foreach (string folder in GhostSkinLibrary.RemovedFolders)
        {
            string path = (GhostSkinLibrary.Root + folder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(path)) AssetDatabase.DeleteAsset(path);
        }
    }

    /// <summary>
    /// Налаштування імпорту того, що гра вантажить через Resources:
    /// рамки й іконки інтерфейсу (піксельні, рамки ріжуться 9-slice), тунель переходу (повторюється), шрифт.
    /// </summary>
    static void PrepareResources()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/UI" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
            bool frame = Path.GetFileNameWithoutExtension(path).StartsWith("ui_frame");
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.spritePixelsPerUnit = 16;
            imp.spriteBorder = frame ? new Vector4(4, 4, 4, 4) : Vector4.zero;
            imp.SaveAndReimport();
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Resources/Transition" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) continue;
            imp.textureType = TextureImporterType.Default;
            imp.npotScale = TextureImporterNPOTScale.None;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
        }
        if (AssetImporter.GetAtPath("Assets/Resources/Fonts/PryvydPixel.ttf") is TrueTypeFontImporter font)
        {
            font.fontRenderingMode = FontRenderingMode.HintedRaster;
            font.fontTextureCase = FontTextureCase.Dynamic;
            font.includeFontData = true;
            font.SaveAndReimport();
        }
    }

    /// <summary>Назва гри, іконка, вікно - те, що побачать гравці у зібраній версії.</summary>
    static void ConfigurePlayer()
    {
        PlayerSettings.productName = ProductName;
        if (string.IsNullOrEmpty(PlayerSettings.companyName) || PlayerSettings.companyName == "DefaultCompany")
            PlayerSettings.companyName = "NotAPirate";
        PlayerSettings.defaultScreenWidth = 1920;
        PlayerSettings.defaultScreenHeight = 1080;
        PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = false;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Spr + "UI/app_icon.png");
        if (icon == null) return;
        PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
        var sizes = PlayerSettings.GetIconSizes(UnityEditor.Build.NamedBuildTarget.Standalone, IconKind.Any);
        if (sizes != null && sizes.Length > 0)
            PlayerSettings.SetIcons(UnityEditor.Build.NamedBuildTarget.Standalone,
                Enumerable.Repeat(icon, sizes.Length).ToArray(), IconKind.Any);
    }

    /// <summary>Назва архіву з грою. README на GitHub посилається саме на цей файл у релізі.</summary>
    public const string ZipName = "PryvydPidzemellya-Windows.zip";

    [MenuItem("Tools/Ghost/Зібрати Windows-версію (.exe)", priority = 20)]
    public static void BuildWindowsMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAllMenu();

        string[] ours = EditorBuildSettings.scenes
            .Where(sc => sc.enabled && IsGameScene(sc.path))
            .Select(sc => sc.path).ToArray();
        string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Build");
        string dir = Path.Combine(root, "PryvydPidzemellya");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);   // щоразу чиста збірка, без старих файлів
        Directory.CreateDirectory(dir);
        var options = new BuildPlayerOptions
        {
            scenes = ours,
            locationPathName = Path.Combine(dir, "PryvydPidzemellya.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            EditorUtility.DisplayDialog("Ghost", "Збірка не вдалася: " + report.summary.result + ". Деталі в Console.", "OK");
            return;
        }

        // папки "..._DoNotShip" / "..._BackUpThisFolder_ButDontShipItWithYourGame" - лише для налагодження,
        // гравцям вони не потрібні: переносимо їх у Build/DoNotShip
        string keep = Path.Combine(root, "DoNotShip");
        foreach (string d in Directory.GetDirectories(dir))
        {
            string n = Path.GetFileName(d);
            if (!n.Contains("DoNotShip") && !n.Contains("DontShip")) continue;
            Directory.CreateDirectory(keep);
            string to = Path.Combine(keep, n);
            if (Directory.Exists(to)) Directory.Delete(to, true);
            Directory.Move(d, to);
        }

        // архів для релізу на GitHub: усередині папка PryvydPidzemellya з грою
        string zip = Path.Combine(root, ZipName);
        if (File.Exists(zip)) File.Delete(zip);
        System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip, System.IO.Compression.CompressionLevel.Optimal, true);
        long mb = new FileInfo(zip).Length / (1024L * 1024L);
        Debug.Log($"Готово: {options.locationPathName} ({report.summary.totalSize / (1024UL * 1024UL)} МБ), архів {zip} ({mb} МБ)");
        EditorUtility.RevealInFinder(zip);
    }

    static bool IsGameScene(string path)
    {
        string n = Path.GetFileNameWithoutExtension(path);
        return path.StartsWith(ScenesFolder) && (n == "MainMenu" || n == "Intro" || n == "Twist" || n == "Ending" || n.StartsWith("Level_"));
    }

    // ===================================================================== префаби

    static PrefabSet EnsurePrefabs(bool force)
    {
        return new PrefabSet
        {
            player = EnsurePrefab("Player", force, MakePlayer),
            skeleton = EnsurePrefab("Skeleton", force, MakeSkeleton),
            spirit = EnsurePrefab("Spirit", force, MakeSpirit),
            vase = EnsurePrefab("Vase", force, MakeVase),
            tv = EnsurePrefab("TV", force, MakeTV),
            phone = EnsurePrefab("Phone", force, MakePhone),
            mannequin = EnsurePrefab("Mannequin", force, MakeMannequin),
            lamp = EnsurePrefab("Lamp", force, MakeLamp),
            exit = EnsurePrefab("Exit", force, MakeExit),
            key = EnsurePrefab("Key", force, MakeKey),
            lever = EnsurePrefab("Lever", force, MakeLever),
        };
    }

    /// <summary>
    /// Оновлює список скінів у готовому префабі привида, щоб нові скіни з GhostSkinLibrary
    /// з'являлися в грі без повного перезбирання префабів.
    /// </summary>
    static void RefreshPlayerSkins(GameObject prefab)
    {
        if (prefab == null) return;
        string path = AssetDatabase.GetAssetPath(prefab);
        var root = PrefabUtility.LoadPrefabContents(path);
        var skin = root.GetComponent<GhostSkin>();
        if (skin != null)
        {
            Set(skin, ("classicFrames", GhostFrames("")), ("skinFrames", AllSkinFrames()), ("skinNames", GhostSkinLibrary.Names));
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    static GameObject EnsurePrefab(string name, bool force, Func<GameObject> make)
    {
        string path = $"{PrefabsFolder}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !force) return existing;

        var go = make();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject MakePlayer()
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S("Characters/Ghost/ghost_idle_0.png");
        UseUnlit(sr);

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        go.AddComponent<CircleCollider2D>().radius = 0.3f;

        go.AddComponent<PlayerMovement>();
        go.AddComponent<FirstPersonMovement>();
        go.AddComponent<PlayerEnergy>();
        go.AddComponent<PlayerInteractor>();
        Set(go.AddComponent<GhostVisibility>(), ("hiddenAlpha", 0.3f), ("fadeSpeed", 4f), ("detectThreshold", 0.45f));
        Set(go.AddComponent<YSort>(), ("offset", 5));

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GhostAnimationSetup.ControllerPath);
        if (controller == null) controller = GhostAnimationSetup.CreateAssets();
        if (controller != null) GhostAnimationSetup.AttachTo(go, controller);

        // скіни: Animator грає класичні кадри, GhostSkin підміняє їх на вибраний скін
        Set(go.AddComponent<GhostSkin>(),
            ("classicFrames", GhostFrames("")),
            ("skinFrames", AllSkinFrames()),
            ("skinNames", GhostSkinLibrary.Names));

        // слабке світіння навколо привида, щоб гравцю було видно, куди летіти
        AddLight(Child(go, "Glow", Vector2.zero), LightPoint, new Color(0.6f, 0.85f, 1f), 0.45f, 2.4f);
        return go;
    }

    static GameObject MakeSkeleton()
    {
        var go = MakeEnemy("Skeleton", "Characters/Skeleton/skeleton_", out var sr);
        // ліхтар у руці: світить на всі боки
        var lantern = Child(go, "Lantern", new Vector2(0.22f, -0.12f));
        AddLight(lantern, LightPoint, new Color(1f, 0.72f, 0.35f), 1.3f, 3.4f, 360f, 360f, 0.6f, 0.4f);
        lantern.AddComponent<TorchFlicker>();
        Set(go.GetComponent<GuardAI>(),
            ("isSpirit", false), ("canCrossWater", false), ("lanternRadius", 3.0f), ("senseRadius", 0f),
            ("walkSpeed", 2.3f), ("investigateSpeed", 3.0f), ("chaseSpeed", 3.25f),
            ("viewDistance", 7f), ("viewAngle", 80f), ("flashlightDistance", 0f), ("noticeTime", 0.6f),
            ("flashlight", lantern.transform),
            ("alertClip", A("skeleton_alert")), ("huhClip", A("skeleton_huh")));
        return go;
    }

    static GameObject MakeSpirit()
    {
        var go = MakeEnemy("Spirit", "Characters/Spirit/spirit_", out var sr);
        // фіолетова аура навколо духа
        var aura = Child(go, "Aura", Vector2.zero);
        AddLight(aura, LightPoint, new Color(0.65f, 0.4f, 1f), 0.9f, 1.8f);
        // шепіт: що ближче дух, то гучніше (гучність рахує GuardAI)
        var whisper = go.AddComponent<AudioSource>();
        whisper.clip = A("spirit_whisper");
        whisper.loop = true;
        whisper.playOnAwake = false;
        whisper.spatialBlend = 0f;
        whisper.volume = 0f;
        // дух відчуває РУХ у радіусі senseRadius (крізь стіни), літає крізь стіни, повільніший за привида
        Set(go.GetComponent<GuardAI>(),
            ("isSpirit", true), ("canCrossWater", true), ("lanternRadius", 0f), ("senseRadius", 2.8f),
            ("walkSpeed", 1.2f), ("investigateSpeed", 3.0f), ("chaseSpeed", 3.1f),
            ("viewDistance", 0f), ("viewAngle", 100f), ("flashlightDistance", 0f), ("noticeTime", 0.6f),
            ("moveThreshold", 0.35f), ("spiritLoseTime", 1.1f), ("catchDistance", 0.45f), ("waitAtPoint", 1.6f), ("searchTime", 3f),
            ("animFps", 6f), ("flashlight", aura.transform), ("whisper", whisper),
            ("alertClip", A("spirit_alert")), ("huhClip", A("spirit_huh")));
        return go;
    }

    /// <summary>Спільне для скелета й духа: спрайт, значок "!/?" над головою, GuardAI з кадрами.</summary>
    static GameObject MakeEnemy(string name, string framesPrefix, out SpriteRenderer sr)
    {
        var go = new GameObject(name);
        sr = go.AddComponent<SpriteRenderer>();
        var down = Frames(framesPrefix + "down_", 4);
        sr.sprite = down.Length > 0 ? down[0] : null;
        UseUnlit(sr);
        Set(go.AddComponent<YSort>(), ("offset", 5));

        var alert = Child(go, "Alert", new Vector2(0f, 0.95f));
        var alertSr = alert.AddComponent<SpriteRenderer>();
        alertSr.sortingOrder = 30000;
        alertSr.enabled = false;
        UseUnlit(alertSr);

        Set(go.AddComponent<GuardAI>(),
            ("body", sr),
            ("walkDown", down),
            ("walkUp", Frames(framesPrefix + "up_", 4)),
            ("walkSide", Frames(framesPrefix + "side_", 4)),
            ("alertIcon", alertSr),
            ("exclaimSprite", S("UI/alert_exclaim.png")),
            ("questionSprite", S("UI/alert_question.png")));
        return go;
    }

    static GameObject MakeLure<T>(string name, string sprite, Vector2 colliderSize, Vector2 colliderOffset,
        string prompt, float cost, float cooldown, bool singleUse, float loudness, float duration, float suspicion, string clip)
        where T : LureObject
    {
        var go = new GameObject(name);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S(sprite);
        go.AddComponent<YSort>();
        var col = go.AddComponent<BoxCollider2D>();
        col.size = colliderSize;
        col.offset = colliderOffset;
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;

        Set(go.AddComponent<T>(),
            ("prompt", prompt), ("energyCost", cost), ("cooldown", cooldown), ("singleUse", singleUse),
            ("loudness", loudness), ("duration", duration), ("suspicion", suspicion),
            ("audioSource", src), ("clip", A(clip)), ("highlightSprite", sr));
        return go;
    }

    static GameObject MakeVase()
    {
        var go = MakeLure<LureObject>("Vase", "Props/vase.png", new Vector2(0.6f, 0.6f), new Vector2(0f, -0.2f),
            "Розбити глечик", 0f, 1f, true, 10f, 0f, 0.15f, "vase_crash");
        Set(go.GetComponent<LureObject>(), ("passableAfterUse", true));
        Set(go.AddComponent<LureVisuals>(),
            ("target", go.GetComponent<SpriteRenderer>()),
            ("activeFrames", new[] { S("Props/vase_broken.png") }),
            ("keepAfterDeactivate", true));
        return go;
    }

    static GameObject MakeTV()
    {
        var go = MakeLure<LureObject>("TV", "Props/tv_off.png", new Vector2(0.9f, 0.8f), new Vector2(0f, -0.1f),
            "Завести патефон", 0f, 8f, false, 14f, 5f, 0.2f, "tv_static");
        var glow = Child(go, "Glow", new Vector2(0f, 0.2f));
        AddLight(glow, LightPoint, new Color(1f, 0.75f, 0.4f), 0.7f, 2f);
        Set(go.AddComponent<LureVisuals>(),
            ("target", go.GetComponent<SpriteRenderer>()),
            ("activeFrames", new[] { S("Props/tv_on_0.png"), S("Props/tv_on_1.png") }),
            ("fps", 12f),
            ("activeOnly", glow));
        return go;
    }

    static GameObject MakePhone()
    {
        var go = MakeLure<LureObject>("Phone", "Props/phone.png", new Vector2(0.8f, 0.6f), new Vector2(0f, -0.2f),
            "Вдарити в дзвін", 0f, 8f, false, 19f, 3f, 0.2f, "phone_ring");
        var glow = Child(go, "Glow", Vector2.zero);
        AddLight(glow, LightPoint, new Color(1f, 0.85f, 0.4f), 0.8f, 1.6f);
        Set(go.AddComponent<LureVisuals>(),
            ("target", go.GetComponent<SpriteRenderer>()),
            ("activeFrames", new[] { S("Props/phone_ring.png"), S("Props/phone.png") }),
            ("fps", 8f),
            ("activeOnly", glow),
            ("shake", 0.03f));
        return go;
    }

    static GameObject MakeMannequin()
    {
        var go = MakeLure<MannequinLure>("Mannequin", "Props/mannequin.png", new Vector2(0.6f, 0.5f), new Vector2(0f, -0.25f),
            "Штовхнути обладунки", 0f, 1f, true, 11f, 0f, 0.3f, "mannequin_move");
        Set(go.GetComponent<MannequinLure>(), ("moveTime", 0.5f));
        return go;
    }

    static GameObject MakeLamp()
    {
        var go = new GameObject("Lamp");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S("Props/lamp.png");
        go.AddComponent<YSort>();
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.5f, 0.4f);
        col.offset = new Vector2(0f, -0.3f);

        var torchLight = Child(go, "Light", new Vector2(0f, 0.3f));
        AddLight(torchLight, LightPoint, new Color(1f, 0.65f, 0.3f), 1.25f, 3.2f, 360f, 360f, 0.7f);
        torchLight.AddComponent<TorchFlicker>();

        var zone = Child(go, "LightZone", Vector2.zero);
        var cc = zone.AddComponent<CircleCollider2D>();
        cc.isTrigger = true;
        cc.radius = 2.4f;
        zone.AddComponent<LightZone>();
        return go;
    }

    static GameObject MakeExit()
    {
        var go = new GameObject("Exit");
        var sr = go.AddComponent<SpriteRenderer>();
        var frames = Enumerable.Range(0, 4).Select(i => S($"Environment/exit_open_{i}.png")).Where(f => f != null).ToArray();
        sr.sprite = frames.Length > 0 ? frames[0] : S("Environment/exit_door.png");
        go.AddComponent<YSort>();
        // тригер трохи більший за клітинку: помічає привида біля дверей
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(1.4f, 1.4f);
        // твердий "блокер": не пускає в зачинені двері
        var block = Child(go, "Blocker", Vector2.zero).AddComponent<BoxCollider2D>();
        block.size = new Vector2(1f, 1f);
        Set(go.AddComponent<LevelExit>(),
            ("body", sr), ("lockedSprite", S("Environment/exit_locked.png")), ("openFrames", frames),
            ("blocker", block), ("openClip", A("door_open")), ("lockedClip", A("door_locked")));
        AddLight(Child(go, "Light", Vector2.zero), LightPoint, new Color(0.4f, 1f, 0.6f), 1.2f, 2.2f);
        return go;
    }

    static GameObject MakeKey()
    {
        var go = new GameObject("Key");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = S("Props/key.png");
        UseUnlit(sr);
        go.AddComponent<YSort>();
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.4f;
        Set(go.AddComponent<KeyPickup>(), ("pickupClip", A("key_pickup")));
        var glow = Child(go, "Glow", Vector2.zero);
        AddLight(glow, LightPoint, new Color(1f, 0.85f, 0.4f), 0.9f, 1.6f);
        glow.AddComponent<TorchFlicker>();
        return go;
    }

    static GameObject MakeLever()
    {
        var go = MakeLure<LeverLure>("Lever", "Props/lever_up.png", new Vector2(0.6f, 0.5f), new Vector2(0f, -0.2f),
            "Потягти важіль (шум почують вороги поруч)", 0f, 1f, true, 12f, 3f, 0.15f, "lever_pull");
        Set(go.GetComponent<LeverLure>(),
            ("body", go.GetComponent<SpriteRenderer>()), ("downSprite", S("Props/lever_down.png")));
        return go;
    }

    // ===================================================================== сцени

    static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera(Vector2.zero, new Color(0.04f, 0.04f, 0.08f));

        var menu = new GameObject("MainMenu");
        Set(menu.AddComponent<MainMenu>(),
            ("skinFrames", AllSkinFrames()),
            ("skinNames", GhostSkinLibrary.Names),
            ("music", MenuMusic()),
            ("startClip", A("whoosh")),
            ("moveClip", A("menu_move")),
            ("selectClip", A("menu_select")),
            ("gameTitle", "ПРИВИД ПІДЗЕМЕЛЛЯ"),
            ("background", S("UI/menu_background.png")));

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    static void BuildIntroScene(string firstLevelName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera(Vector2.zero, Color.black);
        var slides = Enumerable.Range(1, 5).Select(i => S($"Intro/intro_{i}.png")).Where(sp => sp != null).ToArray();
        var intro = new GameObject("Intro");
        Set(intro.AddComponent<IntroCutscene>(),
            ("slides", slides),
            ("lines", IntroLines),
            ("music", MenuMusic()),
            ("typeClip", A("type_blip")),
            ("nextTitle", "РІВЕНЬ 1"),
            ("nextName", firstLevelName),
            ("skinFrames", AllSkinFrames()));
        EditorSceneManager.SaveScene(scene, IntroScenePath);
    }

    static void BuildTwistScene(string nextLevelName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera(Vector2.zero, Color.black);
        var slides = Enumerable.Range(1, 4).Select(i => S($"Intro/Twist/twist_{i}.png")).ToArray();
        var story = new GameObject("Twist");
        Set(story.AddComponent<IntroCutscene>(),
            ("slides", slides), ("lines", TwistLines),
            ("music", MenuMusic()), ("typeClip", A("type_blip")),
            ("autoAdvanceDelay", 5f), ("fadeTime", 0.7f),
            ("nextTitle", "РІВЕНЬ 5"), ("nextName", nextLevelName),
            ("skinFrames", AllSkinFrames()));
        EditorSceneManager.SaveScene(scene, TwistScenePath);
    }

    static void BuildEndingScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera(Vector2.zero, Color.black);
        var slides = Enumerable.Range(1, 4).Select(i => S($"Intro/ending_{i}.png")).Where(sp => sp != null).ToArray();
        var ending = new GameObject("Ending");
        // привид у вибраному скіні: виринає з воріт, вилітає з арки, летить до сонця, а наостанок висить над написом
        Set(ending.AddComponent<IntroCutscene>(),
            ("slides", slides),
            ("lines", EndingLines),
            ("music", MenuMusic()),
            ("typeClip", A("type_blip")),
            ("autoAdvanceDelay", 5f),
            ("nextSceneIndex", 0),
            ("markWin", true),
            ("skinFrames", AllSkinFrames()),
            ("ghostPaths", new[]
            {
                new Vector4(0.5f, 0.3f, 0.5f, 0.55f),
                new Vector4(0.5f, 0.38f, 0.62f, 0.68f),
                new Vector4(0.12f, 0.55f, 0.58f, 0.62f),
                new Vector4(0.5f, 0.45f, 0.5f, 0.45f),
            }),
            ("ghostSizes", new[] { 0.16f, 0.2f, 0.18f, 0.3f }),
            ("ghostTravelTime", 4.5f),
            ("showStats", true));
        EditorSceneManager.SaveScene(scene, EndingScenePath);
    }

    [MenuItem("Tools/Ghost/Перебудувати тільки фінальний рівень")]
    public static void RebuildFinalLevelOnly()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Спочатку зупини Play Mode.");
        AssetDatabase.Refresh();
        MakeSpritesReadable();
        var prefabs = EnsurePrefabs(false);
        var data = LevelData.Parse(File.ReadAllText(LevelsFolder + "/level_05.txt", Encoding.UTF8));
        BuildLevelScene(data, 5, prefabs, ScenesFolder + "/Level_05.unity", "ФІНАЛ", "Нагору, до світанку");
        BuildTwistScene(data.name);
        BuildEndingScene();
        AssetDatabase.SaveAssets();
        Debug.Log("FINAL_LEVEL_REBUILT: Ключник, пастка, ключ, замкнений вихід.");
    }

    static void BuildLevelScene(LevelData data, int number, PrefabSet prefabs, string scenePath, string nextTitle, string nextName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string[] rows = data.rows;
        int h = rows.Length;

        string theme = Themes.ContainsKey(data.theme) ? data.theme : "cellar";
        var look = Themes[theme];
        Sprite floorA = TS(theme, "floor_a"), floorB = TS(theme, "floor_b");
        Sprite wallTop = TS(theme, "wall_top"), wallFront = TS(theme, "wall_front");
        Sprite shelfA = S("Props/shelf_a.png"), shelfB = S("Props/shelf_b.png");
        Sprite counter = S("Props/counter.png"), plant = S("Props/plant.png");
        Sprite shelfC = S("Props/shelf_c.png") ?? shelfA, shelfD = S("Props/shelf_d.png") ?? shelfB;
        Sprite rack = S("Props/rack.png"), boxes = S("Props/boxes.png"), cart = S("Props/cart.png"), pumpkin = S("Props/pumpkin.png");
        Sprite column = TS(theme, "column"), carpet = TS(theme, "carpet") ?? floorA;
        Sprite water = S("Environment/water.png") ?? floorA;
        Sprite[] wallVariants = WallVariantPaths.Select(p => TS(theme, Path.GetFileNameWithoutExtension(p)) ?? wallFront).ToArray();

        var level = new GameObject("Level").transform;
        var floorRoot = Group("Floor", level);
        var wallRoot = Group("Walls", level);
        var propRoot = Group("Props", level);
        var lureRoot = Group("Lures", level);
        var enemyRoot = Group("Enemies", level);

        var gridGo = new GameObject("LevelGrid");
        gridGo.AddComponent<LevelGrid>().SetRows(rows);

        GameObject player = null, exitGo = null;
        var chains = new List<Vector2>();    // F: ланцюги вантажів
        var impacts = new List<Vector2>();   // U: дзвін під вантажем (туди падає камінь)
        var mannequins = new List<GameObject>();
        var targets = new List<Vector2>();
        var marks = new Dictionary<char, Vector2>();   // точки маршрутів: 1..9 і a..l

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                char ch = rows[y][x];
                if (ch == ' ') continue;
                var pos = new Vector2(x, -y);
                int order = y * 10;

                if (ch != '#') Tile(floorRoot, ch == ',' ? carpet : ch == '~' ? water : (x + y) % 2 == 0 ? floorB : floorA, pos, -10000, Vector2.zero);

                switch (ch)
                {
                    case '#':
                        bool front = y + 1 < h && rows[y + 1][x] != '#' && rows[y + 1][x] != ' ';
                        Tile(wallRoot, front ? wallVariants[LevelGrid.WallVariant(x, y, wallVariants.Length)] : wallTop, pos, order, Vector2.one);
                        break;
                    case 'S': Tile(propRoot, LevelGrid.ShelfVariant(x, y) == 0 ? shelfA : shelfC, pos, order, Vector2.one); break;
                    case 's': Tile(propRoot, LevelGrid.ShelfVariant(x, y) == 0 ? shelfB : shelfD, pos, order, Vector2.one); break;
                    case 'O': Tile(propRoot, column, pos, order, Vector2.one); break;
                    case 'R': Tile(propRoot, rack, pos, order, new Vector2(0.8f, 0.5f)); break;
                    case 'B': Tile(propRoot, boxes, pos, order, new Vector2(0.9f, 0.8f)); break;
                    case 'K': Tile(propRoot, cart, pos, order, new Vector2(0.8f, 0.6f)); break;
                    case 'J':
                        Tile(propRoot, pumpkin, pos, order, new Vector2(0.6f, 0.5f));
                        var candleGlow = Child(propRoot.GetChild(propRoot.childCount - 1).gameObject, "Glow", Vector2.zero);
                        AddLight(candleGlow, LightPoint, new Color(1f, 0.7f, 0.3f), 0.9f, 1.7f);
                        candleGlow.AddComponent<TorchFlicker>();
                        break;
                    case 'C': Tile(propRoot, counter, pos, order, new Vector2(1f, 0.8f)); break;
                    case 'p': Tile(propRoot, plant, pos, order, new Vector2(0.7f, 0.7f)); break;
                    case 'V': Place(prefabs.vase, pos, lureRoot); break;
                    case 'T': Place(prefabs.tv, pos, lureRoot); break;
                    case 'H': Place(prefabs.phone, pos, lureRoot); break;
                    case 'L': Place(prefabs.lamp, pos, propRoot); break;
                    case 'E': exitGo = Place(prefabs.exit, pos, level); break;
                    case '*': Place(prefabs.key, pos, lureRoot); break;
                    case 'X': Place(prefabs.lever, pos, lureRoot); break;
                    case 'F': chains.Add(pos); break;
                    case 'U': Place(prefabs.phone, pos, lureRoot); impacts.Add(pos); break;
                    case 'M': mannequins.Add(Place(prefabs.mannequin, pos, lureRoot)); break;
                    case 'm': targets.Add(pos); break;
                    case 'P': player = Place(prefabs.player, pos, null); break;
                    default:
                        if (char.IsDigit(ch) || (ch >= 'a' && ch <= 'l')) marks[ch] = pos;
                        break;
                }
            }
        }

        AddWallEdges(rows, wallRoot, floorRoot);

        // Манекени їдуть до найближчої позначки "m"
        foreach (var m in mannequins)
        {
            if (targets.Count == 0) break;
            Vector2 mp = m.transform.position;
            Vector2 best = targets.OrderBy(t => (t - mp).sqrMagnitude).First();
            var target = new GameObject("MannequinTarget");
            target.transform.SetParent(lureRoot, false);
            target.transform.position = best;
            Set(m.GetComponent<MannequinLure>(), ("targetPoint", target.transform));
        }

        // Є ключ чи важіль - вихід замкнений
        bool bossLevel = data.bossPoints.Length > 0;
        bool hasLock = bossLevel || rows.Any(row => row.IndexOf('*') >= 0 || row.IndexOf('X') >= 0);
        if (exitGo != null && hasLock)
        {
            var exitComp = exitGo.GetComponent<LevelExit>();
            Set(exitComp, ("startsLocked", true));
            if (bossLevel) Set(exitComp, ("requiresKey", true));
            PrefabUtility.RecordPrefabInstancePropertyModifications(exitComp);
        }

        // Скелети й духи
        foreach (var spec in data.enemies)
        {
            var points = spec.points.Where(d => d.Length > 0 && marks.ContainsKey(d[0])).Select(d => marks[d[0]]).ToArray();
            if (points.Length == 0)
            {
                Debug.LogWarning($"{scenePath}: маршрут '{string.Join(" ", spec.points)}' не знайдено на карті");
                continue;
            }
            var g = Place(spec.spirit ? prefabs.spirit : prefabs.skeleton, points[0], enemyRoot);
            var ai = g.GetComponent<GuardAI>();
            ai.Configure(points);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
        }

        if (bossLevel)
        {
            var points = data.bossPoints.Where(p => p.Length > 0 && marks.ContainsKey(p[0])).Select(p => marks[p[0]]).ToArray();
            // старі карти: один ланцюг і камінь над дзвоном H у центрі зали
            if (impacts.Count == 0 && chains.Count > 0) impacts.Add(new Vector2(17f, -11f));
            if (points.Length == 0 || chains.Count == 0) throw new InvalidOperationException("На карті з Ключником потрібні boss: маршрут, F (ланцюг) і U (дзвін під вантажем).");
            // кожен ланцюг керує найближчим вільним вантажем
            var traps = new List<BossTrap>();
            var freeImpacts = new List<Vector2>(impacts);
            foreach (var pair in chains.SelectMany(c => impacts.Select(u => (c, u))).OrderBy(t => (t.c - t.u).sqrMagnitude))
            {
                if (!freeImpacts.Contains(pair.u) || traps.Any(t => (Vector2)t.transform.position == pair.c)) continue;
                freeImpacts.Remove(pair.u);
                traps.Add(MakeBossTrap(pair.c, pair.u, lureRoot));
            }
            var bossGo = Place(prefabs.skeleton, points[0], enemyRoot);
            bossGo.name = "Jailer";
            bossGo.transform.localScale = Vector3.one * 1.75f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(bossGo.transform);
            PrefabUtility.RecordPrefabInstancePropertyModifications(bossGo);
            var ai = bossGo.GetComponent<GuardAI>();
            Set(ai, ("walkSpeed", 1.3f), ("investigateSpeed", 2.2f), ("chaseSpeed", 2.7f),
                ("noticeTime", 0.85f), ("catchDistance", 0.8f), ("waitAtPoint", 0.8f));
            ai.Configure(points);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
            var key = Place(prefabs.key, points[0], lureRoot);
            key.name = "KeyJailer";
            key.SetActive(false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(key);
            var jailer = bossGo.AddComponent<BossJailer>();
            Set(jailer, ("droppedKey", key), ("hitsNeeded", Mathf.Max(1, traps.Count)));
            foreach (var trap in traps) Set(trap, ("boss", jailer));
            var belt = Child(bossGo, "KeyBelt", new Vector2(0.28f, -0.05f)).AddComponent<SpriteRenderer>();
            belt.sprite = S("Props/key.png");
            belt.sortingOrder = 100;
            belt.transform.localScale = Vector3.one * 0.5f;
            UseUnlit(belt);
        }

        // Місячні промені крізь ґрати в стелі
        for (int i = 0; i < data.moons.Count; i++)
        {
            var pts = data.moons[i].Where(d => d.Length > 0 && marks.ContainsKey(d[0])).Select(d => marks[d[0]]).ToArray();
            if (pts.Length < 2)
            {
                Debug.LogWarning($"{scenePath}: місячний промінь '{string.Join(" ", data.moons[i])}' не знайдено на карті");
                continue;
            }
            MakeMoonBeam(level, pts[0], pts[1], i);
        }

        // Системи
        var systems = new GameObject("GameManager");
        Set(systems.AddComponent<GameManager>(), ("levelNumber", number), ("levelTitle", data.name), ("levelHint", data.hint),
            ("nextTitle", nextTitle), ("nextName", nextName));
        systems.AddComponent<SuspicionMeter>();
        Set(systems.AddComponent<GameAudio>(),
            ("music", LevelMusic()),
            ("chaseMusic", AssetDatabase.LoadAssetAtPath<AudioClip>(ChaseMusicPath)),
            ("caughtClip", A("caught")),
            ("levelCompleteClip", A("level_complete")),
            ("noEnergyClip", A("no_energy")),
            ("interactClip", A("whoosh")));
        systems.AddComponent<GameUI>();
        Set(systems.AddComponent<PauseMenu>(), ("moveClip", A("menu_move")), ("selectClip", A("menu_select")));
        if (number == 1) Set(systems.AddComponent<TutorialHints>(), ("popClip", A("menu_select")));

        // Світло й камера
        AddLight(new GameObject("Global Light 2D"), LightGlobal, look.light, look.intensity);
        var cam = MakeCamera(player != null ? (Vector2)player.transform.position : Vector2.zero, new Color(0.02f, 0.02f, 0.04f));
        if (player != null)
        {
            Set(cam.gameObject.AddComponent<CameraFollow>(), ("target", player.transform));
            if (player.GetComponent<FirstPersonMovement>() == null) player.AddComponent<FirstPersonMovement>();
        }
        else Debug.LogWarning($"{scenePath}: на карті немає P (старт привида)");

        // 3D-вигляд від першої особи (вимикається сам, якщо в меню вибрано 2D)
        Set(cam.gameObject.AddComponent<RetroView>(),
            ("floorA", floorA), ("floorB", floorB), ("wallTexture", wallFront),
            ("shelfA", shelfA), ("shelfB", shelfB), ("exitDoor", S("Environment/exit_door.png")),
            ("shelfC", shelfC), ("shelfD", shelfD), ("column", column), ("carpet", carpet), ("wallVariants", wallVariants),
            ("water", water), ("ceilingTexture", TS(theme, "ceiling")), ("fogColor", look.fog));

        EditorSceneManager.SaveScene(scene, scenePath);
    }

    /// <summary>
    /// 2D: щоб було видно, де стіна, а де прохід. На верхній грані стіни з боку проходу - світла кромка,
    /// на підлозі під стіною й поруч - м'яка тінь. У 3D ці спрайти не малюються (батьки Walls і Floor).
    /// </summary>
    static void AddWallEdges(string[] rows, Transform wallRoot, Transform floorRoot)
    {
        Sprite rimN = S("Environment/edge_rim_n.png"), rimW = S("Environment/edge_rim_w.png"), rimE = S("Environment/edge_rim_e.png");
        Sprite shN = S("Environment/floor_shadow_n.png"), shW = S("Environment/floor_shadow_w.png"), shE = S("Environment/floor_shadow_e.png");
        if (rimN == null || shN == null) return;
        int h = rows.Length;
        char Get(int x, int y) => y >= 0 && y < h && x >= 0 && x < rows[y].Length ? rows[y][x] : ' ';
        bool Open(char c) => c != '#' && c != ' ' && c != 'E';
        for (int y = 0; y < h; y++)
        for (int x = 0; x < rows[y].Length; x++)
        {
            char c = rows[y][x];
            var pos = new Vector2(x, -y);
            if (c == '#')
            {
                if (Open(Get(x, y + 1))) continue;   // фасад стіни має власний верх
                int order = y * 10 + 1;
                if (Open(Get(x, y - 1))) Tile(wallRoot, rimN, pos, order, Vector2.zero);
                if (Open(Get(x - 1, y))) Tile(wallRoot, rimW, pos, order, Vector2.zero);
                if (Open(Get(x + 1, y))) Tile(wallRoot, rimE, pos, order, Vector2.zero);
            }
            else if (c != ' ' && c != 'E')
            {
                if (Get(x, y - 1) == '#') Tile(floorRoot, shN, pos, -9990, Vector2.zero);
                if (Get(x - 1, y) == '#') Tile(floorRoot, shW, pos, -9990, Vector2.zero);
                if (Get(x + 1, y) == '#') Tile(floorRoot, shE, pos, -9990, Vector2.zero);
            }
        }
    }

    static void MakeMoonBeam(Transform parent, Vector2 from, Vector2 to, int index)
    {
        var go = new GameObject("MoonBeam");
        go.transform.SetParent(parent, false);
        go.transform.position = from;
        var disc = Child(go, "MoonDisc", Vector2.zero).AddComponent<SpriteRenderer>();
        disc.sprite = S("Environment/moon_pool.png");
        disc.color = new Color(0.72f, 0.84f, 1f, 0.2f);
        disc.sortingOrder = -9500;
        disc.transform.localScale = new Vector3(2.6f, 2.6f, 1f) / Mathf.Max(0.01f, disc.sprite != null ? disc.sprite.bounds.size.x : 1f);
        UseUnlit(disc);
        var light = Child(go, "Moonbeam", Vector2.zero);
        AddLight(light, LightPoint, new Color(0.62f, 0.76f, 1f), 1.1f, 1.7f);
        float length = Vector2.Distance(from, to);
        Set(go.AddComponent<MoonBeam>(), ("from", from), ("to", to), ("radius", 1.3f),
            ("travelTime", Mathf.Max(4f, length * 1.1f)), ("phase", index * 0.37f), ("disc", disc));
    }

    static BossTrap MakeBossTrap(Vector2 control, Vector2 impact, Transform parent)
    {
        var go = new GameObject("TrapChain");
        go.transform.SetParent(parent, false);
        go.transform.position = control;
        // Окремий дочірній малюнок зберігає одиничний масштаб кореня колайдера й пастки.
        var visual = Child(go, "TrapChainVisual", Vector2.zero).AddComponent<SpriteRenderer>();
        visual.sprite = S("Props/Finale/pull_chain.png");
        UseUnlit(visual);
        if (visual.sprite != null) visual.transform.localScale = Vector3.one * (1.6f / visual.sprite.bounds.size.y);
        go.AddComponent<BoxCollider2D>().isTrigger = true;
        var trap = go.AddComponent<BossTrap>();
        Set(trap, ("energyCost", 0f), ("cooldown", 4f), ("singleUse", false),
            ("impactPosition", impact), ("highlightSprite", visual),
            ("chainClip", A("lever_pull")), ("impactClip", A("vase_crash")));
        var stone = Child(go, "HangingStone", impact - control + Vector2.up * 1.6f).AddComponent<SpriteRenderer>();
        stone.sprite = S("Props/Finale/hanging_stone.png");
        if (stone.sprite != null) stone.transform.localScale = Vector3.one * (1.5f / stone.sprite.bounds.size.y);
        UseUnlit(stone);
        var marker = Child(go, "TrapMark", impact - control).AddComponent<SpriteRenderer>();
        marker.sprite = UIFactory.UISprite("ui_shadow");
        marker.color = new Color(1f, 0.65f, 0.1f, 0.4f);
        marker.sortingOrder = -9000;
        UseUnlit(marker);
        Set(trap, ("stone", stone), ("marker", marker));
        return trap;
    }

    static Camera MakeCamera(Vector2 position, Color background)
    {
        var go = new GameObject("Main Camera");
        go.tag = "MainCamera";
        go.transform.position = new Vector3(position.x, position.y, -10f);
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6.5f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        go.AddComponent<AudioListener>();
        return cam;
    }

    static void SetBuildScenes(List<string> ours)
    {
        var list = ours.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (ours.Contains(s.path)) continue;
            string sceneName = Path.GetFileNameWithoutExtension(s.path);
            if (sceneName.StartsWith("Level_") || sceneName == "Intro" || sceneName == "Twist" || sceneName == "Ending") continue;
            list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ===================================================================== помічники

    static Sprite S(string relativePath) => TestSceneBuilder.LoadPixelSprite(Spr + relativePath);

    /// <summary>Текстура з теми рівня, а якщо там такої немає - звичайна з Environment.</summary>
    static Sprite TS(string theme, string file)
    {
        string themed = $"{Spr}Environment/Themes/{theme}/{file}.png";
        if (theme != "cellar" && File.Exists(themed)) return TestSceneBuilder.LoadPixelSprite(themed);
        return S($"Environment/{file}.png");
    }

    static Sprite[] Frames(string relativePrefix, int count)
    {
        var list = new List<Sprite>();
        for (int i = 0; i < count; i++)
        {
            var s = S($"{relativePrefix}{i}.png");
            if (s != null) list.Add(s);
        }
        return list.ToArray();
    }

    /// <summary>14 кадрів привида (idle 0-3, move 0-3, caught 0-5) з підпапки скіна.</summary>
    static Object[] GhostFrames(string folder)
    {
        var list = new List<Object>();
        foreach (string path in GhostSkinLibrary.FramePaths(folder))
        {
            var s = TestSceneBuilder.LoadPixelSprite(path);
            if (s == null) Debug.LogWarning("Немає кадру скіна: " + path);
            list.Add(s);
        }
        return list.ToArray();
    }

    /// <summary>Кадри всіх скінів з GhostSkinLibrary підряд.</summary>
    static Object[] AllSkinFrames()
    {
        var list = new List<Object>();
        foreach (string folder in GhostSkinLibrary.Folders) list.AddRange(GhostFrames(folder));
        return list.ToArray();
    }

    static AudioClip A(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{Sfx}{name}.wav");

    static void UseUnlit(SpriteRenderer sr)
    {
        var unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterialPath);
        if (unlit != null) sr.sharedMaterial = unlit;
    }

    static Transform Group(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    static GameObject Child(GameObject parent, string name, Vector2 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    static void Tile(Transform parent, Sprite sprite, Vector2 pos, int order, Vector2 colliderSize)
    {
        var go = new GameObject(sprite != null ? sprite.name : "tile");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        if (colliderSize != Vector2.zero) go.AddComponent<BoxCollider2D>().size = colliderSize;
    }

    static GameObject Place(GameObject prefab, Vector2 pos, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }

    static Type light2DType;

    /// <summary>Light2D шукається через рефлексію, щоб скрипт компілювався за будь-якої версії URP.</summary>
    static Type Light2DType
    {
        get
        {
            if (light2DType == null)
                light2DType = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType("UnityEngine.Rendering.Universal.Light2D", false))
                    .FirstOrDefault(t => t != null);
            return light2DType;
        }
    }

    static Component AddLight(GameObject go, int type, Color color, float intensity, float radius = 1f,
        float outerAngle = 360f, float innerAngle = 360f, float falloff = 0.5f, float innerRadius = 0f)
    {
        if (Light2DType == null)
        {
            Debug.LogWarning("Light2D не знайдено (немає URP 2D?) - світла не буде.");
            return null;
        }

        var light = go.AddComponent(Light2DType);
        Set(light,
            ("m_LightType", type), ("m_Color", color), ("m_Intensity", intensity), ("m_FalloffIntensity", falloff),
            ("m_PointLightOuterRadius", radius), ("m_PointLightInnerRadius", innerRadius),
            ("m_PointLightOuterAngle", outerAngle), ("m_PointLightInnerAngle", innerAngle));

        // світло діє на всі шари сортування
        var so = new SerializedObject(light);
        var layers = so.FindProperty("m_ApplyToSortingLayers");
        if (layers != null && layers.isArray)
        {
            var all = SortingLayer.layers;
            layers.arraySize = all.Length;
            for (int i = 0; i < all.Length; i++) layers.GetArrayElementAtIndex(i).intValue = all[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        return light;
    }

    /// <summary>Записує значення в [SerializeField] поля так само, як це робиться в інспекторі.</summary>
    static void Set(Object target, params (string name, object value)[] props)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        foreach (var (name, value) in props)
        {
            var p = so.FindProperty(name);
            if (p == null)
            {
                Debug.LogWarning($"{target.GetType().Name}: поле '{name}' не знайдено");
                continue;
            }
            switch (value)
            {
                case null: p.objectReferenceValue = null; break;
                case float f: p.floatValue = f; break;
                case int i:
                    if (p.propertyType == SerializedPropertyType.Float) p.floatValue = i;
                    else p.intValue = i;
                    break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case string[] strs:
                    p.arraySize = strs.Length;
                    for (int k = 0; k < strs.Length; k++) p.GetArrayElementAtIndex(k).stringValue = strs[k];
                    break;
                case Color c: p.colorValue = c; break;
                case Vector4[] v4:
                    p.arraySize = v4.Length;
                    for (int k = 0; k < v4.Length; k++) p.GetArrayElementAtIndex(k).vector4Value = v4[k];
                    break;
                case float[] fl:
                    p.arraySize = fl.Length;
                    for (int k = 0; k < fl.Length; k++) p.GetArrayElementAtIndex(k).floatValue = fl[k];
                    break;
                case Vector2 v: p.vector2Value = v; break;
                case Object[] arr:
                    p.arraySize = arr.Length;
                    for (int k = 0; k < arr.Length; k++) p.GetArrayElementAtIndex(k).objectReferenceValue = arr[k];
                    break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ===================================================================== формат рівня

    class LevelData
    {
        public string name = "";
        public string hint = "";
        public string theme = "cellar";
        public readonly List<(bool spirit, string[] points)> enemies = new List<(bool, string[])>();
        public string[] bossPoints = new string[0];
        public readonly List<string[]> moons = new List<string[]>();
        public string[] rows = new string[0];

        public static LevelData Parse(string text)
        {
            var d = new LevelData();
            var rows = new List<string>();
            bool inMap = false;

            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                if (inMap)
                {
                    rows.Add(raw);
                    continue;
                }
                if (raw.StartsWith("map:"))
                {
                    inMap = true;
                    continue;
                }
                int colon = raw.IndexOf(':');
                if (colon < 0) continue;
                string key = raw.Substring(0, colon).Trim();
                string val = raw.Substring(colon + 1).Trim();
                if (key == "name") d.name = val;
                else if (key == "hint") d.hint = val;
                else if (key == "theme") d.theme = val.ToLowerInvariant();
                else if (key == "boss") d.bossPoints = val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                else if (key == "moon") d.moons.Add(val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                else if (key == "skeleton" || key == "guard" || key == "spirit")
                    d.enemies.Add((key == "spirit", val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)));
            }

            while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0) rows.RemoveAt(rows.Count - 1);
            while (rows.Count > 0 && rows[0].Trim().Length == 0) rows.RemoveAt(0);
            int width = rows.Count > 0 ? rows.Max(r => r.Length) : 0;
            d.rows = rows.Select(r => r.PadRight(width)).ToArray();
            return d;
        }
    }
}

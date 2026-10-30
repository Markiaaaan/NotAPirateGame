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
/// 1. Створює префаби (Player, Guard, Vase, TV, Phone, Mannequin, Lamp, Exit) в Assets/Prefabs,
///    якщо їх ще немає. Існуючі префаби НЕ чіпає, тож їх можна спокійно налаштовувати руками.
/// 2. Будує сцену меню і по сцені на кожен файл Assets/Levels/*.txt (Level_01, Level_02, ...).
/// 3. Записує сцени в Build Profiles у правильному порядку.
///
/// Рівні можна редагувати прямо в .txt і перезбирати. Легенда символів - у README_GAME.md.
/// </summary>
public static class GameBuilder
{
    const string LevelsFolder = "Assets/Levels";
    const string ScenesFolder = "Assets/Scenes";
    const string PrefabsFolder = "Assets/Prefabs";
    const string MenuScenePath = ScenesFolder + "/MainMenu.unity";
    const string Spr = "Assets/Sprites/";
    const string Sfx = "Assets/Audio/SFX/";
    const string MusicPath = "Assets/Audio/Music/ambient_loop.wav";
    const string UnlitMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    const int LightPoint = 3;
    const int LightGlobal = 4;

    class PrefabSet
    {
        public GameObject player, guard, vase, tv, phone, mannequin, lamp, exit;
    }

    // ===================================================================== меню

    [MenuItem("Tools/Ghost/Зібрати всю гру", priority = 0)]
    public static void BuildAllMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildAll(false);
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

            EditorUtility.DisplayProgressBar("Ghost", "Префаби...", 0.1f);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(PrefabsFolder);
            EnsureFolder(ScenesFolder);
            var prefabs = EnsurePrefabs(forcePrefabs);

            EditorUtility.DisplayProgressBar("Ghost", "Меню...", 0.2f);
            var scenes = new List<string>();
            BuildMenuScene();
            scenes.Add(MenuScenePath);

            for (int i = 0; i < levelFiles.Length; i++)
            {
                EditorUtility.DisplayProgressBar("Ghost", $"Рівень {i + 1}...", 0.3f + 0.6f * i / levelFiles.Length);
                var data = LevelData.Parse(File.ReadAllText(levelFiles[i], Encoding.UTF8));
                string path = $"{ScenesFolder}/Level_{i + 1:00}.unity";
                BuildLevelScene(data, i + 1, prefabs, path);
                scenes.Add(path);
            }

            SetBuildScenes(scenes);
            EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);
            Debug.Log($"Гру зібрано: меню + рівнів: {levelFiles.Length}. Тисни Play.");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }

    // ===================================================================== префаби

    static PrefabSet EnsurePrefabs(bool force)
    {
        return new PrefabSet
        {
            player = EnsurePrefab("Player", force, MakePlayer),
            guard = EnsurePrefab("Guard", force, MakeGuard),
            vase = EnsurePrefab("Vase", force, MakeVase),
            tv = EnsurePrefab("TV", force, MakeTV),
            phone = EnsurePrefab("Phone", force, MakePhone),
            mannequin = EnsurePrefab("Mannequin", force, MakeMannequin),
            lamp = EnsurePrefab("Lamp", force, MakeLamp),
            exit = EnsurePrefab("Exit", force, MakeExit),
        };
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
        go.AddComponent<PlayerEnergy>();
        go.AddComponent<PlayerInteractor>();
        Set(go.AddComponent<GhostVisibility>(), ("hiddenAlpha", 0.3f), ("fadeSpeed", 4f), ("detectThreshold", 0.45f));
        Set(go.AddComponent<YSort>(), ("offset", 5));

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(GhostAnimationSetup.ControllerPath);
        if (controller == null) controller = GhostAnimationSetup.CreateAssets();
        if (controller != null) GhostAnimationSetup.AttachTo(go, controller);

        // слабке світіння навколо привида, щоб гравцю було видно, куди летіти
        AddLight(Child(go, "Glow", Vector2.zero), LightPoint, new Color(0.6f, 0.85f, 1f), 0.45f, 2.4f);
        return go;
    }

    static GameObject MakeGuard()
    {
        var go = new GameObject("Guard");
        var sr = go.AddComponent<SpriteRenderer>();
        var down = Frames("Characters/Guard/guard_down_", 4);
        sr.sprite = down.Length > 0 ? down[0] : null;
        UseUnlit(sr);
        Set(go.AddComponent<YSort>(), ("offset", 5));

        var flashlight = Child(go, "Flashlight", Vector2.zero);
        AddLight(flashlight, LightPoint, new Color(1f, 0.95f, 0.75f), 1.4f, 5.5f, 70f, 40f, 0.6f, 0.3f);

        var alert = Child(go, "Alert", new Vector2(0f, 0.95f));
        var alertSr = alert.AddComponent<SpriteRenderer>();
        alertSr.sortingOrder = 30000;
        alertSr.enabled = false;
        UseUnlit(alertSr);

        Set(go.AddComponent<GuardAI>(),
            ("body", sr),
            ("walkDown", down),
            ("walkUp", Frames("Characters/Guard/guard_up_", 4)),
            ("walkSide", Frames("Characters/Guard/guard_side_", 4)),
            ("flashlight", flashlight.transform),
            ("alertIcon", alertSr),
            ("exclaimSprite", S("UI/alert_exclaim.png")),
            ("questionSprite", S("UI/alert_question.png")),
            ("alertClip", A("guard_alert")),
            ("huhClip", A("guard_huh")));
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
            "Скинути вазу", 10f, 1f, true, 7f, 0f, 0.15f, "vase_crash");
        Set(go.AddComponent<LureVisuals>(),
            ("target", go.GetComponent<SpriteRenderer>()),
            ("activeFrames", new[] { S("Props/vase_broken.png") }),
            ("keepAfterDeactivate", true));
        return go;
    }

    static GameObject MakeTV()
    {
        var go = MakeLure<LureObject>("TV", "Props/tv_off.png", new Vector2(0.9f, 0.8f), new Vector2(0f, -0.1f),
            "Увімкнути телевізор", 20f, 8f, false, 10f, 5f, 0.2f, "tv_static");
        var glow = Child(go, "Glow", new Vector2(0f, 0.2f));
        AddLight(glow, LightPoint, new Color(0.55f, 0.75f, 1f), 1.3f, 3f);
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
            "Подзвонити на телефон", 25f, 6f, false, 15f, 3f, 0.2f, "phone_ring");
        var glow = Child(go, "Glow", Vector2.zero);
        AddLight(glow, LightPoint, new Color(1f, 0.35f, 0.35f), 1f, 1.6f);
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
            "Штовхнути манекен", 15f, 1f, true, 8f, 0f, 0.3f, "mannequin_move");
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

        AddLight(Child(go, "Light", new Vector2(0f, 0.3f)), LightPoint, new Color(1f, 0.85f, 0.55f), 1.15f, 3.2f, 360f, 360f, 0.7f);

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
        sr.sprite = S("Environment/exit_door.png");
        go.AddComponent<YSort>();
        var col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(0.9f, 0.9f);
        go.AddComponent<LevelExit>();
        AddLight(Child(go, "Light", Vector2.zero), LightPoint, new Color(0.4f, 1f, 0.6f), 1.2f, 2.2f);
        return go;
    }

    // ===================================================================== сцени

    static void BuildMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        MakeCamera(Vector2.zero, new Color(0.04f, 0.04f, 0.08f));

        var menu = new GameObject("MainMenu");
        Set(menu.AddComponent<MainMenu>(),
            ("ghostFrames", Frames("Characters/Ghost/ghost_idle_", 4)),
            ("guardFrames", Frames("Characters/Guard/guard_side_", 4)),
            ("music", AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath)),
            ("startClip", A("whoosh")));

        EditorSceneManager.SaveScene(scene, MenuScenePath);
    }

    static void BuildLevelScene(LevelData data, int number, PrefabSet prefabs, string scenePath)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        string[] rows = data.rows;
        int h = rows.Length;

        Sprite floorA = S("Environment/floor_a.png"), floorB = S("Environment/floor_b.png");
        Sprite wallTop = S("Environment/wall_top.png"), wallFront = S("Environment/wall_front.png");
        Sprite shelfA = S("Props/shelf_a.png"), shelfB = S("Props/shelf_b.png");
        Sprite counter = S("Props/counter.png"), plant = S("Props/plant.png");

        var level = new GameObject("Level").transform;
        var floorRoot = Group("Floor", level);
        var wallRoot = Group("Walls", level);
        var propRoot = Group("Props", level);
        var lureRoot = Group("Lures", level);
        var guardRoot = Group("Guards", level);

        var gridGo = new GameObject("LevelGrid");
        gridGo.AddComponent<LevelGrid>().SetRows(rows);

        GameObject player = null;
        var mannequins = new List<GameObject>();
        var targets = new List<Vector2>();
        var digits = new Dictionary<char, Vector2>();

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < rows[y].Length; x++)
            {
                char ch = rows[y][x];
                if (ch == ' ') continue;
                var pos = new Vector2(x, -y);
                int order = y * 10;

                if (ch != '#') Tile(floorRoot, (x + y) % 2 == 0 ? floorB : floorA, pos, -10000, Vector2.zero);

                switch (ch)
                {
                    case '#':
                        bool front = y + 1 < h && rows[y + 1][x] != '#' && rows[y + 1][x] != ' ';
                        Tile(wallRoot, front ? wallFront : wallTop, pos, order, Vector2.one);
                        break;
                    case 'S': Tile(propRoot, shelfA, pos, order, Vector2.one); break;
                    case 's': Tile(propRoot, shelfB, pos, order, Vector2.one); break;
                    case 'C': Tile(propRoot, counter, pos, order, new Vector2(1f, 0.8f)); break;
                    case 'p': Tile(propRoot, plant, pos, order, new Vector2(0.7f, 0.7f)); break;
                    case 'V': Place(prefabs.vase, pos, lureRoot); break;
                    case 'T': Place(prefabs.tv, pos, lureRoot); break;
                    case 'H': Place(prefabs.phone, pos, lureRoot); break;
                    case 'L': Place(prefabs.lamp, pos, propRoot); break;
                    case 'E': Place(prefabs.exit, pos, level); break;
                    case 'M': mannequins.Add(Place(prefabs.mannequin, pos, lureRoot)); break;
                    case 'm': targets.Add(pos); break;
                    case 'P': player = Place(prefabs.player, pos, null); break;
                    default:
                        if (char.IsDigit(ch)) digits[ch] = pos;
                        break;
                }
            }
        }

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

        // Сторожі
        foreach (var spec in data.guards)
        {
            var points = spec.Where(d => d.Length > 0 && digits.ContainsKey(d[0])).Select(d => digits[d[0]]).ToArray();
            if (points.Length == 0) continue;
            var g = Place(prefabs.guard, points[0], guardRoot);
            var ai = g.GetComponent<GuardAI>();
            ai.Configure(points);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
        }

        // Системи
        var systems = new GameObject("GameManager");
        Set(systems.AddComponent<GameManager>(), ("levelTitle", $"Рівень {number}: {data.name}"), ("levelHint", data.hint));
        systems.AddComponent<SuspicionMeter>();
        Set(systems.AddComponent<GameAudio>(),
            ("music", AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath)),
            ("caughtClip", A("caught")),
            ("levelCompleteClip", A("level_complete")),
            ("noEnergyClip", A("no_energy")),
            ("interactClip", A("whoosh")));
        Set(systems.AddComponent<GameUI>(), ("energyIcon", S("UI/icon_energy.png")), ("eyeIcon", S("UI/icon_eye.png")));

        // Світло й камера
        AddLight(new GameObject("Global Light 2D"), LightGlobal, new Color(0.62f, 0.66f, 0.95f), 0.45f);
        var cam = MakeCamera(player != null ? (Vector2)player.transform.position : Vector2.zero, new Color(0.02f, 0.02f, 0.04f));
        if (player != null) Set(cam.gameObject.AddComponent<CameraFollow>(), ("target", player.transform));
        else Debug.LogWarning($"{scenePath}: на карті немає P (старт привида)");

        EditorSceneManager.SaveScene(scene, scenePath);
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
            if (Path.GetFileNameWithoutExtension(s.path).StartsWith("Level_")) continue;
            list.Add(new EditorBuildSettingsScene(s.path, s.enabled));
        }
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ===================================================================== помічники

    static Sprite S(string relativePath) => TestSceneBuilder.LoadPixelSprite(Spr + relativePath);

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
                case Color c: p.colorValue = c; break;
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
        public readonly List<string[]> guards = new List<string[]>();
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
                else if (key == "guard") d.guards.Add(val.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            }

            while (rows.Count > 0 && rows[rows.Count - 1].Trim().Length == 0) rows.RemoveAt(rows.Count - 1);
            while (rows.Count > 0 && rows[0].Trim().Length == 0) rows.RemoveAt(0);
            int width = rows.Count > 0 ? rows.Max(r => r.Length) : 0;
            d.rows = rows.Select(r => r.PadRight(width)).ToArray();
            return d;
        }
    }
}

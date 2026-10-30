using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Меню Tools > Ghost > Створити тестову сцену.
/// Будує плоску кімнату з привидом, стінами, полицями, приманками, зоною світла й виходом
/// і зберігає її як Assets/Scenes/TestScene.unity. Можна запускати повторно: сцена перезбереться.
/// </summary>
public static class TestSceneBuilder
{
    const string ScenePath = "Assets/Scenes/TestScene.unity";
    const string TemplateScene = "Assets/Scenes/SampleScene.unity";
    const string SquarePath = "Assets/Sprites/Debug/Square.png";
    const string GhostPath = "Assets/Sprites/New Piskel.png";
    const string GuardPath = "Assets/Sprites/guard_idle.png";

    [MenuItem("Tools/Ghost/Створити тестову сцену")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        if (File.Exists(ScenePath) &&
            !EditorUtility.DisplayDialog("Тестова сцена", "TestScene вже існує. Перезібрати її з нуля?", "Так", "Ні"))
            return;

        // Порожня сцена, щоб безпечно перезаписати TestScene, навіть якщо вона була відкрита.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        if (File.Exists(ScenePath)) AssetDatabase.DeleteAsset(ScenePath);

        // Беремо SampleScene за основу: у шаблоні Universal 2D там уже є камера і Global Light 2D.
        Scene scene;
        if (File.Exists(TemplateScene) && AssetDatabase.CopyAsset(TemplateScene, ScenePath))
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        else
            scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        Sprite square = EnsureSquareSprite();
        // Спершу новий привид з анімаціями, якщо його немає, то старий з Piskel.
        Sprite ghostSprite = LoadPixelSprite(GhostAnimationSetup.FirstFramePath) ?? LoadPixelSprite(GhostPath);
        Sprite guardSprite = LoadPixelSprite(GuardPath);

        // ---------- Кімната ----------
        Box("Floor", square, new Vector2(0, 0), new Vector2(20, 12), new Color(0.13f, 0.14f, 0.22f), -10, false);
        Color wall = new Color(0.07f, 0.07f, 0.12f);
        Box("Wall_Top", square, new Vector2(0, 6.25f), new Vector2(20.5f, 0.5f), wall, -1, true);
        Box("Wall_Bottom", square, new Vector2(0, -6.25f), new Vector2(20.5f, 0.5f), wall, -1, true);
        Box("Wall_Left", square, new Vector2(-10.25f, 0), new Vector2(0.5f, 13f), wall, -1, true);
        Box("Wall_Right", square, new Vector2(10.25f, 0), new Vector2(0.5f, 13f), wall, -1, true);

        Color shelf = new Color(0.35f, 0.25f, 0.2f);
        Box("Shelf_A", square, new Vector2(-1f, 2.5f), new Vector2(4f, 0.6f), shelf, 0, true);
        Box("Shelf_B", square, new Vector2(6f, 0f), new Vector2(0.6f, 4f), shelf, 0, true);

        // ---------- Приманки ----------
        var vase = Box("Vase", square, new Vector2(-4f, 2f), new Vector2(0.6f, 0.8f), new Color(0.85f, 0.45f, 0.3f), 1, true);
        SetProps(vase.AddComponent<LureObject>(),
            ("prompt", "Скинути вазу"), ("energyCost", 10f), ("cooldown", 2f),
            ("loudness", 6f), ("duration", 0f), ("suspicion", 0.15f));

        var tv = Box("TV", square, new Vector2(4f, 2.5f), new Vector2(1.2f, 0.9f), new Color(0.3f, 0.6f, 0.9f), 1, true);
        SetProps(tv.AddComponent<LureObject>(),
            ("prompt", "Увімкнути телевізор"), ("energyCost", 20f), ("cooldown", 6f),
            ("loudness", 10f), ("duration", 5f), ("suspicion", 0.25f));

        var phone = Box("Phone", square, new Vector2(-6f, -3f), new Vector2(0.5f, 0.5f), new Color(0.9f, 0.3f, 0.6f), 1, true);
        SetProps(phone.AddComponent<LureObject>(),
            ("prompt", "Подзвонити на телефон"), ("energyCost", 25f), ("cooldown", 4f),
            ("loudness", 15f), ("duration", 3f), ("suspicion", 0.2f));

        var mannequinTarget = new GameObject("Mannequin_Target");
        mannequinTarget.transform.position = new Vector2(7.5f, -4f);
        var mannequin = Box("Mannequin", square, new Vector2(4f, -3f), new Vector2(0.7f, 1.4f), new Color(0.85f, 0.85f, 0.8f), 1, true);
        SetProps(mannequin.AddComponent<MannequinLure>(),
            ("prompt", "Переставити манекен"), ("energyCost", 15f), ("singleUse", true),
            ("loudness", 5f), ("duration", 0f), ("suspicion", 0.3f),
            ("targetPoint", mannequinTarget.transform));

        // ---------- Зона світла (тут привида видно) ----------
        var lamp = Box("LampLight", square, new Vector2(-1f, -4f), new Vector2(3f, 2f), new Color(1f, 0.9f, 0.4f, 0.25f), -5, false);
        lamp.AddComponent<BoxCollider2D>().isTrigger = true;
        lamp.AddComponent<LightZone>();

        // ---------- Вихід ----------
        var exit = Box("Exit", square, new Vector2(8.8f, 0f), new Vector2(1f, 2f), new Color(0.3f, 0.9f, 0.4f, 0.6f), -1, false);
        exit.AddComponent<BoxCollider2D>().isTrigger = true;
        exit.AddComponent<LevelExit>();

        // ---------- Сторож (поки просто картинка, без AI) ----------
        if (guardSprite != null)
            Box("Guard_Dummy", guardSprite, new Vector2(1.5f, 4.5f), Vector2.one, Color.white, 5, true);

        // ---------- Привид ----------
        var player = new GameObject("Player");
        player.tag = "Player";
        var sr = player.AddComponent<SpriteRenderer>();
        sr.sprite = ghostSprite != null ? ghostSprite : square;
        sr.color = ghostSprite != null ? Color.white : new Color(0.7f, 0.95f, 1f);
        sr.sortingOrder = 10;
        if (ghostSprite == null) player.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var rb = player.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        player.AddComponent<CircleCollider2D>().radius = 0.35f;

        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerEnergy>();
        player.AddComponent<PlayerInteractor>();
        // У тестовій сцені привид трохи видиміший, щоб було зручно ходити.
        SetProps(player.AddComponent<GhostVisibility>(), ("hiddenAlpha", 0.35f));

        // Анімації привида (якщо кадри лежать у Sprites/Characters/Ghost)
        if (File.Exists(GhostAnimationSetup.FirstFramePath))
        {
            var controller = GhostAnimationSetup.CreateAssets();
            if (controller != null) GhostAnimationSetup.AttachTo(player, controller);
        }

        // ---------- Службове ----------
        SetProps(new GameObject("GameManager").AddComponent<GameManager>(), ("restartInsteadOfNext", true));

        var debug = new GameObject("Debug");
        debug.AddComponent<DebugHUD>();
        debug.AddComponent<LureDebugVisualizer>();

        // ---------- Камера ----------
        Camera cam = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();
        if (cam == null)
        {
            cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
        }
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.04f, 0.07f);
        var follow = cam.GetComponent<CameraFollow>();
        if (follow == null) follow = cam.gameObject.AddComponent<CameraFollow>();
        SetProps(follow, ("target", player.transform));

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Selection.activeGameObject = player;

        Debug.Log("Тестову сцену створено: " + ScenePath + ". Тисни Play.");
    }

    // ---------- Допоміжне ----------

    static GameObject Box(string name, Sprite sprite, Vector2 position, Vector2 size, Color color, int order, bool solid)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = order;
        if (solid) go.AddComponent<BoxCollider2D>();
        return go;
    }

    /// <summary>Записує значення в приватні [SerializeField] поля так само, як ти б це зробив в інспекторі.</summary>
    static void SetProps(Object target, params (string name, object value)[] props)
    {
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
                case float f: p.floatValue = f; break;
                case int i: p.intValue = i; break;
                case bool b: p.boolValue = b; break;
                case string s: p.stringValue = s; break;
                case Object o: p.objectReferenceValue = o; break;
            }
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static Sprite EnsureSquareSprite()
    {
        if (!File.Exists(SquarePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SquarePath));
            var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(SquarePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SquarePath);
        }
        return LoadPixelSprite(SquarePath);
    }

    /// <summary>Виставляє правильні налаштування піксель-арту і повертає спрайт (або null, якщо файлу немає).</summary>
    public static Sprite LoadPixelSprite(string path)
    {
        if (!File.Exists(path)) return null;

        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            bool changed = false;
            if (imp.textureType != TextureImporterType.Sprite) { imp.textureType = TextureImporterType.Sprite; changed = true; }
            if (imp.filterMode != FilterMode.Point) { imp.filterMode = FilterMode.Point; changed = true; }
            if (imp.textureCompression != TextureImporterCompression.Uncompressed) { imp.textureCompression = TextureImporterCompression.Uncompressed; changed = true; }
            if (!Mathf.Approximately(imp.spritePixelsPerUnit, 32f)) { imp.spritePixelsPerUnit = 32f; changed = true; }
            if (changed) imp.SaveAndReimport();
        }

        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Меню Tools > Ghost > Налаштувати анімації привида.
/// Бере кадри з Assets/Sprites/Characters/Ghost (ghost_idle_0.png, ghost_move_0.png, ...),
/// створює анімації Idle / Move / Caught і Animator Controller у Assets/Animations/Ghost,
/// а якщо у відкритій сцені є привид (Player), одразу вішає на нього Animator і GhostAnimator.
/// </summary>
public static class GhostAnimationSetup
{
    public const string FramesFolder = "Assets/Sprites/Characters/Ghost";
    public const string AnimFolder = "Assets/Animations/Ghost";
    public const string ControllerPath = AnimFolder + "/Ghost.controller";
    public const string FirstFramePath = FramesFolder + "/ghost_idle_0.png";

    const float FrameRate = 8f;

    [MenuItem("Tools/Ghost/Налаштувати анімації привида")]
    public static void SetupFromMenu()
    {
        var controller = CreateAssets();
        if (controller == null) return;

        var player = Object.FindAnyObjectByType<PlayerMovement>();
        if (player != null)
        {
            AttachTo(player.gameObject, controller);
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log("Анімації підключено до " + player.name + ". Збережи сцену (Ctrl+S) і тисни Play.");
        }
        else
        {
            Debug.Log("Анімації створено. Привида у відкритій сцені не знайшов, тож підключи вручну або перезбери тестову сцену.");
        }
    }

    /// <summary>Створює кліпи й контролер. Повертає null, якщо кадрів немає.</summary>
    public static AnimatorController CreateAssets()
    {
        var idle = LoadFrames("idle");
        var move = LoadFrames("move");
        var caught = LoadFrames("caught");

        if (idle.Length == 0 || move.Length == 0 || caught.Length == 0)
        {
            Debug.LogError($"Не знайшов кадри привида в {FramesFolder} (ghost_idle_0.png, ghost_move_0.png, ghost_caught_0.png ...)");
            return null;
        }

        EnsureFolder(AnimFolder);

        var idleClip = CreateClip("Ghost_Idle", idle, true);
        var moveClip = CreateClip("Ghost_Move", move, true);
        var caughtClip = CreateClip("Ghost_Caught", caught, false);

        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Caught", AnimatorControllerParameterType.Trigger);

        var sm = controller.layers[0].stateMachine;
        var idleState = sm.AddState("Idle");
        idleState.motion = idleClip;
        var moveState = sm.AddState("Move");
        moveState.motion = moveClip;
        var caughtState = sm.AddState("Caught");
        caughtState.motion = caughtClip;
        sm.defaultState = idleState;

        var toMove = idleState.AddTransition(moveState);
        toMove.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
        Instant(toMove);

        var toIdle = moveState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
        Instant(toIdle);

        var toCaught = sm.AddAnyStateTransition(caughtState);
        toCaught.AddCondition(AnimatorConditionMode.If, 0f, "Caught");
        toCaught.canTransitionToSelf = false;
        Instant(toCaught);

        AssetDatabase.SaveAssets();
        return controller;
    }

    public static void AttachTo(GameObject go, RuntimeAnimatorController controller)
    {
        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        if (go.GetComponent<GhostAnimator>() == null) go.AddComponent<GhostAnimator>();

        var sr = go.GetComponent<SpriteRenderer>();
        var first = AssetDatabase.LoadAssetAtPath<Sprite>(FirstFramePath);
        if (sr != null && first != null)
        {
            sr.sprite = first;
            sr.color = Color.white;
            go.transform.localScale = Vector3.one;
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static void Instant(AnimatorStateTransition t)
    {
        t.hasExitTime = false;
        t.duration = 0f;
    }

    static Sprite[] LoadFrames(string anim)
    {
        if (!AssetDatabase.IsValidFolder(FramesFolder)) return new Sprite[0];

        // FindAssets шукає і в підпапках (curly/, swamp/...), а там кадри скінів - їх не беремо
        return AssetDatabase.FindAssets($"ghost_{anim}_ t:Texture2D", new[] { FramesFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => Path.GetDirectoryName(p).Replace('\\', '/') == FramesFolder)
            .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith($"ghost_{anim}_"))
            .OrderBy(p => int.TryParse(Path.GetFileNameWithoutExtension(p).Split('_').Last(), out int n) ? n : 0)
            .Select(TestSceneBuilder.LoadPixelSprite)
            .Where(s => s != null)
            .ToArray();
    }

    static AnimationClip CreateClip(string name, Sprite[] frames, bool loop)
    {
        var clip = new AnimationClip { frameRate = FrameRate };
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // Останній кадр дублюється в кінці, щоб він теж показувався повну тривалість.
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = i / FrameRate, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / FrameRate, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        string path = $"{AnimFolder}/{name}.anim";
        if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }
}

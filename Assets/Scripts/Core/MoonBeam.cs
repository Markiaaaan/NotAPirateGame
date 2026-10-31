using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Місячний промінь крізь ґрати в стелі: бліда пляма світла повільно повзе між двома точками
/// туди й назад (місяць пливе небом). Привид у плямі проявляється, тож вороги можуть його побачити.
/// Світло - звичайний Light2D дочірній "Moonbeam", тож у 3D його малює RetroView, а в 2D - URP.
/// </summary>
public class MoonBeam : MonoBehaviour
{
    public static readonly List<MoonBeam> All = new List<MoonBeam>();

    [SerializeField] Vector2 from;
    [SerializeField] Vector2 to;
    [Tooltip("Скільки секунд пляма йде від однієї точки до іншої")]
    [SerializeField] float travelTime = 9f;
    [Tooltip("Зсув фази 0..1, щоб промені не рухалися однаково")]
    [SerializeField] float phase;
    [SerializeField] float radius = 1.3f;
    [Range(0f, 1f)] [SerializeField] float intensity = 1f;
    [SerializeField] SpriteRenderer disc;

    GhostVisibility ghost;
    float time;

    public float Radius => radius;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    void Start()
    {
        ghost = FindAnyObjectByType<GhostVisibility>();
        time = phase * 2f * Mathf.Max(0.1f, travelTime);
        Move(0f);
    }

    void Update()
    {
        Move(Time.deltaTime);
        if (ghost == null) return;
        float d = Vector2.Distance(ghost.transform.position, transform.position);
        // у середині плями видно повністю, на краю - менше
        if (d < radius) ghost.Expose(intensity * Mathf.Clamp01(1.25f - d / radius));
    }

    void Move(float dt)
    {
        time += dt;
        float t = Mathf.PingPong(time / Mathf.Max(0.1f, travelTime), 1f);
        t = t * t * (3f - 2f * t);   // плавно сповільнюється на краях
        Vector2 p = Vector2.Lerp(from, to, t);
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        if (disc != null)
        {
            var c = disc.color;
            c.a = 0.2f + 0.04f * Mathf.Sin(Time.time * 1.7f + phase * 6f);
            disc.color = c;
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Для тестування: коли спрацьовує приманка, малює коло, що розходиться до радіуса,
/// в якому вороги її почують, і трохи "підстрибує" сам предмет.
/// </summary>
public class LureDebugVisualizer : MonoBehaviour
{
    [SerializeField] float expandTime = 0.6f;
    [SerializeField] int segments = 48;
    [SerializeField] float lineWidth = 0.08f;
    [SerializeField] Color color = new Color(1f, 0.45f, 0.45f, 1f);

    Material material;

    void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader != null) material = new Material(shader);
    }

    void OnEnable() => LureEvents.OnLure += Show;
    void OnDisable() => LureEvents.OnLure -= Show;

    void Show(LureSignal s)
    {
        StartCoroutine(Ring(s.Position, s.Loudness));
        if (s.Source != null) StartCoroutine(Punch(s.Source.transform));
    }

    IEnumerator Ring(Vector2 center, float radius)
    {
        var go = new GameObject("LureRing");
        var lr = go.AddComponent<LineRenderer>();
        if (material != null) lr.material = material;
        lr.loop = true;
        lr.useWorldSpace = true;
        lr.positionCount = segments;
        lr.widthMultiplier = lineWidth;
        lr.sortingOrder = 100;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, expandTime);
            float r = Mathf.Lerp(0.2f, radius, Mathf.SmoothStep(0f, 1f, t));

            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                lr.SetPosition(i, new Vector3(center.x + Mathf.Cos(a) * r, center.y + Mathf.Sin(a) * r, 0f));
            }

            Color c = color;
            c.a = 1f - t * 0.8f;
            lr.startColor = c;
            lr.endColor = c;
            yield return null;
        }

        Destroy(go);
    }

    IEnumerator Punch(Transform target)
    {
        Vector3 baseScale = target.localScale;
        float t = 0f;
        while (t < 1f)
        {
            if (target == null) yield break;
            t += Time.deltaTime / 0.25f;
            target.localScale = baseScale * (1f + Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * 0.2f);
            yield return null;
        }
        if (target != null) target.localScale = baseScale;
    }
}

using UnityEngine;

/// <summary>
/// Камера плавно летить за ціллю (привидом). Вішається на Main Camera.
/// Біля країв мапи камера впирається в межу (з невеликим запасом),
/// щоб пів екрана не займала порожнеча за стінами.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;
    [SerializeField] Vector2 offset;
    [Tooltip("На скільки клітинок камера може заглянути за край мапи")]
    [SerializeField] float edgeMargin = 2f;

    Vector3 velocity;
    Camera cam;

    public void SetTarget(Transform newTarget) => target = newTarget;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target != null) transform.position = Goal();   // одразу на місці, без "підльоту" на старті
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, Goal(), ref velocity, smoothTime);
    }

    Vector3 Goal()
    {
        Vector2 p = (Vector2)target.position + offset;
        var grid = LevelGrid.Instance;
        if (cam != null && cam.orthographic && grid != null && grid.Width > 0)
        {
            float halfH = cam.orthographicSize, halfW = halfH * cam.aspect;
            // клітинка (x, y) займає світ [x-0.5, x+0.5] x [-y-0.5, -y+0.5]
            p.x = Clamp(p.x, -0.5f - edgeMargin + halfW, grid.Width - 0.5f + edgeMargin - halfW);
            p.y = Clamp(p.y, -grid.Height + 0.5f - edgeMargin + halfH, 0.5f + edgeMargin - halfH);
        }
        return new Vector3(p.x, p.y, transform.position.z);
    }

    /// <summary>Як Mathf.Clamp, але якщо мапа менша за екран - тримає її по центру.</summary>
    static float Clamp(float v, float min, float max) => min > max ? (min + max) * 0.5f : Mathf.Clamp(v, min, max);
}

using UnityEngine;

/// <summary>
/// Камера плавно летить за ціллю (привидом). Вішається на Main Camera.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float smoothTime = 0.15f;
    [SerializeField] Vector2 offset;

    Vector3 velocity;

    public void SetTarget(Transform newTarget) => target = newTarget;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 goal = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z);

        transform.position = Vector3.SmoothDamp(transform.position, goal, ref velocity, smoothTime);
    }
}

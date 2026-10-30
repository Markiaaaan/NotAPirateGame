using UnityEngine;

/// <summary>
/// Що нижче на екрані, те малюється поверх. Потрібно для вигляду зверху,
/// щоб сторож правильно заходив за полиці й виходив з-за них.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class YSort : MonoBehaviour
{
    [SerializeField] int offset;

    SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void LateUpdate()
    {
        sr.sortingOrder = offset + Mathf.RoundToInt(-transform.position.y * 10f);
    }
}

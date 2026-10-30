using UnityEngine;

/// <summary>
/// Зона світла (лампа, вітрина, екран). Поки привид усередині, він стає видимим.
/// Потрібен Collider2D з галочкою Is Trigger, що задає форму освітленої зони.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LightZone : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] float intensity = 1f;

    Collider2D zone;
    GhostVisibility ghost;

    void Awake()
    {
        zone = GetComponent<Collider2D>();
    }

    void Start()
    {
        ghost = FindAnyObjectByType<GhostVisibility>();
    }

    void Update()
    {
        if (ghost != null && zone.OverlapPoint(ghost.transform.position))
            ghost.Expose(intensity);
    }
}

using UnityEngine;

/// <summary>
/// Ключ від виходу (символ * на карті). Привид підбирає його, просто пролетівши крізь нього.
/// Тихо: вороги нічого не чують. Ключ трохи "дихає" й світиться, щоб його було видно в темряві.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class KeyPickup : MonoBehaviour
{
    [SerializeField] AudioClip pickupClip;
    [SerializeField] Transform visual;
    [SerializeField] float pulse = 0.12f;

    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        if (visual == null) visual = transform;
    }

    void Update()
    {
        float s = 1f + pulse * Mathf.Sin(Time.time * 4f);
        visual.localScale = new Vector3(s, s, 1f);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        GameAudio.Play(pickupClip);
        if (LevelExit.Instance != null) LevelExit.Instance.Unlock("Ключ знайдено! Вихід відчинено");
        gameObject.SetActive(false);
    }
}

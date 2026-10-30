using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Універсальна приманка: скинути річ, увімкнути ТВ чи радіо, телефон тощо.
/// Усе налаштовується в інспекторі, тож з одного скрипта виходить кілька різних предметів:
///   Скинута ваза: loudness 8,  duration 0, suspicion 0.15
///   Телевізор:    loudness 12, duration 6, suspicion 0.25
///   Телефон:      loudness 18, duration 4, suspicion 0.20
/// </summary>
public class LureObject : Interactable
{
    [Header("Приманка")]
    [Tooltip("Радіус, у якому охоронець чує. Видно як коло в Scene при виділенні")]
    [SerializeField] float loudness = 8f;
    [Tooltip("0 = разовий шум. Більше нуля = триває стільки секунд (ТВ, дзвінок)")]
    [SerializeField] float duration;
    [Range(0f, 1f)]
    [SerializeField] float suspicion = 0.15f;

    [Header("Ефекти")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clip;
    [Tooltip("Анімація, світло, частинки: що завгодно, підключається через інспектор")]
    [SerializeField] UnityEvent onActivated;
    [SerializeField] UnityEvent onDeactivated;

    public bool IsActive { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    protected override void OnInteract(PlayerInteractor who)
    {
        Activate();
    }

    protected void Activate()
    {
        LureEvents.Raise(new LureSignal(transform.position, loudness, duration, suspicion, gameObject));

        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip);
        onActivated.Invoke();

        if (duration > 0f) StartCoroutine(ActiveRoutine());
    }

    IEnumerator ActiveRoutine()
    {
        IsActive = true;
        yield return new WaitForSeconds(duration);
        IsActive = false;
        onDeactivated.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, loudness);
    }
}

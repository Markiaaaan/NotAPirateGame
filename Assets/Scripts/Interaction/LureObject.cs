using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Універсальна приманка: скинути річ, увімкнути ТВ чи радіо, телефон тощо.
/// Усе налаштовується в інспекторі, тож з одного скрипта виходить кілька різних предметів:
///   Скинута ваза: loudness 7,  duration 0, suspicion 0.15
///   Телевізор:    loudness 10, duration 5, suspicion 0.2
///   Телефон:      loudness 15, duration 3, suspicion 0.2
/// </summary>
public class LureObject : Interactable
{
    [Header("Приманка")]
    [Tooltip("Радіус, у якому вороги чують. Видно як коло в Scene при виділенні")]
    [SerializeField] float loudness = 8f;
    [Tooltip("0 = разовий шум. Більше нуля = триває стільки секунд (ТВ, дзвінок)")]
    [SerializeField] float duration;
    [Range(0f, 1f)]
    [SerializeField] float suspicion = 0.15f;
    [Tooltip("Після використання крізь предмет можна пролітати (розбитий глечик)")]
    [SerializeField] bool passableAfterUse;

    [Header("Ефекти")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip clip;
    [Tooltip("Анімація, світло, частинки: що завгодно, підключається через інспектор")]
    [SerializeField] UnityEvent onActivated;
    [SerializeField] UnityEvent onDeactivated;

    public bool IsActive { get; private set; }
    public float Duration => duration;

    /// <summary>Для скриптів (наприклад LureVisuals): приманка спрацювала / закінчилась.</summary>
    public event Action Activated;
    public event Action Deactivated;

    protected override void Awake()
    {
        base.Awake();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource != null) audioSource.volume = GameSettings.SfxVolume;
    }

    protected override void OnInteract(PlayerInteractor who)
    {
        Activate();
        if (passableAfterUse) MakePassable();
    }

    /// <summary>Уламки не заважають: вимикаємо твердий колайдер і звільняємо клітинку для ворогів.</summary>
    void MakePassable()
    {
        foreach (var c in GetComponents<Collider2D>()) if (!c.isTrigger) c.enabled = false;
        if (LevelGrid.Instance != null) LevelGrid.Instance.SetCell(LevelGrid.WorldToCell(transform.position), '.');
    }

    protected void Activate()
    {
        LureEvents.Raise(new LureSignal(transform.position, loudness, duration, suspicion, gameObject));

        if (audioSource != null && clip != null)
        {
            audioSource.volume = GameSettings.SfxVolume;
            audioSource.PlayOneShot(clip);
        }
        onActivated.Invoke();
        Activated?.Invoke();

        if (duration > 0f) StartCoroutine(ActiveRoutine());
    }

    IEnumerator ActiveRoutine()
    {
        IsActive = true;
        yield return new WaitForSeconds(duration);
        IsActive = false;
        onDeactivated.Invoke();
        Deactivated?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.7f);
        Gizmos.DrawWireSphere(transform.position, loudness);
    }
}

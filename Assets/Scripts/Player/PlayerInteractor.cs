using System;
using UnityEngine;

/// <summary>
/// Шукає найближчий Interactable навколо привида, підсвічує його і взаємодіє по E.
/// UI підписується на OnTargetChanged (показати підказку) і OnNotEnoughEnergy (тряска смужки, звук).
/// </summary>
[RequireComponent(typeof(PlayerEnergy))]
public class PlayerInteractor : MonoBehaviour
{
    [SerializeField] float interactRadius = 1.6f;
    [Tooltip("Шар(и), на яких лежать об'єкти для взаємодії. Everything, якщо ще не налаштували.")]
    [SerializeField] LayerMask interactableLayers = ~0;

    public PlayerEnergy Energy { get; private set; }
    public Interactable Current { get; private set; }

    /// <summary>Нова ціль (або null, коли поблизу нічого немає).</summary>
    public event Action<Interactable> OnTargetChanged;
    public event Action<Interactable> OnNotEnoughEnergy;
    public event Action<Interactable> OnInteracted;

    void Awake()
    {
        Energy = GetComponent<PlayerEnergy>();
    }

    void Update()
    {
        UpdateTarget();

        if (Current != null && GameInput.InteractPressed)
        {
            if (Current.TryInteract(this)) OnInteracted?.Invoke(Current);
        }
    }

    void UpdateTarget()
    {
        Interactable best = null;
        float bestDist = float.MaxValue;

        var hits = Physics2D.OverlapCircleAll(transform.position, interactRadius, interactableLayers);
        for (int i = 0; i < hits.Length; i++)
        {
            var candidate = hits[i].GetComponentInParent<Interactable>();
            if (candidate == null || !candidate.IsAvailable) continue;

            float d = ((Vector2)hits[i].transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = candidate;
            }
        }

        if (best == Current) return;

        if (Current != null) Current.SetHighlight(false);
        Current = best;
        if (Current != null) Current.SetHighlight(true);
        OnTargetChanged?.Invoke(Current);
    }

    public void ReportNotEnoughEnergy(Interactable who) => OnNotEnoughEnergy?.Invoke(who);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}

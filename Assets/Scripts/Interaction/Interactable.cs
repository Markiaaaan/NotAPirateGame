using UnityEngine;

/// <summary>
/// Базовий клас для всього, з чим можна взаємодіяти. Потрібен Collider2D (можна Trigger).
/// Сам перевіряє енергію, перезарядку і разове використання. Нащадки пишуть лише OnInteract.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class Interactable : MonoBehaviour
{
    [Header("Взаємодія")]
    [SerializeField] string prompt = "Взаємодіяти";
    [SerializeField] float energyCost = 15f;
    [Tooltip("Секунд до повторного використання")]
    [SerializeField] float cooldown = 3f;
    [SerializeField] bool singleUse;

    [Header("Підсвітка (необов'язково)")]
    [SerializeField] SpriteRenderer highlightSprite;
    [SerializeField] Color highlightColor = new Color(1f, 0.95f, 0.5f, 1f);

    float readyTime;
    bool used;
    Color baseColor = Color.white;

    public virtual string Prompt => prompt;
    public float EnergyCost => energyCost;
    public virtual bool IsAvailable => !(singleUse && used) && Time.time >= readyTime;

    protected virtual void Awake()
    {
        if (highlightSprite == null) highlightSprite = GetComponentInChildren<SpriteRenderer>();
        if (highlightSprite != null) baseColor = highlightSprite.color;
    }

    /// <summary>Спроба взаємодії: перевіряє доступність і енергію, тоді викликає OnInteract.</summary>
    public bool TryInteract(PlayerInteractor who)
    {
        if (!IsAvailable) return false;

        // енергію прибрали з гри: приманки нічого не коштують (energyCost = 0)
        if (energyCost > 0f && who.Energy != null && !who.Energy.TrySpend(energyCost))
        {
            who.ReportNotEnoughEnergy(this);
            return false;
        }

        used = true;
        readyTime = Time.time + cooldown;
        OnInteract(who);
        return true;
    }

    protected abstract void OnInteract(PlayerInteractor who);

    public virtual void SetHighlight(bool on)
    {
        if (highlightSprite == null) return;
        highlightSprite.color = on ? highlightColor : baseColor;
    }
}

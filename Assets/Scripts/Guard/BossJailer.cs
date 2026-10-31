using UnityEngine;

/// <summary>
/// Ключник: великий скелет, якого можна здолати лише кам'яними вантажами.
/// Потрібно кілька влучань (hitsNeeded): після кожного він на мить падає оглушений,
/// а потім лютішає й ходить швидше. Після останнього розсипається й лишає ключ.
/// </summary>
[RequireComponent(typeof(GuardAI))]
public class BossJailer : MonoBehaviour
{
    public static BossJailer Instance { get; private set; }
    [SerializeField] GameObject droppedKey;
    [SerializeField] int hitsNeeded = 3;
    [Tooltip("На скільки швидшає після кожного влучання")]
    [SerializeField] float ragePerHit = 0.18f;
    [SerializeField] float stunTime = 2.2f;

    GuardAI guard;
    SpriteRenderer body;
    Vector3 baseScale;
    float hurtTimer;

    public bool IsDefeated { get; private set; }
    public GameObject KeyObject => droppedKey;
    public int Hits { get; private set; }
    public int HitsNeeded => Mathf.Max(1, hitsNeeded);

    void OnEnable() => LureEvents.OnLure += HearBait;
    void OnDisable() => LureEvents.OnLure -= HearBait;

    void HearBait(LureSignal signal)
    {
        if (guard == null) guard = GetComponent<GuardAI>();
        if (!IsDefeated && !guard.IsStunned && signal.Source != null && signal.Source.GetComponent<LureObject>() != null)
            guard.InvestigateNoise(signal, true, 5f);
    }

    void Awake()
    {
        Instance = this;
        guard = GetComponent<GuardAI>();
        body = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        if (droppedKey != null) droppedKey.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Вантаж упав на Ключника.</summary>
    public void Hit()
    {
        if (IsDefeated) return;
        Hits++;
        if (Hits >= HitsNeeded)
        {
            Defeat();
            return;
        }
        guard.Stun(stunTime);
        guard.SpeedScale = 1f + ragePerHit * Hits;
        hurtTimer = stunTime;
        int left = HitsNeeded - Hits;
        LevelExit.Announce(left == 1
            ? "Влучив! Ключник ледве тримається. Ще один вантаж!"
            : $"Влучив! Ключник похитнувся й розлютився. Лишилось вантажів: {left}.");
    }

    void LateUpdate()
    {
        if (hurtTimer <= 0f) return;
        hurtTimer -= Time.deltaTime;
        // оглушений: блимає червоним і трохи присідає
        bool on = hurtTimer > 0f;
        if (body != null)
            body.color = on && Mathf.Repeat(hurtTimer, 0.24f) > 0.12f ? new Color(1f, 0.45f, 0.4f, body.color.a) : new Color(1f, 1f, 1f, body.color.a);
        transform.localScale = on ? new Vector3(baseScale.x * 1.06f, baseScale.y * 0.9f, baseScale.z) : baseScale;
    }

    public void Defeat()
    {
        if (IsDefeated) return;
        IsDefeated = true;
        Hits = HitsNeeded;
        guard.Retire();
        if (droppedKey != null)
        {
            droppedKey.transform.position = transform.position;
            droppedKey.SetActive(true);
        }
        LevelExit.Announce("Ключник розсипався! Забери його ключ.");
        gameObject.SetActive(false);
    }
}

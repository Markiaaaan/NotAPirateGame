using System;
using UnityEngine;

/// <summary>
/// Вихід із рівня: двері в стіні.
/// Якщо на рівні є важіль (X), двері спочатку замкнені: відчиняються, коли потягнуто ВСІ важелі рівня
/// На фінальному рівні потрібен ключ Ключника.
/// Незамкнені двері самі розчиняються, коли привид підлітає ближче за openDistance,
/// і зачиняються, коли він відлітає. Залетів у відчинені двері - рівень пройдено.
/// Спрайт дверей (body) бере й 3D-вигляд, тож анімацію видно і там.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    public static LevelExit Instance { get; private set; }

    /// <summary>Повідомлення для інтерфейсу ("Замкнено!", "Ключ знайдено!").</summary>
    public static event Action<string> Message;

    [SerializeField] bool startsLocked;
    [SerializeField] bool requiresKey;
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite lockedSprite;
    [Tooltip("Кадри від зачинених до повністю відчинених")]
    [SerializeField] Sprite[] openFrames = new Sprite[0];
    [SerializeField] float openDistance = 2.2f;
    [SerializeField] float openSpeed = 2.5f;
    [Tooltip("Твердий колайдер, що не пускає в зачинені двері")]
    [SerializeField] Collider2D blocker;

    [Header("Звук")]
    [SerializeField] AudioClip openClip;
    [SerializeField] AudioClip lockedClip;

    Transform player;
    bool locked;
    int leversNeeded, leversPulled;
    float open;
    float lockedMessageTimer;

    public bool HasLock => startsLocked;
    public bool IsLocked => locked;
    public bool RequiresKey => requiresKey;
    public int LeversNeeded => leversNeeded;
    public int LeversPulled => leversPulled;
    public Sprite CurrentSprite => body != null ? body.sprite : null;
    public static void Announce(string message) => Message?.Invoke(message);

    // Викликається, коли додаєш компонент: одразу робить колайдер тригером.
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void Awake()
    {
        Instance = this;
        locked = startsLocked;
        if (body == null) body = GetComponent<SpriteRenderer>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null) player = pm.transform;
        leversNeeded = FindObjectsByType<LeverLure>(FindObjectsSortMode.None).Length;
        UpdateVisual();
    }

    /// <summary>Потягнули важіль: якщо це останній - двері відмикаються.</summary>
    public void LeverPulled()
    {
        leversPulled++;
        if (leversPulled >= leversNeeded)
            Unlock(leversNeeded > 1
                ? "Останній важіль! Кам'яні ворота відчинено... тікай!"
                : "Важіль спрацював! Вихід відчинено... але нежить усе чула");
        else
            Message?.Invoke($"Важіль {leversPulled} з {leversNeeded}: щось загуркотіло в стінах. Нежить біжить сюди!");
    }

    /// <summary>Відімкнути двері (ключем чи важелем).</summary>
    public void Unlock(string message)
    {
        if (!locked) return;
        locked = false;
        Message?.Invoke(message);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (lockedMessageTimer > 0f) lockedMessageTimer -= dt;

        float dist = player != null ? Vector2.Distance(player.position, transform.position) : 999f;
        bool wantOpen = !locked && dist < openDistance;
        float before = open;
        open = Mathf.MoveTowards(open, wantOpen ? 1f : 0f, openSpeed * dt);
        if (before <= 0f && open > 0f) GameAudio.Play(openClip, 0.8f);

        if (blocker != null) blocker.enabled = open < 0.6f;
        UpdateVisual();

        // підлетів до відчинених дверей - рівень пройдено (далі екран переходу, а не політ крізь стіну)
        if (!locked && openFrames.Length > 0 && open >= 0.9f && dist < 1.05f && GameManager.Instance != null)
            GameManager.Instance.CompleteLevel();
    }

    void UpdateVisual()
    {
        if (body == null) return;
        if (locked && lockedSprite != null) body.sprite = lockedSprite;
        else if (openFrames.Length > 0)
            body.sprite = openFrames[Mathf.Clamp(Mathf.RoundToInt(open * (openFrames.Length - 1)), 0, openFrames.Length - 1)];
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (locked)
        {
            if (lockedMessageTimer > 0f) return;
            lockedMessageTimer = 2f;
            GameAudio.Play(lockedClip);
            Message?.Invoke(requiresKey ? "Замкнено! Ключ у Ключника. Заманюй його під кам'яний вантаж."
                : leversNeeded > 1
                ? $"Замкнено! Потягни всі важелі: {leversPulled} з {leversNeeded}"
                : "Замкнено! Знайди важіль, щоб відчинити вихід");
        }
        else if (openFrames.Length == 0 && GameManager.Instance != null)
        {
            // старий префаб без анімації дверей: як раніше, одразу вихід
            GameManager.Instance.CompleteLevel();
        }
    }
}

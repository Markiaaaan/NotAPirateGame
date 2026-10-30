using UnityEngine;

/// <summary>
/// Як виглядає приманка, коли спрацювала: міняє спрайт (ваза розбилась, ТВ показує шум),
/// вмикає світіння (Light2D у дочірньому об'єкті) і трохи трясе предмет, поки той активний.
/// </summary>
[RequireComponent(typeof(LureObject))]
public class LureVisuals : MonoBehaviour
{
    [SerializeField] SpriteRenderer target;
    [Tooltip("1 кадр = просто замінити спрайт, кілька = анімація, поки приманка активна")]
    [SerializeField] Sprite[] activeFrames = new Sprite[0];
    [SerializeField] float fps = 10f;
    [Tooltip("Залишити змінений спрайт назавжди (розбита ваза)")]
    [SerializeField] bool keepAfterDeactivate;
    [Tooltip("Об'єкт, що вмикається лише поки приманка активна (світло екрана тощо)")]
    [SerializeField] GameObject activeOnly;
    [SerializeField] float shake = 0f;

    LureObject lure;
    Sprite idleSprite;
    bool animating;
    float time;
    Vector3 basePos;

    void Awake()
    {
        lure = GetComponent<LureObject>();
        if (target == null) target = GetComponentInChildren<SpriteRenderer>();
        if (target != null) idleSprite = target.sprite;
        if (activeOnly != null) activeOnly.SetActive(false);
    }

    void OnEnable()
    {
        lure.Activated += HandleActivated;
        lure.Deactivated += HandleDeactivated;
    }

    void OnDisable()
    {
        lure.Activated -= HandleActivated;
        lure.Deactivated -= HandleDeactivated;
    }

    void HandleActivated()
    {
        if (target != null && activeFrames.Length > 0) target.sprite = activeFrames[0];

        // Разова приманка (ваза): лише замінити спрайт.
        if (lure.Duration <= 0f) return;

        animating = true;
        time = 0f;
        if (target != null) basePos = target.transform.localPosition;
        if (activeOnly != null) activeOnly.SetActive(true);
    }

    void HandleDeactivated()
    {
        animating = false;
        if (activeOnly != null) activeOnly.SetActive(false);
        if (target == null) return;
        target.transform.localPosition = basePos;
        if (!keepAfterDeactivate) target.sprite = idleSprite;
    }

    void Update()
    {
        if (!animating || target == null) return;
        time += Time.deltaTime;
        if (activeFrames.Length > 1)
            target.sprite = activeFrames[(int)(time * fps) % activeFrames.Length];
        if (shake > 0f)
            target.transform.localPosition = basePos + (Vector3)(Random.insideUnitCircle * shake);
    }
}

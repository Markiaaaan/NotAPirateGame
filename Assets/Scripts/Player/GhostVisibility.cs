using UnityEngine;

/// <summary>
/// Прозорість привида. У темряві він ледь видимий, у світлі - проявляється.
/// Усе, що "світить" на привида (конус ліхтаря охоронця, лампа, екран ТВ), кличе Expose() щокадру,
/// поки привид у промені. Охоронець перевіряє IsVisible, щоб вирішити, чи бачить він привида.
/// </summary>
public class GhostVisibility : MonoBehaviour
{
    [Header("Прозорість спрайтів")]
    [Range(0f, 1f)] [SerializeField] float hiddenAlpha = 0.08f;
    [Range(0f, 1f)] [SerializeField] float exposedAlpha = 1f;
    [SerializeField] float fadeSpeed = 6f;

    [Header("Для охоронця")]
    [Tooltip("Видимість вище цього порога = охоронець бачить привида")]
    [Range(0f, 1f)] [SerializeField] float detectThreshold = 0.5f;

    [Header("Налагодження")]
    [Tooltip("Примусово показати привида (для тесту без охоронця)")]
    [SerializeField] bool forceExposed;

    SpriteRenderer[] sprites;
    float exposureThisFrame;

    /// <summary>0 = невидимий, 1 = повністю видимий.</summary>
    public float Visibility { get; private set; }
    public bool IsVisible => Visibility >= detectThreshold;

    void Awake()
    {
        sprites = GetComponentsInChildren<SpriteRenderer>();
        Visibility = 0f;
        Apply();
    }

    /// <summary>Викликати щокадру з джерела світла, поки привид освітлений. amount: 0..1.</summary>
    public void Expose(float amount = 1f)
    {
        exposureThisFrame = Mathf.Max(exposureThisFrame, Mathf.Clamp01(amount));
    }

    // LateUpdate, щоб не залежати від порядку виконання Update у різних скриптах.
    void LateUpdate()
    {
        float target = forceExposed ? 1f : exposureThisFrame;
        exposureThisFrame = 0f;

        Visibility = Mathf.MoveTowards(Visibility, target, fadeSpeed * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        float a = Mathf.Lerp(hiddenAlpha, exposedAlpha, Visibility);
        for (int i = 0; i < sprites.Length; i++)
        {
            var c = sprites[i].color;
            c.a = a;
            sprites[i].color = c;
        }
    }
}

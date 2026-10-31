using UnityEngine;

/// <summary>
/// Керування привидом від першої особи: W/S - вперед/назад, A/D - вбік,
/// миша або стрілки ←/→ - поворот. Фізика та сама, що й у 2D (Rigidbody2D на сітці рівня).
/// Вмикається автоматично, коли в меню вибрано 3D (це робить RetroView на камері).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class FirstPersonMovement : MonoBehaviour
{
    [SerializeField] float moveSpeed = 3.6f;
    [SerializeField] float acceleration = 20f;
    [Tooltip("Градусів за секунду для стрілок")]
    [SerializeField] float turnSpeed = 140f;
    [Tooltip("Градусів на піксель руху миші")]
    [SerializeField] float mouseSensitivity = 0.12f;
    [SerializeField] bool lockCursor = true;

    Rigidbody2D rb;
    Vector2 wish;
    Vector2 velocity;
    float angle;
    bool angleChosen;
    bool frozen;

    /// <summary>Зупинити привида (рівень пройдено): камера більше не рухається.</summary>
    public void Freeze()
    {
        frozen = true;
        wish = Vector2.zero;
        velocity = Vector2.zero;
    }

    /// <summary>Куди дивиться привид (кут у градусах, 0 = праворуч на карті, 90 = вгору).</summary>
    public float Angle => angle;
    public Vector2 Forward => new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
    public bool IsMoving => velocity.sqrMagnitude > 0.01f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void OnEnable()
    {
        if (!lockCursor) return;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Start()
    {
        if (!angleChosen)
        {
            angle = PickStartAngle();
            angleChosen = true;
        }
    }

    /// <summary>На старті дивимось туди, де найдовший вільний коридор.</summary>
    float PickStartAngle()
    {
        var grid = LevelGrid.Instance;
        if (grid == null) return 0f;

        Vector2Int start = LevelGrid.WorldToCell(transform.position);
        // крок по сітці (рядки йдуть вниз) і відповідний кут у світі
        Vector2Int[] steps = { new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(-1, 0), new Vector2Int(0, -1) };
        float[] angles = { 0f, 270f, 180f, 90f };

        int best = 0, bestLen = -1;
        for (int i = 0; i < steps.Length; i++)
        {
            int len = 0;
            var c = start + steps[i];
            while (len < 30 && grid.IsWalkable(c)) { len++; c += steps[i]; }
            if (len > bestLen) { bestLen = len; best = i; }
        }
        return angles[best];
    }

    void Update()
    {
        if (frozen || PauseMenu.IsPaused) return;
        angle -= GameInput.MouseDeltaX * mouseSensitivity * GameSettings.MouseSensitivity;
        angle -= GameInput.TurnAxis * turnSpeed * Time.deltaTime;
        angle = Mathf.Repeat(angle, 360f);

        Vector2 m = GameInput.MoveWASD;
        Vector2 f = Forward;
        Vector2 right = new Vector2(f.y, -f.x);
        wish = Vector2.ClampMagnitude(f * m.y + right * m.x, 1f) * moveSpeed;
    }

    void FixedUpdate()
    {
        if (frozen) return;
        velocity = Vector2.MoveTowards(velocity, wish, acceleration * Time.fixedDeltaTime);
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }
}

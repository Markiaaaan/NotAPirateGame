using UnityEngine;

/// <summary>
/// Рух привида зверху (top-down 2D) з невеликим розгоном і гальмуванням, щоб він "пливав".
/// Потрібен Rigidbody2D: Gravity Scale = 0, Freeze Rotation Z = true.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Рух")]
    [SerializeField] float moveSpeed = 4f;
    [SerializeField] float acceleration = 25f;
    [SerializeField] float deceleration = 18f;

    [Header("Візуал (необов'язково)")]
    [Tooltip("Спрайт, який відзеркалюється вліво/вправо")]
    [SerializeField] SpriteRenderer sprite;

    Rigidbody2D rb;
    Vector2 input;
    Vector2 velocity;

    /// <summary>Напрямок останнього руху (корисно для анімацій і спрямованих ефектів).</summary>
    public Vector2 Facing { get; private set; } = Vector2.down;
    public bool IsMoving => velocity.sqrMagnitude > 0.01f;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
    }

    void Update()
    {
        input = GameInput.Move;

        if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
        if (sprite != null && Mathf.Abs(input.x) > 0.01f) sprite.flipX = input.x < 0f;
    }

    void FixedUpdate()
    {
        Vector2 target = input * moveSpeed;
        float rate = input.sqrMagnitude > 0.01f ? acceleration : deceleration;
        velocity = Vector2.MoveTowards(velocity, target, rate * Time.fixedDeltaTime);

#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = velocity;
#else
        rb.velocity = velocity;
#endif
    }
}

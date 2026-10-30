using UnityEngine;

/// <summary>
/// Перемикає анімації привида: Idle, коли стоїть, Move, коли летить,
/// і Caught, коли його спіймали (GameManager.GameOver). Висить на Player поруч з Animator.
/// Параметри в Animator Controller: bool "Moving", trigger "Caught".
/// </summary>
[RequireComponent(typeof(Animator))]
public class GhostAnimator : MonoBehaviour
{
    static readonly int MovingHash = Animator.StringToHash("Moving");
    static readonly int CaughtHash = Animator.StringToHash("Caught");

    Animator animator;
    PlayerMovement movement;
    PlayerInteractor interactor;
    GhostVisibility visibility;
    Rigidbody2D rb;
    bool caught;

    void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
        interactor = GetComponent<PlayerInteractor>();
        visibility = GetComponent<GhostVisibility>();
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver += PlayCaught;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= PlayCaught;
    }

    void Update()
    {
        if (caught)
        {
            // Щоб анімацію розсипання було добре видно навіть у темряві.
            if (visibility != null) visibility.Expose(1f);
            return;
        }

        animator.SetBool(MovingHash, movement != null && movement.IsMoving);
    }

    public void PlayCaught()
    {
        if (caught) return;
        caught = true;

        animator.SetTrigger(CaughtHash);

        // Привид завмирає: не рухається й не взаємодіє.
        if (movement != null) movement.enabled = false;
        if (interactor != null) interactor.enabled = false;
        if (rb != null) rb.simulated = false;
    }
}

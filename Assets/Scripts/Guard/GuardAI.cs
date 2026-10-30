using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Сторож. Ходить маршрутом, світить ліхтариком перед собою, йде перевіряти шум від приманок
/// і женеться за привидом, якщо той став видимим у його полі зору.
///
/// Стани: Патруль → (почув шум) Перевірка → (побачив) Гонитва → (загубив) Пошук → Патруль.
/// Привид у темряві прозорий: сторож його бачить, лише коли той освітлений (ліхтариком, лампою).
/// </summary>
public class GuardAI : MonoBehaviour
{
    enum State { Patrol, Investigate, Chase, Search, Caught }

    [Header("Швидкість")]
    [SerializeField] float walkSpeed = 2.2f;
    [SerializeField] float investigateSpeed = 3f;
    [SerializeField] float chaseSpeed = 3.5f;
    [SerializeField] float turnSpeed = 300f;

    [Header("Зір")]
    [SerializeField] float viewDistance = 6.5f;
    [SerializeField] float viewAngle = 70f;
    [Tooltip("На цій відстані ліхтарик проявляє привида")]
    [SerializeField] float flashlightDistance = 5f;
    [Tooltip("Скільки секунд треба дивитися на привида, щоб упізнати його")]
    [SerializeField] float noticeTime = 0.8f;
    [SerializeField] float loseSightTime = 2f;
    [SerializeField] float catchDistance = 0.65f;

    [Header("Поведінка")]
    [SerializeField] float waitAtPoint = 1.2f;
    [SerializeField] float searchTime = 4f;
    [SerializeField] float lookAroundAngle = 70f;

    [Header("Маршрут (заповнює збирач рівнів)")]
    [SerializeField] Vector2[] patrolPoints = new Vector2[0];

    [Header("Візуал")]
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite[] walkDown = new Sprite[0];
    [SerializeField] Sprite[] walkUp = new Sprite[0];
    [Tooltip("Кадри, де сторож дивиться вправо. Вліво віддзеркалюються.")]
    [SerializeField] Sprite[] walkSide = new Sprite[0];
    [SerializeField] float animFps = 8f;
    [SerializeField] Transform flashlight;
    [SerializeField] SpriteRenderer alertIcon;
    [SerializeField] Sprite exclaimSprite;
    [SerializeField] Sprite questionSprite;

    [Header("Звук")]
    [SerializeField] AudioClip alertClip;
    [SerializeField] AudioClip huhClip;

    State state;
    LevelGrid grid;
    GhostVisibility ghost;
    Transform ghostTransform;

    readonly List<Vector2> path = new List<Vector2>();
    int pathIndex;
    bool moving;
    bool arrived;

    Vector2 facing = Vector2.down;
    Vector2 lookTarget = Vector2.down;
    Vector2 lookBase = Vector2.down;
    float lookTime;

    int patrolIndex;
    float waitTimer;
    float stateTimer;
    float repathTimer;
    float notice;
    float lostTimer;
    float questionTimer;
    float animTime;
    Vector2 investigatePos;
    Vector2 lastSeenPos;

    float Alert => SuspicionMeter.Level;
    float SpeedMul => 1f + 0.25f * Alert;

    /// <summary>Задати маршрут (точки у світових координатах).</summary>
    public void Configure(Vector2[] points)
    {
        patrolPoints = points;
    }

    void OnEnable() => LureEvents.OnLure += HandleLure;
    void OnDisable() => LureEvents.OnLure -= HandleLure;

    void Start()
    {
        grid = LevelGrid.Instance;
        ghost = FindAnyObjectByType<GhostVisibility>();
        if (ghost != null) ghostTransform = ghost.transform;
        if (body == null) body = GetComponent<SpriteRenderer>();
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver += HandleGameOver;
        BeginPatrol();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= HandleGameOver;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (state != State.Caught)
        {
            Vision(dt);
            switch (state)
            {
                case State.Patrol: UpdatePatrol(dt); break;
                case State.Investigate: UpdateInvestigate(dt); break;
                case State.Chase: UpdateChase(dt); break;
                case State.Search: UpdateSearch(dt); break;
            }
        }
        else moving = false;

        // плавний поворот погляду
        float a = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;
        float b = Mathf.Atan2(lookTarget.y, lookTarget.x) * Mathf.Rad2Deg;
        a = Mathf.MoveTowardsAngle(a, b, turnSpeed * dt) * Mathf.Deg2Rad;
        facing = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        if (flashlight != null) flashlight.up = facing;

        if (questionTimer > 0f) questionTimer -= dt;
        UpdateIcon();
        UpdateSprite(dt);
    }

    // ---------------- Зір ----------------

    void Vision(float dt)
    {
        if (ghostTransform == null || grid == null) return;

        Vector2 pos = transform.position;
        Vector2 gp = ghostTransform.position;
        Vector2 to = gp - pos;
        float dist = to.magnitude;
        float range = viewDistance * (1f + 0.3f * Alert);

        bool inCone = dist <= range
                      && (dist < 0.9f || Vector2.Angle(facing, to) <= viewAngle * 0.5f)
                      && grid.HasLineOfSight(pos, gp);

        // ліхтарик проявляє привида
        if (inCone && dist <= flashlightDistance) ghost.Expose(1f);

        bool sees = (inCone && ghost.IsVisible) || dist < 0.8f;

        if (state == State.Chase)
        {
            if (sees || (inCone && ghost.Visibility > 0.15f))
            {
                lostTimer = 0f;
                lastSeenPos = gp;
            }
            else
            {
                lostTimer += dt;
                if (lostTimer >= loseSightTime) BeginSearch(lastSeenPos);
            }
            if (dist <= catchDistance) Catch();
            return;
        }

        if (sees)
        {
            notice += dt / (noticeTime / (1f + Alert));
            lookTarget = to.normalized;
            if (notice >= 1f || dist < 0.8f) BeginChase(gp);
        }
        else
        {
            notice = Mathf.Max(0f, notice - dt * 0.5f);
        }
    }

    bool Noticing => notice > 0.05f;

    // ---------------- Стани ----------------

    void BeginPatrol()
    {
        state = State.Patrol;
        notice = 0f;
        waitTimer = 0f;
        if (patrolPoints.Length == 0) return;
        patrolIndex = NearestPatrolPoint();
        SetPathTo(patrolPoints[patrolIndex]);
    }

    void UpdatePatrol(float dt)
    {
        if (Noticing) { moving = false; return; }

        if (patrolPoints.Length == 0)
        {
            moving = false;
            LookAround(dt);
            return;
        }

        if (waitTimer > 0f)
        {
            moving = false;
            LookAround(dt);
            waitTimer -= dt;
            if (waitTimer <= 0f)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                SetPathTo(patrolPoints[patrolIndex]);
            }
            return;
        }

        if (FollowPath(walkSpeed, dt)) StartLookAround(Mathf.Max(0.1f, waitAtPoint));
    }

    void HandleLure(LureSignal s)
    {
        if (state == State.Chase || state == State.Caught) return;
        if (Vector2.Distance(transform.position, s.Position) > s.Loudness) return;

        state = State.Investigate;
        notice = 0f;
        arrived = false;
        investigatePos = s.Position;
        stateTimer = Mathf.Max(2.5f, s.Duration + 1.5f);
        questionTimer = 1.5f;
        SetPathTo(investigatePos);
        GameAudio.Play(huhClip, 0.8f);
    }

    void UpdateInvestigate(float dt)
    {
        if (Noticing) { moving = false; return; }
        if (!FollowPath(investigateSpeed, dt)) return;

        if (!arrived)
        {
            arrived = true;
            Vector2 dir = investigatePos - (Vector2)transform.position;
            StartLookAround(0f, dir.sqrMagnitude > 0.01f ? dir.normalized : facing);
        }
        LookAround(dt);
        stateTimer -= dt;
        if (stateTimer <= 0f) BeginPatrol();
    }

    void BeginChase(Vector2 ghostPos)
    {
        state = State.Chase;
        notice = 0f;
        lostTimer = 0f;
        repathTimer = 0f;
        lastSeenPos = ghostPos;
        GameAudio.Play(alertClip);
        if (SuspicionMeter.Instance != null) SuspicionMeter.Instance.Add(0.35f);
    }

    void UpdateChase(float dt)
    {
        repathTimer -= dt;
        if (repathTimer <= 0f)
        {
            repathTimer = 0.25f;
            SetPathTo(lastSeenPos);
        }

        bool done = FollowPath(chaseSpeed, dt);

        // останні сантиметри біжить прямо на привида
        if (done && ghostTransform != null && lostTimer <= 0f)
        {
            Vector2 pos = transform.position;
            Vector2 gp = ghostTransform.position;
            Vector2 next = Vector2.MoveTowards(pos, gp, chaseSpeed * SpeedMul * dt);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            moving = true;
        }

        if (ghostTransform != null && lostTimer <= 0f)
        {
            Vector2 to = (Vector2)ghostTransform.position - (Vector2)transform.position;
            if (to.sqrMagnitude > 0.01f) lookTarget = to.normalized;
        }
    }

    void BeginSearch(Vector2 where)
    {
        state = State.Search;
        stateTimer = searchTime;
        arrived = false;
        questionTimer = 2f;
        SetPathTo(where);
    }

    void UpdateSearch(float dt)
    {
        if (Noticing) { moving = false; return; }
        if (!FollowPath(investigateSpeed, dt)) return;

        if (!arrived)
        {
            arrived = true;
            StartLookAround(0f, facing);
        }
        LookAround(dt);
        stateTimer -= dt;
        if (stateTimer <= 0f) BeginPatrol();
    }

    void Catch()
    {
        state = State.Caught;
        moving = false;
        if (GameManager.Instance != null) GameManager.Instance.GameOver();
    }

    void HandleGameOver()
    {
        if (state != State.Caught) state = State.Caught;
        moving = false;
    }

    // ---------------- Рух ----------------

    void SetPathTo(Vector2 world)
    {
        pathIndex = 0;
        if (grid == null || !grid.FindPath(transform.position, world, path)) path.Clear();
    }

    /// <summary>Іде по шляху. Повертає true, коли дійшов до кінця.</summary>
    bool FollowPath(float speed, float dt)
    {
        if (pathIndex >= path.Count)
        {
            moving = false;
            return true;
        }

        Vector2 pos = transform.position;
        Vector2 target = path[pathIndex];
        Vector2 dir = target - pos;
        if (dir.sqrMagnitude > 0.0001f) lookTarget = dir.normalized;

        Vector2 next = Vector2.MoveTowards(pos, target, speed * SpeedMul * dt);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        moving = true;

        if ((next - target).sqrMagnitude < 0.0004f) pathIndex++;
        return pathIndex >= path.Count;
    }

    void StartLookAround(float wait) => StartLookAround(wait, facing);

    void StartLookAround(float wait, Vector2 baseDir)
    {
        waitTimer = wait;
        lookBase = baseDir;
        lookTime = 0f;
    }

    void LookAround(float dt)
    {
        lookTime += dt;
        float angle = Mathf.Sin(lookTime * 1.6f) * lookAroundAngle;
        lookTarget = Quaternion.Euler(0f, 0f, angle) * lookBase;
    }

    int NearestPatrolPoint()
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            float d = ((Vector2)transform.position - patrolPoints[i]).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // ---------------- Візуал ----------------

    void UpdateIcon()
    {
        if (alertIcon == null) return;
        Sprite want = null;
        if (state == State.Chase || state == State.Caught) want = exclaimSprite;
        else if (Noticing || questionTimer > 0f) want = questionSprite;

        alertIcon.sprite = want;
        alertIcon.enabled = want != null;
    }

    void UpdateSprite(float dt)
    {
        if (body == null) return;

        Sprite[] set;
        bool flip = false;
        if (Mathf.Abs(facing.x) > Mathf.Abs(facing.y))
        {
            set = walkSide;
            flip = facing.x < 0f;
        }
        else set = facing.y > 0f ? walkUp : walkDown;

        if (set == null || set.Length == 0) return;

        animTime = moving ? animTime + dt : 0f;
        int frame = moving ? (int)(animTime * animFps) % set.Length : 0;
        body.sprite = set[frame];
        body.flipX = flip;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.6f);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Gizmos.DrawWireSphere(patrolPoints[i], 0.2f);
            Gizmos.DrawLine(patrolPoints[i], patrolPoints[(i + 1) % patrolPoints.Length]);
        }
    }
}

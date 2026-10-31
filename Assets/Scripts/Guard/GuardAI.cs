using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ворог підземелля: скелет або злий дух (той самий скрипт, різні налаштування в префабах).
///   Скелет несе ліхтар: у радіусі lanternRadius привида видно з усіх боків. Обходить річку.
///   Дух ліхтаря не має, зате відчуває привида поруч (senseRadius) навіть у темряві й летить над водою.
/// Обидва ходять маршрутом, ідуть перевіряти шум від приманок і женуться за привидом, якщо побачили.
///
/// Стани: Патруль → (почув шум) Перевірка → (побачив) Гонитва → (загубив) Пошук → Патруль.
/// Привид у темряві прозорий: ворог його бачить, лише коли той освітлений (ліхтарем, смолоскипом).
/// Швидкість, дальність зору й час упізнавання множаться на складність з меню.
/// Клас лишився з назвою GuardAI, щоб не ламати старі префаби.
/// </summary>
public class GuardAI : MonoBehaviour
{
    enum State { Patrol, Investigate, Chase, Search, Caught }

    [Header("Тип")]
    [Tooltip("Дух (для 3D-вигляду й анімації). Скелет - false")]
    [SerializeField] bool isSpirit;
    [Tooltip("Може ходити над підземною річкою (~)")]
    [SerializeField] bool canCrossWater;
    [Tooltip("Ліхтар: у цьому радіусі привид освітлений з усіх боків (0 = немає ліхтаря)")]
    [SerializeField] float lanternRadius;
    [Tooltip("Чуття: у цьому радіусі ворог помічає привида навіть у темряві (0 = немає)")]
    [SerializeField] float senseRadius;

    [Header("Швидкість")]
    [SerializeField] float walkSpeed = 2.3f;
    [SerializeField] float investigateSpeed = 3f;
    [SerializeField] float chaseSpeed = 3.4f;
    [SerializeField] float turnSpeed = 300f;

    [Header("Зір")]
    [SerializeField] float viewDistance = 7f;
    [SerializeField] float viewAngle = 80f;
    [Tooltip("На цій відстані погляд (конус) сам проявляє привида (0 = не проявляє)")]
    [SerializeField] float flashlightDistance;
    [Tooltip("Скільки секунд треба дивитися на привида, щоб упізнати його")]
    [SerializeField] float noticeTime = 0.6f;
    [SerializeField] float loseSightTime = 2f;
    [SerializeField] float catchDistance = 0.65f;

    [Header("Поведінка")]
    [SerializeField] float waitAtPoint = 1.2f;
    [SerializeField] float searchTime = 4f;
    [SerializeField] float lookAroundAngle = 70f;
    // ЗМІНЕНО: нові налаштування
    [Tooltip("Скільки додаткових місць ворог перевіряє навколо, коли загубив привида")]
    [SerializeField] int extraSearchPoints = 2;
    [Tooltip("Радіус, у якому вибираються додаткові місця пошуку")]
    [SerializeField] float searchSpread = 3f;
    [Tooltip("Шум чути, якщо шлях до нього не довший за гучність, помножену на це число")]
    [SerializeField] float lurePathFactor = 1.6f;

    [Header("Маршрут (заповнює збирач рівнів)")]
    [SerializeField] Vector2[] patrolPoints = new Vector2[0];

    [Header("Візуал")]
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite[] walkDown = new Sprite[0];
    [SerializeField] Sprite[] walkUp = new Sprite[0];
    [Tooltip("Кадри, де ворог дивиться вправо. Вліво віддзеркалюються.")]
    [SerializeField] Sprite[] walkSide = new Sprite[0];
    [SerializeField] float animFps = 8f;
    [Tooltip("Ліхтар чи аура: світло, яке повертається туди, куди дивиться ворог")]
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
    // ЗМІНЕНО: тимчасовий шлях для підрахунку відстані до шуму й черга місць пошуку
    readonly List<Vector2> tmpPath = new List<Vector2>();
    readonly Queue<Vector2> searchQueue = new Queue<Vector2>();
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

    public Vector2 Facing => facing;
    public bool IsSpirit => isSpirit;
    /// <summary>Женеться за привидом (для музики погоні).</summary>
    public bool IsChasing => state == State.Chase;

    /// <summary>Значок над головою ("!" чи "?"), або null.</summary>
    public Sprite AlertSprite => alertIcon != null && alertIcon.enabled ? alertIcon.sprite : null;

    /// <summary>
    /// Кадр ворога для 3D-вигляду: спереду, ззаду чи збоку залежно від того,
    /// звідки на нього дивиться глядач (точка viewer у світових координатах).
    /// </summary>
    public Sprite ViewSprite(Vector2 viewer, out bool flip)
    {
        flip = false;
        Vector2 toViewer = viewer - (Vector2)transform.position;
        if (toViewer.sqrMagnitude < 0.0001f) toViewer = Vector2.down;
        toViewer.Normalize();

        float dot = Vector2.Dot(facing, toViewer);
        Sprite[] set;
        if (dot > 0.7f) set = walkDown;          // дивиться на нас
        else if (dot < -0.7f) set = walkUp;      // стоїть спиною
        else
        {
            set = walkSide;
            // праворуч від глядача, коли той дивиться на ворога
            Vector2 viewDir = -toViewer;
            Vector2 viewerRight = new Vector2(viewDir.y, -viewDir.x);
            flip = Vector2.Dot(facing, viewerRight) < 0f;
        }
        if (set == null || set.Length == 0) return body != null ? body.sprite : null;
        int frame = Animating ? (int)(animTime * animFps) % set.Length : 0;
        return set[frame];
    }

    // дух постійно "майорить", скелет рухає ногами лише під час ходьби
    bool Animating => moving || isSpirit;

    /// <summary>Задати маршрут (точки у світових координатах).</summary>
    public void Configure(Vector2[] points)
    {
        patrolPoints = points;
    }

    void OnEnable() => LureEvents.OnLure += HandleLure;
    void OnDisable() => LureEvents.OnLure -= HandleLure;

    void Awake()
    {
        // складність з меню
        float sp = GameSettings.EnemySpeedMul;
        walkSpeed *= sp;
        investigateSpeed *= sp;
        chaseSpeed *= sp;
        viewDistance *= GameSettings.ViewMul;
        // ЗМІНЕНО: захист від ділення на нуль у Vision
        noticeTime = Mathf.Max(0.05f, noticeTime * GameSettings.NoticeTimeMul);
    }

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
        float reach = Mathf.Max(range, Mathf.Max(lanternRadius, senseRadius * (1f + 0.3f * Alert)));
        bool los = dist <= reach && grid.HasLineOfSight(pos, gp);

        bool inCone = los && dist <= range
                      && (dist < 0.9f || Vector2.Angle(facing, to) <= viewAngle * 0.5f);

        // погляд у конусі проявляє привида
        if (inCone && dist <= flashlightDistance) ghost.Expose(1f);

        // ліхтар скелета світить на всі боки (край кола слабший)
        if (los && lanternRadius > 0f && dist <= lanternRadius)
            ghost.Expose(Mathf.Clamp01((lanternRadius - dist) / (lanternRadius * 0.35f)));

        // дух відчуває привида поруч навіть у темряві
        bool sensed = los && senseRadius > 0f && dist <= senseRadius * (1f + 0.3f * Alert);

        // ЗМІНЕНО: впритул ворог помічає привида лише без стіни між ними
        bool sees = (inCone && ghost.IsVisible) || sensed || (los && dist < 0.8f);

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
            // ЗМІНЕНО: не ловить крізь стіну
            if (dist <= catchDistance && los) Catch();
            return;
        }

        if (sees)
        {
            notice += dt / (noticeTime / (1f + Alert));
            lookTarget = to.normalized;
            // ЗМІНЕНО: dist < 0.8f рахується лише з прямою видимістю (los вже входить у sees)
            if (notice >= 1f || (los && dist < 0.8f)) BeginChase(gp);
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
        searchQueue.Clear();
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
        // ЗМІНЕНО: шум за стіною не чути, якщо йти до нього далеко (або шляху немає)
        if (PathLengthTo(s.Position) > s.Loudness * lurePathFactor) return;

        state = State.Investigate;
        notice = 0f;
        arrived = false;
        investigatePos = s.Position;
        stateTimer = Mathf.Max(2.5f, s.Duration + 1.5f);
        questionTimer = 1.5f;
        SetPathTo(investigatePos);
        GameAudio.Play(huhClip, 0.8f);
    }

    /// <summary>Довжина шляху до точки по сітці (float.MaxValue, якщо дійти не можна).</summary>
    float PathLengthTo(Vector2 world)
    {
        if (grid == null || !grid.FindPathToward(transform.position, world, tmpPath, canCrossWater))
            return float.MaxValue;

        float len = 0f;
        Vector2 prev = transform.position;
        for (int i = 0; i < tmpPath.Count; i++)
        {
            len += Vector2.Distance(prev, tmpPath[i]);
            prev = tmpPath[i];
        }
        return len;
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

        // ЗМІНЕНО: останні сантиметри біжить прямо на привида, але лише якщо по прямій
        // можна пройти (немає стіни, полиці чи води, яку цей ворог не перетинає)
        if (done && ghostTransform != null && lostTimer <= 0f)
        {
            Vector2 pos = transform.position;
            Vector2 gp = ghostTransform.position;
            if (Vector2.Distance(pos, gp) < 2f && grid != null && grid.CanWalkStraight(pos, gp, canCrossWater))
            {
                Vector2 next = Vector2.MoveTowards(pos, gp, chaseSpeed * SpeedMul * dt);
                transform.position = new Vector3(next.x, next.y, transform.position.z);
                moving = true;
            }
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

        // ЗМІНЕНО: окрім останньої точки, де бачив привида, перевіряє ще кілька місць поруч
        searchQueue.Clear();
        if (grid != null)
        {
            for (int i = 0; i < extraSearchPoints; i++)
            {
                Vector2 cand = where + Random.insideUnitCircle * searchSpread;
                if (grid.IsWalkable(LevelGrid.WorldToCell(cand), canCrossWater))
                    searchQueue.Enqueue(cand);
            }
        }
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
        if (stateTimer <= 0f)
        {
            // ЗМІНЕНО: є ще місця - йде в наступне, інакше повертається на патруль
            if (searchQueue.Count > 0)
            {
                SetPathTo(searchQueue.Dequeue());
                arrived = false;
                stateTimer = searchTime * 0.5f;
            }
            else BeginPatrol();
        }
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
        // ЗМІНЕНО: якщо ціль недосяжна, іде до найближчої досяжної точки
        if (grid == null || !grid.FindPathToward(transform.position, world, path, canCrossWater)) path.Clear();
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

        animTime = Animating ? animTime + dt : 0f;
        int frame = Animating ? (int)(animTime * animFps) % set.Length : 0;
        body.sprite = set[frame];
        body.flipX = flip;

        if (isSpirit)
        {
            // дух трохи мерехтить
            var c = body.color;
            c.a = 0.78f + 0.17f * Mathf.Sin(Time.time * 3.1f + transform.position.x);
            body.color = c;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (lanternRadius > 0f)
        {
            Gizmos.color = new Color(1f, 0.7f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, lanternRadius);
        }
        if (senseRadius > 0f)
        {
            Gizmos.color = new Color(0.7f, 0.4f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, senseRadius);
        }
        Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.6f);
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            Gizmos.DrawWireSphere(patrolPoints[i], 0.2f);
            Gizmos.DrawLine(patrolPoints[i], patrolPoints[(i + 1) % patrolPoints.Length]);
        }
    }
}

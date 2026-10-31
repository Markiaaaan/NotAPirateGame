using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ворог підземелля: скелет або злий дух (той самий скрипт, різні налаштування в префабах).
///   Скелет несе ліхтар: у радіусі lanternRadius привида видно з усіх боків. Обходить річку.
///     Ходить маршрутом, іде перевіряти шум від приманок і женеться за привидом, якщо побачив.
///   Злий дух нічого не бачить, зате відчуває РУХ: якщо привид рухається в радіусі senseRadius
///     (навіть за стіною), дух його помічає. Привид, що завмер, для духа невидимий.
///     Дух літає крізь стіни й над водою. Коли женеться, а привид завмер - кружляє навколо й шукає.
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

    [Header("Злий дух")]
    [Tooltip("Швидкість привида, з якої дух уже відчуває рух")]
    [SerializeField] float moveThreshold = 0.35f;
    [Tooltip("Скільки секунд привид має не рухатись, щоб дух його загубив")]
    [SerializeField] float spiritLoseTime = 2.4f;
    [Tooltip("Шепіт духа: що ближче дух, то гучніше")]
    [SerializeField] AudioSource whisper;

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

    // злий дух
    Vector2 lastGhostPos;
    float ghostSpeed, stillTimer, circleAngle, driftTime;
    bool ghostInSense;
    SpriteRenderer senseRing;

    public Vector2 Facing => facing;
    public bool IsSpirit => isSpirit;
    /// <summary>Женеться за привидом (для музики погоні).</summary>
    public bool IsChasing => state == State.Chase;
    /// <summary>Дух: привид у його радіусі чуття (для попередження "ЗАВМРИ!").</summary>
    public bool GhostInSense => isSpirit && ghostInSense && state != State.Caught;
    /// <summary>Наскільки ворог уже помітив привида (0..1).</summary>
    public float NoticeLevel => state == State.Chase ? 1f : Mathf.Clamp01(notice);
    /// <summary>Привид зараз рухається (рахує кожен дух сам, щоб не залежати від гравця).</summary>
    bool GhostMoving => ghostSpeed > moveThreshold;
    /// <summary>Радіус чуття духа зараз (росте, коли підземелля насторожі).</summary>
    public float SenseRange => senseRadius * (1f + 0.3f * Alert);
    public float LanternRadius => lanternRadius;

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
        if (isSpirit) SetupSpirit();
        BeginPatrol();
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnGameOver -= HandleGameOver;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (isSpirit)
        {
            if (state != State.Caught) SpiritUpdate(dt);
            else moving = false;
            UpdateSpiritFx(dt);
        }
        else if (state != State.Caught)
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

    // ---------------- Злий дух ----------------

    void SetupSpirit()
    {
        // дух завжди поверх стін (він крізь них пролітає), тож YSort не потрібен
        var ys = GetComponent<YSort>();
        if (ys != null) ys.enabled = false;
        if (body != null) body.sortingOrder = 25000;
        if (ghostTransform != null) lastGhostPos = ghostTransform.position;
        driftTime = Random.value * 10f;

        // коло чуття (видно у 2D): у ньому рухатися не можна
        var ringGo = new GameObject("SenseRing");
        ringGo.transform.SetParent(transform, false);
        senseRing = ringGo.AddComponent<SpriteRenderer>();
        senseRing.sprite = RingSprite();
        senseRing.sortingOrder = 24999;
        float d = senseRadius * 2f;
        ringGo.transform.localScale = new Vector3(d, d, 1f);
        if (body != null) senseRing.sharedMaterial = body.sharedMaterial;

        if (whisper != null)
        {
            whisper.loop = true;
            whisper.spatialBlend = 0f;
            whisper.volume = 0f;
            if (whisper.clip != null) whisper.Play();
        }
    }

    static Sprite ringSprite;

    static Sprite RingSprite()
    {
        if (ringSprite != null) return ringSprite;
        const int n = 128;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
        for (int x = 0; x < n; x++)
        {
            float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            // піксельне кільце по краю + ледь помітна заливка всередині
            float a = r > 1f ? 0f : r > 0.94f ? 0.85f : r > 0.88f && ((x / 4 + y / 4) & 1) == 0 ? 0.35f : 0.1f * r;
            px[y * n + x] = new Color32(190, 120, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(px);
        tex.Apply();
        ringSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
        return ringSprite;
    }

    void SpiritUpdate(float dt)
    {
        if (ghostTransform == null) return;
        Vector2 pos = transform.position;
        Vector2 gp = ghostTransform.position;

        // швидкість привида (лише переміщення: поворот камери в 3D не рахується)
        float sp = dt > 0f ? Vector2.Distance(gp, lastGhostPos) / dt : 0f;
        ghostSpeed = Mathf.Lerp(ghostSpeed, sp, 1f - Mathf.Exp(-dt * 12f));
        lastGhostPos = gp;

        float dist = Vector2.Distance(pos, gp);
        ghostInSense = dist <= SenseRange;
        bool feels = ghostInSense && GhostMoving;

        // дотик - спіймав, навіть якщо привид завмер
        if (dist <= catchDistance) { Catch(); return; }

        if (state == State.Chase)
        {
            if (GhostMoving && dist <= SenseRange * 2f)
            {
                stillTimer = 0f;
                lastSeenPos = gp;
                Vector2 to = gp - pos;
                lookTarget = to.normalized;
                SetPos(Vector2.MoveTowards(pos, gp, chaseSpeed * SpeedMul * dt));
                moving = true;
            }
            else
            {
                // привид завмер: дух кружляє навколо місця, де востаннє відчув рух
                // (коло починається з того боку, де дух зараз, щоб не пролетіти крізь привида)
                if (stillTimer <= 0f) circleAngle = Mathf.Atan2(pos.y - lastSeenPos.y, pos.x - lastSeenPos.x);
                stillTimer += dt;
                circleAngle += dt * 1.1f;
                Vector2 around = lastSeenPos + new Vector2(Mathf.Cos(circleAngle), Mathf.Sin(circleAngle)) * 1.7f;
                Vector2 to = around - pos;
                if (to.sqrMagnitude > 0.001f) lookTarget = to.normalized;
                SetPos(Vector2.MoveTowards(pos, around, investigateSpeed * 0.6f * dt));
                moving = true;
                if (stillTimer >= spiritLoseTime) BeginSearch(lastSeenPos);
            }
            return;
        }

        if (feels)
        {
            // рух відчувається тим швидше, що ближче привид
            float closeness = 1f + (1f - dist / Mathf.Max(0.1f, SenseRange));
            notice += dt / noticeTime * closeness * (1f + Alert);
            lookTarget = (gp - pos).normalized;
            if (notice >= 1f)
            {
                circleAngle = Mathf.Atan2(pos.y - gp.y, pos.x - gp.x);
                BeginChase(gp);
                return;
            }
        }
        else notice = Mathf.Max(0f, notice - dt * 0.6f);

        // поки щось відчуває - зависає на місці, а не летить далі маршрутом
        if (Noticing) { moving = false; return; }

        switch (state)
        {
            case State.Patrol: UpdatePatrol(dt); break;
            case State.Investigate: UpdateInvestigate(dt); break;
            case State.Search: UpdateSearch(dt); break;
        }
    }

    void SetPos(Vector2 p) => transform.position = new Vector3(p.x, p.y, transform.position.z);

    void UpdateSpiritFx(float dt)
    {
        driftTime += dt;
        if (senseRing != null)
        {
            // кільце пульсує й яскравішає, коли привид усередині й рухається
            float danger = state == State.Chase ? 1f : Mathf.Max(notice, ghostInSense ? 0.35f : 0f);
            float a = 0.25f + 0.55f * danger + 0.08f * Mathf.Sin(driftTime * 3f);
            senseRing.color = new Color(1f, 1f - 0.5f * danger, 1f, a);
            float d = SenseRange * 2f;
            senseRing.transform.localScale = new Vector3(d, d, 1f);
            senseRing.enabled = state != State.Caught;
        }
        if (whisper != null && ghostTransform != null)
        {
            float dist = Vector2.Distance(transform.position, ghostTransform.position);
            float k = Mathf.Clamp01(1f - dist / (SenseRange * 2.2f));
            float want = (0.15f + 0.85f * k) * k * GameSettings.SfxVolume * (state == State.Chase ? 1.2f : 0.9f);
            if (PauseMenu.IsPaused) want = 0f;
            whisper.volume = Mathf.MoveTowards(whisper.volume, want, dt * 2f);
        }
    }

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
        // ЗМІНЕНО: шум за стіною не чути, якщо йти до нього далеко (або шляху немає).
        // Дух летить крізь стіни, тож для нього важить лише відстань.
        if (!isSpirit && PathLengthTo(s.Position) > s.Loudness * lurePathFactor) return;

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
        if (isSpirit)
        {
            // дух летить навпростець, крізь стіни
            path.Clear();
            path.Add(world);
            return;
        }
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

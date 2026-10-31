using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Ретро-3D вигляд від першої особи (як Doom / Wolfenstein), намальований пікселями.
/// Гра всередині та сама, що й у 2D: сітка рівня, скелети й духи, приманки, світло.
/// Цей скрипт лише "дивиться" на неї очима привида і малює картинку в маленьку текстуру,
/// яка розтягується на весь екран.
///
/// Висить на Main Camera рівня. Якщо в меню вибрано 2D (клавіша T), вимикає себе.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RetroView : MonoBehaviour
{
    [Header("Екран")]
    [SerializeField] int width = 320;
    [SerializeField] int height = 180;
    [SerializeField] float fov = 70f;
    [SerializeField] float eyeHeight = 0.75f;
    [SerializeField] float wallHeight = 1.6f;
    [SerializeField] float maxDistance = 22f;

    [Header("Атмосфера")]
    [SerializeField] float fogDensity = 0.12f;
    [SerializeField] Color fogColor = new Color(0.015f, 0.017f, 0.03f);
    [Tooltip("Наскільки сильно Global Light 2D освітлює 3D-світ")]
    [SerializeField] float ambientScale = 1.1f;
    [SerializeField] float lightScale = 1.6f;
    [Tooltip("Менше = світліші тіні")]
    [SerializeField] float gamma = 0.62f;

    [Header("Текстури")]
    [SerializeField] Sprite floorA;
    [SerializeField] Sprite floorB;
    [SerializeField] Sprite wallTexture;
    [SerializeField] Sprite shelfA;
    [SerializeField] Sprite shelfB;
    [SerializeField] Sprite exitDoor;
    [SerializeField] Sprite shelfC;
    [SerializeField] Sprite shelfD;
    [SerializeField] Sprite column;
    [SerializeField] Sprite carpet;
    [SerializeField] Sprite water;
    [Tooltip("Текстура стелі. Якщо порожньо - стеля з панелей і ламп")]
    [SerializeField] Sprite ceilingTexture;
    [Tooltip("Варіанти стін: [0] звичайна, далі постери, годинник, вікно...")]
    [SerializeField] Sprite[] wallVariants = new Sprite[0];

    class Tex
    {
        public int w, h;
        public Color32[] px;
    }

    class Board
    {
        public Transform t;
        public SpriteRenderer sr;
        public GuardAI guard;
        public LeverLure lever;
        public bool isAlert;
        public BossTrap stoneTrap;
        public float height;
        public float baseZ;
        public float fixedLight;   // > 0: не освітлюється, а має сталу яскравість
    }

    // ---- стан ----
    Texture2D target;
    Color32[] buf;
    float[] zbuf;
    float[] vignette;
    LevelGrid grid;
    Transform player;
    FirstPersonMovement fpm;
    Camera cam;

    Tex tFloorA, tFloorB, tWall, tShelfA, tShelfB, tExit, tCeil, tShelfC, tShelfD, tColumn, tCarpet, tWater;
    bool dungeonCeiling;
    Tex[] tWalls;
    readonly Dictionary<Sprite, Tex> cache = new Dictionary<Sprite, Tex>();
    readonly List<Board> boards = new List<Board>();

    // видимі спрайти за кадр
    Board[] vBoard = new Board[128];
    Sprite[] vSprite = new Sprite[128];
    bool[] vFlip = new bool[128];
    float[] vDepth = new float[128];
    float[] vLat = new float[128];
    int[] vOrder = new int[128];

    // світло
    const int MaxLights = 48;
    readonly float[] lx = new float[MaxLights], ly = new float[MaxLights], lz = new float[MaxLights];
    readonly float[] lr = new float[MaxLights], lg = new float[MaxLights], lb = new float[MaxLights];
    readonly float[] lrad2 = new float[MaxLights], linv = new float[MaxLights];
    readonly float[] ldx = new float[MaxLights], ldy = new float[MaxLights];
    readonly float[] lcosO = new float[MaxLights], lcosI = new float[MaxLights];
    readonly bool[] lspot = new bool[MaxLights];
    int lightCount;
    float ambR, ambG, ambB;
    readonly List<Behaviour> lights2D = new List<Behaviour>();
    PropertyInfo pColor, pIntensity, pRadius, pAngle, pType;

    float fogR, fogG, fogB;
    readonly byte[] gammaLut = new byte[1024];

    // ================================================================ запуск

    void Awake()
    {
        cam = GetComponent<Camera>();
        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            player = pm.transform;
            fpm = pm.GetComponent<FirstPersonMovement>();
        }

        bool firstPerson = GameSettings.FirstPerson && fpm != null;
        if (!firstPerson)
        {
            if (fpm != null) fpm.enabled = false;
            enabled = false;
            return;
        }

        pm.enabled = false;
        fpm.enabled = true;

        // 2D-світ більше не малюємо: камера лише чистить екран, а картинку дає RawImage.
        cam.cullingMask = 0;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;

        fogR = fogColor.r; fogG = fogColor.g; fogB = fogColor.b;
        for (int i = 0; i < gammaLut.Length; i++)
            gammaLut[i] = (byte)(Mathf.Pow(i / (gammaLut.Length - 1f), gamma) * 255f);
        CreateScreen();
        CreateTextures();
    }

    void Start()
    {
        grid = LevelGrid.Instance;
        CollectBoards();
        CollectLights();
    }

    void CreateScreen()
    {
        target = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        buf = new Color32[width * height];
        zbuf = new float[width];
        vignette = new float[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float dx = (x + 0.5f) / width * 2f - 1f;
            float dy = (y + 0.5f) / height * 2f - 1f;
            vignette[y * width + x] = Mathf.Clamp01(1f - 0.35f * (dx * dx + dy * dy));
        }

        var canvasGo = new GameObject("RetroViewCanvas", typeof(RectTransform));
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;
        var imgGo = new GameObject("View", typeof(RectTransform));
        imgGo.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)imgGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var raw = imgGo.AddComponent<RawImage>();
        raw.texture = target;
        raw.raycastTarget = false;
    }

    void CreateTextures()
    {
        tFloorA = Get(floorA) ?? Solid(new Color32(56, 60, 88, 255));
        tFloorB = Get(floorB) ?? Solid(new Color32(50, 54, 80, 255));
        tWall = Get(wallTexture) ?? Solid(new Color32(70, 60, 96, 255));
        tShelfA = Get(shelfA) ?? tWall;
        tShelfB = Get(shelfB) ?? tShelfA;
        tExit = Get(exitDoor) ?? Solid(new Color32(60, 180, 100, 255));
        tShelfC = Get(shelfC) ?? tShelfA;
        tShelfD = Get(shelfD) ?? tShelfB;
        tColumn = Get(column) ?? tWall;
        tCarpet = Get(carpet) ?? tFloorA;
        tWater = Get(water) ?? Solid(new Color32(34, 60, 94, 255));
        tWalls = new Tex[Mathf.Max(1, wallVariants.Length)];
        tWalls[0] = tWall;
        for (int i = 1; i < wallVariants.Length; i++) tWalls[i] = Get(wallVariants[i]) ?? tWall;

        var ceilTex = Get(ceilingTexture);
        dungeonCeiling = ceilTex != null;
        if (dungeonCeiling)
        {
            tCeil = ceilTex;
            return;
        }

        // стеля: темні панелі з лініями
        tCeil = new Tex { w = 32, h = 32, px = new Color32[32 * 32] };
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            bool line = x == 0 || y == 0 || x == 16 || y == 16;
            tCeil.px[y * 32 + x] = line ? new Color32(22, 22, 28, 255) : new Color32(40, 41, 50, 255);
        }
    }

    static Tex Solid(Color32 c) => new Tex { w = 1, h = 1, px = new[] { c } };

    /// <summary>Пікселі спрайта (текстура має бути Read/Write, це вмикає збирач гри).</summary>
    Tex Get(Sprite s)
    {
        if (s == null) return null;
        if (cache.TryGetValue(s, out var t)) return t;

        Tex result = null;
        try
        {
            var tex = s.texture;
            Rect r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            int ox = Mathf.RoundToInt(r.x), oy = Mathf.RoundToInt(r.y);
            Color32[] all = tex.GetPixels32();
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = all[(oy + y) * tex.width + ox + x];
            result = new Tex { w = w, h = h, px = px };
        }
        catch (Exception)
        {
            Debug.LogWarning($"RetroView: не можу прочитати '{s.name}'. Увімкни Read/Write у налаштуваннях текстури.");
        }
        cache[s] = result;
        return result;
    }

    void CollectBoards()
    {
        boards.Clear();
        foreach (var sr in Resources.FindObjectsOfTypeAll<SpriteRenderer>())
        {
            if (sr == null || sr.gameObject.scene != gameObject.scene) continue;
            if (player != null && sr.transform.IsChildOf(player)) continue;

            string n = sr.gameObject.name;
            string parent = sr.transform.parent != null ? sr.transform.parent.name : "";
            if (parent == "Floor" || parent == "Walls") continue;
            if (n.StartsWith("shelf_") || n.StartsWith("wall") || n.StartsWith("floor") || n.StartsWith("column") || n == "Exit" || n == "Alert"
                || n == "SenseRing" || n == "TrapMark") continue;

            var guard = sr.GetComponent<GuardAI>();
            if (guard != null)
            {
                // скелет стоїть на підлозі, дух трохи висить у повітрі
                float h = (guard.IsSpirit ? 1.35f : 1.3f) * Mathf.Abs(sr.transform.lossyScale.y);
                float z = guard.IsSpirit ? 0.08f : 0f;
                boards.Add(new Board { t = sr.transform, sr = sr, guard = guard, height = h, baseZ = z, fixedLight = guard.IsSpirit ? 0.9f : 0.8f });
                boards.Add(new Board { t = sr.transform, sr = sr, guard = guard, isAlert = true, height = 0.35f, baseZ = z + h + 0.05f, fixedLight = 1f });
                continue;
            }
            // ключ висить у повітрі й світиться сам
            bool key = n.StartsWith("Key");
            var trap = sr.GetComponentInParent<BossTrap>();
            boards.Add(new Board { t = sr.transform, sr = sr, height = HeightFor(n), baseZ = n == "KeyBelt" ? 1.1f : key ? 0.35f : 0f,
                fixedLight = n.StartsWith("pumpkin") || key || n.StartsWith("TrapChain") ? 1f : 0f, lever = sr.GetComponent<LeverLure>(),
                stoneTrap = trap != null && trap.Stone == sr ? trap : null });
        }
    }

    static float HeightFor(string n)
    {
        if (n.StartsWith("TrapChain")) return 1.6f;
        if (n == "HangingStone") return 1.5f;
        if (n == "KeyBelt") return 0.4f;
        if (n.StartsWith("Mannequin")) return 1.35f;
        if (n.StartsWith("Lamp")) return 1.5f;
        if (n.StartsWith("TV")) return 1.0f;
        if (n.StartsWith("Vase")) return 0.8f;
        if (n.StartsWith("Phone")) return 0.8f;
        if (n.StartsWith("counter")) return 1.0f;
        if (n.StartsWith("plant")) return 0.95f;
        if (n.StartsWith("rack")) return 1.2f;
        if (n.StartsWith("boxes")) return 1.0f;
        if (n.StartsWith("cart")) return 0.95f;
        if (n.StartsWith("pumpkin")) return 0.55f;
        if (n.StartsWith("Key")) return 0.45f;
        if (n.StartsWith("Lever")) return 1.0f;
        return 1f;
    }

    void CollectLights()
    {
        lights2D.Clear();
        Type type = null;
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = a.GetType("UnityEngine.Rendering.Universal.Light2D", false);
            if (type != null) break;
        }
        if (type == null) return;

        pColor = type.GetProperty("color");
        pIntensity = type.GetProperty("intensity");
        pRadius = type.GetProperty("pointLightOuterRadius");
        pAngle = type.GetProperty("pointLightOuterAngle");
        pType = type.GetProperty("lightType");

        foreach (var o in Resources.FindObjectsOfTypeAll(type))
        {
            var b = o as Behaviour;
            if (b != null && b.gameObject.scene == gameObject.scene) lights2D.Add(b);
        }
    }

    // ================================================================ світло

    void UpdateLights()
    {
        lightCount = 0;
        ambR = ambG = ambB = 0f;
        bool hasGlobal = false;

        foreach (var l in lights2D)
        {
            if (l == null || !l.isActiveAndEnabled) continue;

            Color c = pColor != null ? (Color)pColor.GetValue(l) : Color.white;
            float inten = pIntensity != null ? (float)pIntensity.GetValue(l) : 1f;
            int kind = pType != null ? Convert.ToInt32(pType.GetValue(l)) : 3;

            if (kind == 4) // Global
            {
                hasGlobal = true;
                ambR += c.r * inten * ambientScale;
                ambG += c.g * inten * ambientScale;
                ambB += c.b * inten * ambientScale;
                continue;
            }
            if (lightCount >= MaxLights) continue;

            float radius = (pRadius != null ? (float)pRadius.GetValue(l) : 2f) * 1.3f;
            float angle = pAngle != null ? (float)pAngle.GetValue(l) : 360f;
            Vector3 p = l.transform.position;

            int i = lightCount++;
            lx[i] = p.x + 0.5f;
            ly[i] = -p.y + 0.5f;
            lz[i] = LightHeight(l.gameObject.name);
            lr[i] = c.r * inten * lightScale;
            lg[i] = c.g * inten * lightScale;
            lb[i] = c.b * inten * lightScale;
            lrad2[i] = radius * radius;
            linv[i] = 1f / radius;
            lspot[i] = angle < 359f;
            if (lspot[i])
            {
                Vector2 up = l.transform.up;
                ldx[i] = up.x;
                ldy[i] = -up.y;
                lcosO[i] = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
                lcosI[i] = Mathf.Cos(angle * 0.3f * Mathf.Deg2Rad);
            }
        }

        if (!hasGlobal)
        {
            ambR = 0.1f; ambG = 0.11f; ambB = 0.15f;
        }
    }

    static float LightHeight(string name)
    {
        if (name == "Flashlight") return 1.1f;
        if (name == "Lantern") return 0.8f;
        if (name == "Aura") return 1.0f;
        if (name == "Glow") return 0.7f;
        return 1.2f;
    }

    void Shade(float x, float y, float z, out float r, out float g, out float b)
    {
        r = ambR; g = ambG; b = ambB;
        for (int i = 0; i < lightCount; i++)
        {
            float dx = x - lx[i], dy = y - ly[i], dz = z - lz[i];
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 >= lrad2[i]) continue;
            float k = 1f - Mathf.Sqrt(d2) * linv[i];
            k *= k;
            if (lspot[i])
            {
                float len = Mathf.Sqrt(dx * dx + dy * dy) + 0.0001f;
                float cs = (dx * ldx[i] + dy * ldy[i]) / len;
                if (cs <= lcosO[i]) continue;
                if (cs < lcosI[i]) k *= (cs - lcosO[i]) / (lcosI[i] - lcosO[i]);
            }
            r += k * lr[i];
            g += k * lg[i];
            b += k * lb[i];
        }
    }

    // ================================================================ кадр

    Sprite lastExitSprite;

    void LateUpdate()
    {
        if (grid == null || player == null || fpm == null) return;
        UpdateLights();

        // двері виходу анімуються (замок, відчинення) - беремо їхній поточний кадр
        var exit = LevelExit.Instance;
        if (exit != null && exit.CurrentSprite != null && exit.CurrentSprite != lastExitSprite)
        {
            lastExitSprite = exit.CurrentSprite;
            tExit = Get(lastExitSprite) ?? tExit;
        }

        int W = width, H = height;
        Vector2 wp = player.position;
        float posX = wp.x + 0.5f, posY = -wp.y + 0.5f;
        Vector2 f = fpm.Forward;
        float dirX = f.x, dirY = -f.y;
        float rightX = -dirY, rightY = dirX;
        float planeLen = Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
        float planeX = rightX * planeLen, planeY = rightY * planeLen;
        float pv = W * 0.5f / planeLen;
        float hz = H * 0.5f + Mathf.Sin(Time.time * 2.2f) * 1.2f;
        float eye = eyeHeight;

        for (int x = 0; x < W; x++)
        {
            float cx = 2f * (x + 0.5f) / W - 1f;
            float rdx = dirX + planeX * cx, rdy = dirY + planeY * cx;

            // ---- стіни (DDA по сітці) ----
            int mapX = Mathf.FloorToInt(posX), mapY = Mathf.FloorToInt(posY);
            float ddx = Mathf.Abs(rdx) < 1e-6f ? 1e30f : Mathf.Abs(1f / rdx);
            float ddy = Mathf.Abs(rdy) < 1e-6f ? 1e30f : Mathf.Abs(1f / rdy);
            int stepX, stepY;
            float sdx, sdy;
            if (rdx < 0) { stepX = -1; sdx = (posX - mapX) * ddx; } else { stepX = 1; sdx = (mapX + 1f - posX) * ddx; }
            if (rdy < 0) { stepY = -1; sdy = (posY - mapY) * ddy; } else { stepY = 1; sdy = (mapY + 1f - posY) * ddy; }

            int side = 0;
            char hit = '#';
            bool found = false;
            for (int i = 0; i < 128; i++)
            {
                if (sdx < sdy) { sdx += ddx; mapX += stepX; side = 0; }
                else { sdy += ddy; mapY += stepY; side = 1; }
                char c = grid.Get(new Vector2Int(mapX, mapY));
                if (IsWall(c)) { hit = c; found = true; break; }
            }

            float perp = side == 0 ? sdx - ddx : sdy - ddy;
            if (!found || perp > maxDistance) perp = maxDistance;
            if (perp < 0.05f) perp = 0.05f;
            zbuf[x] = perp;

            float wallU = side == 0 ? posY + perp * rdy : posX + perp * rdx;
            wallU -= Mathf.Floor(wallU);
            if (side == 0 && rdx < 0) wallU = 1f - wallU;
            if (side == 1 && rdy > 0) wallU = 1f - wallU;

            float hitX = posX + perp * rdx - rdx * 0.03f;
            float hitY = posY + perp * rdy - rdy * 0.03f;
            float topF = hz - (wallHeight - eye) * pv / perp;
            float botF = hz + eye * pv / perp;
            int top = Mathf.Max(0, Mathf.CeilToInt(topF));
            int bot = Mathf.Min(H - 1, Mathf.FloorToInt(botF));

            Tex wt = WallTex(hit, mapX, mapY);
            float sideShade = side == 1 ? 0.8f : 1f;
            for (int y = top; y <= bot; y++)
            {
                float v = (y + 0.5f - topF) / (botF - topF);
                Shade(hitX, hitY, wallHeight * (1f - v), out float r, out float g, out float b);
                Color32 surface = Sample(wt, wallU, v);
                if (hit == '#')
                {
                    // Холодний камінь із виразними швами відділяє стіни від теплої підлоги.
                    int row = Mathf.FloorToInt(v * 6f);
                    float brickU = Mathf.Repeat(wallU * 2f + (row % 2) * 0.5f, 1f);
                    float brickV = Mathf.Repeat(v * 6f, 1f);
                    float value = (surface.r + surface.g + surface.b) / 3f;
                    if (value < 150f)
                    {
                        float stone = 108f + value * 0.18f;
                        float edge = brickV < 0.09f || brickU < 0.045f ? 0.48f
                            : brickV < 0.18f ? 1.16f : 1f;
                        surface = new Color32((byte)(stone * 0.82f * edge),
                            (byte)(stone * 0.97f * edge), (byte)(stone * 1.1f * edge), 255);
                    }
                    if (v < 0.025f || v > 0.94f)
                        surface = new Color32(57, 71, 84, 255);
                    r = Mathf.Max(r, 0.24f);
                    g = Mathf.Max(g, 0.24f);
                    b = Mathf.Max(b, 0.24f);
                }
                Put(x, y, surface, r * sideShade, g * sideShade, b * sideShade, perp);
            }

            // ---- стеля ----
            for (int y = 0; y < top; y++)
            {
                float den = hz - y - 0.5f;
                if (den <= 0.01f) continue;
                float rd = (wallHeight - eye) * pv / den;
                float fx = posX + rd * rdx, fy = posY + rd * rdy;
                int ci = Mathf.FloorToInt(fx), cj = Mathf.FloorToInt(fy);
                float u = fx - ci, vv = fy - cj;
                if (dungeonCeiling)
                {
                    // рідкі ґрати, крізь які падає місячне світло
                    bool grate = LevelGrid.CellHash(ci, cj) % 13 == 0 && u > 0.3f && u < 0.7f && vv > 0.3f && vv < 0.7f;
                    if (grate)
                    {
                        bool bar = ((int)(u * 20f) % 3) == 0 || ((int)(vv * 20f) % 4) == 0;
                        if (bar) Put(x, y, new Color32(30, 30, 36, 255), ambR, ambG, ambB, rd);
                        else PutEmissive(x, y, 0.35f, 0.45f, 0.7f, rd);
                        continue;
                    }
                }
                else
                {
                    bool lamp = ((ci % 4) + 4) % 4 == 1 && ((cj % 3) + 3) % 3 == 1 && u > 0.25f && u < 0.75f && vv > 0.42f && vv < 0.58f;
                    if (lamp)
                    {
                        PutEmissive(x, y, 0.75f, 0.78f, 0.85f, rd);
                        continue;
                    }
                }
                Shade(fx, fy, wallHeight, out float r, out float g, out float b);
                Put(x, y, Sample(tCeil, u, vv), r, g, b, rd);
            }

            // ---- підлога ----
            for (int y = bot + 1; y < H; y++)
            {
                float den = y + 0.5f - hz;
                if (den <= 0.01f) continue;
                float rd = eye * pv / den;
                float fx = posX + rd * rdx, fy = posY + rd * rdy;
                int ci = Mathf.FloorToInt(fx), cj = Mathf.FloorToInt(fy);
                char fc = grid.Get(new Vector2Int(ci, cj));
                Shade(fx, fy, 0f, out float r, out float g, out float b);
                if (fc == '~')
                {
                    // вода тече: зсуваємо текстуру з часом
                    float wu = fx - ci + Time.time * 0.15f;
                    wu -= Mathf.Floor(wu);
                    Put(x, y, Sample(tWater, wu, fy - cj), r * 1.2f, g * 1.2f, b * 1.3f, rd);
                    continue;
                }
                Tex ft = fc == ',' ? tCarpet : ((ci + cj) & 1) == 0 ? tFloorB : tFloorA;
                Color32 floorColor = Sample(ft, fx - ci, fy - cj);
                if (BossTrap.Instance != null)
                {
                    float mark = BossTrap.Instance.FloorMark(fx - 0.5f, -fy + 0.5f);
                    if (mark > 0f) floorColor = Color32.Lerp(floorColor, new Color32(255, 155, 35, 255), mark);
                }
                Put(x, y, floorColor, r, g, b, rd);
            }
        }

        DrawSprites(wp, posX, posY, dirX, dirY, rightX, rightY, pv, hz, eye);

        target.SetPixels32(buf);
        target.Apply(false);
    }

    static bool IsWall(char c) => c == '#' || c == 'S' || c == 's' || c == 'E' || c == 'O' || c == ' ';

    Tex WallTex(char c, int x, int y)
    {
        switch (c)
        {
            case 'S': return LevelGrid.ShelfVariant(x, y) == 0 ? tShelfA : tShelfC;
            case 's': return LevelGrid.ShelfVariant(x, y) == 0 ? tShelfB : tShelfD;
            case 'E': return tExit;
            case 'O': return tColumn;
            case '#': return tWalls[LevelGrid.WallVariant(x, y, tWalls.Length)];
            default: return tWall;
        }
    }

    static Color32 Sample(Tex t, float u, float v)
    {
        int ix = (int)(u * t.w);
        int iy = t.h - 1 - (int)(v * t.h);
        if (ix < 0) ix = 0; else if (ix >= t.w) ix = t.w - 1;
        if (iy < 0) iy = 0; else if (iy >= t.h) iy = t.h - 1;
        return t.px[iy * t.w + ix];
    }

    void Put(int x, int y, Color32 c, float lightR, float lightG, float lightB, float dist)
    {
        float fog = 1f / (1f + dist * fogDensity + dist * dist * fogDensity * fogDensity * 0.6f);
        float r = c.r * (1f / 255f) * lightR * fog + fogR * (1f - fog);
        float g = c.g * (1f / 255f) * lightG * fog + fogG * (1f - fog);
        float b = c.b * (1f / 255f) * lightB * fog + fogB * (1f - fog);
        Write(x, y, r, g, b);
    }

    void PutEmissive(int x, int y, float r, float g, float b, float dist)
    {
        float fog = 1f / (1f + dist * fogDensity * 0.6f);
        Write(x, y, r * fog + fogR * (1f - fog), g * fog + fogG * (1f - fog), b * fog + fogB * (1f - fog));
    }

    void Write(int x, int y, float r, float g, float b)
    {
        int idx = (height - 1 - y) * width + x;
        float vg = vignette[idx];
        buf[idx] = new Color32(ToByte(r * vg), ToByte(g * vg), ToByte(b * vg), 255);
    }

    byte ToByte(float v)
    {
        if (v <= 0f) return 0;
        if (v >= 1f) return 255;
        return gammaLut[(int)(v * (gammaLut.Length - 1))];
    }

    // ================================================================ спрайти

    void DrawSprites(Vector2 wp, float posX, float posY, float dirX, float dirY, float rightX, float rightY,
        float pv, float hz, float eye)
    {
        int count = 0;
        foreach (var b in boards)
        {
            if (b.t == null || !b.t.gameObject.activeInHierarchy || count >= vBoard.Length) continue;

            Sprite s;
            bool flip = false;
            if (b.isAlert) s = b.guard != null ? b.guard.AlertSprite : null;
            else if (b.guard != null) s = b.guard.ViewSprite(wp, out flip);
            else
            {
                if (b.sr == null || !b.sr.enabled || !b.sr.gameObject.activeInHierarchy) continue;
                s = b.sr.sprite;
                flip = b.sr.flipX;
            }
            if (s == null) continue;

            Vector3 p = b.stoneTrap != null ? (Vector3)b.stoneTrap.ImpactPosition : b.t.position;
            float relX = p.x + 0.5f - posX, relY = -p.y + 0.5f - posY;
            float depth = relX * dirX + relY * dirY;
            if (depth < 0.2f || depth > maxDistance) continue;

            vBoard[count] = b;
            vSprite[count] = s;
            vFlip[count] = flip;
            vDepth[count] = depth;
            vLat[count] = relX * rightX + relY * rightY;
            vOrder[count] = count;
            count++;
        }

        // від далеких до ближніх
        for (int i = 1; i < count; i++)
        {
            int key = vOrder[i];
            int j = i - 1;
            while (j >= 0 && vDepth[vOrder[j]] < vDepth[key]) { vOrder[j + 1] = vOrder[j]; j--; }
            vOrder[j + 1] = key;
        }

        int W = width, H = height;
        for (int k = 0; k < count; k++)
        {
            int i = vOrder[k];
            var b = vBoard[i];
            Tex tx = Get(vSprite[i]);
            if (tx == null) continue;

            float depth = vDepth[i];
            float hW = b.height;
            float baseZ = b.stoneTrap != null ? b.stoneTrap.StoneHeight : b.baseZ;
            float wW = hW * tx.w / tx.h;
            float sx = W * 0.5f + vLat[i] / depth * pv;
            float hp = hW * pv / depth, wpx = wW * pv / depth;
            float topF = hz - (baseZ + hW - eye) * pv / depth;
            float leftF = sx - wpx * 0.5f;

            float r, g, bl;
            if (b.fixedLight > 0f) { r = g = bl = b.fixedLight; }
            else
            {
                Vector3 p = b.t.position;
                Shade(p.x + 0.5f, -p.y + 0.5f, baseZ + hW * 0.5f, out r, out g, out bl);
            }

            int x0 = Mathf.Max(0, Mathf.FloorToInt(leftF)), x1 = Mathf.Min(W - 1, Mathf.CeilToInt(leftF + wpx));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(topF)), y1 = Mathf.Min(H - 1, Mathf.CeilToInt(topF + hp));
            for (int x = x0; x <= x1; x++)
            {
                if (depth >= zbuf[x]) continue;
                float u = (x + 0.5f - leftF) / wpx;
                if (u < 0f || u >= 1f) continue;
                if (vFlip[i]) u = 0.9999f - u;
                for (int y = y0; y <= y1; y++)
                {
                    float v = (y + 0.5f - topF) / hp;
                    if (v < 0f || v >= 1f) continue;
                    Color32 c = Sample(tx, u, v);
                    if (c.a < 128) continue;
                    Put(x, y, c, r, g, bl, depth);
                }
            }
        }
    }
}

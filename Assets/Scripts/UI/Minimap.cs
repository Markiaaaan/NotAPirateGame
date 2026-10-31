using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Мінікарта в правому верхньому куті (і в 3D, і в 2D). Малюється в маленьку текстуру,
/// 6 пікселів на клітинку: стіни, підлога, бруківка, вода, полиці, бочки, колони, саркофаги,
/// смолоскипи з колом світла (там тебе видно!), приманки, важелі, двері виходу, вороги й привид.
/// Нерухоме малюється один раз, решта - щокадру поверх.
/// Додається сама з GameUI, нічого налаштовувати не треба.
/// </summary>
public class Minimap : MonoBehaviour
{
    /// <summary>Нижній край мінікарти в одиницях інтерфейсу (від верху екрана), або -1. Під ним GameUI ставить завдання.</summary>
    public static float BottomUnits { get; private set; } = -1f;

    const int C = 6;          // пікселів на клітинку
    const float Scale = 2f;   // одиниць інтерфейсу на піксель мінікарти

    LevelGrid grid;
    Texture2D tex;
    Color32[] baseLayer, buf;
    int tw, th;
    Transform player;
    FirstPersonMovement fpm;
    readonly List<GuardAI> enemies = new List<GuardAI>();
    readonly List<LureObject> lures = new List<LureObject>();
    float time;

    static readonly Color32 Clear = new Color32(0, 0, 0, 0);

    void Start()
    {
        grid = LevelGrid.Instance;
        if (grid == null || grid.Width == 0) { enabled = false; return; }

        var pm = FindAnyObjectByType<PlayerMovement>();
        if (pm != null)
        {
            player = pm.transform;
            fpm = pm.GetComponent<FirstPersonMovement>();
        }
        enemies.AddRange(FindObjectsByType<GuardAI>(FindObjectsSortMode.None));
        lures.AddRange(FindObjectsByType<LureObject>(FindObjectsSortMode.None));

        tw = grid.Width * C;
        th = grid.Height * C;
        tex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        baseLayer = new Color32[tw * th];
        buf = new Color32[tw * th];
        DrawBase();
        BuildUI();
    }

    void OnDestroy() => BottomUnits = -1f;

    void BuildUI()
    {
        var root = UIFactory.CreateCanvas(transform, "Minimap", 95).transform;
        Vector2 size = new Vector2(tw * Scale, th * Scale);
        var frame = UIFactory.MakePanel(root, "Frame", new Vector2(1f, 1f), new Vector2(-20f, -20f), size + new Vector2(28f, 28f), false, 4f, 0.92f);
        var img = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
        img.transform.SetParent(frame.transform, false);
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        img.texture = tex;
        img.raycastTarget = false;
        BottomUnits = 20f + size.y + 28f;
    }

    // ================================================================ нерухоме

    void DrawBase()
    {
        for (int i = 0; i < baseLayer.Length; i++) baseLayer[i] = Clear;

        for (int y = 0; y < grid.Height; y++)
        for (int x = 0; x < grid.Width; x++)
        {
            char c = grid.Get(new Vector2Int(x, y));
            if (c == ' ') continue;
            if (c == '#' || c == 'E') { DrawWall(x, y); continue; }
            DrawFloor(x, y, c);
            switch (c)
            {
                case 'S': Box(x, y, 0, 0, 6, 6, new Color32(104, 66, 40, 255)); Box(x, y, 1, 1, 4, 1, new Color32(60, 30, 26, 255)); Box(x, y, 1, 3, 4, 1, new Color32(60, 30, 26, 255)); break;
                case 's': Box(x, y, 0, 0, 6, 6, new Color32(86, 82, 88, 255)); Px(x, y, 1, 1, Bone); Px(x, y, 4, 1, Bone); Px(x, y, 1, 4, Bone); Px(x, y, 4, 4, Bone); break;
                case 'O': Circle(x, y, 2.3f, new Color32(128, 124, 136, 255)); Px(x, y, 2, 2, new Color32(168, 164, 176, 255)); break;
                case 'C': Box(x, y, 0, 1, 6, 4, new Color32(120, 114, 106, 255)); Box(x, y, 1, 2, 4, 2, new Color32(96, 90, 84, 255)); break;
                case 'p': Px(x, y, 1, 4, Bone); Px(x, y, 2, 4, Bone); Px(x, y, 4, 2, Bone); Px(x, y, 3, 1, Bone); break;
                case 'R': Circle(x, y, 2.2f, new Color32(126, 80, 44, 255)); Box(x, y, 1, 2, 4, 1, new Color32(70, 46, 30, 255)); break;
                case 'B': Box(x, y, 0, 0, 6, 6, new Color32(84, 58, 34, 255)); Box(x, y, 1, 1, 4, 4, new Color32(158, 116, 64, 255)); break;
                case 'K': Box(x, y, 0, 1, 6, 4, new Color32(96, 100, 112, 255)); Box(x, y, 1, 2, 4, 2, new Color32(60, 62, 70, 255)); break;
                case 'J': Glow(x, y, 1.6f, new Color32(255, 200, 90, 255), 0.18f); Box(x, y, 2, 2, 2, 2, new Color32(255, 214, 110, 255)); break;
                case 'L': Glow(x, y, 2.4f, new Color32(255, 160, 70, 255), 0.26f); Box(x, y, 2, 2, 2, 2, new Color32(255, 150, 50, 255)); Px(x, y, 2, 1, new Color32(255, 230, 140, 255)); break;
            }
        }
    }

    static readonly Color32 Bone = new Color32(214, 206, 186, 255);

    void DrawFloor(int x, int y, char c)
    {
        Color32 a, b;
        if (c == '~') { a = new Color32(28, 56, 96, 255); b = new Color32(44, 84, 128, 255); }
        else if (c == ',') { a = new Color32(64, 56, 52, 255); b = new Color32(80, 70, 62, 255); }
        else { a = ((x + y) & 1) == 0 ? new Color32(36, 33, 44, 255) : new Color32(40, 37, 49, 255); b = a; }
        for (int py = 0; py < C; py++)
        for (int px = 0; px < C; px++)
        {
            bool alt = c == '~' ? ((px + py * 2 + x * 3) % 6) == 0 : c == ',' && ((px + 1) % 3 == 0) && (py % 3 == 1);
            Set(x * C + px, y * C + py, alt ? b : a);
        }
    }

    void DrawWall(int x, int y)
    {
        // стіна біля проходу - світлий камінь з обведенням, глибока стіна - темна
        bool open(int ox, int oy)
        {
            char n = grid.Get(new Vector2Int(x + ox, y + oy));
            return n != '#' && n != ' ' && n != 'E';
        }
        bool edge = false;
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
            if ((dx != 0 || dy != 0) && open(dx, dy)) edge = true;
        Color32 fill = edge ? new Color32(96, 88, 110, 255) : new Color32(46, 42, 56, 230);
        Color32 rim = new Color32(150, 140, 168, 255);
        for (int py = 0; py < C; py++)
        for (int px = 0; px < C; px++)
        {
            bool r = (py == 0 && open(0, -1)) || (py == C - 1 && open(0, 1)) || (px == 0 && open(-1, 0)) || (px == C - 1 && open(1, 0));
            Set(x * C + px, y * C + py, r ? rim : fill);
        }
    }

    void Box(int cx, int cy, int x0, int y0, int w, int h, Color32 col)
    {
        for (int py = y0; py < y0 + h; py++)
        for (int px = x0; px < x0 + w; px++)
            Set(cx * C + px, cy * C + py, col);
    }

    void Px(int cx, int cy, int px, int py, Color32 col) => Set(cx * C + px, cy * C + py, col);

    void Circle(int cx, int cy, float r, Color32 col)
    {
        for (int py = 0; py < C; py++)
        for (int px = 0; px < C; px++)
        {
            float dx = px + 0.5f - C * 0.5f, dy = py + 0.5f - C * 0.5f;
            if (dx * dx + dy * dy <= r * r) Set(cx * C + px, cy * C + py, col);
        }
    }

    /// <summary>Коло світла навколо смолоскипа чи свічок (лише на підлозі, за стіни не заходить).</summary>
    void Glow(int cx, int cy, float radiusCells, Color32 col, float strength)
    {
        int r = Mathf.CeilToInt(radiusCells * C);
        float ox = cx * C + C * 0.5f, oy = cy * C + C * 0.5f;
        for (int y = (int)oy - r; y <= oy + r; y++)
        for (int x = (int)ox - r; x <= ox + r; x++)
        {
            if (x < 0 || y < 0 || x >= tw || y >= th) continue;
            float d = Mathf.Sqrt((x + 0.5f - ox) * (x + 0.5f - ox) + (y + 0.5f - oy) * (y + 0.5f - oy)) / C;
            if (d > radiusCells) continue;
            var cell = new Vector2Int(x / C, y / C);
            if (!grid.IsWalkable(cell, true) && cell != new Vector2Int(cx, cy)) continue;
            if (!grid.HasLineOfSight(new Vector2(cx, -cy), new Vector2(cell.x, -cell.y))) continue;
            float a = strength * (1f - d / radiusCells * 0.7f);
            // піксельний край кола
            if (d > radiusCells - 0.17f) a = strength * 1.6f;
            int i = (th - 1 - y) * tw + x;
            baseLayer[i] = Mix(baseLayer[i], col, a);
        }
    }

    void Set(int x, int y, Color32 c)
    {
        if (x < 0 || y < 0 || x >= tw || y >= th) return;
        baseLayer[(th - 1 - y) * tw + x] = c;
    }

    static Color32 Mix(Color32 o, Color32 c, float a)
    {
        a = Mathf.Clamp01(a);
        return new Color32((byte)(o.r + (c.r - o.r) * a), (byte)(o.g + (c.g - o.g) * a), (byte)(o.b + (c.b - o.b) * a),
            (byte)Mathf.Max(o.a, (int)(255 * a)));
    }

    // ================================================================ щокадру

    void LateUpdate()
    {
        if (tex == null) return;
        time += Time.unscaledDeltaTime;
        System.Array.Copy(baseLayer, buf, buf.Length);
        bool blink = Mathf.Repeat(time, 0.8f) < 0.5f;

        // вихід
        var exit = LevelExit.Instance;
        if (exit != null)
        {
            Vector2Int c = Cell(exit.transform.position);
            bool open = !exit.IsLocked;
            Color32 door = open ? new Color32(90, 240, 130, 255) : new Color32(200, 70, 60, 255);
            if (!open || blink) FillRect(c, 1, 0, 4, 6, door);
            FillRect(c, 2, 1, 2, 4, open ? new Color32(20, 60, 30, 255) : new Color32(70, 30, 26, 255));
            if (!open) Dot(c, 3, 3, new Color32(255, 214, 80, 255));
        }

        foreach (var l in lures)
        {
            if (l == null) continue;
            Vector2Int c = Cell(l.transform.position);
            string n = l.gameObject.name;
            if (l is LeverLure)
            {
                if (l.IsAvailable)
                {
                    Color32 y = blink ? new Color32(255, 220, 60, 255) : new Color32(200, 150, 30, 255);
                    FillRect(c, 1, 4, 4, 2, new Color32(110, 104, 100, 255));
                    FillRect(c, 3, 0, 1, 4, y);
                    FillRect(c, 3, 0, 2, 1, new Color32(255, 80, 60, 255));
                }
                else
                {
                    FillRect(c, 1, 4, 4, 2, new Color32(90, 86, 84, 255));
                    FillRect(c, 1, 3, 3, 1, new Color32(140, 136, 130, 255));
                }
            }
            else if (n.StartsWith("Vase"))
            {
                if (l.IsAvailable) { FillRect(c, 2, 1, 2, 1, new Color32(160, 90, 50, 255)); FillRect(c, 1, 2, 4, 3, new Color32(200, 110, 60, 255)); }
                else { Dot(c, 1, 4, new Color32(170, 96, 56, 255)); Dot(c, 3, 3, new Color32(170, 96, 56, 255)); Dot(c, 4, 4, new Color32(150, 80, 46, 255)); }
            }
            else if (n.StartsWith("TV"))
            {
                FillRect(c, 1, 3, 4, 3, new Color32(120, 80, 46, 255));
                FillRect(c, 2, 0, 3, 2, l.IsActive && blink ? new Color32(255, 240, 150, 255) : new Color32(220, 170, 60, 255));
            }
            else if (n.StartsWith("Phone"))
            {
                int sh = l.IsActive && blink ? 1 : 0;
                FillRect(c, 2 + sh, 0, 2, 1, new Color32(230, 180, 60, 255));
                FillRect(c, 1 + sh, 1, 4, 3, new Color32(230, 180, 60, 255));
                Dot(c, 3, 4, new Color32(160, 110, 40, 255));
            }
            else if (n.StartsWith("Mannequin"))
            {
                FillRect(c, 2, 0, 2, 2, new Color32(180, 184, 196, 255));
                FillRect(c, 1, 2, 4, 3, new Color32(140, 144, 156, 255));
            }
        }

        // вороги: скелет з колом ліхтаря, дух з колом чуття
        foreach (var e in enemies)
        {
            if (e == null || !e.gameObject.activeInHierarchy) continue;
            Vector2 p = e.transform.position;
            float px = (p.x + 0.5f) * C, py = (-p.y + 0.5f) * C;
            bool chase = e.IsChasing;
            if (e.IsSpirit)
            {
                Ring(px, py, e.SenseRange * C, chase ? new Color32(255, 120, 220, 255) : new Color32(180, 120, 255, 255), 0.75f);
                Blob(px, py, chase && blink ? new Color32(255, 160, 255, 255) : new Color32(196, 120, 255, 255));
            }
            else
            {
                Disk(px, py, e.LanternRadius * C, new Color32(255, 170, 80, 255), 0.16f);
                Blob(px, py, chase && blink ? new Color32(255, 255, 255, 255) : new Color32(255, 84, 60, 255));
                Vector2 f = e.Facing;
                Plot(px + f.x * 3.5f, py - f.y * 3.5f, new Color32(255, 200, 160, 255));
            }
        }

        // місячні промені: бліді плями, що повзуть залою
        foreach (var beam in MoonBeam.All)
        {
            if (beam == null || !beam.isActiveAndEnabled) continue;
            Vector2 p = beam.transform.position;
            Disk((p.x + 0.5f) * C, (-p.y + 0.5f) * C, beam.Radius * C, new Color32(170, 205, 255, 255), 0.22f);
        }

        foreach (var trap in BossTrap.All)
        {
            if (trap == null || trap.IsComplete) continue;
            Vector2 p = trap.ImpactPosition;
            Ring((p.x + 0.5f) * C, (-p.y + 0.5f) * C, 1.8f * C,
                trap.IsDropping ? new Color32(255, 80, 40, 255) : new Color32(255, 200, 70, 255), 0.8f);
        }
        var jailer = BossJailer.Instance;
        if (jailer != null && jailer.IsDefeated && jailer.KeyObject != null && jailer.KeyObject.activeInHierarchy)
        {
            Vector2Int keyCell = Cell(jailer.KeyObject.transform.position);
            FillRect(keyCell, 2, 1, 2, 4, new Color32(255, 220, 70, 255));
        }

        // привид: точка і напрям погляду (у 3D)
        if (player != null)
        {
            Vector2 p = player.position;
            float px = (p.x + 0.5f) * C, py = (-p.y + 0.5f) * C;
            var col = new Color32(130, 236, 255, 255);
            if (fpm != null && fpm.isActiveAndEnabled)
            {
                Vector2 f = fpm.Forward;
                for (int i = 2; i <= 6; i++) Plot(px + f.x * i, py - f.y * i, i < 5 ? col : new Color32(130, 236, 255, 140));
            }
            Blob(px, py, col);
        }

        tex.SetPixels32(buf);
        tex.Apply(false);
    }

    static Vector2Int Cell(Vector3 world) => LevelGrid.WorldToCell(world);

    void FillRect(Vector2Int c, int x0, int y0, int w, int h, Color32 col)
    {
        for (int y = y0; y < y0 + h; y++)
        for (int x = x0; x < x0 + w; x++)
            Plot(c.x * C + x, c.y * C + y, col);
    }

    void Dot(Vector2Int c, int x, int y, Color32 col) => Plot(c.x * C + x, c.y * C + y, col);

    void Blob(float px, float py, Color32 col)
    {
        for (int y = -1; y <= 1; y++)
        for (int x = -1; x <= 1; x++)
            if (x == 0 || y == 0) Plot(px + x, py + y, col);
        Plot(px - 1, py - 1, new Color32((byte)(col.r / 2), (byte)(col.g / 2), (byte)(col.b / 2), 255));
    }

    void Disk(float px, float py, float r, Color32 col, float a)
    {
        int ri = Mathf.CeilToInt(r);
        for (int y = -ri; y <= ri; y++)
        for (int x = -ri; x <= ri; x++)
            if (x * x + y * y <= r * r) Blend(px + x, py + y, col, a);
    }

    void Ring(float px, float py, float r, Color32 col, float a)
    {
        int n = Mathf.Max(12, (int)(r * 6.3f / 2f));
        float spin = time * 0.6f;
        for (int i = 0; i < n; i++)
        {
            if ((i & 1) == 1) continue;   // пунктир
            float ang = spin + i * Mathf.PI * 2f / n;
            Blend(px + Mathf.Cos(ang) * r, py + Mathf.Sin(ang) * r, col, a);
        }
    }

    void Plot(float x, float y, Color32 c) => Blend(x, y, c, 1f);

    void Blend(float fx, float fy, Color32 c, float a)
    {
        int x = Mathf.FloorToInt(fx), y = Mathf.FloorToInt(fy);
        if (x < 0 || y < 0 || x >= tw || y >= th) return;
        int i = (th - 1 - y) * tw + x;
        buf[i] = Mix(buf[i], c, a);
    }
}

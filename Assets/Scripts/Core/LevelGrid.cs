using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Карта рівня у вигляді сітки символів (та сама, що в Assets/Levels/*.txt).
/// Клітинка (x, y) лежить у світі в точці (x, -y). Сторожі ходять по ній пошуком шляху
/// і перевіряють, чи не заступає їм огляд стіна або полиця.
/// </summary>
public class LevelGrid : MonoBehaviour
{
    public static LevelGrid Instance { get; private set; }

    // Символи, по яких може ходити сторож
    const string WalkableChars = ".PEm123456789";
    // Символи, крізь які не видно
    const string BlocksViewChars = "#SsCp ";

    [SerializeField] string[] rows = new string[0];

    public int Width => rows.Length > 0 ? rows[0].Length : 0;
    public int Height => rows.Length;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SetRows(string[] newRows) => rows = newRows;

    public static Vector2Int WorldToCell(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(-p.y));
    public static Vector2 CellToWorld(Vector2Int c) => new Vector2(c.x, -c.y);

    public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.y < Height && c.x < Width;
    public char Get(Vector2Int c) => InBounds(c) ? rows[c.y][c.x] : ' ';
    public bool IsWalkable(Vector2Int c) => InBounds(c) && WalkableChars.IndexOf(rows[c.y][c.x]) >= 0;
    public bool BlocksView(Vector2Int c) => !InBounds(c) || BlocksViewChars.IndexOf(rows[c.y][c.x]) >= 0;

    /// <summary>Чи нічого не заступає погляд між двома точками.</summary>
    public bool HasLineOfSight(Vector2 from, Vector2 to)
    {
        Vector2Int a = WorldToCell(from), b = WorldToCell(to);
        int steps = Mathf.CeilToInt(Vector2.Distance(from, to) / 0.2f);
        for (int i = 1; i < steps; i++)
        {
            Vector2Int c = WorldToCell(Vector2.Lerp(from, to, i / (float)steps));
            if (c == a || c == b) continue;
            if (BlocksView(c)) return false;
        }
        return true;
    }

    /// <summary>Найближча клітинка, по якій можна ходити.</summary>
    public Vector2Int NearestWalkable(Vector2Int c)
    {
        if (IsWalkable(c)) return c;
        for (int r = 1; r <= 6; r++)
        {
            Vector2Int best = c;
            float bestD = float.MaxValue;
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                var n = new Vector2Int(c.x + dx, c.y + dy);
                if (!IsWalkable(n)) continue;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = n; }
            }
            if (bestD < float.MaxValue) return best;
        }
        return c;
    }

    static readonly Vector2Int[] Dirs =
    {
        new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
        new Vector2Int(1, 1), new Vector2Int(1, -1), new Vector2Int(-1, 1), new Vector2Int(-1, -1),
    };

    /// <summary>Шлях від from до to (пошук у ширину). Точки шляху записуються в result.</summary>
    public bool FindPath(Vector2 from, Vector2 to, List<Vector2> result)
    {
        result.Clear();
        if (Width == 0) return false;

        Vector2Int start = NearestWalkable(WorldToCell(from));
        Vector2Int goal = NearestWalkable(WorldToCell(to));
        if (start == goal)
        {
            result.Add(CellToWorld(goal));
            return true;
        }

        int w = Width, h = Height;
        var parent = new int[w * h];
        for (int i = 0; i < parent.Length; i++) parent[i] = -1;
        int startIdx = start.y * w + start.x;
        parent[startIdx] = startIdx;

        var queue = new Queue<Vector2Int>();
        queue.Enqueue(start);
        bool found = false;

        while (queue.Count > 0)
        {
            Vector2Int c = queue.Dequeue();
            if (c == goal) { found = true; break; }

            foreach (var d in Dirs)
            {
                var n = c + d;
                if (!IsWalkable(n)) continue;
                // не зрізаємо кути по діагоналі
                if (d.x != 0 && d.y != 0 &&
                    (!IsWalkable(new Vector2Int(c.x + d.x, c.y)) || !IsWalkable(new Vector2Int(c.x, c.y + d.y))))
                    continue;
                int idx = n.y * w + n.x;
                if (parent[idx] != -1) continue;
                parent[idx] = c.y * w + c.x;
                queue.Enqueue(n);
            }
        }

        if (!found) return false;

        int cur = goal.y * w + goal.x;
        while (cur != startIdx)
        {
            result.Add(CellToWorld(new Vector2Int(cur % w, cur / w)));
            cur = parent[cur];
        }
        result.Reverse();
        return true;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ланцюг скидає кам'яний вантаж на позначку. Промах не страшний: вантаж піднімається назад.
/// Влучання по Ключнику розбиває камінь, тож кожен вантаж б'є лише раз.
/// </summary>
public class BossTrap : Interactable
{
    /// <summary>Перший ланцюг на рівні (для старого коду).</summary>
    public static BossTrap Instance => All.Count > 0 ? All[0] : null;
    /// <summary>Усі ланцюги рівня.</summary>
    public static readonly List<BossTrap> All = new List<BossTrap>();
    [SerializeField] BossJailer boss;
    [SerializeField] Vector2 impactPosition;
    [SerializeField] SpriteRenderer stone;
    [SerializeField] SpriteRenderer marker;
    [SerializeField] AudioClip chainClip, impactClip;
    const float Radius = 1.8f;
    const float HangingHeight = 1.6f;
    bool operating, spent;
    public Vector2 ImpactPosition => impactPosition;
    public SpriteRenderer Stone => stone;
    public float StoneHeight { get; private set; } = HangingHeight;
    public bool IsDropping { get; private set; }
    /// <summary>Вантаж уже влучив (розбився) або Ключника вже немає.</summary>
    public bool IsComplete => spent || (boss != null && boss.IsDefeated);
    public bool BossInZone => boss != null && !boss.IsDefeated
        && Vector2.Distance(boss.transform.position, impactPosition) <= Radius;
    public override bool IsAvailable => base.IsAvailable && !operating && !IsComplete;
    public override string Prompt => BossInZone ? "Ключник під вантажем - тягни ланцюг!" : "Потягти ланцюг вантажу";

    protected override void Awake()
    {
        base.Awake();
        if (!All.Contains(this)) All.Add(this);
        if (marker != null)
        {
            marker.sprite = MakeMarker();
            marker.transform.localScale = Vector3.one * (Radius * 2f);
        }
        UpdateStone();
    }

    void OnDestroy()
    {
        All.Remove(this);
    }

    protected override void OnInteract(PlayerInteractor who)
    {
        if (operating || boss == null || boss.IsDefeated) return;
        StartCoroutine(Drop(who));
    }

    bool LevelEnded => GameManager.Instance != null && GameManager.Instance.IsLevelOver;

    IEnumerator Drop(PlayerInteractor who)
    {
        operating = true;
        IsDropping = true;
        GameAudio.Play(chainClip, 0.8f);
        LevelExit.Announce("Ланцюг відпущено! Відійди від позначки!");
        for (float t = 0f; t < 0.85f; t += Time.deltaTime)
        {
            if (LevelEnded) { ResetStone(); yield break; }
            if (marker != null) marker.color = new Color(1f, 0.25f, 0.05f, 0.5f + 0.25f * Mathf.Sin(t * 28f));
            yield return null;
        }
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            if (LevelEnded) { ResetStone(); yield break; }
            StoneHeight = Mathf.Lerp(HangingHeight, 0f, Mathf.Clamp01(t / 0.3f));
            UpdateStone();
            yield return null;
        }
        StoneHeight = 0f;
        UpdateStone();
        GameAudio.Play(impactClip);
        LureEvents.Raise(new LureSignal(impactPosition, 14f, 0f, 0.15f, gameObject));
        if (who != null && Vector2.Distance(who.transform.position, impactPosition) < 1.25f)
        {
            if (GameManager.Instance != null) GameManager.Instance.GameOver();
            ResetStone();
            yield break;
        }
        if (BossInZone)
        {
            spent = true;
            boss.Hit();
            if (stone != null) stone.enabled = false;
            if (marker != null) marker.enabled = false;
            IsDropping = false;
            operating = false;
            yield break;
        }
        LevelExit.Announce("Промах! Удар у дзвін під вантажем, щоб Ключник прийшов, і тягни знову.");
        yield return new WaitForSeconds(0.6f);
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            if (LevelEnded) { ResetStone(); yield break; }
            StoneHeight = Mathf.Lerp(0f, HangingHeight, Mathf.Clamp01(t / 0.8f));
            UpdateStone();
            yield return null;
        }
        ResetStone();
    }

    void ResetStone()
    {
        StoneHeight = HangingHeight;
        UpdateStone();
        IsDropping = false;
        operating = false;
        if (marker != null) marker.color = new Color(1f, 0.65f, 0.1f, 0.4f);
    }

    void UpdateStone()
    {
        if (stone != null) stone.transform.position = (Vector3)impactPosition + Vector3.up * StoneHeight;
    }

    static Sprite markerSprite;
    static Sprite MakeMarker()
    {
        if (markerSprite != null) return markerSprite;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(16f, 16f));
            pixels[y * 32 + x] = new Color32(255, 255, 255, distance > 15.5f ? (byte)0 : distance > 13.5f ? (byte)255 : (byte)45);
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        markerSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32);
        return markerSprite;
    }

    public float FloorMark(float worldX, float worldY)
    {
        if (IsComplete) return 0f;
        float dx = worldX - impactPosition.x, dy = worldY - impactPosition.y;
        float distanceSquared = dx * dx + dy * dy;
        if (distanceSquared > Radius * Radius) return 0f;
        return IsDropping ? 0.5f + 0.15f * Mathf.Sin(Time.time * 20f)
            : distanceSquared > (Radius - 0.12f) * (Radius - 0.12f) ? 0.65f : 0.12f;
    }
}

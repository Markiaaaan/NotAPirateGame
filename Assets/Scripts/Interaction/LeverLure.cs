using UnityEngine;

/// <summary>
/// Важіль (символ X на карті). Відмикає вихід і привертає лише ворогів поблизу.
/// </summary>
public class LeverLure : LureObject
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite downSprite;

    protected override void OnInteract(PlayerInteractor who)
    {
        base.OnInteract(who);   // шум + звук, як у будь-якої приманки
        if (body != null && downSprite != null) body.sprite = downSprite;
        if (LevelExit.Instance != null) LevelExit.Instance.LeverPulled();
    }
}

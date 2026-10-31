using UnityEngine;

/// <summary>
/// Важіль (символ X на карті). Відмикає вихід одразу, без ключа, але гуркоче на все підземелля:
/// це ще й приманка, тож скелети й духи збігаються до важеля. Можна цим скористатися:
/// потягнути й тихо прослизнути до виходу, поки всі біжать сюди.
/// </summary>
public class LeverLure : LureObject
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite downSprite;

    protected override void OnInteract(PlayerInteractor who)
    {
        base.OnInteract(who);   // шум + звук, як у будь-якої приманки
        if (body != null && downSprite != null) body.sprite = downSprite;
        if (LevelExit.Instance != null)
            LevelExit.Instance.Unlock("Важіль спрацював! Вихід відчинено... але нежить усе чула");
    }
}

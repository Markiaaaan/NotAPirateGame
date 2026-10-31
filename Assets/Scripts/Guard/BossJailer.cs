using UnityEngine;

/// <summary>Ключник: великий скелет, якого можна здолати лише кам'яним вантажем.</summary>
[RequireComponent(typeof(GuardAI))]
public class BossJailer : MonoBehaviour
{
    public static BossJailer Instance { get; private set; }
    [SerializeField] GameObject droppedKey;
    public bool IsDefeated { get; private set; }
    public GameObject KeyObject => droppedKey;
    void OnEnable() => LureEvents.OnLure += HearBait;
    void OnDisable() => LureEvents.OnLure -= HearBait;
    void HearBait(LureSignal signal)
    {
        var guard = GetComponent<GuardAI>();
        if (!IsDefeated && signal.Source != null && signal.Source.GetComponent<LureObject>() != null)
            guard.InvestigateNoise(signal, true, 5f);
    }

    void Awake()
    {
        Instance = this;
        if (droppedKey != null) droppedKey.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Defeat()
    {
        if (IsDefeated) return;
        IsDefeated = true;
        GetComponent<GuardAI>().Retire();
        if (droppedKey != null)
        {
            droppedKey.transform.position = transform.position;
            droppedKey.SetActive(true);
        }
        LevelExit.Announce("Ключник розсипався! Забери його ключ.");
        gameObject.SetActive(false);
    }
}

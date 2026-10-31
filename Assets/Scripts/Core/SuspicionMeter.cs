using System;
using UnityEngine;

/// <summary>
/// Загальна підозра нежиті (0..1). Росте від приманок і від того, що привида помітили,
/// повільно спадає. Коли доходить до 1, вмикається тривога: скелети й духи швидші й пильніші.
/// </summary>
public class SuspicionMeter : MonoBehaviour
{
    public static SuspicionMeter Instance { get; private set; }

    [SerializeField] float decayPerSecond = 0.03f;
    [SerializeField] float alertDuration = 8f;
    [Tooltip("Яка підозра залишається після кінця тривоги")]
    [SerializeField] float afterAlertValue = 0.6f;

    float alertTimer;

    public float Value { get; private set; }
    public bool IsAlert => alertTimer > 0f;

    public event Action OnAlert;

    /// <summary>Поточна підозра, або 0, якщо лічильника немає на сцені.</summary>
    public static float Level => Instance != null ? Instance.Value : 0f;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable() => LureEvents.OnLure += HandleLure;
    void OnDisable() => LureEvents.OnLure -= HandleLure;

    void HandleLure(LureSignal s) => Add(s.Suspicion);

    public void Add(float amount)
    {
        Value = Mathf.Clamp01(Value + amount);
        if (Value >= 1f && alertTimer <= 0f)
        {
            alertTimer = alertDuration;
            OnAlert?.Invoke();
        }
    }

    void Update()
    {
        if (alertTimer > 0f)
        {
            alertTimer -= Time.deltaTime;
            if (alertTimer <= 0f) Value = afterAlertValue;
            return;
        }
        Value = Mathf.Max(0f, Value - decayPerSecond * Time.deltaTime);
    }
}

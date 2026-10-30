using System;
using UnityEngine;

/// <summary>
/// Енергія привида (ектоплазма). Витрачається на приманки, повільно відновлюється.
/// UI підписується на OnChanged і оновлює смужку.
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    [SerializeField] float maxEnergy = 100f;
    [SerializeField] float regenPerSecond = 6f;
    [Tooltip("Скільки секунд після витрати енергія не відновлюється")]
    [SerializeField] float regenDelay = 1.5f;

    float lastSpendTime = -999f;

    public float Max => maxEnergy;
    public float Current { get; private set; }
    public float Normalized => maxEnergy <= 0f ? 0f : Current / maxEnergy;

    /// <summary>(поточна, максимальна)</summary>
    public event Action<float, float> OnChanged;

    void Awake()
    {
        Current = maxEnergy;
    }

    void Start()
    {
        OnChanged?.Invoke(Current, maxEnergy);
    }

    void Update()
    {
        if (Current >= maxEnergy) return;
        if (Time.time - lastSpendTime < regenDelay) return;

        Current = Mathf.Min(maxEnergy, Current + regenPerSecond * Time.deltaTime);
        OnChanged?.Invoke(Current, maxEnergy);
    }

    public bool CanAfford(float amount) => Current >= amount;

    public bool TrySpend(float amount)
    {
        if (amount > Current) return false;

        Current -= amount;
        lastSpendTime = Time.time;
        OnChanged?.Invoke(Current, maxEnergy);
        return true;
    }

    public void Restore(float amount)
    {
        Current = Mathf.Min(maxEnergy, Current + amount);
        OnChanged?.Invoke(Current, maxEnergy);
    }
}

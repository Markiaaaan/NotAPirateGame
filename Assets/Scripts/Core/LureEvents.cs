using System;
using UnityEngine;

/// <summary>
/// "Контракт" між гравцем і охоронцем. Приманка створює LureSignal, охоронець його слухає.
/// Обидва програмісти можуть працювати незалежно, поки цей файл не змінюється.
/// </summary>
public readonly struct LureSignal
{
    /// <summary>Де сталася подія.</summary>
    public readonly Vector2 Position;
    /// <summary>Радіус, у якому охоронець це чує.</summary>
    public readonly float Loudness;
    /// <summary>Скільки секунд приманка "діє". 0 = разова (шум).</summary>
    public readonly float Duration;
    /// <summary>Наскільки це підвищує підозру (0..1).</summary>
    public readonly float Suspicion;
    /// <summary>Хто створив сигнал.</summary>
    public readonly GameObject Source;

    public LureSignal(Vector2 position, float loudness, float duration, float suspicion, GameObject source)
    {
        Position = position;
        Loudness = loudness;
        Duration = duration;
        Suspicion = suspicion;
        Source = source;
    }
}

public static class LureEvents
{
    /// <summary>
    /// Охоронець підписується в OnEnable і ОБОВ'ЯЗКОВО відписується в OnDisable:
    ///   LureEvents.OnLure += HandleLure;   /   LureEvents.OnLure -= HandleLure;
    /// У HandleLure перевіряє відстань: Vector2.Distance(signal.Position, transform.position) &lt;= signal.Loudness
    /// </summary>
    public static event Action<LureSignal> OnLure;

    public static void Raise(LureSignal signal) => OnLure?.Invoke(signal);
}

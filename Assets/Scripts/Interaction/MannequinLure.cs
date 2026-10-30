using System.Collections;
using UnityEngine;

/// <summary>
/// Манекен, який привид переставляє в інше місце. Окрім звичайного шуму приманки,
/// охоронець може сам помітити, що манекен стоїть не там (IsDisplaced), і занепокоїтися.
/// Задай targetPoint: порожній дочірній об'єкт там, куди манекен має переміститися.
/// </summary>
public class MannequinLure : LureObject
{
    [Header("Манекен")]
    [SerializeField] Transform targetPoint;
    [SerializeField] float moveTime = 0.4f;

    Vector3 originalPosition;
    Coroutine moveRoutine;

    /// <summary>True, якщо манекен зсунули. Охоронець може на це реагувати.</summary>
    public bool IsDisplaced { get; private set; }

    protected override void Awake()
    {
        base.Awake();
        originalPosition = transform.position;
    }

    protected override void OnInteract(PlayerInteractor who)
    {
        base.OnInteract(who);

        if (targetPoint != null)
        {
            IsDisplaced = true;
            if (moveRoutine != null) StopCoroutine(moveRoutine);
            moveRoutine = StartCoroutine(MoveRoutine(targetPoint.position));
        }
    }

    IEnumerator MoveRoutine(Vector3 destination)
    {
        Vector3 start = transform.position;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, moveTime);
            transform.position = Vector3.Lerp(start, destination, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        transform.position = destination;
    }

    /// <summary>Повернути манекен на місце (наприклад, при перезапуску рівня).</summary>
    public void ResetPosition()
    {
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        transform.position = originalPosition;
        IsDisplaced = false;
    }
}

using UnityEngine;

/// <summary>
/// Вихід із рівня. Постав об'єкт із Collider2D (галочка Is Trigger) там, де двері.
/// Коли привид у нього залітає, рівень пройдено.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelExit : MonoBehaviour
{
    // Викликається, коли додаєш компонент: одразу робить колайдер тригером.
    void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerMovement>() == null) return;
        if (GameManager.Instance != null) GameManager.Instance.CompleteLevel();
    }
}

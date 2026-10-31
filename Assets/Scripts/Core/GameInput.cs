using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Єдине місце, де читається ввід. Працює і зі старим Input Manager, і з новим Input System.
/// WASD / стрілки - рух, E - взаємодія, Enter/Space - старт, R - заново, Esc - меню.
/// Усі ...Pressed повертають true лише в кадрі натискання, тож читати їх треба з Update().
/// </summary>
public static class GameInput
{
    public static Vector2 Move
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed || k.rightArrowKey.isPressed ? 1f : 0f)
                    - (k.aKey.isPressed || k.leftArrowKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed || k.upArrowKey.isPressed ? 1f : 0f)
                    - (k.sKey.isPressed || k.downArrowKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
            return Vector2.ClampMagnitude(
                new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
#endif
        }
    }

    public static bool InteractPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
        }
    }

    public static bool StartPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            return k != null && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }

    public static bool RestartPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.R);
#endif
        }
    }

    public static bool BackPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }
    }

    /// <summary>Рух лише на WASD (для вигляду від першої особи, де стрілки повертають).</summary>
    public static Vector2 MoveWASD
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return Vector2.zero;
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#else
            float x = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float y = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
#endif
        }
    }

    /// <summary>Поворот стрілками: -1 вліво, +1 вправо.</summary>
    public static float TurnAxis
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var k = Keyboard.current;
            if (k == null) return 0f;
            return (k.rightArrowKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed ? 1f : 0f);
#else
            return (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
#endif
        }
    }

    /// <summary>Рух миші по горизонталі за кадр (у пікселях).</summary>
    public static float MouseDeltaX
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.delta.ReadValue().x : 0f;
#else
            return Input.GetAxisRaw("Mouse X") * 10f;
#endif
        }
    }

    /// <summary>T у меню: перемкнути вигляд 3D / 2D.</summary>
    public static bool ToggleViewPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.T);
#endif
        }
    }

    // ---- для меню ----
#if ENABLE_INPUT_SYSTEM
    static bool Down(Key a, Key b)
    {
        var k = Keyboard.current;
        return k != null && (k[a].wasPressedThisFrame || k[b].wasPressedThisFrame);
    }
    public static bool UpPressed => Down(Key.W, Key.UpArrow);
    public static bool DownPressed => Down(Key.S, Key.DownArrow);
    public static bool LeftPressed => Down(Key.A, Key.LeftArrow);
    public static bool RightPressed => Down(Key.D, Key.RightArrow);
#else
    public static bool UpPressed => Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow);
    public static bool DownPressed => Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow);
    public static bool LeftPressed => Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow);
    public static bool RightPressed => Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow);
#endif

    /// <summary>Позиція курсора в пікселях екрана.</summary>
    public static Vector2 MousePosition
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
            return Input.mousePosition;
#endif
        }
    }

    /// <summary>Ліва кнопка миші натиснута в цьому кадрі.</summary>
    public static bool MouseClicked
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }

    /// <summary>Колесо миші: +1 вгору, -1 вниз, 0 - не крутили.</summary>
    public static int MouseScroll
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            float y = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
#else
            float y = Input.mouseScrollDelta.y;
#endif
            return y > 0.01f ? 1 : y < -0.01f ? -1 : 0;
        }
    }

    /// <summary>Тестова клавіша K: "тебе спіймали". Лише для налагодження.</summary>
    public static bool DebugCaughtPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.K);
#endif
        }
    }
}

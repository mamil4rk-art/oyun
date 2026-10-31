using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Tüm tuş okumaları buradan geçer. Hem Unity'nin yeni Input System'i
/// hem de eski Input Manager ile çalışır, proje ayarı değiştirmen gerekmez.
/// Etkileşim tuşları hem fiziksel konuma hem de klavye düzenindeki harfe bakar
/// (Türkçe Q / F klavyelerde de doğru çalışsın diye).
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
            float x = (k.dKey.isPressed ? 1f : 0f) - (k.aKey.isPressed ? 1f : 0f);
            float y = (k.wKey.isPressed ? 1f : 0f) - (k.sKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
#else
            return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
        }
    }

    public static Vector2 Look
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            var m = Mouse.current;
            return m == null ? Vector2.zero : m.delta.ReadValue() * 0.1f;
#else
            return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
        }
    }

    public static bool Run
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
            return Input.GetKey(KeyCode.LeftShift);
#endif
        }
    }

    public static bool JumpPressed
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Space);
#endif
        }
    }

    /// [Space] basılı — arabada el freni
    public static bool HandbrakeHeld
    {
        get
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.isPressed;
#else
            return Input.GetKey(KeyCode.Space);
#endif
        }
    }

    /// [E] — Etkileşim (al, yerleştir, kullan). Sol tık ayrıca PlayerInteractor'da kontrol edilir.
    public static bool InteractPressed => KeyPressed('e');

    /// [G] — Elindekini yere bırak. Sağ tık ayrıca PlayerInteractor'da kontrol edilir.
    public static bool DropPressed => KeyPressed('g');

    /// [Esc] — Fareyi serbest bırak / menü
    public static bool CancelPressed
    {
        get
        {
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) pressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Escape)) pressed = true;
#endif
            return pressed;
        }
    }

    public static bool PrimaryClickPressed
    {
        get
        {
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) pressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(0)) pressed = true;
#endif
            return pressed;
        }
    }

    public static bool SecondaryClickPressed
    {
        get
        {
            bool pressed = false;
#if ENABLE_INPUT_SYSTEM
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame) pressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetMouseButtonDown(1)) pressed = true;
#endif
            return pressed;
        }
    }

    /// Bir harf tuşuna bu karede basıldı mı? Fiziksel konum + klavye düzeni + eski sistem.
    static bool KeyPressed(char letter)
    {
        bool pressed = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            var physical = letter == 'e' ? k.eKey : letter == 'g' ? k.gKey : null;
            if (physical != null && physical.wasPressedThisFrame) pressed = true;

            var byLayout = k.FindKeyOnCurrentKeyboardLayout(letter.ToString());
            if (byLayout != null && byLayout.wasPressedThisFrame) pressed = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (letter == 'e' && Input.GetKeyDown(KeyCode.E)) pressed = true;
        if (letter == 'g' && Input.GetKeyDown(KeyCode.G)) pressed = true;
#endif
        return pressed;
    }
}

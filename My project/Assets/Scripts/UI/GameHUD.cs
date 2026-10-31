using UnityEngine;

/// <summary>
/// GEÇİCİ prototip arayüzü: nişangâh, etkileşim ipucu, para ve bildirimler.
/// Hiçbir kurulum gerektirmez. Oyun ilerleyince yerini düzgün bir Canvas arayüzü alacak.
/// </summary>
public class GameHUD : MonoBehaviour
{
    static GameHUD instance;

    string message = "";
    float messageUntil;

    PlayerInteractor interactor;
    GUIStyle promptStyle, moneyStyle, msgStyle, hintStyle, crossStyle;

    void Awake() => instance = this;

    void Start() => interactor = FindAnyObjectByType<PlayerInteractor>();

    public static void ShowMessage(string text, float seconds = 2.5f)
    {
        if (instance == null) return;
        instance.message = text;
        instance.messageUntil = Time.time + seconds;
    }

    void BuildStyles()
    {
        float s = Screen.height / 1080f;
        promptStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * s), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        moneyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(30 * s), fontStyle = FontStyle.Bold };
        msgStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(24 * s) };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(18 * s) };
        crossStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(28 * s), alignment = TextAnchor.MiddleCenter };
    }

    void OnGUI()
    {
        if (ShopComputer.IsOpen) return; // bilgisayar ekranı kendi arayüzünü çiziyor
        if (promptStyle == null || Event.current.type == EventType.Layout) BuildStyles();

        float w = Screen.width, h = Screen.height;

        // Nişangâh
        GUI.Label(new Rect(w / 2 - 20, h / 2 - 20, 40, 40), "•", crossStyle);

        // Etkileşim ipucu
        if (interactor != null && !string.IsNullOrEmpty(interactor.CurrentPrompt))
            ShadowLabel(new Rect(0, h / 2 + 30, w, 50), interactor.CurrentPrompt, promptStyle);

        // Para
        if (MoneyManager.Instance != null)
            ShadowLabel(new Rect(25, 20, 500, 50), "Kasa: " + MoneyManager.Format(MoneyManager.Instance.Money), moneyStyle);

        // Bildirim
        if (Time.time < messageUntil)
            ShadowLabel(new Rect(25, 70, 1200, 40), message, msgStyle);

        // Alışveriş arabası durumu
        var cart = interactor != null ? interactor.PushedCart : null;
        if (cart != null)
            ShadowLabel(new Rect(25, 110, 1200, 40),
                $"Araba: {cart.Count}/{cart.Capacity} ürün   Ödenecek: {MoneyManager.Format(cart.UnpaidTotal)}", msgStyle);

        // Sürüş
        var car = CarController.Current;
        if (car != null)
        {
            ShadowLabel(new Rect(w - 260, h - 110, 240, 60), $"{Mathf.Abs(car.SpeedKmh):0} km/s", moneyStyle);
            ShadowLabel(new Rect(25, h - 40, w, 30),
                "W: Gaz   S: Fren / Geri   A-D: Direksiyon   Space: El freni   Fare: Etrafa bak   E: Arabadan in", hintStyle);
            return;
        }

        // Kontroller
        ShadowLabel(new Rect(25, h - 40, w, 30),
            "WASD: Yürü   Shift: Koş   E / Sol tık: Al - Yerleştir - Arabayı it - Arabaya bin   G / Sağ tık: Bırak   Esc: Fareyi bırak", hintStyle);
    }

    static void ShadowLabel(Rect r, string text, GUIStyle style)
    {
        var old = style.normal.textColor;
        style.normal.textColor = new Color(0, 0, 0, 0.75f);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, style);
        style.normal.textColor = Color.white;
        GUI.Label(r, text, style);
        style.normal.textColor = old;
    }
}

using System;
using UnityEngine;

/// <summary>
/// Tamirhanedeki dükkân bilgisayarı (satış alanıyla arasındaki bölme duvarında). [E] ile kullanılır; kurgusal "Pencere OS" masaüstü açılır.
/// Uygulamalar: Dükkânım (seviye 2 hedefi), Faturalar, Hesap Makinesi, Not Defteri,
/// Online Alışveriş ve Güvenlik Kamerası (yakında).
/// Esc: önce açık menüyü / uygulamayı, sonra bilgisayarı kapatır.
/// GEÇİCİ: OnGUI ile çiziliyor; ileride Canvas / UI Toolkit'e taşınacak.
/// </summary>
public class ShopComputer : MonoBehaviour, IInteractable
{
    /// Bilgisayar ekranı açık mı? (HUD kendini gizlesin diye)
    public static bool IsOpen { get; private set; }

    [Header("Dükkân seviyesi")]
    public int shopLevel = 1;
    public int level2Cost = 250000;

    enum App { None, Shop, Bills, Market, Camera, Calc, Notes }

    class Bill
    {
        public readonly string name;
        public readonly int amount;
        public bool paid;
        public Bill(string n, int a) { name = n; amount = a; }
    }

    readonly Bill[] bills =
    {
        new Bill("Dükkân kirası", 7500),
        new Bill("Elektrik", 1850),
        new Bill("Su", 420),
        new Bill("İnternet", 650),
    };

    struct AppInfo
    {
        public App app;
        public string title, letter;
        public Color color;
        public AppInfo(App a, string t, string l, Color c) { app = a; title = t; letter = l; color = c; }
    }

    static readonly AppInfo[] Apps =
    {
        new AppInfo(App.Shop, "Dükkânım", "D", new Color(0.95f, 0.5f, 0.1f)),
        new AppInfo(App.Bills, "Faturalar", "F", new Color(0.85f, 0.25f, 0.25f)),
        new AppInfo(App.Market, "Online Alışveriş", "A", new Color(0.2f, 0.6f, 0.3f)),
        new AppInfo(App.Camera, "Güvenlik Kamerası", "K", new Color(0.35f, 0.35f, 0.45f)),
        new AppInfo(App.Calc, "Hesap Makinesi", "=", new Color(0.25f, 0.45f, 0.8f)),
        new AppInfo(App.Notes, "Not Defteri", "N", new Color(0.85f, 0.75f, 0.2f)),
    };

    FirstPersonController fpc;
    bool open, startMenu;
    App app = App.None;
    string appMessage = "";
    string notes = "Yapılacaklar:\n- Toptancıdan parça al\n- Bilgisayar topla ve rafa koy\n- Seviye 2 dükkân için para biriktir";

    // Hesap makinesi
    string calcDisplay = "0";
    double calcAcc;
    char calcOp;
    bool calcFresh = true;

    // Görünüm
    float s = 1f;
    GUIStyle sTitle, sText, sSmall, sIconLetter, sIconLabel, sButton, sWinTitle, sClose, sTask, sCalc, sWatermark, sArea;
    Texture2D texWallpaper, texWhite;

    // ---------- IInteractable ----------

    public bool CanInteract(PlayerInteractor p) => !open && !p.HandsBusy;

    public string GetPrompt(PlayerInteractor p) => "[E] Dükkân bilgisayarını kullan";

    public void Interact(PlayerInteractor p)
    {
        fpc = p.GetComponent<FirstPersonController>();
        open = true;
        IsOpen = true;
        app = App.None;
        startMenu = false;
        appMessage = "";
        if (fpc != null) fpc.SetInputLocked(true);
    }

    void Close()
    {
        open = false;
        IsOpen = false;
        startMenu = false;
        if (fpc != null) fpc.SetInputLocked(false);
    }

    void OnDisable()
    {
        if (open) Close();
    }

    void Update()
    {
        if (!open || !GameInput.CancelPressed) return;
        if (startMenu) startMenu = false;
        else if (app != App.None) app = App.None;
        else Close();
    }

    // ---------- Çizim ----------

    void OnGUI()
    {
        if (!open) return;
        GUI.depth = -100;
        EnsureStyles();

        float w = Screen.width, h = Screen.height;
        GUI.DrawTexture(new Rect(0, 0, w, h), texWallpaper, ScaleMode.StretchToFill);
        GUI.Label(new Rect(0, h * 0.42f, w, 120 * s), "Pencere OS", sWatermark);

        // Masaüstü simgeleri
        for (int i = 0; i < Apps.Length; i++)
        {
            var a = Apps[i];
            var r = new Rect(24 * s, 24 * s + i * 112 * s, 110 * s, 104 * s);
            var box = new Rect(r.x + (r.width - 64 * s) / 2f, r.y, 64 * s, 64 * s);
            Fill(box, a.color);
            GUI.Label(box, a.letter, sIconLetter);
            GUI.Label(new Rect(r.x - 10 * s, r.y + 66 * s, r.width + 20 * s, 36 * s), a.title, sIconLabel);
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) OpenApp(a.app);
        }

        if (app != App.None) DrawWindow(w, h);
        DrawTaskbar(w, h);
    }

    void OpenApp(App a)
    {
        app = a;
        startMenu = false;
        appMessage = "";
    }

    void DrawWindow(float w, float h)
    {
        var win = new Rect(w * 0.2f, h * 0.08f, w * 0.6f, h * 0.76f);
        Fill(new Rect(win.x + 6 * s, win.y + 6 * s, win.width, win.height), new Color(0f, 0f, 0f, 0.35f)); // gölge
        Fill(win, new Color(0.96f, 0.96f, 0.97f));
        var bar = new Rect(win.x, win.y, win.width, 44 * s);
        Fill(bar, new Color(0.13f, 0.17f, 0.24f));
        GUI.Label(new Rect(bar.x + 16 * s, bar.y, bar.width, bar.height), Title(app), sWinTitle);
        if (GUI.Button(new Rect(bar.xMax - 52 * s, bar.y, 52 * s, bar.height), "X", sClose)) app = App.None;

        var content = new Rect(win.x + 28 * s, bar.yMax + 20 * s, win.width - 56 * s, win.height - bar.height - 40 * s);
        GUILayout.BeginArea(content);
        switch (app)
        {
            case App.Shop: ShopApp(content.width); break;
            case App.Bills: BillsApp(); break;
            case App.Market: MarketApp(); break;
            case App.Camera: CameraApp(); break;
            case App.Calc: CalcApp(content.width); break;
            case App.Notes: NotesApp(); break;
        }
        if (!string.IsNullOrEmpty(appMessage))
        {
            GUILayout.Space(10 * s);
            GUILayout.Label(appMessage, sText);
        }
        GUILayout.EndArea();
    }

    void DrawTaskbar(float w, float h)
    {
        var bar = new Rect(0, h - 52 * s, w, 52 * s);
        Fill(bar, new Color(0.08f, 0.1f, 0.14f, 0.96f));
        if (GUI.Button(new Rect(8 * s, bar.y + 6 * s, 150 * s, 40 * s), "◆  Başlat", sButton)) startMenu = !startMenu;

        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        GUI.Label(new Rect(w - 520 * s, bar.y, 330 * s, bar.height), "Kasa: " + MoneyManager.Format(money), sTask);
        GUI.Label(new Rect(w - 170 * s, bar.y, 150 * s, bar.height), DateTime.Now.ToString("HH:mm"), sTask);

        if (!startMenu) return;
        var menu = new Rect(8 * s, bar.y - (Apps.Length * 46 + 70) * s, 320 * s, (Apps.Length * 46 + 64) * s);
        Fill(menu, new Color(0.12f, 0.15f, 0.2f, 0.98f));
        for (int i = 0; i < Apps.Length; i++)
            if (GUI.Button(new Rect(menu.x + 8 * s, menu.y + 8 * s + i * 46 * s, menu.width - 16 * s, 40 * s), Apps[i].title, sButton))
                OpenApp(Apps[i].app);
        if (GUI.Button(new Rect(menu.x + 8 * s, menu.yMax - 50 * s, menu.width - 16 * s, 42 * s), "Bilgisayardan kalk  (Esc)", sButton))
            Close();
    }

    static string Title(App a)
    {
        foreach (var info in Apps) if (info.app == a) return info.title;
        return "";
    }

    // ---------- Uygulamalar ----------

    void ShopApp(float width)
    {
        int money = MoneyManager.Instance != null ? MoneyManager.Instance.Money : 0;
        GUILayout.Label($"Mahalle Dükkânı — Seviye {shopLevel}", sTitle);
        GUILayout.Label("Küçük ve sade bir başlangıç dükkânı. Para biriktir, dükkânını büyüt!", sText);
        GUILayout.Space(16 * s);

        GUILayout.Label($"Hedef: Seviye 2 — Büyük Mağaza   ({MoneyManager.Format(level2Cost)})", sText);
        var r = GUILayoutUtility.GetRect(width, 30 * s);
        float t = Mathf.Clamp01(level2Cost > 0 ? money / (float)level2Cost : 1f);
        Fill(r, new Color(0.82f, 0.83f, 0.86f));
        Fill(new Rect(r.x, r.y, r.width * t, r.height), new Color(0.95f, 0.5f, 0.1f));
        GUI.Label(r, $"%{Mathf.FloorToInt(t * 100f)}", sIconLabel);
        GUILayout.Label($"Kasadaki para: {MoneyManager.Format(money)}     Kalan: {MoneyManager.Format(Mathf.Max(0, level2Cost - money))}", sText);
        GUILayout.Space(12 * s);
        GUILayout.Label("Seviye 2 ile gelecekler:\n•  Daha büyük satış alanı ve vitrin\n•  Daha fazla raf ve tezgâh\n•  Daha geniş depo\n•  Daha çok müşteri", sText);
        GUILayout.Space(16 * s);

        bool canAfford = money >= level2Cost;
        if (GUILayout.Button(canAfford ? "Seviye 2'ye yükselt" : "Henüz yeterli para yok", sButton, GUILayout.Height(46 * s), GUILayout.Width(360 * s)) && canAfford)
            appMessage = "Paran hazır! Seviye 2 dükkân bir sonraki güncellemede geliyor.";
    }

    void BillsApp()
    {
        GUILayout.Label("Bu ayın faturaları", sTitle);
        GUILayout.Space(10 * s);
        foreach (var b in bills)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(b.name, sText, GUILayout.Width(260 * s));
            GUILayout.Label(MoneyManager.Format(b.amount), sText, GUILayout.Width(200 * s));
            if (b.paid) GUILayout.Label("Ödendi ✓", sText);
            else if (GUILayout.Button("Öde", sButton, GUILayout.Width(120 * s), GUILayout.Height(38 * s)))
            {
                if (MoneyManager.Instance != null && MoneyManager.Instance.TrySpend(b.amount, b.name))
                {
                    b.paid = true;
                    appMessage = $"{b.name} ödendi.";
                }
                else appMessage = "Yetersiz bakiye!";
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6 * s);
        }
        GUILayout.Space(10 * s);
        GUILayout.Label("Faturalar gün döngüsü eklenince her ay yenilenecek.", sSmall);
    }

    void MarketApp()
    {
        GUILayout.Label("Online Alışveriş", sTitle);
        GUILayout.Space(10 * s);
        GUILayout.Label("Yakında: parçaları internetten sipariş et, kargoyla dükkânına gelsin.\n" +
                        "Toptancıdan biraz pahalı ama yola çıkmana gerek yok.", sText);
        GUILayout.Space(10 * s);
        GUILayout.Label("(Uygulamanın adı henüz seçilmedi.)", sSmall);
    }

    void CameraApp()
    {
        GUILayout.Label("Güvenlik Kamerası", sTitle);
        GUILayout.Space(10 * s);
        GUILayout.Label("Durum: Kurulu değil", sText);
        GUILayout.Label("Kamera paketi (4 kamera + kayıt cihazı): " + MoneyManager.Format(4500), sText);
        GUILayout.Space(10 * s);
        GUILayout.Label("Yakında satın alınabilecek. Kameralar hırsızları caydırır, kayıtları buradan izlersin.", sSmall);
    }

    void NotesApp()
    {
        notes = GUILayout.TextArea(notes, sArea, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
    }

    void CalcApp(float width)
    {
        float bw = Mathf.Min(width, 480 * s);
        GUILayout.Label(calcDisplay, sCalc, GUILayout.Width(bw), GUILayout.Height(70 * s));
        GUILayout.Space(8 * s);
        string[,] keys =
        {
            { "7", "8", "9", "÷" },
            { "4", "5", "6", "×" },
            { "1", "2", "3", "-" },
            { "C", "0", "=", "+" },
        };
        for (int r = 0; r < 4; r++)
        {
            GUILayout.BeginHorizontal();
            for (int c = 0; c < 4; c++)
                if (GUILayout.Button(keys[r, c], sButton, GUILayout.Width(bw / 4f - 6 * s), GUILayout.Height(60 * s)))
                    CalcPress(keys[r, c][0]);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(8 * s);
        GUILayout.Label("İpucu: kâr hesabı için alış fiyatını yaz, × 1,2 gibi çarp.", sSmall);
    }

    void CalcPress(char k)
    {
        if (char.IsDigit(k))
        {
            calcDisplay = calcFresh || calcDisplay == "0" ? k.ToString() : calcDisplay + k;
            calcFresh = false;
            return;
        }
        double cur = double.TryParse(calcDisplay, out var v) ? v : 0;
        switch (k)
        {
            case 'C':
                calcDisplay = "0"; calcAcc = 0; calcOp = '\0'; calcFresh = true;
                return;
            case '=':
                cur = Apply(calcAcc, cur, calcOp);
                calcOp = '\0';
                calcAcc = 0;
                break;
            default:
                cur = calcOp == '\0' ? cur : Apply(calcAcc, cur, calcOp);
                calcAcc = cur;
                calcOp = k;
                break;
        }
        calcDisplay = double.IsInfinity(cur) || double.IsNaN(cur) ? "Hata" : Math.Round(cur, 4).ToString();
        calcFresh = true;
    }

    static double Apply(double a, double b, char op) => op switch
    {
        '+' => a + b,
        '-' => a - b,
        '×' => a * b,
        '÷' => a / b,
        _ => b,
    };

    // ---------- Stil yardımcıları ----------

    void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, texWhite);
        GUI.color = old;
    }

    void EnsureStyles()
    {
        float ns = Screen.height / 1080f;
        if (sTitle != null && Mathf.Approximately(ns, s)) return;
        s = ns;

        if (texWhite == null) texWhite = Texture2D.whiteTexture;
        if (texWallpaper == null)
        {
            texWallpaper = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            for (int y = 0; y < 64; y++)
                texWallpaper.SetPixel(0, y, Color.Lerp(new Color(0.04f, 0.1f, 0.22f), new Color(0.1f, 0.45f, 0.55f), y / 63f));
            texWallpaper.Apply();
        }

        GUIStyle Label(int size, Color col, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.UpperLeft) =>
            new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(size * s), fontStyle = fs, alignment = a, wordWrap = true, normal = { textColor = col } };

        var dark = new Color(0.12f, 0.13f, 0.16f);
        sTitle = Label(30, dark, FontStyle.Bold);
        sText = Label(22, dark);
        sSmall = Label(18, new Color(0.4f, 0.42f, 0.46f), FontStyle.Italic);
        sIconLetter = Label(34, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        sIconLabel = Label(17, Color.white, FontStyle.Bold, TextAnchor.UpperCenter);
        sWinTitle = Label(22, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
        sTask = Label(20, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
        sWatermark = Label(64, new Color(1f, 1f, 1f, 0.12f), FontStyle.Bold, TextAnchor.MiddleCenter);
        sCalc = new GUIStyle(Label(40, Color.white, FontStyle.Bold, TextAnchor.MiddleRight))
        {
            normal = { background = Solid(new Color(0.13f, 0.17f, 0.24f)), textColor = Color.white },
            padding = new RectOffset(16, 16, 8, 8),
        };
        sButton = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20 * s), fontStyle = FontStyle.Bold };
        sClose = new GUIStyle(sButton)
        {
            normal = { background = Solid(new Color(0.75f, 0.2f, 0.2f)), textColor = Color.white },
            hover = { background = Solid(new Color(0.9f, 0.3f, 0.3f)), textColor = Color.white },
        };
        sArea = new GUIStyle(GUI.skin.textArea) { fontSize = Mathf.RoundToInt(22 * s), wordWrap = true };
    }

    static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}

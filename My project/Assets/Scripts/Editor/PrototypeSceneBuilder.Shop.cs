#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// BİZİM DÜKKÂN — Seviye 1 "mahalle dükkânı": küçük ve ucuz ama derli toplu.
/// (Hikâyede para biriktirip Seviye 2 büyük mağazaya geçilecek.)
///
/// ÜSTTEN PLAN (dünya koordinatı, dükkân kökü orijinde; ön cephe +z → cadde):
///
///                        KALDIRIM / CADDE
///   ┌──[vitrin camı]────[KAPI]────[vitrin camı]──┐ z = +6   (tuğla cephe, tenteler, tabela)
///   │ podyum (Vitrin Sol)        podyum (Vitrin Sağ)│
///   │P                             ┌─dönüş─┐ girişi│
///   │C       [teşhir masası]        │ KASA  │ personel│
///   │                               │ tezgâhı│ alanı │
///   │ [KONSOL RAFI]   poster           │       │ dolap │
///   ├────────────────────────────[kapı]──┬───────────┤ z = 0
///   │ tezgâh 2   [PC MASASI]  TAMİRHANE [kapı] DEPO     │
///   │ (pencere)                          │  depo rafları│
///   │ uzun tezgâh (tamir | kurulum) + alet panosu [ARKA KAPI] │
///   └───────────────────────────────────┴─────────────┘ z = -6
///   x = -8                              x = 3          x = 8
///
/// Eşyalar kenarları yuvarlatılmış parçalardan (RBox) ve fiziksel tabanlı malzemelerden (Pbr) yapılır;
/// ortak araçlar PrototypeSceneBuilder.Props.cs içinde.
/// Raf / tezgâh yuvalarının (PlacementSlot) adları ve yerleri korunuyor.
/// </summary>
public static partial class PrototypeSceneBuilder
{
    static readonly Color ShopAccent = new Color(0.16f, 0.42f, 0.52f);
    const float Threshold = 0.006f; // kapı eşiği yüksekliği (zeminin üstünde)

    static Material MAccent => Pbr("P_Vurgu", ShopAccent, 0.3f);

    static void BuildShop()
    {
        var shop = new GameObject("Dükkân").transform;
        ShopShell(shop);
        ShopShowroom(shop);
        ShopCounter(shop);
        ShopComputerDesk(shop);
        ShopWorkshop(shop);
        ShopStorage(shop);
        ShopDoors(shop);
        ShopExterior(shop);
    }

    // =====================================================================
    // GÖVDE: zeminler, çift katmanlı duvarlar (dışı tuğla, içi boya), asma tavan, çatı, ışık
    // =====================================================================

    static void ShopShell(Transform shop)
    {
        var brickTex = BrickPair();
        var brick = Pbr("P_Tugla", Color.white, 0.12f, 0f, brickTex.color, brickTex.normal, 1f);
        var paintShow = Pbr("P_BoyaSatis", new Color(0.93f, 0.91f, 0.86f), 0.15f);
        var paintAccent = Pbr("P_BoyaVurgu", ShopAccent, 0.15f);
        var paintWork = Pbr("P_BoyaTamirhane", new Color(0.78f, 0.82f, 0.8f), 0.15f);
        var paintStore = Pbr("P_BoyaDepo", new Color(0.72f, 0.72f, 0.7f), 0.12f);
        var lam = LaminatePair();
        var laminate = Pbr("P_Laminat", Color.white, 0.42f, 0f, lam.color, lam.normal, 0.8f);
        var epoxy = TexMat("EpoksiZemin", EpoxyTexture(), new Color(0.72f, 0.78f, 0.8f), 0.55f);
        var storeFloor = TexMat("DepoZemin", EpoxyTexture(), new Color(0.85f, 0.83f, 0.8f), 0.25f);
        var ceil = CeilingPair();
        var ceiling = Pbr("P_AsmaTavan", Color.white, 0.08f, 0f, ceil.color, ceil.normal, 0.8f);

        float H = WallH, P = WallH + 0.45f;

        // Zeminler
        UVBox(shop, "Zemin - Satış Alanı (laminat)", new Vector3(0f, FloorY / 2f, 3f), new Vector3(16f, FloorY, 6f), laminate, 2f, true);
        UVBox(shop, "Zemin - Tamirhane (epoksi)", new Vector3(-2.5f, FloorY / 2f, -3f), new Vector3(11f, FloorY, 6f), epoxy, 2f, true);
        UVBox(shop, "Zemin - Depo (beton)", new Vector3(5.5f, FloorY / 2f, -3f), new Vector3(5f, FloorY, 6f), storeFloor, 2f, true);

        // Dış duvarlar: dış katman tuğla (camlar ve kasalar burada), iç katman oda boyası
        var front = new[] { Opening.Window(-4.8f, 4.6f, 0.6f, 2.4f), Opening.Door(0f, 1.4f, 2.3f), Opening.Window(4.8f, 4.6f, 0.6f, 2.4f) };
        var back = new[] { Opening.Window(-5f, 1.6f, 1.3f, 2.4f), Opening.Door(1.2f, 1.6f, 2.3f), Opening.Window(5.5f, 1.2f, 2.0f, 2.6f) };
        var left = new[] { Opening.Window(-3f, 1.4f, 1.3f, 2.4f) };
        var right = new[] { Opening.Window(4.6f, 1.2f, 1.4f, 2.4f) };

        TexWall(shop, "Ön Cephe (tuğla)", true, MaxZ + 0.05f, MinX - 0.1f, MaxX + 0.1f, P, 0.1f, brick, 1f, true, front);
        TexWall(shop, "Ön Duvar (iç)", true, MaxZ - 0.05f, MinX, MaxX, H, 0.1f, paintShow, 1f, false, front);
        TexWall(shop, "Arka Cephe (tuğla)", true, MinZ - 0.05f, MinX - 0.1f, MaxX + 0.1f, P, 0.1f, brick, 1f, true, back);
        TexWall(shop, "Arka Duvar (tamirhane)", true, MinZ + 0.05f, MinX, DepoX, H, 0.1f, paintWork, 1f, false, back);
        TexWall(shop, "Arka Duvar (depo)", true, MinZ + 0.05f, DepoX, MaxX, H, 0.1f, paintStore, 1f, false, back);
        TexWall(shop, "Sol Cephe (tuğla)", false, MinX - 0.05f, MinZ, MaxZ, P, 0.1f, brick, 1f, true, left);
        TexWall(shop, "Sol Duvar (satış)", false, MinX + 0.05f, SplitZ, MaxZ - 0.1f, H, 0.1f, paintShow, 1f, false, left);
        TexWall(shop, "Sol Duvar (tamirhane)", false, MinX + 0.05f, MinZ + 0.1f, SplitZ, H, 0.1f, paintWork, 1f, false, left);
        TexWall(shop, "Sağ Cephe (tuğla)", false, MaxX + 0.05f, MinZ, MaxZ, P, 0.1f, brick, 1f, true, right);
        TexWall(shop, "Sağ Duvar (satış)", false, MaxX - 0.05f, SplitZ, MaxZ - 0.1f, H, 0.1f, paintShow, 1f, false, right);
        TexWall(shop, "Sağ Duvar (depo)", false, MaxX - 0.05f, MinZ + 0.1f, SplitZ, H, 0.1f, paintStore, 1f, false, right);

        // İç bölmeler (iki yüzü ayrı boya). Satış alanının arka duvarı vurgu rengi.
        var workDoor = new[] { Opening.Door(2f, 1f, 2.2f) };
        var storeDoor = new[] { Opening.Door(-3f, 1f, 2.2f) };
        TexWall(shop, "Bölme (satış yüzü)", true, SplitZ + 0.03f, MinX + 0.1f, MaxX - 0.1f, H, 0.06f, paintAccent, 1f, false, workDoor);
        TexWall(shop, "Bölme (tamirhane yüzü)", true, SplitZ - 0.03f, MinX + 0.1f, DepoX, H, 0.06f, paintWork, 1f, false, workDoor);
        TexWall(shop, "Bölme (depo yüzü)", true, SplitZ - 0.03f, DepoX, MaxX - 0.1f, H, 0.06f, paintStore, 1f, false);
        TexWall(shop, "Depo Bölmesi (tamirhane yüzü)", false, DepoX - 0.03f, MinZ + 0.1f, SplitZ - 0.06f, H, 0.06f, paintWork, 1f, false, storeDoor);
        TexWall(shop, "Depo Bölmesi (depo yüzü)", false, DepoX + 0.03f, MinZ + 0.1f, SplitZ - 0.06f, H, 0.06f, paintStore, 1f, false, storeDoor);

        // Süpürgelik (satış alanı; bölmedeki tamirhane kapısının pervazından önce biter)
        var skirting = Pbr("P_Supurgelik", new Color(0.3f, 0.22f, 0.16f), 0.4f, 0f, WoodPair().color, WoodPair().normal, 0.4f);
        RBox(shop, "Süpürgelik", new Vector3(MinX + 0.11f, FloorY + 0.05f, 3f), new Vector3(0.02f, 0.1f, 5.8f), skirting, 0.004f);
        RBox(shop, "Süpürgelik", new Vector3(MaxX - 0.11f, FloorY + 0.05f, 3f), new Vector3(0.02f, 0.1f, 5.8f), skirting, 0.004f);
        RBox(shop, "Süpürgelik", new Vector3((MinX + 0.1f + 1.42f) / 2f, FloorY + 0.05f, SplitZ + 0.07f), new Vector3(1.42f - (MinX + 0.1f), 0.1f, 0.02f), skirting, 0.004f);
        RBox(shop, "Süpürgelik", new Vector3((2.58f + MaxX - 0.1f) / 2f, FloorY + 0.05f, SplitZ + 0.07f), new Vector3(MaxX - 0.1f - 2.58f, 0.1f, 0.02f), skirting, 0.004f);

        // Asma tavan + çatı + korkuluk harpuştası
        UVBox(shop, "Asma Tavan", new Vector3(0f, H + 0.05f, 0f), new Vector3(16f, 0.1f, 12f), ceiling, 1.2f, false);
        SBox(shop, "Çatı", new Vector3(0f, H + 0.18f, 0f), new Vector3(16f, 0.16f, 12f), Mat("CatiMembran", new Color(0.25f, 0.25f, 0.27f)), true);
        var coping = Pbr("P_Harpusta", new Color(0.78f, 0.78f, 0.76f), 0.3f);
        RBox(shop, "Harpuşta", new Vector3(0f, P + 0.03f, MaxZ + 0.05f), new Vector3(16.3f, 0.06f, 0.2f), coping, 0.01f);
        RBox(shop, "Harpuşta", new Vector3(0f, P + 0.03f, MinZ - 0.05f), new Vector3(16.3f, 0.06f, 0.2f), coping, 0.01f);
        RBox(shop, "Harpuşta", new Vector3(MinX - 0.05f, P + 0.03f, 0f), new Vector3(0.2f, 0.06f, 12.2f), coping, 0.01f);
        RBox(shop, "Harpuşta", new Vector3(MaxX + 0.05f, P + 0.03f, 0f), new Vector3(0.2f, 0.06f, 12.2f), coping, 0.01f);

        // LED lineer armatürler + gölgeli ışıklar
        foreach (float x in new[] { -5f, -1f, 3f })
        foreach (float z in new[] { 1.8f, 4.2f })
            LinearLight(shop, new Vector3(x, H, z));
        foreach (float x in new[] { -4.5f, 0f, 4.5f })
            ShopLight(shop, new Vector3(x, 2.85f, 3f), 8f, 2.6f, true);

        foreach (float x in new[] { -5f, -0.5f })
        {
            LinearLight(shop, new Vector3(x, H, -3f));
            ShopLight(shop, new Vector3(x, 2.85f, -3f), 7.5f, 2.4f, false); // gölge atlası satış alanına ve masa lambasına ayrıldı
        }
        LinearLight(shop, new Vector3(5.5f, H, -3f));
        ShopLight(shop, new Vector3(5.5f, 2.85f, -3f), 6f, 1.9f, false);

        // Yansıma probları: krom, cam ve parlak yüzeyler odayı yansıtsın
        ReflectionProbeBox(shop, "Yansıma Probu (satış)", new Vector3(0f, 1.7f, 3f), new Vector3(15.8f, 3.1f, 5.8f));
        ReflectionProbeBox(shop, "Yansıma Probu (tamirhane)", new Vector3(-2.5f, 1.7f, -3f), new Vector3(10.8f, 3.1f, 5.8f));
        ReflectionProbeBox(shop, "Yansıma Probu (depo)", new Vector3(5.5f, 1.7f, -3f), new Vector3(4.8f, 3.1f, 5.8f));
    }

    /// Tavana sıva üstü LED lineer armatür: fırçalı alüminyum gövde, opal difüzör, uç kapakları.
    static void LinearLight(Transform parent, Vector3 ceilingPoint)
    {
        var g = new GameObject("LED Armatür").transform;
        g.SetParent(parent, false);
        g.localPosition = ceilingPoint;
        RBox(g, "Gövde", new Vector3(0f, -0.025f, 0f), new Vector3(1.24f, 0.05f, 0.16f), MBrushed, 0.012f, false, 0.5f);
        RBox(g, "Difüzör", new Vector3(0f, -0.052f, 0f), new Vector3(1.18f, 0.014f, 0.12f), PbrGlow("P_Difuzor", new Color(1f, 0.97f, 0.9f), 2.2f), 0.006f);
        foreach (float x in new[] { -0.615f, 0.615f })
            RBox(g, "Uç Kapağı", new Vector3(x, -0.026f, 0f), new Vector3(0.02f, 0.054f, 0.164f), MWhitePlastic, 0.006f);
    }

    // =====================================================================
    // SATIŞ ALANI
    // =====================================================================

    static void ShopShowroom(Transform shop)
    {
        var room = new GameObject("Satış Alanı").transform;
        room.SetParent(shop, false);
        float[] levels = { 0.45f, 1.05f, 1.65f };

        // Sol duvar: hazır sistemler (PC rafı); arka (vurgu) duvar: konsollar
        WallShelf(room, "PC Rafı", new Vector3(MinX + 0.1f + 0.225f, FloorY, 2.6f), 90f, 4f, levels, "PC Rafı", "HAZIR SİSTEMLER");
        WallShelf(room, "Konsol Rafı", new Vector3(-4f, FloorY, SplitZ + 0.06f + 0.225f), 0f, 4f, levels, "Konsol Rafı", "KONSOLLAR");

        // Vitrin podyumları (cadde tarafından görünür)
        Podium(room, "Vitrin Sol", new Vector3(-4.8f, FloorY, 5.55f), new Vector3(4.4f, 0.45f, 0.6f), "Vitrin", 4);
        Podium(room, "Vitrin Sağ", new Vector3(4.8f, FloorY, 5.55f), new Vector3(4.4f, 0.45f, 0.6f), "Vitrin", 4);

        DisplayCase(room, new Vector3(-3.6f, FloorY, 2.9f));

        // Posterler, saat, bitkiler, paspas
        Poster(room, new Vector3(-0.7f, 1.75f, SplitZ + 0.075f), 180f, new Vector2(0.9f, 1.2f), new Color(0.06f, 0.08f, 0.16f), "PlayZone 5 Pro", "ŞİMDİ STOKTA", 0);
        Poster(room, new Vector3(MinX + 0.115f, 1.8f, 5.25f), -90f, new Vector2(0.6f, 0.85f), new Color(0.35f, 0.06f, 0.05f), "Gryphon GX460", "OYUN CANAVARI", 1);
        Poster(room, new Vector3(-6.6f, 2.55f, SplitZ + 0.075f), 180f, new Vector2(0.9f, 0.5f), new Color(0.95f, 0.82f, 0.18f), "TAMİR - FORMAT", "PC TOPLAMA", 2);
        WallClock(room, new Vector3(0f, 2.72f, MaxZ - 0.12f), 0f);
        Plant(room, new Vector3(1.75f, FloorY, 5.35f), 1.0f, 1);
        Plant(room, new Vector3(-7.4f, FloorY, 5.0f), 1.15f, 2);
        DoorMat(room, new Vector3(0f, FloorY, 5.25f));

        // Camdaki yazılar (dışarıdan okunur)
        Label(room, "AÇIK", new Vector3(-6.4f, 1.85f, MaxZ - 0.14f), 180f, 1f, false, new Color(1f, 0.2f, 0.15f));
        Label(room, "TAMİR - FORMAT - PC TOPLAMA", new Vector3(4.8f, 2.2f, MaxZ - 0.14f), 180f, 0.4f, false, Color.white);
    }

    /// Duvar rafı: lamel arkalık, uç panelleri, siyah dikmeler ve konsollar, meşe raf tahtaları,
    /// şeffaf fiyat kanalı + etiketler, LED şerit, arkadan aydınlatmalı başlık. Yerel +z = ön. Her katta 4 yuva.
    static Transform WallShelf(Transform parent, string name, Vector3 pos, float yaw, float length, float[] levels, string slotBase, string header)
    {
        var u = new GameObject(name).transform;
        u.SetParent(parent, false);
        u.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        var slatTex = SlatwallPair();
        var slat = Pbr("P_Lamel", new Color(0.97f, 0.97f, 0.96f), 0.35f, 0f, slatTex.color, slatTex.normal, 1f);
        var priceChannel = Pbr("P_FiyatKanali", new Color(0.95f, 0.95f, 0.95f), 0.7f);
        var tag = Pbr("P_FiyatEtiketi", new Color(1f, 0.86f, 0.15f), 0.35f);
        var led = PbrGlow("P_LedSerit", new Color(1f, 0.94f, 0.82f), 2.5f);
        const float D = 0.45f, H = 2.2f;

        // Kaide, arkalık, uç panelleri
        RBox(u, "Kaide", new Vector3(0f, 0.06f, -0.02f), new Vector3(length, 0.12f, D - 0.04f), MMattePlastic, 0.006f, true);
        RBox(u, "Lamel Arkalık", new Vector3(0f, 0.12f + (H - 0.12f) / 2f, -D / 2f + 0.0125f), new Vector3(length, H - 0.12f, 0.025f), slat, 0.004f, true, 0.6f);
        foreach (float sx in new[] { -1f, 1f })
            RBox(u, "Uç Paneli", new Vector3(sx * (length / 2f + 0.015f), H / 2f + 0.05f, 0f), new Vector3(0.03f, H + 0.1f, D), MBlackMetal, 0.008f, true);

        // Dikmeler (arkalığa vidalı) ve konsollar
        int posts = Mathf.Max(1, Mathf.RoundToInt(length));
        for (int k = 0; k <= posts; k++)
        {
            float x = Mathf.Clamp(-length / 2f + k * length / posts, -length / 2f + 0.04f, length / 2f - 0.04f);
            RBox(u, "Dikme", new Vector3(x, 0.12f + (H - 0.2f) / 2f, -D / 2f + 0.033f), new Vector3(0.028f, H - 0.2f, 0.016f), MBlackMetal, 0.004f);
            foreach (float y in levels)
            {
                RBox(u, "Konsol", new Vector3(x, y - 0.05f, -0.01f), new Vector3(0.012f, 0.05f, D - 0.07f), MBlackMetal, 0.003f);
                RBox(u, "Konsol Ucu", new Vector3(x, y - 0.035f, D / 2f - 0.045f), new Vector3(0.014f, 0.02f, 0.02f), MBlackMetal, 0.004f);
            }
        }

        int n = 1;
        foreach (float y in levels)
        {
            RBox(u, "Raf Tahtası", new Vector3(0f, y - 0.014f, 0f), new Vector3(length - 0.006f, 0.028f, D), MOak, 0.005f, true);
            RBox(u, "Fiyat Kanalı", new Vector3(0f, y - 0.022f, D / 2f + 0.007f), new Vector3(length - 0.01f, 0.038f, 0.012f), priceChannel, 0.004f);
            RBox(u, "LED Şerit", new Vector3(0f, y - 0.034f, D / 2f - 0.06f), new Vector3(length - 0.1f, 0.008f, 0.016f), led, 0.003f);
            for (int i = 0; i < 4; i++)
            {
                float sx = -length / 2f + length / 8f + i * length / 4f;
                CreateSlot(u, $"{slotBase} {n++}", new Vector3(sx, y, 0.02f));
                RBox(u, "Fiyat Etiketi", new Vector3(sx, y - 0.022f, D / 2f + 0.0145f), new Vector3(0.09f, 0.03f, 0.003f), tag, 0.002f);
            }
        }

        // Arkadan aydınlatmalı başlık panosu
        RBox(u, "Başlık Panosu", new Vector3(0f, H + 0.17f, -D / 2f + 0.04f), new Vector3(length + 0.06f, 0.34f, 0.04f), MAccent, 0.012f);
        RBox(u, "Başlık Işığı", new Vector3(0f, H + 0.005f, -D / 2f + 0.06f), new Vector3(length, 0.008f, 0.01f), led, 0.003f);
        Label(u, header, u.TransformPoint(new Vector3(0f, H + 0.17f, -D / 2f + 0.065f)), yaw + 180f, 0.75f, false, Color.white);
        return u;
    }

    /// Vitrin podyumu: yuvarlatılmış gövde, keçe kaplı üst, fırçalı alüminyum süpürgelik, tavanda ray spotlar.
    static void Podium(Transform parent, string name, Vector3 pos, Vector3 size, string slotBase, int slots)
    {
        var p = new GameObject(name).transform;
        p.SetParent(parent, false);
        p.position = pos;
        var felt = FeltPair();
        var top = Pbr("P_PodyumKece", new Color(0.32f, 0.07f, 0.09f), 0.08f, 0f, felt.color, felt.normal, 0.6f);
        var body = Pbr("P_PodyumGovde", new Color(0.13f, 0.13f, 0.14f), 0.35f);

        RBox(p, "Gövde", new Vector3(0f, size.y / 2f, 0f), size, body, 0.025f, true);
        RBox(p, "Keçe Üst", new Vector3(0f, size.y + 0.004f, 0f), new Vector3(size.x - 0.03f, 0.012f, size.z - 0.03f), top, 0.005f, false, 0.5f);
        foreach (float sz in new[] { -1f, 1f })
            RBox(p, "Alüminyum Süpürgelik", new Vector3(0f, 0.03f, sz * (size.z / 2f + 0.002f)), new Vector3(size.x - 0.06f, 0.05f, 0.006f), MBrushed, 0.002f, false, 0.5f);
        AddSlotGrid(p, slotBase, size.y + 0.02f, slots, 1, size.x / slots, 0f);

        // Tavanda ray ve spotlar
        float ceilY = WallH - FloorY;
        RBox(p, "Spot Rayı", new Vector3(0f, ceilY - 0.018f, 0f), new Vector3(size.x, 0.034f, 0.034f), MBlackMetal, 0.006f);
        var lens = PbrGlow("P_SpotLens", new Color(1f, 0.95f, 0.85f), 4f);
        for (int i = 0; i < slots; i++)
        {
            float x = (i - (slots - 1) / 2f) * size.x / slots;
            RBox(p, "Spot Adaptörü", new Vector3(x, ceilY - 0.05f, 0f), new Vector3(0.04f, 0.035f, 0.05f), MBlackMetal, 0.006f);
            Prim(PrimitiveType.Cylinder, p, "Spot Kolu", new Vector3(x, ceilY - 0.09f, 0f), new Vector3(0.012f, 0.03f, 0.012f), MBlackMetal, false);
            Prim(PrimitiveType.Cylinder, p, "Spot Gövdesi", new Vector3(x, ceilY - 0.17f, 0f), new Vector3(0.075f, 0.06f, 0.075f), MBlackMetal, false);
            Prim(PrimitiveType.Cylinder, p, "Spot Halkası", new Vector3(x, ceilY - 0.232f, 0f), new Vector3(0.08f, 0.004f, 0.08f), MBrushed, false);
            Prim(PrimitiveType.Cylinder, p, "Spot Merceği", new Vector3(x, ceilY - 0.236f, 0f), new Vector3(0.055f, 0.002f, 0.055f), lens, false);
        }
        foreach (float sx in new[] { -0.25f, 0.25f })
        {
            var l = new GameObject("Vitrin Spotu").AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = new Vector3(sx * size.x, ceilY - 0.25f, 0f);
            l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            l.type = LightType.Spot;
            l.spotAngle = 60f;
            l.innerSpotAngle = 30f;
            l.range = 4f;
            l.intensity = 3f;
            l.color = new Color(1f, 0.92f, 0.8f);
            l.shadows = LightShadows.None;
        }
    }

    /// Ortadaki camlı aksesuar vitrini: melamin dolap, alüminyum profilli cam kutu, içinde ürünler, üstünde 3 yuva.
    static void DisplayCase(Transform parent, Vector3 pos)
    {
        var d = new GameObject("Teşhir Masası").transform;
        d.SetParent(parent, false);
        d.position = pos;
        const float W = 1.8f, Dp = 0.7f, baseH = 0.78f, caseH = 0.37f;

        RBox(d, "Süpürgelik", new Vector3(0f, 0.04f, 0f), new Vector3(W - 0.06f, 0.08f, Dp - 0.06f), MMattePlastic, 0.004f);
        RBox(d, "Dolap", new Vector3(0f, 0.08f + (baseH - 0.08f) / 2f, 0f), new Vector3(W, baseH - 0.08f, Dp), MMelamine, 0.012f, true);
        RBox(d, "Vurgu Bandı", new Vector3(0f, baseH - 0.07f, 0f), new Vector3(W + 0.006f, 0.05f, Dp + 0.006f), MAccent, 0.006f);
        RBox(d, "İç Taban (keçe)", new Vector3(0f, baseH + 0.003f, 0f), new Vector3(W - 0.04f, 0.006f, Dp - 0.04f),
            Pbr("P_KoyuKece", new Color(0.08f, 0.08f, 0.09f), 0.05f, 0f, FeltPair().color, FeltPair().normal, 0.5f), 0.002f, false, 0.5f);

        // Cam kutu: alüminyum köşe profilleri + cam paneller
        float cy = baseH + caseH / 2f;
        foreach (float x in new[] { -W / 2f + 0.01f, W / 2f - 0.01f })
        foreach (float z in new[] { -Dp / 2f + 0.01f, Dp / 2f - 0.01f })
            RBox(d, "Köşe Profili", new Vector3(x, cy, z), new Vector3(0.02f, caseH, 0.02f), MBrushed, 0.004f, false, 0.5f);
        foreach (float z in new[] { -Dp / 2f + 0.01f, Dp / 2f - 0.01f })
        {
            RBox(d, "Üst Profil", new Vector3(0f, baseH + caseH - 0.008f, z), new Vector3(W, 0.016f, 0.02f), MBrushed, 0.004f, false, 0.5f);
            RBox(d, "Cam (ön/arka)", new Vector3(0f, cy, z), new Vector3(W - 0.03f, caseH - 0.02f, 0.006f), MGlassClean, 0.001f);
        }
        foreach (float x in new[] { -W / 2f + 0.01f, W / 2f - 0.01f })
            RBox(d, "Cam (yan)", new Vector3(x, cy, 0f), new Vector3(0.006f, caseH - 0.02f, Dp - 0.03f), MGlassClean, 0.001f);
        RBox(d, "Üst Cam", new Vector3(0f, baseH + caseH + 0.004f, 0f), new Vector3(W, 0.008f, Dp), MGlassClean, 0.002f);
        RBox(d, "İç LED", new Vector3(0f, baseH + caseH - 0.02f, -Dp / 2f + 0.03f), new Vector3(W - 0.08f, 0.006f, 0.012f), PbrGlow("P_LedSerit", new Color(1f, 0.94f, 0.82f), 2.5f), 0.002f);
        var glassCol = new GameObject("Cam Kutu Çarpışıcısı").AddComponent<BoxCollider>();
        glassCol.transform.SetParent(d, false);
        glassCol.center = new Vector3(0f, cy + 0.005f, 0f);
        glassCol.size = new Vector3(W, caseH + 0.01f, Dp);

        // İçindeki aksesuarlar: fareler, kulaklık, kutulu ürünler, web kamerası
        float fy = baseH + 0.006f;
        Mouse(d, new Vector3(-0.72f, fy, -0.1f), MBlackPlastic, 15f);
        Mouse(d, new Vector3(-0.55f, fy, 0.08f), MWhitePlastic, -20f);
        Headset(d, new Vector3(-0.25f, fy, 0f));
        var sticker = Pbr("P_KutuEtiketi", new Color(0.95f, 0.95f, 0.93f), 0.3f);
        Color[] boxCols = { new Color(0.75f, 0.12f, 0.1f), new Color(0.1f, 0.25f, 0.6f), new Color(0.08f, 0.08f, 0.09f) };
        for (int i = 0; i < 3; i++)
        {
            var bs = new Vector3(0.16f, 0.1f + i * 0.03f, 0.12f);
            var bp = new Vector3(0.12f + i * 0.2f, fy + bs.y / 2f, (i % 2 == 0 ? -0.1f : 0.1f));
            RBox(d, "Aksesuar Kutusu", bp, bs, Pbr("P_AksesuarKutu_" + i, boxCols[i], 0.5f), 0.006f, false, 1f, new Vector3(0f, i * 12f - 10f, 0f));
            RBox(d, "Kutu Etiketi", bp + new Vector3(0f, 0.01f, 0f), new Vector3(bs.x * 0.7f, bs.y * 0.45f, bs.z + 0.002f), sticker, 0.003f, false, 1f, new Vector3(0f, i * 12f - 10f, 0f));
        }
        Webcam(d, new Vector3(0.75f, fy, 0.05f));

        AddSlotGrid(d, "Teşhir", baseH + caseH + 0.012f, 3, 1, 0.6f, 0f);
        Label(d, "AKSESUAR", d.TransformPoint(new Vector3(0f, baseH - 0.07f, Dp / 2f + 0.006f)), 180f, 0.3f, false, Color.white);
    }

    static void Mouse(Transform parent, Vector3 floorPos, Material body, float yaw)
    {
        var g = new GameObject("Fare").transform;
        g.SetParent(parent, false);
        g.localPosition = floorPos;
        g.localRotation = Quaternion.Euler(0f, yaw, 0f);
        RBox(g, "Gövde", new Vector3(0f, 0.017f, 0f), new Vector3(0.062f, 0.034f, 0.112f), body, 0.016f);
        RBox(g, "Tuş Çizgisi", new Vector3(0f, 0.033f, 0.03f), new Vector3(0.002f, 0.004f, 0.05f), MMattePlastic, 0.001f);
        Prim(PrimitiveType.Cylinder, g, "Tekerlek", new Vector3(0f, 0.033f, 0.028f), new Vector3(0.012f, 0.004f, 0.012f), MRubber, false, new Vector3(0f, 0f, 90f));
    }

    static void Headset(Transform parent, Vector3 floorPos)
    {
        var g = new GameObject("Kulaklık").transform;
        g.SetParent(parent, false);
        g.localPosition = floorPos;
        // Kafa bandı: yay şeklinde dizilmiş parçalar
        const int segs = 9;
        for (int i = 0; i < segs; i++)
        {
            float a = Mathf.Lerp(-80f, 80f, i / (float)(segs - 1)) * Mathf.Deg2Rad;
            var p = new Vector3(Mathf.Sin(a) * 0.085f, 0.03f + Mathf.Cos(a) * 0.1f, 0f);
            RBox(g, "Bant", p, new Vector3(0.024f, 0.012f, 0.03f), MMattePlastic, 0.005f, false, 1f, new Vector3(0f, 0f, -a * Mathf.Rad2Deg));
        }
        foreach (float sx in new[] { -1f, 1f })
        {
            Prim(PrimitiveType.Cylinder, g, "Kulaklık Yastığı", new Vector3(sx * 0.09f, 0.04f, 0f), new Vector3(0.075f, 0.014f, 0.085f), MLeather, false, new Vector3(0f, 0f, 90f));
            Prim(PrimitiveType.Cylinder, g, "Kulaklık Kabı", new Vector3(sx * 0.105f, 0.04f, 0f), new Vector3(0.065f, 0.012f, 0.075f), MBlackPlastic, false, new Vector3(0f, 0f, 90f));
            Prim(PrimitiveType.Cylinder, g, "RGB Halka", new Vector3(sx * 0.118f, 0.04f, 0f), new Vector3(0.05f, 0.002f, 0.058f), PbrGlow("P_RgbKirmizi", new Color(1f, 0.1f, 0.1f), 2f), false, new Vector3(0f, 0f, 90f));
        }
    }

    static void Webcam(Transform parent, Vector3 floorPos)
    {
        var g = new GameObject("Web Kamerası").transform;
        g.SetParent(parent, false);
        g.localPosition = floorPos;
        RBox(g, "Ayak", new Vector3(0f, 0.01f, 0f), new Vector3(0.05f, 0.02f, 0.04f), MMattePlastic, 0.008f);
        RBox(g, "Gövde", new Vector3(0f, 0.04f, 0f), new Vector3(0.09f, 0.03f, 0.03f), MBlackPlastic, 0.014f);
        Prim(PrimitiveType.Cylinder, g, "Lens", new Vector3(0f, 0.04f, 0.016f), new Vector3(0.018f, 0.002f, 0.018f), MGlassClean, false, new Vector3(90f, 0f, 0f));
    }

    /// Duvar posteri: alüminyum çerçeve, baskı ve basit ürün çizimi. yaw: posteri okuyan kişinin baktığı yön.
    /// art: 0 = konsol, 1 = ekran kartı, 2 = sadece yazı.
    static void Poster(Transform parent, Vector3 pos, float yaw, Vector2 size, Color bg, string title, string subtitle, int art)
    {
        var p = new GameObject("Poster - " + title).transform;
        p.SetParent(parent, false);
        p.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        // Okuyucu yerel +z'ye bakıyor: duvar +z tarafında, poster yüzü -z tarafında
        RBox(p, "Çerçeve", new Vector3(0f, 0f, 0.012f), new Vector3(size.x + 0.05f, size.y + 0.05f, 0.02f), MBlackMetal, 0.006f);
        RBox(p, "Baskı", new Vector3(0f, 0f, 0f), new Vector3(size.x, size.y, 0.006f), Pbr("P_Poster_" + ColorUtility.ToHtmlStringRGB(bg), bg, 0.35f), 0.001f);
        RBox(p, "Işık Hüzmesi", new Vector3(-size.x * 0.1f, size.y * 0.05f, -0.004f), new Vector3(size.x * 0.12f, size.y * 1.1f, 0.001f),
            Pbr("P_PosterIsik_" + art, Color.Lerp(bg, Color.white, 0.18f), 0.35f), 0f, false, 1f, new Vector3(0f, 0f, -25f));

        Color tc = art == 2 ? new Color(0.08f, 0.08f, 0.08f) : Color.white;
        float titleY = art == 2 ? size.y * 0.12f : size.y * 0.36f;
        float subY = art == 2 ? -size.y * 0.2f : -size.y * 0.4f;
        if (art == 0)
        {
            // Dikey duran konsol: beyaz kapaklar, siyah orta gövde, mavi ışık şeridi
            float cx = size.x * 0.18f, cy = -size.y * 0.05f;
            RBox(p, "Konsol Kapak", new Vector3(cx - 0.035f, cy, -0.008f), new Vector3(0.05f, 0.42f, 0.006f), MWhitePlastic, 0.02f);
            RBox(p, "Konsol Kapak", new Vector3(cx + 0.035f, cy, -0.008f), new Vector3(0.05f, 0.42f, 0.006f), MWhitePlastic, 0.02f);
            RBox(p, "Konsol Gövde", new Vector3(cx, cy, -0.006f), new Vector3(0.022f, 0.4f, 0.006f), MBlackPlastic, 0.006f);
            RBox(p, "Konsol Işığı", new Vector3(cx, cy, -0.0095f), new Vector3(0.004f, 0.36f, 0.002f), PbrGlow("P_KonsolMavi", new Color(0.2f, 0.5f, 1f), 3f), 0.001f);
        }
        else if (art == 1)
        {
            // Ekran kartı: siyah gövde, iki fan
            var cy = -size.y * 0.02f;
            RBox(p, "Kart Gövdesi", new Vector3(0f, cy, -0.008f), new Vector3(size.x * 0.75f, 0.13f, 0.008f), MBlackPlastic, 0.01f);
            foreach (float fx in new[] { -0.11f, 0.11f })
            {
                Prim(PrimitiveType.Cylinder, p, "Fan", new Vector3(fx * size.x / 0.6f, cy, -0.012f), new Vector3(0.1f, 0.002f, 0.1f), MMattePlastic, false, new Vector3(90f, 0f, 0f));
                Prim(PrimitiveType.Cylinder, p, "Fan Halkası", new Vector3(fx * size.x / 0.6f, cy, -0.0135f), new Vector3(0.105f, 0.0005f, 0.105f),
                    PbrGlow("P_RgbKirmizi", new Color(1f, 0.1f, 0.1f), 2f), false, new Vector3(90f, 0f, 0f));
                Prim(PrimitiveType.Cylinder, p, "Fan Göbeği", new Vector3(fx * size.x / 0.6f, cy, -0.014f), new Vector3(0.03f, 0.0005f, 0.03f), MBrushed, false, new Vector3(90f, 0f, 0f));
            }
        }

        Label(p, title, p.TransformPoint(new Vector3(0f, titleY, -0.012f)), yaw, Mathf.Min(0.5f, size.x / (0.14f * title.Length + 0.2f) * 0.95f), false, tc);
        Label(p, subtitle, p.TransformPoint(new Vector3(0f, subY, -0.012f)), yaw, Mathf.Min(0.35f, size.x / (0.14f * subtitle.Length + 0.2f) * 0.95f), false,
            art == 2 ? tc : new Color(1f, 0.75f, 0.2f));
    }

    /// Duvar saati: siyah çerçeve, beyaz kadran, 60 dakika çizgisi, rakamlar, akrep-yelkovan-saniye (10:10), cam.
    static void WallClock(Transform parent, Vector3 pos, float readerYaw)
    {
        var c = new GameObject("Duvar Saati").transform;
        c.SetParent(parent, false);
        c.SetPositionAndRotation(pos, Quaternion.Euler(0f, readerYaw, 0f));
        // Okuyucu +z'ye bakıyor; kadran -z tarafında
        var ink = Pbr("P_SaatMurekkep", new Color(0.05f, 0.05f, 0.05f), 0.3f);
        Prim(PrimitiveType.Cylinder, c, "Çerçeve", new Vector3(0f, 0f, 0.012f), new Vector3(0.38f, 0.022f, 0.38f), MBlackPlastic, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, c, "Kadran", new Vector3(0f, 0f, -0.0105f), new Vector3(0.34f, 0.001f, 0.34f), MWhitePlastic, false, new Vector3(90f, 0f, 0f));

        var cube = RoundedBoxMesh(Vector3.one, 0f, 1f);
        var ticks = new List<Matrix4x4>();
        for (int i = 0; i < 60; i++)
        {
            float a = i * 6f * Mathf.Deg2Rad;
            bool major = i % 5 == 0;
            float len = major ? 0.022f : 0.008f, r = 0.155f - len / 2f;
            ticks.Add(Matrix4x4.TRS(new Vector3(Mathf.Sin(a) * r, Mathf.Cos(a) * r, -0.0115f), Quaternion.Euler(0f, 0f, -i * 6f),
                new Vector3(major ? 0.006f : 0.0025f, len, 0.001f)));
        }
        CombinedMeshes(c, "Dakika Çizgileri", cube, ticks, ink);
        string[] nums = { "12", "3", "6", "9" };
        for (int i = 0; i < 4; i++)
        {
            float a = i * 90f * Mathf.Deg2Rad;
            Label(c, nums[i], c.TransformPoint(new Vector3(Mathf.Sin(a) * 0.11f, Mathf.Cos(a) * 0.11f, -0.012f)), readerYaw, 0.2f, false, new Color(0.05f, 0.05f, 0.05f));
        }

        void Hand(string name, float angleDeg, float length, float width, float z, Material m)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            var center = new Vector3(Mathf.Sin(a) * length * 0.38f, Mathf.Cos(a) * length * 0.38f, z);
            RBox(c, name, center, new Vector3(width, length, 0.0015f), m, width * 0.45f, false, 1f, new Vector3(0f, 0f, -angleDeg));
        }
        Hand("Akrep", 305f, 0.09f, 0.009f, -0.013f, ink);
        Hand("Yelkovan", 60f, 0.13f, 0.006f, -0.0145f, ink);
        Hand("Saniye", 200f, 0.14f, 0.002f, -0.016f, Pbr("P_SaniyeKirmizi", new Color(0.85f, 0.1f, 0.08f), 0.4f));
        Prim(PrimitiveType.Cylinder, c, "Göbek", new Vector3(0f, 0f, -0.017f), new Vector3(0.012f, 0.002f, 0.012f), MBlackPlastic, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, c, "Cam", new Vector3(0f, 0f, -0.02f), new Vector3(0.345f, 0.001f, 0.345f), MGlassClean, false, new Vector3(90f, 0f, 0f));
    }

    /// Saksıda ficus: parlak seramik saksı, toprak, ince gövde ve dallar boyunca dizilmiş onlarca yaprak.
    static void Plant(Transform parent, Vector3 pos, float scale, int seed)
    {
        var p = new GameObject("Saksı Bitkisi").transform;
        p.SetParent(parent, false);
        p.position = pos;
        p.localScale = Vector3.one * scale;
        var ceramic = Pbr("P_Seramik", new Color(0.93f, 0.92f, 0.9f), 0.85f);
        var trunk = Pbr("P_BitkiGovde", new Color(0.36f, 0.27f, 0.18f), 0.2f);
        var leafDark = Pbr("P_YaprakKoyu", new Color(0.09f, 0.26f, 0.08f), 0.55f);
        var leafLight = Pbr("P_YaprakAcik", new Color(0.18f, 0.4f, 0.12f), 0.55f);

        Prim(PrimitiveType.Cylinder, p, "Saksı", new Vector3(0f, 0.19f, 0f), new Vector3(0.34f, 0.19f, 0.34f), ceramic, true);
        Prim(PrimitiveType.Cylinder, p, "Saksı Ağzı", new Vector3(0f, 0.375f, 0f), new Vector3(0.36f, 0.012f, 0.36f), ceramic, false);
        Prim(PrimitiveType.Cylinder, p, "Toprak", new Vector3(0f, 0.37f, 0f), new Vector3(0.32f, 0.008f, 0.32f), Pbr("P_Toprak", new Color(0.16f, 0.11f, 0.07f), 0.05f), false);
        Prim(PrimitiveType.Cylinder, p, "Gövde", new Vector3(0f, 0.7f, 0f), new Vector3(0.025f, 0.34f, 0.025f), trunk, false);

        var rng = new System.Random(seed * 97 + 5);
        var dark = new List<Matrix4x4>();
        var light = new List<Matrix4x4>();
        for (int i = 0; i < 70; i++)
        {
            float t = i / 69f;
            float yaw = i * 137.5f + Rand(rng) * 20f;                 // altın açı dizilimi
            float y = 0.55f + t * 0.75f;
            float radius = 0.05f + Mathf.Sin(t * Mathf.PI) * 0.17f + Rand(rng) * 0.04f;
            float tilt = Mathf.Lerp(25f, -10f, t) + (Rand(rng) - 0.5f) * 30f;
            var rot = Quaternion.Euler(0f, yaw, 0f) * Quaternion.Euler(0f, 0f, tilt) * Quaternion.Euler(Rand(rng) * 30f - 15f, 0f, 0f);
            var stem = new Vector3(0f, y, 0f) + Quaternion.Euler(0f, yaw, 0f) * new Vector3(radius * 0.6f, 0f, 0f);
            var leafPos = stem + rot * new Vector3(0.06f, 0f, 0f);
            var m = Matrix4x4.TRS(leafPos, rot, new Vector3(0.14f, 0.008f, 0.075f) * (0.85f + Rand(rng) * 0.3f));
            (Rand(rng) < 0.5f ? dark : light).Add(m);
        }
        CombinedMeshes(p, "Yapraklar (koyu)", SphereMesh, dark, leafDark);
        CombinedMeshes(p, "Yapraklar (açık)", SphereMesh, light, leafLight);
    }

    static void DoorMat(Transform parent, Vector3 pos)
    {
        var coir = FeltPair();
        RBox(parent, "Paspas Kenarı", pos + new Vector3(0f, 0.004f, 0f), new Vector3(1.4f, 0.008f, 0.9f), MRubber, 0.004f);
        RBox(parent, "Paspas", pos + new Vector3(0f, 0.007f, 0f), new Vector3(1.32f, 0.008f, 0.82f),
            Pbr("P_Paspas", new Color(0.2f, 0.19f, 0.18f), 0.05f, 0f, coir.color, coir.normal, 1.2f), 0.003f, false, 0.3f);
    }

    // =====================================================================
    // KASA TEZGÂHI (L şeklinde) — yazar kasa, müşteri ekranı, kart terminali, barkod okuyucu
    // =====================================================================

    static void ShopCounter(Transform shop)
    {
        // Ana kol: x 3.85…4.55, z 0.9…4.3 — müşteri -x tarafında. Dönüş kolu: x 4.55…6.3, z 3.6…4.3.
        var c = new GameObject("Kasa Tezgâhı").transform;
        c.SetParent(shop, false);
        c.position = new Vector3(4.2f, FloorY, 2.6f);
        const float ty = 1.04f;
        var backing = Pbr("P_KoyuPanel", new Color(0.08f, 0.09f, 0.1f), 0.3f);
        var slatMesh = RoundedBoxMesh(new Vector3(0.018f, 0.8f, 0.036f), 0.005f, 1f);
        var slatMeshX = RoundedBoxMesh(new Vector3(0.036f, 0.8f, 0.018f), 0.005f, 1f);

        // Ana kol gövdesi
        RBox(c, "Süpürgelik", new Vector3(0.02f, 0.05f, 0f), new Vector3(0.62f, 0.1f, 3.34f), MMattePlastic, 0.004f);
        RBox(c, "Gövde", new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.9f, 3.4f), backing, 0.008f, true);
        var slats = new List<Matrix4x4>();
        for (float z = -1.65f; z <= 1.65f; z += 0.06f)
            slats.Add(Matrix4x4.Translate(new Vector3(-0.36f, 0.55f, z)));
        CombinedMeshes(c, "Çıta Kaplama (ön)", slatMesh, slats, MWalnut);
        RBox(c, "Tezgâh Üstü (kuvars)", new Vector3(0f, 1.02f, 0f), new Vector3(0.82f, 0.04f, 3.48f), MQuartz, 0.008f, false, 0.6f);

        // Logo plakası (çıtaların üstünde fırçalı alüminyum)
        RBox(c, "Logo Plakası", new Vector3(-0.375f, 0.66f, 0f), new Vector3(0.008f, 0.16f, 1.25f), MBrushed, 0.003f, false, 0.5f);
        Label(c, "BİLGİSAYAR DÜKKANI", c.TransformPoint(new Vector3(-0.381f, 0.66f, 0f)), 90f, 0.45f, false, new Color(0.08f, 0.09f, 0.1f));

        // Dönüş kolu
        RBox(c, "Süpürgelik (dönüş)", new Vector3(1.225f, 0.05f, 1.33f), new Vector3(1.75f, 0.1f, 0.62f), MMattePlastic, 0.004f);
        RBox(c, "Gövde (dönüş)", new Vector3(1.225f, 0.55f, 1.35f), new Vector3(1.75f, 0.9f, 0.7f), backing, 0.008f, true);
        var slats2 = new List<Matrix4x4>();
        for (float x = 0.38f; x <= 2.08f; x += 0.06f)
            slats2.Add(Matrix4x4.Translate(new Vector3(x, 0.55f, 1.71f)));
        CombinedMeshes(c, "Çıta Kaplama (dönüş)", slatMeshX, slats2, MWalnut);
        RBox(c, "Tezgâh Üstü (dönüş)", new Vector3(1.225f, 1.02f, 1.35f), new Vector3(1.85f, 0.04f, 0.82f), MQuartz, 0.008f, false, 0.6f);
        RBox(c, "Uç Paneli", new Vector3(2.1f, 0.55f, 1.35f), new Vector3(0.02f, 0.9f, 0.7f), MWalnut, 0.005f);

        // Satış yuvaları (müşteri tarafında, ürün buraya konup satılacak)
        CreateSlot(c, "Kasa 1", new Vector3(-0.05f, ty, -1.1f));
        CreateSlot(c, "Kasa 2", new Vector3(-0.05f, ty, -0.3f));

        // Para çekmecesi
        RBox(c, "Para Çekmecesi", new Vector3(0.12f, ty + 0.053f, 0.75f), new Vector3(0.41f, 0.105f, 0.42f), MBlackPlastic, 0.01f);
        RBox(c, "Çekmece Yarığı", new Vector3(0.325f, ty + 0.07f, 0.75f), new Vector3(0.004f, 0.004f, 0.38f), MMattePlastic, 0.001f);
        Prim(PrimitiveType.Cylinder, c, "Çekmece Kilidi", new Vector3(0.327f, ty + 0.04f, 0.75f), new Vector3(0.016f, 0.003f, 0.016f), MChrome, false, new Vector3(0f, 0f, 90f));

        // Dokunmatik POS ekranı (kasiyere dönük, 15° geriye eğik)
        RBox(c, "POS Tabanı", new Vector3(0.12f, ty + 0.118f, 0.75f), new Vector3(0.18f, 0.025f, 0.2f), MGrayPlastic, 0.01f);
        RBox(c, "POS Boynu", new Vector3(0.13f, ty + 0.2f, 0.75f), new Vector3(0.05f, 0.15f, 0.06f), MGrayPlastic, 0.012f, false, 1f, new Vector3(0f, 0f, 10f));
        RBox(c, "POS Ekranı", new Vector3(0.15f, ty + 0.33f, 0.75f), new Vector3(0.035f, 0.26f, 0.37f), MBlackPlastic, 0.01f, false, 1f, new Vector3(0f, 0f, 15f));
        RBox(c, "POS Görüntüsü", new Vector3(0.167f, ty + 0.3345f, 0.75f), new Vector3(0.003f, 0.225f, 0.33f), PbrGlow("P_POSEkran", new Color(0.18f, 0.45f, 0.78f), 1.4f), 0.001f, false, 1f, new Vector3(0f, 0f, 15f));

        // Fiş yazıcı
        RBox(c, "Fiş Yazıcı", new Vector3(0.15f, ty + 0.065f, 1.12f), new Vector3(0.15f, 0.13f, 0.19f), MGrayPlastic, 0.022f);
        RBox(c, "Yazıcı Kapak Çizgisi", new Vector3(0.11f, ty + 0.131f, 1.12f), new Vector3(0.004f, 0.002f, 0.16f), MMattePlastic, 0.001f);
        RBox(c, "Fiş", new Vector3(0.07f, ty + 0.15f, 1.12f), new Vector3(0.002f, 0.06f, 0.075f), MWhitePlastic, 0.0005f, false, 1f, new Vector3(0f, 0f, 25f));

        // Müşteri ekranı (direkli)
        Prim(PrimitiveType.Cylinder, c, "Müşteri Ekranı Direği", new Vector3(-0.08f, ty + 0.2f, 1.0f), new Vector3(0.025f, 0.2f, 0.025f), MChrome, false);
        RBox(c, "Müşteri Ekranı", new Vector3(-0.08f, ty + 0.44f, 1.0f), new Vector3(0.05f, 0.1f, 0.3f), MBlackPlastic, 0.012f);
        RBox(c, "Müşteri Ekranı Yazısı", new Vector3(-0.106f, ty + 0.44f, 1.0f), new Vector3(0.003f, 0.065f, 0.26f), PbrGlow("P_VFDEkran", new Color(0.12f, 0.8f, 0.7f), 1.3f), 0.001f);
        Label(c, "HOŞ GELDİNİZ", c.TransformPoint(new Vector3(-0.112f, ty + 0.44f, 1.0f)), 90f, 0.14f, false, new Color(0.03f, 0.18f, 0.16f));

        // Kart terminali (müşteri tarafı): tuş takımı + ekran
        var term = new GameObject("Kart Terminali").transform;
        term.SetParent(c, false);
        term.localPosition = new Vector3(-0.22f, ty, 0.45f);
        term.localRotation = Quaternion.Euler(0f, 0f, 12f);
        RBox(term, "Gövde", new Vector3(0f, 0.022f, 0f), new Vector3(0.085f, 0.044f, 0.17f), MBlackPlastic, 0.014f);
        RBox(term, "Ekran", new Vector3(0f, 0.0445f, -0.045f), new Vector3(0.055f, 0.002f, 0.05f), PbrGlow("P_KartEkran", new Color(0.3f, 0.7f, 0.45f), 1.2f), 0.001f);
        var keys = new List<Matrix4x4>();
        for (int r = 0; r < 4; r++)
            for (int k = 0; k < 3; k++)
                keys.Add(Matrix4x4.Translate(new Vector3(-0.02f + k * 0.02f, 0.045f, 0.0f + r * 0.018f)));
        CombinedMeshes(term, "Tuşlar", RoundedBoxMesh(new Vector3(0.015f, 0.004f, 0.013f), 0.002f, 1f), keys, MGrayPlastic);

        // El tipi barkod okuyucu, sehpasında
        var scan = new GameObject("Barkod Okuyucu").transform;
        scan.SetParent(c, false);
        scan.localPosition = new Vector3(0.05f, ty, 0.28f);
        RBox(scan, "Sehpa", new Vector3(0f, 0.015f, 0f), new Vector3(0.08f, 0.03f, 0.1f), MMattePlastic, 0.012f);
        RBox(scan, "Sap", new Vector3(0.01f, 0.08f, 0f), new Vector3(0.035f, 0.11f, 0.045f), MBlackPlastic, 0.014f, false, 1f, new Vector3(0f, 0f, -15f));
        RBox(scan, "Başlık", new Vector3(-0.02f, 0.15f, 0f), new Vector3(0.08f, 0.05f, 0.075f), MBlackPlastic, 0.018f, false, 1f, new Vector3(0f, 0f, 20f));
        RBox(scan, "Okuma Penceresi", new Vector3(-0.058f, 0.135f, 0f), new Vector3(0.004f, 0.03f, 0.055f), PbrGlow("P_BarkodKirmizi", new Color(1f, 0.1f, 0.05f), 2f), 0.002f, false, 1f, new Vector3(0f, 0f, 20f));

        // Dönüş kolunda: akrilik standda fırsat ürünleri, kalemlik, yapışkan notlar, poşetler
        var acrylic = MGlassClean;
        RBox(c, "Akrilik Stand", new Vector3(1.05f, ty + 0.08f, 1.3f), new Vector3(0.8f, 0.004f, 0.25f), acrylic, 0.002f);
        RBox(c, "Akrilik Stand Arkası", new Vector3(1.05f, ty + 0.08f, 1.18f), new Vector3(0.8f, 0.16f, 0.004f), acrylic, 0.002f);
        Color[] impulse = { new Color(0.8f, 0.12f, 0.1f), new Color(0.1f, 0.3f, 0.7f), new Color(0.1f, 0.1f, 0.11f), new Color(0.95f, 0.75f, 0.1f) };
        for (int i = 0; i < 8; i++)
        {
            var s = new Vector3(0.13f, 0.065f, 0.17f);
            var at = new Vector3(0.75f + (i % 4) * 0.2f, i < 4 ? ty + s.y / 2f : ty + 0.082f + s.y / 2f, 1.3f);
            RBox(c, "Fırsat Ürünü", at, s, Pbr("P_FirsatUrun_" + (i % 4), impulse[i % 4], 0.5f), 0.006f);
            RBox(c, "Ürün Etiketi", at + new Vector3(0f, 0f, 0.001f), new Vector3(s.x * 0.6f, s.y * 0.5f, s.z), Pbr("P_KutuEtiketi", new Color(0.95f, 0.95f, 0.93f), 0.3f), 0.003f);
        }
        Label(c, "FIRSAT", c.TransformPoint(new Vector3(1.05f, ty + 0.21f, 1.17f)), 180f, 0.3f, true, Color.black, new Color(1f, 0.85f, 0.1f));
        Prim(PrimitiveType.Cylinder, c, "Kalemlik", new Vector3(1.75f, ty + 0.05f, 1.15f), new Vector3(0.07f, 0.05f, 0.07f), MBlackMetal, false);
        Color[] pens = { new Color(0.1f, 0.2f, 0.7f), new Color(0.8f, 0.1f, 0.1f), new Color(0.1f, 0.1f, 0.1f) };
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, c, "Kalem", new Vector3(1.74f + i * 0.012f, ty + 0.12f, 1.15f + (i - 1) * 0.01f), new Vector3(0.008f, 0.07f, 0.008f),
                Pbr("P_Kalem_" + i, pens[i], 0.6f), false, new Vector3((i - 1) * 8f, 0f, (i - 1) * 6f));
        RBox(c, "Yapışkan Notlar", new Vector3(1.75f, ty + 0.012f, 1.4f), new Vector3(0.075f, 0.024f, 0.075f), Pbr("P_YapiskanNot", new Color(1f, 0.92f, 0.35f), 0.2f), 0.002f);
        RBox(c, "Poşet Destesi", new Vector3(1.85f, ty + 0.02f, 1.55f), new Vector3(0.3f, 0.04f, 0.2f), MWhitePlastic, 0.01f);

        // Kasiyer taburesi (personel tarafı)
        Stool(shop, new Vector3(5.05f, FloorY, 2.6f), 0.72f);
    }

    /// Krom bar taburesi: taban diski, kolon, ayak halkası, deri minder.
    static void Stool(Transform parent, Vector3 pos, float height)
    {
        var s = new GameObject("Tabure").transform;
        s.SetParent(parent, false);
        s.position = pos;
        Prim(PrimitiveType.Cylinder, s, "Taban", new Vector3(0f, 0.012f, 0f), new Vector3(0.42f, 0.012f, 0.42f), MChrome, true);
        Prim(PrimitiveType.Cylinder, s, "Kolon", new Vector3(0f, height / 2f, 0f), new Vector3(0.05f, height / 2f, 0.05f), MChrome, false);
        const int segs = 14;
        float ringR = 0.17f, ringY = height * 0.45f;
        for (int i = 0; i < segs; i++)
        {
            float a = i * 360f / segs;
            var p = Quaternion.Euler(0f, a, 0f) * new Vector3(ringR, ringY, 0f);
            Prim(PrimitiveType.Cylinder, s, "Ayak Halkası", p, new Vector3(0.016f, ringR * Mathf.PI / segs + 0.002f, 0.016f), MChrome, false, new Vector3(90f, a, 0f));
        }
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            var p = Quaternion.Euler(0f, a, 0f) * new Vector3(ringR / 2f, ringY, 0f);
            RBox(s, "Halka Kolu", p, new Vector3(ringR, 0.012f, 0.012f), MChrome, 0.005f, false, 1f, new Vector3(0f, a, 0f));
        }
        Prim(PrimitiveType.Cylinder, s, "Minder", new Vector3(0f, height + 0.02f, 0f), new Vector3(0.4f, 0.03f, 0.4f), MLeather, true);
        Prim(PrimitiveType.Sphere, s, "Minder Üstü", new Vector3(0f, height + 0.05f, 0f), new Vector3(0.4f, 0.04f, 0.4f), MLeather, false);
    }

    // =====================================================================
    // DÜKKÂN BİLGİSAYARI — tamirhanede, satış alanıyla arasındaki bölme duvarında (ortanın biraz solunda)
    // =====================================================================

    static void ShopComputerDesk(Transform shop)
    {
        // Personel alanında (kasanın arkası) iki kapaklı dolap
        var cab = new GameObject("Dolap").transform;
        cab.SetParent(shop, false);
        cab.SetPositionAndRotation(new Vector3(MaxX - 0.1f - 0.25f, FloorY, 0.55f), Quaternion.Euler(0f, -90f, 0f));
        Cabinet(cab, new Vector3(0.7f, 1.9f, 0.48f));

        // Masa bölme duvarına (z = 0, tamirhane yüzü) yaslı; yerel +z odaya (-z) bakar.
        var d = new GameObject("Dükkân Bilgisayarı").transform;
        d.SetParent(shop, false);
        d.SetPositionAndRotation(new Vector3(-4f, FloorY, SplitZ - 0.06f - 0.35f), Quaternion.Euler(0f, 180f, 0f));
        float yaw = d.eulerAngles.y;

        // Masa: meşe tabla, siyah metal çerçeve ayaklar, arka panel
        RBox(d, "Masa Üstü", new Vector3(0f, 0.74f, 0f), new Vector3(1.4f, 0.03f, 0.7f), MOak, 0.006f, true);
        foreach (float x in new[] { -0.66f, 0.66f })
        {
            RBox(d, "Ayak Çerçevesi", new Vector3(x, 0.36f, 0f), new Vector3(0.05f, 0.72f, 0.05f), MBlackMetal, 0.008f, false, 1f);
            RBox(d, "Ayak Tabanı", new Vector3(x, 0.015f, 0f), new Vector3(0.06f, 0.03f, 0.62f), MBlackMetal, 0.008f);
            RBox(d, "Üst Kiriş", new Vector3(x, 0.7f, 0f), new Vector3(0.05f, 0.04f, 0.6f), MBlackMetal, 0.006f);
            foreach (float z in new[] { -0.29f, 0.29f })
                Prim(PrimitiveType.Cylinder, d, "Ayar Ayağı", new Vector3(x, 0.004f, z), new Vector3(0.04f, 0.004f, 0.04f), MRubber, false);
        }
        RBox(d, "Arka Panel", new Vector3(0f, 0.45f, -0.3f), new Vector3(1.26f, 0.45f, 0.015f), MBlackMetal, 0.004f);

        // Monitör: ince çerçeve, arka kasa, ayak (ekran yerel +z'ye bakar)
        RBox(d, "Monitör Tabanı", new Vector3(0f, 0.762f, -0.15f), new Vector3(0.26f, 0.012f, 0.18f), MBrushed, 0.006f, false, 0.5f);
        RBox(d, "Monitör Boynu", new Vector3(0f, 0.88f, -0.19f), new Vector3(0.05f, 0.24f, 0.02f), MBrushed, 0.006f, false, 0.5f);
        RBox(d, "Monitör Arka Kasa", new Vector3(0f, 1.08f, -0.165f), new Vector3(0.4f, 0.24f, 0.03f), MBlackPlastic, 0.015f);
        RBox(d, "Monitör", new Vector3(0f, 1.085f, -0.14f), new Vector3(0.62f, 0.37f, 0.018f), MBlackPlastic, 0.005f, true);
        RBox(d, "Ekran", new Vector3(0f, 1.092f, -0.1305f), new Vector3(0.605f, 0.34f, 0.002f), PbrGlow("P_PCDuvarKagidi", new Color(0.1f, 0.33f, 0.55f), 1.3f, 0.95f), 0.001f);
        RBox(d, "Görev Çubuğu", new Vector3(0f, 0.928f, -0.129f), new Vector3(0.605f, 0.016f, 0.001f), MBlackPlastic, 0f);
        var icon = PbrGlow("P_MasaustuSimge", new Color(0.95f, 0.55f, 0.15f), 1.5f);
        for (int i = 0; i < 4; i++)
            RBox(d, "Masaüstü Simgesi", new Vector3(-0.27f, 1.22f - i * 0.055f, -0.129f), new Vector3(0.03f, 0.03f, 0.001f), icon, 0.004f);
        Label(d, "Pencere OS", d.TransformPoint(new Vector3(0.02f, 1.1f, -0.1285f)), yaw + 180f, 0.15f, false, new Color(1f, 1f, 1f, 0.75f));
        RBox(d, "Güç Işığı", new Vector3(0.28f, 0.904f, -0.13f), new Vector3(0.008f, 0.004f, 0.002f), PbrGlow("P_GucLed", new Color(0.2f, 0.6f, 1f), 4f), 0.001f);

        // Klavye (tek tek tuşlar), fare, mousepad
        RBox(d, "Klavye", new Vector3(-0.05f, 0.765f, 0.12f), new Vector3(0.44f, 0.02f, 0.145f), MMattePlastic, 0.006f, true);
        var keyMesh = RoundedBoxMesh(new Vector3(0.016f, 0.01f, 0.016f), 0.003f, 1f);
        var keys = new List<Matrix4x4>();
        for (int r = 0; r < 5; r++)
            for (int k = 0; k < 22; k++)
            {
                if (r == 4 && k > 3 && k < 11) continue; // boşluk tuşu yeri
                keys.Add(Matrix4x4.Translate(new Vector3(-0.25f + k * 0.0185f, 0.778f, 0.07f + r * 0.0225f)));
            }
        keys.Add(Matrix4x4.TRS(new Vector3(-0.25f + 7f * 0.0185f, 0.778f, 0.07f + 4f * 0.0225f), Quaternion.identity, new Vector3(7.9f, 1f, 1f)));
        CombinedMeshes(d, "Tuşlar", keyMesh, keys, MBlackPlastic);
        RBox(d, "Mousepad", new Vector3(0.33f, 0.757f, 0.13f), new Vector3(0.25f, 0.003f, 0.21f), Pbr("P_Mousepad", ShopAccent * 0.7f, 0.1f, 0f, FeltPair().color, FeltPair().normal, 0.3f), 0.002f, false, 0.3f);
        Mouse(d, new Vector3(0.33f, 0.759f, 0.13f), MBlackPlastic, 0f);

        // Hoparlörler
        foreach (float x in new[] { -0.47f, 0.47f })
        {
            RBox(d, "Hoparlör", new Vector3(x, 0.845f, -0.2f), new Vector3(0.1f, 0.17f, 0.11f), MMattePlastic, 0.012f);
            Prim(PrimitiveType.Cylinder, d, "Woofer", new Vector3(x, 0.82f, -0.144f), new Vector3(0.07f, 0.002f, 0.07f), MRubber, false, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, d, "Tweeter", new Vector3(x, 0.895f, -0.144f), new Vector3(0.025f, 0.002f, 0.025f), MBrushed, false, new Vector3(90f, 0f, 0f));
        }

        // Kupa (kulplu), kâğıtlar, not defteri
        Prim(PrimitiveType.Cylinder, d, "Kupa", new Vector3(-0.58f, 0.8f, 0.12f), new Vector3(0.08f, 0.048f, 0.08f), Pbr("P_Kupa", new Color(0.85f, 0.2f, 0.15f), 0.85f), false);
        RBox(d, "Kupa Kulpu", new Vector3(-0.63f, 0.8f, 0.12f), new Vector3(0.015f, 0.05f, 0.012f), Pbr("P_Kupa", new Color(0.85f, 0.2f, 0.15f), 0.85f), 0.006f);
        Prim(PrimitiveType.Cylinder, d, "Kahve", new Vector3(-0.58f, 0.842f, 0.12f), new Vector3(0.07f, 0.001f, 0.07f), Pbr("P_Kahve", new Color(0.12f, 0.06f, 0.03f), 0.95f), false);
        RBox(d, "Kâğıtlar", new Vector3(0.58f, 0.758f, -0.08f), new Vector3(0.21f, 0.005f, 0.3f), MWhitePlastic, 0.001f, false, 1f, new Vector3(0f, 8f, 0f));
        RBox(d, "Not Defteri", new Vector3(-0.42f, 0.762f, 0.15f), new Vector3(0.15f, 0.014f, 0.21f), Pbr("P_Defter", new Color(0.12f, 0.2f, 0.35f), 0.3f), 0.004f, false, 1f, new Vector3(0f, -12f, 0f));

        PcTower(d, new Vector3(0.45f, 0f, -0.06f));
        OfficeChair(d, new Vector3(0f, 0f, 0.75f));

        // Üstte duvar rafı: klasörler (sırt etiketli) + kutu
        RBox(d, "Duvar Rafı", new Vector3(0f, 1.65f, -0.22f), new Vector3(1.2f, 0.025f, 0.26f), MOak, 0.004f);
        foreach (float x in new[] { -0.5f, 0.5f })
            RBox(d, "Raf Konsolu", new Vector3(x, 1.61f, -0.24f), new Vector3(0.012f, 0.06f, 0.2f), MBlackMetal, 0.003f);
        Color[] binders = { new Color(0.15f, 0.3f, 0.6f), new Color(0.75f, 0.2f, 0.15f), new Color(0.2f, 0.5f, 0.25f) };
        var binderMesh = RoundedBoxMesh(new Vector3(0.06f, 0.29f, 0.22f), 0.006f, 1f);
        var labelMesh = RoundedBoxMesh(new Vector3(0.035f, 0.12f, 0.002f), 0.001f, 1f);
        var labelList = new List<Matrix4x4>();
        for (int b = 0; b < binders.Length; b++)
        {
            var list = new List<Matrix4x4>();
            for (int i = b; i < 9; i += binders.Length)
            {
                list.Add(Matrix4x4.Translate(new Vector3(-0.5f + i * 0.066f, 1.808f, -0.22f)));
                labelList.Add(Matrix4x4.Translate(new Vector3(-0.5f + i * 0.066f, 1.84f, -0.109f)));
            }
            CombinedMeshes(d, "Klasörler", binderMesh, list, Pbr("P_Klasor_" + b, binders[b], 0.4f));
        }
        CombinedMeshes(d, "Klasör Etiketleri", labelMesh, labelList, MWhitePlastic);
        RBox(d, "Kutu", new Vector3(0.35f, 1.74f, -0.22f), new Vector3(0.3f, 0.15f, 0.2f), Pbr("P_Karton", new Color(0.66f, 0.5f, 0.33f), 0.2f), 0.005f);

        d.gameObject.AddComponent<ShopComputer>();
    }

    /// Masa altında oyuncu kasası: temperli cam yan panel, içinde ekran kartı ve RGB fanlar, ön panelde fanlar.
    static void PcTower(Transform parent, Vector3 floorPos)
    {
        var t = new GameObject("Bilgisayar Kasası").transform;
        t.SetParent(parent, false);
        t.localPosition = floorPos;
        var rgb = PbrGlow("P_RgbMavi", new Color(0.25f, 0.45f, 1f), 3f);
        RBox(t, "Kasa", new Vector3(0f, 0.235f, 0f), new Vector3(0.21f, 0.45f, 0.44f), MBlackMetal, 0.012f, true);
        RBox(t, "Ön Panel", new Vector3(0f, 0.235f, 0.222f), new Vector3(0.2f, 0.44f, 0.008f), Pbr("P_OnPanelFile", new Color(0.04f, 0.04f, 0.045f), 0.2f, 0f, FeltPair().color, FeltPair().normal, 1.5f), 0.006f, false, 0.1f);
        foreach (float y in new[] { 0.13f, 0.27f })
        {
            Prim(PrimitiveType.Cylinder, t, "Ön Fan Halkası", new Vector3(0f, y, 0.2275f), new Vector3(0.12f, 0.001f, 0.12f), rgb, false, new Vector3(90f, 0f, 0f));
            Prim(PrimitiveType.Cylinder, t, "Ön Fan", new Vector3(0f, y, 0.228f), new Vector3(0.1f, 0.001f, 0.1f), MMattePlastic, false, new Vector3(90f, 0f, 0f));
        }
        RBox(t, "Güç Düğmesi", new Vector3(0f, 0.43f, 0.2f), new Vector3(0.02f, 0.006f, 0.02f), PbrGlow("P_GucLed", new Color(0.2f, 0.6f, 1f), 4f), 0.003f);
        // Odaya bakan yan: temperli cam ve iç donanım (yerel +x yüzü)
        RBox(t, "Temperli Cam", new Vector3(0.106f, 0.235f, 0f), new Vector3(0.004f, 0.42f, 0.42f), MGlassClean, 0.002f);
        RBox(t, "Ekran Kartı", new Vector3(0.05f, 0.2f, 0f), new Vector3(0.05f, 0.05f, 0.3f), MBlackPlastic, 0.006f);
        RBox(t, "Ekran Kartı Işığı", new Vector3(0.076f, 0.2f, 0f), new Vector3(0.002f, 0.006f, 0.28f), rgb, 0.001f);
        RBox(t, "Anakart", new Vector3(-0.09f, 0.28f, 0.0f), new Vector3(0.01f, 0.3f, 0.3f), Pbr("P_Anakart", new Color(0.08f, 0.1f, 0.1f), 0.5f), 0.002f);
        RBox(t, "İşlemci Soğutucu", new Vector3(-0.04f, 0.33f, 0.02f), new Vector3(0.09f, 0.11f, 0.11f), MBrushed, 0.01f, false, 0.2f);
        RBox(t, "RAM Işığı", new Vector3(-0.06f, 0.36f, -0.08f), new Vector3(0.04f, 0.01f, 0.04f), PbrGlow("P_RgbMor", new Color(0.7f, 0.2f, 1f), 3f), 0.002f);
    }

    /// Ofis koltuğu: deri minder ve sırtlık, kolçaklar, amortisör, 5 kollu yıldız ayak ve tekerlekler.
    static void OfficeChair(Transform parent, Vector3 floorPos)
    {
        var ch = new GameObject("Ofis Koltuğu").transform;
        ch.SetParent(parent, false);
        ch.localPosition = floorPos;
        // Koltuk masaya (yerel -z) bakar
        for (int i = 0; i < 5; i++)
        {
            float a = i * 72f + 18f;
            var dir = Quaternion.Euler(0f, a, 0f);
            RBox(ch, "Yıldız Kolu", dir * new Vector3(0.14f, 0.07f, 0f), new Vector3(0.28f, 0.035f, 0.045f), MBlackPlastic, 0.012f, false, 1f, new Vector3(0f, a, 0f));
            Prim(PrimitiveType.Sphere, ch, "Tekerlek", dir * new Vector3(0.28f, 0.03f, 0f), new Vector3(0.05f, 0.05f, 0.05f), MBlackPlastic, false);
        }
        Prim(PrimitiveType.Cylinder, ch, "Amortisör Kılıfı", new Vector3(0f, 0.17f, 0f), new Vector3(0.06f, 0.09f, 0.06f), MBlackPlastic, false);
        Prim(PrimitiveType.Cylinder, ch, "Amortisör", new Vector3(0f, 0.32f, 0f), new Vector3(0.04f, 0.08f, 0.04f), MChrome, false);
        RBox(ch, "Mekanizma", new Vector3(0f, 0.41f, 0f), new Vector3(0.2f, 0.04f, 0.22f), MBlackMetal, 0.01f);
        RBox(ch, "Oturak", new Vector3(0f, 0.47f, 0f), new Vector3(0.5f, 0.08f, 0.48f), MLeather, 0.035f, true, 0.4f);
        RBox(ch, "Sırt Bağlantısı", new Vector3(0f, 0.5f, 0.26f), new Vector3(0.06f, 0.1f, 0.04f), MBlackMetal, 0.01f);
        RBox(ch, "Sırtlık", new Vector3(0f, 0.86f, 0.28f), new Vector3(0.46f, 0.6f, 0.08f), MLeather, 0.035f, false, 0.4f, new Vector3(-8f, 0f, 0f));
        RBox(ch, "Bel Desteği", new Vector3(0f, 0.72f, 0.235f), new Vector3(0.36f, 0.12f, 0.03f), MLeather, 0.014f, false, 0.4f, new Vector3(-8f, 0f, 0f));
        foreach (float sx in new[] { -1f, 1f })
        {
            RBox(ch, "Kolçak Direği", new Vector3(sx * 0.24f, 0.56f, 0.04f), new Vector3(0.03f, 0.17f, 0.04f), MBlackPlastic, 0.008f);
            RBox(ch, "Kolçak", new Vector3(sx * 0.24f, 0.65f, 0.02f), new Vector3(0.06f, 0.025f, 0.24f), MMattePlastic, 0.012f);
        }
    }

    /// İki kapaklı melamin dolap. Yerel +z = ön. size: genişlik, yükseklik, derinlik.
    static void Cabinet(Transform c, Vector3 size)
    {
        RBox(c, "Kaide", new Vector3(0f, 0.04f, -0.02f), new Vector3(size.x - 0.04f, 0.08f, size.z - 0.04f), MMattePlastic, 0.004f);
        RBox(c, "Gövde", new Vector3(0f, 0.08f + (size.y - 0.08f) / 2f, -0.01f), new Vector3(size.x, size.y - 0.08f, size.z - 0.02f), MMelamine, 0.008f, true);
        float doorW = size.x / 2f - 0.006f;
        foreach (float sx in new[] { -1f, 1f })
        {
            RBox(c, "Kapak", new Vector3(sx * (size.x / 4f + 0.0015f), 0.08f + (size.y - 0.08f) / 2f, size.z / 2f - 0.009f), new Vector3(doorW, size.y - 0.1f, 0.018f), MMelamine, 0.004f);
            RBox(c, "Kulp", new Vector3(sx * 0.04f, size.y * 0.55f, size.z / 2f + 0.012f), new Vector3(0.012f, 0.16f, 0.012f), MChrome, 0.005f);
            foreach (float y in new[] { 0.06f, size.y - 0.06f })
                RBox(c, "Kulp Ayağı", new Vector3(sx * 0.04f, size.y * 0.55f + (y > 0.5f ? 0.07f : -0.07f), size.z / 2f + 0.004f), new Vector3(0.01f, 0.01f, 0.016f), MChrome, 0.003f);
        }
    }

    // =====================================================================
    // TAMİRHANE
    // =====================================================================

    static void ShopWorkshop(Transform shop)
    {
        var room = new GameObject("Tamirhane").transform;
        room.SetParent(shop, false);

        // Arka duvar boyunca tek uzun tezgâh: sol kısmı tamir (pencerenin altı), sağ kısmı kurulum/test.
        // Yerel x = dünya x; tezgâh merkezi x = -4.5 → uçlar -7.6 … -1.4
        float wallZ = MinZ + 0.1f;
        var bench = Workbench(room, "Tamir ve Kurulum Tezgâhı", new Vector3(-4.5f, FloorY, wallZ + 0.375f), 0f, 6.2f);
        float[] repairX = { -2.3f, -1.5f, -0.7f };
        for (int i = 0; i < repairX.Length; i++) CreateSlot(bench, $"Tezgâh {i + 1}", new Vector3(repairX[i], 0.92f, 0.02f));
        float[] setupX = { 1.3f, 2.1f };
        for (int i = 0; i < setupX.Length; i++) CreateSlot(bench, $"Kurulum {i + 1}", new Vector3(setupX[i], 0.92f, 0.02f));

        // Kurulum köşesinin test monitörü (sağ uç)
        RBox(bench, "Monitör Tabanı", new Vector3(2.75f, 0.909f, -0.25f), new Vector3(0.22f, 0.01f, 0.15f), MBlackPlastic, 0.005f);
        RBox(bench, "Monitör Boynu", new Vector3(2.75f, 1.0f, -0.29f), new Vector3(0.04f, 0.18f, 0.02f), MBlackPlastic, 0.006f);
        RBox(bench, "Test Monitörü", new Vector3(2.75f, 1.17f, -0.27f), new Vector3(0.52f, 0.31f, 0.02f), MBlackPlastic, 0.005f);
        RBox(bench, "Test Ekranı", new Vector3(2.75f, 1.176f, -0.2595f), new Vector3(0.505f, 0.285f, 0.002f), PbrGlow("P_TestEkran", new Color(0.04f, 0.12f, 0.3f), 1f, 0.95f), 0.001f);
        Label(bench, "TEST ...  OK", bench.TransformPoint(new Vector3(2.75f, 1.18f, -0.258f)), 180f, 0.14f, false, new Color(0.4f, 1f, 0.5f));

        // Delikli alet panosu (pencerenin sağında, kurulum köşesinin üstünde) + parça kutusu rafı
        var pegCenter = new Vector3(-2.6f, 1.85f, wallZ + 0.012f);
        var peg = PegboardPair();
        UVBox(room, "Delikli Pano", pegCenter, new Vector3(2.4f, 1f, 0.02f), Pbr("P_DelikliPano", Color.white, 0.15f, 0f, peg.color, peg.normal, 1f), 0.5f, false);
        foreach (float sy in new[] { -0.51f, 0.51f })
            RBox(room, "Pano Çerçevesi", pegCenter + new Vector3(0f, sy, 0.012f), new Vector3(2.44f, 0.025f, 0.03f), MBrushed, 0.004f, false, 0.5f);
        foreach (float sx in new[] { -1.21f, 1.21f })
            RBox(room, "Pano Çerçevesi", pegCenter + new Vector3(sx, 0f, 0.012f), new Vector3(0.025f, 1.04f, 0.03f), MBrushed, 0.004f, false, 0.5f);
        PegboardTools(room, pegCenter + new Vector3(0f, 0f, 0.01f));
        RBox(room, "Kutu Rafı", new Vector3(-2.6f, 2.55f, wallZ + 0.14f), new Vector3(2.4f, 0.025f, 0.26f), MBlackMetal, 0.004f);
        foreach (float sx in new[] { -1.1f, 0f, 1.1f })
            RBox(room, "Raf Konsolu", new Vector3(-2.6f + sx, 2.5f, wallZ + 0.12f), new Vector3(0.012f, 0.08f, 0.2f), MBlackMetal, 0.003f);
        Bins(room, new Vector3(-2.6f, 2.5625f, wallZ + 0.14f), 2.2f, 8);

        DeskLamp(room, new Vector3(-7.35f, FloorY + 0.904f, wallZ + 0.25f));
        SolderingStation(room, new Vector3(-4.5f, FloorY + 0.904f, wallZ + 0.2f));
        Stool(room, new Vector3(-6f, FloorY, -4.7f), 0.65f);
        Stool(room, new Vector3(-2.8f, FloorY, -4.7f), 0.65f);

        // Tezgâh 2: sol duvarda, pencerenin altında
        var b2 = Workbench(room, "Tamir Tezgâhı 2", new Vector3(MinX + 0.1f + 0.375f, FloorY, -2.6f), 90f, 2.6f);
        AddSlotGrid(b2, "Tezgâh", 0.92f, 3, 1, 0.75f, 0f);
        Stool(room, new Vector3(-6.8f, FloorY, -2.6f), 0.65f);

        // Parça rafı (depo bölmesinin önünde)
        var rack = MetalShelf(room, "Parça Rafı", new Vector3(DepoX - 0.06f - 0.2f, FloorY, -4.8f), -90f, 1.6f, 0.4f, new[] { 0.3f, 0.8f, 1.3f, 1.8f }, null);
        foreach (float y in new[] { 0.3f, 0.8f, 1.3f, 1.8f })
            Bins(rack, new Vector3(0f, y, 0f), 1.4f, 5);

        FireExtinguisher(room, new Vector3(-0.2f, FloorY, MinZ + 0.1f));
        TrashBin(room, new Vector3(-7.45f, FloorY, -0.5f));
    }

    /// Atölye tezgâhı: kasap bloğu meşe tabla, antistatik mat (topraklama klipsli), kare profil ayaklar,
    /// çekmeceli metal dolaplar, alt raf. Yerel +z = ön.
    static Transform Workbench(Transform parent, string name, Vector3 pos, float yaw, float length)
    {
        var b = new GameObject(name).transform;
        b.SetParent(parent, false);
        b.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var frame = Pbr("P_TezgahCelik", new Color(0.33f, 0.36f, 0.4f), 0.5f, 0.6f);
        var drawerMat = Pbr("P_CekmeceGri", new Color(0.55f, 0.57f, 0.6f), 0.45f, 0.5f);

        RBox(b, "Tezgâh Üstü", new Vector3(0f, 0.875f, 0f), new Vector3(length, 0.05f, 0.75f), MOak, 0.008f, true);
        RBox(b, "Antistatik Mat", new Vector3(0f, 0.902f, 0.02f), new Vector3(length - 0.25f, 0.004f, 0.6f), Pbr("P_AntistatikMat", new Color(0.12f, 0.3f, 0.55f), 0.25f), 0.002f);
        RBox(b, "Topraklama Klipsi", new Vector3(length / 2f - 0.17f, 0.906f, 0.3f), new Vector3(0.02f, 0.006f, 0.02f), MChrome, 0.004f);

        var legX = new List<float> { -length / 2f + 0.05f, length / 2f - 0.05f };
        if (length > 4f) legX.Add(0f);
        foreach (float x in legX)
        foreach (float z in new[] { -0.3f, 0.3f })
        {
            RBox(b, "Ayak", new Vector3(x, 0.43f, z), new Vector3(0.05f, 0.84f, 0.05f), frame, 0.006f);
            Prim(PrimitiveType.Cylinder, b, "Ayar Ayağı", new Vector3(x, 0.006f, z), new Vector3(0.045f, 0.006f, 0.045f), MRubber, false);
        }
        RBox(b, "Arka Kiriş", new Vector3(0f, 0.82f, -0.33f), new Vector3(length - 0.1f, 0.06f, 0.03f), frame, 0.005f);
        RBox(b, "Alt Raf", new Vector3(0f, 0.2f, 0f), new Vector3(length - 0.12f, 0.025f, 0.6f), MOak, 0.005f);

        var cabinets = length > 4f ? new[] { -length / 2f + 0.35f, length / 2f - 0.35f } : new[] { length / 2f - 0.35f };
        foreach (float cx in cabinets)
        {
            RBox(b, "Çekmeceli Dolap", new Vector3(cx, 0.48f, -0.01f), new Vector3(0.5f, 0.7f, 0.64f), drawerMat, 0.01f);
            for (int i = 0; i < 3; i++)
            {
                float dy = 0.245f + i * 0.225f;
                RBox(b, "Çekmece", new Vector3(cx, dy, 0.317f), new Vector3(0.47f, 0.21f, 0.014f), drawerMat, 0.005f);
                RBox(b, "Çekmece Kulpu", new Vector3(cx, dy + 0.06f, 0.333f), new Vector3(0.16f, 0.014f, 0.014f), MChrome, 0.006f);
                RBox(b, "Etiketlik", new Vector3(cx, dy - 0.04f, 0.3255f), new Vector3(0.08f, 0.03f, 0.002f), MWhitePlastic, 0.002f);
            }
        }
        var col = b.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.43f, 0f);
        col.size = new Vector3(length, 0.85f, 0.75f);
        return b;
    }

    // ---------- Delikli panodaki aletler ----------
    // Her alet bir grup; grup orijini panodaki askı çengelinin dibi. Yerel: x sağ, y yukarı, +z panodan dışarı.

    static Material Steel => MChrome;
    static Material DarkSteel => Pbr("P_KoyuCelik", new Color(0.3f, 0.31f, 0.33f), 0.65f, 1f);
    static Material Rubber(string key, Color c) => Pbr("P_Sap_" + key, c, 0.35f);

    static Transform ToolGroup(Transform parent, string name, Vector3 pos, float rotZ = 0f)
    {
        var g = new GameObject(name).transform;
        g.SetParent(parent, false);
        g.localPosition = pos;
        g.localRotation = Quaternion.Euler(0f, 0f, rotZ);
        return g;
    }

    /// Panodan dışarı çıkan çengel (aletin asıldığı tel).
    static void Hook(Transform g, Vector3 at)
        => Prim(PrimitiveType.Cylinder, g, "Askı Çengeli", at + new Vector3(0f, 0f, 0.022f), new Vector3(0.006f, 0.022f, 0.006f), Steel, false, new Vector3(90f, 0f, 0f));

    static void PegboardTools(Transform parent, Vector3 center)
    {
        var red = new Color(0.82f, 0.12f, 0.1f);
        var yellow = new Color(0.95f, 0.72f, 0.08f);
        var blue = new Color(0.12f, 0.35f, 0.75f);
        var green = new Color(0.15f, 0.55f, 0.25f);
        var orange = new Color(0.95f, 0.45f, 0.08f);

        // Tornavida takımı: farklı boylar, düz ve yıldız uçlar
        float[] lens = { 0.08f, 0.11f, 0.14f, 0.11f, 0.08f, 0.17f };
        Color[] cols = { red, yellow, blue, red, green, yellow };
        for (int i = 0; i < lens.Length; i++)
            Screwdriver(parent, center + new Vector3(-1.06f + i * 0.075f, 0.36f, 0f), cols[i], lens[i], i % 2 == 1);

        Pliers(parent, center + new Vector3(-0.56f, 0.32f, 0f), red, 0);     // kombine pense
        Pliers(parent, center + new Vector3(-0.42f, 0.32f, 0f), blue, 1);    // kargaburun
        Pliers(parent, center + new Vector3(-0.28f, 0.32f, 0f), orange, 2);  // yan keski
        Wrench(parent, center + new Vector3(-0.12f, 0.35f, 0f));
        Hammer(parent, center + new Vector3(0.08f, 0.34f, 0f));
        Multimeter(parent, center + new Vector3(0.36f, 0.3f, 0f));
        TapeMeasure(parent, center + new Vector3(0.62f, 0.3f, 0f));
        UtilityKnife(parent, center + new Vector3(0.8f, 0.32f, 0f), yellow);
        Screwdriver(parent, center + new Vector3(0.95f, 0.36f, 0f), blue, 0.06f, true); // hassas tornavida
        Screwdriver(parent, center + new Vector3(1.02f, 0.36f, 0f), blue, 0.06f, false);

        // Alt sırada: termal macun şırıngası, kablo bağları
        var paste = ToolGroup(parent, "Termal Macun", center + new Vector3(0.62f, -0.1f, 0f));
        Hook(paste, Vector3.zero);
        Prim(PrimitiveType.Cylinder, paste, "Şırınga", new Vector3(0f, -0.06f, 0.03f), new Vector3(0.018f, 0.045f, 0.018f), MWhitePlastic, false);
        Prim(PrimitiveType.Cylinder, paste, "Macun", new Vector3(0f, -0.07f, 0.03f), new Vector3(0.019f, 0.025f, 0.019f), Pbr("P_Macun", new Color(0.7f, 0.72f, 0.75f), 0.6f, 0.3f), false);
        Prim(PrimitiveType.Cylinder, paste, "Uç", new Vector3(0f, -0.115f, 0.03f), new Vector3(0.006f, 0.01f, 0.006f), MWhitePlastic, false);
        var ties = ToolGroup(parent, "Kablo Bağları", center + new Vector3(0.85f, -0.1f, 0f));
        Hook(ties, Vector3.zero);
        for (int i = 0; i < 6; i++)
            RBox(ties, "Kablo Bağı", new Vector3(-0.012f + i * 0.005f, -0.09f, 0.03f), new Vector3(0.003f, 0.16f, 0.002f), MWhitePlastic, 0.001f);
    }

    /// Tornavida: lastik halkalı sap, metal bilezik, gövde, düz ya da yıldız uç. Uç aşağı bakar.
    static void Screwdriver(Transform parent, Vector3 pos, Color handle, float shaftLen, bool phillips)
    {
        var g = ToolGroup(parent, phillips ? "Yıldız Tornavida" : "Düz Tornavida", pos);
        Hook(g, Vector3.zero);
        float z = 0.03f;
        var grip = Rubber(ColorUtility.ToHtmlStringRGB(handle), handle);
        Prim(PrimitiveType.Sphere, g, "Sap Ucu", new Vector3(0f, -0.012f, z), new Vector3(0.026f, 0.016f, 0.026f), grip, false);
        Prim(PrimitiveType.Cylinder, g, "Sap", new Vector3(0f, -0.055f, z), new Vector3(0.028f, 0.042f, 0.028f), grip, false);
        foreach (float y in new[] { -0.035f, -0.06f, -0.085f })
            Prim(PrimitiveType.Cylinder, g, "Lastik Halka", new Vector3(0f, y, z), new Vector3(0.03f, 0.004f, 0.03f), MRubber, false);
        Prim(PrimitiveType.Cylinder, g, "Bilezik", new Vector3(0f, -0.1f, z), new Vector3(0.014f, 0.006f, 0.014f), Steel, false);
        Prim(PrimitiveType.Cylinder, g, "Gövde", new Vector3(0f, -0.106f - shaftLen / 2f, z), new Vector3(0.006f, shaftLen / 2f, 0.006f), Steel, false);
        float tipY = -0.106f - shaftLen - 0.006f;
        if (phillips)
        {
            SBox(g, "Yıldız Uç", new Vector3(0f, tipY, z), new Vector3(0.007f, 0.012f, 0.0025f), Steel, false);
            SBox(g, "Yıldız Uç", new Vector3(0f, tipY, z), new Vector3(0.0025f, 0.012f, 0.007f), Steel, false);
        }
        else
            SBox(g, "Düz Uç", new Vector3(0f, tipY, z), new Vector3(0.009f, 0.012f, 0.0018f), Steel, false);
    }

    /// Pense ailesi: iki kauçuk saplı kol, perçin pimi, çelik ağız. kind 0 = kombine, 1 = kargaburun, 2 = yan keski.
    static void Pliers(Transform parent, Vector3 pos, Color grip, int kind)
    {
        string[] names = { "Kombine Pense", "Kargaburun", "Yan Keski" };
        var g = ToolGroup(parent, names[kind], pos);
        Hook(g, new Vector3(-0.012f, 0f, 0f));
        Hook(g, new Vector3(0.012f, 0f, 0f));
        float z = 0.03f, pivotY = -0.17f;
        var rub = Rubber(ColorUtility.ToHtmlStringRGB(grip), grip);

        foreach (int side in new[] { -1, 1 })
        {
            float ang = side * -7f;
            RBox(g, "Kauçuk Sap", new Vector3(side * 0.016f, -0.07f, z), new Vector3(0.017f, 0.13f, 0.012f), rub, 0.0055f, false, 1f, new Vector3(0f, 0f, ang));
            RBox(g, "Sap Ucu", new Vector3(side * 0.021f, -0.005f, z), new Vector3(0.019f, 0.012f, 0.013f), rub, 0.005f, false, 1f, new Vector3(0f, 0f, ang));
            RBox(g, "Çelik Kol", new Vector3(side * 0.007f, -0.148f, z), new Vector3(0.011f, 0.035f, 0.01f), DarkSteel, 0.003f, false, 1f, new Vector3(0f, 0f, ang * 1.5f));
        }
        Prim(PrimitiveType.Cylinder, g, "Perçin", new Vector3(0f, pivotY, z), new Vector3(0.024f, 0.009f, 0.024f), DarkSteel, false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Perçin Başı", new Vector3(0f, pivotY, z + 0.01f), new Vector3(0.01f, 0.002f, 0.01f), Steel, false, new Vector3(90f, 0f, 0f));

        float jawLen = kind == 1 ? 0.075f : kind == 2 ? 0.028f : 0.05f;
        float jawW = kind == 1 ? 0.006f : kind == 2 ? 0.014f : 0.011f;
        foreach (int side in new[] { -1, 1 })
        {
            float taper = kind == 2 ? side * 12f : side * 3f;
            RBox(g, "Ağız", new Vector3(side * (jawW / 2f + 0.0005f), pivotY - 0.012f - jawLen / 2f, z),
                new Vector3(jawW, jawLen, 0.01f), DarkSteel, 0.0025f, false, 1f, new Vector3(0f, 0f, taper));
        }
        if (kind == 0)
            SBox(g, "Kesici", new Vector3(0f, pivotY - 0.02f, z + 0.0055f), new Vector3(0.02f, 0.006f, 0.001f), Steel, false);
    }

    /// Ayarlı İngiliz anahtarı: düz gövde, sabit ve hareketli çene, ayar vidası.
    static void Wrench(Transform parent, Vector3 pos)
    {
        var g = ToolGroup(parent, "İngiliz Anahtarı", pos, 4f);
        Hook(g, Vector3.zero);
        float z = 0.028f;
        Prim(PrimitiveType.Cylinder, g, "Asma Deliği", new Vector3(0f, -0.012f, z), new Vector3(0.012f, 0.005f, 0.012f), MBlackMetal, false, new Vector3(90f, 0f, 0f));
        RBox(g, "Gövde", new Vector3(0f, -0.1f, z), new Vector3(0.024f, 0.19f, 0.007f), Steel, 0.003f);
        RBox(g, "Baş", new Vector3(0.006f, -0.21f, z), new Vector3(0.05f, 0.04f, 0.011f), Steel, 0.005f);
        RBox(g, "Sabit Çene", new Vector3(-0.014f, -0.245f, z), new Vector3(0.016f, 0.035f, 0.011f), Steel, 0.004f);
        RBox(g, "Hareketli Çene", new Vector3(0.022f, -0.24f, z), new Vector3(0.014f, 0.028f, 0.01f), Steel, 0.004f);
        Prim(PrimitiveType.Cylinder, g, "Ayar Vidası", new Vector3(0.008f, -0.205f, z + 0.002f), new Vector3(0.012f, 0.009f, 0.012f), DarkSteel, false, new Vector3(0f, 0f, 90f));
    }

    /// Çekiç: çelik baş (çivi sökme pençeli), sap ve kauçuk tutma yeri. Başından asılı.
    static void Hammer(Transform parent, Vector3 pos)
    {
        var g = ToolGroup(parent, "Çekiç", pos);
        Hook(g, new Vector3(-0.02f, 0f, 0f));
        Hook(g, new Vector3(0.02f, 0f, 0f));
        float z = 0.03f;
        RBox(g, "Baş", new Vector3(0f, -0.018f, z), new Vector3(0.08f, 0.026f, 0.026f), DarkSteel, 0.005f);
        Prim(PrimitiveType.Cylinder, g, "Vurma Yüzü", new Vector3(0.05f, -0.018f, z), new Vector3(0.03f, 0.012f, 0.03f), Steel, false, new Vector3(0f, 0f, 90f));
        RBox(g, "Pençe", new Vector3(-0.055f, -0.01f, z), new Vector3(0.04f, 0.014f, 0.022f), DarkSteel, 0.004f, false, 1f, new Vector3(0f, 0f, -25f));
        RBox(g, "Sap", new Vector3(0f, -0.13f, z), new Vector3(0.022f, 0.21f, 0.017f), Pbr("P_CekicSap", new Color(0.62f, 0.45f, 0.26f), 0.5f, 0f, WoodPair().color, WoodPair().normal, 0.5f), 0.007f, false, 0.3f);
        RBox(g, "Tutma Yeri", new Vector3(0f, -0.2f, z), new Vector3(0.026f, 0.09f, 0.021f), MRubber, 0.009f);
    }

    /// Dijital multimetre: sarı kauçuk kılıf, ekran (değer yazılı), kademe düğmesi, soketler, kırmızı-siyah problar.
    static void Multimeter(Transform parent, Vector3 pos)
    {
        var g = ToolGroup(parent, "Multimetre", pos);
        Hook(g, new Vector3(-0.025f, 0f, 0f));
        Hook(g, new Vector3(0.025f, 0f, 0f));
        float z = 0.035f;
        RBox(g, "Kılıf", new Vector3(0f, -0.1f, z), new Vector3(0.09f, 0.17f, 0.035f), Pbr("P_MultimetreKilif", new Color(0.95f, 0.72f, 0.08f), 0.3f), 0.012f);
        RBox(g, "Ön Yüz", new Vector3(0f, -0.1f, z + 0.017f), new Vector3(0.074f, 0.152f, 0.003f), Pbr("P_CihazGriKoyu", new Color(0.18f, 0.19f, 0.21f), 0.5f), 0.006f);
        RBox(g, "Ekran", new Vector3(0f, -0.04f, z + 0.0195f), new Vector3(0.06f, 0.032f, 0.002f), Pbr("P_LCD", new Color(0.62f, 0.68f, 0.56f), 0.8f), 0.002f);
        Label(g, "12.06", g.TransformPoint(new Vector3(0f, -0.04f, z + 0.022f)), 180f, 0.11f, false, new Color(0.08f, 0.08f, 0.08f));
        Prim(PrimitiveType.Cylinder, g, "Kademe Düğmesi", new Vector3(0f, -0.1f, z + 0.021f), new Vector3(0.042f, 0.004f, 0.042f), MBlackPlastic, false, new Vector3(90f, 0f, 0f));
        RBox(g, "Düğme İbresi", new Vector3(0f, -0.09f, z + 0.025f), new Vector3(0.004f, 0.018f, 0.002f), MWhitePlastic, 0.001f);
        Color[] jack = { new Color(0.85f, 0.1f, 0.08f), new Color(0.1f, 0.1f, 0.1f), new Color(0.1f, 0.1f, 0.1f) };
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, g, "Soket", new Vector3(-0.022f + i * 0.022f, -0.158f, z + 0.02f), new Vector3(0.009f, 0.003f, 0.009f),
                Pbr("P_Soket_" + i, jack[i], 0.6f), false, new Vector3(90f, 0f, 0f));

        Color[] probe = { new Color(0.85f, 0.1f, 0.08f), new Color(0.1f, 0.1f, 0.1f) };
        Hook(g, new Vector3(0.075f, 0f, 0f));
        for (int i = 0; i < 2; i++)
        {
            var m = Pbr("P_Prob_" + i, probe[i], 0.5f);
            float x = 0.07f + i * 0.012f;
            Prim(PrimitiveType.Cylinder, g, "Prob Kablosu", new Vector3(x, -0.09f, 0.03f), new Vector3(0.004f, 0.08f, 0.004f), m, false);
            Prim(PrimitiveType.Cylinder, g, "Prob Sapı", new Vector3(x, -0.19f, 0.03f), new Vector3(0.01f, 0.025f, 0.01f), m, false);
            Prim(PrimitiveType.Cylinder, g, "Prob Ucu", new Vector3(x, -0.225f, 0.03f), new Vector3(0.002f, 0.012f, 0.002f), Steel, false);
        }
    }

    /// Şerit metre: sarı yuvarlak gövde, çıkan şerit ucu, siyah kemer klipsi.
    static void TapeMeasure(Transform parent, Vector3 pos)
    {
        var g = ToolGroup(parent, "Şerit Metre", pos);
        Hook(g, Vector3.zero);
        float z = 0.035f;
        RBox(g, "Klips", new Vector3(0f, -0.035f, 0.012f), new Vector3(0.022f, 0.05f, 0.004f), MBlackMetal, 0.002f);
        Prim(PrimitiveType.Cylinder, g, "Gövde", new Vector3(0f, -0.05f, z), new Vector3(0.07f, 0.017f, 0.07f), Pbr("P_MultimetreKilif", new Color(0.95f, 0.72f, 0.08f), 0.3f), false, new Vector3(90f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Etiket", new Vector3(0f, -0.05f, z + 0.018f), new Vector3(0.04f, 0.001f, 0.04f), MBlackPlastic, false, new Vector3(90f, 0f, 0f));
        RBox(g, "Şerit", new Vector3(0.03f, -0.083f, z), new Vector3(0.025f, 0.003f, 0.02f), Pbr("P_SeritSari", new Color(0.98f, 0.85f, 0.2f), 0.5f), 0.001f);
        RBox(g, "Şerit Ucu", new Vector3(0.042f, -0.081f, z), new Vector3(0.003f, 0.008f, 0.02f), Steel, 0.001f);
    }

    static void UtilityKnife(Transform parent, Vector3 pos, Color body)
    {
        var g = ToolGroup(parent, "Maket Bıçağı", pos);
        Hook(g, Vector3.zero);
        float z = 0.028f;
        RBox(g, "Gövde", new Vector3(0f, -0.07f, z), new Vector3(0.022f, 0.13f, 0.012f), Rubber(ColorUtility.ToHtmlStringRGB(body), body), 0.005f);
        RBox(g, "Tutma Yeri", new Vector3(0f, -0.06f, z + 0.001f), new Vector3(0.024f, 0.06f, 0.012f), MRubber, 0.005f);
        RBox(g, "Sürgü", new Vector3(0f, -0.1f, z + 0.007f), new Vector3(0.008f, 0.012f, 0.004f), MBlackPlastic, 0.002f);
        RBox(g, "Bıçak", new Vector3(0.002f, -0.145f, z), new Vector3(0.012f, 0.026f, 0.0015f), Steel, 0.0005f, false, 1f, new Vector3(0f, 0f, 12f));
    }

    /// Tezgâh üstünde lehim istasyonu: ekranlı gövde, sıcaklık düğmesi, spiral tutucuda havya, sünger.
    static void SolderingStation(Transform parent, Vector3 basePos)
    {
        var g = new GameObject("Lehim İstasyonu").transform;
        g.SetParent(parent, false);
        g.localPosition = basePos;
        var body = Pbr("P_CihazGriKoyu", new Color(0.18f, 0.19f, 0.21f), 0.5f);

        RBox(g, "Gövde", new Vector3(0f, 0.055f, 0f), new Vector3(0.16f, 0.11f, 0.18f), body, 0.012f);
        RBox(g, "Ekran", new Vector3(-0.03f, 0.075f, 0.0905f), new Vector3(0.06f, 0.03f, 0.002f), PbrGlow("P_LehimEkran", new Color(1f, 0.35f, 0.1f), 1.5f), 0.002f);
        Label(g, "350°", g.TransformPoint(new Vector3(-0.03f, 0.075f, 0.093f)), 180f, 0.09f, false, new Color(1f, 0.85f, 0.7f));
        Prim(PrimitiveType.Cylinder, g, "Sıcaklık Düğmesi", new Vector3(0.045f, 0.06f, 0.095f), new Vector3(0.035f, 0.008f, 0.035f), MBlackPlastic, false, new Vector3(90f, 0f, 0f));

        var holder = new Vector3(0.22f, 0f, 0.02f);
        RBox(g, "Tutucu Tabanı", holder + new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.02f, 0.12f), MBlackMetal, 0.008f);
        RBox(g, "Sünger", holder + new Vector3(0.025f, 0.026f, 0.025f), new Vector3(0.05f, 0.012f, 0.05f), Pbr("P_Sunger", new Color(0.9f, 0.75f, 0.2f), 0.05f), 0.004f);
        Prim(PrimitiveType.Cylinder, g, "Spiral Tutucu", holder + new Vector3(-0.02f, 0.07f, -0.01f), new Vector3(0.035f, 0.05f, 0.035f), Steel, false, new Vector3(-35f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Havya Sapı", holder + new Vector3(-0.02f, 0.1f, 0.03f), new Vector3(0.022f, 0.06f, 0.022f), Rubber("Mavi", new Color(0.12f, 0.35f, 0.75f)), false, new Vector3(-35f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Havya Ucu", holder + new Vector3(-0.02f, 0.03f, -0.07f), new Vector3(0.007f, 0.045f, 0.007f), Steel, false, new Vector3(-35f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Kablo", new Vector3(0.12f, 0.06f, 0.08f), new Vector3(0.008f, 0.07f, 0.008f), MRubber, false, new Vector3(0f, 0f, 70f));
    }

    /// Bir sıra plastik parça kutusu: yuvarlak kenarlı, önlerinde beyaz etiket. Yerel merkez = rafın üst yüzeyi.
    static void Bins(Transform parent, Vector3 localCenter, float width, int count)
    {
        Color[] cols = { new Color(0.15f, 0.35f, 0.75f), new Color(0.85f, 0.2f, 0.15f), new Color(0.95f, 0.75f, 0.1f) };
        var binSize = new Vector3(width / count - 0.03f, 0.12f, 0.24f);
        var binMesh = RoundedBoxMesh(binSize, 0.012f, 1f);
        var labelMesh = RoundedBoxMesh(new Vector3(binSize.x * 0.6f, 0.035f, 0.002f), 0.001f, 1f);
        var labels = new List<Matrix4x4>();
        for (int c = 0; c < cols.Length; c++)
        {
            var list = new List<Matrix4x4>();
            for (int i = c; i < count; i += cols.Length)
            {
                float x = localCenter.x - width / 2f + (i + 0.5f) * width / count;
                list.Add(Matrix4x4.Translate(new Vector3(x, localCenter.y + 0.06f, localCenter.z)));
                labels.Add(Matrix4x4.Translate(new Vector3(x, localCenter.y + 0.07f, localCenter.z + 0.121f)));
            }
            CombinedMeshes(parent, "Parça Kutuları", binMesh, list, Pbr("P_Kutucuk_" + c, cols[c], 0.55f));
        }
        CombinedMeshes(parent, "Kutu Etiketleri", labelMesh, labels, MWhitePlastic);
    }

    /// Mimar tipi masa lambası: ağırlıklı taban, eklemli kollar, metal başlık, aşağı bakan spot ışık.
    static void DeskLamp(Transform parent, Vector3 basePos)
    {
        var g = new GameObject("Masa Lambası").transform;
        g.SetParent(parent, false);
        g.localPosition = basePos;
        Prim(PrimitiveType.Cylinder, g, "Taban", new Vector3(0f, 0.012f, 0f), new Vector3(0.16f, 0.012f, 0.16f), MBlackMetal, false);
        Prim(PrimitiveType.Sphere, g, "Eklem", new Vector3(0f, 0.03f, 0f), Vector3.one * 0.03f, MChrome, false);
        RBox(g, "Alt Kol", new Vector3(0f, 0.2f, 0.06f), new Vector3(0.016f, 0.35f, 0.016f), MBlackMetal, 0.006f, false, 1f, new Vector3(20f, 0f, 0f));
        Prim(PrimitiveType.Sphere, g, "Eklem", new Vector3(0f, 0.365f, 0.12f), Vector3.one * 0.028f, MChrome, false);
        RBox(g, "Üst Kol", new Vector3(0f, 0.39f, 0.25f), new Vector3(0.014f, 0.014f, 0.27f), MBlackMetal, 0.006f, false, 1f, new Vector3(-10f, 0f, 0f));
        Prim(PrimitiveType.Cylinder, g, "Başlık", new Vector3(0f, 0.37f, 0.38f), new Vector3(0.12f, 0.05f, 0.12f), MBlackMetal, false);
        Prim(PrimitiveType.Cylinder, g, "Ampul Yüzü", new Vector3(0f, 0.319f, 0.38f), new Vector3(0.1f, 0.002f, 0.1f), PbrGlow("P_LambaAmpul", new Color(1f, 0.9f, 0.75f), 4f), false);
        var l = new GameObject("Masa Lambası Işığı").AddComponent<Light>();
        l.transform.SetParent(g, false);
        l.transform.localPosition = new Vector3(0f, 0.3f, 0.38f);
        l.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        l.type = LightType.Spot;
        l.spotAngle = 80f;
        l.innerSpotAngle = 40f;
        l.range = 2.5f;
        l.intensity = 2.5f;
        l.color = new Color(1f, 0.88f, 0.72f);
        l.shadows = LightShadows.Soft;
    }

    static void FireExtinguisher(Transform parent, Vector3 wallFloorPos)
    {
        var g = new GameObject("Yangın Tüpü").transform;
        g.SetParent(parent, false);
        g.localPosition = wallFloorPos + new Vector3(0f, 0f, 0.15f);
        var red = Pbr("P_YanginTupu", new Color(0.78f, 0.06f, 0.05f), 0.8f);
        Prim(PrimitiveType.Cylinder, g, "Tüp", new Vector3(0f, 0.3f, 0f), new Vector3(0.16f, 0.27f, 0.16f), red, true);
        Prim(PrimitiveType.Sphere, g, "Tüp Başı", new Vector3(0f, 0.57f, 0f), new Vector3(0.16f, 0.08f, 0.16f), red, false);
        RBox(g, "Valf", new Vector3(0f, 0.63f, 0f), new Vector3(0.04f, 0.06f, 0.04f), MChrome, 0.008f);
        RBox(g, "Tetik", new Vector3(0.04f, 0.665f, 0f), new Vector3(0.1f, 0.012f, 0.025f), MBlackMetal, 0.005f, false, 1f, new Vector3(0f, 0f, -10f));
        RBox(g, "Hortum", new Vector3(-0.07f, 0.45f, 0.04f), new Vector3(0.018f, 0.32f, 0.018f), MRubber, 0.008f, false, 1f, new Vector3(0f, 0f, 8f));
        RBox(g, "Etiket", new Vector3(0f, 0.3f, 0.081f), new Vector3(0.08f, 0.12f, 0.002f), MWhitePlastic, 0.003f);
    }

    static void TrashBin(Transform parent, Vector3 floorPos)
    {
        var g = new GameObject("Çöp Kutusu").transform;
        g.SetParent(parent, false);
        g.localPosition = floorPos;
        Prim(PrimitiveType.Cylinder, g, "Kova", new Vector3(0f, 0.3f, 0f), new Vector3(0.36f, 0.3f, 0.36f), MBrushed, true);
        Prim(PrimitiveType.Cylinder, g, "Kapak", new Vector3(0f, 0.61f, 0f), new Vector3(0.37f, 0.012f, 0.37f), MBlackPlastic, false);
        RBox(g, "Pedal", new Vector3(0f, 0.03f, 0.2f), new Vector3(0.12f, 0.02f, 0.06f), MBlackPlastic, 0.006f);
    }

    // =====================================================================
    // DEPO
    // =====================================================================

    static void ShopStorage(Transform shop)
    {
        var room = new GameObject("Depo").transform;
        room.SetParent(shop, false);
        float[] levels = { 0.45f, 1.1f, 1.75f };
        float[] withTop = { 0.45f, 1.1f, 1.75f, 2.3f };

        var r1 = MetalShelf(room, "Depo Rafı 1", new Vector3(MaxX - 0.1f - 0.25f, FloorY, -3f), -90f, 3f, 0.5f, withTop, "Depo Rafı 1", levels.Length);
        var r2 = MetalShelf(room, "Depo Rafı 2", new Vector3(5.1f, FloorY, MinZ + 0.1f + 0.25f), 0f, 3f, 0.5f, withTop, "Depo Rafı 2", levels.Length);
        Cartons(r1, new Vector3(0f, 2.3f, 0f), 2.8f, 11);
        Cartons(r2, new Vector3(0f, 2.3f, 0f), 2.8f, 12);

        Cartons(room, new Vector3(4.1f, FloorY, -0.6f), 1f, 21, stacked: true);
        Cartons(room, new Vector3(6.9f, FloorY, -0.55f), 0.8f, 22, stacked: true);
    }

    /// Galvaniz metal raf: delikli köşe dikmeleri, sac katlar, ön kirişler. slotBase verilirse ilk 'slotLevels' katta 4'er yuva.
    static Transform MetalShelf(Transform parent, string name, Vector3 pos, float yaw, float length, float depth, float[] levels, string slotBase, int slotLevels = 0)
    {
        var u = new GameObject(name).transform;
        u.SetParent(parent, false);
        u.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var post = Pbr("P_RafDikme", new Color(0.3f, 0.33f, 0.37f), 0.45f, 0.5f);
        var deck = Pbr("P_Galvaniz", new Color(0.74f, 0.76f, 0.78f), 0.55f, 0.8f, BrushedMetalPair().color, BrushedMetalPair().normal, 0.3f);
        float top = levels[levels.Length - 1] + 0.05f;

        foreach (float x in new[] { -length / 2f + 0.02f, length / 2f - 0.02f })
        foreach (float z in new[] { -depth / 2f + 0.02f, depth / 2f - 0.02f })
        {
            RBox(u, "Dikme", new Vector3(x, top / 2f, z), new Vector3(0.04f, top, 0.04f), post, 0.004f);
            RBox(u, "Dikme Ayağı", new Vector3(x, 0.006f, z), new Vector3(0.055f, 0.012f, 0.055f), MRubber, 0.003f);
        }

        int n = 1;
        for (int li = 0; li < levels.Length; li++)
        {
            float y = levels[li];
            RBox(u, "Raf Katı", new Vector3(0f, y - 0.015f, 0f), new Vector3(length - 0.01f, 0.03f, depth - 0.01f), deck, 0.004f, true, 0.5f);
            RBox(u, "Ön Kiriş", new Vector3(0f, y - 0.045f, depth / 2f - 0.012f), new Vector3(length - 0.06f, 0.05f, 0.02f), post, 0.004f);
            RBox(u, "Arka Kiriş", new Vector3(0f, y - 0.045f, -depth / 2f + 0.012f), new Vector3(length - 0.06f, 0.05f, 0.02f), post, 0.004f);
            if (slotBase != null && li < slotLevels)
                for (int i = 0; i < 4; i++)
                    CreateSlot(u, $"{slotBase} {n++}", new Vector3(-length / 2f + length / 8f + i * length / 4f, y, 0f));
        }
        return u;
    }

    /// Koli dizisi (görüntü): oluklu karton, üstünde koli bandı. stacked: yerde üst üste yığın.
    static void Cartons(Transform parent, Vector3 localBase, float width, int seed, bool stacked = false)
    {
        var rng = new System.Random(seed);
        Color[] cols = { new Color(0.66f, 0.5f, 0.33f), new Color(0.72f, 0.57f, 0.38f), new Color(0.58f, 0.44f, 0.29f) };
        var tape = Pbr("P_KoliBandi", new Color(0.78f, 0.62f, 0.38f), 0.7f);
        var boxes = new List<(Vector3 pos, Vector3 size, float yaw)>();
        if (!stacked)
        {
            float x = -width / 2f;
            while (x < width / 2f - 0.25f)
            {
                var s = new Vector3(0.25f + Rand(rng) * 0.25f, 0.18f + Rand(rng) * 0.2f, 0.3f + Rand(rng) * 0.12f);
                if (x + s.x > width / 2f) break;
                boxes.Add((localBase + new Vector3(x + s.x / 2f, s.y / 2f, 0f), s, 0f));
                x += s.x + 0.03f;
            }
        }
        else
        {
            float y = 0f;
            for (int i = 0; i < 4; i++)
            {
                var s = new Vector3(width * (1f - i * 0.12f), 0.3f + Rand(rng) * 0.15f, 0.5f - i * 0.04f);
                boxes.Add((localBase + new Vector3(0f, y + s.y / 2f, 0f), s, Rand(rng) * 16f - 8f));
                y += s.y;
            }
        }
        foreach (var (pos, size, yaw) in boxes)
        {
            int ci = rng.Next(cols.Length);
            var mat = Pbr("P_Koli_" + ci, cols[ci], 0.15f, 0f, FeltPair().color, FeltPair().normal, 0.25f);
            RBox(parent, "Koli", pos, size, mat, 0.008f, false, 0.5f, new Vector3(0f, yaw, 0f));
            RBox(parent, "Koli Bandı", pos + new Vector3(0f, size.y / 2f, 0f), new Vector3(0.05f, 0.002f, size.z + 0.004f), tape, 0.001f, false, 1f, new Vector3(0f, yaw, 0f));
        }
    }

    // =====================================================================
    // KAPILAR (Door.cs: [E] ile açılır/kapanır)
    // =====================================================================

    static void ShopDoors(Transform shop)
    {
        var glassFrame = MBrushed;
        var wood = Pbr("P_KapiAhsap", new Color(0.62f, 0.46f, 0.32f), 0.4f, 0f, WoodPair().color, WoodPair().normal, 0.5f);
        var metal = Pbr("P_KapiMetal", new Color(0.42f, 0.47f, 0.52f), 0.45f, 0.4f);

        // Hepsi yerel -z yönüne açılır (dükkân kapısı içeri, arka kapı dışarı, iç kapılar arka odalara).
        // Menteşe, kapının açıldığı yüzde ve kasa dikmesinin hemen yanında; kanat kapalıyken duvarın içinde durur.
        // Dış kapı boşluklarında alüminyum kasa dikmeleri boşluğa 6 cm girer, üst kasa 2.24'te başlar.
        // Kanatlar eşiğin 3 mm üstünden başlar (zemine / eşiğe değip titremesin).
        float y = FloorY + Threshold + 0.003f;
        HingedDoor(shop, "Dükkân kapısı", new Vector3(-0.63f, y, MaxZ - 0.105f), 0f, 1.26f, 2.12f, glassFrame, true);
        // İç kapılar: pervaz boşluğa 1 cm taştığı için kanat 0.95 m, menteşe pervazdan 1 cm içeride
        HingedDoor(shop, "Tamirhane kapısı", new Vector3(1.52f, y, SplitZ - 0.03f), 0f, 0.95f, 2.07f, wood, false);
        HingedDoor(shop, "Depo kapısı", new Vector3(DepoX + 0.03f, y, -3.48f), -90f, 0.95f, 2.07f, wood, false);
        HingedDoor(shop, "Arka kapı", new Vector3(0.47f, y, MinZ + 0.03f), 0f, 1.46f, 2.12f, metal, false);

        // Eşikler: zemin birleşimlerini ve dış kapıların altındaki boşluğu örter.
        var sill = Pbr("P_Esik", new Color(0.62f, 0.64f, 0.67f), 0.55f, 0.8f, BrushedMetalPair().color, BrushedMetalPair().normal, 0.3f);
        float ty = FloorY + Threshold / 2f;
        SBox(shop, "Eşik (giriş)", new Vector3(0f, (FloorY + Threshold) / 2f, MaxZ + 0.01f), new Vector3(1.38f, FloorY + Threshold, 0.24f), sill, true);
        SBox(shop, "Eşik (arka kapı)", new Vector3(1.2f, (FloorY + Threshold) / 2f, MinZ - 0.01f), new Vector3(1.58f, FloorY + Threshold, 0.24f), sill, true);
        SBox(shop, "Eşik (tamirhane)", new Vector3(2f, ty, SplitZ), new Vector3(1f, Threshold, 0.14f), sill, false);
        SBox(shop, "Eşik (depo)", new Vector3(DepoX, ty, -3f), new Vector3(0.14f, Threshold, 1f), sill, false);

        // İç kapı kasaları
        var casing = Pbr("P_KapiKasasi", new Color(0.95f, 0.95f, 0.93f), 0.4f);
        DoorCasing(shop, true, SplitZ, 2f, 1f, 2.2f, casing);
        DoorCasing(shop, false, DepoX, -3f, 1f, 2.2f, casing);
    }

    static void HingedDoor(Transform parent, string name, Vector3 hingePos, float yaw, float width, float height, Material mat, bool glass)
    {
        var h = new GameObject(name).transform;
        h.SetParent(parent, false);
        h.SetPositionAndRotation(hingePos, Quaternion.Euler(0f, yaw, 0f));

        // Kanat menteşe çizgisinin +z tarafında durur; açılınca kalınlığı kapı boşluğuna doğru döner,
        // böylece duvara ya da kasaya girmez.
        const float thick = 0.045f;
        var leaf = new GameObject("Kanat").transform;
        leaf.SetParent(h, false);
        leaf.localPosition = new Vector3(0f, 0f, thick / 2f + 0.002f);

        if (glass)
        {
            RBox(leaf, "Cam", new Vector3(width / 2f, height / 2f, 0f), new Vector3(width - 0.1f, height - 0.1f, 0.012f), MGlassClean, 0.002f, true);
            RBox(leaf, "Kasa", new Vector3(0.03f, height / 2f, 0f), new Vector3(0.06f, height, thick), mat, 0.006f, false, 0.5f);
            RBox(leaf, "Kasa", new Vector3(width - 0.03f, height / 2f, 0f), new Vector3(0.06f, height, thick), mat, 0.006f, false, 0.5f);
            RBox(leaf, "Kasa", new Vector3(width / 2f, height - 0.04f, 0f), new Vector3(width - 0.12f, 0.08f, thick), mat, 0.006f, false, 0.5f);
            RBox(leaf, "Kasa", new Vector3(width / 2f, 0.07f, 0f), new Vector3(width - 0.12f, 0.14f, thick), mat, 0.006f, false, 0.5f);
            // İtme barı (iki yüzde)
            foreach (float side in new[] { -1f, 1f })
            {
                RBox(leaf, "İtme Barı", new Vector3(width / 2f + 0.15f, 1.05f, side * (thick / 2f + 0.04f)), new Vector3(0.7f, 0.03f, 0.03f), MChrome, 0.014f);
                foreach (float bx in new[] { width / 2f - 0.15f, width / 2f + 0.45f })
                    RBox(leaf, "Bar Ayağı", new Vector3(bx, 1.05f, side * (thick / 2f + 0.02f)), new Vector3(0.025f, 0.025f, 0.04f), MChrome, 0.008f);
            }
        }
        else
        {
            RBox(leaf, "Kanat", new Vector3(width / 2f, height / 2f, 0f), new Vector3(width, height, thick), mat, 0.006f, true, 1f);
            // Kabartma paneller (iki yüzde)
            foreach (float side in new[] { -1f, 1f })
                foreach (float py in new[] { height * 0.72f, height * 0.28f })
                    RBox(leaf, "Kapı Paneli", new Vector3(width / 2f, py, side * (thick / 2f + 0.002f)), new Vector3(width - 0.24f, height * 0.36f, 0.006f), mat, 0.003f, false, 1f);
            // Kol: rozet + yatay kol
            foreach (float side in new[] { -1f, 1f })
            {
                Prim(PrimitiveType.Cylinder, leaf, "Kol Rozeti", new Vector3(width - 0.09f, 1.0f, side * (thick / 2f + 0.004f)), new Vector3(0.055f, 0.004f, 0.055f), MChrome, false, new Vector3(90f, 0f, 0f));
                Prim(PrimitiveType.Cylinder, leaf, "Kol Boynu", new Vector3(width - 0.09f, 1.0f, side * (thick / 2f + 0.022f)), new Vector3(0.018f, 0.018f, 0.018f), MChrome, false, new Vector3(90f, 0f, 0f));
                RBox(leaf, "Kol", new Vector3(width - 0.15f, 1.0f, side * (thick / 2f + 0.04f)), new Vector3(0.13f, 0.02f, 0.02f), MChrome, 0.009f);
                Prim(PrimitiveType.Cylinder, leaf, "Anahtar Deliği", new Vector3(width - 0.09f, 0.86f, side * (thick / 2f + 0.003f)), new Vector3(0.03f, 0.003f, 0.03f), MChrome, false, new Vector3(90f, 0f, 0f));
            }
        }

        foreach (float hy in new[] { 0.25f, height - 0.25f })
            Prim(PrimitiveType.Cylinder, leaf, "Menteşe", new Vector3(0.004f, hy, -thick / 2f), new Vector3(0.016f, 0.05f, 0.016f), MChrome, false);

        var rb = h.gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true; // hareket eden çarpışıcı

        var door = h.gameObject.AddComponent<Door>();
        door.doorName = name;
        door.openAngle = 90f; // yerel -z tarafına, dik açıyla açılır (duvara yaslanmaz)
    }

    /// İç kapı pervazı: zeminden (FloorY) kapı boşluğunun üstüne kadar.
    /// Boşluğun içine 1 cm taşar: duvarın boşluk kenarındaki yüzlerini örter. (Aynı düzlemde kalırsa
    /// iki yüzey titreşir — z-fighting — ve hareket ettikçe yer değiştiren çizgiler görünür.)
    static void DoorCasing(Transform parent, bool alongX, float fixedC, float center, float width, float openingTop, Material mat)
    {
        const float lip = 0.01f, w = 0.07f;
        Vector3 P(float along, float y) => alongX ? new Vector3(along, y, fixedC) : new Vector3(fixedC, y, along);
        // 18 cm: duvar yüzlerinden (±6 cm) ve süpürgelikten (8 cm) belirgin şekilde dışarıda
        Vector3 S(float len, float h) => alongX ? new Vector3(len, h, 0.18f) : new Vector3(0.18f, h, len);
        float s = center - width / 2f, e = center + width / 2f;
        float jambTop = openingTop + w;
        float jambH = jambTop - FloorY;
        RBox(parent, "Kapı Pervazı", P(s - (w - lip) / 2f, FloorY + jambH / 2f), S(w + lip, jambH), mat, 0.006f);
        RBox(parent, "Kapı Pervazı", P(e + (w - lip) / 2f, FloorY + jambH / 2f), S(w + lip, jambH), mat, 0.006f);
        RBox(parent, "Kapı Pervazı", P(center, openingTop + (w - lip) / 2f), S(width - 2f * lip, w + lip), mat, 0.006f);
    }

    // =====================================================================
    // DIŞ CEPHE: tabela, tenteler, lamba, klima
    // =====================================================================

    static void ShopExterior(Transform shop)
    {
        float face = MaxZ + 0.1f;

        // Kutu harf tabela: koyu kutu, beyaz yazı, alt kenarda LED
        RBox(shop, "Tabela Kutusu", new Vector3(0f, 2.95f, face + 0.06f), new Vector3(3.8f, 0.62f, 0.12f), Pbr("P_TabelaKutusu", new Color(0.08f, 0.1f, 0.12f), 0.5f), 0.02f);
        RBox(shop, "Tabela Yüzü", new Vector3(0f, 2.95f, face + 0.121f), new Vector3(3.7f, 0.52f, 0.002f), MAccent, 0.004f);
        RBox(shop, "Tabela LED", new Vector3(0f, 2.645f, face + 0.1f), new Vector3(3.6f, 0.01f, 0.02f), PbrGlow("P_LedSerit", new Color(1f, 0.94f, 0.82f), 2.5f), 0.003f);
        Label(shop, "BİLGİSAYAR DÜKKANI", new Vector3(0f, 2.95f, face + 0.13f), 180f, 1.3f, false, Color.white);
        RBox(shop, "Kapı No Plakası", new Vector3(1.05f, 1.9f, face + 0.006f), new Vector3(0.24f, 0.12f, 0.012f), Pbr("P_NoPlaka", new Color(0.15f, 0.25f, 0.45f), 0.6f), 0.01f);
        Label(shop, "No: 12", new Vector3(1.05f, 1.9f, face + 0.014f), 180f, 0.3f, false, Color.white);

        // Çizgili tenteler (vitrin camlarının üstünde)
        var awning = TexMat("Tente", AwningTexture(), Color.white, 0.1f);
        foreach (float x in new[] { -4.8f, 4.8f })
        {
            Prim(PrimitiveType.Cube, shop, "Tente", new Vector3(x, 2.62f, face + 0.45f), new Vector3(4.8f, 0.03f, 1.0f), awning, false, new Vector3(25f, 0f, 0f));
            SBox(shop, "Tente Saçağı", new Vector3(x, 2.33f, face + 0.9f), new Vector3(4.8f, 0.2f, 0.02f), awning, false);
            foreach (float dx in new[] { -2.35f, 2.35f })
                RBox(shop, "Tente Kolu", new Vector3(x + dx, 2.62f, face + 0.45f), new Vector3(0.025f, 0.025f, 1.0f), MBlackMetal, 0.008f, false, 1f, new Vector3(25f, 0f, 0f));
        }

        // Aplik lambalar
        foreach (var lp in new[] { new Vector3(0f, 2.48f, face + 0.08f), new Vector3(1.2f, 2.5f, MinZ - 0.18f) })
        {
            RBox(shop, "Aplik", lp, new Vector3(0.3f, 0.08f, 0.15f), MBlackMetal, 0.02f);
            RBox(shop, "Aplik Işığı", lp + new Vector3(0f, -0.041f, 0f), new Vector3(0.26f, 0.004f, 0.11f), PbrGlow("P_Difuzor", new Color(1f, 0.97f, 0.9f), 2.2f), 0.002f);
        }

        // Yan duvarda klima dış ünitesi
        var g = new GameObject("Klima Dış Ünitesi").transform;
        g.SetParent(shop, false);
        g.localPosition = new Vector3(MinX - 0.4f, 2.3f, -1.5f);
        RBox(g, "Gövde", Vector3.zero, new Vector3(0.5f, 0.55f, 0.85f), MWhitePlastic, 0.02f);
        Prim(PrimitiveType.Cylinder, g, "Fan Izgarası", new Vector3(-0.252f, 0f, -0.1f), new Vector3(0.42f, 0.004f, 0.42f), MBlackMetal, false, new Vector3(0f, 0f, 90f));
        Prim(PrimitiveType.Cylinder, g, "Fan Göbeği", new Vector3(-0.256f, 0f, -0.1f), new Vector3(0.08f, 0.004f, 0.08f), MWhitePlastic, false, new Vector3(0f, 0f, 90f));
        foreach (float dz in new[] { -0.35f, 0.35f })
            RBox(shop, "Klima Askısı", new Vector3(MinX - 0.3f, 2.0f, -1.5f + dz), new Vector3(0.45f, 0.04f, 0.04f), MBlackMetal, 0.006f);

        ShopDriveway(shop);
    }

    /// Dükkânın yanından (klima tarafı, x &lt; -8) arka otoparka giden asfalt yol.
    /// Caddeden çıkar, yaya kaldırımının üstünden alçak bir rampayla geçer (araba ilerde buradan
    /// arka kapıya gelip malı depoya indirecek).
    static void ShopDriveway(Transform shop)
    {
        var asphalt = TexMat("YolAsfalt", AsphaltTexture(), Color.white, 0.15f);
        float x0 = MinX - 4.5f, x1 = MinX - 0.5f; // -12.5 … -8.5 (4 m)

        // (z, yükseklik) profili: cadde üstünden başlar, kaldırımın (z 6.1 … 8.6, üstü 0.16) üstünden geçer,
        // arka otoparkın (z -6.1 … -17.9) içine biraz girerek biter.
        Vector2[] profile =
        {
            new Vector2(9.3f, 0.03f),
            new Vector2(8.7f, 0.175f),
            new Vector2(6.0f, 0.175f),
            new Vector2(5.4f, 0.03f),
            new Vector2(-8f, 0.03f),
        };

        var md = new MeshData();
        const float bottom = -0.02f, uv = 4f;
        for (int i = 0; i < profile.Length - 1; i++)
        {
            Vector2 a = profile[i], b = profile[i + 1]; // a.x = z (büyükten küçüğe), a.y = yükseklik
            var p00 = new Vector3(x0, a.y, a.x);
            var p10 = new Vector3(x1, a.y, a.x);
            var p01 = new Vector3(x0, b.y, b.x);
            var p11 = new Vector3(x1, b.y, b.x);
            md.TriUp(p00, p10, p11, uv);
            md.TriUp(p00, p11, p01, uv);

            // Yan yüzler (-x ve +x dışa bakar)
            md.Quad(new Vector3(x0, bottom, a.x), p00, p01, new Vector3(x0, bottom, b.x),
                new Vector2(a.x, bottom) / uv, new Vector2(a.x, a.y) / uv, new Vector2(b.x, b.y) / uv, new Vector2(b.x, bottom) / uv);
            md.Quad(new Vector3(x1, bottom, b.x), p11, p10, new Vector3(x1, bottom, a.x),
                new Vector2(b.x, bottom) / uv, new Vector2(b.x, b.y) / uv, new Vector2(a.x, a.y) / uv, new Vector2(a.x, bottom) / uv);
        }
        MeshObject(shop, "Yan Yol (arka otoparka)", md, asphalt, true);

        // Yolun dükkân tarafında ince beton bordür (çim ile asfalt arasında)
        SBox(shop, "Yol Bordürü", new Vector3(x1 + 0.08f, 0.06f, -1.3f), new Vector3(0.16f, 0.12f, 13.4f), Mat("Beton", new Color(0.55f, 0.55f, 0.55f)), true);
    }

    // =====================================================================
    // DOKULAR (renk + kabartma çiftleri Props.cs'de)
    // =====================================================================

    /// Epoksi / beton zemin: hafif lekeli düz gri. Renk malzemeden. 256 px = 2 m.
    static Texture2D EpoxyTexture() => LoadOrCreateTexture("T_Epoksi", 256, () =>
    {
        const int S = 256;
        var rng = new System.Random(23);
        var blot = NoiseGrid(rng, 8);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v = 0.82f + (SampleNoise(blot, 8, x / (float)S, y / (float)S) - 0.5f) * 0.08f + ((float)rng.NextDouble() - 0.5f) * 0.03f;
                if (rng.NextDouble() < 0.01) v -= 0.1f;
                px[y * S + x] = new Color(v, v, v);
            }
        return px;
    });

    /// Tente kumaşı: 10 kırmızı-krem dikey çizgi (tentenin genişliğine bir kez gerilir).
    static Texture2D AwningTexture() => LoadOrCreateTexture("T_Tente", 256, () =>
    {
        const int S = 256;
        var px = new Color[S * S];
        Color red = new Color(0.72f, 0.12f, 0.1f), cream = new Color(0.95f, 0.92f, 0.84f);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
                px[y * S + x] = (x * 10 / S) % 2 == 0 ? red : cream;
        return px;
    });
}
#endif

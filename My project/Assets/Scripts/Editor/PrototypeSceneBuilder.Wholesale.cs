#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// TOPTANCI MAĞAZASI — çevre yolunun kuzeyinde (x &lt; -88), dış kaldırıma bakan büyük mağaza.
///
/// Kök dünyada (-102, 0, 44), 90° dönük. Aşağıdaki her şey KÖKE GÖRE YEREL:
///   x: -19 … +19 (38 m, dünyada z 63 … 25)   z: -10 … +10 (20 m)   ön cephe z = +10 → çevre yolu
///
///   Ön cephe (z = +10): iki büyük vitrin camı, ortada otomatik kayan kapı (x -2 … +2), saçak, tabela
///   Kapının içinde, girerken sağda (x -4.7 … -7): 4 alışveriş arabası
///   Ön sağ (x 8.6 … 16): 2 kasa şeridi, kasiyerler
///   Ortada: 2 çift yüzlü reyon (G1 x = -11, G2 x = -3), sol duvarda ekran kartı/güç rafı,
///           sağ duvarda konsol rafı + TV duvarı, önünde promosyon masası
///   Arka duvar: palet rafları (depo stoğu, sadece görüntü)
///   Arka cephe dışı: mal kabul kapısı
///
/// Oynanış: arabayı al [E] → raflardan ürünleri arabaya ekle [E] → kasanın yanındaki şeride getir,
/// kasaya bakıp öde [E] → ödenmemiş ürünle otomatik kapı açılmaz.
/// </summary>
public static partial class PrototypeSceneBuilder
{
    const float StoreHalfW = 19f, StoreHalfD = 10f, StoreH = 7f, StoreParapet = 0.8f;
    const float StoreFloor = 0.16f, StoreWallT = 0.3f;
    static readonly Color BrandOrange = new Color(0.95f, 0.5f, 0.1f);

    static readonly float[] ShelfLevels = { 0.15f, 0.65f, 1.15f, 1.65f };
    static readonly float[] TallShelfLevels = { 0.15f, 0.8f, 1.45f };

    static void BuildWholesaleStore(Transform parent, List<ItemData> items)
    {
        var root = new GameObject("Toptancı").transform;
        root.SetParent(parent, false);
        root.SetPositionAndRotation(new Vector3(-102f, 0f, 44f), Quaternion.Euler(0f, 90f, 0f));

        // Raftan alınınca kopyalanacak pasif kutu şablonları
        var templateRoot = new GameObject("_Kutu Şablonları").transform;
        templateRoot.SetParent(root, false);
        templateRoot.localPosition = new Vector3(0f, -5f, 0f);
        var byId = new Dictionary<string, ItemData>();
        var templates = new Dictionary<string, ItemBox>();
        foreach (var it in items)
        {
            if (it == null) continue;
            byId[it.itemId] = it;
            templates[it.itemId] = MakeBoxTemplate(templateRoot, it);
        }

        StoreShell(root);
        StoreExterior(root);
        StoreEntrance(root);
        StoreShelves(root, byId, templates);
        StoreRacking(root);
        StoreCheckouts(root);
        StoreCarts(root);
    }

    static ItemBox MakeBoxTemplate(Transform parent, ItemData item)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Şablon - " + item.displayName;
        go.transform.SetParent(parent, false);
        go.transform.localScale = item.boxSize;
        go.GetComponent<Renderer>().sharedMaterial = Mat("Kutu_" + item.itemId, item.boxColor);
        var box = go.AddComponent<ItemBox>(); // Rigidbody otomatik eklenir
        box.Setup(item);
        go.GetComponent<Rigidbody>().mass = item.mass;
        go.SetActive(false);
        return box;
    }

    // =====================================================================
    // GÖVDE: zemin, duvarlar, çatı, iç kaplama, ışıklar
    // =====================================================================

    static void StoreShell(Transform root)
    {
        var clad = TexMat("ToptanciCephe", MetalPanelTexture(), new Color(0.24f, 0.29f, 0.37f), 0.35f);
        var lining = Mat("ToptanciIcDuvar", new Color(0.93f, 0.93f, 0.91f));
        var floor = TexMat("ToptanciZemin", FloorTileTexture(), Color.white, 0.6f);
        var ceiling = Mat("ToptanciTavan", new Color(0.86f, 0.86f, 0.86f));
        var orange = Mat("ToptanciTuruncu", BrandOrange);
        float hw = StoreHalfW, hd = StoreHalfD, t = StoreWallT, top = StoreH + StoreParapet;

        UVBox(root, "Zemin (karo)", new Vector3(0f, StoreFloor / 2f, 0f), new Vector3(hw * 2f, StoreFloor, hd * 2f), floor, 1.2f, true);

        // Ön cephe: iki büyük vitrin camı + ortada otomatik kapı boşluğu
        StoreWall(root, "Ön Cephe", true, hd, -hw - t / 2f, hw + t / 2f, top, clad,
            Opening.Window(-10.5f, 15f, 0.45f, 3.4f), Opening.Door(0f, 4f, 3f), Opening.Window(10.5f, 15f, 0.45f, 3.4f));
        StoreWall(root, "Arka Cephe", true, -hd, -hw - t / 2f, hw + t / 2f, top, clad);
        StoreWall(root, "Yan Cephe (+x)", false, hw, -hd + t / 2f, hd - t / 2f, top, clad);
        StoreWall(root, "Yan Cephe (-x)", false, -hw, -hd + t / 2f, hd - t / 2f, top, clad);

        // Çatı (alt yüzü tavan)
        UVBox(root, "Çatı / Tavan", new Vector3(0f, StoreH - 0.15f, 0f), new Vector3(hw * 2f - t, 0.3f, hd * 2f - t), ceiling, 2f, true);

        // İç kaplama (beyaz) + turuncu şerit — arka ve yan duvarlar
        float iw = hw - t / 2f, id = hd - t / 2f, li = 0.03f;
        float lh = StoreH - 0.3f - StoreFloor, ly = StoreFloor + lh / 2f;
        SBox(root, "İç Kaplama (arka)", new Vector3(0f, ly, -id + li / 2f), new Vector3(iw * 2f, lh, li), lining, false);
        SBox(root, "İç Kaplama (+x)", new Vector3(iw - li / 2f, ly, 0f), new Vector3(li, lh, id * 2f), lining, false);
        SBox(root, "İç Kaplama (-x)", new Vector3(-iw + li / 2f, ly, 0f), new Vector3(li, lh, id * 2f), lining, false);
        SBox(root, "İç Şerit (arka)", new Vector3(0f, 3.2f, -id + li + 0.005f), new Vector3(iw * 2f, 0.35f, 0.01f), orange, false);
        SBox(root, "İç Şerit (+x)", new Vector3(iw - li - 0.005f, 3.2f, 0f), new Vector3(0.01f, 0.35f, id * 2f), orange, false);
        SBox(root, "İç Şerit (-x)", new Vector3(-iw + li + 0.005f, 3.2f, 0f), new Vector3(0.01f, 0.35f, id * 2f), orange, false);
        SBox(root, "Süpürgelik", new Vector3(0f, StoreFloor + 0.06f, -id + li + 0.01f), new Vector3(iw * 2f, 0.12f, 0.02f), Mat("RafKaide", new Color(0.2f, 0.21f, 0.23f)), false);

        // Dış turuncu korkuluk bandı (dört cephe)
        float bandY = top - 0.35f, o = 0.03f;
        SBox(root, "Dış Bant (ön)", new Vector3(0f, bandY, hd + t / 2f + o), new Vector3(hw * 2f + t + 0.12f, 0.7f, 0.06f), orange, false);
        SBox(root, "Dış Bant (arka)", new Vector3(0f, bandY, -hd - t / 2f - o), new Vector3(hw * 2f + t + 0.12f, 0.7f, 0.06f), orange, false);
        SBox(root, "Dış Bant (+x)", new Vector3(hw + t / 2f + o, bandY, 0f), new Vector3(0.06f, 0.7f, hd * 2f + t), orange, false);
        SBox(root, "Dış Bant (-x)", new Vector3(-hw - t / 2f - o, bandY, 0f), new Vector3(0.06f, 0.7f, hd * 2f + t), orange, false);

        // Çatı üstü klima üniteleri
        var metal = Mat("Metal", new Color(0.4f, 0.42f, 0.45f));
        var fan = Mat("Fan", new Color(0.12f, 0.12f, 0.13f));
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector3(-11f + i * 11f, StoreH + 0.6f, -4f + (i % 2) * 6f);
            SBox(root, "Klima Ünitesi", p, new Vector3(2.4f, 1.2f, 1.6f), metal, false);
            Prim(PrimitiveType.Cylinder, root, "Klima Fanı", p + new Vector3(-0.5f, 0.61f, 0f), new Vector3(0.9f, 0.01f, 0.9f), fan, false);
            Prim(PrimitiveType.Cylinder, root, "Klima Fanı", p + new Vector3(0.6f, 0.61f, 0f), new Vector3(0.9f, 0.01f, 0.9f), fan, false);
        }

        // Tavan armatürleri + ışıklar (Forward+ açık, ışık sayısı sorun değil)
        foreach (float x in new[] { -14f, -5f, 4f, 13f })
        foreach (float z in new[] { -5.5f, 3f })
        {
            SBox(root, "Armatür", new Vector3(x, StoreH - 0.33f, z), new Vector3(0.45f, 0.06f, 4f), EmissiveMat(), false);
            StoreLight(root, new Vector3(x, StoreH - 0.8f, z), 14f, 3f);
        }
    }

    static void StoreWall(Transform root, string name, bool alongX, float fixedC, float a0, float a1, float height, Material mat, params Opening[] ops)
        => TexWall(root, name, alongX, fixedC, a0, a1, height, StoreWallT, mat, 1f, true, ops);

    /// Yerel eksenli, dokusu metre cinsinden tekrarlanan duvar; kapı/pencere boşluklarını atlar.
    /// a0…a1 dışında kalan boşluklar yok sayılır (aynı boşluk listesiyle duvarı parça parça örmek için).
    /// glazing: camı, alüminyum kayıtları ve kapı kasasını bu katman çizer (çift katmanlı duvarda dış katman).
    /// Çizmeyen iç katman pencerelere denizlik koyar.
    static void TexWall(Transform root, string name, bool alongX, float fixedC, float a0, float a1, float height, float thick,
        Material mat, float uvM, bool glazing, params Opening[] ops)
    {
        var w = new GameObject(name).transform;
        w.SetParent(root, false);
        var frame = Mat("Aluminyum", new Color(0.28f, 0.3f, 0.33f));

        Vector3 P(float along, float y) => alongX ? new Vector3(along, y, fixedC) : new Vector3(fixedC, y, along);
        Vector3 S(float len, float h, float th) => alongX ? new Vector3(len, h, th) : new Vector3(th, h, len);
        void Piece(float a, float b, float y0, float y1) => UVBox(w, "Duvar", P((a + b) / 2f, (y0 + y1) / 2f), S(b - a, y1 - y0, thick), mat, uvM, true);

        var list = new List<Opening>(ops);
        list.Sort((p, q) => p.center.CompareTo(q.center));
        float cursor = a0;
        foreach (var op in list)
        {
            float s = op.center - op.width / 2f, e = op.center + op.width / 2f;
            if (e <= a0 || s >= a1) continue;
            s = Mathf.Max(s, a0);
            e = Mathf.Min(e, a1);
            if (s > cursor) Piece(cursor, s, 0f, height);
            if (op.bottom > 0f) Piece(s, e, 0f, op.bottom);
            if (op.top < height) Piece(s, e, op.top, Mathf.Max(op.top, height));

            float midY = (op.bottom + op.top) / 2f, hgt = op.top - op.bottom, len = e - s;
            if (op.glass && glazing)
            {
                SBox(w, "Cam", P((s + e) / 2f, midY), S(len, hgt, 0.04f), GlassMat(), true);
                int n = Mathf.Max(1, Mathf.RoundToInt(len / 2.5f));
                for (int k = 0; k <= n; k++)
                    SBox(w, "Dikey Kayıt", P(s + k * len / n, midY), S(0.08f, hgt, thick + 0.04f), frame, false);
                SBox(w, "Yatay Kayıt", P((s + e) / 2f, op.bottom + 0.04f), S(len, 0.08f, thick + 0.04f), frame, false);
                SBox(w, "Yatay Kayıt", P((s + e) / 2f, op.top - 0.04f), S(len, 0.08f, thick + 0.04f), frame, false);
                if (op.bottom + 2.2f < op.top - 0.3f)
                    SBox(w, "Yatay Kayıt", P((s + e) / 2f, op.bottom + 2.2f), S(len, 0.08f, thick + 0.04f), frame, false);
            }
            else if (op.glass && op.bottom > 0f)
                SBox(w, "Denizlik", P((s + e) / 2f, op.bottom - 0.02f), S(len + 0.1f, 0.04f, thick + 0.1f), Mat("Pervaz", new Color(0.95f, 0.95f, 0.95f)), false);
            else if (!op.glass && glazing)
            {
                SBox(w, "Kapı Kasası", P(s, op.top / 2f), S(0.12f, op.top, thick + 0.04f), frame, false);
                SBox(w, "Kapı Kasası", P(e, op.top / 2f), S(0.12f, op.top, thick + 0.04f), frame, false);
                SBox(w, "Kapı Kasası", P((s + e) / 2f, op.top), S(len + 0.12f, 0.12f, thick + 0.04f), frame, false);
            }
            cursor = e;
        }
        if (cursor < a1) Piece(cursor, a1, 0f, height);
    }

    static void StoreLight(Transform root, Vector3 local, float range, float intensity)
    {
        var go = new GameObject("Işık");
        go.transform.SetParent(root, false);
        go.transform.localPosition = local;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = new Color(1f, 0.98f, 0.94f);
        l.shadows = LightShadows.None;
    }

    // =====================================================================
    // DIŞ MEKÂN: ön meydan, saçak, tabela, banklar, arka mal kabul
    // =====================================================================

    static void StoreExterior(Transform root)
    {
        float hw = StoreHalfW, hd = StoreHalfD, t = StoreWallT;
        var stone = TexMat("KaldirimTasi", PavingTexture(), Color.white, 0.1f);
        var clad = TexMat("ToptanciCephe", MetalPanelTexture(), new Color(0.24f, 0.29f, 0.37f), 0.35f);
        var orange = Mat("ToptanciTuruncu", BrandOrange);
        var concrete = Mat("Beton", new Color(0.55f, 0.55f, 0.55f));
        var metal = Mat("Metal", new Color(0.4f, 0.42f, 0.45f));
        float yaw = root.eulerAngles.y;

        // Ön meydan: dış kaldırıma kadar taş zemin (kaldırımın 5 mm altında kalır, birleşim titremez)
        BuildStrip(root, "Ön Meydan", new List<Vector3> { new Vector3(-hw - 0.3f, 0f, 12.15f), new Vector3(hw + 0.3f, 0f, 12.15f) },
            false, 4.3f, 0.155f, -0.05f, stone, 2f, true);

        // Yanlarda ve arkada beton şerit
        SBox(root, "Beton Şerit (arka)", new Vector3(0f, 0.05f, -hd - 3.15f), new Vector3(hw * 2f + 4.6f, 0.1f, 6f), concrete, true);
        SBox(root, "Beton Şerit (+x)", new Vector3(hw + 1.15f, 0.05f, -3.1f), new Vector3(2f, 0.1f, 26.1f), concrete, true);
        SBox(root, "Beton Şerit (-x)", new Vector3(-hw - 1.15f, 0.05f, -3.1f), new Vector3(2f, 0.1f, 26.1f), concrete, true);

        // Giriş saçağı
        UVBox(root, "Saçak", new Vector3(0f, 3.75f, 12f), new Vector3(9f, 0.25f, 4f), clad, 1f, false);
        SBox(root, "Saçak Bandı", new Vector3(0f, 3.75f, 14.02f), new Vector3(9.1f, 0.3f, 0.06f), orange, false);
        foreach (float x in new[] { -4.2f, 4.2f })
            Prim(PrimitiveType.Cylinder, root, "Saçak Direği", new Vector3(x, 0.155f + 1.8f, 13.6f), new Vector3(0.18f, 1.8f, 0.18f), metal, true);
        foreach (float x in new[] { -2.5f, 0f, 2.5f })
            Prim(PrimitiveType.Cylinder, root, "Spot", new Vector3(x, 3.615f, 12.5f), new Vector3(0.3f, 0.01f, 0.3f), EmissiveMat(), false);
        StoreLight(root, new Vector3(-2f, 3.3f, 12.3f), 7f, 1.5f);
        StoreLight(root, new Vector3(2f, 3.3f, 12.3f), 7f, 1.5f);

        // Tabelalar
        float face = hd + t / 2f + 0.12f;
        Label(root, "TOPTANCI", root.TransformPoint(new Vector3(0f, 5.45f, face)), yaw + 180f, 4f, true, Color.white, BrandOrange);
        Label(root, "BİLGİSAYAR PARÇALARI - KONSOL - TOPTAN SATIŞ", root.TransformPoint(new Vector3(0f, 4.15f, face)), yaw + 180f, 0.9f,
            true, Color.white, new Color(0.12f, 0.15f, 0.2f));
        Label(root, "TOPTANCI", root.TransformPoint(new Vector3(hw + t / 2f + 0.12f, 5.2f, 0f)), yaw - 90f, 3f, true, Color.white, BrandOrange);
        Label(root, "TOPTANCI", root.TransformPoint(new Vector3(-hw - t / 2f - 0.12f, 5.2f, 0f)), yaw + 90f, 3f, true, Color.white, BrandOrange);
        Label(root, "AÇIK  08:00 - 22:00", root.TransformPoint(new Vector3(3.2f, 2.3f, hd + 0.12f)), yaw + 180f, 0.35f, true, Color.white, new Color(0.12f, 0.15f, 0.2f));

        // Cephe aplikleri
        foreach (float x in new[] { -17f, -12f, 12f, 17f })
            SBox(root, "Aplik", new Vector3(x, 3.75f, hd + t / 2f + 0.1f), new Vector3(0.5f, 0.12f, 0.2f), EmissiveMat(), false);

        // Saksılar, banklar, çöp kutuları
        var plant = Mat("Bitki", new Color(0.2f, 0.42f, 0.17f));
        var wood = Mat("Bank", new Color(0.55f, 0.38f, 0.22f));
        var bin = Mat("CopKutusu", new Color(0.15f, 0.3f, 0.2f));
        foreach (float x in new[] { -6.3f, 6.3f })
        {
            SBox(root, "Saksı", new Vector3(x, 0.155f + 0.35f, 12.9f), new Vector3(1.4f, 0.7f, 1.4f), concrete, true);
            Prim(PrimitiveType.Sphere, root, "Çalı", new Vector3(x, 0.155f + 1.05f, 12.9f), new Vector3(1.3f, 0.9f, 1.3f), plant, false);
        }
        foreach (float x in new[] { -12f, 12f })
        {
            var b = new GameObject("Bank").transform;
            b.SetParent(root, false);
            b.localPosition = new Vector3(x, 0.155f, 13.3f);
            SBox(b, "Oturak", new Vector3(0f, 0.45f, 0f), new Vector3(2f, 0.07f, 0.5f), wood, true);
            SBox(b, "Sırtlık", new Vector3(0f, 0.75f, -0.24f), new Vector3(2f, 0.4f, 0.06f), wood, false);
            foreach (float lx in new[] { -0.85f, 0.85f })
                SBox(b, "Ayak", new Vector3(lx, 0.22f, 0f), new Vector3(0.08f, 0.44f, 0.45f), metal, false);
        }
        foreach (float x in new[] { -8.6f, 8.6f })
            Prim(PrimitiveType.Cylinder, root, "Çöp Kutusu", new Vector3(x, 0.155f + 0.45f, 13.6f), new Vector3(0.5f, 0.45f, 0.5f), bin, true);

        // Arka: mal kabul kepengi, rampa tamponları, personel kapısı
        float back = -hd - t / 2f;
        UVBox(root, "Mal Kabul Kepengi", new Vector3(10f, 2.2f, back - 0.05f), new Vector3(4f, 4.2f, 0.1f), Mat("Kepenk", new Color(0.7f, 0.72f, 0.75f)), 0.5f, false);
        foreach (float x in new[] { 7.7f, 12.3f })
            SBox(root, "Rampa Tamponu", new Vector3(x, 0.6f, back - 0.15f), new Vector3(0.3f, 0.4f, 0.2f), Mat("Lastik", new Color(0.08f, 0.08f, 0.08f)), false);
        Label(root, "MAL KABUL", root.TransformPoint(new Vector3(10f, 4.8f, back - 0.12f)), yaw, 1.1f, true, Color.white, BrandOrange);
        SBox(root, "Personel Kapısı", new Vector3(-8f, 1.15f, back - 0.03f), new Vector3(1f, 2.2f, 0.06f), Mat("Kapi", new Color(0.25f, 0.18f, 0.12f)), false);
    }

    // =====================================================================
    // GİRİŞ: otomatik kapı, güvenlik kapısı, paspas, bilgi panosu
    // =====================================================================

    static void StoreEntrance(Transform root)
    {
        float hd = StoreHalfD, yaw = root.eulerAngles.y;
        var frameMat = Mat("Aluminyum", new Color(0.28f, 0.3f, 0.33f));

        var doorRoot = new GameObject("Otomatik Kapı").transform;
        doorRoot.SetParent(root, false);
        doorRoot.localPosition = new Vector3(0f, StoreFloor, hd);

        Transform Panel(string name, float x)
        {
            var p = new GameObject(name).transform;
            p.SetParent(doorRoot, false);
            p.localPosition = new Vector3(x, 0f, -0.22f);
            SBox(p, "Cam", new Vector3(0f, 1.42f, 0f), new Vector3(1.96f, 2.84f, 0.04f), GlassMat(), true);
            SBox(p, "Kasa", new Vector3(-0.98f, 1.42f, 0f), new Vector3(0.06f, 2.84f, 0.07f), frameMat, false);
            SBox(p, "Kasa", new Vector3(0.98f, 1.42f, 0f), new Vector3(0.06f, 2.84f, 0.07f), frameMat, false);
            SBox(p, "Kasa", new Vector3(0f, 2.8f, 0f), new Vector3(1.96f, 0.08f, 0.07f), frameMat, false);
            SBox(p, "Kasa", new Vector3(0f, 0.04f, 0f), new Vector3(1.96f, 0.08f, 0.07f), frameMat, false);
            var rb = p.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; // hareket eden çarpışıcı
            return p;
        }

        var left = Panel("Sol Kanat", -1f);
        var right = Panel("Sağ Kanat", 1f);
        SBox(doorRoot, "Kapı Motoru", new Vector3(0f, 2.98f, -0.33f), new Vector3(8.2f, 0.25f, 0.18f), frameMat, false);

        // Güvenlik kapıları (ödenmemiş ürünle kırmızı yanar)
        var gateMat = Mat("GuvenlikKapisi", new Color(0.92f, 0.92f, 0.94f));
        var alarm = GlowMat("AlarmIsik", new Color(0.2f, 1f, 0.35f), 2f);
        var lights = new List<Renderer>();
        foreach (float x in new[] { -2.5f, 2.5f })
        {
            SBox(doorRoot, "Güvenlik Kapısı", new Vector3(x, 0.75f, -1.3f), new Vector3(0.12f, 1.5f, 0.6f), gateMat, true);
            lights.Add(SBox(doorRoot, "Alarm Işığı", new Vector3(x, 1.53f, -1.3f), new Vector3(0.14f, 0.06f, 0.62f), alarm, false).GetComponent<Renderer>());
        }

        var door = doorRoot.gameObject.AddComponent<AutomaticDoor>();
        door.leftPanel = left;
        door.rightPanel = right;
        door.alarmLights = lights.ToArray();

        // İçeriden kapının üstünde ÇIKIŞ
        Label(root, "ÇIKIŞ", root.TransformPoint(new Vector3(0f, 3.55f,hd - StoreWallT / 2f - 0.1f)), yaw, 0.9f, true, Color.white, new Color(0.1f, 0.55f, 0.25f));

        // Paspas
        SBox(root, "Paspas", new Vector3(0f, StoreFloor + 0.005f, hd - 1.3f), new Vector3(3.6f, 0.01f, 1.8f), Mat("Paspas", new Color(0.16f, 0.17f, 0.18f)), false);

        // Bilgi panosu (girerken solda)
        var board = new GameObject("Bilgi Panosu").transform;
        board.SetParent(root, false);
        board.localPosition = new Vector3(5.2f, StoreFloor, 8.6f);
        foreach (float x in new[] { -1.2f, 1.2f })
            SBox(board, "Ayak", new Vector3(x, 1.0f, 0f), new Vector3(0.06f, 2f, 0.06f), Mat("Metal", new Color(0.4f, 0.42f, 0.45f)), false);
        SBox(board, "Pano", new Vector3(0f, 1.55f, 0f), new Vector3(2.6f, 1.25f, 0.05f), Mat("BeyazPano", new Color(0.96f, 0.96f, 0.95f)), true);
        SBox(board, "Pano Başlığı", new Vector3(0f, 2.06f, 0.001f), new Vector3(2.6f, 0.24f, 0.052f), Mat("ToptanciTuruncu", BrandOrange), false);
        string[] lines =
        {
            "1. Girişin sağındaki arabalardan birini al  [E]",
            "2. Raflardaki ürünleri arabaya ekle  [E]",
            "3. Arabayı kasanın yanındaki şeride getir",
            "4. Kasaya bak ve öde  [E]",
            "Ödenmemiş ürünle çıkış kapısı açılmaz!",
        };
        // Pano girişe (+z) bakar; yazılar ön yüzde
        Label(board, "NASIL ALIŞVERİŞ YAPILIR?", board.TransformPoint(new Vector3(0f, 2.06f, 0.06f)), yaw + 180f, 0.5f, false, Color.white);
        for (int i = 0; i < lines.Length; i++)
            Label(board, lines[i], board.TransformPoint(new Vector3(0f, 1.82f - i * 0.2f, 0.04f)), yaw + 180f, 0.3f, false,
                i == lines.Length - 1 ? new Color(0.75f, 0.1f, 0.05f) : new Color(0.12f, 0.12f, 0.12f));
    }

    // =====================================================================
    // RAFLAR VE ÜRÜNLER
    // =====================================================================

    static void StoreShelves(Transform root, Dictionary<string, ItemData> items, Dictionary<string, ItemBox> tpl)
    {
        float yaw = root.eulerAngles.y;
        ItemData I(string id) => items.TryGetValue(id, out var d) ? d : null;
        ItemBox T(string id) => tpl.TryGetValue(id, out var b) ? b : null;
        void Section(Transform unit, string id, float x0, float x1, float[] levels) => ProductSection(unit, I(id), T(id), x0, x1, levels);

        // G1: işlemci | bellek - SSD
        var g1a = ShelfUnit(root, "Reyon 1 (işlemci)", new Vector3(-11.25f, StoreFloor, -3f), -90f, 7f, ShelfLevels);
        var g1b = ShelfUnit(root, "Reyon 1 (bellek-ssd)", new Vector3(-10.75f, StoreFloor, -3f), 90f, 7f, ShelfLevels);
        Section(g1a, "cpu_r5", -3.5f, 0f, ShelfLevels);
        Section(g1a, "cpu_r7", 0f, 3.5f, ShelfLevels);
        Section(g1b, "ram_16", -3.5f, 0f, ShelfLevels);
        Section(g1b, "ssd_1tb", 0f, 3.5f, ShelfLevels);
        AisleSign(root, "İŞLEMCİ  |  BELLEK - SSD", new Vector3(-11f, 4.3f, 0.6f));

        // G2: anakart | bilgisayar kasası (yüksek raflı)
        var g2a = ShelfUnit(root, "Reyon 2 (anakart)", new Vector3(-3.25f, StoreFloor, -3f), -90f, 7f, ShelfLevels);
        var g2b = ShelfUnit(root, "Reyon 2 (kasa)", new Vector3(-2.75f, StoreFloor, -3f), 90f, 7f, TallShelfLevels);
        Section(g2a, "mb_b60", -3.5f, 3.5f, ShelfLevels);
        Section(g2b, "case_k1", -3.5f, 3.5f, TallShelfLevels);
        AisleSign(root, "ANAKART  |  BİLGİSAYAR KASASI", new Vector3(-3f, 4.3f, 0.6f));

        // Sol duvar: ekran kartı + güç kaynağı
        float wallX = StoreHalfW - StoreWallT / 2f - 0.03f - 0.25f;
        var left = ShelfUnit(root, "Duvar Rafı (ekran kartı-güç)", new Vector3(-wallX, StoreFloor, -2f), 90f, 10f, ShelfLevels);
        Section(left, "gpu_gx460", -5f, 0f, ShelfLevels);
        Section(left, "psu_650", 0f, 5f, ShelfLevels);
        Label(root, "EKRAN KARTI  -  GÜÇ KAYNAĞI", root.TransformPoint(new Vector3(-wallX - 0.2f, 3.9f, -2f)), yaw - 90f, 1.2f, true, Color.white, BrandOrange);

        // Sağ duvar: konsollar + TV duvarı
        var right = ShelfUnit(root, "Duvar Rafı (konsol)", new Vector3(wallX, StoreFloor, -3f), -90f, 8f, ShelfLevels);
        Section(right, "con_pz5", -4f, 0f, ShelfLevels);
        Section(right, "con_pz5pro", 0f, 4f, ShelfLevels);
        Label(root, "KONSOL DÜNYASI", root.TransformPoint(new Vector3(wallX + 0.2f, 5.2f, -3f)), yaw + 90f, 1.3f, true, Color.white, BrandOrange);
        var tvFrame = Mat("Monitor", new Color(0.05f, 0.05f, 0.08f));
        var screen = GlowMat("TVEkran", new Color(0.08f, 0.25f, 0.55f), 1.6f);
        foreach (float z in new[] { -6f, -3f, 0f })
        {
            float sx = wallX + 0.22f;
            SBox(root, "TV", new Vector3(sx, 3.35f, z), new Vector3(0.08f, 1.05f, 1.8f), tvFrame, false);
            SBox(root, "TV Ekranı", new Vector3(sx - 0.045f, 3.35f, z), new Vector3(0.01f, 0.95f, 1.7f), screen, false);
            Label(root, "PlayZone 5 Pro", root.TransformPoint(new Vector3(sx - 0.07f, 3.35f, z)), yaw + 90f, 0.5f, false, Color.white);
        }

        // Promosyon masası
        PromoIsland(root, I("con_pz5pro"), T("con_pz5pro"));
    }

    /// Tek yüzlü raf ünitesi. Ünitenin yerel +z'si ön (koridor), x'i uzunluğu.
    static Transform ShelfUnit(Transform parent, string name, Vector3 localPos, float localYaw, float length, float[] levels)
    {
        var u = new GameObject(name).transform;
        u.SetParent(parent, false);
        u.localPosition = localPos;
        u.localRotation = Quaternion.Euler(0f, localYaw, 0f);

        var metal = Mat("RafMetal", new Color(0.86f, 0.87f, 0.89f));
        var back = Mat("RafSirt", new Color(0.7f, 0.73f, 0.77f));
        var rail = Mat("ToptanciTuruncu", BrandOrange);
        var plinth = Mat("RafKaide", new Color(0.2f, 0.21f, 0.23f));
        const float D = 0.5f, H = 2.1f;

        SBox(u, "Kaide", new Vector3(0f, 0.06f, 0f), new Vector3(length, 0.12f, D), plinth, false);
        SBox(u, "Sırt Panosu", new Vector3(0f, H / 2f, -D / 2f + 0.02f), new Vector3(length, H, 0.04f), back, false);
        foreach (float y in levels)
        {
            SBox(u, "Raf", new Vector3(0f, y - 0.015f, 0.01f), new Vector3(length, 0.03f, D - 0.02f), metal, false);
            SBox(u, "Fiyat Rayı", new Vector3(0f, y - 0.035f, D / 2f + 0.006f), new Vector3(length, 0.07f, 0.012f), rail, false);
        }
        foreach (float sx in new[] { -length / 2f, length / 2f })
            SBox(u, "Dikme", new Vector3(sx, H / 2f, 0f), new Vector3(0.05f, H, D), metal, false);
        SBox(u, "Üst Bant", new Vector3(0f, H + 0.05f, 0f), new Vector3(length + 0.05f, 0.1f, D), rail, false);

        var col = u.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, H / 2f, 0f);
        col.size = new Vector3(length, H, D);
        return u;
    }

    /// Raf ünitesinin x0…x1 aralığına bir ürünü dizer: kutular (tek mesh), etiketler, fiyatlar ve [E] ile alma alanı.
    static void ProductSection(Transform unit, ItemData item, ItemBox template, float x0, float x1, float[] levels)
    {
        if (item == null) return;
        var sec = new GameObject("Ürün - " + item.displayName).transform;
        sec.SetParent(unit, false);

        Vector3 b = item.boxSize;
        float width = x1 - x0, mid = (x0 + x1) / 2f;
        const float gap = 0.03f;
        int nx = Mathf.Max(1, Mathf.FloorToInt((width - 0.1f + gap) / (b.x + gap)));
        int nz = Mathf.Clamp(Mathf.FloorToInt(0.44f / (b.z + 0.02f)), 1, 3);
        float spanX = nx * (b.x + gap) - gap;

        var boxes = new List<Matrix4x4>();
        var stickers = new List<Matrix4x4>();
        float unitYaw = unit.eulerAngles.y;
        for (int li = 0; li < levels.Length; li++)
        {
            float y = levels[li];
            float ceil = li + 1 < levels.Length ? levels[li + 1] - 0.04f : 2.05f;
            int ns = Mathf.Clamp(Mathf.FloorToInt((ceil - y) / (b.y + 0.004f)), 0, 3);
            if (ns == 0) continue;

            for (int s = 0; s < ns; s++)
            for (int k = 0; k < nz; k++)
            for (int i = 0; i < nx; i++)
            {
                float px = mid - spanX / 2f + b.x / 2f + i * (b.x + gap);
                float pz = 0.23f - b.z / 2f - k * (b.z + 0.02f);
                float py = y + b.y / 2f + s * (b.y + 0.004f);
                boxes.Add(Matrix4x4.TRS(new Vector3(px, py, pz), Quaternion.identity, b));
                if (k == 0)
                    stickers.Add(Matrix4x4.TRS(new Vector3(px, py, pz + b.z / 2f + 0.002f), Quaternion.identity,
                        new Vector3(b.x * 0.62f, b.y * 0.38f, 0.003f)));
            }

            // Her katta fiyat etiketi
            Label(sec, MoneyManager.Format(item.wholesalePrice), unit.TransformPoint(new Vector3(mid, y - 0.035f, 0.295f)),
                unitYaw + 180f, 0.3f, true, Color.black, new Color(1f, 0.85f, 0.1f));
        }
        CombinedCubes(sec, "Kutular", boxes, Mat("Kutu_" + item.itemId, item.boxColor));
        CombinedCubes(sec, "Kutu Etiketleri", stickers, Mat("KutuEtiket", new Color(0.95f, 0.95f, 0.93f)));

        // Ürün adı rafın üstünde
        Label(sec, item.displayName, unit.TransformPoint(new Vector3(mid, 2.33f, 0f)), unitYaw + 180f, 0.5f);

        // [E] ile alma alanı (rafın önünü kaplayan tetik)
        var trig = new GameObject("Al - " + item.displayName);
        trig.transform.SetParent(sec, false);
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(mid, 1.0f, 0.17f);
        bc.size = new Vector3(width - 0.04f, 1.9f, 0.24f);
        var sp = trig.AddComponent<ShelfProduct>();
        sp.item = item;
        sp.boxTemplate = template;
    }

    /// Tavandan sarkan reyon tabelası (girişten okunur).
    static void AisleSign(Transform root, string text, Vector3 local)
    {
        float yaw = root.eulerAngles.y;
        Label(root, text, root.TransformPoint(local), yaw + 180f, 0.8f, true, Color.white, BrandOrange);
        float half = 0.14f * 0.8f * text.Length / 2f - 0.2f;
        foreach (float dx in new[] { -half, half })
        {
            float top = StoreH - 0.3f, bottom = local.y + 0.15f;
            SBox(root, "Askı Teli", new Vector3(local.x + dx, (top + bottom) / 2f, local.z + 0.03f), new Vector3(0.015f, top - bottom, 0.015f),
                Mat("Metal", new Color(0.4f, 0.42f, 0.45f)), false);
        }
    }

    static void PromoIsland(Transform root, ItemData item, ItemBox template)
    {
        if (item == null) return;
        float yaw = root.eulerAngles.y;
        var isl = new GameObject("Promosyon Masası").transform;
        isl.SetParent(root, false);
        isl.localPosition = new Vector3(9f, StoreFloor, -3.5f);

        SBox(isl, "Masa Gövdesi", new Vector3(0f, 0.4f, 0f), new Vector3(2.4f, 0.8f, 1.2f), Mat("ToptanciTuruncu", BrandOrange), true);
        SBox(isl, "Masa Üstü", new Vector3(0f, 0.82f, 0f), new Vector3(2.6f, 0.04f, 1.4f), Mat("TezgahUst", new Color(0.82f, 0.82f, 0.8f)), false);

        // Piramit gibi dizilmiş kutular
        Vector3 b = item.boxSize;
        var boxes = new List<Matrix4x4>();
        var stickers = new List<Matrix4x4>();
        int[] cols = { 4, 3, 2 };
        int[] rows = { 2, 1, 1 };
        for (int l = 0; l < cols.Length; l++)
        for (int r = 0; r < rows[l]; r++)
        for (int c = 0; c < cols[l]; c++)
        {
            float px = (c - (cols[l] - 1) / 2f) * (b.x + 0.02f);
            float pz = (r - (rows[l] - 1) / 2f) * (b.z + 0.02f);
            float py = 0.84f + b.y / 2f + l * (b.y + 0.004f);
            boxes.Add(Matrix4x4.TRS(new Vector3(px, py, pz), Quaternion.identity, b));
            if (r == rows[l] - 1)
                stickers.Add(Matrix4x4.TRS(new Vector3(px, py, pz + b.z / 2f + 0.002f), Quaternion.identity, new Vector3(b.x * 0.62f, b.y * 0.38f, 0.003f)));
        }
        CombinedCubes(isl, "Kutular", boxes, Mat("Kutu_" + item.itemId, item.boxColor));
        CombinedCubes(isl, "Kutu Etiketleri", stickers, Mat("KutuEtiket", new Color(0.95f, 0.95f, 0.93f)));

        // Pano
        SBox(isl, "Pano Ayağı", new Vector3(0f, 1.6f, -0.85f), new Vector3(0.06f, 1.6f, 0.06f), Mat("Metal", new Color(0.4f, 0.42f, 0.45f)), false);
        SBox(isl, "Pano", new Vector3(0f, 2.45f, -0.85f), new Vector3(1.8f, 1.1f, 0.04f), Mat("PromoPano", new Color(0.1f, 0.12f, 0.18f)), false);
        Label(isl, "YENİ!", isl.TransformPoint(new Vector3(0f, 2.75f, -0.8f)), yaw + 180f, 1f, false, BrandOrange);
        Label(isl, item.displayName, isl.TransformPoint(new Vector3(0f, 2.42f, -0.8f)), yaw + 180f, 0.5f, false, Color.white);
        Label(isl, MoneyManager.Format(item.wholesalePrice), isl.TransformPoint(new Vector3(0f, 2.12f, -0.8f)), yaw + 180f, 0.6f, false, new Color(1f, 0.85f, 0.1f));
        Label(isl, MoneyManager.Format(item.wholesalePrice), isl.TransformPoint(new Vector3(0f, 0.6f, 0.65f)), yaw + 180f, 0.4f, true, Color.black, new Color(1f, 0.85f, 0.1f));

        var trig = new GameObject("Al - " + item.displayName);
        trig.transform.SetParent(isl, false);
        var bc = trig.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center = new Vector3(0f, 1.15f, 0f);
        bc.size = new Vector3(2.6f, 0.65f, 1.4f);
        var sp = trig.AddComponent<ShelfProduct>();
        sp.item = item;
        sp.boxTemplate = template;
    }

    // =====================================================================
    // PALET RAFLARI (arka duvar, depo stoğu — sadece görüntü)
    // =====================================================================

    static void StoreRacking(Transform root)
    {
        var rack = new GameObject("Palet Rafları").transform;
        rack.SetParent(root, false);
        float zBack = -StoreHalfD + StoreWallT / 2f + 0.1f, depth = 1.1f, zc = zBack + depth / 2f;
        rack.localPosition = new Vector3(0f, StoreFloor, 0f);

        var blue = Mat("RafDikme", new Color(0.12f, 0.3f, 0.6f));
        var beam = Mat("ToptanciTuruncu", BrandOrange);
        var wood = Mat("Palet", new Color(0.62f, 0.48f, 0.3f));
        Material[] cartons =
        {
            Mat("Koli_1", new Color(0.66f, 0.5f, 0.33f)),
            Mat("Koli_2", new Color(0.72f, 0.57f, 0.38f)),
            Mat("Koli_3", new Color(0.58f, 0.44f, 0.29f)),
        };

        const int bays = 13;
        const float bayW = 2.7f, H = 6f;
        float x0 = -bays * bayW / 2f;
        float[] beamLevels = { 2.2f, 4.2f };
        var rng = new System.Random(42);

        for (int i = 0; i <= bays; i++)
        {
            float x = x0 + i * bayW;
            foreach (float z in new[] { zBack + 0.05f, zBack + depth - 0.05f })
                SBox(rack, "Dikme", new Vector3(x, H / 2f, z), new Vector3(0.09f, H, 0.09f), blue, false);
        }

        for (int i = 0; i < bays; i++)
        {
            float cx = x0 + (i + 0.5f) * bayW;
            foreach (float y in beamLevels)
            foreach (float z in new[] { zBack + 0.05f, zBack + depth - 0.05f })
                SBox(rack, "Kiriş", new Vector3(cx, y, z), new Vector3(bayW - 0.09f, 0.12f, 0.06f), beam, false);

            float[] floors = { 0f, beamLevels[0] + 0.06f, beamLevels[1] + 0.06f };
            for (int f = 0; f < floors.Length; f++)
            for (int p = 0; p < 2; p++)
            {
                if (rng.NextDouble() < 0.12) continue; // birkaç boş göz
                float px = cx + (p - 0.5f) * 1.25f;
                float maxH = f < floors.Length - 1 ? (beamLevels[f] - floors[f] - 0.3f) : 1.4f;
                float h = Mathf.Lerp(0.9f, maxH, (float)rng.NextDouble());
                SBox(rack, "Palet", new Vector3(px, floors[f] + 0.07f, zc), new Vector3(1.15f, 0.14f, 1.0f), wood, false);
                SBox(rack, "Koli Yığını", new Vector3(px, floors[f] + 0.14f + h / 2f, zc), new Vector3(1.1f, h, 0.95f), cartons[rng.Next(cartons.Length)], false);
            }
        }

        var col = rack.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, H / 2f, zc);
        col.size = new Vector3(bays * bayW, H, depth);

        float yaw = root.eulerAngles.y;
        Label(root, "DEPO STOĞU", root.TransformPoint(new Vector3(-6f, 1.9f + StoreFloor, zBack + depth + 0.02f)), yaw + 180f, 0.8f, true, Color.white, new Color(0.12f, 0.3f, 0.6f));
        Label(root, "DEPO STOĞU", root.TransformPoint(new Vector3(6f, 1.9f + StoreFloor, zBack + depth + 0.02f)), yaw + 180f, 0.8f, true, Color.white, new Color(0.12f, 0.3f, 0.6f));
    }

    // =====================================================================
    // KASALAR
    // =====================================================================

    static void StoreCheckouts(Transform root)
    {
        var body = Mat("KasaGovde", new Color(0.24f, 0.26f, 0.3f));
        var orange = Mat("ToptanciTuruncu", BrandOrange);
        var top = Mat("TezgahUst", new Color(0.82f, 0.82f, 0.8f));
        var belt = Mat("Bant", new Color(0.08f, 0.08f, 0.09f));
        var metal = Mat("RafMetal", new Color(0.86f, 0.87f, 0.89f));
        var dark = Mat("Monitor", new Color(0.05f, 0.05f, 0.08f));

        int lane = 1;
        foreach (float cx in new[] { 9f, 14f })
        {
            var c = new GameObject($"Kasa {lane}").transform;
            c.SetParent(root, false);
            c.localPosition = new Vector3(cx, StoreFloor, 4.6f);
            float yaw = c.eulerAngles.y;

            SBox(c, "Gövde", new Vector3(0f, 0.45f, 0f), new Vector3(0.8f, 0.9f, 3.2f), body, true);
            SBox(c, "Ön Panel", new Vector3(0.405f, 0.5f, 0f), new Vector3(0.012f, 0.55f, 3f), orange, false);
            SBox(c, "Tezgâh Üstü", new Vector3(0f, 0.915f, 0f), new Vector3(0.86f, 0.03f, 3.26f), top, false);
            SBox(c, "Bant", new Vector3(0f, 0.935f, -0.45f), new Vector3(0.56f, 0.015f, 2.1f), belt, false);
            foreach (float bx in new[] { -0.3f, 0.3f })
                SBox(c, "Bant Kenarı", new Vector3(bx, 0.95f, -0.45f), new Vector3(0.03f, 0.04f, 2.1f), metal, false);
            SBox(c, "Okuyucu Camı", new Vector3(0f, 0.935f, 0.85f), new Vector3(0.34f, 0.015f, 0.34f), Mat("OkuyucuCam", new Color(0.05f, 0.07f, 0.08f)), false);
            SBox(c, "Okuyucu Işığı", new Vector3(0f, 0.944f, 0.85f), new Vector3(0.3f, 0.003f, 0.02f), GlowMat("OkuyucuIsik", new Color(1f, 0.1f, 0.05f), 2f), false);

            // Kasiyer ekranı (kasiyere dönük) ve müşteri ekranı (şeride dönük)
            SBox(c, "POS Ayağı", new Vector3(-0.2f, 1.05f, 1.3f), new Vector3(0.05f, 0.25f, 0.05f), metal, false);
            SBox(c, "POS Ekranı", new Vector3(-0.22f, 1.25f, 1.3f), new Vector3(0.04f, 0.28f, 0.4f), dark, false);
            SBox(c, "POS Görüntüsü", new Vector3(-0.243f, 1.25f, 1.3f), new Vector3(0.005f, 0.24f, 0.36f), GlowMat("POSEkran", new Color(0.2f, 0.5f, 0.8f), 1.2f), false);
            SBox(c, "Müşteri Ekranı", new Vector3(0.32f, 1.12f, 1.35f), new Vector3(0.03f, 0.26f, 0.55f), dark, false);
            var text = Label(c, "HOŞ GELDİNİZ", c.TransformPoint(new Vector3(0.345f, 1.12f, 1.35f)), yaw - 90f, 0.24f, false, new Color(0.4f, 1f, 0.5f));
            SBox(c, "Kart Okuyucu", new Vector3(0.3f, 0.98f, 1.0f), new Vector3(0.08f, 0.1f, 0.14f), dark, false);
            SBox(c, "Poşet Rafı", new Vector3(0f, 0.75f, 1.75f), new Vector3(0.7f, 0.04f, 0.3f), metal, false);

            // Kasa numarası direği
            SBox(c, "Kasa Direği", new Vector3(0.36f, 1.55f, -1.5f), new Vector3(0.05f, 1.3f, 0.05f), metal, false);
            SBox(c, "Kasa Lambası", new Vector3(0.36f, 2.35f, -1.5f), new Vector3(0.6f, 0.32f, 0.12f), GlowMat("KasaLambasi", BrandOrange, 1.5f), false);
            Label(c, $"KASA {lane}", c.TransformPoint(new Vector3(0.36f, 2.35f, -1.58f)), yaw, 0.4f, false, Color.white);
            Label(c, $"KASA {lane}", c.TransformPoint(new Vector3(0.36f, 2.35f, -1.42f)), yaw + 180f, 0.4f, false, Color.white);

            Cashier(c, new Vector3(-0.85f, 0f, 0.9f), 90f);

            var counter = c.gameObject.AddComponent<CheckoutCounter>();
            counter.display = text.GetComponent<TextMesh>();
            lane++;
        }
    }

    static void Cashier(Transform parent, Vector3 pos, float yaw)
    {
        var r = new GameObject("Kasiyer").transform;
        r.SetParent(parent, false);
        r.localPosition = pos;
        r.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var vest = Mat("KasiyerYelek", BrandOrange * 0.9f);
        var shirt = Mat("KasiyerGomlek", new Color(0.9f, 0.9f, 0.92f));
        var pants = Mat("Pantolon", new Color(0.15f, 0.17f, 0.22f));
        var skin = Mat("Ten", new Color(0.87f, 0.7f, 0.58f));
        var hair = Mat("Sac", new Color(0.15f, 0.1f, 0.07f));

        foreach (float x in new[] { -0.1f, 0.1f })
            SBox(r, "Bacak", new Vector3(x, 0.45f, 0f), new Vector3(0.14f, 0.9f, 0.16f), pants, false);
        Prim(PrimitiveType.Capsule, r, "Gövde", new Vector3(0f, 1.2f, 0f), new Vector3(0.42f, 0.33f, 0.26f), vest, true);
        foreach (float x in new[] { -0.27f, 0.27f })
            SBox(r, "Kol", new Vector3(x, 1.18f, 0.06f), new Vector3(0.1f, 0.5f, 0.1f), shirt, false);
        Prim(PrimitiveType.Sphere, r, "Baş", new Vector3(0f, 1.68f, 0f), new Vector3(0.23f, 0.25f, 0.23f), skin, false);
        Prim(PrimitiveType.Sphere, r, "Saç", new Vector3(0f, 1.74f, -0.02f), new Vector3(0.25f, 0.18f, 0.25f), hair, false);
    }

    // =====================================================================
    // ALIŞVERİŞ ARABALARI (girişin sağında)
    // =====================================================================

    static void StoreCarts(Transform root)
    {
        for (int i = 0; i < 4; i++)
            BuildShoppingCart(root, new Vector3(-4.7f - i * 0.75f, StoreFloor, 8.15f), 180f, i + 1);

        float yaw = root.eulerAngles.y;
        Label(root, "ALIŞVERİŞ ARABALARI", root.TransformPoint(new Vector3(-5.8f, 2.8f, 8.3f)), yaw + 180f, 0.55f, true, Color.white, BrandOrange);
        float wireBottom = 2.9f, wireTop = StoreH - 0.3f;
        foreach (float dx in new[] { -1.2f, 1.2f })
            SBox(root, "Askı Teli", new Vector3(-5.8f + dx, (wireBottom + wireTop) / 2f, 8.33f), new Vector3(0.015f, wireTop - wireBottom, 0.015f),
                Mat("Metal", new Color(0.4f, 0.42f, 0.45f)), false);
    }

    static void BuildShoppingCart(Transform parent, Vector3 localPos, float localYaw, int n)
    {
        var cart = new GameObject($"Alışveriş Arabası {n}").transform;
        cart.SetParent(parent, false);
        cart.localPosition = localPos;
        cart.localRotation = Quaternion.Euler(0f, localYaw, 0f);

        var wire = CutoutMat("SepetTel", WireMeshTexture(), new Color(0.82f, 0.84f, 0.86f));
        var chrome = Mat("Krom", new Color(0.75f, 0.77f, 0.8f));
        var red = Mat("ArabaKolu", new Color(0.8f, 0.12f, 0.1f));
        var rubber = Mat("Lastik", new Color(0.08f, 0.08f, 0.08f));

        // Tekerlekler (görsel) + temas küreleri (küçük basamaklara takılmasın)
        foreach (float wx in new[] { -0.22f, 0.22f })
        foreach (float wz in new[] { -0.33f, 0.33f })
        {
            Prim(PrimitiveType.Cylinder, cart, "Teker", new Vector3(wx, 0.06f, wz), new Vector3(0.12f, 0.015f, 0.12f), rubber, false, new Vector3(0f, 0f, 90f));
            SBox(cart, "Teker Çatalı", new Vector3(wx, 0.12f, wz), new Vector3(0.02f, 0.08f, 0.04f), chrome, false);
            var sc = cart.gameObject.AddComponent<SphereCollider>();
            sc.center = new Vector3(wx, 0.06f, wz);
            sc.radius = 0.06f;
        }

        // Şasi, alt tepsi, dikmeler
        foreach (float x in new[] { -0.22f, 0.22f })
            SBox(cart, "Şasi", new Vector3(x, 0.17f, 0f), new Vector3(0.025f, 0.025f, 0.8f), chrome, false);
        UVBox(cart, "Alt Tepsi", new Vector3(0f, 0.19f, 0f), new Vector3(0.46f, 0.015f, 0.62f), wire, 0.1f, false);
        foreach (float x in new[] { -0.24f, 0.24f })
        foreach (float z in new[] { -0.36f, 0.36f })
            SBox(cart, "Dikme", new Vector3(x, 0.31f, z), new Vector3(0.022f, 0.28f, 0.022f), chrome, false);

        // Tel sepet (çarpışıcılı: kutular ve duvarlar buna çarpar)
        UVBox(cart, "Sepet Tabanı", new Vector3(0f, 0.44f, 0.02f), new Vector3(0.56f, 0.02f, 0.86f), wire, 0.1f, true);
        UVBox(cart, "Sepet Sol", new Vector3(-0.29f, 0.66f, 0.02f), new Vector3(0.02f, 0.42f, 0.86f), wire, 0.1f, true);
        UVBox(cart, "Sepet Sağ", new Vector3(0.29f, 0.66f, 0.02f), new Vector3(0.02f, 0.42f, 0.86f), wire, 0.1f, true);
        UVBox(cart, "Sepet Ön", new Vector3(0f, 0.66f, 0.45f), new Vector3(0.58f, 0.42f, 0.02f), wire, 0.1f, true);
        UVBox(cart, "Sepet Arka", new Vector3(0f, 0.68f, -0.41f), new Vector3(0.58f, 0.46f, 0.02f), wire, 0.1f, true);

        // Üst çerçeve ve tutma kolu
        SBox(cart, "Çerçeve", new Vector3(0f, 0.875f, 0.45f), new Vector3(0.6f, 0.02f, 0.02f), chrome, false);
        SBox(cart, "Çerçeve", new Vector3(0f, 0.915f, -0.41f), new Vector3(0.6f, 0.02f, 0.02f), chrome, false);
        foreach (float x in new[] { -0.29f, 0.29f })
        {
            SBox(cart, "Çerçeve", new Vector3(x, 0.875f, 0.02f), new Vector3(0.02f, 0.02f, 0.88f), chrome, false);
            SBox(cart, "Kol Bağlantısı", new Vector3(x * 0.86f, 0.95f, -0.47f), new Vector3(0.02f, 0.12f, 0.12f), chrome, false);
        }
        Prim(PrimitiveType.Cylinder, cart, "Tutma Kolu", new Vector3(0f, 1.0f, -0.52f), new Vector3(0.04f, 0.3f, 0.04f), red, false, new Vector3(0f, 0f, 90f));

        var rb = cart.gameObject.AddComponent<Rigidbody>();
        rb.mass = 15f;

        // Sepet yuvaları (yerleri ShoppingCart kutu boyutuna göre ayarlar)
        for (int i = 0; i < 12; i++)
        {
            var slot = CreateSlot(cart, $"Araba {n} - {i + 1}", new Vector3(0f, 0.46f, 0.02f));
            var bc = slot.GetComponent<BoxCollider>();
            bc.size = new Vector3(0.2f, 0.2f, 0.2f);
            bc.center = new Vector3(0f, 0.1f, 0f);
        }

        cart.gameObject.AddComponent<ShoppingCart>();
    }

    // =====================================================================
    // YARDIMCILAR
    // =====================================================================

    /// Ebeveyne göre yerel kutu; collider=false ise çarpışıcı silinir.
    static GameObject SBox(Transform parent, string name, Vector3 c, Vector3 s, Material m, bool collider = true)
    {
        var go = LocalBlock(parent, name, c, s, m);
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    static GameObject Prim(PrimitiveType type, Transform parent, string name, Vector3 pos, Vector3 scale, Material m, bool collider, Vector3? euler = null)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = m;
        if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    /// Doku her yüzde metre cinsinden tekrarlanan kutu (ölçekleyince doku uzamasın diye).
    /// UV'ler ebeveyn uzayındaki konumdan gelir; yan yana parçalarda desen kesintisiz devam eder.
    static GameObject UVBox(Transform parent, string name, Vector3 c, Vector3 s, Material mat, float uvMeters, bool collider)
    {
        var md = new MeshData();
        Vector3 h = s / 2f;
        Vector3 P(float sx, float sy, float sz) => c + Vector3.Scale(h, new Vector3(sx, sy, sz));
        Vector2 Uz(Vector3 v) => new Vector2(v.x, v.y) / uvMeters;
        Vector2 Ux(Vector3 v) => new Vector2(v.z, v.y) / uvMeters;
        Vector2 Uy(Vector3 v) => new Vector2(v.x, v.z) / uvMeters;
        void Face(Vector3 a, Vector3 b, Vector3 cc, Vector3 d, System.Func<Vector3, Vector2> uv) => md.Quad(a, b, cc, d, uv(a), uv(b), uv(cc), uv(d));

        Face(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), Uz);       // +z
        Face(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Uz);   // -z
        Face(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Ux);       // +x
        Face(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), Ux);   // -x
        Face(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Uy);       // üst
        Face(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), Uy);   // alt

        var go = MeshObject(parent, name, md, mat, false);
        if (collider)
        {
            var bc = go.AddComponent<BoxCollider>();
            bc.center = c;
            bc.size = s;
        }
        return go;
    }

    static Mesh cubeMesh;

    /// Çok sayıda kutuyu tek mesh'te birleştirir (raftaki yüzlerce kutu = tek çizim).
    static void CombinedCubes(Transform parent, string name, List<Matrix4x4> transforms, Material mat)
    {
        if (transforms.Count == 0) return;
        if (cubeMesh == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
        }
        var ci = new CombineInstance[transforms.Count];
        for (int i = 0; i < ci.Length; i++) ci[i] = new CombineInstance { mesh = cubeMesh, transform = transforms[i] };

        var mesh = new Mesh { name = name };
        if (transforms.Count * 24 > 65000) mesh.indexFormat = IndexFormat.UInt32;
        mesh.CombineMeshes(ci, true, true);

        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static Material GlowMat(string key, Color color, float intensity)
    {
        var m = Mat(key, color);
        m.color = color;
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// Kesik (alpha clip) malzeme: tel sepet gibi delikli yüzeyler, iki taraflı.
    static Material CutoutMat(string key, Texture2D tex, Color color)
    {
        var m = TexMat(key, tex, color, 0.7f);
        if (m.HasProperty("_AlphaClip"))
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetOverrideTag("RenderType", "TransparentCutout");
            m.renderQueue = (int)RenderQueue.AlphaTest;
        }
        if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.8f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Dokular ----------

    /// Cilalı karo: 2×2 karo (60 cm), ince derz, hafif benekli. 256 px = 1.2 m.
    static Texture2D FloorTileTexture() => LoadOrCreateTexture("T_ToptanciKaro", 256, () =>
    {
        const int S = 256, Tile = 128;
        var rng = new System.Random(21);
        var shade = new float[4];
        for (int i = 0; i < 4; i++) shade[i] = 0.84f + (float)rng.NextDouble() * 0.04f;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v;
                if (x % Tile < 2 || y % Tile < 2) v = 0.6f;
                else
                {
                    v = shade[(x / Tile) + 2 * (y / Tile)] + ((float)rng.NextDouble() - 0.5f) * 0.03f;
                    double s = rng.NextDouble();
                    if (s < 0.02) v -= 0.12f;
                    else if (s < 0.03) v += 0.06f;
                }
                px[y * S + x] = new Color(v, v * 0.99f, v * 0.97f);
            }
        return px;
    });

    /// Trapez sac cephe kaplaması: dikey nervürler (25 cm). Gri; renk malzemeden gelir. 128 px = 1 m.
    static Texture2D MetalPanelTexture() => LoadOrCreateTexture("T_SacKaplama", 128, () =>
    {
        const int S = 128, Rib = 32;
        var rng = new System.Random(5);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int r = x % Rib;
                float v = r < 8 ? 1f : r < 10 ? 0.7f : r > Rib - 3 ? 0.9f : 0.84f;
                v += ((float)rng.NextDouble() - 0.5f) * 0.03f;
                px[y * S + x] = new Color(v, v, v);
            }
        return px;
    });

    /// Tel örgü: 10 cm göz (uvMeters = 0.1 ile), arası saydam.
    static Texture2D WireMeshTexture() => LoadOrCreateTexture("T_TelOrgu", 64, () =>
    {
        const int S = 64;
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                bool wire = x < 4 || y < 4;
                px[y * S + x] = wire ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0f);
            }
        return px;
    }, alpha: true);
}
#endif

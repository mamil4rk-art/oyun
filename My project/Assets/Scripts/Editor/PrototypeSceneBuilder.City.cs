#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Dükkânın çevresindeki şehir: asfalt yollar, binalar, taş yaya yolları.
///
/// ÜSTTEN BAKIŞ (Unity sahne görünümündeki gibi: +x aşağı, +z sağa):
///   - Çevre yolu: köşeleri geniş yarıçaplı (keskin virajsız) dikdörtgen
///   - Orta cadde (z = 12.1): dükkânın önünden geçer, çevre yolunu ikiye böler
///   - Bağlantı yolu: orta caddeden çıkıp yumuşak bir S virajıyla doğu çevre yoluna bağlanır
///   - Binalar arasında taş yaya yolları: 15 cm bordürle yükseltilmiş, yola açılan
///     uçlarında babalar var (yaya geçer, araba sığmaz)
///
/// Dokular Assets/Data/Textures içinde kodla üretilir. Gerçek bir doku koymak istersen
/// aynı isimli PNG'nin üzerine yaz; dosya varsa yeniden üretilmez.
/// </summary>
public static partial class PrototypeSceneBuilder
{
    const string TexturesFolder = "Assets/Data/Textures";

    const float RoadW = 7f;       // yol genişliği (iki şerit)
    const float PathW = 3f;       // yaya yolu genişliği
    const float PathTop = 0.15f;  // yaya yolu bordür yüksekliği
    const float SidewalkW = 2.5f; // yol kenarı kaldırımı (dükkân önündekiyle aynı genişlik)
    const float SidewalkTop = 0.16f; // yaya yollarından 1 cm yüksek: üst üste bindikleri yerde titreme olmasın
    const float FilletR = 6f;     // kavşak köşe yarıçapı
    const float CityFloorH = 3.2f;

    // Çevre yolu merkez çizgisi ve köşe yarıçapı
    const float RingMinX = -82f, RingMaxX = 64f, RingMinZ = -54f, RingMaxZ = 78f, RingR = 18f;
    const float StreetZ = 12.1f;                 // orta cadde (dükkânın önü)
    const float LinkX0 = -18f, LinkX1 = -46f;    // bağlantı yolunun iki düz kısmı

    struct CityBlock
    {
        public string name;
        public float x0, x1, z0, z1;
        public int floors, style;
        public Vector3 door; // kapının baktığı yön (yaya yoluna doğru)
    }

    static CityBlock CB(string name, float x0, float x1, float z0, float z1, int floors, int style, Vector3 door)
        => new CityBlock { name = name, x0 = x0, x1 = x1, z0 = z0, z1 = z1, floors = floors, style = style, door = door };

    static void BuildCity(Transform parent)
    {
        var city = new GameObject("_Şehir").transform;
        city.SetParent(parent, false);

        var asphalt = TexMat("YolAsfalt", AsphaltTexture(), Color.white, 0.15f);
        var stone = TexMat("KaldirimTasi", PavingTexture(), Color.white, 0.1f);

        var sidewalk = TexMat("YolKaldirimi", PavingTexture(), new Color(0.92f, 0.92f, 0.94f), 0.1f);
        BuildRoads(city, asphalt, Mat("YolCizgisi", Color.white), sidewalk);
        BuildFootpaths(city, stone);
        BuildBuildings(city);
    }

    // =====================================================================
    // YOLLAR
    // =====================================================================

    /// Kavşakta iki yol kenarının dik birleştiği köşe. d1: 1. yolun kenarı boyunca, d2: 2. yolun kenarı boyunca (köşeden dışa).
    struct RoadCorner { public Vector3 c, d1, d2; }

    static RoadCorner RC(float x, float z, Vector3 d1, Vector3 d2)
        => new RoadCorner { c = new Vector3(x, 0f, z), d1 = d1, d2 = d2 };

    // Köşe yayının 1. ve 2. yol tarafındaki teğet noktaları (yol kenarından 'off' kadar dışarıda)
    static Vector3 CornerT1(RoadCorner k, float off) => k.c + k.d1 * FilletR + k.d2 * off;
    static Vector3 CornerT2(RoadCorner k, float off) => k.c + k.d2 * FilletR + k.d1 * off;

    /// Köşe yayı boyunca noktalar ekler (from1: T1 → T2, değilse T2 → T1).
    static void AddCornerArc(List<Vector3> pts, RoadCorner k, float off, bool from1)
    {
        Vector3 o = k.c + (k.d1 + k.d2) * FilletR;
        float r = FilletR - off;
        const int segs = 10;
        for (int i = 0; i <= segs; i++)
        {
            float a = (from1 ? i : segs - i) * 90f / segs * Mathf.Deg2Rad;
            Append(pts, o - k.d2 * (r * Mathf.Cos(a)) - k.d1 * (r * Mathf.Sin(a)));
        }
    }

    static void BuildRoads(Transform city, Material asphalt, Material lineMat, Material sidewalk)
    {
        var root = new GameObject("Yollar").transform;
        root.SetParent(city, false);

        var ring = Resample(RoundedRect(RingMinX, RingMaxX, RingMinZ, RingMaxZ, RingR), true, 1f);
        var street = Resample(new List<Vector3> { new Vector3(RingMinX, 0f, StreetZ), new Vector3(RingMaxX, 0f, StreetZ) }, false, 1f);

        var link = LinkCenterline();

        // Kavşaklarda üst üste binen yollar z-fighting yapmasın diye hafif farklı yükseklikler
        BuildStrip(root, "Çevre Yolu", ring, true, RoadW, 0.02f, -0.05f, asphalt, 4f, true);
        BuildStrip(root, "Orta Cadde", street, false, RoadW, 0.026f, -0.05f, asphalt, 4f, true);
        BuildStrip(root, "Bağlantı Yolu", link, false, RoadW, 0.032f, -0.05f, asphalt, 4f, true);

        // T kavşaklarının köşeleri (1. yol = kenarı d1 boyunca uzanan yol)
        float h = RoadW / 2f;
        var nW = RC(RingMinX + h, StreetZ - h, Vector3.back, Vector3.right);      // kuzey kavşak: çevre yolu + cadde
        var nE = RC(RingMinX + h, StreetZ + h, Vector3.forward, Vector3.right);
        var sW = RC(RingMaxX - h, StreetZ - h, Vector3.back, Vector3.left);       // güney kavşak: çevre yolu + cadde
        var sE = RC(RingMaxX - h, StreetZ + h, Vector3.forward, Vector3.left);
        var l0W = RC(LinkX0 - h, StreetZ + h, Vector3.left, Vector3.forward);     // bağlantı başı: cadde + bağlantı
        var l0E = RC(LinkX0 + h, StreetZ + h, Vector3.right, Vector3.forward);
        var l1W = RC(LinkX1 - h, RingMaxZ - h, Vector3.left, Vector3.back);       // bağlantı sonu: çevre yolu + bağlantı
        var l1E = RC(LinkX1 + h, RingMaxZ - h, Vector3.right, Vector3.back);

        // Köşeleri yuvarlat (dik köşe kalmasın)
        var md = new MeshData();
        foreach (var k in new[] { nW, nE, sW, sE, l0W, l0E, l1W, l1E })
            Fillet(md, k.c, k.d1, k.d2, FilletR, 0.02f);
        MeshObject(root, "Kavşak Köşeleri", md, asphalt, true);

        // Yaya geçitleri: kavşak ağızlarında, köşe yayının hemen bitiminde
        float cw = h + FilletR + 1.5f;
        var crossings = new[]
        {
            (new Vector3(RingMinX, 0f, StreetZ - cw), Vector3.forward, 0.026f), // kuzey: çevre yolu (batı kolu)
            (new Vector3(RingMinX + cw, 0f, StreetZ), Vector3.right, 0.032f),   // kuzey: cadde
            (new Vector3(LinkX0 - cw, 0f, StreetZ), Vector3.right, 0.032f),     // bağlantı başı: cadde (kuzey)
            (new Vector3(LinkX0 + cw, 0f, StreetZ), Vector3.right, 0.032f),     // bağlantı başı: cadde (güney)
            (new Vector3(LinkX0, 0f, StreetZ + cw), Vector3.forward, 0.038f),   // bağlantı yolu başı
            (new Vector3(8.5f, 0f, RingMinZ), Vector3.right, 0.026f),           // batı çevre yolu: yaya yolunun ucu
            (new Vector3(RingMaxX - cw, 0f, StreetZ), Vector3.right, 0.032f),   // güney: cadde
            (new Vector3(RingMaxX, 0f, StreetZ - cw), Vector3.forward, 0.026f), // güney: çevre yolu (batı kolu)
            (new Vector3(RingMaxX, 0f, StreetZ + cw), Vector3.forward, 0.026f), // güney: çevre yolu (doğu kolu)
            (new Vector3(LinkX1 - cw, 0f, RingMaxZ), Vector3.right, 0.026f),    // bağlantı sonu: çevre yolu (kuzey)
            (new Vector3(LinkX1 + cw, 0f, RingMaxZ), Vector3.right, 0.026f),    // bağlantı sonu: çevre yolu (güney)
            (new Vector3(LinkX1, 0f, RingMaxZ - cw), Vector3.forward, 0.038f),  // bağlantı yolu sonu
        };
        var crossPts = new List<Vector3>();
        foreach (var (pos, along, y) in crossings)
        {
            Crosswalk(root, pos, along, y, lineMat);
            crossPts.Add(pos);
        }

        var junctions = new[]
        {
            new Vector3(RingMinX, 0f, StreetZ), new Vector3(RingMaxX, 0f, StreetZ),
            new Vector3(LinkX0, 0f, StreetZ), new Vector3(LinkX1, 0f, RingMaxZ),
        };
        BuildDashes(root, "Şerit Çizgisi - Çevre Yolu", ring, true, 0.025f, junctions, crossPts, lineMat);
        BuildDashes(root, "Şerit Çizgisi - Orta Cadde", street, false, 0.031f, junctions, crossPts, lineMat);
        BuildDashes(root, "Şerit Çizgisi - Bağlantı Yolu", link, false, 0.037f, junctions, crossPts, lineMat);

        // ---- Kaldırımlar: her yolun iki yanında ----
        // Dış taraf: çevre yolunun dışı tek parça. İç taraf: yolların böldüğü 3 bloğun
        // her birinin çevresinde kesintisiz bir halka (kavşak köşelerinde yay çizer).
        var walks = new GameObject("Kaldırımlar").transform;
        walks.SetParent(root, false);
        float off = SidewalkW / 2f, side = h + off; // yol merkezinden kaldırım merkezine
        var ringIn = Offset(ring, true, -side);   // çevre yolunun sağı dışarı bakar
        var linkW = Offset(link, false, -side);   // bağlantı yolunun solu (-x) batı
        var linkE = Offset(link, false, side);
        var linkERev = new List<Vector3>(linkE);
        linkERev.Reverse();

        BuildStrip(walks, "Kaldırım - Çevre Yolu Dışı", Offset(ring, true, side), true, SidewalkW, SidewalkTop, -0.05f, sidewalk, 2f, true);

        // Batı blok (dükkânın olduğu taraf)
        var west = new List<Vector3>();
        Append(west, Span(ringIn, true, CornerT1(nW, off), CornerT1(sW, off)));
        AddCornerArc(west, sW, off, true);
        Append(west, CornerT2(nW, off));
        AddCornerArc(west, nW, off, false);
        BuildLoop(walks, "Kaldırım - Batı Blok", west, sidewalk);

        // Kuzeydoğu blok (cadde, bağlantı yolu ve çevre yolu arası)
        var ne = new List<Vector3>();
        Append(ne, Span(ringIn, true, CornerT1(l1W, off), CornerT1(nE, off)));
        AddCornerArc(ne, nE, off, true);
        Append(ne, CornerT1(l0W, off));
        AddCornerArc(ne, l0W, off, true);
        Append(ne, Span(linkW, false, CornerT2(l0W, off), CornerT2(l1W, off)));
        AddCornerArc(ne, l1W, off, false);
        BuildLoop(walks, "Kaldırım - Kuzeydoğu Blok", ne, sidewalk);

        // Güneydoğu blok
        var se = new List<Vector3>();
        Append(se, CornerT1(l0E, off));
        Append(se, CornerT2(sE, off));
        AddCornerArc(se, sE, off, false);
        Append(se, Span(ringIn, true, CornerT1(sE, off), CornerT1(l1E, off)));
        AddCornerArc(se, l1E, off, true);
        Append(se, Span(linkERev, false, CornerT2(l1E, off), CornerT2(l0E, off)));
        AddCornerArc(se, l0E, off, false);
        BuildLoop(walks, "Kaldırım - Güneydoğu Blok", se, sidewalk);
    }

    /// Bağlantı yolunun merkez çizgisi: düz → S virajı → düz.
    static List<Vector3> LinkCenterline()
    {
        var ctrl = new List<Vector3> { new Vector3(LinkX0, 0f, StreetZ) };
        Vector3 b0 = new Vector3(LinkX0, 0f, 22f), b1 = new Vector3(LinkX0, 0f, 40f);
        Vector3 b2 = new Vector3(LinkX1, 0f, 40f), b3 = new Vector3(LinkX1, 0f, 58f);
        for (int i = 0; i <= 24; i++) ctrl.Add(Bezier(b0, b1, b2, b3, i / 24f));
        ctrl.Add(new Vector3(LinkX1, 0f, RingMaxZ));
        return Resample(ctrl, false, 1f);
    }

    /// Çizginin verilen z değerini kestiği yerdeki x (bağlantı yolu z boyunca hep ilerlediği için tek kesişim var).
    static float XAtZ(List<Vector3> line, float z)
    {
        for (int i = 0; i < line.Count - 1; i++)
        {
            Vector3 a = line[i], b = line[i + 1];
            if ((a.z - z) * (b.z - z) <= 0f && !Mathf.Approximately(a.z, b.z))
                return Mathf.Lerp(a.x, b.x, (z - a.z) / (b.z - a.z));
        }
        return line[line.Count - 1].x;
    }

    static void BuildLoop(Transform parent, string name, List<Vector3> pts, Material mat)
    {
        if (pts.Count > 2 && (pts[pts.Count - 1] - pts[0]).sqrMagnitude < 0.01f) pts.RemoveAt(pts.Count - 1);
        BuildStrip(parent, name, pts, true, SidewalkW, SidewalkTop, -0.05f, mat, 2f, true);
    }

    /// Zebra yaya geçidi: yol yönünde uzanan 7 beyaz şerit (3 m uzunluk, 1 m aralık).
    static void Crosswalk(Transform parent, Vector3 center, Vector3 along, float y, Material mat)
    {
        var md = new MeshData();
        Vector3 t = along.normalized * 1.5f, r = Vector3.Cross(Vector3.up, along).normalized;
        Vector3 s = r * 0.25f;
        for (int k = -3; k <= 3; k++)
        {
            Vector3 c = center + r * k;
            c.y = y;
            md.Quad(c - t - s, c + t - s, c + t + s, c - t + s, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        }
        MeshObject(parent, "Yaya Geçidi", md, mat, false);
    }

    /// Çizgiyi sağına (+) ya da soluna (-) paralel kaydırır.
    static List<Vector3> Offset(List<Vector3> pts, bool closed, float offset)
    {
        int n = pts.Count;
        var res = new List<Vector3>(n);
        for (int i = 0; i < n; i++)
        {
            Vector3 prev = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
            Vector3 next = closed ? pts[(i + 1) % n] : pts[Mathf.Min(i + 1, n - 1)];
            res.Add(pts[i] + Vector3.Cross(Vector3.up, next - prev).normalized * offset);
        }
        return res;
    }

    /// Çizginin a noktasından b noktasına kadar olan kısmı (çizgi yönünde; kapalıysa başa sararak).
    static List<Vector3> Span(List<Vector3> line, bool closed, Vector3 a, Vector3 b)
    {
        int n = line.Count;
        int Next(int i) => closed ? (i + 1) % n : Mathf.Min(i + 1, n - 1);
        int ia = Nearest(line, a), ib = Nearest(line, b);

        var res = new List<Vector3> { a };
        int cur = ia;
        if (Vector3.Dot(line[cur] - a, line[Next(cur)] - line[cur]) <= 0f) cur = Next(cur); // a'nın gerisinde kalan noktayı atla
        int guard = 0;
        while (cur != ib && guard++ < n)
        {
            Append(res, line[cur]);
            cur = Next(cur);
        }
        int prevB = closed ? (ib - 1 + n) % n : Mathf.Max(ib - 1, 0);
        if (Vector3.Dot(b - line[ib], line[ib] - line[prevB]) > 0f) Append(res, line[ib]); // b'nin önündeyse ekleme
        Append(res, b);
        return res;
    }

    static int Nearest(List<Vector3> line, Vector3 p)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < line.Count; i++)
        {
            float d = (line[i] - p).sqrMagnitude;
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    /// Bir önceki noktaya çok yakın olmayan noktaları ekler.
    static void Append(List<Vector3> dst, Vector3 p)
    {
        if (dst.Count == 0 || (p - dst[dst.Count - 1]).sqrMagnitude > 0.04f) dst.Add(p);
    }

    static void Append(List<Vector3> dst, List<Vector3> src)
    {
        foreach (var p in src) Append(dst, p);
    }

    /// Kesikli orta şerit çizgisi (3 m çizgi, 3 m boşluk). Kavşak ve yaya geçitlerinde çizilmez.
    static void BuildDashes(Transform parent, string name, List<Vector3> pts, bool closed, float y, Vector3[] junctions, List<Vector3> crossings, Material mat)
    {
        var md = new MeshData();
        int segs = closed ? pts.Count : pts.Count - 1;
        float dist = 0f;
        for (int i = 0; i < segs; i++)
        {
            Vector3 a = pts[i], b = pts[(i + 1) % pts.Count];
            float len = Vector3.Distance(a, b);
            bool on = (dist + len / 2f) % 6f < 3f;
            dist += len;
            if (!on) continue;

            Vector3 mid = (a + b) / 2f;
            bool skip = false;
            foreach (var j in junctions)
                if ((mid - j).sqrMagnitude < 14f * 14f) { skip = true; break; }
            foreach (var c in crossings)
                if ((mid - c).sqrMagnitude < 3f * 3f) { skip = true; break; }
            if (skip) continue;

            Vector3 side = Vector3.Cross(Vector3.up, b - a).normalized * 0.075f;
            a.y = b.y = y;
            md.Quad(a - side, b - side, b + side, a + side, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        }
        MeshObject(parent, name, md, mat, false);
    }

    /// İki yol kenarının dik birleştiği köşeyi r yarıçaplı bir yayla doldurur.
    /// d1, d2: köşeden yol kenarları boyunca dışa doğru yönler.
    static void Fillet(MeshData md, Vector3 corner, Vector3 d1, Vector3 d2, float r, float y)
    {
        Vector3 c = new Vector3(corner.x, y, corner.z);
        Vector3 o = c + (d1 + d2) * r;
        const int segs = 10;
        Vector3 prev = o - d2 * r;
        for (int i = 1; i <= segs; i++)
        {
            float a = i * 90f / segs * Mathf.Deg2Rad;
            Vector3 p = o - d2 * (r * Mathf.Cos(a)) - d1 * (r * Mathf.Sin(a));
            md.TriUp(c, prev, p, 4f);
            prev = p;
        }
    }

    // =====================================================================
    // TAŞ YAYA YOLLARI
    // =====================================================================

    static void BuildFootpaths(Transform city, Material stone)
    {
        var root = new GameObject("Yaya Yolları (taş)").transform;
        root.SetParent(city, false);

        // Yol merkezinden, yol kaldırımının içine 0,5 m girecek noktaya (boşluk kalmasın)
        float e = RoadW / 2f + SidewalkW - 0.5f;
        float p = PathW / 2f; // yaya yolu merkezinden kenarına

        // Virajlı bağlantı yoluna bağlanan yaya yolları, yol kaldırımının tam ortasında biter
        // (çapraz kesiştiği için ucu kaldırımın içinde kalır, boşluk olmaz)
        var link = LinkCenterline();
        float side = RoadW / 2f + SidewalkW / 2f;
        var linkWalkW = Offset(link, false, -side);
        var linkWalkE = Offset(link, false, side);
        const float inset = 2.2f; // babalar kaldırımın dışında, yaya yolunun üstünde dursun

        // Kuzeydoğu: A ile B binaları arası (çevre yolu kaldırımından virajın kaldırımına)
        Footpath(root, stone, new Vector3(RingMinX + e, 0f, 47f), new Vector3(XAtZ(linkWalkW, 47f), 0f, 47f), true, true, 0.4f, inset);

        // Doğu blokları: ana yaya yolu + binalar arasına uzanan kollar
        Footpath(root, stone, new Vector3(XAtZ(linkWalkE, 44f), 0f, 44f), new Vector3(RingMaxX - e, 0f, 44f), true, true, inset, 0.4f);
        Footpath(root, stone, new Vector3(11f, 0f, StreetZ + e), new Vector3(11f, 0f, 44f - p), true, false);
        Footpath(root, stone, new Vector3(-1f, 0f, 44f + p), new Vector3(-1f, 0f, RingMaxZ - e), false, true);
        Footpath(root, stone, new Vector3(27.5f, 0f, 44f + p), new Vector3(27.5f, 0f, RingMaxZ - e), false, true);

        // Batı: dükkânın arka otoparkından binaların arasına
        Footpath(root, stone, new Vector3(8.5f, 0f, RingMinZ + e), new Vector3(8.5f, 0f, -18f), true, true);
        Footpath(root, stone, new Vector3(8.5f + p, 0f, -24.5f), new Vector3(RingMaxX - e, 0f, -24.5f), false, true);
    }

    static void Footpath(Transform parent, Material stone, Vector3 a, Vector3 b, bool bollardsA, bool bollardsB, float insetA = 0.4f, float insetB = 0.4f)
    {
        BuildStrip(parent, "Yaya Yolu", new List<Vector3> { a, b }, false, PathW, PathTop, -0.05f, stone, 2f, true);
        if (bollardsA) Bollards(parent, a, (b - a).normalized, insetA);
        if (bollardsB) Bollards(parent, b, (a - b).normalized, insetB);
    }

    /// Yola açılan uçta iki baba: aralarından insan geçer, araba geçemez.
    static void Bollards(Transform parent, Vector3 end, Vector3 inward, float inset)
    {
        Vector3 side = Vector3.Cross(Vector3.up, inward).normalized;
        var mat = Mat("Baba", new Color(0.22f, 0.22f, 0.24f));
        foreach (float s in new[] { -0.7f, 0.7f })
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Baba (araba engeli)";
            go.transform.SetParent(parent, false);
            Vector3 pos = end + inward * inset + side * s;
            go.transform.position = new Vector3(pos.x, PathTop + 0.45f, pos.z);
            go.transform.localScale = new Vector3(0.22f, 0.45f, 0.22f);
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    // =====================================================================
    // BİNALAR
    // =====================================================================

    static void BuildBuildings(Transform city)
    {
        var root = new GameObject("Binalar").transform;
        root.SetParent(city, false);

        var facadeTex = FacadeTexture();
        Color[] tints =
        {
            new Color(1f, 0.93f, 0.8f),    // krem
            new Color(0.8f, 0.5f, 0.4f),   // tuğla
            new Color(0.75f, 0.8f, 0.88f), // gri-mavi
            new Color(0.95f, 0.85f, 0.65f),// kum
            new Color(0.85f, 0.85f, 0.85f),// açık gri
        };

        var blocks = new[]
        {
            // Doğu tarafı (orta caddenin karşısı)
            CB("Bina A", -71f, -32f, 19.5f, 30f, 9, 0, Vector3.left),
            CB("Bina B", -70f, -54f, 52f, 69f, 6, 1, Vector3.left),
            CB("Bina C", -36f, -4f, 54f, 71f, 12, 2, Vector3.right),
            CB("Bina D", -10f, 8f, 20f, 36f, 5, 3, Vector3.right),
            CB("Bina E", 2f, 25f, 50f, 70f, 8, 4, Vector3.left),
            CB("Bina F", 14f, 50f, 20f, 38f, 10, 1, Vector3.forward),
            CB("Bina G", 30f, 53f, 50f, 68f, 7, 0, Vector3.left),
            // Batı tarafı (dükkânın arkası)
            CB("Bina H", -20f, 4f, -46f, -32f, 6, 2, Vector3.right),
            CB("Bina I", 13f, 50f, -46f, -28f, 11, 3, Vector3.left),
            CB("Bina J", 18f, 54f, -21f, -2f, 8, 4, Vector3.back),
        };

        var rng = new System.Random(1234);
        foreach (var b in blocks)
        {
            Color tint = tints[b.style];
            var facade = TexMat("Cephe_" + b.style, facadeTex, tint, 0.2f);
            var trim = Mat("CepheDuz_" + b.style, tint * 0.8f);
            CityBuilding(root, b, facade, trim, rng);
        }
    }

    static void CityBuilding(Transform parent, CityBlock b, Material facade, Material trim, System.Random rng)
    {
        float sx = b.x1 - b.x0, sz = b.z1 - b.z0, h = b.floors * CityFloorH;
        var root = new GameObject(b.name).transform;
        root.SetParent(parent, false);
        root.position = new Vector3((b.x0 + b.x1) / 2f, 0f, (b.z0 + b.z1) / 2f);

        var col = root.gameObject.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, h / 2f, 0f);
        col.size = new Vector3(sx, h, sz);

        // Cephe: doku her kata ve yaklaşık her 3 m'ye bir pencere düşürür
        float x0 = -sx / 2f, x1 = sx / 2f, z0 = -sz / 2f, z1 = sz / 2f;
        float ux = Mathf.Max(1f, Mathf.Round(sx / 3f)), uz = Mathf.Max(1f, Mathf.Round(sz / 3f));
        var md = new MeshData();
        md.Wall(new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1), h, ux, b.floors); // +z
        md.Wall(new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), h, ux, b.floors); // -z
        md.Wall(new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), h, uz, b.floors); // +x
        md.Wall(new Vector3(x0, 0f, z1), new Vector3(x0, 0f, z0), h, uz, b.floors); // -x
        MeshObject(root, "Cephe", md, facade, false);

        // Kaide, çatı, korkuluk
        Decor(root, "Kaide", new Vector3(0f, 0.3f, 0f), new Vector3(sx + 0.12f, 0.6f, sz + 0.12f), Mat("Kaide", new Color(0.3f, 0.3f, 0.32f)));
        Decor(root, "Çatı", new Vector3(0f, h + 0.1f, 0f), new Vector3(sx + 0.2f, 0.2f, sz + 0.2f), Mat("Cati", new Color(0.35f, 0.35f, 0.37f)));
        float py = h + 0.6f;
        Decor(root, "Korkuluk", new Vector3(0f, py, z1 - 0.025f), new Vector3(sx + 0.2f, 0.8f, 0.25f), trim);
        Decor(root, "Korkuluk", new Vector3(0f, py, z0 + 0.025f), new Vector3(sx + 0.2f, 0.8f, 0.25f), trim);
        Decor(root, "Korkuluk", new Vector3(x1 - 0.025f, py, 0f), new Vector3(0.25f, 0.8f, sz + 0.2f), trim);
        Decor(root, "Korkuluk", new Vector3(x0 + 0.025f, py, 0f), new Vector3(0.25f, 0.8f, sz + 0.2f), trim);

        // Çatıdaki makine dairesi
        float mw = 3f + (float)rng.NextDouble() * 2f, mdp = 3f + (float)rng.NextDouble() * 2f;
        float mx = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0f, sx - mw - 2f);
        float mz = ((float)rng.NextDouble() - 0.5f) * Mathf.Max(0f, sz - mdp - 2f);
        Decor(root, "Makine Dairesi", new Vector3(mx, h + 0.2f + 1.25f, mz), new Vector3(mw, 2.5f, mdp), Mat("Beton", new Color(0.55f, 0.55f, 0.55f)));

        // Giriş kapısı + tente (yaya yoluna bakan cephede)
        Vector3 d = b.door;
        bool alongX = Mathf.Abs(d.x) > 0.5f;
        Vector3 face = new Vector3(d.x * sx / 2f, 0f, d.z * sz / 2f);
        Decor(root, "Giriş Kapısı", face + d * 0.08f + Vector3.up * 1.3f,
            alongX ? new Vector3(0.2f, 2.6f, 2f) : new Vector3(2f, 2.6f, 0.2f), Mat("Kapi", new Color(0.25f, 0.18f, 0.12f)));
        Decor(root, "Tente", face + d * 0.7f + Vector3.up * 3.0f,
            alongX ? new Vector3(1.4f, 0.15f, 3f) : new Vector3(3f, 0.15f, 1.4f), Mat("Tente", new Color(0.6f, 0.15f, 0.12f)));
    }

    /// Çarpışmasız süs parçası (bina gövdesinin çarpışması kökteki BoxCollider'da).
    static void Decor(Transform root, string name, Vector3 localCenter, Vector3 size, Material mat)
    {
        var go = LocalBlock(root, name, localCenter, size, mat);
        Object.DestroyImmediate(go.GetComponent<Collider>());
    }

    // =====================================================================
    // MESH YARDIMCILARI
    // =====================================================================

    class MeshData
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> tris = new List<int>();

        /// a→b→c→d çevresi; ön yüz cross(b - a, c - a) yönüne bakar.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            uvs.Add(ua); uvs.Add(ub); uvs.Add(uc); uvs.Add(ud);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        /// Yukarı bakan üçgen, dünya koordinatından düzlemsel UV.
        public void TriUp(Vector3 a, Vector3 b, Vector3 c, float uvMeters)
        {
            if (Vector3.Cross(b - a, c - a).y < 0f) { var t = b; b = c; c = t; }
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            uvs.Add(new Vector2(a.x, a.z) / uvMeters);
            uvs.Add(new Vector2(b.x, b.z) / uvMeters);
            uvs.Add(new Vector2(c.x, c.z) / uvMeters);
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }

        /// Dikey duvar: dışarıdan bakan kişinin solundaki alt köşe → sağındaki alt köşe.
        public void Wall(Vector3 bottomLeft, Vector3 bottomRight, float height, float u, float v)
        {
            Vector3 up = Vector3.up * height;
            Quad(bottomLeft, bottomLeft + up, bottomRight + up, bottomRight,
                new Vector2(0f, 0f), new Vector2(0f, v), new Vector2(u, v), new Vector2(u, 0f));
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }
    }

    static GameObject MeshObject(Transform parent, string name, MeshData md, Material mat, bool collider)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var mesh = md.ToMesh(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
        return go;
    }

    /// Bir çizgi boyunca kalınlığı olan şerit (yol / yaya yolu): üst yüz + yan bordürler (+ uç kapakları).
    /// UV metre cinsinden: doku her uvMeters metrede bir tekrarlar.
    static GameObject BuildStrip(Transform parent, string name, List<Vector3> pts, bool closed, float width, float top, float bottom, Material mat, float uvMeters, bool collider)
    {
        int n = pts.Count;
        int count = closed ? n + 1 : n;
        float hw = width / 2f, dist = 0f;
        var L = new List<Vector3>(count);
        var R = new List<Vector3>(count);
        var D = new List<float>(count);

        for (int k = 0; k < count; k++)
        {
            int i = k % n;
            Vector3 prev = closed ? pts[(i - 1 + n) % n] : pts[Mathf.Max(i - 1, 0)];
            Vector3 next = closed ? pts[(i + 1) % n] : pts[Mathf.Min(i + 1, n - 1)];
            Vector3 right = Vector3.Cross(Vector3.up, next - prev).normalized * hw;
            if (k > 0) dist += Vector3.Distance(pts[(k - 1) % n], pts[i]);
            Vector3 p = pts[i];
            L.Add(new Vector3(p.x - right.x, 0f, p.z - right.z));
            R.Add(new Vector3(p.x + right.x, 0f, p.z + right.z));
            D.Add(dist);
        }

        Vector3 T(Vector3 v) => new Vector3(v.x, top, v.z);
        Vector3 B(Vector3 v) => new Vector3(v.x, bottom, v.z);
        float wU = width / uvMeters, tV = top / uvMeters, bV = bottom / uvMeters;

        var md = new MeshData();
        for (int k = 0; k < count - 1; k++)
        {
            float v0 = D[k] / uvMeters, v1 = D[k + 1] / uvMeters;
            md.Quad(T(L[k]), T(L[k + 1]), T(R[k + 1]), T(R[k]),
                new Vector2(0f, v0), new Vector2(0f, v1), new Vector2(wU, v1), new Vector2(wU, v0));
            md.Quad(T(L[k]), B(L[k]), B(L[k + 1]), T(L[k + 1]),
                new Vector2(v0, tV), new Vector2(v0, bV), new Vector2(v1, bV), new Vector2(v1, tV));
            md.Quad(T(R[k]), T(R[k + 1]), B(R[k + 1]), B(R[k]),
                new Vector2(v0, tV), new Vector2(v1, tV), new Vector2(v1, bV), new Vector2(v0, bV));
        }
        if (!closed)
        {
            int e = count - 1;
            md.Quad(T(L[0]), T(R[0]), B(R[0]), B(L[0]),
                new Vector2(0f, tV), new Vector2(wU, tV), new Vector2(wU, bV), new Vector2(0f, bV));
            md.Quad(T(R[e]), T(L[e]), B(L[e]), B(R[e]),
                new Vector2(0f, tV), new Vector2(wU, tV), new Vector2(wU, bV), new Vector2(0f, bV));
        }
        return MeshObject(parent, name, md, mat, collider);
    }

    /// Köşeleri r yarıçaplı yay olan kapalı dikdörtgen (merkez çizgisi).
    static List<Vector3> RoundedRect(float x0, float x1, float z0, float z1, float r)
    {
        var pts = new List<Vector3>();
        var centers = new[] { new Vector2(x1 - r, z1 - r), new Vector2(x0 + r, z1 - r), new Vector2(x0 + r, z0 + r), new Vector2(x1 - r, z0 + r) };
        const int segs = 16;
        for (int c = 0; c < 4; c++)
            for (int i = 0; i <= segs; i++)
            {
                float a = (c * 90f + i * 90f / segs) * Mathf.Deg2Rad;
                pts.Add(new Vector3(centers[c].x + Mathf.Cos(a) * r, 0f, centers[c].y + Mathf.Sin(a) * r));
            }
        return pts;
    }

    static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
    {
        float u = 1f - t;
        return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
    }

    /// Çizgiyi eşit aralıklı noktalara böler (düzgün mesh ve şerit çizgileri için).
    static List<Vector3> Resample(List<Vector3> pts, bool closed, float step)
    {
        int segCount = closed ? pts.Count : pts.Count - 1;
        var seg = new List<float>(segCount);
        float total = 0f;
        for (int i = 0; i < segCount; i++)
        {
            float len = Vector3.Distance(pts[i], pts[(i + 1) % pts.Count]);
            seg.Add(len);
            total += len;
        }

        int n = Mathf.Max(1, Mathf.RoundToInt(total / step));
        float dStep = total / n;
        var res = new List<Vector3>();
        int si = 0;
        float segStart = 0f;
        int last = closed ? n - 1 : n;
        for (int k = 0; k <= last; k++)
        {
            float d = Mathf.Min(k * dStep, total);
            while (si < segCount - 1 && segStart + seg[si] < d) { segStart += seg[si]; si++; }
            float t = seg[si] > 0f ? (d - segStart) / seg[si] : 0f;
            res.Add(Vector3.Lerp(pts[si], pts[(si + 1) % pts.Count], Mathf.Clamp01(t)));
        }
        return res;
    }

    // =====================================================================
    // DOKULAR (kodla üretilir, PNG olarak kaydedilir)
    // =====================================================================

    static Material TexMat(string key, Texture2D tex, Color tint, float smoothness)
    {
        var m = Mat(key, tint);
        m.mainTexture = tex;
        m.color = tint;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Texture2D LoadOrCreateTexture(string name, int size, System.Func<Color[]> generate, bool alpha = false)
    {
        EnsureFolder(TexturesFolder);
        string path = $"{TexturesFolder}/{name}.png";
        var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;

        return SavePng(path, size, generate(), alpha, false);
    }

    /// Pikselleri PNG olarak kaydeder ve tekrarlanan (repeat) doku olarak içe aktarır.
    static Texture2D SavePng(string path, int size, Color[] pixels, bool alpha, bool normalMap)
    {
        var tex = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
        tex.SetPixels(pixels);
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        if (AssetImporter.GetAtPath(path) is TextureImporter imp)
        {
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.anisoLevel = 8;
            if (normalMap) imp.textureType = TextureImporterType.NormalMap;
            if (alpha)
            {
                imp.alphaIsTransparency = true;
                imp.mipMapsPreserveCoverage = true; // uzaktan bakınca tel kaybolmasın
                imp.alphaTestReferenceValue = 0.5f;
            }
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// Kenarları birleşen (döşenebilir) yumuşak gürültü ızgarası.
    static float[,] NoiseGrid(System.Random rng, int cells)
    {
        var g = new float[cells, cells];
        for (int y = 0; y < cells; y++)
            for (int x = 0; x < cells; x++)
                g[x, y] = (float)rng.NextDouble();
        return g;
    }

    static float SampleNoise(float[,] g, int cells, float u, float v)
    {
        float x = u * cells, y = v * cells;
        int x0 = (int)x % cells, y0 = (int)y % cells;
        int xa = (x0 + 1) % cells, ya = (y0 + 1) % cells;
        float fx = x - Mathf.Floor(x), fy = y - Mathf.Floor(y);
        fx = fx * fx * (3f - 2f * fx);
        fy = fy * fy * (3f - 2f * fy);
        return Mathf.Lerp(Mathf.Lerp(g[x0, y0], g[xa, y0], fx), Mathf.Lerp(g[x0, ya], g[xa, ya], fx), fy);
    }

    /// Asfalt: koyu gri, ince taneli, hafif lekeli. 256 px = 4 m.
    static Texture2D AsphaltTexture() => LoadOrCreateTexture("T_Asfalt", 256, () =>
    {
        const int S = 256;
        var rng = new System.Random(11);
        var blot = NoiseGrid(rng, 8);
        var px = new Color[S * S];
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v = 0.2f + ((float)rng.NextDouble() - 0.5f) * 0.07f;
                v += (SampleNoise(blot, 8, x / (float)S, y / (float)S) - 0.5f) * 0.06f;
                double s = rng.NextDouble();
                if (s < 0.04) v += 0.12f;        // açık renk agrega
                else if (s < 0.06) v -= 0.06f;   // koyu tane
                px[y * S + x] = new Color(v, v, v * 1.02f);
            }
        return px;
    });

    /// Kaldırım taşı: şaşırtmalı dizilmiş dikdörtgen taşlar, koyu derz. 512 px = 2 m.
    static Texture2D PavingTexture() => LoadOrCreateTexture("T_KaldirimTasi", 512, () =>
    {
        const int S = 512, RowH = 64, Rows = S / RowH;
        var rng = new System.Random(7);
        var grain = NoiseGrid(rng, 32);
        var px = new Color[S * S];
        Color stoneA = new Color(0.58f, 0.57f, 0.55f), stoneB = new Color(0.72f, 0.69f, 0.64f);
        Color grout = new Color(0.26f, 0.26f, 0.25f);

        for (int r = 0; r < Rows; r++)
        {
            // Satırdaki taş uzunlukları (toplam tam 512 olsun ki doku döşenebilsin)
            var lens = new List<int>();
            int sum = 0;
            while (sum < S)
            {
                int len = rng.Next(90, 151);
                if (S - sum - len < 60) len = S - sum;
                lens.Add(len);
                sum += len;
            }
            int offset = rng.Next(0, S);
            var shades = new List<Color>();
            foreach (var _ in lens)
            {
                Color c = Color.Lerp(stoneA, stoneB, (float)rng.NextDouble());
                if (rng.NextDouble() < 0.15) c *= 0.85f;
                shades.Add(c);
            }

            for (int y = r * RowH; y < (r + 1) * RowH; y++)
            {
                int dy = Mathf.Min(y - r * RowH, (r + 1) * RowH - 1 - y);
                for (int x = 0; x < S; x++)
                {
                    int rel = (x - offset + S) % S, start = 0, idx = 0;
                    while (rel >= start + lens[idx]) { start += lens[idx]; idx++; }
                    int dx = Mathf.Min(rel - start, start + lens[idx] - 1 - rel);
                    int edge = Mathf.Min(dx, dy);

                    Color c;
                    if (edge < 3) c = grout;
                    else
                    {
                        c = shades[idx];
                        if (edge < 9) c *= Mathf.Lerp(0.8f, 1f, (edge - 3) / 6f); // kenar pahı
                        float n = (SampleNoise(grain, 32, x / (float)S, y / (float)S) - 0.5f) * 0.1f
                                  + ((float)rng.NextDouble() - 0.5f) * 0.06f;
                        c = new Color(c.r + n, c.g + n, c.b + n);
                    }
                    px[y * S + x] = c;
                }
            }
        }
        return px;
    });

    /// Bina cephesi: bir kat (3.2 m) × bir pencere aralığı (3 m). Beyaza yakın;
    /// renk malzemenin tonuyla verilir. 128 px.
    static Texture2D FacadeTexture() => LoadOrCreateTexture("T_Cephe", 128, () =>
    {
        const int S = 128;
        var rng = new System.Random(3);
        var px = new Color[S * S];
        Color glassLow = new Color(0.16f, 0.22f, 0.3f), glassHigh = new Color(0.42f, 0.52f, 0.62f);
        const int wx0 = 20, wx1 = 108, wy0 = 34, wy1 = 112, frame = 4;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float w = 0.93f + ((float)rng.NextDouble() - 0.5f) * 0.04f;
                Color c = new Color(w, w, w);

                if (y < 3) c = new Color(0.78f, 0.78f, 0.78f);                              // kat çizgisi
                else if (y >= wy0 - 4 && y < wy0 && x >= wx0 - 4 && x < wx1 + 4) c = new Color(0.98f, 0.98f, 0.98f); // denizlik
                else if (x >= wx0 && x < wx1 && y >= wy0 && y < wy1)
                {
                    bool isFrame = x < wx0 + frame || x >= wx1 - frame || y < wy0 + frame || y >= wy1 - frame
                                   || (x >= 62 && x < 66) || (y >= 84 && y < 87);
                    if (isFrame) c = new Color(0.42f, 0.42f, 0.44f);
                    else
                    {
                        c = Color.Lerp(glassLow, glassHigh, (y - wy0) / (float)(wy1 - wy0));
                        int band = (x + y) % 64;
                        if (band > 10 && band < 22) c += new Color(0.07f, 0.07f, 0.07f); // yansıma
                    }
                }
                px[y * S + x] = c;
            }
        return px;
    });
}
#endif

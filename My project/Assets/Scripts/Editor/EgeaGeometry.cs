using System;
using System.Collections.Generic;
using UnityEngine;

// =====================================================================
// Fiat Egea (sedan) görünümlü arabanın kodla üretilen geometrisi.
// Saf hesap: Unity'nin yerel (native) işlevlerini kullanmaz; sahne kurucu bu parçaları Mesh'e çevirir,
// Unity açmadan önizleme (yazılım çizici) de aynı sınıfı kullanır.
// Araç ekseni: +z ön, +x sağ, y yukarı; zemin y = 0. Ölçüler metre (gerçek Egea: 4.53 × 1.79 × 1.50, aks 2.64).
// Marka logosu / yazısı yok: amblem sade krom halka.
// =====================================================================

/// Malzeme anahtarlarına göre üçgen listeleri tutan basit mesh verisi.
public class CarMesh
{
    public readonly string name;
    /// Pivot grubu: "" = gövde, "Bagaj Kapağı", "Tekerlek ÖnSol/Dönen"...
    public readonly string group;
    public readonly List<Vector3> v = new List<Vector3>();
    public readonly List<Vector2> uv = new List<Vector2>();
    public readonly Dictionary<string, List<int>> tris = new Dictionary<string, List<int>>();
    public Vector3[] normals;

    public CarMesh(string name, string group) { this.name = name; this.group = group; }

    public int Add(Vector3 p, Vector2 t) { v.Add(p); uv.Add(t); return v.Count - 1; }
    public int Add(Vector3 p) => Add(p, new Vector2(p.z, p.y));

    List<int> T(string mat)
    {
        if (!tris.TryGetValue(mat, out var l)) tris[mat] = l = new List<int>();
        return l;
    }

    /// Unity'de ön yüz saat yönündedir: normal = (b-a) × (c-a).
    public void Tri(string mat, int a, int b, int c) { var l = T(mat); l.Add(a); l.Add(b); l.Add(c); }

    /// Üçgeni, normali 'dışarı' yönüne bakacak şekilde ekler.
    public void TriOut(string mat, int a, int b, int c, Vector3 outward)
    {
        var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(n, outward) >= 0f) Tri(mat, a, b, c);
        else Tri(mat, a, c, b);
    }

    public void QuadOut(string mat, int a, int b, int c, int d, Vector3 outward)
    {
        TriOut(mat, a, b, c, outward);
        TriOut(mat, a, c, d, outward);
    }

    public int TriCount { get { int n = 0; foreach (var l in tris.Values) n += l.Count / 3; return n; } }

    /// Alan ağırlıklı yumuşak normaller (ortak köşeler yumuşar, ayrı köşeler keskin kalır).
    public void ComputeNormals()
    {
        var n = new Vector3[v.Count];
        foreach (var l in tris.Values)
            for (int i = 0; i < l.Count; i += 3)
            {
                int a = l[i], b = l[i + 1], c = l[i + 2];
                var fn = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                n[a] += fn; n[b] += fn; n[c] += fn;
            }
        for (int i = 0; i < n.Length; i++)
        {
            float m = n[i].magnitude;
            n[i] = m > 1e-12f ? n[i] / m : Vector3.up;
        }
        normals = n;
    }
}

public class CarModel
{
    public readonly List<CarMesh> meshes = new List<CarMesh>();
    /// Grup adı → menteşe / dönme noktası (araç uzayında).
    public readonly Dictionary<string, Vector3> pivots = new Dictionary<string, Vector3>();
    /// Plaka, sürücü gözü, bagaj zemini gibi yerleştirme noktaları.
    public readonly Dictionary<string, (Vector3 pos, Vector3 normal)> anchors = new Dictionary<string, (Vector3, Vector3)>();

    public CarMesh Get(string name, string group = "")
    {
        foreach (var m in meshes) if (m.name == name && m.group == group) return m;
        var nm = new CarMesh(name, group);
        meshes.Add(nm);
        return nm;
    }

    public Vector3 Pivot(string group) => group != null && pivots.TryGetValue(group, out var p) ? p : Vector3.zero;
}

/// Monoton kübik eğri (Fritsch–Carlson): anahtar noktalardan taşmadan yumuşak geçer.
public class ProfileCurve
{
    readonly float[] xs, ys, ms;

    public ProfileCurve(params float[] xy)
    {
        int n = xy.Length / 2;
        var pts = new List<Vector2>();
        for (int i = 0; i < n; i++) pts.Add(new Vector2(xy[2 * i], xy[2 * i + 1]));
        pts.Sort((a, b) => a.x.CompareTo(b.x));
        xs = new float[n]; ys = new float[n]; ms = new float[n];
        for (int i = 0; i < n; i++) { xs[i] = pts[i].x; ys[i] = pts[i].y; }
        var d = new float[n - 1];
        for (int i = 0; i < n - 1; i++) d[i] = (ys[i + 1] - ys[i]) / (xs[i + 1] - xs[i]);
        ms[0] = d[0]; ms[n - 1] = d[n - 2];
        for (int i = 1; i < n - 1; i++) ms[i] = d[i - 1] * d[i] <= 0f ? 0f : (d[i - 1] + d[i]) / 2f;
        for (int i = 0; i < n - 1; i++)
        {
            if (Mathf.Abs(d[i]) < 1e-9f) { ms[i] = 0f; ms[i + 1] = 0f; continue; }
            float a = ms[i] / d[i], b = ms[i + 1] / d[i];
            float s = a * a + b * b;
            if (s > 9f)
            {
                float t = 3f / Mathf.Sqrt(s);
                ms[i] = t * a * d[i];
                ms[i + 1] = t * b * d[i];
            }
        }
    }

    public float this[float x] => Eval(x);

    public float Eval(float x)
    {
        int n = xs.Length;
        if (x <= xs[0]) return ys[0];
        if (x >= xs[n - 1]) return ys[n - 1];
        int lo = 0, hi = n - 1;
        while (hi - lo > 1) { int mid = (lo + hi) / 2; if (xs[mid] <= x) lo = mid; else hi = mid; }
        float h = xs[hi] - xs[lo], t = (x - xs[lo]) / h;
        float t2 = t * t, t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * ys[lo] + (t3 - 2 * t2 + t) * h * ms[lo] + (-2 * t3 + 3 * t2) * ys[hi] + (t3 - t2) * h * ms[hi];
    }
}

public static class EgeaGeometry
{
    // ---------------- Ana ölçüler ----------------
    public const float ZF = 2.266f, ZR = -2.266f;        // ön / arka tampon ucu
    public const float AxleF = 1.346f, AxleR = -1.292f;  // aks aralığı 2.638
    public const float WheelR = 0.316f;                  // 205/55 R16
    public const float TrackHalf = 0.765f;
    public const float ArchR = 0.372f, ArchY = 0.31f;    // çamurluk kemeri
    public const float LidZ0 = -1.64f, LidZ1 = -2.17f;   // bagaj kapağı (menteşe önde)
    public const float CabinF = 0.98f, CabinR = -1.64f;  // gövde üstünün kabin için açık olduğu aralık
    public const float GhF = 1.03f, GhR = -1.69f;        // cam/tavan bölümü
    public const float TrunkFloorY = 0.42f, TrunkFrontZ = -1.30f, TrunkRearZ = -2.15f, TrunkHalfW = 0.80f;

    // ---------------- Yan görünüş / üst görünüş profilleri ----------------
    // Gövdenin üst kenar çizgisi (kaput kenarı → kemer hattı → bagaj kenarı)
    static readonly ProfileCurve cTopEdge = new ProfileCurve(
        -2.266f, 0.745f, -2.248f, 0.87f, -2.22f, 0.965f, -2.17f, 1.03f, -2.05f, 1.045f, -1.8f, 1.035f,
        -1.5f, 1.02f, -0.5f, 0.995f, 0.5f, 0.968f, 1.0f, 0.94f, 1.6f, 0.88f, 2.0f, 0.84f, 2.12f, 0.812f,
        2.19f, 0.765f, 2.235f, 0.685f, 2.266f, 0.58f);

    // Kaput / bagaj kapağının ortadaki kubbesi (kenara göre)
    static readonly ProfileCurve cCrown = new ProfileCurve(
        -2.266f, 0.008f, -2.2f, 0.02f, -2.1f, 0.03f, -1.7f, 0.03f, -1.45f, 0.02f, 0.9f, 0.02f, 1.1f, 0.035f,
        2.1f, 0.03f, 2.2f, 0.02f, 2.266f, 0.008f);

    // Yarım genişlik (üstten bakınca tampon köşeleri yuvarlak)
    static readonly ProfileCurve cHalfW = new ProfileCurve(
        -2.266f, 0.70f, -2.255f, 0.78f, -2.23f, 0.83f, -2.17f, 0.86f, -2.0f, 0.878f, -1.6f, 0.889f, 0f, 0.896f,
        1.6f, 0.891f, 2.0f, 0.878f, 2.16f, 0.848f, 2.22f, 0.80f, 2.25f, 0.735f, 2.266f, 0.64f);

    // Marşpiyel / tampon alt kenarı
    static readonly ProfileCurve cUnder = new ProfileCurve(
        -2.266f, 0.42f, -2.245f, 0.36f, -2.19f, 0.30f, -2.0f, 0.245f, -1.75f, 0.19f, 0f, 0.17f, 1.75f, 0.18f,
        2.05f, 0.20f, 2.19f, 0.235f, 2.245f, 0.29f, 2.266f, 0.36f);

    // Tavan orta çizgisi (ön cam → tavan → arka cam)
    static readonly ProfileCurve cRoof = new ProfileCurve(
        -1.69f, 1.0f, -1.62f, 1.06f, -1.45f, 1.17f, -1.2f, 1.31f, -0.95f, 1.41f, -0.75f, 1.46f, -0.35f, 1.49f,
        0.0f, 1.478f, 0.12f, 1.455f, 0.35f, 1.33f, 0.65f, 1.15f, 0.9f, 1.00f, 1.03f, 0.91f);

    // Tavan kenarı (yan cam üstü) yarım genişliği
    static readonly ProfileCurve cRail = new ProfileCurve(
        -1.69f, 0.74f, -1.3f, 0.68f, -0.8f, 0.655f, -0.2f, 0.645f, 0.3f, 0.655f, 0.8f, 0.70f, 1.03f, 0.76f);

    public static float TopEdge(float z) => cTopEdge[z];
    public static float Crown(float z) => cCrown[z];
    public static float HalfW(float z) => cHalfW[z];
    public static float Under(float z) => cUnder[z];
    public static float Belt(float z) => cTopEdge[z] + 0.006f;

    /// Yan camın üst kenarı (tavan kenarı) yüksekliği.
    static float RailY(float z) => GreenhouseRing(z).p[7].y;

    /// Tekerlek kemerinin alt kenar yüksekliği (kemer dışında marşpiyel).
    static float ArchBottom(float z)
    {
        float y = cUnder[z];
        foreach (float a in new[] { AxleF, AxleR })
        {
            float dz = z - a;
            if (Mathf.Abs(dz) < ArchR) y = Mathf.Max(y, ArchY + Mathf.Sqrt(ArchR * ArchR - dz * dz));
        }
        return y;
    }

    // ---------------- Kesit (halka) üretimi ----------------
    // Kenar etiketleri: halka noktası i → i+1 arasındaki şeridin türü
    const int TSkip = -1, TBottom = 0, TWell = 1, TLip = 2, TSide = 3, TShoulder = 4, TTop = 5;
    const int GSeal = 10, GSide = 11, GPillar = 12, GTop = 13, GBottom = 14;

    class Ring
    {
        public readonly List<Vector2> p = new List<Vector2>();
        public readonly List<int> tag = new List<int>();
        public void Add(Vector2 pt, int nextTag) { p.Add(pt); tag.Add(nextTag); }

        /// Merkezcil Catmull-Rom zinciri (uçlarda yansıtılmış teğet).
        public void Chain(Vector2[] k, int[] subs, int[] tags, int tagAfter)
        {
            int n = k.Length;
            for (int s = 0; s < n - 1; s++)
            {
                Vector2 p0 = s == 0 ? 2f * k[0] - k[1] : k[s - 1];
                Vector2 p3 = s + 2 < n ? k[s + 2] : 2f * k[n - 1] - k[n - 2];
                for (int i = 0; i < subs[s]; i++) Add(CatmullRom(p0, k[s], k[s + 1], p3, i / (float)subs[s]), tags[s]);
            }
            Add(k[n - 1], tagAfter);
        }
    }

    static Vector2 M(Vector2 p) => new Vector2(-p.x, p.y);

    static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t0 = 0f;
        float t1 = t0 + Mathf.Sqrt((p1 - p0).magnitude + 1e-5f);
        float t2 = t1 + Mathf.Sqrt((p2 - p1).magnitude + 1e-5f);
        float t3 = t2 + Mathf.Sqrt((p3 - p2).magnitude + 1e-5f);
        float tt = t1 + (t2 - t1) * t;
        Vector2 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
        Vector2 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
        Vector2 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
        Vector2 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
        Vector2 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
        return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
    }

    /// Alt gövde kesiti: alt taban, tekerlek yuvası, kemer dudağı, yan yüzey,
    /// keskin omuz çizgisi (Egea'nın yan karakter hattı), üst kenar ve kaput/bagaj kubbesi.
    static Ring LowerRing(float z)
    {
        float w = cHalfW[z], yU = cUnder[z], yb = ArchBottom(z);
        float top = cTopEdge[z], crown = cCrown[z];
        float H = top - yU;
        float bx = Mathf.Min(0.60f, w - 0.09f);

        var B0 = new Vector2(0f, yU);
        var B1 = new Vector2(bx, yU);
        var B2 = new Vector2(bx, yb);
        var S0 = new Vector2(w - 0.075f, yb);
        var S3 = new Vector2(w, top - Mathf.Min(0.09f, 0.3f * H));               // omuz çizgisi
        var S2 = new Vector2(w - 0.026f,Mathf.Min(Mathf.Max(yU + 0.42f * H, yb + 0.08f), S3.y - 0.03f));
        var S1 = new Vector2(w - 0.03f, Mathf.Min(yb + 0.035f, S2.y - 0.012f));
        var S4 = new Vector2(w - 0.055f, top);                                   // üst kenar
        var S5 = new Vector2(0.55f * w, top + crown * 0.85f);
        var S6 = new Vector2(0f, top + crown);

        var r = new Ring();
        r.Add(B0, TBottom);
        r.Add(B1, TWell);
        r.Add(B2, TWell);
        r.Add(S0, TSkip);
        r.Chain(new[] { S0, S1, S2, S3 }, new[] { 3, 4, 4 }, new[] { TLip, TSide, TSide }, TSkip);
        r.Chain(new[] { S3, S4, S5, S6, M(S5), M(S4), M(S3) }, new[] { 4, 5, 5, 5, 5, 4 },
            new[] { TShoulder, TTop, TTop, TTop, TTop, TShoulder }, TSkip);
        r.Chain(new[] { M(S3), M(S2), M(S1), M(S0) }, new[] { 4, 4, 3 }, new[] { TSide, TSide, TLip }, TSkip);
        r.Add(M(S0), TWell);
        r.Add(M(B2), TWell);
        r.Add(M(B1), TBottom);
        return r;
    }

    /// Cam/tavan kesiti: conta, yan cam (içe eğik), direk, tavan / ön cam / arka cam.
    static Ring GreenhouseRing(float z)
    {
        float belt = Belt(z), yTop = cRoof[z];
        float h = Mathf.Max(yTop - belt, 0f);
        float bw = cHalfW[z] - 0.085f, rw = cRail[z];

        var G0 = new Vector2(bw - 0.03f, belt - 0.07f);
        var G1 = new Vector2(bw, belt - 0.012f);
        var G2 = new Vector2(rw, belt + h - Mathf.Min(0.07f, 0.6f * h));
        var G3 = new Vector2(rw - 0.045f, yTop - Mathf.Min(0.028f, 0.3f * h));
        var G4 = new Vector2(rw * 0.5f, yTop - 0.004f);
        var G5 = new Vector2(0f, yTop);

        var r = new Ring();
        r.Add(G0, GSeal);
        r.Chain(new[] { G1, G2, G3, G4, G5, M(G4), M(G3), M(G2), M(G1) }, new[] { 6, 3, 5, 5, 5, 5, 3, 6 },
            new[] { GSide, GPillar, GTop, GTop, GTop, GTop, GPillar, GSide }, GSeal);
        r.Add(M(G0), GBottom);
        return r;
    }

    // ---------------- İçerde mi? (ışınla yüzeye yansıtma için) ----------------
    const float CacheStep = 0.002f;
    static List<Vector2>[] lowerCache, ghCache;

    static void EnsureCaches()
    {
        if (lowerCache != null) return;
        int n = Mathf.CeilToInt((ZF - ZR) / CacheStep) + 1;
        lowerCache = new List<Vector2>[n];
        for (int i = 0; i < n; i++) lowerCache[i] = LowerRing(Mathf.Min(ZR + i * CacheStep, ZF)).p;
        int g = Mathf.CeilToInt((GhF - GhR) / CacheStep) + 1;
        ghCache = new List<Vector2>[g];
        for (int i = 0; i < g; i++) ghCache[i] = GreenhouseRing(Mathf.Min(GhR + i * CacheStep, GhF)).p;
    }

    static bool InRings(List<Vector2>[] cache, float z0, float z1, Vector3 q)
    {
        if (q.z < z0 || q.z > z1) return false;
        float f = (q.z - z0) / CacheStep;
        int i = Mathf.Min((int)f, cache.Length - 2);
        float t = f - i;
        var a = cache[i]; var b = cache[i + 1];
        bool inside = false;
        int n = a.Count;
        for (int k = 0, j = n - 1; k < n; j = k++)
        {
            var pk = Vector2.LerpUnclamped(a[k], b[k], t);
            var pj = Vector2.LerpUnclamped(a[j], b[j], t);
            if ((pk.y > q.y) != (pj.y > q.y) && q.x < (pj.x - pk.x) * (q.y - pk.y) / (pj.y - pk.y + 1e-12f) + pk.x)
                inside = !inside;
        }
        return inside;
    }

    static bool InsideBody(Vector3 q) => InRings(lowerCache, ZR, ZF, q) || InRings(ghCache, GhR, GhF, q);

    static bool Cast(Vector3 o, Vector3 d, out Vector3 hit)
    {
        hit = o;
        if (InsideBody(o)) return false;
        const float step = 0.01f;
        float t = 0f;
        for (; t < 3f; t += step)
            if (InsideBody(o + d * (t + step))) break;
        if (t >= 3f) return false;
        float lo = t, hi = t + step;
        for (int i = 0; i < 14; i++)
        {
            float m = (lo + hi) / 2f;
            if (InsideBody(o + d * m)) hi = m; else lo = m;
        }
        hit = o + d * lo;
        return true;
    }

    // ---------------- Yansıtma çerçeveleri ----------------
    /// 2B koordinatı (metre) bir ışına çevirir: başlangıç noktası ve yön.
    public delegate void Frame(Vector2 c, out Vector3 o, out Vector3 d);

    static Frame Front => (Vector2 c, out Vector3 o, out Vector3 d) => { o = new Vector3(c.x, c.y, ZF + 0.5f); d = Vector3.back; };
    static Frame Rear => (Vector2 c, out Vector3 o, out Vector3 d) => { o = new Vector3(c.x, c.y, ZR - 0.5f); d = Vector3.forward; };
    static Frame Top => (Vector2 c, out Vector3 o, out Vector3 d) => { o = new Vector3(c.x, 2.2f, c.y); d = Vector3.down; };

    /// Yandan: c.x = z, c.y = y. side = +1 sağ, -1 sol.
    static Frame Side(int side) => (Vector2 c, out Vector3 o, out Vector3 d) =>
    { o = new Vector3(side * 1.4f, c.y, c.x); d = new Vector3(-side, 0f, 0f); };

    /// Köşeyi saran parçalar (far, stop) için silindirik yansıtma: düşey eksen (ax, az) etrafında,
    /// c.x = yay uzunluğu (θ·rNom, θ=0 tam önden / arkadan), c.y = yükseklik.
    static Frame Wrap(float ax, float az, float rNom, int side, bool rear) => (Vector2 c, out Vector3 o, out Vector3 d) =>
    {
        float th = c.x / rNom;
        var dir = new Vector3(side * Mathf.Sin(th), 0f, (rear ? -1f : 1f) * Mathf.Cos(th));
        o = new Vector3(ax, c.y, az) + dir * 2f;
        d = -dir;
    };

    static bool Surf(Frame f, Vector2 c, out Vector3 p, out Vector3 n)
    {
        f(c, out var o, out var d);
        n = -d;
        if (!Cast(o, d, out p)) return false;
        const float e = 0.004f;
        f(c + new Vector2(e, 0f), out var o1, out var d1);
        f(c + new Vector2(0f, e), out var o2, out var d2);
        f(c - new Vector2(e, 0f), out var o3, out var d3);
        f(c - new Vector2(0f, e), out var o4, out var d4);
        if (Cast(o1, d1, out var p1) && Cast(o2, d2, out var p2) && Cast(o3, d3, out var p3) && Cast(o4, d4, out var p4))
        {
            var nn = Vector3.Cross(p1 - p3, p2 - p4).normalized;
            if (Vector3.Dot(nn, d) > 0f) nn = -nn;
            if (nn.sqrMagnitude > 0.5f) n = nn;
        }
        return true;
    }

    /// Yüzeye yapışık 'çıkartma': 2B dış hat → halka halka içe doğru ızgara → her köşe yüzeye yansıtılır,
    /// normal boyunca 'offset' kadar dışarı alınır. skirt > 0 ise kenarı yüzeyin içine kadar kapatan bir etek eklenir.
    static void Decal(CarMesh m, string mat, Frame f, IList<Vector2> outline, float offset, int rings = 5,
        string skirtMat = null, float skirtDepth = 0.004f, Vector2? center = null, float uvScale = 1f)
    {
        Vector2 c = Vector2.zero;
        if (center.HasValue) c = center.Value;
        else { foreach (var p in outline) c += p; c /= outline.Count; }

        int n = outline.Count;
        int Vert(Vector2 q, float off, out Vector3 nrm)
        {
            if (!Surf(f, q, out var p, out nrm)) { f(q, out var o, out var d); p = o + d * 0.5f; }
            return m.Add(p + nrm * off, q / uvScale);
        }

        int ci = Vert(c, offset, out var cn);
        var prev = new int[n];
        var prevN = new Vector3[n];
        var cur = new int[n];
        var curN = new Vector3[n];
        for (int r = 1; r <= rings; r++)
        {
            float t = r / (float)rings;
            for (int k = 0; k < n; k++) cur[k] = Vert(c + (outline[k] - c) * t, offset, out curN[k]);
            for (int k = 0; k < n; k++)
            {
                int k2 = (k + 1) % n;
                if (r == 1) m.TriOut(mat, ci, cur[k], cur[k2], cn + curN[k] + curN[k2]);
                else m.QuadOut(mat, prev[k], cur[k], cur[k2], prev[k2], prevN[k] + curN[k] + curN[k2]);
            }
            Array.Copy(cur, prev, n);
            Array.Copy(curN, prevN, n);
        }

        if (skirtMat == null) return;
        var cPos = m.v[ci];
        var low = new int[n];
        for (int k = 0; k < n; k++) low[k] = m.Add(m.v[prev[k]] - prevN[k] * (offset + skirtDepth), m.uv[prev[k]]);
        for (int k = 0; k < n; k++)
        {
            int k2 = (k + 1) % n;
            var mid = (m.v[prev[k]] + m.v[prev[k2]]) / 2f;
            var outward = mid - cPos;
            outward -= prevN[k] * Vector3.Dot(outward, prevN[k]);
            m.QuadOut(skirtMat, prev[k], prev[k2], low[k2], low[k], outward);
        }
    }

    /// Yüzey üzerinde ince şerit (kapı aralığı, kaput çizgisi, krom çıta).
    static void Strip(CarMesh m, string mat, List<Vector3> pts, List<Vector3> nrm, float width, float offset)
    {
        int n = pts.Count;
        if (n < 2) return;
        int prevA = -1, prevB = -1;
        for (int i = 0; i < n; i++)
        {
            var t = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
            var b = Vector3.Cross(nrm[i], t).normalized * (width / 2f);
            var p = pts[i] + nrm[i] * offset;
            int a = m.Add(p - b), c = m.Add(p + b);
            if (i > 0) m.QuadOut(mat, prevA, a, c, prevB, nrm[i] + nrm[i - 1]);
            prevA = a; prevB = c;
        }
    }

    /// 2B çoklu çizgiyi 1 cm aralıklarla yüzeye yansıtıp şerit yapar.
    static void SurfaceLine(CarMesh m, string mat, Frame f, IList<Vector2> poly, float width, float offset, float step = 0.01f)
    {
        var pts = new List<Vector3>();
        var nrm = new List<Vector3>();
        for (int i = 0; i < poly.Count - 1; i++)
        {
            float len = (poly[i + 1] - poly[i]).magnitude;
            int k = Mathf.Max(1, Mathf.CeilToInt(len / step));
            for (int j = 0; j < k; j++)
            {
                var q = Vector2.Lerp(poly[i], poly[i + 1], j / (float)k);
                if (Surf(f, q, out var p, out var n)) { pts.Add(p); nrm.Add(n); }
            }
        }
        if (Surf(f, poly[poly.Count - 1], out var pl, out var nl)) { pts.Add(pl); nrm.Add(nl); }
        Strip(m, mat, pts, nrm, width, offset);
    }

    // ---------------- Katı parçalar ----------------
    static float SPow(float x, float e) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), e);

    /// Süper elips cisim: e küçükse köşeli-yuvarlak kutu, 1 ise elipsoit. Eksenler: ax (x), ay (y), az (z) yarı boylarıyla.
    static void Blob(CarMesh m, string mat, Vector3 c, Vector3 ax, Vector3 ay, Vector3 az, float eLat = 0.5f, float eLon = 0.5f,
        int seg = 16)
    {
        int lat = seg / 2, lon = seg;
        var idx = new int[lat + 1, lon + 1];
        for (int i = 0; i <= lat; i++)
        {
            float th = -Mathf.PI / 2f + Mathf.PI * i / lat;
            for (int j = 0; j <= lon; j++)
            {
                float ph = -Mathf.PI + 2f * Mathf.PI * j / lon;
                float x = SPow(Mathf.Cos(th), eLat) * SPow(Mathf.Cos(ph), eLon);
                float y = SPow(Mathf.Sin(th), eLat);
                float zz = SPow(Mathf.Cos(th), eLat) * SPow(Mathf.Sin(ph), eLon);
                idx[i, j] = (j == lon) ? idx[i, 0] : m.Add(c + ax * x + ay * y + az * zz, new Vector2(j / (float)lon, i / (float)lat));
            }
        }
        for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a = idx[i, j], b = idx[i, j + 1], cc = idx[i + 1, j + 1], d = idx[i + 1, j];
                var mid = (m.v[a] + m.v[cc]) / 2f - c;
                m.QuadOut(mat, a, b, cc, d, mid);
            }
    }

    /// Uçları farklı kalınlıkta, düz yüzlü çubuk (jant kolu, silecek, konsol...). wAx: genişlik ekseni.
    static void Bar(CarMesh m, string mat, Vector3 a, Vector3 b, Vector3 wAx, float w0, float t0, float w1, float t1)
    {
        var fwd = (b - a).normalized;
        var wx = (wAx - fwd * Vector3.Dot(wAx, fwd)).normalized;
        var tx = Vector3.Cross(fwd, wx).normalized;
        var c = (a + b) / 2f;
        Vector3 P(Vector3 o, float w, float t, int sw, int st) => o + wx * (sw * w / 2f) + tx * (st * t / 2f);
        var A = new[] { P(a, w0, t0, -1, -1), P(a, w0, t0, 1, -1), P(a, w0, t0, 1, 1), P(a, w0, t0, -1, 1) };
        var B = new[] { P(b, w1, t1, -1, -1), P(b, w1, t1, 1, -1), P(b, w1, t1, 1, 1), P(b, w1, t1, -1, 1) };
        void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i0 = m.Add(p0), i1 = m.Add(p1), i2 = m.Add(p2), i3 = m.Add(p3);
            m.QuadOut(mat, i0, i1, i2, i3, (p0 + p1 + p2 + p3) / 4f - c);
        }
        for (int k = 0; k < 4; k++) Face(A[k], A[(k + 1) % 4], B[(k + 1) % 4], B[k]);
        Face(A[0], A[1], A[2], A[3]);
        Face(B[0], B[1], B[2], B[3]);
    }

    /// İçi görünen (normalleri içe bakan) üstü açık kutu: bagaj havuzu.
    static void InnerBox(CarMesh m, string mat, Vector3 min, Vector3 max)
    {
        var c = (min + max) / 2f;
        void Face(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int i0 = m.Add(p0), i1 = m.Add(p1), i2 = m.Add(p2), i3 = m.Add(p3);
            m.QuadOut(mat, i0, i1, i2, i3, c - (p0 + p1 + p2 + p3) / 4f);
        }
        float x0 = min.x, x1 = max.x, y0 = min.y, y1 = max.y, z0 = min.z, z1 = max.z;
        Face(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y0, z1), new Vector3(x0, y0, z1)); // taban
        Face(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1));
        Face(new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x1, y0, z1));
        Face(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0));
        Face(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1));
    }

    /// Eksen etrafında döndürülmüş profil (lastik, jant, disk). Profil (a = eksen boyu, r = yarıçap);
    /// yüzün dışı, ilerleme yönünün (-dr, da) tarafıdır. mats: her profil parçası için malzeme.
    static void Revolve(CarMesh m, Vector3 c, Vector3 axis, Vector3 up, IList<Vector2> prof, IList<string> mats, int seg,
        float uvLen = 0.25f)
    {
        var right = Vector3.Cross(axis, up).normalized;
        up = Vector3.Cross(right, axis).normalized;
        int n = prof.Count;
        var cum = new float[n];
        for (int i = 1; i < n; i++) cum[i] = cum[i - 1] + (prof[i] - prof[i - 1]).magnitude;
        var idx = new int[n, seg + 1];
        for (int j = 0; j <= seg; j++)
        {
            float ang = 2f * Mathf.PI * j / seg;
            var rad = up * Mathf.Cos(ang) + right * Mathf.Sin(ang);
            for (int i = 0; i < n; i++)
                idx[i, j] = m.Add(c + axis * prof[i].x + rad * prof[i].y, new Vector2(cum[i] / uvLen, ang * 0.3f / uvLen));
        }
        for (int i = 0; i < n - 1; i++)
        {
            var dp = prof[i + 1] - prof[i];
            if (dp.sqrMagnitude < 1e-12f || mats[i] == null) continue;
            var n2 = new Vector2(-dp.y, dp.x);
            for (int j = 0; j < seg; j++)
            {
                float ang = 2f * Mathf.PI * (j + 0.5f) / seg;
                var rad = up * Mathf.Cos(ang) + right * Mathf.Sin(ang);
                m.QuadOut(mats[i], idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j], axis * n2.x + rad * n2.y);
            }
        }
    }

    static void Torus(CarMesh m, string mat, Vector3 c, Vector3 axis, Vector3 up, float R, float r, int seg = 40, int tube = 10)
    {
        var right = Vector3.Cross(axis, up).normalized;
        up = Vector3.Cross(right, axis).normalized;
        var idx = new int[seg + 1, tube + 1];
        for (int i = 0; i <= seg; i++)
        {
            float a = 2f * Mathf.PI * i / seg;
            var rad = up * Mathf.Cos(a) + right * Mathf.Sin(a);
            for (int j = 0; j <= tube; j++)
            {
                float b = 2f * Mathf.PI * j / tube;
                idx[i, j] = m.Add(c + rad * (R + r * Mathf.Cos(b)) + axis * (r * Mathf.Sin(b)));
            }
        }
        for (int i = 0; i < seg; i++)
            for (int j = 0; j < tube; j++)
            {
                float a = 2f * Mathf.PI * (i + 0.5f) / seg;
                var ctr = c + (up * Mathf.Cos(a) + right * Mathf.Sin(a)) * R;
                var mid = (m.v[idx[i, j]] + m.v[idx[i + 1, j + 1]]) / 2f;
                m.QuadOut(mat, idx[i, j], idx[i + 1, j], idx[i + 1, j + 1], idx[i, j + 1], mid - ctr);
            }
    }

    static List<Vector2> RoundRect(float cx, float cy, float hw, float hh, float r, int seg = 4)
    {
        var l = new List<Vector2>();
        r = Mathf.Min(r, Mathf.Min(hw, hh));
        var cs = new[] { new Vector2(hw - r, hh - r), new Vector2(-hw + r, hh - r), new Vector2(-hw + r, -hh + r), new Vector2(hw - r, -hh + r) };
        for (int k = 0; k < 4; k++)
            for (int i = 0; i <= seg; i++)
            {
                float a = (k * 90f + 90f * i / seg) * Mathf.Deg2Rad;
                l.Add(new Vector2(cx, cy) + cs[k] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        return l;
    }

    static List<Vector2> Circle(float cx, float cy, float r, int seg = 24)
    {
        var l = new List<Vector2>();
        for (int i = 0; i < seg; i++)
        {
            float a = 2f * Mathf.PI * i / seg;
            l.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
        }
        return l;
    }

    /// Kapalı çokgeni kenar boyunca sıklaştırır (yansıtılan çıkartma yüzeye iyi otursun).
    static List<Vector2> Dense(IList<Vector2> poly, float step = 0.02f)
    {
        var l = new List<Vector2>();
        for (int i = 0; i < poly.Count; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Count];
            int k = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / step));
            for (int j = 0; j < k; j++) l.Add(Vector2.Lerp(a, b, j / (float)k));
        }
        return l;
    }

    static List<Vector2> MirrorX(IList<Vector2> poly)
    {
        var l = new List<Vector2>();
        for (int i = poly.Count - 1; i >= 0; i--) l.Add(new Vector2(-poly[i].x, poly[i].y));
        return l;
    }

    // =====================================================================
    // KURULUM
    // =====================================================================

    public static CarModel Build()
    {
        EnsureCaches();
        var model = new CarModel();
        BuildShell(model);
        BuildCabinInterior(model);
        BuildTrunk(model);
        BuildFront(model);
        BuildRear(model);
        BuildSides(model);
        foreach (int s in new[] { -1, 1 })
        {
            BuildWheel(model, s < 0 ? "Tekerlek ÖnSol" : "Tekerlek ÖnSağ", new Vector3(s * TrackHalf, WheelR, AxleF), s, true);
            BuildWheel(model, s < 0 ? "Tekerlek ArkaSol" : "Tekerlek ArkaSağ", new Vector3(s * TrackHalf, WheelR, AxleR), s, false);
        }
        foreach (var m in model.meshes) m.ComputeNormals();
        return model;
    }

    static List<float> Samples(float z0, float z1, Func<float, float> step, params float[] breaks)
    {
        var l = new List<float>();
        for (float z = z0; z < z1; z += step(z)) l.Add(z);
        l.Add(z1);
        foreach (var b in breaks) if (b > z0 && b < z1) l.Add(b);
        l.Sort();
        var o = new List<float>();
        foreach (var z in l) if (o.Count == 0 || z - o[o.Count - 1] > 0.0015f) o.Add(z);
        return o;
    }

    /// Halkaları z boyunca birleştirip malzemesine göre ilgili mesh'e yazar.
    static void Loft(CarModel model, List<float> zs, Func<float, Ring> ring, Func<float, int, (CarMesh m, string mat)?> pick,
        bool capFront, bool capRear, string capMat)
    {
        var rings = new List<Ring>();
        foreach (var z in zs) rings.Add(ring(z));
        int n = rings[0].p.Count;
        var maps = new Dictionary<CarMesh, Dictionary<int, int>>();

        int V(CarMesh m, int i, int j)
        {
            if (!maps.TryGetValue(m, out var map)) maps[m] = map = new Dictionary<int, int>();
            int key = i * n + j;
            if (!map.TryGetValue(key, out var id))
            {
                var p = rings[i].p[j];
                map[key] = id = m.Add(new Vector3(p.x, p.y, zs[i]), new Vector2(zs[i], p.y + Mathf.Abs(p.x)));
            }
            return id;
        }

        for (int i = 0; i < zs.Count - 1; i++)
        {
            float zm = (zs[i] + zs[i + 1]) / 2f;
            for (int j = 0; j < n; j++)
            {
                int tag = rings[i].tag[j];
                if (tag == TSkip) continue;
                var sel = pick(zm, tag);
                if (!sel.HasValue) continue;
                var (m, mat) = sel.Value;
                int j2 = (j + 1) % n;
                int a = V(m, i, j), b = V(m, i, j2), c = V(m, i + 1, j2), d = V(m, i + 1, j);
                // Halka sağ alttan yukarı, sol üstten aşağı döner; z öne doğru → (j→j2) × (i→i+1) dışa bakar
                var outward = Vector3.Cross(m.v[b] - m.v[a], m.v[d] - m.v[a]);
                if (outward.sqrMagnitude < 1e-14f) outward = Vector3.Cross(m.v[c] - m.v[d], m.v[d] - m.v[a]);
                m.QuadOut(mat, a, b, c, d, outward);
            }
        }

        void Cap(int i, Vector3 dir)
        {
            var m = model.Get("Gövde");
            var ctr = Vector3.zero;
            foreach (var p in rings[i].p) ctr += new Vector3(p.x, p.y, zs[i]);
            ctr /= n;
            int ci = m.Add(ctr);
            for (int j = 0; j < n; j++)
            {
                var p0 = rings[i].p[j]; var p1 = rings[i].p[(j + 1) % n];
                int a = m.Add(new Vector3(p0.x, p0.y, zs[i])), b = m.Add(new Vector3(p1.x, p1.y, zs[i]));
                m.TriOut(capMat, ci, a, b, dir);
            }
        }
        if (capFront) Cap(zs.Count - 1, Vector3.forward);
        if (capRear) Cap(0, Vector3.back);
    }

    // ---------------- Gövde kabuğu + camlar ----------------
    static void BuildShell(CarModel model)
    {
        var body = model.Get("Gövde");
        var lid = model.Get("Bagaj Kapağı Dış", "Bagaj Kapağı");
        float lidTop = cTopEdge[LidZ0] + cCrown[LidZ0];
        model.pivots["Bagaj Kapağı"] = new Vector3(0f, lidTop, LidZ0);

        float Step(float z)
        {
            float e = Mathf.Min(z - ZR, ZF - z);
            if (e < 0.06f) return 0.004f;
            if (e < 0.16f) return 0.01f;
            if (Mathf.Abs(z - AxleF) < ArchR + 0.02f || Mathf.Abs(z - AxleR) < ArchR + 0.02f) return 0.02f;
            return 0.04f;
        }
        var zs = Samples(ZR, ZF, Step, LidZ0, LidZ1, CabinF, CabinR,
            AxleF - ArchR, AxleF + ArchR, AxleR - ArchR, AxleR + ArchR, AxleF, AxleR);

        Loft(model, zs, LowerRing, (zm, tag) =>
        {
            if (tag == TBottom || tag == TWell) return (body, "alt");
            if (tag == TTop)
            {
                if (zm < LidZ0 && zm > LidZ1) return (lid, "boya");
                if (zm > CabinR && zm < CabinF) return null;   // kabin üstü açık: cam ve iç mekân görünsün
            }
            return (body, "boya");
        }, true, true, "boya");

        // Bagaj kapağının iç yüzü (açınca görünür)
        var lidIn = model.Get("Bagaj Kapağı İç", "Bagaj Kapağı");
        int baseCount = lid.v.Count;
        lid.ComputeNormals();
        var map = new Dictionary<int, int>();
        foreach (var kv in lid.tris)
        {
            var l = kv.Value;
            for (int i = 0; i < l.Count; i += 3)
            {
                int[] t = { l[i], l[i + 2], l[i + 1] };
                for (int k = 0; k < 3; k++)
                    if (!map.TryGetValue(t[k], out var id))
                        map[t[k]] = id = lidIn.Add(lid.v[t[k]] - lid.normals[t[k]] * 0.012f);
                lidIn.Tri("boya", map[t[0]], map[t[1]], map[t[2]]);
            }
        }

        // Cam / tavan
        var zg = Samples(GhR, GhF, z => 0.03f, 0.10f, -0.80f, 0.82f, -0.02f, -0.11f, -0.93f, -0.98f, -1.42f);
        var gh = model.Get("Kabin");
        Loft(model, zg, GreenhouseRing, (zm, tag) =>
        {
            switch (tag)
            {
                case GSeal: return (gh, "conta");
                case GBottom: return null;
                case GPillar: return (gh, "boya");
                case GTop:
                    if (zm > 0.10f) return (gh, "cam");      // ön cam
                    if (zm < -0.80f) return (gh, "cam");     // arka cam
                    return (gh, "boya");                     // tavan
                case GSide:
                    if (zm > 0.82f) return (gh, "cerceve");  // ayna köşesi
                    if (zm > -0.02f) return (gh, "cam");     // ön kapı camı
                    if (zm > -0.11f) return (gh, "cerceve"); // B direği (parlak siyah)
                    if (zm > -0.93f) return (gh, "cam");     // arka kapı camı
                    if (zm > -0.98f) return (gh, "cerceve");
                    if (zm > -1.42f) return (gh, "cam");     // çeyrek cam (arka ucu C direği çıkartmasıyla eğik kesilir)
                    return (gh, "boya");                     // C direği
            }
            return null;
        }, false, false, null);

        // Kemer hattı krom çıtası (camın dibinde, kabuk ile cam arasındaki aralığı da kapatır)
        var trim = model.Get("Krom Çıtalar");
        foreach (int s in new[] { -1, 1 })
        {
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            for (float z = -1.38f; z <= 0.84f; z += 0.02f)
            {
                float x = s * (cHalfW[z] - 0.07f);
                pts.Add(new Vector3(x, Belt(z) + 0.004f, z));
                nrm.Add(new Vector3(s * 0.25f, 1f, 0f).normalized);
            }
            Strip(trim, "krom", pts, nrm, 0.034f, 0f);

            // Yan cam üst çerçevesi (krom), tavan kenarı boyunca
            pts = new List<Vector3>();
            nrm = new List<Vector3>();
            for (float z = -1.16f; z <= 0.80f; z += 0.01f)
            {
                var r = GreenhouseRing(z);
                // G2 noktası: ilk zincirin 6. örneği (G0 + 6 alt bölüm)
                var p = r.p[7]; var pa = r.p[6]; var pb = r.p[8];
                var t = (pb - pa).normalized;
                var nn = new Vector2(t.y, -t.x);
                pts.Add(new Vector3(s * p.x, p.y, z));
                nrm.Add(new Vector3(s * nn.x, nn.y, 0f).normalized);
            }
            Strip(trim, "krom", pts, nrm, 0.012f, 0.003f);
        }

        // C direği: çeyrek camın arka kenarı eğik (alttan arkaya, üstten öne)
        foreach (int s in new[] { -1, 1 })
        {
            var cp = new List<Vector2> { new Vector2(-1.38f, Belt(-1.38f) - 0.02f), new Vector2(-1.17f, RailY(-1.17f) + 0.012f) };
            for (float z = -1.22f; z >= -1.66f; z -= 0.04f) cp.Add(new Vector2(z, RailY(z) + 0.012f));
            for (float z = -1.66f; z <= -1.42f; z += 0.04f) cp.Add(new Vector2(z, Belt(z) - 0.02f));
            Decal(gh, "boya", Side(s), Dense(cp, 0.015f), 0.0015f, 4, "boya", 0.003f);
        }

        // Kaput / ön cam arası (silecek havuzu)
        Bar(body, "plastikMat", new Vector3(-0.80f, 0.95f, 0.962f), new Vector3(0.80f, 0.95f, 0.962f), Vector3.forward, 0.07f, 0.02f, 0.07f, 0.02f);
    }

    // ---------------- Kabin içi ----------------
    static void BuildCabinInterior(CarModel model)
    {
        var inn = model.Get("İç Mekân");
        // İç kabuk (kapı döşemeleri + taban), normaller içe
        var zs = Samples(CabinR - 0.02f, CabinF + 0.01f, z => 0.05f);
        Ring Inner(float z)
        {
            float w = cHalfW[z], top = cTopEdge[z], bw = w - 0.085f;
            var F0 = new Vector2(0f, 0.30f);
            var F1 = new Vector2(w - 0.12f, 0.30f);
            var F2 = new Vector2(w - 0.07f, 0.42f);
            var F3 = new Vector2(w - 0.06f, 0.70f);
            var F4 = new Vector2(w - 0.075f, top - 0.012f);
            var F5 = new Vector2(bw - 0.05f, top - 0.012f);
            var r = new Ring();
            r.Add(M(F5), 1); r.Add(M(F4), 1); r.Add(M(F3), 1); r.Add(M(F2), 2); r.Add(M(F1), 2); r.Add(F0, 2);
            r.Add(F1, 2); r.Add(F2, 1); r.Add(F3, 1); r.Add(F4, 1); r.Add(F5, TSkip);
            return r;
        }
        var rings = new List<Ring>();
        foreach (var z in zs) rings.Add(Inner(z));
        int n = rings[0].p.Count;
        var idx = new int[zs.Count, n];
        for (int i = 0; i < zs.Count; i++)
            for (int j = 0; j < n; j++) idx[i, j] = inn.Add(new Vector3(rings[i].p[j].x, rings[i].p[j].y, zs[i]));
        for (int i = 0; i < zs.Count - 1; i++)
            for (int j = 0; j < n - 1; j++)
            {
                int tag = rings[i].tag[j];
                var pa = rings[i].p[j]; var pb = rings[i].p[j + 1];
                var mid = (pa + pb) / 2f;
                // içe bakan: kesitin ortasına doğru
                var inward = new Vector3(-mid.x, 0.75f - mid.y, 0f);
                if (tag == 2) inward = Vector3.up;
                if (j == 0 || j == n - 2) inward = Vector3.down; // üst flanş: yukarıdan cam aralığından bakınca siyah
                inn.QuadOut(tag == 2 ? "hali" : (j == 0 || j == n - 2 ? "conta" : "icPlastik"),
                    idx[i, j], idx[i, j + 1], idx[i + 1, j + 1], idx[i + 1, j], inward);
            }
        // Flanşın üst yüzü yukarıdan görünür (normal yukarı)
        for (int i = 0; i < zs.Count - 1; i++)
            foreach (int j in new[] { 0, n - 2 })
            {
                int a = inn.Add(inn.v[idx[i, j]]), b = inn.Add(inn.v[idx[i, j + 1]]), c = inn.Add(inn.v[idx[i + 1, j + 1]]), d = inn.Add(inn.v[idx[i + 1, j]]);
                inn.QuadOut("conta", a, b, c, d, Vector3.up);
            }

        // Ön bölme (motor tarafını kapatır)
        Bar(inn, "icPlastik", new Vector3(0f, 0.30f, CabinF - 0.01f), new Vector3(0f, 0.94f, CabinF - 0.01f), Vector3.right, 1.62f, 0.04f, 1.62f, 0.04f);

        // Torpido: ana gövde, gösterge kaputu, orta ekran, havalandırmalar
        Blob(inn, "torpido", new Vector3(0f, 0.80f, 0.78f), Vector3.right * 0.80f, Vector3.up * 0.17f, Vector3.forward * 0.22f, 0.35f, 0.3f, 20);
        Blob(inn, "torpido", new Vector3(-0.37f, 0.97f, 0.62f), Vector3.right * 0.19f, Vector3.up * 0.06f, Vector3.forward * 0.10f, 0.5f, 0.4f);
        Blob(inn, "ekran", new Vector3(-0.37f, 0.93f, 0.56f), Vector3.right * 0.15f, Vector3.up * 0.05f, Vector3.forward * 0.012f, 0.3f, 0.3f);
        var scrUp = new Vector3(0f, 0.95f, 0.3f).normalized;
        Blob(inn, "ekran", new Vector3(0f, 0.95f, 0.56f), Vector3.right * 0.10f, scrUp * 0.06f, Vector3.Cross(Vector3.right, scrUp) * 0.012f, 0.25f, 0.25f);
        foreach (float vx in new[] { -0.70f, -0.13f, 0.13f, 0.70f })
            Blob(inn, "cerceve", new Vector3(vx, 0.86f, 0.565f), Vector3.right * 0.045f, Vector3.up * 0.03f, Vector3.forward * 0.01f, 0.4f, 0.4f);

        // Orta konsol + vites
        Blob(inn, "icPlastik", new Vector3(0f, 0.45f, 0.30f), Vector3.right * 0.12f, Vector3.up * 0.16f, Vector3.forward * 0.42f, 0.3f, 0.3f);
        Blob(inn, "deri", new Vector3(0f, 0.62f, 0.38f), Vector3.right * 0.05f, Vector3.up * 0.04f, Vector3.forward * 0.06f, 0.8f, 0.8f);
        Bar(inn, "krom", new Vector3(0f, 0.62f, 0.38f), new Vector3(0f, 0.72f, 0.37f), Vector3.right, 0.012f, 0.012f, 0.012f, 0.012f);
        Blob(inn, "deri", new Vector3(0f, 0.74f, 0.37f), Vector3.right * 0.025f, Vector3.up * 0.03f, Vector3.forward * 0.03f, 0.9f, 0.9f);

        // Direksiyon (ayrı grup: sürüşte döner)
        var colAxis = new Vector3(0f, 0.42f, -0.91f).normalized;   // sürücüye bakan eksen
        var wc = new Vector3(-0.37f, 0.93f, 0.50f);
        model.pivots["Direksiyon"] = wc;
        model.anchors["Direksiyon Ekseni"] = (wc, colAxis);
        var sw = model.Get("Direksiyon", "Direksiyon");
        Torus(sw, "deri", wc, colAxis, Vector3.up, 0.183f, 0.017f);
        Blob(sw, "icPlastik", wc - colAxis * 0.01f, Vector3.right * 0.075f, Vector3.Cross(Vector3.right, colAxis) * 0.065f, colAxis * 0.035f, 0.5f, 0.5f);
        var swUp = Vector3.Cross(Vector3.right, colAxis).normalized;   // simidin "yukarısı"
        foreach (var dir in new[] { Vector3.right, -Vector3.right, -swUp })  // sol, sağ ve aşağı kol
            Bar(sw, "icPlastik", wc + dir * 0.06f, wc + dir * 0.17f, Vector3.Cross(colAxis, dir), 0.04f, 0.018f, 0.03f, 0.016f);
        // Direksiyon mili simitten torpidoya doğru (sürücünün tersine) uzanır
        Bar(inn, "icPlastik", wc - colAxis * 0.03f, wc - colAxis * 0.25f, Vector3.right, 0.07f, 0.07f, 0.09f, 0.09f);

        // Koltuklar (ön iki koltuk + arka sıra)
        foreach (float sx in new[] { -0.37f, 0.37f })
        {
            Blob(inn, "kumas", new Vector3(sx, 0.47f, 0.02f), Vector3.right * 0.25f, Vector3.up * 0.075f, Vector3.forward * 0.26f, 0.35f, 0.3f);
            var back = new Vector3(0f, 0.96f, -0.27f).normalized;
            Blob(inn, "kumas", new Vector3(sx, 0.83f, -0.30f), Vector3.right * 0.25f, back * 0.32f, Vector3.Cross(Vector3.right, back) * 0.075f, 0.35f, 0.3f);
            Blob(inn, "kumas", new Vector3(sx, 1.22f, -0.40f), Vector3.right * 0.13f, back * 0.09f, Vector3.Cross(Vector3.right, back) * 0.055f, 0.5f, 0.45f);
            Bar(inn, "krom", new Vector3(sx - 0.07f, 1.10f, -0.37f), new Vector3(sx - 0.07f, 1.16f, -0.385f), Vector3.right, 0.01f, 0.01f, 0.01f, 0.01f);
            Bar(inn, "krom", new Vector3(sx + 0.07f, 1.10f, -0.37f), new Vector3(sx + 0.07f, 1.16f, -0.385f), Vector3.right, 0.01f, 0.01f, 0.01f, 0.01f);
            Bar(inn, "icPlastik", new Vector3(sx, 0.34f, 0.02f), new Vector3(sx, 0.40f, 0.02f), Vector3.right, 0.4f, 0.45f, 0.4f, 0.45f);
        }
        Blob(inn, "kumas", new Vector3(0f, 0.47f, -0.86f), Vector3.right * 0.68f, Vector3.up * 0.085f, Vector3.forward * 0.26f, 0.35f, 0.3f);
        var rb = new Vector3(0f, 0.94f, -0.34f).normalized;
        Blob(inn, "kumas", new Vector3(0f, 0.80f, -1.14f), Vector3.right * 0.68f, rb * 0.30f, Vector3.Cross(Vector3.right, rb) * 0.08f, 0.35f, 0.3f);
        foreach (float sx in new[] { -0.42f, 0.42f })
            Blob(inn, "kumas", new Vector3(sx, 1.15f, -1.27f), Vector3.right * 0.12f, rb * 0.07f, Vector3.Cross(Vector3.right, rb) * 0.05f, 0.5f, 0.45f);

        // Arka çamurluk içi (lastiğin kabine/bagaja bakan kılıfı). Yüzleri yalnızca içeri bakar,
        // dışarıdan (tekerlek yuvasından) bakınca görünmez, lastik görünür kalır.
        foreach (int s in new[] { -1, 1 })
        {
            float x0 = s * 0.60f, x1 = s * 0.84f, y0 = 0.29f, y1 = 0.74f, z0 = AxleR - 0.40f, z1 = AxleR + 0.40f;
            void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 o)
            {
                int i0 = inn.Add(a), i1 = inn.Add(b), i2 = inn.Add(c), i3 = inn.Add(d);
                inn.QuadOut("hali", i0, i1, i2, i3, o);
            }
            Q(new Vector3(x0, y0, z0), new Vector3(x0, y1, z0), new Vector3(x0, y1, z1), new Vector3(x0, y0, z1), new Vector3(-s, 0f, 0f));
            Q(new Vector3(x0, y1, z0), new Vector3(x1, y1, z0), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.up);
            Q(new Vector3(x0, y0, z1), new Vector3(x1, y0, z1), new Vector3(x1, y1, z1), new Vector3(x0, y1, z1), Vector3.forward);
            Q(new Vector3(x0, y0, z0), new Vector3(x1, y0, z0), new Vector3(x1, y1, z0), new Vector3(x0, y1, z0), Vector3.back);
        }

        // Tavan döşemesi (cam/tavanın iç yüzü, sadece boyalı bölgelerde)
        var hl = model.Get("Tavan Döşemesi");
        var zr = Samples(-0.80f, 0.10f, z => 0.05f);
        int[] prev = null;
        foreach (var z in zr)
        {
            var r = GreenhouseRing(z);
            var cur = new int[r.p.Count];
            for (int j = 0; j < r.p.Count; j++)
            {
                var p = r.p[j];
                cur[j] = hl.Add(new Vector3(p.x * 0.985f, p.y - 0.018f, z));
            }
            if (prev != null)
                for (int j = 7; j < r.p.Count - 8; j++)   // tavan bölümü (G2'den G2'ye)
                    hl.QuadOut("tavan", prev[j], prev[j + 1], cur[j + 1], cur[j], Vector3.down);
            prev = cur;
        }

        model.anchors["Sürücü Göz"] = (new Vector3(-0.37f, 1.17f, -0.08f), Vector3.forward);
    }

    // ---------------- Bagaj ----------------
    static void BuildTrunk(CarModel model)
    {
        var tr = model.Get("Bagaj");
        float rim = cTopEdge[-1.9f] - 0.015f;
        InnerBox(tr, "hali", new Vector3(-TrunkHalfW, TrunkFloorY, TrunkRearZ), new Vector3(TrunkHalfW, rim, TrunkFrontZ));
        // Kenar flanşları (kapak açıkken görünen siyah oluk): havuz duvarından gövde üst kenarına kadar
        var zs = Samples(LidZ1 - 0.01f, LidZ0 - 0.03f, z => 0.03f); // önde kabin iç kabuğunun flanşıyla çakışmasın
        foreach (int s in new[] { -1, 1 })
            for (int i = 0; i < zs.Count - 1; i++)
            {
                float za = zs[i], zb = zs[i + 1];
                int a = tr.Add(new Vector3(s * (TrunkHalfW - 0.01f), rim, za)), b = tr.Add(new Vector3(s * (cHalfW[za] - 0.06f), rim, za));
                int c = tr.Add(new Vector3(s * (cHalfW[zb] - 0.06f), rim, zb)), d = tr.Add(new Vector3(s * (TrunkHalfW - 0.01f), rim, zb));
                tr.QuadOut("conta", a, b, c, d, Vector3.up);
            }
        {
            float x = cHalfW[LidZ1] - 0.06f;
            int a = tr.Add(new Vector3(-x, rim, TrunkRearZ + 0.01f)), b = tr.Add(new Vector3(x, rim, TrunkRearZ + 0.01f));
            int c = tr.Add(new Vector3(x, rim, LidZ1 - 0.005f)), d = tr.Add(new Vector3(-x, rim, LidZ1 - 0.005f));
            tr.QuadOut("conta", a, b, c, d, Vector3.up);
        }
        // Arka raf (arka camın altı)
        float shelfY = cTopEdge[-1.5f] - 0.02f;
        Bar(tr, "hali", new Vector3(0f, shelfY, TrunkFrontZ + 0.02f), new Vector3(0f, shelfY, LidZ0 - 0.03f), Vector3.right, 1.62f, 0.02f, 1.62f, 0.02f);
        model.anchors["Bagaj Zemini"] = (new Vector3(0f, TrunkFloorY, -1.72f), Vector3.up);
    }

    // ---------------- Ön yüz ----------------
    static void BuildFront(CarModel model)
    {
        var fr = model.Get("Ön");
        var lamps = model.Get("Farlar");

        // Alt hava girişi (petek), plaka bunun üstüne
        var intake = new List<Vector2>
        {
            new Vector2(-0.44f, 0.50f), new Vector2(0.44f, 0.50f), new Vector2(0.46f, 0.45f), new Vector2(0.40f, 0.29f),
            new Vector2(-0.40f, 0.29f), new Vector2(-0.46f, 0.45f)
        };
        Decal(fr, "plastikMat", Front, Dense(Expand(intake, 0.018f)), 0.002f, 6);
        Decal(fr, "petek", Front, Dense(intake), 0.0035f, 6, "plastikMat", 0.004f, null, 1f);
        // Alt dudak (spoiler)
        Decal(fr, "plastikMat", Front, Dense(new List<Vector2> { new Vector2(-0.62f, 0.25f), new Vector2(0.62f, 0.25f), new Vector2(0.58f, 0.215f), new Vector2(-0.58f, 0.215f) }), 0.003f, 3);

        // Üst ızgara: krom çerçeve + siyah petek + amblem
        var grille = new List<Vector2>
        {
            new Vector2(-0.33f, 0.765f), new Vector2(0.33f, 0.765f), new Vector2(0.30f, 0.715f), new Vector2(0.26f, 0.685f),
            new Vector2(-0.26f, 0.685f), new Vector2(-0.30f, 0.715f)
        };
        Decal(fr, "krom", Front, Dense(Expand(grille, 0.012f)), 0.004f, 5, "krom", 0.006f);
        Decal(fr, "petek", Front, Dense(grille), 0.0055f, 5);
        Decal(fr, "krom", Front, Dense(new List<Vector2> { new Vector2(-0.29f, 0.731f), new Vector2(0.29f, 0.731f), new Vector2(0.29f, 0.719f), new Vector2(-0.29f, 0.719f) }), 0.0075f, 2);
        Emblem(fr, Front, new Vector2(0f, 0.727f), 0.048f, 0.01f);

        foreach (int s in new[] { -1, 1 })
        {
            // Far: köşeyi saran gövde, projektör, LED gündüz farı, sinyal, şeffaf cam
            var hf = Wrap(s * 0.30f, 1.72f, 0.6f, s, false);
            var hl = new List<Vector2>
            {
                new Vector2(0.01f, 0.672f), new Vector2(0.01f, 0.758f), new Vector2(0.14f, 0.788f), new Vector2(0.30f, 0.803f),
                new Vector2(0.46f, 0.806f), new Vector2(0.58f, 0.798f), new Vector2(0.64f, 0.786f), new Vector2(0.57f, 0.772f),
                new Vector2(0.46f, 0.748f), new Vector2(0.34f, 0.712f), new Vector2(0.20f, 0.676f), new Vector2(0.09f, 0.664f)
            };
            var hd = Dense(hl, 0.012f);
            Decal(lamps, "farGovde", hf, hd, 0.002f, 7);
            // İki projektör yuvası (krom çanak + mercek)
            foreach (var pc in new[] { new Vector2(0.14f, 0.725f), new Vector2(0.28f, 0.748f) })
            {
                Decal(lamps, "reflektor", hf, Dense(Circle(pc.x, pc.y, 0.034f, 24), 0.008f), 0.0045f, 3, "reflektor", 0.004f);
                Decal(lamps, "farGovde", hf, Dense(Circle(pc.x, pc.y, 0.022f, 20), 0.008f), 0.0065f, 2, "reflektor", 0.002f);
                Decal(lamps, "farLens", hf, Dense(Circle(pc.x, pc.y, 0.017f, 16), 0.008f), 0.0085f, 2);
            }
            // LED gündüz farı: üst kenar boyunca ince ışık bandı
            Decal(lamps, "farDRL", hf, Dense(Strip2(new[] { new Vector2(0.04f, 0.760f), new Vector2(0.17f, 0.783f), new Vector2(0.33f, 0.794f), new Vector2(0.48f, 0.795f), new Vector2(0.58f, 0.788f) }, 0.006f), 0.01f), 0.006f, 2);
            // Sinyal (dış uç)
            Decal(lamps, "amber", hf, Dense(Strip2(new[] { new Vector2(0.40f, 0.752f), new Vector2(0.50f, 0.766f), new Vector2(0.57f, 0.778f) }, 0.008f), 0.01f), 0.0055f, 2);
            Decal(lamps, "farLens", hf, hd, 0.012f, 6, "cerceve", 0.004f);

            // Sis farı: siyah yuva, krom C çıta, yansıtıcı + cam
            var fogC = new Vector2(s * 0.665f, 0.40f);
            var fogHousing = new List<Vector2>();
            foreach (var o in new[] { new Vector2(-0.085f, 0.06f), new Vector2(0.085f, 0.075f), new Vector2(0.10f, -0.03f),
                                      new Vector2(0.06f, -0.08f), new Vector2(-0.07f, -0.075f), new Vector2(-0.10f, -0.01f) })
                fogHousing.Add(fogC + new Vector2(o.x * s, o.y));
            Decal(fr, "plastikMat", Front, Dense(fogHousing, 0.012f), 0.0025f, 4, "plastikMat", 0.004f);
            Decal(fr, "reflektor", Front, Dense(Circle(fogC.x, fogC.y, 0.042f, 24), 0.008f), 0.005f, 3, "krom", 0.004f);
            Decal(fr, "farLens", Front, Dense(Circle(fogC.x, fogC.y, 0.036f, 24), 0.008f), 0.009f, 2);
            // İç tarafı açık "C" krom çıta
            var arc = new List<Vector2>();
            for (int i = 0; i <= 14; i++)
            {
                float ar = (-120f + 240f * i / 14f) * Mathf.Deg2Rad;
                arc.Add(fogC + new Vector2(Mathf.Cos(ar) * 0.062f * s, Mathf.Sin(ar) * 0.062f));
            }
            SurfaceLine(fr, "krom", Front, arc, 0.009f, 0.0045f, 0.006f);
        }

        // Kaput çizgisi (üstten): çamurluk kenarı boyunca, önde ızgaranın üstünde döner
        var hood = new List<Vector2>();
        for (float z = CabinF + 0.005f; z <= 2.04f; z += 0.04f) hood.Add(new Vector2(cHalfW[z] - 0.11f, z));
        hood.Add(new Vector2(0.72f, 2.10f)); hood.Add(new Vector2(0.45f, 2.135f)); hood.Add(new Vector2(0f, 2.145f));
        var full = new List<Vector2>(hood);
        for (int i = hood.Count - 2; i >= 0; i--) full.Add(new Vector2(-hood[i].x, hood[i].y));
        SurfaceLine(fr, "aralik", Top, full, 0.0045f, 0.0012f);

        // Tampon ↔ çamurluk birleşimi: farın alt dış ucundan kemerin önüne
        foreach (int s in new[] { -1, 1 })
            SurfaceLine(fr, "aralik", Side(s), new List<Vector2>
            {
                new Vector2(1.93f, 0.80f), new Vector2(1.84f, 0.74f), new Vector2(AxleF + ArchR + 0.035f, 0.66f)
            }, 0.004f, 0.0012f);

        model.anchors["Plaka Ön"] = AnchorOf(Front, new Vector2(0f, 0.43f));
    }

    // ---------------- Arka yüz ----------------
    static void BuildRear(CarModel model)
    {
        var rr = model.Get("Arka");
        var lamps = model.Get("Stoplar");

        foreach (int s in new[] { -1, 1 })
        {
            var tf = Wrap(s * 0.40f, -1.85f, 0.5f, s, true);
            var tl = new List<Vector2>
            {
                new Vector2(-0.09f, 0.850f), new Vector2(-0.09f, 0.990f), new Vector2(0.12f, 1.006f), new Vector2(0.36f, 1.012f),
                new Vector2(0.54f, 1.006f), new Vector2(0.68f, 0.990f), new Vector2(0.66f, 0.962f), new Vector2(0.52f, 0.925f),
                new Vector2(0.34f, 0.880f), new Vector2(0.14f, 0.855f)
            };
            var td = Dense(tl, 0.012f);
            Decal(lamps, "stopIc", tf, td, 0.002f, 7);
            // LED çubukları + geri vites lambası
            Decal(lamps, "stopKirmizi", tf, Dense(Strip2(new[] { new Vector2(-0.07f, 0.976f), new Vector2(0.14f, 0.992f), new Vector2(0.40f, 0.997f), new Vector2(0.62f, 0.986f) }, 0.010f), 0.01f), 0.0045f, 2);
            Decal(lamps, "stopKirmizi", tf, Dense(Strip2(new[] { new Vector2(-0.05f, 0.880f), new Vector2(0.16f, 0.888f), new Vector2(0.38f, 0.915f), new Vector2(0.56f, 0.950f) }, 0.007f), 0.01f), 0.0045f, 2);
            Decal(lamps, "stopBeyaz", tf, Dense(RoundRect(0.06f, 0.933f, 0.09f, 0.02f, 0.008f), 0.01f), 0.004f, 2);
            Decal(lamps, "stopLens", tf, td, 0.009f, 6, "cerceve", 0.004f);

            // Arka tampon reflektörü
            Decal(rr, "stopKirmizi", Rear, Dense(RoundRect(s * 0.66f, 0.50f, 0.07f, 0.009f, 0.008f), 0.01f), 0.003f, 2, "plastikMat", 0.003f);
        }

        // Difüzör + egzoz çıkış çerçeveleri
        var diff = new List<Vector2> { new Vector2(-0.70f, 0.455f), new Vector2(0.70f, 0.455f), new Vector2(0.64f, 0.30f), new Vector2(-0.64f, 0.30f) };
        Decal(rr, "plastikMat", Rear, Dense(diff), 0.0025f, 5);
        foreach (int s in new[] { -1, 1 })
        {
            Decal(rr, "krom", Rear, Dense(RoundRect(s * 0.47f, 0.365f, 0.09f, 0.035f, 0.02f), 0.01f), 0.005f, 3, "krom", 0.004f);
            Decal(rr, "alt", Rear, Dense(RoundRect(s * 0.47f, 0.365f, 0.075f, 0.022f, 0.014f), 0.01f), 0.0065f, 2);
            for (int k = -1; k <= 1; k++)
                Decal(rr, "plastikMat", Rear, Dense(RoundRect(s * (0.17f + 0.07f * k), 0.35f, 0.006f, 0.045f, 0.004f), 0.01f), 0.006f, 2, "plastikMat", 0.004f);
        }

        // Bagaj kapağı çizgisi (üstten)
        var lidL = new List<Vector2>();
        lidL.Add(new Vector2(0f, LidZ0 + 0.003f));
        for (float x = 0f; x <= 0.80f; x += 0.05f) lidL.Insert(0, new Vector2(-x, LidZ0 + 0.003f));
        var lidPoly = new List<Vector2>();
        for (float x = -0.80f; x <= 0.80f; x += 0.05f) lidPoly.Add(new Vector2(x, LidZ0 - 0.003f));
        for (float z = LidZ0 - 0.05f; z >= LidZ1; z -= 0.05f) lidPoly.Add(new Vector2(cHalfW[z] - 0.058f, z));
        for (float x = 0.80f; x >= -0.80f; x -= 0.05f) lidPoly.Add(new Vector2(Mathf.Min(x, cHalfW[LidZ1] - 0.058f), LidZ1 + 0.003f));
        for (float z = LidZ1 + 0.05f; z <= LidZ0; z += 0.05f) lidPoly.Add(new Vector2(-(cHalfW[z] - 0.058f), z));
        lidPoly.Add(lidPoly[0]);
        SurfaceLine(rr, "aralik", Top, lidPoly, 0.0045f, 0.0012f);

        // Tampon üst birleşim çizgisi: arkadan çamurluğa, arka kemerin arkasına kadar
        var bump = new List<Vector2>();
        for (float x = -0.86f; x <= 0.861f; x += 0.04f) bump.Add(new Vector2(x, 0.765f));
        SurfaceLine(rr, "aralik", Rear, bump, 0.004f, 0.0012f);
        foreach (int s in new[] { -1, 1 })
            SurfaceLine(rr, "aralik", Side(s), new List<Vector2> { new Vector2(-2.17f, 0.765f), new Vector2(-1.95f, 0.745f), new Vector2(AxleR - ArchR - 0.035f, 0.66f) }, 0.004f, 0.0012f);

        Emblem(rr, Rear, new Vector2(0f, 0.985f), 0.04f, 0.006f);
        model.anchors["Plaka Arka"] = AnchorOf(Rear, new Vector2(0f, 0.88f));
    }

    // ---------------- Yanlar: kapı aralıkları, kollar, aynalar, depo kapağı ----------------
    static void BuildSides(CarModel model)
    {
        var sd = model.Get("Yanlar");
        foreach (int s in new[] { -1, 1 })
        {
            var f = Side(s);
            float By(float z) => Belt(z) - 0.004f;

            // Ön kapı ön kenarı → alt kenar → B aralığı
            SurfaceLine(sd, "aralik", f, new List<Vector2> { new Vector2(0.975f, By(0.975f)), new Vector2(0.960f, 0.62f), new Vector2(0.955f, 0.27f), new Vector2(-0.88f, 0.27f) }, 0.0045f, 0.0012f);
            SurfaceLine(sd, "aralik", f, new List<Vector2> { new Vector2(-0.06f, By(-0.06f)), new Vector2(-0.065f, 0.27f) }, 0.0045f, 0.0012f);
            // Arka kapı arka kenarı: dikey iner, kemerin etrafından döner
            var rd = new List<Vector2> { new Vector2(-0.99f, By(-0.99f)), new Vector2(-0.99f, 0.575f) };
            for (int i = 1; i <= 8; i++)
            {
                float zz = Mathf.Lerp(-0.99f, AxleR + 0.40f, i / 8f);
                float dz = zz - AxleR;
                rd.Add(new Vector2(zz, ArchY + Mathf.Sqrt(Mathf.Max(0f, 0.40f * 0.40f - dz * dz))));
            }
            rd.Add(new Vector2(AxleR + 0.40f, 0.27f));
            SurfaceLine(sd, "aralik", f, rd, 0.0045f, 0.0012f);

            // Kapı kolları (gövde renginde) ve arkalarındaki çukur
            foreach (float hz in new[] { 0.22f, -0.78f })
            {
                // Omuz çizgisinin hemen altında (çukur keskin kırıma taşmasın)
                var (p, n) = AnchorOf(f, new Vector2(hz, 0.85f));
                Decal(sd, "cerceve", f, Dense(RoundRect(hz, 0.85f, 0.075f, 0.024f, 0.02f), 0.01f), 0.0012f, 3);
                Blob(sd, "boya", p + n * 0.011f, Vector3.forward * 0.075f, Vector3.up * 0.016f, n * 0.011f, 0.35f, 0.45f, 16);
            }

            // Ayna: siyah ayak + gövde renkli kapak + ayna camı
            var mb = new Vector3(s * 0.845f, Belt(0.86f) + 0.03f, 0.865f);
            Blob(sd, "cerceve", mb, Vector3.right * 0.045f, Vector3.up * 0.03f, Vector3.forward * 0.06f, 0.6f, 0.5f, 14);
            var mc = new Vector3(s * 0.955f, Belt(0.86f) + 0.07f, 0.85f);
            var mx = new Vector3(s, 0f, 0.15f).normalized;
            var mz = Vector3.Cross(mx, Vector3.up).normalized * s;
            Blob(sd, "boya", mc, mx * 0.095f, Vector3.up * 0.058f, mz * 0.05f, 0.55f, 0.45f, 24);
            Blob(sd, "aynaCam", mc - mz * 0.047f + mx * 0.003f, mx * 0.082f, Vector3.up * 0.046f, mz * 0.003f, 0.3f, 0.3f, 16);
            Bar(sd, "cerceve", mb + new Vector3(s * 0.02f, 0.005f, 0f), mc + new Vector3(-s * 0.05f, -0.015f, 0.005f), Vector3.up, 0.035f, 0.04f, 0.025f, 0.03f);
        }

        // Depo kapağı (sağ arka çamurluk)
        Decal(sd, "boya", Side(1), Dense(RoundRect(-1.55f, 0.83f, 0.085f, 0.072f, 0.03f), 0.01f), 0.0015f, 3, "aralik", 0.003f);

        // Silecekler (ön cam dibinde, kaputun hemen üstünde)
        foreach (var (x0, x1, z0, z1) in new[] { (-0.72f, 0.02f, 0.905f, 0.865f), (0.10f, 0.70f, 0.875f, 0.905f) })
        {
            var pts = new List<Vector3>();
            var nrm = new List<Vector3>();
            for (int i = 0; i <= 30; i++)
            {
                float t = i / 30f;
                if (Surf(Top, new Vector2(Mathf.Lerp(x0, x1, t), Mathf.Lerp(z0, z1, t)), out var p, out var n)) { pts.Add(p); nrm.Add(n); }
            }
            Strip(sd, "plastikMat", pts, nrm, 0.012f, 0.012f);
            Strip(sd, "plastikMat", pts, nrm, 0.006f, 0.02f);
            if (pts.Count > 2)
            {
                var mid = pts[pts.Count / 2] + nrm[pts.Count / 2] * 0.025f;
                var piv = new Vector3((x0 + x1) / 2f - 0.15f, 0.965f, 0.955f);
                Bar(sd, "plastikMat", piv, mid, Vector3.up, 0.014f, 0.01f, 0.01f, 0.008f);
            }
        }

        // Anten (tavan arkası)
        var (ap, an) = AnchorOf(Top, new Vector2(0f, -0.72f));
        Blob(sd, "cerceve", ap + an * 0.012f + Vector3.back * 0.02f, Vector3.right * 0.022f, Vector3.up * 0.035f, Vector3.forward * 0.075f, 0.7f, 0.6f, 14);
    }

    // ---------------- Tekerlek ----------------
    static void BuildWheel(CarModel model, string grp, Vector3 hub, int side, bool front)
    {
        string spin = grp + "/Dönen";
        model.pivots[grp] = hub;
        model.pivots[spin] = hub;
        var tire = model.Get("Lastik", spin);
        var rim = model.Get("Jant", spin);
        var ax = new Vector3(side, 0f, 0f);

        // Lastik: 205/55 R16, sırt bölgesinde 4 boyuna kanal
        var tp = new List<Vector2>
        {
            new Vector2(-0.092f, 0.205f), new Vector2(-0.100f, 0.225f), new Vector2(-0.104f, 0.255f), new Vector2(-0.101f, 0.282f),
            new Vector2(-0.094f, 0.300f), new Vector2(-0.083f, 0.311f), new Vector2(-0.070f, 0.316f)
        };
        foreach (float g in new[] { -0.052f, -0.018f, 0.018f, 0.052f })
        {
            tp.Add(new Vector2(g - 0.0055f, 0.316f)); tp.Add(new Vector2(g - 0.0045f, 0.307f));
            tp.Add(new Vector2(g + 0.0045f, 0.307f)); tp.Add(new Vector2(g + 0.0055f, 0.316f));
        }
        tp.AddRange(new[]
        {
            new Vector2(0.070f, 0.316f), new Vector2(0.083f, 0.311f), new Vector2(0.094f, 0.300f), new Vector2(0.101f, 0.282f),
            new Vector2(0.104f, 0.255f), new Vector2(0.100f, 0.225f), new Vector2(0.092f, 0.205f)
        });
        var tm = new List<string>();
        for (int i = 0; i < tp.Count - 1; i++) tm.Add(tp[i].y >= 0.306f && tp[i + 1].y >= 0.306f ? "lastikSirt" : "lastik");
        Revolve(tire, hub, ax, Vector3.up, tp, tm, 72);

        // Jant çemberi: iç bölüm koyu, dış dudak parlak
        var rp = new List<Vector2>
        {
            new Vector2(0.075f, 0.186f), new Vector2(-0.090f, 0.186f), new Vector2(-0.095f, 0.210f), new Vector2(0.085f, 0.210f),
            new Vector2(0.092f, 0.222f), new Vector2(0.100f, 0.215f), new Vector2(0.100f, 0.200f), new Vector2(0.094f, 0.189f),
            new Vector2(0.075f, 0.186f)
        };
        Revolve(rim, hub, ax, Vector3.up, rp, new[] { "jantIc", "jantIc", null, "jant", "jant", "jant", "jant", "jant" }, 64);

        // Göbek kapağı + amblem
        var hp = new List<Vector2> { new Vector2(0.050f, 0.075f), new Vector2(0.066f, 0.040f), new Vector2(0.074f, 0.030f), new Vector2(0.075f, 0.0f) };
        Revolve(rim, hub, ax, Vector3.up, hp, new[] { "jant", "jant", "jant" }, 32);
        var ep = new List<Vector2> { new Vector2(0.0765f, 0.022f), new Vector2(0.0765f, 0.0f) };
        Revolve(rim, hub, ax, Vector3.up, ep, new[] { "cerceve" }, 24);
        var er = new List<Vector2> { new Vector2(0.0755f, 0.026f), new Vector2(0.078f, 0.024f), new Vector2(0.078f, 0.020f) };
        Revolve(rim, hub, ax, Vector3.up, er, new[] { "krom", "krom" }, 24);

        // Bijonlar (4 × 98)
        for (int k = 0; k < 4; k++)
        {
            float a = (45f + 90f * k) * Mathf.Deg2Rad;
            var rad = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
            var c = hub + rad * 0.049f + ax * 0.064f;
            Blob(rim, "krom", c, ax * 0.006f, Vector3.up * 0.009f, Vector3.forward * 0.009f, 0.6f, 0.3f, 10);
        }

        // 5 çift kol: göbekten dudağa, hafif çanaklı
        for (int k = 0; k < 5; k++)
            foreach (float off in new[] { -5.5f, 5.5f })
            {
                float a = (k * 72f + off) * Mathf.Deg2Rad;
                var rad = new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a));
                var tan = new Vector3(0f, -Mathf.Sin(a), Mathf.Cos(a));
                float aOut = (k * 72f + off * 1.15f) * Mathf.Deg2Rad;
                var radOut = new Vector3(0f, Mathf.Cos(aOut), Mathf.Sin(aOut));
                var tanOut = new Vector3(0f, -Mathf.Sin(aOut), Mathf.Cos(aOut));
                Bar(rim, "jant", hub + rad * 0.066f + ax * 0.060f, hub + radOut * 0.192f + ax * 0.087f, (tan + tanOut).normalized, 0.030f, 0.026f, 0.026f, 0.018f);
            }

        // Fren diski (döner) + kaliper (dönmez)
        var dp = new List<Vector2>
        {
            new Vector2(-0.018f, 0.10f), new Vector2(-0.018f, 0.142f), new Vector2(-0.040f, 0.142f), new Vector2(-0.040f, 0.10f),
            new Vector2(-0.018f, 0.10f)
        };
        Revolve(rim, hub, ax, Vector3.up, dp, new[] { "disk", "disk", "disk", "disk" }, 40);
        var hubp = new List<Vector2> { new Vector2(0.03f, 0.10f), new Vector2(0.03f, 0.0f) };
        Revolve(rim, hub, ax, Vector3.up, hubp, new[] { "disk" }, 24);

        var cal = model.Get("Kaliper", grp);
        float ca = (front ? -50f : -40f) * Mathf.Deg2Rad;  // arka üst
        var cr = new Vector3(0f, Mathf.Cos(ca), Mathf.Sin(ca));
        var ct = new Vector3(0f, -Mathf.Sin(ca), Mathf.Cos(ca));
        Blob(cal, "kaliper", hub + cr * 0.128f - ax * 0.029f, ax * 0.026f, ct * 0.055f, cr * 0.03f, 0.4f, 0.4f, 14);
    }

    // ---------------- Yardımcılar ----------------
    static (Vector3, Vector3) AnchorOf(Frame f, Vector2 c)
    {
        if (!Surf(f, c, out var p, out var n)) { f(c, out var o, out var d); p = o + d; n = -d; }
        return (p, n);
    }

    static void Emblem(CarMesh m, Frame f, Vector2 c, float r, float off)
    {
        Decal(m, "krom", f, Circle(c.x, c.y, r, 32), off, 3, "krom", 0.004f);
        Decal(m, "cerceve", f, Circle(c.x, c.y, r * 0.8f, 32), off + 0.0015f, 3);
        Decal(m, "krom", f, Circle(c.x, c.y, r * 0.35f, 24), off + 0.0025f, 2);
    }

    /// Çokgeni ağırlık merkezinden dışa doğru 'd' kadar büyütür.
    static List<Vector2> Expand(IList<Vector2> poly, float d)
    {
        var c = Vector2.zero;
        foreach (var p in poly) c += p;
        c /= poly.Count;
        var l = new List<Vector2>();
        int n = poly.Count;
        for (int i = 0; i < n; i++)
        {
            var a = poly[(i - 1 + n) % n]; var p = poly[i]; var b = poly[(i + 1) % n];
            var e1 = (p - a).normalized; var e2 = (b - p).normalized;
            var n1 = new Vector2(e1.y, -e1.x); var n2 = new Vector2(e2.y, -e2.x);
            var nn = (n1 + n2).normalized;
            if (Vector2.Dot(nn, p - c) < 0f) nn = -nn;
            l.Add(p + nn * d);
        }
        return l;
    }

    /// Orta çizgi boyunca sabit kalınlıkta kapalı şerit çokgeni.
    static List<Vector2> Strip2(IList<Vector2> line, float halfW)
    {
        var top = new List<Vector2>();
        var bot = new List<Vector2>();
        for (int i = 0; i < line.Count; i++)
        {
            var t = (line[Mathf.Min(i + 1, line.Count - 1)] - line[Mathf.Max(i - 1, 0)]).normalized;
            var nn = new Vector2(-t.y, t.x);
            top.Add(line[i] + nn * halfW);
            bot.Add(line[i] - nn * halfW);
        }
        bot.Reverse();
        top.AddRange(bot);
        return top;
    }

    static List<Vector2> Reverse(List<Vector2> l) { var r = new List<Vector2>(l); r.Reverse(); return r; }
}

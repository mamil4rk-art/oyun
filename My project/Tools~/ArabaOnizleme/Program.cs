using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Yazılım çizici: EgeaGeometry çıktısını BMP olarak çizer (Unity olmadan şekil kontrolü).
static class Program
{
    record Mat(Color c, float spec, float alpha = 1f, bool twoSided = false);

    static readonly Dictionary<string, Mat> mats = new()
    {
        ["boya"] = new(new Color(0.55f, 0.04f, 0.05f), 0.9f),
        ["alt"] = new(new Color(0.03f, 0.03f, 0.03f), 0.1f),
        ["cam"] = new(new Color(0.06f, 0.08f, 0.09f), 1f, 0.6f, true),
        ["cerceve"] = new(new Color(0.02f, 0.02f, 0.02f), 0.9f),
        ["krom"] = new(new Color(0.8f, 0.8f, 0.82f), 1f),
        ["plastikMat"] = new(new Color(0.05f, 0.05f, 0.05f), 0.2f),
        ["petek"] = new(new Color(0.02f, 0.02f, 0.02f), 0.4f),
        ["farGovde"] = new(new Color(0.15f, 0.15f, 0.16f), 0.9f),
        ["farLens"] = new(new Color(0.9f, 0.95f, 1f), 1f, 0.12f),
        ["farDRL"] = new(new Color(0.95f, 0.95f, 0.95f), 0.5f),
        ["amber"] = new(new Color(0.9f, 0.45f, 0.05f), 0.6f),
        ["reflektor"] = new(new Color(0.85f, 0.85f, 0.88f), 1f),
        ["stopIc"] = new(new Color(0.25f, 0.02f, 0.02f), 0.8f),
        ["stopKirmizi"] = new(new Color(0.8f, 0.05f, 0.05f), 0.8f),
        ["stopBeyaz"] = new(new Color(0.75f, 0.75f, 0.78f), 0.9f),
        ["stopLens"] = new(new Color(0.6f, 0.03f, 0.04f), 1f, 0.45f),
        ["lastik"] = new(new Color(0.06f, 0.06f, 0.06f), 0.15f),
        ["lastikSirt"] = new(new Color(0.045f, 0.045f, 0.045f), 0.05f),
        ["jant"] = new(new Color(0.72f, 0.73f, 0.75f), 0.8f),
        ["jantIc"] = new(new Color(0.3f, 0.31f, 0.33f), 0.4f),
        ["disk"] = new(new Color(0.5f, 0.5f, 0.5f), 0.4f),
        ["kaliper"] = new(new Color(0.15f, 0.15f, 0.16f), 0.3f),
        ["aynaCam"] = new(new Color(0.7f, 0.75f, 0.8f), 1f),
        ["icPlastik"] = new(new Color(0.07f, 0.07f, 0.075f), 0.2f),
        ["torpido"] = new(new Color(0.08f, 0.08f, 0.085f), 0.2f),
        ["deri"] = new(new Color(0.06f, 0.06f, 0.06f), 0.3f),
        ["kumas"] = new(new Color(0.14f, 0.14f, 0.15f), 0.05f),
        ["tavan"] = new(new Color(0.55f, 0.55f, 0.53f), 0.05f),
        ["hali"] = new(new Color(0.08f, 0.08f, 0.08f), 0.05f),
        ["ekran"] = new(new Color(0.01f, 0.01f, 0.015f), 1f),
        ["conta"] = new(new Color(0.03f, 0.03f, 0.03f), 0.2f),
        ["aralik"] = new(new Color(0.02f, 0.0f, 0.0f), 0.0f),
    };

    static void Main(string[] args)
    {
        string outDir = args.Length > 0 ? args[0] : ".";
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var model = EgeaGeometry.Build();
        Console.WriteLine($"Build: {sw.ElapsedMilliseconds} ms");
        int tris = 0;
        foreach (var m in model.meshes)
        {
            Console.WriteLine($"  {m.group}/{m.name}: {m.v.Count} v, {m.TriCount} t");
            tris += m.TriCount;
            foreach (var k in m.tris.Keys) if (!mats.ContainsKey(k)) Console.WriteLine("   EKSİK MALZEME: " + k);
            foreach (var p in m.v) if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) { Console.WriteLine("   NaN!"); break; }
        }
        Console.WriteLine($"Toplam üçgen: {tris}");

        var views = new (string name, Vector3 eye, Vector3 target, float fov)[]
        {
            ("1_on_sol", new Vector3(-3.6f, 1.25f, 5.2f), new Vector3(0f, 0.65f, 0.3f), 34f),
            ("2_on_sag", new Vector3(4.4f, 1.0f, 3.6f), new Vector3(0f, 0.65f, 0.1f), 34f),
            ("3_arka", new Vector3(1.6f, 1.3f, -5.6f), new Vector3(0f, 0.75f, -0.8f), 34f),
            ("4_yan", new Vector3(7.5f, 0.8f, 0f), new Vector3(0f, 0.75f, 0f), 36f),
            ("5_on", new Vector3(0f, 0.85f, 7.5f), new Vector3(0f, 0.7f, 0f), 18f),
            ("6_arka_duz", new Vector3(0f, 1.0f, -7.5f), new Vector3(0f, 0.75f, 0f), 18f),
            ("7_ust", new Vector3(0.01f, 9f, 0f), new Vector3(0f, 0f, 0f), 32f),
            ("8_far_yakin", new Vector3(1.4f, 1.0f, 3.2f), new Vector3(0.45f, 0.75f, 2.1f), 30f),
            ("11_surucu", new Vector3(-0.37f, 1.17f, -0.08f), new Vector3(-0.37f, 0.98f, 0.8f), 62f),
            ("10_c_direk",new Vector3(3.2f, 1.25f, -1.4f), new Vector3(0.8f, 1.12f, -1.2f), 30f),
            ("9_jant",new Vector3(2.4f, 0.45f, 1.5f), new Vector3(0.8f, 0.33f, 1.35f), 28f),
        };
        foreach (var v in views)
        {
            var img = Render(model, v.eye, v.target, v.fov, 1000, 620);
            string path = Path.Combine(outDir, v.name + ".bmp");
            SaveBmp(path, img, 1000, 620);
        }
        Console.WriteLine($"Bitti: {sw.ElapsedMilliseconds} ms");
    }

    static Color[] Render(CarModel model, Vector3 eye, Vector3 target, float fov, int W, int H)
    {
        const int SS = 2;
        int w = W * SS, h = H * SS;
        var col = new Color[w * h];
        var zb = new float[w * h];
        for (int i = 0; i < zb.Length; i++) zb[i] = float.MaxValue;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                col[y * w + x] = Color.Lerp(new Color(0.78f, 0.84f, 0.9f), new Color(0.55f, 0.66f, 0.8f), y / (float)h);

        var f = (target - eye).normalized;
        var r = Vector3.Cross(Vector3.up, f).normalized;
        var u = Vector3.Cross(f, r);
        float focal = (h / 2f) / Mathf.Tan(fov * Mathf.Deg2Rad / 2f);
        var light = new Vector3(-0.45f, 0.8f, 0.4f).normalized;

        Vector3 Proj(Vector3 p)
        {
            var d = p - eye;
            float z = Vector3.Dot(d, f);
            return new Vector3(w / 2f + Vector3.Dot(d, r) / z * focal, h / 2f - Vector3.Dot(d, u) / z * focal, z);
        }

        // Zemin
        var g = new List<(Vector3, Vector3, Vector3, Vector3[], Mat)>();
        var gm = new Mat(new Color(0.42f, 0.42f, 0.42f), 0f);
        var G = new[] { new Vector3(-8, 0, -8), new Vector3(8, 0, -8), new Vector3(8, 0, 8), new Vector3(-8, 0, 8) };
        void Ground(Vector3 a, Vector3 b, Vector3 c) => DrawTri(a, b, c, Vector3.up, Vector3.up, Vector3.up, gm, false);

        var transparent = new List<(Vector3, Vector3, Vector3, Vector3, Vector3, Vector3, Mat)>();

        void DrawTri(Vector3 a, Vector3 b, Vector3 c, Vector3 na, Vector3 nb, Vector3 nc, Mat m, bool blend)
        {
            var A = Proj(a); var B = Proj(b); var C = Proj(c);
            if (A.z < 0.05f || B.z < 0.05f || C.z < 0.05f) return;
            int minX = Math.Max(0, (int)Math.Floor(Math.Min(A.x, Math.Min(B.x, C.x))));
            int maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(A.x, Math.Max(B.x, C.x))));
            int minY = Math.Max(0, (int)Math.Floor(Math.Min(A.y, Math.Min(B.y, C.y))));
            int maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(A.y, Math.Max(B.y, C.y))));
            float area = (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x);
            if (Math.Abs(area) < 1e-9f) return;
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    float px = x + 0.5f, py = y + 0.5f;
                    float w0 = ((B.x - px) * (C.y - py) - (B.y - py) * (C.x - px)) / area;
                    float w1 = ((C.x - px) * (A.y - py) - (C.y - py) * (A.x - px)) / area;
                    float w2 = 1f - w0 - w1;
                    if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                    // perspektif doğru derinlik
                    float iz = w0 / A.z + w1 / B.z + w2 / C.z;
                    float z = 1f / iz;
                    int idx = y * w + x;
                    if (z >= zb[idx]) continue;
                    var n = ((na * (w0 / A.z) + nb * (w1 / B.z) + nc * (w2 / C.z)) * z).normalized;
                    var p = (a * (w0 / A.z) + b * (w1 / B.z) + c * (w2 / C.z)) * z;
                    var v = (eye - p).normalized;
                    if (Vector3.Dot(n, v) < 0) n = -n;
                    float diff = Math.Max(0f, Vector3.Dot(n, light));
                    var hv = (light + v).normalized;
                    float sp = Mathf.Pow(Math.Max(0f, Vector3.Dot(n, hv)), 20f + 200f * m.spec) * m.spec;
                    // gökyüzü yansıması (parlak yüzeyler için)
                    var refl = Vector3.Reflect(-v, n);
                    float sky = Mathf.Clamp01(refl.y * 0.5f + 0.5f);
                    var env = Color.Lerp(new Color(0.35f, 0.35f, 0.35f), new Color(0.75f, 0.82f, 0.9f), sky);
                    float fres = 0.04f + 0.96f * Mathf.Pow(1f - Math.Max(0f, Vector3.Dot(n, v)), 5f);
                    var cc = m.c * (0.28f + 0.72f * diff) + Color.white * sp + env * (m.spec * (0.08f + 0.5f * fres));
                    cc.a = 1f;
                    if (blend)
                    {
                        col[idx] = Color.Lerp(col[idx], cc, m.alpha);
                    }
                    else
                    {
                        col[idx] = cc;
                        zb[idx] = z;
                    }
                }
        }

        for (float gx = -6f; gx < 6f; gx += 0.5f)
            for (float gz = -6f; gz < 6f; gz += 0.5f)
            {
                Vector3 a = new(gx, 0, gz), b = new(gx + 0.5f, 0, gz), c = new(gx + 0.5f, 0, gz + 0.5f), d = new(gx, 0, gz + 0.5f);
                Ground(a, b, c);
                Ground(a, c, d);
            }

        foreach (var cm in model.meshes)
        {
            foreach (var kv in cm.tris)
            {
                var m = mats.TryGetValue(kv.Key, out var mm) ? mm : new Mat(new Color(1f, 0f, 1f), 0f);
                var l = kv.Value;
                for (int i = 0; i < l.Count; i += 3)
                {
                    var a = cm.v[l[i]]; var b = cm.v[l[i + 1]]; var c = cm.v[l[i + 2]];
                    var fn = Vector3.Cross(b - a, c - a);
                    bool front = Vector3.Dot(fn, eye - a) > 0;
                    if (!front && !m.twoSided) continue;
                    if (m.alpha < 1f) { transparent.Add((a, b, c, cm.normals[l[i]], cm.normals[l[i + 1]], cm.normals[l[i + 2]], m)); continue; }
                    DrawTri(a, b, c, cm.normals[l[i]], cm.normals[l[i + 1]], cm.normals[l[i + 2]], m, false);
                }
            }
        }
        transparent.Sort((p, q) => Vector3.Dot(q.Item1 - eye, f).CompareTo(Vector3.Dot(p.Item1 - eye, f)));
        foreach (var t in transparent) DrawTri(t.Item1, t.Item2, t.Item3, t.Item4, t.Item5, t.Item6, t.Item7, true);

        var outc = new Color[W * H];
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                var s = new Color(0, 0, 0, 0);
                for (int dy = 0; dy < SS; dy++)
                    for (int dx = 0; dx < SS; dx++) s += col[(y * SS + dy) * w + x * SS + dx];
                outc[y * W + x] = s / (SS * SS);
            }
        return outc;
    }

    static void SaveBmp(string path, Color[] px, int W, int H)
    {
        int rowSize = (W * 3 + 3) & ~3;
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write((byte)'B'); bw.Write((byte)'M');
        bw.Write(54 + rowSize * H); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(W); bw.Write(H); bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(rowSize * H);
        bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        var row = new byte[rowSize];
        for (int y = H - 1; y >= 0; y--)
        {
            for (int x = 0; x < W; x++)
            {
                var c = px[y * W + x];
                // basit ton eşleme + gama
                float Tm(float v) => Mathf.Pow(Mathf.Clamp01(v), 1f / 2.2f);
                row[x * 3] = (byte)(Tm(c.b) * 255);
                row[x * 3 + 1] = (byte)(Tm(c.g) * 255);
                row[x * 3 + 2] = (byte)(Tm(c.r) * 255);
            }
            bw.Write(row);
        }
    }
}

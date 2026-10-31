#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Gerçekçi eşya yapımı için ortak araçlar:
///   - RBox: kenarları yuvarlatılmış kutu (gerçek eşyada keskin küp kenarı olmaz; pah ışığı yakalar)
///   - Pbr: parlaklık / metaliklik / kabartma (normal) haritası ayarlı malzeme
///   - Renk + kabartma doku çiftleri (parke, tuğla, ahşap, lamel duvar, keçe, fırçalı metal…)
///   - Gölgeli ışık ve yansıma probu
/// </summary>
public static partial class PrototypeSceneBuilder
{
    static readonly Dictionary<string, Mesh> roundedMeshCache = new Dictionary<string, Mesh>();

    // =====================================================================
    // YUVARLATILMIŞ KUTU
    // =====================================================================

    /// Kenar ve köşeleri 'radius' yarıçapıyla yuvarlatılmış kutu. UV'ler metre cinsinden düzlemsel
    /// (uvMeters'te bir tekrar), normaller yumuşak; normal haritası için tanjantlar hesaplanır.
    static Mesh RoundedBoxMesh(Vector3 size, float radius, float uvMeters, int seg = 3)
    {
        float minHalf = Mathf.Min(size.x, Mathf.Min(size.y, size.z)) / 2f;
        float r = Mathf.Clamp(radius, 0f, minHalf * 0.98f);
        string key = $"{size.x:F4}|{size.y:F4}|{size.z:F4}|{r:F4}|{uvMeters:F3}|{seg}";
        if (roundedMeshCache.TryGetValue(key, out var cached) && cached != null) return cached;

        Vector3 h = size / 2f;
        Vector3 inner = h - Vector3.one * r;
        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        float[] Samples(float hh)
        {
            var s = new List<float>();
            if (r <= 0f) { s.Add(-hh); s.Add(hh); return s.ToArray(); }
            for (int i = 0; i <= seg; i++) s.Add(-hh + r * i / seg);
            for (int i = 0; i <= seg; i++) s.Add(hh - r + r * i / seg);
            return s.ToArray();
        }

        for (int a = 0; a < 3; a++)
        foreach (int sign in new[] { -1, 1 })
        {
            int u = (a + 1) % 3, v = (a + 2) % 3;
            float[] su = Samples(h[u]), sv = Samples(h[v]);
            int nu = su.Length, nv = sv.Length, start = verts.Count;
            var axisN = Vector3.zero;
            axisN[a] = sign;

            for (int j = 0; j < nv; j++)
            for (int i = 0; i < nu; i++)
            {
                var p = Vector3.zero;
                p[a] = sign * h[a];
                p[u] = su[i];
                p[v] = sv[j];
                var ic = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                Vector3 d = p - ic;
                Vector3 n = r > 0f && d.sqrMagnitude > 1e-12f ? d.normalized : axisN;
                Vector3 pos = r > 0f ? ic + n * r : p;
                verts.Add(pos);
                norms.Add(n);
                uvs.Add(a == 2 ? new Vector2(pos.x, pos.y) / uvMeters
                      : a == 0 ? new Vector2(pos.z, pos.y) / uvMeters
                      : new Vector2(pos.x, pos.z) / uvMeters);
            }

            for (int j = 0; j < nv - 1; j++)
            for (int i = 0; i < nu - 1; i++)
            {
                int i00 = start + j * nu + i, i10 = i00 + 1, i01 = i00 + nu, i11 = i01 + 1;
                // u × v = +a ekseni: +yüzde (00,10,11) sırası dışa bakar
                if (sign > 0) { tris.Add(i00); tris.Add(i10); tris.Add(i11); tris.Add(i00); tris.Add(i11); tris.Add(i01); }
                else { tris.Add(i00); tris.Add(i11); tris.Add(i10); tris.Add(i00); tris.Add(i01); tris.Add(i11); }
            }
        }

        var mesh = new Mesh { name = "Yuvarlak Kutu " + key };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        roundedMeshCache[key] = mesh;
        return mesh;
    }

    /// Ebeveyne göre yerel, kenarları yuvarlatılmış kutu.
    static GameObject RBox(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
        float radius = 0.01f, bool collider = false, float uvMeters = 1f, Vector3? euler = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.AddComponent<MeshFilter>().sharedMesh = RoundedBoxMesh(size, radius, uvMeters);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    /// Aynı mesh'in birçok kopyasını tek mesh'te birleştirir (yüzlerce tuş, çıta, yaprak = tek çizim).
    static GameObject CombinedMeshes(Transform parent, string name, Mesh mesh, List<Matrix4x4> transforms, Material mat)
    {
        if (transforms.Count == 0) return null;
        var ci = new CombineInstance[transforms.Count];
        for (int i = 0; i < ci.Length; i++) ci[i] = new CombineInstance { mesh = mesh, transform = transforms[i] };
        var combined = new Mesh { name = name };
        if (transforms.Count * mesh.vertexCount > 65000) combined.indexFormat = IndexFormat.UInt32;
        combined.CombineMeshes(ci, true, true);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = combined;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    static Mesh sphereMesh, cylinderMesh;

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var tmp = GameObject.CreatePrimitive(type);
        var m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(tmp);
        return m;
    }

    static Mesh SphereMesh => sphereMesh != null ? sphereMesh : (sphereMesh = PrimitiveMesh(PrimitiveType.Sphere));
    static Mesh CylinderMesh => cylinderMesh != null ? cylinderMesh : (cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder));

    // =====================================================================
    // MALZEMELER
    // =====================================================================

    /// Fiziksel tabanlı malzeme: renk, parlaklık (smoothness), metaliklik, isteğe bağlı doku ve kabartma haritası.
    /// Aynı anahtar her kurulumda yeniden ayarlanır.
    static Material Pbr(string key, Color color, float smoothness, float metallic = 0f, Texture2D albedo = null, Texture2D normal = null, float normalScale = 1f)
    {
        var m = Mat(key, color);
        m.color = color;
        m.mainTexture = albedo;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        if (m.HasProperty("_BumpMap"))
        {
            m.SetTexture("_BumpMap", normal);
            if (normal != null)
            {
                m.SetFloat("_BumpScale", normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            else m.DisableKeyword("_NORMALMAP");
        }
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PbrGlow(string key, Color color, float intensity, float smoothness = 0.8f)
    {
        var m = Pbr(key, color, smoothness);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", color * intensity);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(m);
        return m;
    }

    // Sık kullanılan malzemeler
    static Material MChrome => Pbr("P_Krom", new Color(0.86f, 0.87f, 0.89f), 0.92f, 1f);
    static Material MBrushed => Pbr("P_FircaliAlu", new Color(0.82f, 0.83f, 0.85f), 0.65f, 1f, BrushedMetalPair().color, BrushedMetalPair().normal, 0.3f);
    static Material MBlackMetal => Pbr("P_SiyahBoyaMetal", new Color(0.06f, 0.06f, 0.065f), 0.5f, 0.55f);
    static Material MBlackPlastic => Pbr("P_SiyahPlastik", new Color(0.035f, 0.035f, 0.04f), 0.6f);
    static Material MMattePlastic => Pbr("P_MatPlastik", new Color(0.05f, 0.05f, 0.055f), 0.3f);
    static Material MGrayPlastic => Pbr("P_GriPlastik", new Color(0.33f, 0.34f, 0.36f), 0.45f);
    static Material MWhitePlastic => Pbr("P_BeyazPlastik", new Color(0.92f, 0.92f, 0.91f), 0.55f);
    static Material MMelamine => Pbr("P_Melamin", new Color(0.93f, 0.93f, 0.91f), 0.45f);
    static Material MOak => Pbr("P_Mese", new Color(0.92f, 0.8f, 0.66f), 0.38f, 0f, WoodPair().color, WoodPair().normal, 0.5f);
    static Material MWalnut => Pbr("P_Ceviz", new Color(0.55f, 0.4f, 0.3f), 0.42f, 0f, WoodPair().color, WoodPair().normal, 0.5f);
    static Material MQuartz => Pbr("P_Kuvars", new Color(0.93f, 0.92f, 0.89f), 0.8f, 0f, QuartzPair().color, QuartzPair().normal, 0.15f);
    static Material MLeather => Pbr("P_SuniDeri", new Color(0.07f, 0.07f, 0.075f), 0.42f, 0f, FeltPair().color, FeltPair().normal, 0.35f);
    static Material MRubber => Pbr("P_Kaucuk", new Color(0.04f, 0.04f, 0.04f), 0.15f);
    static Material MGlassClean => GlassMat();

    // =====================================================================
    // IŞIK
    // =====================================================================

    /// Gerçekçi tavan ışığı: 4000 K sıcak beyaz, isteğe bağlı yumuşak gölge.
    static Light ShopLight(Transform parent, Vector3 localPos, float range, float intensity, bool shadows)
    {
        var go = new GameObject("Işık");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = range;
        l.intensity = intensity;
        l.color = new Color(1f, 0.93f, 0.84f);
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        l.shadowStrength = 0.75f;
        l.shadowBias = 0.03f;
        l.shadowNormalBias = 0.3f;
        return l;
    }

    /// Kutu izdüşümlü gerçek zamanlı yansıma probu (krom, cam ve parlak yüzeyler odayı yansıtsın).
    static void ReflectionProbeBox(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        var p = go.AddComponent<ReflectionProbe>();
        p.mode = ReflectionProbeMode.Realtime;
        p.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        p.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        p.size = size;
        p.boxProjection = true;
        p.resolution = 128;
        p.importance = 1;
        p.blendDistance = 0.5f;
    }

    // =====================================================================
    // DOKU ÇİFTLERİ (renk + kabartma)
    // =====================================================================

    static readonly Dictionary<string, (Texture2D color, Texture2D normal)> pairCache = new Dictionary<string, (Texture2D, Texture2D)>();

    /// Renk ve yükseklik birlikte üretilir; yükseklikten normal haritası çıkarılır.
    /// İki dosya da varsa yeniden üretilmez (kendi dokunu aynı isimle koyabilirsin).
    static (Texture2D color, Texture2D normal) LoadOrCreatePair(string name, int size, float strength, System.Action<Color[], float[]> generate)
    {
        if (pairCache.TryGetValue(name, out var c) && c.color != null && c.normal != null) return c;
        EnsureFolder(TexturesFolder);
        string cPath = $"{TexturesFolder}/{name}.png", nPath = $"{TexturesFolder}/{name}_N.png";
        var color = AssetDatabase.LoadAssetAtPath<Texture2D>(cPath);
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(nPath);
        if (color == null || normal == null)
        {
            var col = new Color[size * size];
            var hgt = new float[size * size];
            generate(col, hgt);
            color = SavePng(cPath, size, col, false, false);
            normal = SavePng(nPath, size, HeightToNormal(hgt, size, strength), false, true);
        }
        pairCache[name] = (color, normal);
        return (color, normal);
    }

    static Color[] HeightToNormal(float[] h, int size, float strength)
    {
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float l = h[y * size + (x - 1 + size) % size], r = h[y * size + (x + 1) % size];
                float d = h[((y - 1 + size) % size) * size + x], u = h[((y + 1) % size) * size + x];
                var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f);
            }
        return px;
    }

    static float Rand(System.Random r) => (float)r.NextDouble();

    /// Laminat parke (20 cm şaşırtmalı tahtalar, derzler oyuk, damar). 512 px = 2 m.
    static (Texture2D color, Texture2D normal) LaminatePair() => LoadOrCreatePair("T_Laminat2", 512, 2.5f, (px, hg) =>
    {
        const int S = 512, RowH = 51;
        var rng = new System.Random(31);
        Color oakA = new Color(0.66f, 0.49f, 0.33f), oakB = new Color(0.52f, 0.37f, 0.24f);
        int rows = S / RowH + 1;
        for (int r = 0; r < rows; r++)
        {
            var cuts = new List<int>();
            int off = rng.Next(0, S), x = 0;
            while (x < S) { cuts.Add((x + off) % S); x += rng.Next(240, 380); }
            var shade = new Color[cuts.Count];
            var phase = new float[cuts.Count];
            for (int i = 0; i < cuts.Count; i++) { shade[i] = Color.Lerp(oakA, oakB, Rand(rng)); phase[i] = Rand(rng) * 10f; }

            for (int y = r * RowH; y < Mathf.Min((r + 1) * RowH, S); y++)
            {
                int dy = y - r * RowH;
                for (int xx = 0; xx < S; xx++)
                {
                    int best = 0, bestDist = int.MaxValue;
                    for (int i = 0; i < cuts.Count; i++)
                    {
                        int d = (xx - cuts[i] + S) % S;
                        if (d < bestDist) { bestDist = d; best = i; }
                    }
                    int toEnd = int.MaxValue;
                    foreach (int cu in cuts) toEnd = Mathf.Min(toEnd, (cu - xx + S) % S);
                    float grainWave = Mathf.Sin(xx * 0.045f + phase[best] + Mathf.Sin(dy * 0.31f + phase[best]) * 1.6f);
                    float fine = Rand(rng) - 0.5f;
                    float grain = grainWave * 0.035f + fine * 0.025f;
                    Color c = shade[best];
                    c = new Color(c.r + grain, c.g + grain * 0.8f, c.b + grain * 0.6f);

                    // Derz: tahta kenarlarında 1-2 px oyuk ve hafif pah
                    int edge = Mathf.Min(Mathf.Min(bestDist, toEnd), Mathf.Min(dy, RowH - 1 - dy));
                    float hgt = edge == 0 ? 0f : edge == 1 ? 0.55f : 1f;
                    if (edge == 0) c *= 0.55f;
                    else if (edge == 1) c *= 0.85f;
                    hg[y * S + xx] = hgt + grainWave * 0.04f + fine * 0.02f;
                    px[y * S + xx] = c;
                }
            }
        }
    });

    /// Tuğla (25 × 6.25 cm, harç girintili). 256 px = 1 m.
    static (Texture2D color, Texture2D normal) BrickPair() => LoadOrCreatePair("T_Tugla2", 256, 4f, (px, hg) =>
    {
        const int S = 256, RowH = 16, BrickW = 64, Mortar = 2;
        var rng = new System.Random(17);
        var shade = new Color[S / RowH, S / BrickW + 1];
        for (int r = 0; r < S / RowH; r++)
            for (int b = 0; b <= S / BrickW; b++)
            {
                shade[r, b] = Color.Lerp(new Color(0.6f, 0.27f, 0.18f), new Color(0.45f, 0.19f, 0.13f), Rand(rng));
                if (rng.NextDouble() < 0.1) shade[r, b] = new Color(0.66f, 0.38f, 0.26f);
            }
        for (int y = 0; y < S; y++)
        {
            int r = y / RowH, dy = y % RowH, off = (r % 2) * (BrickW / 2);
            for (int x = 0; x < S; x++)
            {
                int bx = (x + off) % S, b = bx / BrickW, dx = bx % BrickW;
                bool mortar = dy < Mortar || dx < Mortar;
                float n = Rand(rng) - 0.5f;
                if (mortar)
                {
                    float m = 0.72f + n * 0.06f;
                    px[y * S + x] = new Color(m, m * 0.97f, m * 0.92f);
                    hg[y * S + x] = 0.1f + n * 0.05f;
                }
                else
                {
                    var c = shade[r, b];
                    px[y * S + x] = new Color(c.r + n * 0.06f, c.g + n * 0.05f, c.b + n * 0.04f);
                    int e = Mathf.Min(Mathf.Min(dy - Mortar, RowH - 1 - dy), Mathf.Min(dx - Mortar, BrickW - 1 - dx));
                    hg[y * S + x] = Mathf.Min(1f, 0.6f + e * 0.2f) + n * 0.08f;
                }
            }
        }
    });

    /// Asma tavan karoları (60 cm, kabartmalı T-profil, benekli yüzey). 256 px = 1.2 m.
    static (Texture2D color, Texture2D normal) CeilingPair() => LoadOrCreatePair("T_AsmaTavan2", 256, 3f, (px, hg) =>
    {
        const int S = 256, Tile = 128;
        var rng = new System.Random(13);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int ex = Mathf.Min(x % Tile, Tile - 1 - x % Tile), ey = Mathf.Min(y % Tile, Tile - 1 - y % Tile);
                int e = Mathf.Min(ex, ey);
                float v, h;
                if (e < 2) { v = 0.97f; h = 1f; }            // T-profil
                else if (e < 4) { v = 0.8f; h = 0.3f; }       // profil ile karo arasındaki gölge çizgisi
                else
                {
                    v = 0.88f + (Rand(rng) - 0.5f) * 0.03f;
                    h = 0.6f;
                    if (rng.NextDouble() < 0.05) { v -= 0.1f; h = 0.45f; } // delikli akustik yüzey
                }
                px[y * S + x] = new Color(v, v, v * 0.98f);
                hg[y * S + x] = h;
            }
    });

    /// Meşe ahşap (mobilya): damar dalgaları, yıllık halka çizgileri. 512 px = 1 m. Açık renk; ton malzemeden.
    static (Texture2D color, Texture2D normal) WoodPair() => LoadOrCreatePair("T_Ahsap", 512, 1.2f, (px, hg) =>
    {
        const int S = 512;
        var rng = new System.Random(41);
        var warp = NoiseGrid(rng, 8);
        var tone = NoiseGrid(rng, 4);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float u = x / (float)S, v = y / (float)S;
                float w = SampleNoise(warp, 8, u, v);
                float ring = Mathf.Sin((v * 28f + w * 3.5f) * Mathf.PI * 2f);      // boyuna damarlar (x boyunca uzanır)
                float streak = Mathf.Pow(Mathf.Abs(ring), 6f);
                float fine = Rand(rng) - 0.5f;
                float t = 0.78f + (SampleNoise(tone, 4, u, v) - 0.5f) * 0.12f - streak * 0.18f + fine * 0.04f;
                px[y * S + x] = new Color(t, t * 0.86f, t * 0.7f);
                hg[y * S + x] = 1f - streak * 0.5f + fine * 0.05f;
            }
    });

    /// Lamel duvar paneli: 15 cm aralıklı yatay oluklar (raf arkalıkları). 256 px = 0.6 m.
    static (Texture2D color, Texture2D normal) SlatwallPair() => LoadOrCreatePair("T_Lamel", 256, 4f, (px, hg) =>
    {
        const int S = 256, Step = 64;
        var rng = new System.Random(7);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int d = y % Step;
                float v = 0.95f + (Rand(rng) - 0.5f) * 0.015f, h = 1f;
                if (d < 7) { v = 0.55f; h = 0.1f; }          // oluk içi
                else if (d < 9) { v = 0.8f; h = 0.6f; }      // oluk kenarı
                px[y * S + x] = new Color(v, v, v * 0.99f);
                hg[y * S + x] = h;
            }
    });

    /// Keçe / halı / suni deri yüzey: ince lifli gürültü. 256 px = 0.5 m. Gri; ton malzemeden.
    static (Texture2D color, Texture2D normal) FeltPair() => LoadOrCreatePair("T_Kece", 256, 0.8f, (px, hg) =>
    {
        const int S = 256;
        var rng = new System.Random(19);
        var blot = NoiseGrid(rng, 16);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = Rand(rng);
                float b = SampleNoise(blot, 16, x / (float)S, y / (float)S);
                float v = 0.85f + (n - 0.5f) * 0.18f + (b - 0.5f) * 0.1f;
                px[y * S + x] = new Color(v, v, v);
                hg[y * S + x] = n * 0.6f + b * 0.4f;
            }
    });

    /// Fırçalı alüminyum: yatay çizik izleri. 256 px = 0.5 m.
    static (Texture2D color, Texture2D normal) BrushedMetalPair() => LoadOrCreatePair("T_FircaliMetal", 256, 0.6f, (px, hg) =>
    {
        const int S = 256;
        var rng = new System.Random(23);
        var rowNoise = new float[S];
        for (int y = 0; y < S; y++) rowNoise[y] = Rand(rng);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float n = rowNoise[y] * 0.7f + Rand(rng) * 0.3f;
                float v = 0.86f + (n - 0.5f) * 0.12f;
                px[y * S + x] = new Color(v, v, v);
                hg[y * S + x] = n;
            }
    });

    /// Kuvars tezgâh: çok ince benekli taş. 256 px = 0.6 m.
    static (Texture2D color, Texture2D normal) QuartzPair() => LoadOrCreatePair("T_Kuvars", 256, 0.3f, (px, hg) =>
    {
        const int S = 256;
        var rng = new System.Random(29);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float v = 0.96f + (Rand(rng) - 0.5f) * 0.02f;
                double s = rng.NextDouble();
                if (s < 0.012) v -= 0.25f;
                else if (s < 0.03) v -= 0.08f;
                px[y * S + x] = new Color(v, v * 0.99f, v * 0.97f);
                hg[y * S + x] = v;
            }
    });

    /// Delikli alet panosu (6 cm aralıklı, gerçekten oyuk delikler). 128 px = 0.5 m.
    static (Texture2D color, Texture2D normal) PegboardPair() => LoadOrCreatePair("T_DelikliPano2", 128, 3f, (px, hg) =>
    {
        const int S = 128, Step = 16;
        var rng = new System.Random(29);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int dx = x % Step - Step / 2, dy = y % Step - Step / 2;
                float r2 = dx * dx + dy * dy;
                float v = 0.72f + (Rand(rng) - 0.5f) * 0.05f;
                Color c = new Color(v, v * 0.84f, v * 0.6f);
                float h = 1f;
                if (r2 <= 4f) { c = new Color(0.1f, 0.08f, 0.06f); h = 0f; }
                else if (r2 <= 7f) { c *= 0.8f; h = 0.5f; }
                px[y * S + x] = c;
                hg[y * S + x] = h;
            }
    });
}
#endif

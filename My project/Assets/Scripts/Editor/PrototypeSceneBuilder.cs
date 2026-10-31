#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Üst menüde "PC Shop > Prototip Sahnesini Kur" komutunu ekler.
///
/// DÜKKÂN PLANI (üstten, ön cephe caddeye bakar):
///
///            CADDE / KALDIRIM
///   ┌──────[vitrin]──[GİRİŞ]──[vitrin]──────┐  z = +6
///   │  PC     KONSOL                 KASA   │
///   │  RAFI   RAFI       SATIŞ ALANI        │
///   ├────────[kapı]───────────┬─────────────┤  z = 0
///   │ PC      TAMİRHANE       │    DEPO     │
///   │ masası                [kapı]  rafları │
///   │   tezgâhlar             │             │
///   └───────────[ARKA KAPI]───┴─────────────┘  z = -6
///              ARKA OTOPARK (araba)
///
/// Çevredeki şehir (yollar, binalar, taş yaya yolları): PrototypeSceneBuilder.City.cs
/// Toptancı mağazası (çevre yolunun kuzey dışında): PrototypeSceneBuilder.Wholesale.cs
/// </summary>
public static partial class PrototypeSceneBuilder
{
    const string ItemsFolder = "Assets/Data/Items";
    const string MaterialsFolder = "Assets/Data/Materials";
    const string ScenePath = "Assets/Scenes/PrototypeScene.unity";
    const string TextShaderPath = "Assets/Scripts/Shaders/Text3D.shader";

    // Dükkân ölçüleri (metre)
    const float WallH = 3.2f;   // duvar yüksekliği
    const float WallT = 0.2f;   // duvar kalınlığı
    const float FloorY = 0.1f;  // iç zemin üst yüzeyi
    const float MinX = -8f, MaxX = 8f, MinZ = -6f, MaxZ = 6f;
    const float SplitZ = 0f;    // satış alanı ↔ arka odalar duvarı
    const float DepoX = 3f;     // tamirhane ↔ depo duvarı

    struct Opening
    {
        public float center, width, bottom, top;
        public bool glass;

        public static Opening Door(float center, float width = 1.4f, float height = 2.4f)
            => new Opening { center = center, width = width, bottom = 0f, top = height };

        public static Opening Window(float center, float width, float bottom = 1.0f, float top = 2.6f)
            => new Opening { center = center, width = width, bottom = bottom, top = top, glass = true };
    }

    static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
    static Shader textShader;
    static Font signFont;

    [MenuItem("PC Shop/Prototip Sahnesini Kur")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        matCache.Clear();
        roundedMeshCache.Clear();
        pairCache.Clear();
        textShader = AssetDatabase.LoadAssetAtPath<Shader>(TextShaderPath);
        signFont = LoadBuiltinFont();

        var items = CreateSampleItems();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var defaultCam = GameObject.Find("Main Camera");
        if (defaultCam != null) Object.DestroyImmediate(defaultCam);
        var sun = Object.FindAnyObjectByType<Light>();
        if (sun != null) sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        var env = new GameObject("_Çevre").transform;
        BuildGround(env);
        BuildCity(env);

        BuildShop(); // → PrototypeSceneBuilder.Shop.cs

        BuildBackLot(env);
        BuildWholesaleStore(env, items);

        var systems = new GameObject("_OyunSistemleri");
        systems.AddComponent<MoneyManager>();
        systems.AddComponent<GameHUD>();

        BuildPlayer(new Vector3(0f, FloorY + 0.05f, 4.3f), 180f);

        EnsureFolder("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("PC Shop: Dükkân sahnesi hazır → " + ScenePath + "  (Play'e basıp deneyebilirsin)");
    }

    // =====================================================================
    // ÇEVRE
    // =====================================================================

    static void BuildGround(Transform parent)
    {
        var grass = GameObject.CreatePrimitive(PrimitiveType.Plane);
        grass.name = "Zemin (çim)";
        grass.transform.SetParent(parent, false);
        grass.transform.localScale = new Vector3(26f, 1f, 26f); // 260 m: şehrin tamamı
        Paint(grass, "Cim", new Color(0.33f, 0.45f, 0.27f));
        // Cadde ve dükkân önündeki kaldırım artık şehir yol ağının parçası → City.cs
    }

    static void BuildBackLot(Transform parent)
    {
        Block(parent, "Arka Otopark", new Vector3(0f, 0.01f, -12f), new Vector3(22f, 0.02f, 11.8f), Mat("Asfalt", new Color(0.16f, 0.16f, 0.17f)));

        // Arka kapının önünde bekleyen araba: burnu otoparka (-z), bagajı dükkânın arka kapısına (+z) bakar.
        // Model ve malzemeler → PrototypeSceneBuilder.Car.cs
        BuildCar(parent, new Vector3(1.2f, 0f, -10.6f), 180f);
    }

    // =====================================================================
    // OYUNCU
    // =====================================================================

    static void BuildPlayer(Vector3 pos, float yaw)
    {
        var player = new GameObject("Oyuncu");
        player.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0f, 0.9f, 0f);
        cc.stepOffset = 0.3f;

        var camGo = new GameObject("Kamera");
        camGo.tag = "MainCamera";
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0f, 1.65f, 0f);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();

        var hold = new GameObject("TutmaNoktası");
        hold.transform.SetParent(camGo.transform, false);
        hold.transform.localPosition = new Vector3(0f, -0.35f, 0.85f);

        var fpc = player.AddComponent<FirstPersonController>();
        fpc.cameraRoot = camGo.transform;

        var interactor = player.AddComponent<PlayerInteractor>();
        interactor.cam = cam;
        interactor.holdPoint = hold.transform;
    }

    // =====================================================================
    // YAPI YARDIMCILARI
    // =====================================================================

    /// Dünya koordinatında kutu.
    static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// Ebeveyne göre yerel koordinatta kutu (ebeveynin ölçeği 1 olmalı).
    static GameObject LocalBlock(Transform parent, string name, Vector3 localCenter, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localCenter;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// cols × rows yuva ızgarası (ebeveynin ölçeği 1 olmalı).
    static void AddSlotGrid(Transform parent, string baseName, float height, int cols, int rows, float spacingX, float spacingZ)
    {
        int n = 1;
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            float px = (c - (cols - 1) / 2f) * spacingX;
            float pz = (r - (rows - 1) / 2f) * spacingZ;
            CreateSlot(parent, $"{baseName} {n++}", new Vector3(px, height, pz));
        }
    }

    static PlacementSlot CreateSlot(Transform parent, string name, Vector3 localPos)
    {
        var go = new GameObject("Yuva - " + name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var bc = go.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.size = new Vector3(0.52f, 0.35f, 0.52f);
        bc.center = new Vector3(0f, 0.175f, 0f);
        var slot = go.AddComponent<PlacementSlot>();
        slot.slotName = name;
        return slot;
    }

    // =====================================================================
    // TABELA YAZILARI
    // =====================================================================

    /// pos: dünya koordinatı. yaw: yazıyı okuyan kişinin baktığı yön (derece). 0 = +z'ye bakarken okunur.
    static GameObject Label(Transform parent, string text, Vector3 pos, float yaw, float size = 1f, bool withBoard = true,
        Color? textColor = null, Color? boardColor = null)
    {
        var go = new GameObject("Tabela - " + text);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = 0.035f * size;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = textColor ?? Color.white;
        if (signFont != null)
        {
            tm.font = signFont;
            go.GetComponent<MeshRenderer>().sharedMaterial = signFont.material;
        }
        if (textShader != null) go.AddComponent<SignText>().SetShader(textShader);

        if (withBoard)
        {
            var board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Pano";
            board.transform.SetParent(go.transform, false);
            board.transform.localPosition = new Vector3(0f, 0f, 0.03f);
            board.transform.localScale = new Vector3(0.14f * size * text.Length + 0.2f, 0.36f * size, 0.02f);
            board.GetComponent<Renderer>().sharedMaterial = boardColor.HasValue
                ? Mat("Pano_" + ColorUtility.ToHtmlStringRGB(boardColor.Value), boardColor.Value)
                : Mat("TabelaPano", new Color(0.12f, 0.25f, 0.45f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
        }
        return go;
    }

    static Font LoadBuiltinFont()
    {
        foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            try
            {
                var f = Resources.GetBuiltinResource<Font>(name);
                if (f != null) return f;
            }
            catch { /* bu sürümde bu isimde font yok, diğerini dene */ }
        }
        return null;
    }

    // =====================================================================
    // KUTULAR VE ÖRNEK VERİ
    // Marka adları kurgusal (telif / marka sorunu olmasın diye).
    // =====================================================================

    static void SpawnBox(ItemData item, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Kutu - " + item.displayName;
        go.transform.position = pos;
        go.transform.localScale = item.boxSize;
        go.GetComponent<Renderer>().sharedMaterial = Mat("Kutu_" + item.itemId, item.boxColor);

        var box = go.AddComponent<ItemBox>(); // Rigidbody otomatik eklenir
        box.Setup(item);
        go.GetComponent<Rigidbody>().mass = item.mass;
    }

    static List<ItemData> CreateSampleItems()
    {
        EnsureFolder(ItemsFolder);
        var list = new List<ItemData>
        {
            Comp("cpu_r5", "Voltrix R5 İşlemci", ComponentType.CPU, 4500, 5800, new Vector3(0.14f, 0.07f, 0.14f), new Color(0.9f, 0.45f, 0.15f),
                c => { c.socket = SocketType.S100; c.powerDraw = 65; c.performanceScore = 60; }),
            Comp("cpu_r7", "Voltrix R7 İşlemci", ComponentType.CPU, 8500, 10500, new Vector3(0.14f, 0.07f, 0.14f), new Color(0.85f, 0.25f, 0.1f),
                c => { c.socket = SocketType.S100; c.powerDraw = 105; c.performanceScore = 80; }),
            Comp("mb_b60", "Kronos B60 Anakart", ComponentType.Motherboard, 3200, 4200, new Vector3(0.34f, 0.08f, 0.3f), new Color(0.2f, 0.25f, 0.5f),
                c => { c.socket = SocketType.S100; c.memoryType = MemoryType.DDR5; c.powerDraw = 30; }),
            Comp("ram_16", "MemoCore 16GB DDR5", ComponentType.RAM, 1400, 1900, new Vector3(0.16f, 0.05f, 0.08f), new Color(0.15f, 0.6f, 0.3f),
                c => { c.memoryType = MemoryType.DDR5; c.powerDraw = 5; c.performanceScore = 10; }),
            Comp("gpu_gx460", "Gryphon GX460 Ekran Kartı", ComponentType.GPU, 11000, 13500, new Vector3(0.38f, 0.12f, 0.2f), new Color(0.1f, 0.1f, 0.12f),
                c => { c.powerDraw = 160; c.performanceScore = 70; }),
            Comp("psu_650", "Ampera 650W Güç Kaynağı", ComponentType.PSU, 1800, 2400, new Vector3(0.22f, 0.15f, 0.2f), new Color(0.8f, 0.75f, 0.2f),
                c => { c.psuWattage = 650; }),
            Comp("ssd_1tb", "Fluxdisk 1TB NVMe SSD", ComponentType.Storage, 1500, 2000, new Vector3(0.14f, 0.04f, 0.1f), new Color(0.5f, 0.2f, 0.6f),
                c => { c.powerDraw = 7; c.performanceScore = 15; }),
            Comp("case_k1", "Kasa Pro K1", ComponentType.Case, 1600, 2200, new Vector3(0.5f, 0.5f, 0.25f), new Color(0.76f, 0.6f, 0.42f),
                c => { }),
            Item("con_pz5", "PlayZone 5 Konsol", ItemCategory.Console, 22000, 26000, new Vector3(0.45f, 0.15f, 0.35f), new Color(0.95f, 0.95f, 0.97f)),
            Item("con_pz5pro", "PlayZone 5 Pro Konsol", ItemCategory.Console, 32000, 37500, new Vector3(0.45f, 0.16f, 0.36f), new Color(0.7f, 0.72f, 0.8f)),
        };
        AssetDatabase.SaveAssets();
        return list;
    }

    static ComponentData Comp(string id, string name, ComponentType type, int buy, int market, Vector3 size, Color color, System.Action<ComponentData> extra)
    {
        string path = $"{ItemsFolder}/{id}.asset";
        var a = AssetDatabase.LoadAssetAtPath<ComponentData>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<ComponentData>();
        a.itemId = id; a.displayName = name; a.componentType = type; a.category = ItemCategory.Component;
        a.wholesalePrice = buy; a.marketPrice = market; a.boxSize = size; a.boxColor = color;
        a.mass = Mathf.Max(0.3f, size.x * size.y * size.z * 40f);
        extra(a);
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    static ItemData Item(string id, string name, ItemCategory cat, int buy, int market, Vector3 size, Color color)
    {
        string path = $"{ItemsFolder}/{id}.asset";
        var a = AssetDatabase.LoadAssetAtPath<ItemData>(path);
        if (a != null) return a;
        a = ScriptableObject.CreateInstance<ItemData>();
        a.itemId = id; a.displayName = name; a.category = cat;
        a.wholesalePrice = buy; a.marketPrice = market; a.boxSize = size; a.boxColor = color; a.mass = 4f;
        AssetDatabase.CreateAsset(a, path);
        return a;
    }

    // =====================================================================
    // MALZEMELER
    // =====================================================================

    static Shader LitShader()
    {
        return Shader.Find("Universal Render Pipeline/Lit")
               ?? Shader.Find("HDRP/Lit")
               ?? Shader.Find("Standard");
    }

    static Material Mat(string key, Color color)
    {
        string path = $"{MaterialsFolder}/M_{SafeName(key)}.mat";
        if (matCache.TryGetValue(path, out var cached) && cached != null) return cached;

        EnsureFolder(MaterialsFolder);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(LitShader()) { color = color };
            AssetDatabase.CreateAsset(mat, path);
        }
        matCache[path] = mat;
        return mat;
    }

    static Material GlassMat()
    {
        string path = $"{MaterialsFolder}/M_Cam.mat";
        if (matCache.TryGetValue(path, out var cached) && cached != null) return cached;

        EnsureFolder(MaterialsFolder);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var shader = LitShader();
            m = new Material(shader);
            if (shader.name.Contains("Universal"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_Smoothness", 0.95f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (shader.name == "Standard")
            {
                m.SetFloat("_Mode", 3f);
                m.SetInt("_SrcBlend", (int)BlendMode.One);
                m.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.DisableKeyword("_ALPHATEST_ON");
                m.DisableKeyword("_ALPHABLEND_ON");
                m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.renderQueue = (int)RenderQueue.Transparent;
            }
            m.color = new Color(0.65f, 0.85f, 0.95f, 0.22f);
            AssetDatabase.CreateAsset(m, path);
        }
        matCache[path] = m;
        return m;
    }

    static Material EmissiveMat()
    {
        string path = $"{MaterialsFolder}/M_Lamba.mat";
        if (matCache.TryGetValue(path, out var cached) && cached != null) return cached;

        EnsureFolder(MaterialsFolder);
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(LitShader()) { color = Color.white };
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", new Color(1f, 0.97f, 0.9f) * 1.5f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            AssetDatabase.CreateAsset(m, path);
        }
        matCache[path] = m;
        return m;
    }

    static void Paint(GameObject go, string key, Color color) => go.GetComponent<Renderer>().sharedMaterial = Mat(key, color);

    static string SafeName(string s)
    {
        var map = new Dictionary<char, char> { { 'ı', 'i' }, { 'İ', 'I' }, { 'ş', 's' }, { 'Ş', 'S' }, { 'ğ', 'g' }, { 'Ğ', 'G' }, { 'ü', 'u' }, { 'Ü', 'U' }, { 'ö', 'o' }, { 'Ö', 'O' }, { 'ç', 'c' }, { 'Ç', 'C' }, { 'â', 'a' } };
        var chars = s.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (map.TryGetValue(chars[i], out var r)) chars[i] = r;
            else if (!char.IsLetterOrDigit(chars[i])) chars[i] = '_';
        }
        return new string(chars);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
#endif

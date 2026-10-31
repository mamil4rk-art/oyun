#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Oyuncunun arabası: Fiat Egea (sedan) görünümlü, gövdesi EgeaGeometry ile kodla üretilir.
/// Mesh'ler Assets/Data/Meshes/Egea içine kaydedilir. Hiyerarşi sürüş için hazır:
///   Araba - Egea
///     ├ Bagaj Kapağı (CarTrunk, menteşe)      ├ Tekerlek ÖnSol / ÖnSağ / ArkaSol / ArkaSağ (göbek)
///     ├ Direksiyon (dönme noktası)             │    └ Dönen (lastik + jant + disk)
///     ├ Bagaj (4 yuva)                         └ Kaliper (dönmez)
/// </summary>
public static partial class PrototypeSceneBuilder
{
    const string CarMeshFolder = "Assets/Data/Meshes/Egea";
    /// Blender'da üretilen model (Tools~/Blender/egea.py). Yoksa kodla üretilen yedek gövde kullanılır.
    const string CarModelPath = "Assets/Models/Egea/Egea.fbx";

    /// Araç uzayında sabit noktalar (Blender betiği ve EgeaGeometry ile aynı ölçüler)
    static readonly Vector3 DriverEyePos = new Vector3(-0.37f, 1.17f, -0.08f);
    static readonly Vector3 SteeringAxis = new Vector3(0f, 0.42f, -0.91f).normalized;

    /// pos: zemindeki merkez noktası. yaw: 0 iken araba +z'ye bakar.
    static GameObject BuildCar(Transform parent, Vector3 pos, float yaw)
    {
        var root = new GameObject("Araba - Egea");
        root.transform.SetParent(parent, false);
        root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

        var groups = BuildCarVisualFromModel(root.transform, out var info) ?? BuildCarVisualProcedural(root.transform);

        PlacementSlot[] slots;
        Vector3 eyePos = DriverEyePos, steerAxis = SteeringAxis;
        float wheelR = EgeaGeometry.WheelR;
        if (info != null)
        {
            // Station wagon (Tipo) modeli: ölçüler Blender betiğinin yazdığı Egea_ayar.json'dan
            BuildWagonColliders(root.transform, info);
            slots = BuildWagonTrunk(root.transform, groups["Bagaj Kapağı"], info);
            eyePos = new Vector3(-0.36f, 1.15f, -0.06f);
            if (info.direksiyonEkseni != null && info.direksiyonEkseni.Length == 3)
                steerAxis = new Vector3(info.direksiyonEkseni[0], info.direksiyonEkseni[1], info.direksiyonEkseni[2]).normalized;
            if (info.tekerYaricap > 0.2f) wheelR = info.tekerYaricap;
        }
        else
        {
            BuildCarColliders(root.transform);
            slots = BuildCarTrunk(root.transform, groups["Bagaj Kapağı"]);
        }
        // Plakalar kullanıcı isteğiyle şimdilik takılmıyor (BuildPlate hazır duruyor)

        var eye = new GameObject("Sürücü Gözü");
        eye.transform.SetParent(root.transform, false);
        eye.transform.localPosition = eyePos;

        // Sürüş: arcade fizik (CarController kütle, ağırlık merkezi vb. kendisi ayarlar)
        root.AddComponent<Rigidbody>();
        var ctl = root.AddComponent<CarController>();
        CarController.Wheel W(string g, bool front) => new CarController.Wheel { pivot = groups[g], spinner = groups[g + "/Dönen"], front = front };
        ctl.wheels = new[] { W("Tekerlek ÖnSol", true), W("Tekerlek ÖnSağ", true), W("Tekerlek ArkaSol", false), W("Tekerlek ArkaSağ", false) };
        ctl.steeringWheel = groups["Direksiyon"];
        ctl.steeringAxis = steerAxis;
        ctl.driverEye = eye.transform;
        ctl.trunkSlots = slots;
        ctl.wheelRadius = wheelR;
        return root.gameObject;
    }

    /// Blender betiğinin (tools~/Blender/tipo.py) modelin yanına yazdığı ölçüler (araç uzayı, metre).
    [System.Serializable]
    class CarModelInfo
    {
        public float tekerYaricap;
        public float[] direksiyon, direksiyonEkseni, mentese, min, max;
        public float bagajZemin, bagajTavan, bagajSag, bagajOn, bagajArka;
    }

    const string CarModelInfoPath = "Assets/Models/Egea/Egea_ayar.json";

    /// Station wagon gövdesi: bagaj tavana kadar açık, arka kapak tavandan menteşeli.
    static void BuildWagonColliders(Transform root, CarModelInfo info)
    {
        var go = new GameObject("Çarpışma");
        go.transform.SetParent(root, false);
        void Box(Vector3 c, Vector3 s)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = c;
            b.size = s;
        }
        float floor = info.bagajZemin;
        Box(new Vector3(0f, 0.58f, 0.62f), new Vector3(1.78f, 0.86f, 3.28f));            // ön + kabin alt (z -1.02 … 2.26)
        Box(new Vector3(0f, 1.235f, -0.365f), new Vector3(1.36f, 0.47f, 2.63f));          // kabin üstü / tavan
        Box(new Vector3(0f, (0.12f + floor) / 2f - 0.003f, -1.635f), new Vector3(1.70f, floor - 0.126f, 1.23f)); // bagaj tabanı altı
        Box(new Vector3(-0.72f, (floor + 1.0f) / 2f, -1.62f), new Vector3(0.34f, 1.0f - floor, 1.2f));          // bagaj yanları
        Box(new Vector3(0.72f, (floor + 1.0f) / 2f, -1.62f), new Vector3(0.34f, 1.0f - floor, 1.2f));
        Box(new Vector3(0f, 0.37f, -2.19f), new Vector3(1.60f, 0.44f, 0.15f));            // arka tampon (eşiğin altı)
    }

    static PlacementSlot[] BuildWagonTrunk(Transform root, Transform lid, CarModelInfo info)
    {
        var trunk = lid.gameObject.AddComponent<CarTrunk>();
        trunk.openAngle = 80f;

        // Kapağın çarpıştırıcısı: görsel sınırlarından (menteşe uzayında)
        var b = new Bounds();
        bool any = false;
        foreach (var r in lid.GetComponentsInChildren<MeshRenderer>())
        {
            var wb = r.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = wb.center + Vector3.Scale(wb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var lp = lid.InverseTransformPoint(corner);
                if (!any) { b = new Bounds(lp, Vector3.zero); any = true; } else b.Encapsulate(lp);
            }
        }
        var col = lid.gameObject.AddComponent<BoxCollider>();
        col.center = any ? b.center : new Vector3(0f, -0.4f, -0.25f);
        col.size = any ? b.size : new Vector3(1.2f, 0.8f, 0.5f);

        var floor = new GameObject("Bagaj");
        floor.transform.SetParent(root, false);
        floor.transform.localPosition = new Vector3(0f, info.bagajZemin, -1.62f);
        var slots = new List<PlacementSlot>();
        int n = 1;
        foreach (float z in new[] { 0.22f, -0.23f })
            foreach (float x in new[] { -0.25f, 0.25f })
            {
                var s = CreateSlot(floor.transform, $"Bagaj {n++}", new Vector3(x, 0f, z));
                var bc = s.GetComponent<BoxCollider>();
                bc.size = new Vector3(0.48f, 0.45f, 0.42f);
                bc.center = new Vector3(0f, 0.225f, 0f);
                slots.Add(s);
            }
        trunk.slots = slots.ToArray();
        return trunk.slots;
    }

    /// Blender modelini yerleştirir. Hareketli parçalar (tekerlekler, bagaj kapağı, direksiyon) için
    /// kendi pivot nesnelerimizi kurar ve modelin parçalarını onların altına taşır; böylece FBX'in
    /// eksen dönüşümü ne olursa olsun sürüş kodu araç uzayında temiz (dönüşsüz) pivotlarla çalışır.
    /// Model yoksa null döner.
    static Dictionary<string, Transform> BuildCarVisualFromModel(Transform root, out CarModelInfo info)
    {
        info = null;
        var infoAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(CarModelInfoPath);
        if (infoAsset != null) info = JsonUtility.FromJson<CarModelInfo>(infoAsset.text);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CarModelPath) == null)
            AssetDatabase.ImportAsset(CarModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(CarModelPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogWarning("PC Shop: " + CarModelPath + " bulunamadı, kodla üretilen araba kullanılıyor.");
            info = null;
            return null;
        }
        bool changed = false;
        if (importer.importTangents != ModelImporterTangents.CalculateMikk) { importer.importTangents = ModelImporterTangents.CalculateMikk; changed = true; }
        if (importer.importNormals != ModelImporterNormals.Import) { importer.importNormals = ModelImporterNormals.Import; changed = true; }
        if (importer.importAnimation) { importer.importAnimation = false; changed = true; }
        if (importer.importCameras || importer.importLights) { importer.importCameras = false; importer.importLights = false; changed = true; }
        if (changed) importer.SaveAndReimport();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarModelPath);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Model";
        inst.transform.SetParent(root, false);

        var byName = new Dictionary<string, Transform>();
        foreach (var t in inst.GetComponentsInChildren<Transform>(true)) byName[t.name] = t;
        Transform Find(string n)
        {
            if (byName.TryGetValue(n, out var t)) return t;
            Debug.LogError("PC Shop: araba modelinde '" + n + "' bulunamadı.");
            return null;
        }
        Vector3 Local(Transform t) => root.InverseTransformPoint(t.position);

        // Yön / ölçek denetimi: sol ön teker (-0.765, 0.316, 1.346) civarında olmalı.
        // Parçaların dönme noktası kendi merkezleri (tekerlekte göbek, kapakta menteşe).
        var fl = Find("Tekerlek_OnSol_Donen"); var fr = Find("Tekerlek_OnSag_Donen"); var rl = Find("Tekerlek_ArkaSol_Donen");
        if (fl == null || fr == null || rl == null) { Object.DestroyImmediate(inst); info = null; return null; }
        float track = Vector3.Distance(Local(fl), Local(fr));
        if (Mathf.Abs(track - 2f * EgeaGeometry.TrackHalf) > 0.05f && track > 1e-4f)
            inst.transform.localScale *= 2f * EgeaGeometry.TrackHalf / track;
        if (Local(fl).z < Local(rl).z) inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
        if (Local(fl).x > 0f) { var s = inst.transform.localScale; s.x = -s.x; inst.transform.localScale = s; }
        var p = Local(fl);
        if (Mathf.Abs(p.y - EgeaGeometry.WheelR) > 0.05f || Mathf.Abs(p.z - EgeaGeometry.AxleF) > 0.05f)
            Debug.LogWarning($"PC Shop: araba modelinin yönü beklenenden farklı (sol ön teker {p}). Ekran görüntüsü at, düzelteyim.");

        // Malzemeler: FBX'teki adlar (boya, cam, krom...) Unity malzemeleriyle değiştirilir
        foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null) mats[i] = CarMat(mats[i].name.Replace(" (Instance)", "").Split('.')[0]);
            r.sharedMaterials = mats;
        }

        var groups = new Dictionary<string, Transform> { { "", root } };
        Transform Pivot(string name, Transform parent, Vector3 carPos)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.position = root.TransformPoint(carPos);
            go.localRotation = Quaternion.identity;
            return go;
        }

        foreach (var (src, dst) in new[] { ("Tekerlek_OnSol", "Tekerlek ÖnSol"), ("Tekerlek_OnSag", "Tekerlek ÖnSağ"),
                                           ("Tekerlek_ArkaSol", "Tekerlek ArkaSol"), ("Tekerlek_ArkaSag", "Tekerlek ArkaSağ") })
        {
            var spinMesh = Find(src + "_Donen");
            var caliper = Find(src + "_Kaliper");
            if (spinMesh == null) continue;
            var hub = Pivot(dst, root, Local(spinMesh));
            var spin = Pivot("Dönen", hub, Local(spinMesh));
            spinMesh.SetParent(spin, true);
            if (caliper != null) caliper.SetParent(hub, true);
            groups[dst] = hub;
            groups[dst + "/Dönen"] = spin;
        }

        var lidMesh = Find("Bagaj_Kapagi");
        var lid = Pivot("Bagaj Kapağı", root, lidMesh != null ? Local(lidMesh) : new Vector3(0f, 1.06f, EgeaGeometry.LidZ0));
        if (lidMesh != null) lidMesh.SetParent(lid, true);
        groups["Bagaj Kapağı"] = lid;

        var swMesh = Find("Direksiyon");
        var sw = Pivot("Direksiyon", root, swMesh != null ? Local(swMesh) : new Vector3(-0.37f, 0.93f, 0.50f));
        if (swMesh != null) swMesh.SetParent(sw, true);
        groups["Direksiyon"] = sw;

        return groups;
    }

    /// Yedek: Blender modeli yoksa gövdeyi EgeaGeometry ile kodla üretir.
    static Dictionary<string, Transform> BuildCarVisualProcedural(Transform rootT)
    {
        var model = EgeaGeometry.Build();
        var root = rootT.gameObject;

        var groups = new Dictionary<string, Transform> { { "", root.transform } };
        Transform Group(string path)
        {
            if (groups.TryGetValue(path, out var t)) return t;
            int cut = path.LastIndexOf('/');
            string parentPath = cut < 0 ? "" : path.Substring(0, cut);
            var p = Group(parentPath);
            var go = new GameObject(cut < 0 ? path : path.Substring(cut + 1));
            go.transform.SetParent(p, false);
            go.transform.localPosition = model.Pivot(path) - model.Pivot(parentPath);
            groups[path] = go.transform;
            return go.transform;
        }

        EnsureFolder(CarMeshFolder);
        foreach (var cm in model.meshes)
        {
            var t = Group(cm.group);
            var piv = model.Pivot(cm.group);
            var verts = new Vector3[cm.v.Count];
            for (int i = 0; i < verts.Length; i++) verts[i] = cm.v[i] - piv;

            var mesh = new Mesh { name = "Egea " + cm.name };
            if (verts.Length > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.normals = cm.normals;
            mesh.SetUVs(0, cm.uv);
            mesh.subMeshCount = cm.tris.Count;
            var mats = new Material[cm.tris.Count];
            int k = 0;
            foreach (var kv in cm.tris)
            {
                mesh.SetTriangles(kv.Value, k, false);
                mats[k++] = CarMat(kv.Key);
            }
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            string path = $"{CarMeshFolder}/{SafeName(cm.group + "_" + cm.name)}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);

            var go = new GameObject(cm.name);
            go.transform.SetParent(t, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
        }

        // Ana grupların hepsi var olsun (mesh'i olmayan olsa bile)
        foreach (var g in model.pivots.Keys) Group(g);
        return groups;
    }

    /// Gövdeyi kaba kutularla sarar (oyuncu içinden geçmesin). Bagaj boşluğu açık kalır ki ışın yuvalara ulaşsın.
    static void BuildCarColliders(Transform root)
    {
        var go = new GameObject("Çarpışma");
        go.transform.SetParent(root, false);
        void Box(Vector3 c, Vector3 s)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = c;
            b.size = s;
        }
        // Bagaj havuzundaki kutulara değmesin: havuz z -1.25 … -2.15, taban y 0.42
        Box(new Vector3(0f, 0.60f, 0.51f), new Vector3(1.79f, 0.86f, 3.52f));     // ön + kabin alt (z -1.25 … 2.27)
        Box(new Vector3(0f, 1.25f, -0.32f), new Vector3(1.32f, 0.42f, 2.40f));    // kabin üstü
        Box(new Vector3(0f, 0.29f, -1.78f), new Vector3(1.74f, 0.24f, 0.98f));    // bagaj tabanı altı (üstü y 0.41)
        Box(new Vector3(-0.835f, 0.73f, -1.76f), new Vector3(0.12f, 0.62f, 0.90f));
        Box(new Vector3(0.835f, 0.73f, -1.76f), new Vector3(0.12f, 0.62f, 0.90f));
        Box(new Vector3(0f, 0.62f, -2.20f), new Vector3(1.60f, 0.70f, 0.12f));    // arka tampon
    }

    static PlacementSlot[] BuildCarTrunk(Transform root, Transform lid)
    {
        var trunk = lid.gameObject.AddComponent<CarTrunk>();
        var col = lid.gameObject.AddComponent<BoxCollider>();
        float len = EgeaGeometry.LidZ0 - EgeaGeometry.LidZ1;
        col.center = new Vector3(0f, -0.02f, -len / 2f);
        col.size = new Vector3(1.66f, 0.07f, len);

        var floor = new GameObject("Bagaj");
        floor.transform.SetParent(root, false);
        floor.transform.localPosition = new Vector3(0f, EgeaGeometry.TrunkFloorY, -1.72f);
        var slots = new List<PlacementSlot>();
        int n = 1;
        foreach (float z in new[] { 0.215f, -0.215f })
            foreach (float x in new[] { -0.29f, 0.29f })
            {
                var s = CreateSlot(floor.transform, $"Bagaj {n++}", new Vector3(x, 0f, z));
                var bc = s.GetComponent<BoxCollider>();
                bc.size = new Vector3(0.55f, 0.45f, 0.42f);
                bc.center = new Vector3(0f, 0.225f, 0f);
                slots.Add(s);
            }
        trunk.slots = slots.ToArray();
        return trunk.slots;
    }

    /// Türk plakası: beyaz zemin, solda mavi TR şeridi, siyah yazı (kurgusal numara).
    static void BuildPlate(Transform root, string name, (Vector3 pos, Vector3 normal) a)
    {
        var plate = new GameObject(name).transform;
        plate.SetParent(root, false);
        plate.localPosition = a.pos + a.normal * 0.012f; // ızgara / çıkartmaların önünde dursun
        plate.localRotation = Quaternion.LookRotation(-a.normal, Vector3.up);

        var white = Pbr("Araba_PlakaBeyaz", new Color(0.93f, 0.93f, 0.92f), 0.55f);
        var blue = Pbr("Araba_PlakaMavi", new Color(0.05f, 0.18f, 0.6f), 0.55f);
        RBox(plate, "Zemin", Vector3.zero, new Vector3(0.52f, 0.11f, 0.004f), white, 0.002f);
        RBox(plate, "TR Şeridi", new Vector3(-0.235f, 0f, -0.0025f), new Vector3(0.045f, 0.105f, 0.0015f), blue, 0.0005f);
        RBox(plate, "Çerçeve", new Vector3(0f, 0f, 0.003f), new Vector3(0.535f, 0.125f, 0.003f), MBlackPlastic, 0.003f);
        PlateText(plate, "TR", new Vector3(-0.235f, -0.03f, -0.0035f), 0.0028f, Color.white);
        PlateText(plate, "34 PCD 452", new Vector3(0.022f, 0f, -0.003f), 0.0105f, new Color(0.02f, 0.02f, 0.02f));
    }

    static void PlateText(Transform parent, string text, Vector3 localPos, float charSize, Color color)
    {
        var go = new GameObject("Yazı - " + text);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 64;
        tm.characterSize = charSize;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = color;
        if (signFont != null)
        {
            tm.font = signFont;
            go.GetComponent<MeshRenderer>().sharedMaterial = signFont.material;
        }
        if (textShader != null) go.AddComponent<SignText>().SetShader(textShader);
    }

    // =====================================================================
    // ARABA MALZEMELERİ
    // =====================================================================

    static Material CarMat(string key)
    {
        switch (key)
        {
            case "boya": return CarPaint();
            case "alt": return Pbr("Araba_Alt", new Color(0.025f, 0.025f, 0.025f), 0.1f);
            case "cam": return CarGlass("Araba_Cam", new Color(0.04f, 0.06f, 0.07f, 0.55f), 0.96f, true);
            case "cerceve": return Pbr("Araba_ParlakSiyah", new Color(0.012f, 0.012f, 0.014f), 0.92f);
            case "krom": return MChrome;
            case "plastikMat": return Pbr("Araba_DokuluPlastik", new Color(0.035f, 0.035f, 0.038f), 0.25f, 0f, FeltPair().color, FeltPair().normal, 0.4f);
            case "petek":
            {
                var m = Pbr("Araba_Petek", new Color(0.03f, 0.03f, 0.032f), 0.55f, 0.2f, HoneycombPair().color, HoneycombPair().normal, 1f);
                m.mainTextureScale = new Vector2(12f, 12f);
                return m;
            }
            case "farGovde": return Pbr("Araba_FarGovde", new Color(0.09f, 0.09f, 0.1f), 0.9f, 0.9f);
            case "farLens": return CarGlass("Araba_FarCami", new Color(1f, 1f, 1f, 0.08f), 1f, false);
            case "farDRL": return Pbr("Araba_GunduzFari", new Color(0.92f, 0.93f, 0.95f), 0.75f);
            case "amber": return Pbr("Araba_Sinyal", new Color(0.95f, 0.5f, 0.06f), 0.85f);
            case "reflektor": return MChrome;
            case "stopIc": return Pbr("Araba_StopIc", new Color(0.2f, 0.01f, 0.012f), 0.85f, 0.3f);
            case "stopKirmizi": return Pbr("Araba_StopLed", new Color(0.75f, 0.03f, 0.03f), 0.85f);
            case "stopBeyaz": return Pbr("Araba_GeriVites", new Color(0.85f, 0.85f, 0.88f), 0.9f, 0.6f);
            case "stopLens": return CarGlass("Araba_StopCami", new Color(0.6f, 0.02f, 0.03f, 0.45f), 0.97f, false);
            case "lastik": return Pbr("Araba_Lastik", new Color(0.05f, 0.05f, 0.05f), 0.3f);
            case "lastikSirt": return Pbr("Araba_LastikSirt", new Color(0.045f, 0.045f, 0.045f), 0.15f, 0f, TreadPair().color, TreadPair().normal, 1f);
            case "jant": return Pbr("Araba_Jant", new Color(0.72f, 0.73f, 0.75f), 0.78f, 0.9f);
            case "jantIc": return Pbr("Araba_JantIc", new Color(0.33f, 0.34f, 0.36f), 0.5f, 0.8f);
            case "disk": return MBrushed;
            case "kaliper": return Pbr("Araba_Kaliper", new Color(0.12f, 0.12f, 0.13f), 0.4f, 0.6f);
            case "aynaCam": return Pbr("Araba_AynaCam", new Color(0.9f, 0.92f, 0.95f), 0.98f, 1f);
            case "icPlastik": return Pbr("Araba_IcPlastik", new Color(0.055f, 0.055f, 0.06f), 0.3f, 0f, FeltPair().color, FeltPair().normal, 0.3f);
            case "torpido": return Pbr("Araba_Torpido", new Color(0.065f, 0.065f, 0.07f), 0.28f, 0f, FeltPair().color, FeltPair().normal, 0.35f);
            case "deri": return MLeather;
            case "kumas": return Pbr("Araba_Kumas", new Color(0.11f, 0.11f, 0.12f), 0.12f, 0f, FeltPair().color, FeltPair().normal, 0.8f);
            case "tavan": return Pbr("Araba_TavanDosemesi", new Color(0.5f, 0.5f, 0.48f), 0.08f, 0f, FeltPair().color, FeltPair().normal, 0.5f);
            case "hali": return Pbr("Araba_Hali", new Color(0.06f, 0.06f, 0.06f), 0.05f, 0f, FeltPair().color, FeltPair().normal, 0.8f);
            case "ekran": return Pbr("Araba_Ekran", new Color(0.01f, 0.01f, 0.015f), 0.95f);
            case "conta": return MRubber;
            case "aralik": return Pbr("Araba_PanelAraligi", new Color(0.01f, 0.004f, 0.004f), 0.1f);
        }
        Debug.LogWarning("PC Shop: araba için tanımsız malzeme anahtarı: " + key);
        return Pbr("Araba_" + SafeName(key), Color.magenta, 0.5f);
    }

    /// Koyu kırmızı metalik boya. URP Complex Lit varsa vernik (clear coat) katmanı da açılır.
    static Material CarPaint()
    {
        string path = $"{MaterialsFolder}/M_Araba_Boya.mat";
        if (matCache.TryGetValue(path, out var cached) && cached != null) return cached;
        EnsureFolder(MaterialsFolder);

        var complex = Shader.Find("Universal Render Pipeline/Complex Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(complex != null ? complex : LitShader());
            AssetDatabase.CreateAsset(m, path);
        }
        else if (complex != null && m.shader != complex) m.shader = complex;

        m.color = new Color(0.5f, 0.035f, 0.045f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.82f);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.35f);
        if (m.HasProperty("_ClearCoatMask"))
        {
            m.SetFloat("_ClearCoat", 1f);
            m.SetFloat("_ClearCoatMask", 1f);
            m.SetFloat("_ClearCoatSmoothness", 0.96f);
            m.EnableKeyword("_CLEARCOAT");
        }
        EditorUtility.SetDirty(m);
        matCache[path] = m;
        return m;
    }

    /// Saydam araba camı / far camı. twoSided: içeriden de görünsün (kabin camları).
    static Material CarGlass(string key, Color color, float smoothness, bool twoSided)
    {
        string path = $"{MaterialsFolder}/M_{SafeName(key)}.mat";
        if (matCache.TryGetValue(path, out var cached) && cached != null) return cached;
        EnsureFolder(MaterialsFolder);

        var shader = LitShader();
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        if (shader.name.Contains("Universal"))
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", twoSided ? (float)CullMode.Off : (float)CullMode.Back);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        m.color = color;
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
        if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        matCache[path] = m;
        return m;
    }

    /// Petek ızgara (altıgen delikler). 256 px = bir tekrar.
    static (Texture2D color, Texture2D normal) HoneycombPair() => LoadOrCreatePair("T_Petek", 256, 6f, (px, hg) =>
    {
        const int S = 256;
        const float R = 32f; // altıgen yarıçapı (piksel)
        float w = Mathf.Sqrt(3f) * R, hgt = 1.5f * R;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // En yakın altıgen merkezi (ofsetli satırlar)
                float best = float.MaxValue;
                for (int r = -1; r <= (int)(S / hgt) + 1; r++)
                {
                    float cy = r * hgt;
                    if (Mathf.Abs(cy - y) > hgt * 1.2f) continue;
                    float off = (r & 1) * w / 2f;
                    for (int c = -1; c <= (int)(S / w) + 1; c++)
                    {
                        float cx = c * w + off;
                        float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                        float hexD = Mathf.Max(dx * 0.866f + dy * 0.5f, dy);
                        if (hexD < best) best = hexD;
                    }
                }
                float edge = R * 0.82f - best; // pozitif: delik içi
                float v = edge > 2f ? 0.05f : edge > 0f ? 0.25f : 0.6f;
                px[y * S + x] = new Color(v, v, v);
                hg[y * S + x] = edge > 0f ? 0f : 1f;
            }
    });

    /// Lastik sırtı: enine kanallar ve küçük blok yarıkları. 256 px = 0.25 m.
    static (Texture2D color, Texture2D normal) TreadPair() => LoadOrCreatePair("T_LastikSirt", 256, 5f, (px, hg) =>
    {
        const int S = 256, Pitch = 32;
        var rng = new System.Random(11);
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                int d = (y + x / 6) % Pitch;
                bool groove = d < 4;
                bool sipe = ((y + x / 3) % (Pitch / 2)) == 0;
                float n = Rand(rng) * 0.05f;
                float v = groove ? 0.35f : sipe ? 0.6f : 0.9f + n;
                px[y * S + x] = new Color(v, v, v);
                hg[y * S + x] = groove ? 0f : sipe ? 0.5f : 1f;
            }
    });
}
#endif

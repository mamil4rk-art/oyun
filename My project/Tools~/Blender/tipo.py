# -*- coding: utf-8 -*-
# =====================================================================
# Fiat Tipo Station Wagon 2021 (Sketchfab, Nieve5677, CC Attribution) → oyuna hazır FBX.
#
#   blender -b --factory-startup --python tipo.py -- --src <model.gltf> --fbx <Egea.fbx>
#           [--blend <kayıt.blend>] [--render <klasör>] [--ratio 0.2]
#
# Yaptıkları:
#   - Ters duran (mm) modeli çevirir, metreye indirir. Sonuç Blender'da: ön -Y, yukarı +Z, sürücü (sol) +X
#     (egea.py ile aynı düzen; Unity sahne kurucusu aynı isimleri bekler).
#   - Plakaları siler.
#   - Dört tekerleği ayırır: "<Tekerlek>_Donen" (lastik, jant, disk, göbek) ve "<Tekerlek>_Kaliper" (dönmez);
#     dönme noktası göbek merkezi.
#   - Direksiyon simidini iç mekândan ayırır ("Direksiyon", dönme noktası simit merkezi).
#   - Arka kapağı (bagaj kapısı) gövdeden ayırır ("Bagaj_Kapagi", dönme noktası üstteki menteşe).
#   - Üçgen sayısını düşürür (oyun için), malzemeleri Unity anahtarlarına göre yeniden adlandırır.
#   - Unity için ölçüleri "Egea_ayar.json" olarak FBX'in yanına yazar.
# =====================================================================
import bpy
import bmesh
import json
import math
import os
import sys
from mathutils import Vector, Matrix

try:
    import numpy as np
except Exception:
    np = None

# Modelin malzemesi → oyundaki malzeme anahtarı (PrototypeSceneBuilder.CarMat)
MAT_MAP = {
    "body": "boya", "black": "cerceve", "black_matt": "plastikMat", "black_shinny": "cerceve",
    "brakes1": "disk", "chrome": "krom", "coper": "aralik", "d_glass": "cam", "glass": "farLens",
    "interior": "torpido", "interior.001": "icPlastik", "milk_glass": "farDRL", "o_glass": "amber",
    "r_glass": "stopLens", "red": "stopKirmizi", "silver": "jant", "silver_d": "jantIc",
    "taillight": "stopIc", "tire_mat1": "lastik", "white_led": "farDRL",
}
INTERIOR_MATS = {"interior", "interior.001"}


def car(p):
    """Blender (ön -Y, yukarı +Z, sol +X) → araç uzayı (x sağ, y yukarı, z ön)."""
    return Vector((-p.x, p.z, -p.y))


def blender(p):
    return Vector((-p[0], -p[2], p[1]))


def args():
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    o = {}
    i = 0
    while i < len(a):
        if a[i].startswith("--") and i + 1 < len(a):
            o[a[i][2:]] = a[i + 1]
            i += 2
        else:
            i += 1
    return o


def mesh_objects():
    return [o for o in bpy.context.scene.objects if o.type == 'MESH']


def base_mat_name(m):
    return m.name if m is not None else ""


# ---------------------------------------------------------------------
def import_and_flatten(src):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=src)
    fix = Matrix.Rotation(math.pi, 4, 'X') @ Matrix.Scale(0.001, 4)
    for ob in mesh_objects():
        mw = fix @ ob.matrix_world
        if ob.data.users > 1:
            ob.data = ob.data.copy()
        ob.data.transform(mw)
        if mw.determinant() < 0:
            ob.data.flip_normals()
    for ob in mesh_objects():
        ob.parent = None
        ob.matrix_world = Matrix.Identity(4)
    for ob in list(bpy.context.scene.objects):
        if ob.type != 'MESH':
            bpy.data.objects.remove(ob, do_unlink=True)
    for ob in mesh_objects():
        ob.data.update()


def bounds(ob):
    vs = [v.co for v in ob.data.vertices]
    if not vs:
        return Vector((0, 0, 0)), Vector((0, 0, 0))
    lo = Vector((min(v.x for v in vs), min(v.y for v in vs), min(v.z for v in vs)))
    hi = Vector((max(v.x for v in vs), max(v.y for v in vs), max(v.z for v in vs)))
    return lo, hi


def mat_of(ob):
    return base_mat_name(ob.data.materials[0]) if len(ob.data.materials) else ""


def remove_plates():
    for ob in mesh_objects():
        if any(m is not None and m.name.startswith("plate") for m in ob.data.materials):
            bpy.data.objects.remove(ob, do_unlink=True)


def join(objs, name):
    objs = [o for o in objs if o is not None]
    if not objs:
        return None
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    ob.data.name = name
    return ob


def set_origin(ob, p):
    """Mesh'i kaydırıp nesnenin dönme noktasını p (Blender koordinatı) yapar."""
    ob.data.transform(Matrix.Translation(-p))
    ob.matrix_world = Matrix.Translation(p)


# ---------------------------------------------------------------------
# Tekerlekler
# ---------------------------------------------------------------------
def split_wheels():
    tires = [o for o in mesh_objects() if mat_of(o) == "tire_mat1"]
    hubs = []
    for t in tires:
        lo, hi = bounds(t)
        hubs.append(((lo + hi) / 2, (hi.z - lo.z) / 2))
    print("Lastik sayısı:", len(tires), [("%.3f %.3f %.3f r=%.3f" % (c.x, c.y, c.z, r)) for c, r in hubs])

    def name_of(c):
        front = c.y < 0          # ön -Y
        left = c.x > 0           # sol +X
        return "Tekerlek_" + ("On" if front else "Arka") + ("Sol" if left else "Sag")

    groups = {name_of(c): {"hub": c, "r": r, "spin": [], "cal": []} for c, r in hubs}
    for ob in mesh_objects():
        lo, hi = bounds(ob)
        for nm, g in groups.items():
            c, r = g["hub"], g["r"]
            inside = (abs(lo.x) > 0.55 and abs(hi.x) > 0.55 and (lo.x > 0) == (c.x > 0)
                      and all(math.hypot(p.y - c.y, p.z - c.z) <= r + 0.02 for p in
                              (Vector((0, lo.y, lo.z)), Vector((0, lo.y, hi.z)), Vector((0, hi.y, lo.z)), Vector((0, hi.y, hi.z)))))
            if not inside:
                continue
            ctr = (lo + hi) / 2
            off = math.hypot(ctr.y - c.y, ctr.z - c.z)
            if mat_of(ob) == "silver_d" and off > 0.05:
                g["cal"].append(ob)          # kaliper: eksenden kaçık, dönmez
            else:
                g["spin"].append(ob)
            break
    for nm, g in groups.items():
        s = join(g["spin"], nm + "_Donen")
        set_origin(s, g["hub"])
        k = join(g["cal"], nm + "_Kaliper")
        if k is not None:
            set_origin(k, g["hub"])
        print(nm, "dönen parça:", len(g["spin"]), "kaliper:", len(g["cal"]))
    return groups


# ---------------------------------------------------------------------
# Direksiyon simidi
# ---------------------------------------------------------------------
def split_steering():
    """İç mekân mesh'lerinde sürücü önündeki halkayı bulur, ayrı nesne yapar."""
    if np is None:
        print("numpy yok: direksiyon ayrılmadı")
        return None, None
    # Kaba kutu (araç uzayı): sürücü önü
    def in_box(q):
        return -0.62 < q.x < -0.12 and 0.70 < q.y < 1.16 and 0.30 < q.z < 0.80
    cands = []
    for ob in mesh_objects():
        if not any(m is not None and m.name in INTERIOR_MATS for m in ob.data.materials):
            continue
        for v in ob.data.vertices:
            q = car(v.co)
            if in_box(q):
                cands.append(q)
    if len(cands) < 50:
        print("Direksiyon adayı az:", len(cands))
        return None, None
    P = np.array([[q.x, q.y, q.z] for q in cands])
    ctr = P.mean(axis=0)
    # Halka düzlemine iki tur yakınsama (PCA: en küçük özvektör = simit ekseni)
    for _ in range(3):
        C = np.cov((P - ctr).T)
        w, vec = np.linalg.eigh(C)
        n = vec[:, 0]
        d = (P - ctr) @ n
        rad = np.linalg.norm((P - ctr) - np.outer(d, n), axis=1)
        keep = (np.abs(d) < 0.06) & (rad < 0.22)
        if keep.sum() < 30:
            break
        P = P[keep]
        ctr = P.mean(axis=0)
    n = Vector(n.tolist()).normalized()
    if n.z > 0:              # eksen sürücüye (arkaya, -z) baksın
        n = -n
    center = Vector(ctr.tolist())
    print("Direksiyon merkezi (araç): %.3f %.3f %.3f eksen: %.3f %.3f %.3f" % (*center, *n))

    pieces = []
    for ob in mesh_objects():
        if not any(m is not None and m.name in INTERIOR_MATS for m in ob.data.materials):
            continue
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        sel = []
        for f in bm.faces:
            ok = True
            for v in f.verts:
                q = car(v.co)
                dv = q - center
                dd = dv.dot(n)
                rr = (dv - n * dd).length
                if not (abs(dd) < 0.07 and rr < 0.205):
                    ok = False
                    break
            if ok:
                sel.append(f)
        if not sel:
            bm.free()
            continue
        nb = bmesh.new()
        vmap = {}
        for f in sel:
            vs = []
            for v in f.verts:
                if v.index not in vmap:
                    vmap[v.index] = nb.verts.new(v.co)
                vs.append(vmap[v.index])
            try:
                nf = nb.faces.new(vs)
                nf.material_index = f.material_index
                nf.smooth = f.smooth
            except ValueError:
                pass
        bmesh.ops.delete(bm, geom=sel, context='FACES_ONLY')
        bm.to_mesh(ob.data)
        bm.free()
        me = bpy.data.meshes.new("Direksiyon_parca")
        nb.to_mesh(me)
        nb.free()
        for m in ob.data.materials:
            me.materials.append(m)
        po = bpy.data.objects.new("Direksiyon_parca", me)
        bpy.context.scene.collection.objects.link(po)
        pieces.append(po)
        print("  direksiyon yüzü:", len(sel), "←", ob.name)
    sw = join(pieces, "Direksiyon")
    if sw is not None:
        set_origin(sw, blender(center))
    return center, n


# ---------------------------------------------------------------------
# Arka kapak (bagaj kapısı)
# ---------------------------------------------------------------------
def tailgate_pred(c):
    """Araç uzayındaki yüz merkezi arka kapağa mı ait?"""
    if c.y < 0.60 or abs(c.x) > 0.64:
        return False
    if c.y > 1.40:
        return c.z < -1.86
    zlim = -1.70 - (1.40 - c.y) * 0.55          # üstte -1.70, altta (y 0.6) -2.14
    return c.z < zlim


def split_tailgate():
    pieces = []
    for ob in list(mesh_objects()):
        if ob.name.startswith("Tekerlek_") or ob.name == "Direksiyon":
            continue
        mats = [m.name if m else "" for m in ob.data.materials]
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        sel = []
        for f in bm.faces:
            mname = mats[f.material_index] if f.material_index < len(mats) else ""
            if mname in INTERIOR_MATS:
                continue
            if tailgate_pred(car(f.calc_center_median())):
                sel.append(f)
        if not sel:
            bm.free()
            continue
        nb = bmesh.new()
        uv_src = bm.loops.layers.uv.active
        uv_dst = nb.loops.layers.uv.new("UVMap") if uv_src is not None else None
        vmap = {}
        for f in sel:
            vs = []
            for v in f.verts:
                if v.index not in vmap:
                    vmap[v.index] = nb.verts.new(v.co)
                vs.append(vmap[v.index])
            try:
                nf = nb.faces.new(vs)
            except ValueError:
                continue
            nf.material_index = f.material_index
            nf.smooth = f.smooth
            if uv_src is not None:
                for l_src, l_dst in zip(f.loops, nf.loops):
                    l_dst[uv_dst].uv = l_src[uv_src].uv
        bmesh.ops.delete(bm, geom=sel, context='FACES_ONLY')
        bm.to_mesh(ob.data)
        bm.free()
        me = bpy.data.meshes.new("kapak_parca")
        nb.to_mesh(me)
        nb.free()
        for m in ob.data.materials:
            me.materials.append(m)
        po = bpy.data.objects.new("kapak_parca", me)
        bpy.context.scene.collection.objects.link(po)
        pieces.append(po)
    lid = join(pieces, "Bagaj_Kapagi")
    # Menteşe: kapağın en üst kenarının en öndeki noktası (orta hatta)
    pts = [car(v.co) for v in lid.data.vertices]
    top = max(p.y for p in pts)
    front = max(p.z for p in pts if p.y > top - 0.04)
    hinge = Vector((0.0, top - 0.01, front))
    set_origin(lid, blender(hinge))
    print("Arka kapak: %d köşe, menteşe (araç) %.3f %.3f %.3f" % (len(pts), *hinge))
    return hinge


# ---------------------------------------------------------------------
def weld_and_sharpen(ob, angle=35.0):
    """Ayrık köşeleri birleştirir (glTF dikişleri), sonra yumuşatmayı açıya göre yeniden kurar:
    keskin kırımlar (40° üstü) sert kalır, kavisli yüzeyler yumuşak."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.00005)
    lim = math.radians(angle)
    for f in bm.faces:
        f.smooth = True
    for e in bm.edges:
        if len(e.link_faces) == 2:
            e.smooth = e.calc_face_angle(0.0) < lim
    bm.to_mesh(ob.data)
    bm.free()
    if hasattr(ob.data, "free_normals_split"):
        try:
            ob.data.free_normals_split()
        except Exception:
            pass
    try:
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True)
        bpy.ops.mesh.customdata_custom_splitnormals_clear()
    except Exception:
        pass


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def decimate(ratio):
    for ob in mesh_objects():
        n = tri_count(ob)
        weld_and_sharpen(ob)
        if n < 6000:
            continue
        # Gövde sacı daha nazik azaltılır (görünüşü taşıyan yüzey); detaylar (jant, krom, lamba içi) daha çok
        mats = {m.name for m in ob.data.materials if m is not None}
        r = ratio
        if "body" in mats:
            r = max(ratio * 2.0, 0.45)
        if ob.name in ("Direksiyon",) or ob.name.endswith("_Kaliper"):
            r = max(ratio, 0.5)
        md = ob.modifiers.new("Azalt", 'DECIMATE')
        md.ratio = r
        md.use_collapse_triangulate = True
        try:
            md.use_symmetry = False
        except Exception:
            pass
        bpy.context.view_layer.objects.active = ob
        bpy.ops.object.select_all(action='DESELECT')
        ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=md.name)
        weld_and_sharpen(ob)
    print("Azaltma sonrası üçgen:", sum(tri_count(o) for o in mesh_objects()))


def rename_materials():
    for m in list(bpy.data.materials):
        key = MAT_MAP.get(m.name)
        if key is None:
            continue
        existing = bpy.data.materials.get(key)
        if existing is not None and existing is not m:
            for ob in mesh_objects():
                for i, s in enumerate(ob.data.materials):
                    if s is m:
                        ob.data.materials[i] = existing
        else:
            m.name = key


def merge_body():
    """Geri kalan sabit parçaları malzemeye göre birkaç nesnede toplar (Unity'de az çizim çağrısı)."""
    fixed = [o for o in mesh_objects() if not (o.name.startswith("Tekerlek_") or o.name in ("Direksiyon", "Bagaj_Kapagi"))]
    inner = [o for o in fixed if all(m is not None and m.name in INTERIOR_MATS for m in o.data.materials)]
    glass = [o for o in fixed if o not in inner and all(m is not None and m.name in ("d_glass",) for m in o.data.materials)]
    rest = [o for o in fixed if o not in inner and o not in glass]
    join(inner, "Ic_Mekan")
    join(glass, "Camlar")
    join(rest, "Govde")


def measure(meta):
    """Bagaj tabanı / tavanı / genişliği: aşağı-yukarı ışınlar (sadece görsel mesh'lere)."""
    from mathutils.bvhtree import BVHTree
    verts, polys = [], []
    for ob in mesh_objects():
        if ob.name == "Bagaj_Kapagi":
            continue
        mw = ob.matrix_world
        base = len(verts)
        verts += [car(mw @ v.co) for v in ob.data.vertices]
        polys += [tuple(base + i for i in p.vertices) for p in ob.data.polygons]
    bvh = BVHTree.FromPolygons(verts, polys)

    def ray(o, d):
        loc, n, i, dist = bvh.ray_cast(Vector(o), Vector(d), 3.0)
        return loc
    floor = []
    for z in (-1.45, -1.65, -1.85):
        for x in (-0.25, 0.0, 0.25):
            # Aşağı doğru art arda çarpılan yüzeyler; bagaj tabanı 0.85'in altındaki ilk yukarı bakan yüzey
            o = Vector((x, 1.0, z))
            seq = []
            for _ in range(6):
                loc, n, i, dist = bvh.ray_cast(o, Vector((0, -1, 0)), 3.0)
                if loc is None:
                    break
                seq.append((round(loc.y, 3), round(n.y, 2)))
                o = loc + Vector((0, -0.002, 0))
            print("  taban ışını x=%.2f z=%.2f: %s" % (x, z, seq))
            for y, ny in seq:
                if 0.15 < y < 0.85 and ny > 0.5:
                    floor.append(y)
                    break
    meta["bagajZemin"] = round(sorted(floor)[len(floor) // 2], 3) if floor else 0.6
    for nm, o, d in (("bagajTavan", (0, meta["bagajZemin"] + 0.1, -1.7), (0, 1, 0)),
                     ("bagajSag", (0, meta["bagajZemin"] + 0.15, -1.75), (1, 0, 0)),
                     ("bagajOn", (0, meta["bagajZemin"] + 0.15, -1.75), (0, 0, 1)),
                     ("bagajArka", (0, meta["bagajZemin"] + 0.15, -1.75), (0, 0, -1))):
        p = ray(o, d)
        meta[nm] = round((p.y if nm == "bagajTavan" else p.x if nm == "bagajSag" else p.z), 3) if p is not None else None
    lo = Vector((1e9, 1e9, 1e9)); hi = -lo
    for v in verts:
        lo = Vector(map(min, lo, v)); hi = Vector(map(max, hi, v))
    meta["min"] = [round(a, 3) for a in lo]
    meta["max"] = [round(a, 3) for a in hi]
    print("Ölçüler:", meta)


def export_fbx(path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    root = bpy.data.objects.new("Egea", None)
    bpy.context.scene.collection.objects.link(root)
    for ob in mesh_objects():
        mw = ob.matrix_world.copy()
        ob.parent = root
        ob.matrix_world = mw
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for ob in root.children:
        ob.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False,
        add_leaf_bones=False, bake_anim=False, path_mode='AUTO', embed_textures=False)
    return root


def render(folder, lid_open):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except Exception:
            pass
    sc.render.resolution_x, sc.render.resolution_y = 1000, 620
    world = bpy.data.worlds.new("W")
    sc.world = world
    try:
        world.use_nodes = True
        nt = world.node_tree
        sky = nt.nodes.new("ShaderNodeTexSky")
        nt.links.new(sky.outputs["Color"], nt.nodes["Background"].inputs["Color"])
        nt.nodes["Background"].inputs["Strength"].default_value = 0.35
    except Exception:
        pass
    sun = bpy.data.objects.new("S", bpy.data.lights.new("S", 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))
    sc.collection.objects.link(sun)
    gm = bpy.data.meshes.new("G")
    gm.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [], [(0, 1, 2, 3)])
    sc.collection.objects.link(bpy.data.objects.new("G", gm))
    # Önizleme renkleri (Unity kendi malzemesini kullanır)
    tint = {"boya": (0.42, 0.012, 0.018, 1), "lastik": (0.03, 0.03, 0.03, 1)}
    for k, c in tint.items():
        m = bpy.data.materials.get(k)
        if m is not None and m.node_tree is not None:
            b = m.node_tree.nodes.get("Principled BSDF")
            if b is not None:
                b.inputs["Base Color"].default_value = c
                for l in list(b.inputs["Base Color"].links):
                    m.node_tree.links.remove(l)
    cam = bpy.data.objects.new("C", bpy.data.cameras.new("C"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.clip_start = 0.02
    lid = bpy.data.objects.get("Bagaj_Kapagi")
    views = [("on_sol", (-3.6, 1.25, 5.2), (0, 0.65, 0.3), 34, False),
             ("arka", (1.6, 1.3, -5.6), (0, 0.75, -0.8), 34, False),
             ("yan", (7.5, 0.8, 0), (0, 0.75, 0), 36, False),
             ("surucu", (-0.37, 1.15, -0.10), (-0.37, 0.95, 0.8), 70, False),
             ("bagaj_acik", (1.2, 1.6, -4.4), (0, 0.8, -1.6), 45, True),
             ("jant", (2.3, 0.45, 1.6), (0.75, 0.33, 1.37), 28, False)]
    for name, eye, tgt, fov, open_ in views:
        if lid is not None:
            # Blender'da araç x ekseni = -X; Unity'de +x etrafında +açı → burada -X etrafında
            lid.rotation_euler = (math.radians(-75) if (open_ and lid_open) else 0, 0, 0)
        e, t = blender(eye), blender(tgt)
        cam.location = e
        cam.rotation_mode = 'QUATERNION'
        cam.rotation_quaternion = (t - e).to_track_quat('-Z', 'Y')
        cam.data.sensor_fit = 'VERTICAL'
        cam.data.angle_y = math.radians(fov)
        sc.render.filepath = os.path.join(folder, name + ".png")
        bpy.ops.render.render(write_still=True)
    if lid is not None:
        lid.rotation_euler = (0, 0, 0)


def main():
    o = args()
    import time
    t0 = time.time()
    import_and_flatten(o["src"])
    remove_plates()
    groups = split_wheels()
    center, axis = split_steering()
    hinge = split_tailgate()
    rename_before = None
    decimate(float(o.get("ratio", "0.3")))
    merge_body()
    meta = {}
    fl = groups.get("Tekerlek_OnSol")
    meta["tekerYaricap"] = round(fl["r"], 4) if fl else 0.32
    if center is not None:
        meta["direksiyon"] = [round(a, 4) for a in center]
        meta["direksiyonEkseni"] = [round(a, 4) for a in axis]
    meta["mentese"] = [round(a, 4) for a in hinge]
    measure(meta)
    rename_materials()
    print("Süre: %.1f sn" % (time.time() - t0))
    if "render" in o:
        render(os.path.abspath(o["render"]), True)
    if "blend" in o:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(o["blend"]))
    if "fbx" in o:
        path = os.path.abspath(o["fbx"])
        export_fbx(path)
        with open(os.path.join(os.path.dirname(path), "Egea_ayar.json"), "w", encoding="utf-8") as f:
            json.dump(meta, f, ensure_ascii=False, indent=1)
        print("FBX:", path)
    print("Bitti: %.1f sn" % (time.time() - t0))


main()

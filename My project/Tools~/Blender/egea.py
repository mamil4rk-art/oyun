# -*- coding: utf-8 -*-
# =====================================================================
# Fiat Egea (sedan) görünümlü oyuncu arabası — Blender betiği.
#
# Çalıştırma (komut satırı):
#   blender -b --factory-startup --python egea.py -- --fbx <çıktı.fbx> [--blend <kayıt.blend>] [--render <klasör>]
#
# Araç uzayı (betik içi): x sağ, y yukarı, z ön (Unity ile aynı sayılar), metre.
# Blender'a geçerken B(x,y,z) = (-x, -z, y): araç Blender'da -Y'ye bakar, sürücü (sol) +X'tedir.
# FBX (ileri -Z, yukarı Y, dönüşüm gömülü) ile Unity'ye araç +z'ye bakar hâlde gelir.
# Malzeme adları Unity tarafındaki anahtarlarla aynıdır (Unity kendi malzemesiyle değiştirir).
# Logo / model yazısı / plaka yok.
# =====================================================================
import bpy
import math
import sys
import os
from mathutils import Vector
from mathutils.bvhtree import BVHTree

V = Vector

# ---------------------------------------------------------------------
# Ölçüler
# ---------------------------------------------------------------------
ZF, ZR = 2.266, -2.266            # tampon uçları (boy 4.532)
AXLE_F, AXLE_R = 1.346, -1.292    # aks aralığı 2.638
WHEEL_R = 0.316                   # 205/55 R16
TRACK_HALF = 0.765
ARCH_R, ARCH_Y = 0.372, 0.31
LID_Z0, LID_Z1 = -1.64, -2.17     # bagaj kapağı (menteşe önde)
CABIN_F, CABIN_R = 0.98, -1.64
GH_F, GH_R = 1.03, -1.69
TRUNK_FLOOR_Y, TRUNK_FRONT_Z, TRUNK_REAR_Z, TRUNK_HALF_W = 0.42, -1.30, -2.15, 0.80


class Curve:
    """Monoton kübik eğri (Fritsch–Carlson): anahtar noktalardan taşmadan yumuşak geçer."""

    def __init__(self, *xy):
        pts = sorted(zip(xy[0::2], xy[1::2]))
        self.x = [p[0] for p in pts]
        self.y = [p[1] for p in pts]
        n = len(pts)
        d = [(self.y[i + 1] - self.y[i]) / (self.x[i + 1] - self.x[i]) for i in range(n - 1)]
        m = [0.0] * n
        m[0], m[-1] = d[0], d[-1]
        for i in range(1, n - 1):
            m[i] = 0.0 if d[i - 1] * d[i] <= 0 else (d[i - 1] + d[i]) / 2
        for i in range(n - 1):
            if abs(d[i]) < 1e-9:
                m[i] = m[i + 1] = 0.0
                continue
            a, b = m[i] / d[i], m[i + 1] / d[i]
            s = a * a + b * b
            if s > 9:
                t = 3 / math.sqrt(s)
                m[i], m[i + 1] = t * a * d[i], t * b * d[i]
        self.m = m

    def __call__(self, x):
        xs = self.x
        if x <= xs[0]:
            return self.y[0]
        if x >= xs[-1]:
            return self.y[-1]
        lo, hi = 0, len(xs) - 1
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if xs[mid] <= x:
                lo = mid
            else:
                hi = mid
        h = xs[hi] - xs[lo]
        t = (x - xs[lo]) / h
        t2, t3 = t * t, t * t * t
        return ((2 * t3 - 3 * t2 + 1) * self.y[lo] + (t3 - 2 * t2 + t) * h * self.m[lo]
                + (-2 * t3 + 3 * t2) * self.y[hi] + (t3 - t2) * h * self.m[hi])


# Gövdenin üst kenar çizgisi (kaput kenarı → kemer hattı → bagaj kenarı)
TOP_EDGE = Curve(
    -2.266, 0.745, -2.248, 0.87, -2.22, 0.965, -2.17, 1.03, -2.05, 1.045, -1.8, 1.035,
    -1.5, 1.02, -0.5, 0.995, 0.5, 0.968, 1.0, 0.94, 1.6, 0.88, 2.0, 0.84, 2.12, 0.812,
    2.19, 0.765, 2.235, 0.685, 2.266, 0.58)
# Kaput / bagaj kubbesi
CROWN = Curve(
    -2.266, 0.008, -2.2, 0.02, -2.1, 0.03, -1.7, 0.03, -1.45, 0.02, 0.9, 0.02, 1.1, 0.035,
    2.1, 0.03, 2.2, 0.02, 2.266, 0.008)
# Yarım genişlik
HALF_W = Curve(
    -2.266, 0.70, -2.255, 0.78, -2.23, 0.83, -2.17, 0.86, -2.0, 0.878, -1.6, 0.889, 0.0, 0.896,
    1.6, 0.891, 2.0, 0.878, 2.16, 0.848, 2.22, 0.80, 2.25, 0.735, 2.266, 0.64)
# Marşpiyel / tampon alt kenarı
UNDER = Curve(
    -2.266, 0.42, -2.245, 0.36, -2.19, 0.30, -2.0, 0.245, -1.75, 0.19, 0.0, 0.17, 1.75, 0.18,
    2.05, 0.20, 2.19, 0.235, 2.245, 0.29, 2.266, 0.36)
# Tavan orta çizgisi
ROOF = Curve(
    -1.69, 1.0, -1.62, 1.06, -1.45, 1.17, -1.2, 1.31, -0.95, 1.41, -0.75, 1.46, -0.35, 1.49,
    0.0, 1.478, 0.12, 1.455, 0.35, 1.33, 0.65, 1.15, 0.9, 1.00, 1.03, 0.91)
# Tavan kenarı yarım genişliği
RAIL = Curve(-1.69, 0.74, -1.3, 0.68, -0.8, 0.655, -0.2, 0.645, 0.3, 0.655, 0.8, 0.70, 1.03, 0.76)


def belt(z):
    return TOP_EDGE(z) + 0.006


def arch_bottom(z):
    y = UNDER(z)
    for a in (AXLE_F, AXLE_R):
        dz = z - a
        if abs(dz) < ARCH_R:
            y = max(y, ARCH_Y + math.sqrt(ARCH_R * ARCH_R - dz * dz))
    return y


# ---------------------------------------------------------------------
# Kesit halkaları
# ---------------------------------------------------------------------
T_SKIP, T_BOTTOM, T_WELL, T_LIP, T_SIDE, T_SHOULDER, T_TOP = -1, 0, 1, 2, 3, 4, 5
G_SEAL, G_SIDE, G_PILLAR, G_TOP, G_BOTTOM = 10, 11, 12, 13, 14


def catmull_rom(p0, p1, p2, p3, t):
    t0 = 0.0
    t1 = t0 + math.sqrt((p1 - p0).length + 1e-5)
    t2 = t1 + math.sqrt((p2 - p1).length + 1e-5)
    t3 = t2 + math.sqrt((p3 - p2).length + 1e-5)
    tt = t1 + (t2 - t1) * t
    a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1
    a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2
    a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3
    b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2
    b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3
    return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2


class Ring:
    def __init__(self):
        self.p = []
        self.tag = []

    def add(self, pt, next_tag):
        self.p.append(V(pt))
        self.tag.append(next_tag)

    def chain(self, k, subs, tags, tag_after):
        n = len(k)
        for s in range(n - 1):
            p0 = 2 * k[0] - k[1] if s == 0 else k[s - 1]
            p3 = k[s + 2] if s + 2 < n else 2 * k[n - 1] - k[n - 2]
            for i in range(subs[s]):
                self.add(catmull_rom(p0, k[s], k[s + 1], p3, i / subs[s]), tags[s])
        self.add(k[n - 1], tag_after)


def M(p):
    return V((-p.x, p.y))


def lower_ring(z):
    """Alt gövde kesiti: taban, tekerlek yuvası, kemer dudağı, yan yüzey, keskin omuz çizgisi, üst kenar, kubbe."""
    w, yU, yb = HALF_W(z), UNDER(z), arch_bottom(z)
    top, crown = TOP_EDGE(z), CROWN(z)
    H = top - yU
    bx = min(0.60, w - 0.09)
    B0, B1, B2 = V((0, yU)), V((bx, yU)), V((bx, yb))
    S0 = V((w - 0.075, yb))
    S3 = V((w, top - min(0.09, 0.3 * H)))                                   # omuz çizgisi
    # Omuz çizgisinin altı hafif içe çekik, alt kısım (marşpiyel / tampon altı) içe doğru toplanır
    S2 = V((w - 0.03, min(max(yU + 0.42 * H, yb + 0.08), S3.y - 0.03)))
    S1 = V((w - 0.05, min(yb + 0.035, S2.y - 0.012)))
    S4 = V((w - 0.055, top))                                                # üst kenar
    S5 = V((0.55 * w, top + crown * 0.85))
    S6 = V((0, top + crown))
    r = Ring()
    r.add(B0, T_BOTTOM)
    r.add(B1, T_WELL)
    r.add(B2, T_WELL)
    r.add(S0, T_SKIP)
    r.chain([S0, S1, S2, S3], [3, 4, 4], [T_LIP, T_SIDE, T_SIDE], T_SKIP)
    r.chain([S3, S4, S5, S6, M(S5), M(S4), M(S3)], [4, 5, 5, 5, 5, 4],
            [T_SHOULDER, T_TOP, T_TOP, T_TOP, T_TOP, T_SHOULDER], T_SKIP)
    r.chain([M(S3), M(S2), M(S1), M(S0)], [4, 4, 3], [T_SIDE, T_SIDE, T_LIP], T_SKIP)
    r.add(M(S0), T_WELL)
    r.add(M(B2), T_WELL)
    r.add(M(B1), T_BOTTOM)
    return r


def gh_ring(z):
    """Cam / tavan kesiti: conta, yan cam (içe eğik), direk, tavan / ön cam / arka cam."""
    bl, y_top = belt(z), ROOF(z)
    h = max(y_top - bl, 0.0)
    bw, rw = HALF_W(z) - 0.085, RAIL(z)
    G0 = V((bw - 0.03, bl - 0.07))
    G1 = V((bw, bl - 0.012))
    G2 = V((rw, bl + h - min(0.07, 0.6 * h)))
    G3 = V((rw - 0.045, y_top - min(0.028, 0.3 * h)))
    G4 = V((rw * 0.5, y_top - 0.004))
    G5 = V((0, y_top))
    r = Ring()
    r.add(G0, G_SEAL)
    r.chain([G1, G2, G3, G4, G5, M(G4), M(G3), M(G2), M(G1)], [6, 3, 5, 5, 5, 5, 3, 6],
            [G_SIDE, G_PILLAR, G_TOP, G_TOP, G_TOP, G_TOP, G_PILLAR, G_SIDE], G_SEAL)
    r.add(M(G0), G_BOTTOM)
    return r


def rail_y(z):
    return gh_ring(z).p[7].y


# ---------------------------------------------------------------------
# Mesh toplayıcı
# ---------------------------------------------------------------------
class MB:
    """Araç uzayında köşe + yüz toplar. Yüzler 'dışarı' referansına göre yönlendirilir."""

    def __init__(self, name, pivot=(0, 0, 0), parent=None):
        self.name = name
        self.pivot = V(pivot)
        self.parent = parent
        self.v = []
        self.f = []
        self.fm = []
        self.mods = []          # (tip, ayarlar)

    def add(self, p):
        self.v.append(V(p))
        return len(self.v) - 1

    def newell(self, idx):
        n = V((0, 0, 0))
        for i in range(len(idx)):
            a, b = self.v[idx[i]], self.v[idx[(i + 1) % len(idx)]]
            n.x += (a.y - b.y) * (a.z + b.z)
            n.y += (a.z - b.z) * (a.x + b.x)
            n.z += (a.x - b.x) * (a.y + b.y)
        return n

    def face(self, idx, mat, out=None):
        idx = list(idx)
        if len(set(idx)) < 3:
            return
        if out is not None and self.newell(idx).dot(out) < 0:
            idx.reverse()
        self.f.append(idx)
        self.fm.append(mat)

    def vertex_normals(self):
        n = [V((0, 0, 0)) for _ in self.v]
        for f in self.f:
            fn = self.newell(f)
            for i in f:
                n[i] += fn
        return [x.normalized() if x.length > 1e-12 else V((0, 1, 0)) for x in n]


MESHES = {}
PIVOTS = {}
ANCHORS = {}


def mb(name, pivot=None, parent=None):
    if name not in MESHES:
        MESHES[name] = MB(name, pivot if pivot is not None else (0, 0, 0), parent)
    return MESHES[name]


# ---------------------------------------------------------------------
# Yüzeye yansıtma (BVH)
# ---------------------------------------------------------------------
HULL = None


def build_hull():
    """Yansıtma için kapalı gövde + cam bölümü (kabin üstü dahil)."""
    global HULL
    verts, polys = [], []

    def loft(zs, ring_fn):
        rings = [ring_fn(z) for z in zs]
        n = len(rings[0].p)
        base = len(verts)
        for z, r in zip(zs, rings):
            for p in r.p:
                verts.append(V((p.x, p.y, z)))
        for i in range(len(zs) - 1):
            for j in range(n):
                j2 = (j + 1) % n
                a, b = base + i * n + j, base + i * n + j2
                c, d = base + (i + 1) * n + j2, base + (i + 1) * n + j
                polys.append((a, b, c, d))
        return base, n

    zs = samples(ZR, ZF, lambda z: 0.004 if min(z - ZR, ZF - z) < 0.08 else 0.015)
    base, n = loft(zs, lower_ring)
    for i, z in ((0, zs[0]), (len(zs) - 1, zs[-1])):
        c = len(verts)
        ctr = V((0, 0, 0))
        for j in range(n):
            ctr += verts[base + i * n + j]
        verts.append(ctr / n)
        for j in range(n):
            polys.append((c, base + i * n + j, base + i * n + (j + 1) % n))
    loft(samples(GH_R, GH_F, lambda z: 0.01), gh_ring)
    HULL = BVHTree.FromPolygons(verts, polys, all_triangles=False)


def cast(o, d):
    hit = HULL.ray_cast(o, d, 4.0)
    return hit[0] if hit[0] is not None else None


def surf(frame, c):
    o, d = frame(c)
    p = cast(o, d)
    if p is None:
        return None, -d
    e = 0.004
    pts = []
    for dc in ((e, 0), (0, e), (-e, 0), (0, -e)):
        oo, dd = frame(V((c.x + dc[0], c.y + dc[1])))
        pts.append(cast(oo, dd))
    n = -d
    if all(q is not None for q in pts):
        nn = (pts[0] - pts[2]).cross(pts[1] - pts[3])
        if nn.length > 1e-12:
            nn.normalize()
            if nn.dot(d) > 0:
                nn = -nn
            n = nn
    return p, n


def front_frame(c):
    return V((c.x, c.y, ZF + 0.5)), V((0, 0, -1))


def rear_frame(c):
    return V((c.x, c.y, ZR - 0.5)), V((0, 0, 1))


def top_frame(c):
    return V((c.x, 2.2, c.y)), V((0, -1, 0))


def side_frame(s):
    return lambda c: (V((s * 1.4, c.y, c.x)), V((-s, 0, 0)))


def wrap_frame(ax, az, r_nom, s, rear):
    """Köşeyi saran parçalar için silindirik yansıtma: c.x = yay uzunluğu, c.y = yükseklik."""
    def f(c):
        th = c.x / r_nom
        dr = V((s * math.sin(th), 0, (-1 if rear else 1) * math.cos(th)))
        return V((ax, c.y, az)) + dr * 2, -dr
    return f


def decal(m, mat, frame, outline, offset, rings=5, skirt_mat=None, skirt_depth=0.004):
    """Yüzeye yapışık çıkartma: dış hattan merkeze halka halka ızgara, her köşe yüzeye yansıtılır."""
    outline = [V(p) for p in outline]
    c = V((0, 0))
    for p in outline:
        c += p
    c /= len(outline)
    n = len(outline)

    def vert(q, off):
        p, nrm = surf(frame, q)
        if p is None:
            o, d = frame(q)
            p = o + d * 0.5
        return m.add(p + nrm * off), nrm

    ci, cn = vert(c, offset)
    prev, prev_n = [], []
    for r in range(1, rings + 1):
        t = r / rings
        cur, cur_n = [], []
        for k in range(n):
            i, nn = vert(c + (outline[k] - c) * t, offset)
            cur.append(i)
            cur_n.append(nn)
        for k in range(n):
            k2 = (k + 1) % n
            if r == 1:
                m.face((ci, cur[k], cur[k2]), mat, cn + cur_n[k] + cur_n[k2])
            else:
                m.face((prev[k], cur[k], cur[k2], prev[k2]), mat, prev_n[k] + cur_n[k] + cur_n[k2])
        prev, prev_n = cur, cur_n
    if skirt_mat is None:
        return
    cpos = m.v[ci]
    low = [m.add(m.v[prev[k]] - prev_n[k] * (offset + skirt_depth)) for k in range(n)]
    for k in range(n):
        k2 = (k + 1) % n
        mid = (m.v[prev[k]] + m.v[prev[k2]]) / 2
        out = mid - cpos
        out -= prev_n[k] * out.dot(prev_n[k])
        m.face((prev[k], prev[k2], low[k2], low[k]), skirt_mat, out)


def strip(m, mat, pts, nrm, width, offset):
    n = len(pts)
    if n < 2:
        return
    pa = pb = None
    for i in range(n):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        b = nrm[i].cross(t).normalized() * (width / 2)
        p = pts[i] + nrm[i] * offset
        a, c = m.add(p - b), m.add(p + b)
        if i > 0:
            m.face((pa, a, c, pb), mat, nrm[i] + nrm[i - 1])
        pa, pb = a, c


def surface_line(m, mat, frame, poly, width, offset, step=0.01):
    poly = [V(p) for p in poly]
    pts, nrm = [], []
    for i in range(len(poly) - 1):
        k = max(1, math.ceil((poly[i + 1] - poly[i]).length / step))
        for j in range(k):
            p, n = surf(frame, poly[i].lerp(poly[i + 1], j / k))
            if p is not None:
                pts.append(p)
                nrm.append(n)
    p, n = surf(frame, poly[-1])
    if p is not None:
        pts.append(p)
        nrm.append(n)
    strip(m, mat, pts, nrm, width, offset)


# ---------------------------------------------------------------------
# Katı parçalar
# ---------------------------------------------------------------------
def spow(x, e):
    return math.copysign(abs(x) ** e, x)


def blob(m, mat, c, ax, ay, az, e_lat=0.5, e_lon=0.5, seg=16):
    """Süper elips cisim: e küçükse köşeli-yuvarlak kutu, 1 ise elipsoit."""
    c, ax, ay, az = V(c), V(ax), V(ay), V(az)
    lat, lon = seg // 2, seg
    idx = [[0] * (lon + 1) for _ in range(lat + 1)]
    for i in range(lat + 1):
        th = -math.pi / 2 + math.pi * i / lat
        for j in range(lon + 1):
            if j == lon:
                idx[i][j] = idx[i][0]
                continue
            ph = -math.pi + 2 * math.pi * j / lon
            x = spow(math.cos(th), e_lat) * spow(math.cos(ph), e_lon)
            y = spow(math.sin(th), e_lat)
            z = spow(math.cos(th), e_lat) * spow(math.sin(ph), e_lon)
            idx[i][j] = m.add(c + ax * x + ay * y + az * z)
    for i in range(lat):
        for j in range(lon):
            a, b, cc, d = idx[i][j], idx[i][j + 1], idx[i + 1][j + 1], idx[i + 1][j]
            m.face((a, b, cc, d), mat, (m.v[a] + m.v[cc]) / 2 - c)


def bar(m, mat, a, b, w_ax, w0, t0, w1, t1):
    """Uçları farklı kalınlıkta, düz yüzlü çubuk."""
    a, b, w_ax = V(a), V(b), V(w_ax)
    fwd = (b - a).normalized()
    wx = (w_ax - fwd * w_ax.dot(fwd)).normalized()
    tx = fwd.cross(wx).normalized()
    c = (a + b) / 2

    def P(o, w, t, sw, st):
        return o + wx * (sw * w / 2) + tx * (st * t / 2)
    A = [P(a, w0, t0, -1, -1), P(a, w0, t0, 1, -1), P(a, w0, t0, 1, 1), P(a, w0, t0, -1, 1)]
    Bq = [P(b, w1, t1, -1, -1), P(b, w1, t1, 1, -1), P(b, w1, t1, 1, 1), P(b, w1, t1, -1, 1)]

    def F(*ps):
        ids = [m.add(p) for p in ps]
        ctr = sum(ps, V((0, 0, 0))) / len(ps)
        m.face(ids, mat, ctr - c)
    for k in range(4):
        F(A[k], A[(k + 1) % 4], Bq[(k + 1) % 4], Bq[k])
    F(*A)
    F(*Bq)


def quad_out(m, mat, ps, out):
    m.face([m.add(p) for p in ps], mat, V(out))


def inner_box(m, mat, mn, mx):
    c = (V(mn) + V(mx)) / 2
    x0, y0, z0 = mn
    x1, y1, z1 = mx
    faces = [
        [(x0, y0, z0), (x1, y0, z0), (x1, y0, z1), (x0, y0, z1)],
        [(x0, y0, z0), (x0, y1, z0), (x0, y1, z1), (x0, y0, z1)],
        [(x1, y0, z0), (x1, y1, z0), (x1, y1, z1), (x1, y0, z1)],
        [(x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)],
        [(x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1)],
    ]
    for f in faces:
        ps = [V(p) for p in f]
        ctr = sum(ps, V((0, 0, 0))) / 4
        quad_out(m, mat, ps, c - ctr)


def revolve(m, c, axis, up, prof, mats, seg):
    """Eksen etrafında döndürülmüş profil (a = eksen boyu, r = yarıçap). Dış yüz (-dr, da) tarafı."""
    c, axis, up = V(c), V(axis), V(up)
    right = axis.cross(up).normalized()
    up = right.cross(axis).normalized()
    n = len(prof)
    idx = [[0] * (seg + 1) for _ in range(n)]
    for j in range(seg + 1):
        if j == seg:
            for i in range(n):
                idx[i][j] = idx[i][0]
            continue
        ang = 2 * math.pi * j / seg
        rad = up * math.cos(ang) + right * math.sin(ang)
        for i in range(n):
            idx[i][j] = m.add(c + axis * prof[i][0] + rad * prof[i][1])
    for i in range(n - 1):
        da, dr = prof[i + 1][0] - prof[i][0], prof[i + 1][1] - prof[i][1]
        if da * da + dr * dr < 1e-12 or mats[i] is None:
            continue
        for j in range(seg):
            ang = 2 * math.pi * (j + 0.5) / seg
            rad = up * math.cos(ang) + right * math.sin(ang)
            m.face((idx[i][j], idx[i][j + 1], idx[i + 1][j + 1], idx[i + 1][j]), mats[i], axis * (-dr) + rad * da)


def torus(m, mat, c, axis, up, R, r, seg=48, tube=12):
    c, axis, up = V(c), V(axis), V(up)
    right = axis.cross(up).normalized()
    up = right.cross(axis).normalized()
    idx = [[0] * (tube + 1) for _ in range(seg + 1)]
    for i in range(seg + 1):
        a = 2 * math.pi * (i % seg) / seg
        rad = up * math.cos(a) + right * math.sin(a)
        for j in range(tube + 1):
            if i == seg:
                idx[i][j] = idx[0][j]
                continue
            if j == tube:
                idx[i][j] = idx[i][0]
                continue
            b = 2 * math.pi * j / tube
            idx[i][j] = m.add(c + rad * (R + r * math.cos(b)) + axis * (r * math.sin(b)))
    for i in range(seg):
        a = 2 * math.pi * (i + 0.5) / seg
        ctr = c + (up * math.cos(a) + right * math.sin(a)) * R
        for j in range(tube):
            q = (idx[i][j], idx[i + 1][j], idx[i + 1][j + 1], idx[i][j + 1])
            m.face(q, mat, (m.v[q[0]] + m.v[q[2]]) / 2 - ctr)


def round_rect(cx, cy, hw, hh, r, seg=4):
    r = min(r, hw, hh)
    cs = [(hw - r, hh - r), (-hw + r, hh - r), (-hw + r, -hh + r), (hw - r, -hh + r)]
    out = []
    for k in range(4):
        for i in range(seg + 1):
            a = math.radians(k * 90 + 90 * i / seg)
            out.append(V((cx + cs[k][0] + math.cos(a) * r, cy + cs[k][1] + math.sin(a) * r)))
    return out


def circle(cx, cy, r, seg=24):
    return [V((cx + math.cos(2 * math.pi * i / seg) * r, cy + math.sin(2 * math.pi * i / seg) * r)) for i in range(seg)]


def dense(poly, step=0.02):
    poly = [V(p) for p in poly]
    out = []
    for i in range(len(poly)):
        a, b = poly[i], poly[(i + 1) % len(poly)]
        k = max(1, math.ceil((b - a).length / step))
        for j in range(k):
            out.append(a.lerp(b, j / k))
    return out


def expand(poly, d):
    poly = [V(p) for p in poly]
    c = sum(poly, V((0, 0))) / len(poly)
    out = []
    n = len(poly)
    for i in range(n):
        a, p, b = poly[i - 1], poly[i], poly[(i + 1) % n]
        e1, e2 = (p - a).normalized(), (b - p).normalized()
        nn = (V((e1.y, -e1.x)) + V((e2.y, -e2.x))).normalized()
        if nn.dot(p - c) < 0:
            nn = -nn
        out.append(p + nn * d)
    return out


def strip2(line, half_w):
    line = [V(p) for p in line]
    top, bot = [], []
    for i in range(len(line)):
        t = (line[min(i + 1, len(line) - 1)] - line[max(i - 1, 0)]).normalized()
        nn = V((-t.y, t.x))
        top.append(line[i] + nn * half_w)
        bot.append(line[i] - nn * half_w)
    return top + bot[::-1]


def samples(z0, z1, step, *breaks):
    out = []
    z = z0
    while z < z1:
        out.append(z)
        z += step(z)
    out.append(z1)
    out += [b for b in breaks if z0 < b < z1]
    out.sort()
    res = []
    for z in out:
        if not res or z - res[-1] > 0.0015:
            res.append(z)
    return res


def anchor(frame, c):
    p, n = surf(frame, V(c))
    if p is None:
        o, d = frame(V(c))
        p, n = o + d, -d
    return p, n


def emblem(m, frame, c, r, off):
    decal(m, "krom", frame, circle(c[0], c[1], r, 32), off, 3, "krom", 0.004)
    decal(m, "cerceve", frame, circle(c[0], c[1], r * 0.8, 32), off + 0.0015, 3)
    decal(m, "krom", frame, circle(c[0], c[1], r * 0.35, 24), off + 0.0025, 2)


# =====================================================================
# PARÇALAR
# =====================================================================
def loft(zs, ring_fn, pick, cap_front=False, cap_rear=False, cap_mat="boya", cap_mesh="Govde"):
    rings = [ring_fn(z) for z in zs]
    n = len(rings[0].p)
    maps = {}

    def vid(m, i, j):
        d = maps.setdefault(m.name, {})
        key = i * n + j
        if key not in d:
            p = rings[i].p[j]
            d[key] = m.add(V((p.x, p.y, zs[i])))
        return d[key]

    for i in range(len(zs) - 1):
        zm = (zs[i] + zs[i + 1]) / 2
        for j in range(n):
            tag = rings[i].tag[j]
            if tag == T_SKIP:
                continue
            sel = pick(zm, tag)
            if sel is None:
                continue
            m, mat = sel
            j2 = (j + 1) % n
            a, b, c, d = vid(m, i, j), vid(m, i, j2), vid(m, i + 1, j2), vid(m, i + 1, j)
            out = (m.v[b] - m.v[a]).cross(m.v[d] - m.v[a])
            if out.length < 1e-12:
                out = (m.v[c] - m.v[d]).cross(m.v[d] - m.v[a])
            m.face((a, b, c, d), mat, out)

    def cap(i, direction):
        m = mb(cap_mesh)
        pts = [V((p.x, p.y, zs[i])) for p in rings[i].p]
        ctr = sum(pts, V((0, 0, 0))) / n
        ci = m.add(ctr)
        for j in range(n):
            a, b = m.add(pts[j]), m.add(pts[(j + 1) % n])
            m.face((ci, a, b), cap_mat, direction)
    if cap_front:
        cap(len(zs) - 1, V((0, 0, 1)))
    if cap_rear:
        cap(0, V((0, 0, -1)))


def build_shell():
    body = mb("Govde")
    lid_top = TOP_EDGE(LID_Z0) + CROWN(LID_Z0)
    PIVOTS["Bagaj_Kapagi"] = V((0, lid_top, LID_Z0))
    lid = mb("Bagaj_Kapagi", PIVOTS["Bagaj_Kapagi"])

    def step(z):
        e = min(z - ZR, ZF - z)
        if e < 0.06:
            return 0.004
        if e < 0.16:
            return 0.01
        if abs(z - AXLE_F) < ARCH_R + 0.02 or abs(z - AXLE_R) < ARCH_R + 0.02:
            return 0.02
        return 0.04
    zs = samples(ZR, ZF, step, LID_Z0, LID_Z1, CABIN_F, CABIN_R, AXLE_F - ARCH_R, AXLE_F + ARCH_R,
                 AXLE_R - ARCH_R, AXLE_R + ARCH_R, AXLE_F, AXLE_R)

    def pick(zm, tag):
        if tag in (T_BOTTOM, T_WELL):
            return body, "alt"
        if tag == T_TOP:
            if LID_Z1 < zm < LID_Z0:
                return lid, "boya"
            if CABIN_R < zm < CABIN_F:
                return None             # kabin üstü açık: camdan iç görünsün
        return body, "boya"
    loft(zs, lower_ring, pick, True, True)

    # Bagaj kapağının iç yüzü
    nrm = lid.vertex_normals()
    base = {}
    for f in list(lid.f):
        ids = []
        for i in f:
            if i not in base:
                base[i] = lid.add(lid.v[i] - nrm[i] * 0.012)
            ids.append(base[i])
        c = sum((lid.v[i] for i in f), V((0, 0, 0))) / len(f)
        lid.face(ids, "boya", -(nrm[f[0]] + nrm[f[1]] + nrm[f[2]]))

    # Cam / tavan
    gh = mb("Kabin")
    zg = samples(GH_R, GH_F, lambda z: 0.03, 0.10, -0.80, 0.82, -0.02, -0.11, -0.93, -0.98, -1.42)

    def pick_g(zm, tag):
        if tag == G_SEAL:
            return gh, "conta"
        if tag == G_BOTTOM:
            return None
        if tag == G_PILLAR:
            return gh, "boya"
        if tag == G_TOP:
            if zm > 0.10 or zm < -0.80:
                return gh, "cam"
            return gh, "boya"
        if tag == G_SIDE:
            if zm > 0.82:
                return gh, "cerceve"     # ayna köşesi
            if zm > -0.02:
                return gh, "cam"         # ön kapı camı
            if zm > -0.11:
                return gh, "cerceve"     # B direği
            if zm > -0.93:
                return gh, "cam"         # arka kapı camı
            if zm > -0.98:
                return gh, "cerceve"
            if zm > -1.42:
                return gh, "cam"         # çeyrek cam
            return gh, "boya"            # C direği
        return None
    loft(zg, gh_ring, pick_g)

    # Krom çıtalar: kemer hattı + yan cam üst çerçevesi
    trim = mb("Krom_Citalar")
    for s in (-1, 1):
        pts, nrm = [], []
        z = -1.38
        while z <= 0.84:
            pts.append(V((s * (HALF_W(z) - 0.07), belt(z) + 0.004, z)))
            nrm.append(V((s * 0.25, 1, 0)).normalized())
            z += 0.02
        strip(trim, "krom", pts, nrm, 0.034, 0.0)
        pts, nrm = [], []
        z = -1.16
        while z <= 0.80:
            r = gh_ring(z)
            p, pa, pb = r.p[7], r.p[6], r.p[8]
            t = (pb - pa).normalized()
            nn = V((t.y, -t.x))
            pts.append(V((s * p.x, p.y, z)))
            nrm.append(V((s * nn.x, nn.y, 0)).normalized())
            z += 0.01
        strip(trim, "krom", pts, nrm, 0.012, 0.003)

    # C direği: çeyrek camın arka kenarı eğik
    for s in (-1, 1):
        cp = [V((-1.38, belt(-1.38) - 0.02)), V((-1.17, rail_y(-1.17) + 0.012))]
        z = -1.22
        while z >= -1.66:
            cp.append(V((z, rail_y(z) + 0.012)))
            z -= 0.04
        z = -1.66
        while z <= -1.42:
            cp.append(V((z, belt(z) - 0.02)))
            z += 0.04
        decal(gh, "boya", side_frame(s), dense(cp, 0.015), 0.0015, 4, "boya", 0.003)

    # Kaput / ön cam arası (silecek havuzu)
    bar(body, "plastikMat", (-0.80, 0.95, 0.962), (0.80, 0.95, 0.962), (0, 0, 1), 0.07, 0.02, 0.07, 0.02)


def build_cabin():
    inn = mb("Ic_Mekan")
    zs = samples(CABIN_R - 0.02, CABIN_F + 0.01, lambda z: 0.05)
    rows = []
    for z in zs:
        w, top = HALF_W(z), TOP_EDGE(z)
        bw = w - 0.085
        F = [V((-(bw - 0.05), top - 0.012)), V((-(w - 0.075), top - 0.012)), V((-(w - 0.06), 0.70)),
             V((-(w - 0.07), 0.42)), V((-(w - 0.12), 0.30)), V((0, 0.30)),
             V((w - 0.12, 0.30)), V((w - 0.07, 0.42)), V((w - 0.06, 0.70)), V((w - 0.075, top - 0.012)),
             V((bw - 0.05, top - 0.012))]
        rows.append([inn.add(V((p.x, p.y, z))) for p in F])
    n = len(rows[0])
    for i in range(len(zs) - 1):
        for j in range(n - 1):
            pa, pb = inn.v[rows[i][j]], inn.v[rows[i][j + 1]]
            mid = (pa + pb) / 2
            floor = 3 <= j <= 6
            flange = j == 0 or j == n - 2
            if floor:
                inward, mat = V((0, 1, 0)), "hali"
            elif flange:
                inward, mat = V((0, 1, 0)), "conta"   # yukarıdan, cam aralığından bakınca siyah
            else:
                inward, mat = V((-mid.x, 0.75 - mid.y, 0)), "icPlastik"
            inn.face((rows[i][j], rows[i][j + 1], rows[i + 1][j + 1], rows[i + 1][j]), mat, inward)

    # Ön bölme (motor tarafını kapatır)
    bar(inn, "icPlastik", (0, 0.30, CABIN_F - 0.01), (0, 0.94, CABIN_F - 0.01), (1, 0, 0), 1.62, 0.04, 1.62, 0.04)

    # Torpido, gösterge kaputu, ekranlar, havalandırmalar
    blob(inn, "torpido", (0, 0.80, 0.78), (0.80, 0, 0), (0, 0.17, 0), (0, 0, 0.22), 0.35, 0.3, 20)
    blob(inn, "torpido", (-0.37, 0.97, 0.62), (0.19, 0, 0), (0, 0.06, 0), (0, 0, 0.10), 0.5, 0.4)
    blob(inn, "ekran", (-0.37, 0.93, 0.56), (0.15, 0, 0), (0, 0.05, 0), (0, 0, 0.012), 0.3, 0.3)
    scr_up = V((0, 0.95, 0.3)).normalized()
    blob(inn, "ekran", (0, 0.95, 0.56), (0.10, 0, 0), scr_up * 0.06, V((1, 0, 0)).cross(scr_up) * 0.012, 0.25, 0.25)
    for vx in (-0.70, -0.13, 0.13, 0.70):
        blob(inn, "cerceve", (vx, 0.86, 0.565), (0.045, 0, 0), (0, 0.03, 0), (0, 0, 0.01), 0.4, 0.4)

    # Orta konsol + vites
    blob(inn, "icPlastik", (0, 0.45, 0.30), (0.12, 0, 0), (0, 0.16, 0), (0, 0, 0.42), 0.3, 0.3)
    blob(inn, "deri", (0, 0.62, 0.38), (0.05, 0, 0), (0, 0.04, 0), (0, 0, 0.06), 0.8, 0.8)
    bar(inn, "krom", (0, 0.62, 0.38), (0, 0.72, 0.37), (1, 0, 0), 0.012, 0.012, 0.012, 0.012)
    blob(inn, "deri", (0, 0.74, 0.37), (0.025, 0, 0), (0, 0.03, 0), (0, 0, 0.03), 0.9, 0.9)

    # Direksiyon (ayrı nesne: sürüşte döner). colAxis: simitten sürücüye bakan eksen.
    col = V((0, 0.42, -0.91)).normalized()
    wc = V((-0.37, 0.93, 0.50))
    PIVOTS["Direksiyon"] = wc
    ANCHORS["Direksiyon_Ekseni"] = (wc, col)
    sw = mb("Direksiyon", wc)
    right = V((1, 0, 0))
    sw_up = right.cross(col).normalized()
    torus(sw, "deri", wc, col, (0, 1, 0), 0.183, 0.017)
    blob(sw, "icPlastik", wc - col * 0.01, right * 0.075, sw_up * 0.065, col * 0.035, 0.5, 0.5)
    for d in (right, -right, -sw_up):                       # sol, sağ, alt kol
        bar(sw, "icPlastik", wc + d * 0.06, wc + d * 0.17, col.cross(d), 0.04, 0.018, 0.03, 0.016)
    bar(inn, "icPlastik", wc - col * 0.03, wc - col * 0.25, right, 0.07, 0.07, 0.09, 0.09)   # mil torpidoya

    # Koltuklar
    for sx in (-0.37, 0.37):
        blob(inn, "kumas", (sx, 0.47, 0.02), (0.25, 0, 0), (0, 0.075, 0), (0, 0, 0.26), 0.35, 0.3)
        back = V((0, 0.96, -0.27)).normalized()
        bz = right.cross(back)
        blob(inn, "kumas", (sx, 0.83, -0.30), (0.25, 0, 0), back * 0.32, bz * 0.075, 0.35, 0.3)
        blob(inn, "kumas", (sx, 1.22, -0.40), (0.13, 0, 0), back * 0.09, bz * 0.055, 0.5, 0.45)
        for dx in (-0.07, 0.07):
            bar(inn, "krom", (sx + dx, 1.10, -0.37), (sx + dx, 1.16, -0.385), right, 0.01, 0.01, 0.01, 0.01)
        bar(inn, "icPlastik", (sx, 0.34, 0.02), (sx, 0.40, 0.02), right, 0.4, 0.45, 0.4, 0.45)
    blob(inn, "kumas", (0, 0.47, -0.86), (0.68, 0, 0), (0, 0.085, 0), (0, 0, 0.26), 0.35, 0.3)
    rb = V((0, 0.94, -0.34)).normalized()
    rbz = right.cross(rb)
    blob(inn, "kumas", (0, 0.80, -1.14), (0.68, 0, 0), rb * 0.30, rbz * 0.08, 0.35, 0.3)
    for sx in (-0.42, 0.42):
        blob(inn, "kumas", (sx, 1.15, -1.27), (0.12, 0, 0), rb * 0.07, rbz * 0.05, 0.5, 0.45)

    # Arka çamurluk iç kılıfı: yüzler sadece içe bakar, dışarıdan lastik görünür kalır
    for s in (-1, 1):
        x0, x1, y0, y1, z0, z1 = s * 0.60, s * 0.84, 0.29, 0.74, AXLE_R - 0.40, AXLE_R + 0.40
        quad_out(inn, "hali", [V((x0, y0, z0)), V((x0, y1, z0)), V((x0, y1, z1)), V((x0, y0, z1))], (-s, 0, 0))
        quad_out(inn, "hali", [V((x0, y1, z0)), V((x1, y1, z0)), V((x1, y1, z1)), V((x0, y1, z1))], (0, 1, 0))
        quad_out(inn, "hali", [V((x0, y0, z1)), V((x1, y0, z1)), V((x1, y1, z1)), V((x0, y1, z1))], (0, 0, 1))
        quad_out(inn, "hali", [V((x0, y0, z0)), V((x1, y0, z0)), V((x1, y1, z0)), V((x0, y1, z0))], (0, 0, -1))

    # Tavan döşemesi
    hl = mb("Tavan_Dosemesi")
    prev = None
    for z in samples(-0.80, 0.10, lambda z: 0.05):
        r = gh_ring(z)
        cur = [hl.add(V((p.x * 0.985, p.y - 0.018, z))) for p in r.p]
        if prev is not None:
            for j in range(7, len(r.p) - 8):
                hl.face((prev[j], prev[j + 1], cur[j + 1], cur[j]), "tavan", V((0, -1, 0)))
        prev = cur

    ANCHORS["Surucu_Goz"] = (V((-0.37, 1.17, -0.08)), V((0, 0, 1)))


def build_trunk():
    tr = mb("Bagaj")
    rim = TOP_EDGE(-1.9) - 0.015
    inner_box(tr, "hali", (-TRUNK_HALF_W, TRUNK_FLOOR_Y, TRUNK_REAR_Z), (TRUNK_HALF_W, rim, TRUNK_FRONT_Z))
    zs = samples(LID_Z1 - 0.01, LID_Z0 - 0.03, lambda z: 0.03)
    for s in (-1, 1):
        for i in range(len(zs) - 1):
            za, zb = zs[i], zs[i + 1]
            quad_out(tr, "conta", [V((s * (TRUNK_HALF_W - 0.01), rim, za)), V((s * (HALF_W(za) - 0.06), rim, za)),
                                   V((s * (HALF_W(zb) - 0.06), rim, zb)), V((s * (TRUNK_HALF_W - 0.01), rim, zb))], (0, 1, 0))
    x = HALF_W(LID_Z1) - 0.06
    quad_out(tr, "conta", [V((-x, rim, TRUNK_REAR_Z + 0.01)), V((x, rim, TRUNK_REAR_Z + 0.01)),
                           V((x, rim, LID_Z1 - 0.005)), V((-x, rim, LID_Z1 - 0.005))], (0, 1, 0))
    shelf_y = TOP_EDGE(-1.5) - 0.02
    bar(tr, "hali", (0, shelf_y, TRUNK_FRONT_Z + 0.02), (0, shelf_y, LID_Z0 - 0.03), (1, 0, 0), 1.62, 0.02, 1.62, 0.02)
    ANCHORS["Bagaj_Zemini"] = (V((0, TRUNK_FLOOR_Y, -1.72)), V((0, 1, 0)))


def build_front():
    fr = mb("On")
    lamps = mb("Farlar")
    # Alt hava girişi: ızgaranın hemen altından başlayan geniş yamuk
    intake = [(-0.50, 0.565), (0.50, 0.565), (0.53, 0.50), (0.44, 0.30), (-0.44, 0.30), (-0.53, 0.50)]
    decal(fr, "plastikMat", front_frame, dense(expand(intake, 0.018)), 0.002, 6)
    decal(fr, "petek", front_frame, dense(intake), 0.0035, 6, "plastikMat", 0.004)
    decal(fr, "plastikMat", front_frame, dense([(-0.62, 0.25), (0.62, 0.25), (0.58, 0.215), (-0.58, 0.215)]), 0.003, 3)

    grille = [(-0.33, 0.765), (0.33, 0.765), (0.30, 0.715), (0.26, 0.685), (-0.26, 0.685), (-0.30, 0.715)]
    decal(fr, "krom", front_frame, dense(expand(grille, 0.012)), 0.004, 5, "krom", 0.006)
    decal(fr, "petek", front_frame, dense(grille), 0.0055, 5)
    decal(fr, "krom", front_frame, dense([(-0.29, 0.731), (0.29, 0.731), (0.29, 0.719), (-0.29, 0.719)]), 0.0075, 2)
    emblem(fr, front_frame, (0, 0.727), 0.048, 0.01)

    for s in (-1, 1):
        hf = wrap_frame(s * 0.30, 1.72, 0.6, s, False)
        hl = [(0.01, 0.672), (0.01, 0.758), (0.14, 0.788), (0.30, 0.803), (0.46, 0.806), (0.58, 0.798),
              (0.64, 0.786), (0.57, 0.772), (0.46, 0.748), (0.34, 0.712), (0.20, 0.676), (0.09, 0.664)]
        hd = dense(hl, 0.012)
        decal(lamps, "farGovde", hf, hd, 0.002, 7)
        for pc in ((0.14, 0.725), (0.28, 0.748)):
            decal(lamps, "reflektor", hf, dense(circle(pc[0], pc[1], 0.034, 24), 0.008), 0.0045, 3, "reflektor", 0.004)
            decal(lamps, "farGovde", hf, dense(circle(pc[0], pc[1], 0.022, 20), 0.008), 0.0065, 2, "reflektor", 0.002)
            decal(lamps, "farLens", hf, dense(circle(pc[0], pc[1], 0.017, 16), 0.008), 0.0085, 2)
        decal(lamps, "farDRL", hf, dense(strip2([(0.04, 0.760), (0.17, 0.783), (0.33, 0.794), (0.48, 0.795), (0.58, 0.788)], 0.006), 0.01), 0.006, 2)
        decal(lamps, "amber", hf, dense(strip2([(0.40, 0.752), (0.50, 0.766), (0.57, 0.778)], 0.008), 0.01), 0.0055, 2)
        decal(lamps, "farLens", hf, hd, 0.012, 6, "cerceve", 0.004)

        fog = V((s * 0.67, 0.41))
        housing = [fog + V((o[0] * s, o[1])) for o in ((-0.10, 0.075), (0.095, 0.10), (0.115, -0.03), (0.07, -0.095), (-0.08, -0.09), (-0.12, -0.01))]
        decal(fr, "plastikMat", front_frame, dense(housing, 0.012), 0.0025, 4, "plastikMat", 0.004)
        decal(fr, "reflektor", front_frame, dense(circle(fog.x, fog.y, 0.042, 24), 0.008), 0.005, 3, "krom", 0.004)
        decal(fr, "farLens", front_frame, dense(circle(fog.x, fog.y, 0.036, 24), 0.008), 0.009, 2)
        arc = []
        for i in range(15):
            a = math.radians(-120 + 240 * i / 14)
            arc.append(fog + V((math.cos(a) * 0.062 * s, math.sin(a) * 0.062)))
        surface_line(fr, "krom", front_frame, arc, 0.009, 0.0045, 0.006)

    # Kaput çizgisi
    hood = []
    z = CABIN_F + 0.005
    while z <= 2.04:
        hood.append(V((HALF_W(z) - 0.11, z)))
        z += 0.04
    hood += [V((0.72, 2.10)), V((0.45, 2.135)), V((0, 2.145))]
    full = hood + [V((-p.x, p.y)) for p in reversed(hood[:-1])]
    surface_line(fr, "aralik", top_frame, full, 0.0045, 0.0012)
    # Tampon ↔ çamurluk birleşimi
    for s in (-1, 1):
        surface_line(fr, "aralik", side_frame(s), [(1.93, 0.80), (1.84, 0.74), (AXLE_F + ARCH_R + 0.035, 0.66)], 0.004, 0.0012)


def build_rear():
    rr = mb("Arka")
    lamps = mb("Stoplar")
    for s in (-1, 1):
        tf = wrap_frame(s * 0.40, -1.85, 0.5, s, True)
        # Büyük, köşeyi saran stop: iç ucu plaka yuvasına kadar, alt kenarı dışa doğru yükselir
        tl = [(-0.13, 0.835), (-0.13, 0.995), (0.10, 1.010), (0.36, 1.016), (0.54, 1.010), (0.70, 0.992),
              (0.68, 0.955), (0.52, 0.912), (0.32, 0.862), (0.10, 0.838)]
        td = dense(tl, 0.012)
        decal(lamps, "stopIc", tf, td, 0.002, 7)
        decal(lamps, "stopKirmizi", tf, dense(strip2([(-0.07, 0.976), (0.14, 0.992), (0.40, 0.997), (0.62, 0.986)], 0.010), 0.01), 0.0045, 2)
        decal(lamps, "stopKirmizi", tf, dense(strip2([(-0.05, 0.880), (0.16, 0.888), (0.38, 0.915), (0.56, 0.950)], 0.007), 0.01), 0.0045, 2)
        decal(lamps, "stopBeyaz", tf, dense(round_rect(0.06, 0.933, 0.09, 0.02, 0.008), 0.01), 0.004, 2)
        decal(lamps, "stopLens", tf, td, 0.009, 6, "cerceve", 0.004)
        decal(rr, "stopKirmizi", rear_frame, dense(round_rect(s * 0.66, 0.50, 0.07, 0.009, 0.008), 0.01), 0.003, 2, "plastikMat", 0.003)

    diff = [(-0.70, 0.455), (0.70, 0.455), (0.64, 0.30), (-0.64, 0.30)]
    decal(rr, "plastikMat", rear_frame, dense(diff), 0.0025, 5)
    for s in (-1, 1):
        decal(rr, "krom", rear_frame, dense(round_rect(s * 0.47, 0.365, 0.09, 0.035, 0.02), 0.01), 0.005, 3, "krom", 0.004)
        decal(rr, "alt", rear_frame, dense(round_rect(s * 0.47, 0.365, 0.075, 0.022, 0.014), 0.01), 0.0065, 2)
        for k in (-1, 0, 1):
            decal(rr, "plastikMat", rear_frame, dense(round_rect(s * (0.17 + 0.07 * k), 0.35, 0.006, 0.045, 0.004), 0.01), 0.006, 2, "plastikMat", 0.004)

    # Bagaj kapağı çizgisi
    poly = []
    x = -0.80
    while x <= 0.80:
        poly.append(V((x, LID_Z0 - 0.003)))
        x += 0.05
    z = LID_Z0 - 0.05
    while z >= LID_Z1:
        poly.append(V((HALF_W(z) - 0.058, z)))
        z -= 0.05
    x = 0.80
    while x >= -0.80:
        poly.append(V((min(x, HALF_W(LID_Z1) - 0.058), LID_Z1 + 0.003)))
        x -= 0.05
    z = LID_Z1 + 0.05
    while z <= LID_Z0:
        poly.append(V((-(HALF_W(z) - 0.058), z)))
        z += 0.05
    poly.append(poly[0])
    surface_line(rr, "aralik", top_frame, poly, 0.0045, 0.0012)

    # Tampon üst birleşim çizgisi
    bump = []
    x = -0.86
    while x <= 0.861:
        bump.append(V((x, 0.765)))
        x += 0.04
    surface_line(rr, "aralik", rear_frame, bump, 0.004, 0.0012)
    for s in (-1, 1):
        surface_line(rr, "aralik", side_frame(s), [(-2.17, 0.765), (-1.95, 0.745), (AXLE_R - ARCH_R - 0.035, 0.66)], 0.004, 0.0012)
    # Plaka yuvası (plaka takılı değil, yuvanın kenarı görünür)
    surface_line(rr, "aralik", rear_frame, round_rect(0, 0.885, 0.27, 0.07, 0.02) + [round_rect(0, 0.885, 0.27, 0.07, 0.02)[0]], 0.004, 0.0012)
    emblem(rr, rear_frame, (0, 0.985), 0.04, 0.006)


def build_sides():
    sd = mb("Yanlar")
    for s in (-1, 1):
        f = side_frame(s)

        def by(z):
            return belt(z) - 0.004
        surface_line(sd, "aralik", f, [(0.975, by(0.975)), (0.960, 0.62), (0.955, 0.27), (-0.88, 0.27)], 0.0045, 0.0012)
        surface_line(sd, "aralik", f, [(-0.06, by(-0.06)), (-0.065, 0.27)], 0.0045, 0.0012)
        rd = [(-0.99, by(-0.99)), (-0.99, 0.575)]
        for i in range(1, 9):
            zz = -0.99 + (AXLE_R + 0.40 + 0.99) * i / 8
            dz = zz - AXLE_R
            rd.append((zz, ARCH_Y + math.sqrt(max(0.0, 0.16 - dz * dz))))
        rd.append((AXLE_R + 0.40, 0.27))
        surface_line(sd, "aralik", f, rd, 0.0045, 0.0012)

        for hz in (0.22, -0.78):
            p, n = anchor(f, (hz, 0.85))
            decal(sd, "cerceve", f, dense(round_rect(hz, 0.85, 0.075, 0.024, 0.02), 0.01), 0.0012, 3)
            blob(sd, "boya", p + n * 0.011, (0, 0, 0.075), (0, 0.016, 0), n * 0.011, 0.35, 0.45, 16)

        mbase = V((s * 0.845, belt(0.86) + 0.03, 0.865))
        blob(sd, "cerceve", mbase, (0.045, 0, 0), (0, 0.03, 0), (0, 0, 0.06), 0.6, 0.5, 14)
        # Ayna kapağı: dışa doğru incelen, gövde renginde; camı arkaya bakar
        mc = V((s * 0.975, belt(0.86) + 0.08, 0.84))
        mx = V((s, 0, 0.18)).normalized()
        mz = mx.cross(V((0, 1, 0))).normalized() * s
        blob(sd, "boya", mc, mx * 0.118, (0, 0.068, 0), mz * 0.056, 0.6, 0.5, 28)
        blob(sd, "aynaCam", mc - mz * 0.053 + mx * 0.004, mx * 0.102, (0, 0.055, 0), mz * 0.003, 0.3, 0.3, 16)
        bar(sd, "cerceve", mbase + V((s * 0.02, 0.005, 0)), mc + V((-s * 0.05, -0.015, 0.005)), (0, 1, 0), 0.035, 0.04, 0.025, 0.03)

    decal(sd, "boya", side_frame(1), dense(round_rect(-1.55, 0.83, 0.085, 0.072, 0.03), 0.01), 0.0015, 3, "aralik", 0.003)

    # Silecekler
    for x0, x1, z0, z1 in ((-0.72, 0.02, 0.905, 0.865), (0.10, 0.70, 0.875, 0.905)):
        pts, nrm = [], []
        for i in range(31):
            t = i / 30
            p, n = surf(top_frame, V((x0 + (x1 - x0) * t, z0 + (z1 - z0) * t)))
            if p is not None:
                pts.append(p)
                nrm.append(n)
        strip(sd, "plastikMat", pts, nrm, 0.012, 0.012)
        strip(sd, "plastikMat", pts, nrm, 0.006, 0.02)
        if len(pts) > 2:
            mid = pts[len(pts) // 2] + nrm[len(pts) // 2] * 0.025
            piv = V(((x0 + x1) / 2 - 0.15, 0.965, 0.955))
            bar(sd, "plastikMat", piv, mid, (0, 1, 0), 0.014, 0.01, 0.01, 0.008)

    ap, an = anchor(top_frame, (0, -0.72))
    blob(sd, "cerceve", ap + an * 0.012 + V((0, 0, -0.02)), (0.022, 0, 0), (0, 0.035, 0), (0, 0, 0.075), 0.7, 0.6, 14)


def build_wheel(name, hub, side, front):
    hub = V(hub)
    PIVOTS[name] = hub
    spin_name = name + "_Donen"
    PIVOTS[spin_name] = hub
    w = mb(spin_name, hub, parent=name)
    ax = V((side, 0, 0))
    up = V((0, 1, 0))

    tp = [(-0.092, 0.205), (-0.100, 0.225), (-0.104, 0.255), (-0.101, 0.282), (-0.094, 0.300), (-0.083, 0.311), (-0.070, 0.316)]
    for g in (-0.052, -0.018, 0.018, 0.052):
        tp += [(g - 0.0055, 0.316), (g - 0.0045, 0.307), (g + 0.0045, 0.307), (g + 0.0055, 0.316)]
    tp += [(0.070, 0.316), (0.083, 0.311), (0.094, 0.300), (0.101, 0.282), (0.104, 0.255), (0.100, 0.225), (0.092, 0.205)]
    tm = ["lastikSirt" if tp[i][1] >= 0.306 and tp[i + 1][1] >= 0.306 else "lastik" for i in range(len(tp) - 1)]
    revolve(w, hub, ax, up, tp, tm, 96)

    rp = [(0.075, 0.186), (-0.090, 0.186), (-0.095, 0.210), (0.085, 0.210), (0.092, 0.222), (0.100, 0.215),
          (0.100, 0.200), (0.094, 0.189), (0.075, 0.186)]
    revolve(w, hub, ax, up, rp, ["jantIc", "jantIc", None, "jant", "jant", "jant", "jant", "jant"], 96)
    revolve(w, hub, ax, up, [(0.050, 0.075), (0.066, 0.040), (0.074, 0.030), (0.075, 0.0)], ["jant"] * 3, 48)
    revolve(w, hub, ax, up, [(0.0765, 0.022), (0.0765, 0.0)], ["cerceve"], 32)
    revolve(w, hub, ax, up, [(0.0755, 0.026), (0.078, 0.024), (0.078, 0.020)], ["krom", "krom"], 32)
    for k in range(4):
        a = math.radians(45 + 90 * k)
        rad = V((0, math.cos(a), math.sin(a)))
        blob(w, "krom", hub + rad * 0.049 + ax * 0.064, ax * 0.006, (0, 0.009, 0), (0, 0, 0.009), 0.6, 0.3, 10)
    for k in range(5):
        for off in (-5.5, 5.5):
            a = math.radians(k * 72 + off)
            rad = V((0, math.cos(a), math.sin(a)))
            tan = V((0, -math.sin(a), math.cos(a)))
            ao = math.radians(k * 72 + off * 1.15)
            rad_o = V((0, math.cos(ao), math.sin(ao)))
            tan_o = V((0, -math.sin(ao), math.cos(ao)))
            bar(w, "jant", hub + rad * 0.066 + ax * 0.060, hub + rad_o * 0.192 + ax * 0.087, (tan + tan_o).normalized(), 0.030, 0.026, 0.026, 0.018)
    revolve(w, hub, ax, up, [(-0.018, 0.10), (-0.018, 0.142), (-0.040, 0.142), (-0.040, 0.10), (-0.018, 0.10)], ["disk"] * 4, 48)
    revolve(w, hub, ax, up, [(0.03, 0.10), (0.03, 0.0)], ["disk"], 24)

    cal = mb(name + "_Kaliper", hub, parent=name)
    ca = math.radians(-50 if front else -40)
    cr = V((0, math.cos(ca), math.sin(ca)))
    ct = V((0, -math.sin(ca), math.cos(ca)))
    blob(cal, "kaliper", hub + cr * 0.128 - ax * 0.029, ax * 0.026, ct * 0.055, cr * 0.03, 0.4, 0.4, 14)


def build_all():
    build_hull()
    build_shell()
    build_cabin()
    build_trunk()
    build_front()
    build_rear()
    build_sides()
    for s in (-1, 1):
        build_wheel("Tekerlek_OnSol" if s < 0 else "Tekerlek_OnSag", (s * TRACK_HALF, WHEEL_R, AXLE_F), s, True)
        build_wheel("Tekerlek_ArkaSol" if s < 0 else "Tekerlek_ArkaSag", (s * TRACK_HALF, WHEEL_R, AXLE_R), s, False)


# =====================================================================
# BLENDER SAHNESİ
# =====================================================================
def B(p):
    """Araç uzayı → Blender: (x, y, z) → (-x, -z, y)."""
    return V((-p[0], -p[2], p[1]))


# Önizleme malzemeleri (Unity bunları aynı isimli kendi malzemeleriyle değiştirir)
MATS = {
    "boya": dict(c=(0.42, 0.012, 0.018), r=0.25, m=0.4, coat=1.0),
    "alt": dict(c=(0.02, 0.02, 0.02), r=0.85),
    "cam": dict(c=(0.03, 0.04, 0.045), r=0.03, alpha=0.55),
    "cerceve": dict(c=(0.01, 0.01, 0.011), r=0.08),
    "krom": dict(c=(0.85, 0.86, 0.88), r=0.06, m=1.0),
    "plastikMat": dict(c=(0.03, 0.03, 0.032), r=0.7),
    "petek": dict(c=(0.015, 0.015, 0.016), r=0.45),
    "farGovde": dict(c=(0.08, 0.08, 0.09), r=0.12, m=0.9),
    "farLens": dict(c=(1, 1, 1), r=0.0, alpha=0.08),
    "farDRL": dict(c=(0.9, 0.9, 0.92), r=0.25),
    "amber": dict(c=(0.95, 0.42, 0.04), r=0.15),
    "reflektor": dict(c=(0.85, 0.86, 0.88), r=0.05, m=1.0),
    "stopIc": dict(c=(0.15, 0.005, 0.008), r=0.15, m=0.3),
    "stopKirmizi": dict(c=(0.7, 0.02, 0.02), r=0.15),
    "stopBeyaz": dict(c=(0.85, 0.85, 0.88), r=0.1, m=0.6),
    "stopLens": dict(c=(0.6, 0.01, 0.02), r=0.02, alpha=0.45),
    "lastik": dict(c=(0.03, 0.03, 0.03), r=0.7),
    "lastikSirt": dict(c=(0.025, 0.025, 0.025), r=0.85),
    "jant": dict(c=(0.62, 0.63, 0.65), r=0.22, m=0.9),
    "jantIc": dict(c=(0.25, 0.26, 0.28), r=0.45, m=0.8),
    "disk": dict(c=(0.45, 0.45, 0.45), r=0.35, m=1.0),
    "kaliper": dict(c=(0.1, 0.1, 0.11), r=0.5, m=0.5),
    "aynaCam": dict(c=(0.9, 0.92, 0.95), r=0.02, m=1.0),
    "icPlastik": dict(c=(0.04, 0.04, 0.045), r=0.7),
    "torpido": dict(c=(0.05, 0.05, 0.055), r=0.7),
    "deri": dict(c=(0.04, 0.04, 0.04), r=0.55),
    "kumas": dict(c=(0.08, 0.08, 0.09), r=0.9),
    "tavan": dict(c=(0.45, 0.45, 0.42), r=0.9),
    "hali": dict(c=(0.04, 0.04, 0.04), r=0.95),
    "ekran": dict(c=(0.005, 0.005, 0.008), r=0.05),
    "conta": dict(c=(0.02, 0.02, 0.02), r=0.8),
    "aralik": dict(c=(0.005, 0.002, 0.002), r=0.9),
}


def get_mat(key):
    m = bpy.data.materials.get(key)
    if m is not None:
        return m
    d = MATS.get(key, dict(c=(1, 0, 1), r=0.5))
    m = bpy.data.materials.new(key)
    try:
        m.use_nodes = True
    except Exception:
        pass
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    col = d["c"]
    m.diffuse_color = (col[0], col[1], col[2], d.get("alpha", 1.0))
    if bsdf is not None:
        bsdf.inputs["Base Color"].default_value = (col[0], col[1], col[2], 1)
        bsdf.inputs["Roughness"].default_value = d.get("r", 0.5)
        bsdf.inputs["Metallic"].default_value = d.get("m", 0.0)
        if "coat" in d:
            for nm in ("Coat Weight", "Coat"):
                if nm in bsdf.inputs:
                    bsdf.inputs[nm].default_value = d["coat"]
                    break
            if "Coat Roughness" in bsdf.inputs:
                bsdf.inputs["Coat Roughness"].default_value = 0.03
        if "alpha" in d:
            bsdf.inputs["Alpha"].default_value = d["alpha"]
            try:
                m.surface_render_method = 'BLENDED'
            except Exception:
                try:
                    m.blend_method = 'BLEND'
                except Exception:
                    pass
            try:
                m.use_backface_culling = False
            except Exception:
                pass
    return m


def make_objects():
    coll = bpy.context.scene.collection
    root = bpy.data.objects.new("Egea", None)
    coll.objects.link(root)
    objs = {"": root}
    for name, m in MESHES.items():
        if not m.f:
            continue
        piv = m.pivot
        verts = [B(p - piv) for p in m.v]
        faces = [f[::-1] for f in m.f]          # B bir yansıma: sarım yönü ters çevrilir
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], faces)
        keys = []
        for k in m.fm:
            if k not in keys:
                keys.append(k)
        for k in keys:
            me.materials.append(get_mat(k))
        me.polygons.foreach_set("material_index", [keys.index(k) for k in m.fm])
        me.polygons.foreach_set("use_smooth", [True] * len(m.f))
        me.validate(clean_customdata=False)
        me.update()
        # Kutu izdüşümlü UV (metre cinsinden): Unity'deki dokular (petek, kumaş, lastik) otursun
        uvl = me.uv_layers.new(name="UVMap")
        uv = []
        verts_co = [v.co + B(piv) for v in me.vertices]
        loop_vi = [0] * len(me.loops)
        me.loops.foreach_get("vertex_index", loop_vi)
        for poly in me.polygons:
            n = poly.normal
            ax = 0 if abs(n.x) >= abs(n.y) and abs(n.x) >= abs(n.z) else (1 if abs(n.y) >= abs(n.z) else 2)
            for li in poly.loop_indices:
                co = verts_co[loop_vi[li]]
                if ax == 0:
                    uv += (co.y, co.z)
                elif ax == 1:
                    uv += (co.x, co.z)
                else:
                    uv += (co.x, co.y)
        uvl.data.foreach_set("uv", uv)
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        # Hiyerarşi düz tutulur (FBX'te iç içe dönüşümler sorun çıkarabiliyor):
        # her parça kökün doğrudan çocuğu, dönme noktası parçanın kendi merkezi (origin).
        ob.parent = root
        ob.location = B(piv)
        objs[name] = ob
    return root


def setup_render():
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE", "BLENDER_EEVEE_NEXT"):
        try:
            sc.render.engine = eng
            break
        except Exception:
            continue
    sc.render.resolution_x, sc.render.resolution_y = 1200, 740
    sc.render.film_transparent = False
    try:
        sc.eevee.taa_render_samples = 48
    except Exception:
        pass
    try:
        sc.view_settings.view_transform = 'AgX'
    except Exception:
        pass

    world = bpy.data.worlds.new("Dunya")
    sc.world = world
    try:
        world.use_nodes = True
    except Exception:
        pass
    nt = world.node_tree
    bg = nt.nodes.get("Background")
    try:
        sky = nt.nodes.new("ShaderNodeTexSky")
        nt.links.new(sky.outputs["Color"], bg.inputs["Color"])
        bg.inputs["Strength"].default_value = 0.35
    except Exception:
        bg.inputs["Color"].default_value = (0.6, 0.7, 0.85, 1)

    sun_d = bpy.data.lights.new("Gunes", 'SUN')
    sun_d.energy = 3.5
    sun_d.angle = math.radians(2)
    sun = bpy.data.objects.new("Gunes", sun_d)
    sc.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35))

    # Zemin + arkadan yumuşak ışık
    gm = bpy.data.meshes.new("Zemin")
    gm.from_pydata([(-30, -30, 0), (30, -30, 0), (30, 30, 0), (-30, 30, 0)], [], [(0, 1, 2, 3)])
    g = bpy.data.objects.new("Zemin", gm)
    gmat = bpy.data.materials.new("zemin")
    try:
        gmat.use_nodes = True
    except Exception:
        pass
    gb = gmat.node_tree.nodes.get("Principled BSDF")
    gb.inputs["Base Color"].default_value = (0.22, 0.22, 0.22, 1)
    gb.inputs["Roughness"].default_value = 0.6
    gm.materials.append(gmat)
    sc.collection.objects.link(g)
    return g, sun


VIEWS = [
    ("1_on_sol", (-3.6, 1.25, 5.2), (0, 0.65, 0.3), 34),
    ("2_on_sag", (4.4, 1.0, 3.6), (0, 0.65, 0.1), 34),
    ("3_arka", (1.6, 1.3, -5.6), (0, 0.75, -0.8), 34),
    ("4_yan", (7.5, 0.8, 0), (0, 0.75, 0), 36),
    ("5_on", (0, 0.85, 7.5), (0, 0.7, 0), 18),
    ("6_arka_duz", (0, 1.0, -7.5), (0, 0.75, 0), 18),
    ("8_far_yakin", (1.4, 1.0, 3.2), (0.45, 0.75, 2.1), 30),
    ("9_jant", (2.4, 0.45, 1.5), (0.8, 0.33, 1.35), 28),
    ("11_surucu", (-0.37, 1.17, -0.08), (-0.37, 0.98, 0.8), 62),
]


def render_views(folder, only=None):
    os.makedirs(folder, exist_ok=True)
    sc = bpy.context.scene
    cam_d = bpy.data.cameras.new("Kamera")
    cam = bpy.data.objects.new("Kamera", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam_d.clip_start = 0.02
    for name, eye, target, fov in VIEWS:
        if only and name not in only:
            continue
        e, t = B(eye), B(target)
        cam.location = e
        cam.rotation_mode = 'QUATERNION'
        cam.rotation_quaternion = (t - e).to_track_quat('-Z', 'Y')
        # fov: dikey açı (önizleme çizicisiyle aynı) → Blender yatay sensöre göre
        aspect = sc.render.resolution_x / sc.render.resolution_y
        cam_d.sensor_fit = 'VERTICAL'
        cam_d.angle_y = math.radians(fov)
        sc.render.filepath = os.path.join(folder, name + ".png")
        bpy.ops.render.render(write_still=True)


def export_fbx(path, root):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    for ob in bpy.context.scene.objects:
        ob.select_set(False)
    keep = [root] + list(root.children_recursive)
    for ob in keep:
        ob.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        use_mesh_modifiers=True, mesh_smooth_type='OFF', use_tspace=False,
        add_leaf_bones=False, bake_anim=False, path_mode='AUTO', embed_textures=False)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    opts = {}
    i = 0
    while i < len(argv):
        if argv[i].startswith("--") and i + 1 < len(argv):
            opts[argv[i][2:]] = argv[i + 1]
            i += 2
        else:
            i += 1

    bpy.ops.wm.read_factory_settings(use_empty=True)
    import time
    t0 = time.time()
    build_all()
    print("Geometri: %.1f sn" % (time.time() - t0))
    root = make_objects()
    tris = sum(len(m.f) for m in MESHES.values())
    print("Nesne: %d, yüz: %d" % (len(MESHES), tris))

    if "blend" in opts:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(opts["blend"]))
    if "fbx" in opts:
        export_fbx(os.path.abspath(opts["fbx"]), root)
        print("FBX:", opts["fbx"])
    if "render" in opts:
        setup_render()
        only = opts.get("views")
        render_views(os.path.abspath(opts["render"]), only.split(",") if only else None)
    print("Bitti: %.1f sn" % (time.time() - t0))


main()

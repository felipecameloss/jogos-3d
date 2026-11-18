"""
Ícones dos itens: um modelinho low-poly por item (mesmo estilo dos insetos: cores
chapadas, sem textura) renderizado em 256x256 com fundo transparente e contorno escuro.

Uso:
    blender --background --python Tools/Blender/build_item_icons.py [-- NomeDoItem ...]

Gera:
    Assets/Art/Icons/<Item>.png           (nome = nome do asset em ScriptableObjects/Items)
    Tools/Blender/previews/ItemIcons.png  folha de contato com todos (fundo cinza)
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ant_common as ac  # noqa: E402
from ant_common import ellipsoid, tube  # noqa: E402

OUT = os.path.join(ac.ROOT, "Assets", "Art", "Icons")
SHEET = os.path.join(HERE, "previews", "ItemIcons.png")
SIZE = 256
OUTLINE = (0.07, 0.05, 0.05)
VIEW_DIR = Vector((0.55, -1.0, 0.8)).normalized()  # de onde a câmera olha (3/4 de cima)

MATS = {}


def mat(name, color, roughness=0.5, metallic=0.0):
    if name not in MATS:
        MATS[name] = ac.material(f"Icon_{name}", color, roughness, metallic)
    return MATS[name]


def obj(name, build, materials, smooth=False, matrix=None):
    """Cria um objeto: build(bm, M) monta a malha; M(nome) devolve o índice do material."""
    names = list(materials)
    bm = bmesh.new()
    build(bm, lambda n: names.index(n))
    if matrix is not None:
        bmesh.ops.transform(bm, matrix=matrix, verts=list(bm.verts))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for p in mesh.polygons:
        p.use_smooth = smooth
    for n in names:
        mesh.materials.append(materials[n])
    o = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(o)
    return o


def new_faces(bm, before):
    return [f for f in bm.faces if f not in before]


def set_mat(faces, idx):
    for f in faces:
        f.material_index = idx


def add(bm, idx, fn, *args, **kw):
    """Roda uma primitiva (ellipsoid/tube/...) e pinta as faces novas com `idx`."""
    before = set(bm.faces)
    fn(bm, *args, **kw)
    set_mat(new_faces(bm, before), idx)


def cone(bm, base, tip, r1, r2, segs=8):
    """Cone/cilindro entre dois pontos."""
    base, tip = Vector(base), Vector(tip)
    d = tip - base
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    m = Matrix.Translation((base + tip) / 2) @ rot
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r1, radius2=r2, depth=d.length, matrix=m)


def teardrop(bm, center, radius, point=1.3):
    """Gota: esfera com o topo puxado em ponta."""
    geom = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=9, radius=1.0)
    for v in geom["verts"]:
        if v.co.z > 0:
            k = v.co.z
            v.co.x *= 1 - 0.85 * k ** 1.5
            v.co.y *= 1 - 0.85 * k ** 1.5
            v.co.z *= 1 + point * k
        v.co = Vector(center) + v.co * radius


def ring(center, radius, n, axis="Z", start=0.0):
    pts = []
    for i in range(n + 1):
        a = start + 2 * math.pi * i / n
        c, s = math.cos(a) * radius, math.sin(a) * radius
        p = Vector((c, s, 0)) if axis == "Z" else Vector((c, 0, s)) if axis == "Y" else Vector((0, c, s))
        pts.append(Vector(center) + p)
    return pts


def tilt(rx=0, ry=0, rz=0):
    return (Matrix.Rotation(math.radians(rz), 4, "Z") @ Matrix.Rotation(math.radians(ry), 4, "Y")
            @ Matrix.Rotation(math.radians(rx), 4, "X"))


# ================================================================ itens

def asa_de_mosca():
    m = {"membrana": mat("WingMembrane", (0.78, 0.9, 1.0), 0.25), "veia": mat("WingVein", (0.22, 0.24, 0.32), 0.6)}

    def build(bm, M):
        L, W = 2.2, 0.75
        top, bot = [], []
        for i in range(1, 13):
            t = i / 12
            x = L * t
            top.append(bm.verts.new((x, W * math.sin(math.pi * t) ** 0.6 * (1 - 0.25 * t), 0.05 * math.sin(t * 3))))
            bot.append(bm.verts.new((x, -0.55 * W * math.sin(math.pi * t) ** 0.8, 0.05 * math.sin(t * 3))))
        base = bm.verts.new((0, 0, 0))
        outline = top + list(reversed(bot))
        for a, b in zip(outline, outline[1:] + outline[:1]):
            f = bm.faces.new((base, a, b)); f.material_index = M("membrana")
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.02)
        for tip in [(2.0, 0.25), (1.6, 0.55), (1.1, 0.62), (1.8, -0.2), (1.2, -0.35)]:
            add(bm, M("veia"), tube, [Vector((0.05, 0, 0.04)), Vector((tip[0] * 0.5, tip[1] * 0.45, 0.05)), Vector((tip[0], tip[1], 0.05))],
                [0.035, 0.022, 0.012], 4)
        add(bm, M("veia"), ellipsoid, Vector((0, 0, 0.02)), (0.12, 0.09, 0.07), 6, 4)

    obj("AsaDeMosca", build, m, matrix=tilt(rx=48, rz=20))


def cabeca_de_fosforo():
    m = {"palito": mat("MatchStick", (0.95, 0.8, 0.52), 0.7), "cabeca": mat("MatchHead", (0.88, 0.08, 0.05), 0.45)}

    def build(bm, M):
        add(bm, M("palito"), cone, (0, 0, -1.1), (0, 0, 0.9), 0.09, 0.09, 6)
        add(bm, M("cabeca"), ellipsoid, Vector((0, 0, 1.0)), (0.18, 0.18, 0.26), 10, 7)

    obj("CabecaDeFosforo", build, m, matrix=tilt(ry=-38, rz=20))


def caco_de_lente():
    m = {"vidro": mat("LensGlass", (0.3, 0.72, 0.95), 0.1), "borda": mat("LensEdge", (0.75, 0.94, 1.0), 0.1),
         "brilho": mat("LensShine", (1, 1, 1), 0.1)}

    def build(bm, M):
        pts = [(-1.0, -0.55), (0.15, -0.8), (1.05, 0.05), (0.05, 1.05), (-0.35, 0.2)]  # caco pontudo
        bot = [bm.verts.new((x, y, 0)) for x, y in pts]
        top = [bm.verts.new((x * 0.96, y * 0.96, 0.16)) for x, y in pts]
        f = bm.faces.new(top); f.material_index = M("vidro")
        f = bm.faces.new(list(reversed(bot))); f.material_index = M("vidro")
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            f = bm.faces.new((bot[i], bot[j], top[j], top[i])); f.material_index = M("borda")
        # reflexo
        for quad in ([(-0.55, -0.3), (0.05, 0.45), (0.15, 0.38), (-0.45, -0.37)],
                     [(0.2, -0.35), (0.55, 0.05), (0.62, 0.0), (0.27, -0.4)]):
            f = bm.faces.new([bm.verts.new((x, y, 0.17)) for x, y in quad]); f.material_index = M("brilho")

    obj("CacoDeLente", build, m, matrix=tilt(rx=30, rz=10))


def cafe_derramado():
    m = {"xicara": mat("Cup", (0.97, 0.95, 0.9), 0.4), "faixa": mat("CupBand", (0.85, 0.25, 0.15), 0.4),
         "cafe": mat("Coffee", (0.36, 0.18, 0.07), 0.25), "espuma": mat("Foam", (0.78, 0.6, 0.4), 0.5)}

    def build(bm, M):
        # xícara tombada, boca virada para a poça
        cup = Matrix.Translation((-0.45, 0.2, 0.42)) @ tilt(ry=78)
        before = set(bm.faces)
        bmesh.ops.create_cone(bm, cap_ends=True, segments=12, radius1=0.42, radius2=0.52, depth=1.0, matrix=cup)
        set_mat(new_faces(bm, before), M("xicara"))
        add(bm, M("faixa"), lambda b: bmesh.ops.create_cone(b, cap_ends=False, segments=12, radius1=0.5, radius2=0.53,
                                                              depth=0.18, matrix=cup @ Matrix.Translation((0, 0, 0.12))))
        add(bm, M("cafe"), lambda b: bmesh.ops.create_cone(b, cap_ends=True, segments=12, radius1=0.44, radius2=0.44,
                                                             depth=0.02, matrix=cup @ Matrix.Translation((0, 0, 0.5))))
        # alça
        add(bm, M("xicara"), tube, [cup @ Vector((0.0, 0.5, 0.25)), cup @ Vector((0.0, 0.78, 0.0)), cup @ Vector((0.0, 0.5, -0.25))],
            [0.07, 0.07, 0.07], 6)
        # poça + gotas
        rng = random.Random(3)
        before = set(bm.faces)
        verts = []
        for i in range(16):
            a = 2 * math.pi * i / 16
            r = 0.9 * (1 + 0.25 * math.sin(a * 3 + 1) + rng.uniform(-0.08, 0.08))
            verts.append(bm.verts.new((0.6 + math.cos(a) * r * 1.1, -0.1 + math.sin(a) * r * 0.8, 0.02)))
        bm.faces.new(verts)
        set_mat(new_faces(bm, before), M("cafe"))
        add(bm, M("espuma"), ellipsoid, Vector((0.25, 0.05, 0.05)), (0.25, 0.18, 0.04), 8, 4)
        for p in [(1.75, 0.5), (1.6, -0.75), (-0.2, -0.85)]:
            add(bm, M("cafe"), ellipsoid, Vector((p[0], p[1], 0.04)), (0.1, 0.1, 0.05), 6, 4)

    obj("CafeDerramado", build, m)


def casca_de_amendoim():
    m = {"casca": mat("PeanutShell", (0.86, 0.66, 0.38), 0.8), "covinha": mat("PeanutDimple", (0.6, 0.4, 0.2), 0.8)}

    def build(bm, M):
        before = set(bm.faces)
        geom = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=14, radius=1.0)
        for v in geom["verts"]:
            v.co = Matrix.Rotation(math.radians(90), 3, "Y") @ v.co  # polos em X (comprido)
            x = v.co.x
            waist = 1.0 - 0.32 * math.exp(-(x * x) / 0.06)  # cintura no meio
            bulge = 1.0 + 0.06 * (1 if x < 0 else -1)
            v.co.y *= 0.42 * waist * bulge
            v.co.z *= 0.4 * waist * bulge
            v.co.x *= 1.05
        bm.normal_update()
        for f in new_faces(bm, before):
            c = f.calc_center_median()
            a = math.atan2(c.z, c.y)
            # frisos ao longo do comprimento + covinhas em xadrez
            ridge = math.sin(a * 6) > 0.55
            dimple = math.sin(c.x * 14) * math.sin(a * 6) > 0.45
            f.material_index = M("covinha") if ridge or dimple else M("casca")

    obj("CascaDeAmendoim", build, m, matrix=tilt(rz=30, rx=-15))


def casca_de_semente():
    m = {"preto": mat("SeedBlack", (0.1, 0.1, 0.11), 0.5), "listra": mat("SeedStripe", (0.9, 0.9, 0.86), 0.5)}

    def build(bm, M):
        before = set(bm.faces)
        geom = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=10, radius=1.0)
        for v in geom["verts"]:
            v.co.x *= 0.36
            v.co.z *= 0.18
            v.co.y *= 0.8
            if v.co.y > 0:  # ponta
                v.co.x *= 1 - 0.55 * (v.co.y / 0.8)
                v.co.z *= 1 - 0.4 * (v.co.y / 0.8)
        bm.normal_update()
        for f in new_faces(bm, before):
            c = f.calc_center_median()
            f.material_index = M("listra") if abs(math.sin(c.x * 26)) > 0.75 and c.z > -0.05 else M("preto")

    obj("CascaDeSemente", build, m, matrix=tilt(rz=-35, rx=-10))


def casco_de_tatuzinho():
    m = {"a": mat("PillBugA", (0.48, 0.53, 0.64), 0.45), "b": mat("PillBugB", (0.36, 0.4, 0.5), 0.45)}

    def build(bm, M):
        n = 7
        for i in range(n):
            t = (i / (n - 1)) * 2 - 1
            w = 0.62 * (1 - 0.35 * t * t)
            before = set(bm.faces)
            geom = bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=1.0)
            for v in geom["verts"]:
                v.co.x *= w
                v.co.y *= 0.2
                v.co.z *= 0.42 * (1 - 0.3 * t * t)
                if v.co.z < 0:
                    v.co.z *= 0.25
                v.co.y += t * 0.95
                v.co.z += 0.05 * (1 - abs(t))
            set_mat(new_faces(bm, before), M("a") if i % 2 == 0 else M("b"))

    obj("CascoDeTatuzinho", build, m, matrix=tilt(rz=70))


def clipe_de_papel():
    m = {"metal": mat("Clip", (0.72, 0.76, 0.82), 0.3, 0.5)}

    def arc(c, r, a0, a1, n=8):
        return [Vector((c[0] + math.cos(math.radians(a0 + (a1 - a0) * k / n)) * r,
                        c[1] + math.sin(math.radians(a0 + (a1 - a0) * k / n)) * r, 0)) for k in range(n + 1)]

    def build(bm, M):
        pts = [Vector((-0.3, -0.5, 0))] + arc((0, 0.9), 0.3, 180, 0) + arc((0.05, -1.0), 0.25, 0, -180) \
            + arc((-0.05, 0.6), 0.15, 180, 0) + [Vector((0.1, -0.4, 0))]
        # remove pontos repetidos/colados
        clean = [pts[0]]
        for p in pts[1:]:
            if (p - clean[-1]).length > 0.04:
                clean.append(p)
        add(bm, M("metal"), tube, clean, [0.045] * len(clean), 6)

    obj("ClipeDePapel", build, m, matrix=tilt(rx=48, rz=-25))


def cola_branca():
    m = {"frasco": mat("GlueBottle", (0.97, 0.97, 0.95), 0.4), "rotulo": mat("GlueLabel", (0.15, 0.4, 0.9), 0.45),
         "tampa": mat("GlueCap", (1.0, 0.55, 0.08), 0.4)}

    def build(bm, M):
        before = set(bm.faces)
        ellipsoid(bm, Vector((0, 0, 0)), (0.42, 0.42, 0.7), 12, 9)
        bm.normal_update()
        for f in new_faces(bm, before):
            f.material_index = M("rotulo") if -0.25 < f.calc_center_median().z < 0.2 else M("frasco")
        add(bm, M("tampa"), cone, (0, 0, 0.62), (0, 0, 1.25), 0.24, 0.05, 10)
        add(bm, M("frasco"), teardrop, (0.33, -0.25, 0.35), 0.1, 0.6)  # pingo

    obj("ColaBranca", build, m, matrix=tilt(ry=-12, rz=15))


def espinho_de_rosa():
    m = {"caule": mat("RoseStem", (0.25, 0.58, 0.15), 0.5), "espinho": mat("Thorn", (0.72, 0.16, 0.1), 0.45)}

    def build(bm, M):
        add(bm, M("caule"), cone, (-1.1, 0, 0), (1.1, 0, 0), 0.18, 0.18, 10)
        pts = [Vector((-0.15, 0, 0.1)), Vector((0.05, 0, 0.5)), Vector((0.35, 0, 0.85)), Vector((0.7, 0, 1.0))]
        add(bm, M("espinho"), tube, pts, [0.3, 0.17, 0.07, 0.008], 8)

    obj("EspinhoDeRosa", build, m, matrix=tilt(rz=20))


def ferrao_de_abelha():
    m = {"amarelo": mat("BeeYellow", (1.0, 0.78, 0.08), 0.4), "preto": mat("BeeBlack", (0.1, 0.08, 0.06), 0.45),
         "ferrao": mat("Stinger", (0.25, 0.22, 0.2), 0.3)}

    def build(bm, M):
        before = set(bm.faces)
        ellipsoid(bm, Vector((0, 0, 0)), (0.5, 0.75, 0.5), 12, 10, poles_y=True)
        bm.normal_update()
        for f in new_faces(bm, before):
            y = f.calc_center_median().y
            f.material_index = M("preto") if math.sin(y * 7.5) > 0.2 else M("amarelo")
        add(bm, M("ferrao"), cone, (0, -0.68, 0), (0, -1.45, 0), 0.1, 0.0, 8)

    obj("FerraoDeAbelha", build, m, matrix=tilt(rx=20, rz=-40))


def gota_de_orvalho():
    m = {"agua": mat("Dew", (0.5, 0.8, 1.0), 0.1), "brilho": mat("DewShine", (1, 1, 1), 0.1)}

    def build(bm, M):
        add(bm, M("agua"), teardrop, (0, 0, 0), 0.75)
        add(bm, M("brilho"), ellipsoid, Vector((-0.3, -0.5, 0.25)), (0.1, 0.06, 0.18), 6, 4)

    obj("GotaDeOrvalho", build, m, smooth=True)


def gotinha_de_mel():
    m = {"mel": mat("Honey", (1.0, 0.58, 0.04), 0.15), "brilho": mat("HoneyShine", (1.0, 0.95, 0.75), 0.1),
         "favo": mat("Comb", (0.96, 0.78, 0.3), 0.6)}

    def build(bm, M):
        add(bm, M("favo"), cone, (0, 0, -0.75), (0, 0, -0.6), 0.85, 0.85, 6)
        add(bm, M("mel"), teardrop, (0, 0, 0), 0.62)
        add(bm, M("brilho"), ellipsoid, Vector((-0.25, -0.42, 0.22)), (0.08, 0.05, 0.15), 6, 4)

    obj("GotinhaDeMel", build, m, smooth=False)


def grafite_de_lapis():
    m = {"amarelo": mat("PencilYellow", (1.0, 0.8, 0.1), 0.5), "madeira": mat("PencilWood", (0.96, 0.8, 0.58), 0.7),
         "grafite": mat("Graphite", (0.18, 0.18, 0.2), 0.3)}

    def build(bm, M):
        add(bm, M("amarelo"), cone, (0, -1.0, 0), (0, 0.4, 0), 0.3, 0.3, 6)
        add(bm, M("madeira"), cone, (0, 0.4, 0), (0, 1.0, 0), 0.3, 0.1, 6)
        add(bm, M("grafite"), cone, (0, 1.0, 0), (0, 1.32, 0), 0.1, 0.0, 6)

    obj("GrafiteDeLapis", build, m, matrix=tilt(rx=-15, rz=-50))


def grao_de_acucar():
    m = {"acucar": mat("Sugar", (0.97, 0.97, 1.0), 0.3), "faceta": mat("SugarFacet", (0.82, 0.88, 1.0), 0.3)}

    def build(bm, M):
        for c, s, r in [((0, 0, 0), 0.8, (20, 15, 30)), ((1.0, -0.35, -0.25), 0.38, (40, 10, 70))]:
            before = set(bm.faces)
            bmesh.ops.create_cube(bm, size=s, matrix=Matrix.Translation(c) @ tilt(*r))
            for i, f in enumerate(new_faces(bm, before)):
                f.material_index = M("faceta") if i % 3 == 0 else M("acucar")

    obj("GraoDeAcucar", build, m)


def lupa():
    m = {"aro": mat("LupaRim", (0.2, 0.22, 0.28), 0.35, 0.4), "lente": mat("LupaLens", (0.66, 0.9, 1.0), 0.05),
         "cabo": mat("LupaHandle", (0.5, 0.26, 0.1), 0.6), "brilho": mat("LupaShine", (1, 1, 1), 0.1)}

    def build(bm, M):
        add(bm, M("aro"), tube, ring((0, 0, 0), 0.62, 20, axis="Y"), [0.08] * 21, 6)
        add(bm, M("lente"), lambda b: bmesh.ops.create_cone(b, cap_ends=True, segments=20, radius1=0.6, radius2=0.6,
                                                              depth=0.06, matrix=tilt(rx=90)))
        add(bm, M("brilho"), ellipsoid, Vector((-0.22, -0.05, 0.22)), (0.14, 0.02, 0.07), 6, 4, tilt(ry=-40))
        add(bm, M("aro"), cone, (0.36, 0, -0.5), (0.48, 0, -0.68), 0.09, 0.09, 8)
        add(bm, M("cabo"), cone, (0.48, 0, -0.68), (0.95, 0, -1.4), 0.12, 0.1, 8)

    obj("Lupa", build, m, matrix=tilt(rz=30, rx=10))


def pedaco_de_chiclete():
    m = {"chiclete": mat("Gum", (1.0, 0.45, 0.72), 0.4), "brilho": mat("GumShine", (1.0, 0.8, 0.9), 0.3)}

    def build(bm, M):
        rng = random.Random(9)
        for c, r in (((-0.45, 0, 0), (0.55, 0.45, 0.4)), ((0.75, 0.1, -0.05), (0.35, 0.3, 0.28))):
            before = set(bm.faces)
            ellipsoid(bm, Vector(c), r, 10, 7)
            for v in {v for f in new_faces(bm, before) for v in f.verts}:
                v.co += Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))) * 0.05
            set_mat(new_faces(bm, before), M("chiclete"))
        add(bm, M("chiclete"), tube, [Vector((-0.1, 0, 0.05)), Vector((0.2, 0.05, 0.0)), Vector((0.5, 0.1, -0.02))], [0.18, 0.07, 0.15], 6)

    obj("PedacoDeChiclete", build, m)


def perna_de_gafanhoto():
    m = {"verde": mat("HopperGreen", (0.42, 0.76, 0.14), 0.5), "escuro": mat("HopperDark", (0.22, 0.45, 0.08), 0.5),
         "espinho": mat("HopperSpike", (0.65, 0.2, 0.1), 0.5)}

    def build(bm, M):
        hip, knee = Vector((-1.0, 0, -0.2)), Vector((0.75, 0, 0.75))
        foot = Vector((0.3, 0, -1.0))
        add(bm, M("verde"), tube, [hip, hip.lerp(knee, 0.35), hip.lerp(knee, 0.75), knee], [0.16, 0.26, 0.18, 0.1], 8)
        add(bm, M("escuro"), ellipsoid, knee, (0.12, 0.12, 0.12), 6, 4)
        add(bm, M("escuro"), tube, [knee, knee.lerp(foot, 0.5), foot], [0.12, 0.1, 0.08], 6)
        for k in (0.25, 0.45, 0.65, 0.85):
            p = knee.lerp(foot, k)
            add(bm, M("espinho"), cone, p, p + Vector((0.26, 0, 0.06)), 0.06, 0.0, 4)
        add(bm, M("escuro"), tube, [foot, foot + Vector((0.3, 0, -0.08))], [0.05, 0.03], 5)

    obj("PernaDeGafanhoto", build, m, matrix=tilt(rz=20))


def semente_de_dente_de_leao():
    m = {"branco": mat("Pappus", (0.97, 0.97, 0.95), 0.5), "semente": mat("DandelionSeed", (0.55, 0.36, 0.18), 0.6)}

    def build(bm, M):
        add(bm, M("semente"), ellipsoid, Vector((0, 0, -1.1)), (0.07, 0.07, 0.22), 6, 5)
        add(bm, M("branco"), cone, (0, 0, -0.9), (0, 0, 0.55), 0.045, 0.04, 5)
        top = Vector((0, 0, 0.55))
        n = 10
        for i in range(n):
            a = 2 * math.pi * i / n
            mid = top + Vector((math.cos(a) * 0.35, math.sin(a) * 0.35, 0.35))
            end = top + Vector((math.cos(a) * 0.85, math.sin(a) * 0.85, 0.5))
            add(bm, M("branco"), tube, [top, mid, end], [0.05, 0.04, 0.03], 4)
            add(bm, M("branco"), ellipsoid, end, (0.08, 0.08, 0.08), 6, 4)

    obj("SementeDeDenteDeLeao", build, m, matrix=tilt(ry=18, rz=10))


def tampa_de_caneta():
    m = {"azul": mat("PenBlue", (0.12, 0.32, 0.92), 0.4), "escuro": mat("PenBlueDark", (0.06, 0.16, 0.5), 0.4)}

    def build(bm, M):
        add(bm, M("azul"), cone, (0, 0, -0.8), (0, 0, 0.6), 0.3, 0.27, 14)
        add(bm, M("azul"), ellipsoid, Vector((0, 0, 0.6)), (0.27, 0.27, 0.18), 14, 6)
        add(bm, M("escuro"), cone, (0, 0, -0.8), (0, 0, -0.72), 0.31, 0.31, 14)  # borda
        # presilha
        add(bm, M("escuro"), tube, [Vector((0, -0.28, 0.55)), Vector((0, -0.42, 0.45)), Vector((0, -0.42, -0.35)),
                                    Vector((0, -0.34, -0.45))], [0.07, 0.07, 0.07, 0.08], 4)

    obj("TampaDeCaneta", build, m, matrix=tilt(ry=-30, rz=-20))


def pastilha_de_menta():
    m = {"bala": mat("Mint", (0.97, 0.99, 0.97), 0.3), "menta": mat("MintStripe", (0.05, 0.68, 0.38), 0.35),
         "papel": mat("Wrapper", (0.72, 0.95, 0.88), 0.2)}

    def build(bm, M):
        # pastilha redonda e abaulada com espiral verde
        before = set(bm.faces)
        geom = bmesh.ops.create_uvsphere(bm, u_segments=24, v_segments=8, radius=1.0)
        for v in geom["verts"]:
            v.co.z *= 0.42
            v.co *= 0.62
        bm.normal_update()
        for f in new_faces(bm, before):
            c = f.calc_center_median()
            a = math.atan2(c.y, c.x)
            r = math.hypot(c.x, c.y)
            # cata-vento: faixas verdes saindo do centro, levemente torcidas
            f.material_index = M("menta") if math.sin(a * 4 + r * 2.5) > 0.1 else M("bala")
        # embrulho torcido nas duas pontas (laços)
        for sx in (-1, 1):
            add(bm, M("papel"), cone, (sx * 0.55, 0, 0), (sx * 0.78, 0, 0), 0.18, 0.06, 6)
            add(bm, M("papel"), cone, (sx * 0.78, 0, 0), (sx * 1.2, 0, 0), 0.06, 0.32, 6)

    obj("PastilhaDeMenta", build, m, matrix=tilt(rx=35, rz=-15))


ITEMS = {
    "AsaDeMosca": asa_de_mosca, "CabecaDeFosforo": cabeca_de_fosforo, "CacoDeLente": caco_de_lente,
    "CafeDerramado": cafe_derramado, "CascaDeAmendoim": casca_de_amendoim, "CascaDeSemente": casca_de_semente,
    "CascoDeTatuzinho": casco_de_tatuzinho, "ClipeDePapel": clipe_de_papel, "ColaBranca": cola_branca,
    "EspinhoDeRosa": espinho_de_rosa, "FerraoDeAbelha": ferrao_de_abelha, "GotaDeOrvalho": gota_de_orvalho,
    "GotinhaDeMel": gotinha_de_mel, "GrafiteDeLapis": grafite_de_lapis, "GraoDeAcucar": grao_de_acucar,
    "Lupa": lupa, "PastilhaDeMenta": pastilha_de_menta, "PedacoDeChiclete": pedaco_de_chiclete, "PernaDeGafanhoto": perna_de_gafanhoto,
    "SementeDeDenteDeLeao": semente_de_dente_de_leao, "TampaDeCaneta": tampa_de_caneta,
}


# ================================================================ render

def setup_render(scene):
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in engines else engines[0]
    scene.render.resolution_x = scene.render.resolution_y = SIZE
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.view_settings.view_transform = "Standard"
    world = bpy.data.worlds.new("IconWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (1, 1, 1, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.55
    scene.world = world
    # contorno (Freestyle), se esta versão do Blender tiver
    if hasattr(scene.render, "use_freestyle"):
        scene.render.use_freestyle = True
        scene.render.line_thickness_mode = "ABSOLUTE"
        scene.render.line_thickness = 2.2
        ls = scene.view_layers[0].freestyle_settings.linesets
        lineset = ls[0] if len(ls) else ls.new("Outline")
        lineset.select_silhouette = True
        lineset.select_border = True
        lineset.select_crease = False
        if lineset.linestyle is None:
            lineset.linestyle = bpy.data.linestyles.new("IconOutline")
        lineset.linestyle.color = OUTLINE
        lineset.linestyle.thickness = 2.2
        return True
    return False


def frame_camera(scene):
    pts = [o.matrix_world @ Vector(c) for o in scene.objects if o.type == "MESH" for c in o.bound_box]
    center = sum(pts, Vector()) / len(pts)
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    center = (lo + hi) / 2
    cam = bpy.data.objects.new("IconCam", bpy.data.cameras.new("IconCam"))
    cam.data.type = "ORTHO"
    scene.collection.objects.link(cam)
    cam.location = center + VIEW_DIR * 20
    cam.rotation_euler = (-VIEW_DIR).to_track_quat("-Z", "Y").to_euler()
    bpy.context.view_layer.update()
    inv = cam.matrix_world.inverted()
    local = [inv @ p for p in pts]
    w = max(p.x for p in local) - min(p.x for p in local)
    h = max(p.y for p in local) - min(p.y for p in local)
    cx = (max(p.x for p in local) + min(p.x for p in local)) / 2
    cy = (max(p.y for p in local) + min(p.y for p in local)) / 2
    cam.location = cam.matrix_world @ Vector((cx, cy, 0))  # centraliza na tela
    cam.data.ortho_scale = max(w, h) * 1.14
    scene.camera = cam


def lights(scene):
    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
    key.data.energy = 3.0
    key.rotation_euler = (math.radians(40), math.radians(-25), math.radians(-35))
    scene.collection.objects.link(key)
    rim = bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", "SUN"))
    rim.data.energy = 1.2
    rim.rotation_euler = (math.radians(-50), math.radians(30), math.radians(150))
    scene.collection.objects.link(rim)


THIN_OUTLINE = {"SementeDeDenteDeLeao": 1.0}


def render_item(name, builder):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    MATS.clear()
    scene = bpy.context.scene
    outline = setup_render(scene)
    if outline and name in THIN_OUTLINE:
        scene.render.line_thickness = THIN_OUTLINE[name]
        scene.view_layers[0].freestyle_settings.linesets[0].linestyle.thickness = THIN_OUTLINE[name]
    builder()
    tris = ac.triangle_count()
    lights(scene)
    frame_camera(scene)
    path = os.path.join(OUT, f"{name}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"[icons] {name}: {tris} tris, contorno={'sim' if outline else 'nao'}")
    return path


def contact_sheet(paths, cols=5):
    """Folha com todos os ícones sobre cinza (para ver a transparência e comparar)."""
    import numpy as np
    rows = math.ceil(len(paths) / cols)
    pad = 12
    W, H = cols * (SIZE + pad) + pad, rows * (SIZE + pad) + pad
    sheet = np.zeros((H, W, 4), dtype=np.float32)
    sheet[:, :] = (0.32, 0.34, 0.3, 1.0)
    for i, p in enumerate(paths):
        img = bpy.data.images.load(p)
        px = np.array(img.pixels[:], dtype=np.float32).reshape(SIZE, SIZE, 4)
        r, c = divmod(i, cols)
        y0 = H - pad - (r + 1) * (SIZE + pad) + pad  # pixels do Blender começam embaixo
        x0 = pad + c * (SIZE + pad)
        dst = sheet[y0:y0 + SIZE, x0:x0 + SIZE]
        a = px[:, :, 3:4]
        dst[:, :, :3] = px[:, :, :3] * a + dst[:, :, :3] * (1 - a)
    out = bpy.data.images.new("ItemIcons", W, H, alpha=True)
    out.pixels = sheet.ravel()
    out.filepath_raw = SHEET
    out.file_format = "PNG"
    out.save()


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(ITEMS)
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    for n in names:
        render_item(n, ITEMS[n])
    contact_sheet([os.path.join(OUT, f"{n}.png") for n in ITEMS if os.path.exists(os.path.join(OUT, f"{n}.png"))])
    print(f"[icons] {len(names)} ícones em {OUT}; folha: {SHEET}")


main()

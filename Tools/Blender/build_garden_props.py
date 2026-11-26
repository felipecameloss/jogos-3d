"""
Props da horta (fase 2, issue #29), em escala de inseto (operária = 1 m; no jogo ~1,8x):
tomate e abóbora gigantes (obstáculos), regador (o perigo da fase: aparece no céu e despeja
água) e alface e cenoura para os canteiros. Low-poly, cores chapadas.

Uso:
    blender --background --python Tools/Blender/build_garden_props.py

Gera:
    Assets/Art/Models/Props/Tomato.fbx       tomate caído (~3,2 m)
    Assets/Art/Models/Props/Pumpkin.fbx      abóbora com gomos (~5 m)
    Assets/Art/Models/Props/WateringCan.fbx  regador verde; peça Rose = o crivo (de onde sai a água)
    Assets/Art/Models/Props/Lettuce.fbx      pé de alface (~2,2 m)
    Assets/Art/Models/Props/Carrot.fbx       cenoura no chão: ponta laranja e folhas (~2,5 m)
    Tools/Blender/previews/GardenProps.png   o conjunto + uma operária para escala
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, part, tube  # noqa: E402

OUT = os.path.join(ac.ROOT, "Assets", "Art", "Models", "Props")
MAX_TRIS = 1200


def new_root(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))
    return root


def lobed_sphere(bm, center, radius, squash, lobes, depth, segs=16, rings=10):
    """Esfera achatada com gomos (tomate/abóbora): o raio varia com o ângulo em volta do eixo."""
    verts = ellipsoid(bm, Vector((0, 0, 0)), (1, 1, 1), segs, rings)
    for v in verts:
        a = math.atan2(v.co.y, v.co.x)
        r = 1 - depth * (0.5 - 0.5 * math.cos(a * lobes))
        flat = math.sqrt(max(0.0, 1 - v.co.z ** 2))
        v.co.x *= radius * (r * flat + (1 - flat)) / max(flat, 1e-4) * flat if flat > 1e-4 else 0
        v.co.y *= radius * (r * flat + (1 - flat)) / max(flat, 1e-4) * flat if flat > 1e-4 else 0
        v.co.z *= radius * squash
        # pólos levemente afundados (o "umbigo" do tomate/abóbora)
        if abs(v.co.z) > radius * squash * 0.9:
            v.co.z *= 0.9
        v.co += Vector(center)
    return verts


def calyx(bm, top, size, points=5):
    """Estrela de folhinhas verdes em cima do tomate."""
    for i in range(points):
        a = 2 * math.pi * i / points
        tip = top + Vector((math.cos(a) * size, math.sin(a) * size, -size * 0.25))
        tube(bm, [top, top.lerp(tip, 0.5) + Vector((0, 0, size * 0.12)), tip], [size * 0.22, size * 0.16, size * 0.02], 4)


def build_tomato():
    root = new_root("Tomato")
    m_red = material("Tomato_Red", (0.78, 0.05, 0.02), 0.25)
    m_green = material("Tomato_Green", (0.12, 0.45, 0.06), 0.5)
    r, sq = 1.6, 0.82
    c = Vector((0, 0, r * sq))
    body = part("Body", root, c, lambda bm, o: lobed_sphere(bm, c - o, r, sq, 5, 0.06), m_red, smooth=True)
    top = Vector((0, 0, r * sq * 2 * 0.95)) - c
    add_to(body, m_green, lambda bm: _new_verts(bm, lambda: (calyx(bm, top, 0.75),
                                                            tube(bm, [top, top + Vector((0.1, 0.05, 0.45))], [0.12, 0.09], 5))))
    return root


def build_pumpkin():
    root = new_root("Pumpkin")
    m_orange = material("Pumpkin_Orange", (0.92, 0.36, 0.02), 0.45)
    m_stem = material("Pumpkin_Stem", (0.3, 0.28, 0.08), 0.6)
    r, sq = 2.6, 0.72
    c = Vector((0, 0, r * sq))
    body = part("Body", root, c, lambda bm, o: lobed_sphere(bm, c - o, r, sq, 10, 0.12, 20, 10), m_orange, smooth=False)
    top = Vector((0, 0, r * sq * 2 * 0.9)) - c
    add_to(body, m_stem, lambda bm: _new_verts(bm, lambda: tube(
        bm, [top, top + Vector((0.05, 0, 0.6)), top + Vector((0.35, 0.1, 1.0))], [0.32, 0.26, 0.2], 6)))
    return root


def build_watering_can():
    root = new_root("WateringCan")
    m_body = material("WateringCan_Body", (0.08, 0.42, 0.22), 0.35)
    m_dark = material("WateringCan_Dark", (0.04, 0.22, 0.12), 0.4)
    m_rose = material("WateringCan_Rose", (0.75, 0.78, 0.8), 0.3, 0.6)
    R, H = 2.4, 4.2
    body = part("Body", root, Vector((0, 0, H / 2)),
                lambda bm, o: bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=R, radius2=R * 0.92,
                                                    depth=H, matrix=Matrix.Identity(4)), m_body)
    # faixas escuras em cima e embaixo
    add_to(body, m_dark, lambda bm: _new_verts(bm, lambda: bmesh.ops.create_cone(
        bm, cap_ends=True, segments=16, radius1=R * 1.03, radius2=R * 1.03, depth=0.35,
        matrix=Matrix.Translation((0, 0, -H / 2 + 0.2)))))
    add_to(body, m_dark, lambda bm: _new_verts(bm, lambda: bmesh.ops.create_cone(
        bm, cap_ends=True, segments=16, radius1=R * 0.95, radius2=R * 0.95, depth=0.3,
        matrix=Matrix.Translation((0, 0, H / 2 - 0.15)))))
    # alça em arco por cima, de trás para o meio
    pts = [Vector((0, -R * 0.85, H * 0.45)) + Vector((0, 0, 0)), Vector((0, -R * 1.2, H * 0.95)),
           Vector((0, -R * 0.6, H * 1.25)), Vector((0, R * 0.2, H * 1.1)), Vector((0, R * 0.45, H * 0.5))]
    part("Handle", root, pts[0], lambda bm, o: tube(bm, [p - o for p in pts], [0.22] * 5, 6), m_dark)
    # bico: sai de baixo na frente e sobe inclinado até o crivo
    s0 = Vector((0, R * 0.85, H * 0.15))
    s1 = Vector((0, R * 2.4, H * 0.75))
    s2 = Vector((0, R * 3.3, H * 1.15))
    part("Spout", root, s0, lambda bm, o: tube(bm, [s0 - o, s1 - o, s2 - o], [0.45, 0.32, 0.26], 8), m_body)
    # crivo (o chuveirinho): pivô no centro da face, a água sai dali
    rose_c = s2 + Vector((0, 0.35, 0.18))
    rose = part("Rose", root, rose_c, lambda bm, o: bmesh.ops.create_cone(
        bm, cap_ends=True, segments=12, radius1=0.25, radius2=0.95, depth=0.6,
        matrix=Matrix.Translation(rose_c - o) @ Matrix.Rotation(math.radians(-70), 4, "X")), m_rose)
    return root


def build_lettuce():
    root = new_root("Lettuce")
    m_leaf = material("Lettuce_Leaf", (0.38, 0.72, 0.12), 0.5)
    m_light = material("Lettuce_Light", (0.68, 0.9, 0.35), 0.5)
    c = Vector((0, 0, 0.7))
    body = part("Body", root, c, lambda bm, o: ellipsoid(bm, c - o + Vector((0, 0, 0.1)), (0.75, 0.75, 0.62), 10, 7), m_light)
    for i in range(7):
        a = 2 * math.pi * i / 7
        lc = Vector((math.cos(a) * 0.75, math.sin(a) * 0.75, 0.45)) - c
        rot = Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(math.radians(-55), 4, "Y")
        add_to(body, m_leaf, lambda bm, lc=lc, rot=rot: ellipsoid(bm, lc, (0.85, 0.6, 0.12), 8, 4, rot))
    return root


def build_carrot():
    root = new_root("Carrot")
    m_orange = material("Carrot_Orange", (0.95, 0.4, 0.03), 0.45)
    m_green = material("Carrot_Green", (0.16, 0.55, 0.1), 0.5)
    c = Vector((0, 0, 0.3))
    body = part("Body", root, c, lambda bm, o: bmesh.ops.create_cone(
        bm, cap_ends=True, segments=10, radius1=0.25, radius2=0.55, depth=0.8, matrix=Matrix.Translation((0, 0, 0))), m_orange)
    for i in range(6):
        a = 2 * math.pi * i / 6 + 0.3
        tip = Vector((math.cos(a) * 0.8, math.sin(a) * 0.8, 2.2)) - c
        mid = Vector((math.cos(a) * 0.3, math.sin(a) * 0.3, 1.3)) - c
        add_to(body, m_green, lambda bm, mid=mid, tip=tip: _new_verts(bm, lambda: tube(
            bm, [Vector((0, 0, 0.35)), mid, tip], [0.09, 0.07, 0.02], 4)))
    return root


def _new_verts(bm, build):
    """Roda build() e devolve os vértices novos (para add_to pintar com outro material)."""
    before = set(bm.verts)
    build()
    return [v for v in bm.verts if v not in before]


PROPS = [
    ("Tomato", build_tomato),
    ("Pumpkin", build_pumpkin),
    ("WateringCan", build_watering_can),
    ("Lettuce", build_lettuce),
    ("Carrot", build_carrot),
]


def main():
    os.makedirs(OUT, exist_ok=True)
    report = []
    for name, builder in PROPS:
        ac.reset_scene()
        ac.PIVOTS.clear()
        builder()
        bpy.context.view_layer.update()
        tris = ac.triangle_count()
        pts = [(o.matrix_world @ Vector(c)) for o in bpy.data.objects if o.type == "MESH" for c in o.bound_box]
        size = (max(p.x for p in pts) - min(p.x for p in pts), max(p.y for p in pts) - min(p.y for p in pts), max(p.z for p in pts))
        report.append(f"[garden] {name}: {tris} tris, {size[0]:.2f} x {size[1]:.2f} x {size[2]:.2f} m")
        if tris > MAX_TRIS:
            print(f"[garden] ERRO: {name} com {tris} triangulos (limite {MAX_TRIS})")
            sys.exit(1)
        ac.export_fbx(os.path.join(OUT, f"{name}.fbx"))

    # prévia: todos juntos + uma operária de 1 m para escala
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ac.RESET_SCENE = False
    layout = {"Tomato": (-4.5, 0), "Pumpkin": (1.5, 1.5), "WateringCan": (8.5, 5), "Lettuce": (-1.5, -3.5), "Carrot": (-5.5, -4)}
    for name, builder in PROPS:
        before = set(bpy.data.objects)
        ac.PIVOTS.clear()
        root = builder()
        for o in [o for o in bpy.data.objects if o not in before]:
            o.name = f"{name}_{o.name}"
        root.location = (*layout[name], 0)
    before = set(bpy.data.objects)
    ac.PIVOTS.clear()
    ac.build_ant({**ac.DEFAULTS, "name": "WorkerAntRef"})
    ant = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    k = 1.0 / ac.body_length()
    for o in ant:
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(k, 4))
        o.location = o.location * k
    next(o for o in ant if o.parent is None).location = (-2.5, -1.0, 0)
    ac.render_preview(os.path.join(HERE, "previews", "GardenProps.png"), cam_pos=(4.0, -22.0, 11.0), target=(1.5, 1.0, 2.0),
                      lens=40, ground_size=80)
    print("\n".join(report))


main()

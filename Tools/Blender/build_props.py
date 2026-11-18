"""
Props do quintal em escala de inseto (mesmo estilo dos insetos: low-poly, cores
chapadas, sem textura). Escala: formiga operária = 1 m (ver CLAUDE.md); no jogo os
insetos estão ~1,8x, então os props vão na mesma proporção.

Uso:
    blender --background --python Tools/Blender/build_props.py

Gera (cada um com raiz no chão, frente em +Y no Blender / +Z no Unity):
    Assets/Art/Models/Props/RockPebble.fbx     pedrinha redonda (~0,9 m)
    Assets/Art/Models/Props/RockMossy.fbx      pedra média com musgo (~1,9 m)
    Assets/Art/Models/Props/RockSlab.fbx       laje achatada (~3,2 m)
    Assets/Art/Models/Props/FallenLeaf.fbx     folha seca caída, curvada (~3,6 m)
    Assets/Art/Models/Props/GrassStalk.fbx     touceira com 3 talos de grama (~5,5 m)
    Assets/Art/Models/Props/BottleCapChest.fbx tampinha (baú): peças Base + Lid (Chest.lid)
    Assets/Art/Models/Props/BottleCapChestGold.fbx  a mesma, dourada (baú grande)
    Assets/Art/Models/Props/CrushedCan.fbx     lata de refrigerante amassada, deitada (~6 m)
    Tools/Blender/previews/Props.png           o conjunto + uma operária para escala
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
from ant_common import material, part, tube  # noqa: E402

OUT = os.path.join(ac.ROOT, "Assets", "Art", "Models", "Props")
MAX_PROP_TRIS = 900


def new_root(name):
    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))
    return root


def flat_mesh(name, parent, pivot, build, mats):
    """Peça com vários materiais: build(bm, o) devolve nada; use face.material_index."""
    obj = part(name, parent, pivot, build, mats[0])
    for m in mats[1:]:
        obj.data.materials.append(m)
    return obj


def ground_it(root):
    """Encosta o prop no chão (menor z = 0) movendo as peças filhas."""
    bpy.context.view_layer.update()
    zs = [(o.matrix_world @ Vector(c)).z for o in root.children_recursive if o.type == "MESH" for c in o.bound_box]
    dz = -min(zs)
    for c in root.children:
        c.location.z += dz
        ac.PIVOTS[c.name] = ac.PIVOTS.get(c.name, Vector()) + Vector((0, 0, dz))
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- pedras
def build_rock(name, size, squash, seed, colors, moss=None, roughness_amt=0.22):
    """Icosfera deformada, base achatada, sombreamento facetado. moss: cor do musgo no topo."""
    rng = random.Random(seed)
    root = new_root(name)
    m_rock = material(f"{name}_Stone", colors[0], 0.85)
    m_dark = material(f"{name}_StoneDark", colors[1], 0.9)
    mats = [m_rock, m_dark] + ([material(f"{name}_Moss", moss, 0.9)] if moss else [])

    def build(bm, o):
        bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
        for v in bm.verts:
            v.co *= 1.0 + rng.uniform(-roughness_amt, roughness_amt)
            v.co.x *= size[0]; v.co.y *= size[1]; v.co.z *= size[2]
            if v.co.z < 0:  # base achatada: senta no chão
                v.co.z *= squash
        bm.normal_update()
        for f in bm.faces:
            n = f.normal
            if moss and n.z > 0.72:
                f.material_index = 2
            elif rng.random() < 0.22:  # facetas mais escuras: leitura de volume
                f.material_index = 1

    flat_mesh("Mesh", root, (0, 0, 0), build, mats)
    ground_it(root)
    return root


# ---------------------------------------------------------------- folha
def build_leaf(name="FallenLeaf", length=3.6, width=1.5):
    root = new_root(name)
    m_leaf = material(f"{name}_Leaf", (0.98, 0.5, 0.06), 0.6)     # laranja de outono
    m_tip = material(f"{name}_LeafTip", (0.9, 0.28, 0.04), 0.6)   # borda/ponta mais queimada
    m_vein = material(f"{name}_Vein", (0.45, 0.16, 0.03), 0.7)

    nu, nv = 12, 6

    def surface(u, v):
        """u em [0,1] (cabo -> ponta), v em [-1,1] (lado a lado)."""
        w = width * 0.5 * (math.sin(math.pi * u) ** 0.75) * (1 - 0.15 * u)
        x = v * w
        y = (u - 0.5) * length
        # curvatura: bordas levantam (folha seca enrolando) e a ponta sobe um pouco
        z = 0.18 * (abs(v) ** 1.6) * (w / (width * 0.5) + 0.2) + 0.25 * (u ** 3) + 0.05 * math.sin(u * 9) * abs(v)
        return Vector((x, y, z))

    def build(bm, o):
        grid = [[bm.verts.new(surface(i / nu, -1 + 2 * j / nv) - o) for j in range(nv + 1)] for i in range(nu + 1)]
        for i in range(nu):
            for j in range(nv):
                f = bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
                edge = j in (0, nv - 1) or i >= nu - 2
                f.material_index = 1 if edge else 0
        # espessura (dupla face com volume)
        bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.035)
        # nervura central + cabo
        rib = [surface(u, 0) + Vector((0, 0, 0.03)) for u in (0.0, 0.25, 0.5, 0.75, 0.97)]
        stem = [surface(0, 0) + Vector((0, -0.45, 0.06)), surface(0, 0) + Vector((0, 0, 0.03))]
        before = set(bm.faces)
        tube(bm, [p - o for p in stem + rib[1:]], [0.05, 0.045, 0.04, 0.032, 0.022, 0.01], 4)
        # nervuras laterais
        for u in (0.25, 0.42, 0.59, 0.75):
            for side in (-1, 1):
                a = surface(u, 0) + Vector((0, 0, 0.03))
                b = surface(min(u + 0.12, 0.95), side * 0.7) + Vector((0, 0, 0.03))
                tube(bm, [a - o, b - o], [0.02, 0.008], 3)
        for f in bm.faces:
            if f not in before:
                f.material_index = 2

    flat_mesh("Mesh", root, (0, 0, 0), build, [m_leaf, m_tip, m_vein])
    ground_it(root)
    return root


# ---------------------------------------------------------------- grama
def blade(bm, o, base, height, bend, lean_dir, width, twist):
    """Talo em V (dobrado no meio), afinando até a ponta, curvado para lean_dir."""
    n = 7
    side = Vector((-lean_dir.y, lean_dir.x, 0)).normalized()
    sections = []
    for i in range(n + 1):
        t = i / n
        center = base + Vector((0, 0, height * t)) + lean_dir * (bend * t * t * height)
        w = width * (1 - t) ** 0.8 + 0.01
        rot = Matrix.Rotation(twist * t, 3, "Z")
        s = rot @ side
        fold = rot @ lean_dir * (w * 0.35)  # vinco do V
        sections.append([bm.verts.new(center - s * w - o), bm.verts.new(center - fold - o), bm.verts.new(center + s * w - o)])
    for a, b in zip(sections, sections[1:]):
        bm.faces.new((a[0], a[1], b[1], b[0]))
        bm.faces.new((a[1], a[2], b[2], b[1]))


def build_grass(name="GrassStalk"):
    root = new_root(name)
    m_grass = material(f"{name}_Grass", (0.3, 0.72, 0.1), 0.55)
    m_base = material(f"{name}_GrassBase", (0.16, 0.42, 0.06), 0.6)
    blades = [  # (posição, altura, curva, direção, largura, torção)
        (Vector((0, 0, 0)), 5.5, 0.18, Vector((0.2, 1, 0)), 0.26, 0.5),
        (Vector((0.35, -0.2, 0)), 4.2, 0.28, Vector((1, -0.3, 0)), 0.22, -0.4),
        (Vector((-0.3, -0.15, 0)), 3.3, 0.35, Vector((-1, -0.5, 0)), 0.2, 0.7),
    ]
    for i, (pos, h, bend, d, w, tw) in enumerate(blades, start=1):
        def build(bm, o, pos=pos, h=h, bend=bend, d=d.normalized(), w=w, tw=tw):
            blade(bm, o, pos, h, bend, d, w, tw)
            bmesh.ops.solidify(bm, geom=list(bm.faces), thickness=0.03)
            for f in bm.faces:  # base mais escura
                f.material_index = 1 if (f.calc_center_median() + o).z < h * 0.18 else 0

        flat_mesh(f"Blade_{i}", root, pos, build, [m_grass, m_base])
    return root


# ---------------------------------------------------------------- tampinha (baú)
def build_bottle_cap(name="BottleCapChest", radius=1.2, height=0.62, crimps=21,
                     cap_color=(0.86, 0.05, 0.04), ring_color=(0.98, 0.95, 0.88)):
    """Tampinha apoiada na borda (topo para cima). Lid = a tampinha inteira (o Chest.cs
    levanta e vira); Base = terra afundada embaixo, de onde o item sai."""
    root = new_root(name)
    m_red = material(f"{name}_Cap", cap_color, 0.35, 0.3)
    m_white = material(f"{name}_Ring", ring_color, 0.4)
    m_inner = material(f"{name}_Inner", (0.75, 0.72, 0.68), 0.5)
    m_dirt = material(f"{name}_Dirt", (0.32, 0.2, 0.11), 0.95)

    def build_base(bm, o):
        bmesh.ops.create_cone(bm, cap_ends=True, segments=16, radius1=radius * 1.08, radius2=radius * 0.95,
                              depth=0.08, matrix=Matrix.Translation(Vector((0, 0, 0.04)) - o))

    part("Base", root, (0, 0, 0), build_base, m_dirt)

    # pivô no meio da altura: o Chest.cs vira a tampa 180° no pivô; com o pivô na base ela
    # afundaria no chão, assim ela fica apoiada de boca para cima
    lid_pivot = Vector((0, 0, 0.03 + height / 2))

    def build_lid(bm, o):
        n = crimps * 2
        bottom, top_ring = [], []
        for i in range(n):
            a = 2 * math.pi * i / n
            r = radius * (1.06 if i % 2 == 0 else 0.98)  # saia ondulada (o "crimp")
            bottom.append(bm.verts.new(Vector((math.cos(a) * r, math.sin(a) * r, 0.03)) - o))
            r2 = radius * (1.0 if i % 2 == 0 else 0.96)
            top_ring.append(bm.verts.new(Vector((math.cos(a) * r2, math.sin(a) * r2, height)) - o))
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((bottom[i], bottom[j], top_ring[j], top_ring[i]))
        # topo: anel externo vermelho, anel branco (marca), miolo vermelho levemente abaulado
        rings = [(0.9, height + 0.02, 0), (0.78, height + 0.03, 1), (0.6, height + 0.035, 1), (0.0, height + 0.06, 0)]
        prev = top_ring
        for k, (rr, z, mat_i) in enumerate(rings):
            if rr == 0.0:
                c = bm.verts.new(Vector((0, 0, z)) - o)
                for i in range(n):
                    f = bm.faces.new((prev[i], prev[(i + 1) % n], c))
                    f.material_index = mat_i
                break
            ring = [bm.verts.new(Vector((math.cos(2 * math.pi * i / n) * radius * rr,
                                         math.sin(2 * math.pi * i / n) * radius * rr, z)) - o) for i in range(n)]
            for i in range(n):
                f = bm.faces.new((prev[i], prev[(i + 1) % n], ring[(i + 1) % n], ring[i]))
                f.material_index = mat_i
            prev = ring
        # parte de dentro (aparece quando a tampa vira ao abrir)
        inner = [bm.verts.new(v.co * Vector((0.94, 0.94, 1)) + Vector((0, 0, 0.0))) for v in bottom]
        inner_top = [bm.verts.new(Vector((v.co.x * 0.94, v.co.y * 0.94, height - 0.06 - o.z))) for v in bottom]
        for i in range(n):
            j = (i + 1) % n
            f = bm.faces.new((bottom[j], bottom[i], inner[i], inner[j])); f.material_index = 2
            f = bm.faces.new((inner[j], inner[i], inner_top[i], inner_top[j])); f.material_index = 2
        c = bm.verts.new(Vector((0, 0, height - 0.06)) - o)
        for i in range(n):
            f = bm.faces.new((inner_top[(i + 1) % n], inner_top[i], c)); f.material_index = 2

    lid = part("Lid", root, lid_pivot, build_lid, m_red)
    lid.data.materials.append(m_white)
    lid.data.materials.append(m_inner)
    return root


# ---------------------------------------------------------------- lata amassada
def build_can(name="CrushedCan", length=6.0, radius=1.45, seed=7):
    rng = random.Random(seed)
    root = new_root(name)
    m_red = material(f"{name}_Red", (0.78, 0.03, 0.06), 0.3, 0.5)
    m_white = material(f"{name}_Stripe", (0.97, 0.96, 0.95), 0.35)
    m_metal = material(f"{name}_Metal", (0.78, 0.8, 0.84), 0.25, 0.9)
    m_hole = material(f"{name}_Hole", (0.06, 0.05, 0.06), 0.6)

    segs = 14
    # anéis ao longo da lata (t de 0 a 1); os de ponta são a borda fina de metal
    ts = [0.0, 0.035, 0.14, 0.28, 0.42, 0.55, 0.68, 0.8, 0.9, 0.965, 1.0]

    def build(bm, o):
        grid = []
        for r, t in enumerate(ts):
            x = (t - 0.5) * length
            # pisada: a metade de trás achata (z) e estufa dos lados (y); a da frente fica redonda
            crush = max(0.0, (t - 0.42) / 0.58) ** 0.8
            sz = 1.0 - 0.62 * crush
            sy = 1.0 + 0.22 * crush
            # um vinco torto no meio e uma leve torção
            crease = 1.0 - 0.18 * math.exp(-((t - 0.47) ** 2) / 0.004)
            twist = 0.35 * crush
            rim = 0.93 if r in (0, len(ts) - 1) else 1.0
            ring = []
            for s_ in range(segs):
                a = 2 * math.pi * s_ / segs + twist
                dent = 1.0 - (0.1 if rng.random() < 0.2 and 1 < r < len(ts) - 2 else 0.0)
                y = math.cos(a) * radius * sy * crease * dent * rim
                z = math.sin(a) * radius * sz * crease * dent * rim
                ring.append(bm.verts.new(Vector((x, y, z)) - o))
            grid.append(ring)
        for r in range(len(ts) - 1):
            for s_ in range(segs):
                f = bm.faces.new((grid[r][s_], grid[r][(s_ + 1) % segs], grid[r + 1][(s_ + 1) % segs], grid[r + 1][s_]))
                if r == 0 or r == len(ts) - 2:
                    f.material_index = 2  # borda de metal
                else:
                    # faixa branca ondulada em volta da lata
                    t_mid = (ts[r] + ts[r + 1]) / 2
                    wave = 0.2 + 0.06 * math.sin(2 * math.pi * s_ / segs * 2)
                    if abs(t_mid - wave) < 0.07:
                        f.material_index = 1
        # tampas: fundo de metal; topo (frente) com a abertura escura
        for idx, ring in ((0, grid[0]), (1, grid[-1])):
            c = bm.verts.new(sum((v.co for v in ring), Vector()) / len(ring))
            for s_ in range(segs):
                a, b = ring[s_], ring[(s_ + 1) % segs]
                f = bm.faces.new((b, a, c) if idx == 0 else (a, b, c))
                f.material_index = 3 if idx == 0 and s_ in (3, 4) else 2
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    flat_mesh("Mesh", root, (0, 0, 0), build, [m_red, m_white, m_metal, m_hole])  # deitada ao longo de X
    ground_it(root)
    return root


PROPS = [
    ("RockPebble", lambda: build_rock("RockPebble", (0.5, 0.45, 0.42), 0.35, 3,
                                      [(0.6, 0.56, 0.5), (0.47, 0.43, 0.39)], roughness_amt=0.12)),
    ("RockMossy", lambda: build_rock("RockMossy", (1.0, 0.85, 0.7), 0.3, 11,
                                     [(0.5, 0.51, 0.55), (0.38, 0.39, 0.43)], moss=(0.38, 0.66, 0.14))),
    ("RockSlab", lambda: build_rock("RockSlab", (1.7, 1.25, 0.42), 0.25, 21,
                                    [(0.66, 0.58, 0.46), (0.52, 0.45, 0.35)], roughness_amt=0.16)),
    ("FallenLeaf", build_leaf),
    ("GrassStalk", build_grass),
    ("BottleCapChest", build_bottle_cap),
    # baú grande: tampinha dourada com anel vermelho
    ("BottleCapChestGold", lambda: build_bottle_cap("BottleCapChestGold", cap_color=(0.95, 0.68, 0.1),
                                                    ring_color=(0.8, 0.08, 0.05))),
    ("CrushedCan", build_can),
]


def export_each():
    os.makedirs(OUT, exist_ok=True)
    report = []
    for name, builder in PROPS:
        ac.reset_scene()
        ac.PIVOTS.clear()
        builder()
        bpy.context.view_layer.update()
        tris = ac.triangle_count()
        pts = [(o.matrix_world @ Vector(c)) for o in bpy.data.objects if o.type == "MESH" for c in o.bound_box]
        size = Vector((max(p.x for p in pts) - min(p.x for p in pts), max(p.y for p in pts) - min(p.y for p in pts),
                       max(p.z for p in pts)))
        parts = ", ".join(sorted(o.name for o in bpy.data.objects if o.parent is not None))
        report.append(f"[props] {name}: {tris} tris, {size.x:.2f} x {size.y:.2f} x {size.z:.2f} m, pecas: {parts}")
        if tris > MAX_PROP_TRIS:
            print(f"[props] ERRO: {name} com {tris} triangulos (limite {MAX_PROP_TRIS})")
            sys.exit(1)
        ac.export_fbx(os.path.join(OUT, f"{name}.fbx"))
    print("\n".join(report))


def render_set():
    """Todos juntos, com uma operária para dar a escala."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ac.RESET_SCENE = False
    layout = {
        "GrassStalk": (-6.2, -2.0), "CrushedCan": (-1.5, -4.2), "RockSlab": (4.8, -2.2),
        "FallenLeaf": (5.2, 2.2), "BottleCapChest": (0.6, 1.6), "BottleCapChestGold": (3.0, 4.6), "RockMossy": (-4.6, 2.4),
        "RockPebble": (-2.4, 3.6),
    }
    for name, builder in PROPS:
        before = set(bpy.data.objects)
        ac.PIVOTS.clear()
        root = builder()
        for o in [o for o in bpy.data.objects if o not in before]:
            o.name = f"{name}_{o.name}"
        root.location = (*layout[name], 0)
    # operária de referência (1 m) ao lado da tampinha
    before = set(bpy.data.objects)
    ac.PIVOTS.clear()
    ac.build_ant({**ac.DEFAULTS, "name": "WorkerAntRef"})
    ant_objs = [o for o in bpy.data.objects if o not in before]
    bpy.context.view_layer.update()
    k = 1.0 / ac.body_length()
    for o in ant_objs:
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(k, 4))
        o.location = o.location * k
    ant_root = next(o for o in ant_objs if o.parent is None)
    ant_root.location = (-1.6, 3.4, 0)
    ant_root.rotation_euler = (0, 0, math.radians(-35))
    bpy.context.view_layer.update()

    ac.render_preview(os.path.join(HERE, "previews", "Props.png"),
                      cam_pos=(3.0, 17.5, 8.5), target=(-0.3, -0.3, 0.9), lens=36, ground_size=80)
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    bpy.ops.render.render(write_still=True)


export_each()
render_set()
print("[props] preview: " + os.path.join(HERE, "previews", "Props.png"))

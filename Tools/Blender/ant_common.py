"""
Base dos insetos estilo formiga (low-poly, cores chapadas, cabeça e olhos grandes).

Cada modelo é um script curto que chama run(cfg) com um dicionário que sobrescreve
DEFAULTS (que é a formiga operária). Ver build_worker_ant.py / build_soldier_ant.py.

Convenções (ver CLAUDE.md):
- 1 unidade = 1 m; cfg["body_length"] = comprimento de cabeça + tórax + abdômen
- Frente em +Y, cima em +Z no Blender (chega no Unity como +Z / +Y)
- Cada peça é um objeto filho com pivô na articulação, para animar por código:
    <Nome> (raiz, no chão)
      Body                 tórax (pivô no centro)
        Head               pivô no pescoço
          Eye_L / Eye_R
          Antenna_L / Antenna_R   pivô na base
          Mandible_L / Mandible_R pivô na boca
        Abdomen            pivô na cintura (pecíolo)
        Leg_L1..L3 / Leg_R1..R3   pivô no quadril (1 = dianteira)
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
MAX_TRIS = 1500

DEFAULTS = {
    "name": "WorkerAnt",
    "body_length": 1.0,
    # cores
    "color_body": (0.80, 0.07, 0.04),
    "color_dark": (0.42, 0.03, 0.02),      # pernas, antenas
    "color_light": (1.00, 0.38, 0.18),     # faixas do abdômen, ponta das antenas
    "color_mandible": (0.25, 0.04, 0.03),
    "color_eye": (0.97, 0.97, 0.95),
    "color_pupil": (0.03, 0.02, 0.03),
    "color_plate": None,                    # armadura (sobrancelha, placa no tórax); None = sem
    # proporções (antes da escala final para body_length)
    "hip_z": 0.27,
    "thorax_radii": (0.13, 0.19, 0.12),
    "head_offset": (0.38, 0.17),           # (y, z) do centro da cabeça, z relativo ao quadril
    "head_radii": (0.23, 0.2, 0.21),
    "eye_offset": (0.13, 0.13, 0.06),      # (x, y, z) a partir do centro da cabeça
    "eye_radii": (0.085, 0.07, 0.095),
    "pupil_radii": (0.05, 0.035, 0.065),
    "brow": False,                          # placa inclinada sobre cada olho (cara brava)
    "antenna": {"elbow": (0.06, 0.02, 0.2), "tip": (0.1, 0.17, 0.04), "radii": (0.022, 0.018, 0.015), "ball": 0.04},
    "mandible": {  # offsets a partir da boca (x é espelhado por lado)
        "points": [(0, 0, 0), (0.02, 0.07, -0.01), (-0.04, 0.11, -0.02)],
        "radii": [0.032, 0.026, 0.01],
        "mouth": (0.07, 0.17, -0.11),
        "sides": 5,
    },
    "abdomen_offset": (-0.4, 0.07),
    "abdomen_radii": (0.2, 0.26, 0.2),
    "thorax_plate": False,
    "leg_radii": (0.048, 0.04, 0.03, 0.026),
    "knee_segs": (6, 4),
    "head_segs": (11, 7),
    "leg_reach": 0.4,                       # x do tornozelo
    "knee_lift": 0.13,
    "legs": [(1, 0.11, 0.24), (2, 0.02, 0.0), (3, -0.07, -0.25)],  # (índice, y do quadril, direção do pé)
    # preview
    "cam_pos": (1.6, 2.15, 1.2),
    "cam_target": (0, -0.02, 0.26),
    "lens": 70,
}


# ---------------------------------------------------------------- materiais / geometria
def material(name, color, roughness=0.55, metallic=0.0):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    m.metallic = metallic
    m.roughness = roughness
    m.diffuse_color = (*color, 1.0)
    return m


def ellipsoid(bm, center, radii, segs=10, rings=7, matrix=None, poles_y=False):
    """Elipsoide low-poly em coordenadas locais da peça. poles_y: anéis ao longo de Y."""
    geom = bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    verts = list(geom["verts"])
    if poles_y:
        bmesh.ops.rotate(bm, verts=verts, cent=(0, 0, 0), matrix=Matrix.Rotation(math.radians(90), 3, "X"))
    m = Matrix.Translation(Vector(center)) @ (matrix or Matrix.Identity(4)) @ Matrix.Diagonal((*radii, 1.0))
    bmesh.ops.transform(bm, matrix=m, verts=verts)
    return verts


def tube(bm, points, radii, sides=5):
    """Tubo afunilado passando pelos pontos (pernas, antenas). Pontas fechadas."""
    rings = []
    for i, p in enumerate(points):
        p = Vector(p)
        if i == 0:
            d = Vector(points[1]) - p
        elif i == len(points) - 1:
            d = p - Vector(points[i - 1])
        else:
            d = Vector(points[i + 1]) - Vector(points[i - 1])
        d.normalize()
        up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
        a = d.cross(up).normalized()
        b = d.cross(a).normalized()
        ring = []
        for s in range(sides):
            t = 2 * math.pi * s / sides
            ring.append(bm.verts.new(p + (a * math.cos(t) + b * math.sin(t)) * radii[i]))
        rings.append(ring)
    for r0, r1 in zip(rings, rings[1:]):
        for s in range(sides):
            bm.faces.new((r0[s], r0[(s + 1) % sides], r1[(s + 1) % sides], r1[s]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])


PIVOTS = {}
RESET_SCENE = True  # render_lineup.py desliga para juntar vários modelos na mesma cena


def reset_scene():
    if RESET_SCENE:
        bpy.ops.wm.read_factory_settings(use_empty=True)


def part(name, parent, pivot_world, build, mat, smooth=False):
    """Peça com origem (pivô) em pivot_world. `build(bm, o)` recebe o pivô: use (ponto - o)."""
    bm = bmesh.new()
    o = Vector(pivot_world)
    build(bm, o)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    for poly in mesh.polygons:
        poly.use_smooth = smooth
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    PIVOTS[name] = o.copy()
    if parent is not None:
        # posição local = pivô - pivô do pai (nenhuma peça tem rotação)
        obj.parent = parent
        obj.location = o - PIVOTS[parent.name]
    else:
        obj.location = o
    return obj


def paint(obj, mat, select):
    """Pinta com `mat` as faces cujo centro (local) passa em select(centro)."""
    obj.data.materials.append(mat)
    slot = len(obj.data.materials) - 1
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    for f in bm.faces:
        if select(f.calc_center_median()):
            f.material_index = slot
    bm.to_mesh(obj.data)
    bm.free()


def add_to(obj, mat, build, smooth=None):
    """Acrescenta geometria (outro material) a uma peça já criada. build(bm) devolve os verts novos."""
    if mat.name not in obj.data.materials:
        obj.data.materials.append(mat)
    slot = list(obj.data.materials).index(mat)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    new = build(bm)
    for f in {f for v in new for f in v.link_faces}:
        f.material_index = slot
        if smooth is not None:
            f.smooth = smooth
    bm.to_mesh(obj.data)
    bm.free()


# ---------------------------------------------------------------- inseto
def build_ant(cfg):
    reset_scene()
    PIVOTS.clear()
    name = cfg["name"]

    m_body = material(f"{name}_Body", cfg["color_body"], 0.5)
    m_dark = material(f"{name}_Dark", cfg["color_dark"], 0.6)
    m_light = material(f"{name}_Light", cfg["color_light"], 0.5)
    m_eye = material(f"{name}_EyeWhite", cfg["color_eye"], 0.25)
    m_pupil = material(f"{name}_Pupil", cfg["color_pupil"], 0.15)
    m_mand = material(f"{name}_Mandible", cfg["color_mandible"], 0.45)
    m_plate = material(f"{name}_Plate", cfg["color_plate"], 0.45) if cfg["color_plate"] else None

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    root.empty_display_size = 0.3
    PIVOTS[name] = Vector((0, 0, 0))

    hip_z = cfg["hip_z"]
    # ---- tórax
    thorax_c = Vector((0, 0.02, hip_z))
    tr = cfg["thorax_radii"]
    body = part("Body", root, thorax_c,
                lambda bm, o: ellipsoid(bm, thorax_c - o, tr, 9, 6, Matrix.Rotation(math.radians(-12), 4, "X")),
                m_body)
    if cfg["thorax_plate"] and m_plate:
        # placa de armadura nas costas
        add_to(body, m_plate, lambda bm: ellipsoid(
            bm, Vector((0, -0.01, tr[2] * 0.75)), (tr[0] * 0.95, tr[1] * 0.85, tr[2] * 0.45), 7, 4,
            Matrix.Rotation(math.radians(-12), 4, "X")))

    # ---- cabeça
    neck = Vector((0, 0.19, hip_z + 0.05))
    hy, hz = cfg["head_offset"]
    head_c = Vector((0, hy, hip_z + hz))
    hr = cfg["head_radii"]

    def build_head(bm, o):
        ellipsoid(bm, head_c - o, hr, *cfg["head_segs"])
        tube(bm, [neck - o, (neck + head_c) / 2 - o], [0.06, 0.08], 6)  # pescoço

    head = part("Head", body, neck, build_head, m_body)

    # ---- olhos (branco + pupila + brilho no mesmo objeto)
    ex, ey, ez = cfg["eye_offset"]
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * ex, ey, ez))
        eye = part(f"Eye_{side}", head, eye_c,
                   lambda bm, o, eye_c=eye_c: ellipsoid(bm, eye_c - o, cfg["eye_radii"], 9, 6),
                   m_eye, smooth=True)
        er = cfg["eye_radii"]
        pup_c = Vector((sx * er[0] * 0.38, er[1] * 0.72, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, cfg["pupil_radii"], 8, 6), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(
            bm, pup_c + Vector((sx * -0.012, 0.03, 0.03)), (0.016, 0.012, 0.02), 6, 4), smooth=True)

        if cfg["brow"] and m_plate:
            # placa inclinada sobre o olho, mais baixa no meio da cara: cara de brava
            brow_c = eye_c + Vector((sx * -0.005, er[1] * 0.35, er[2] * 0.85)) - PIVOTS["Head"]
            tilt = Matrix.Rotation(math.radians(sx * 22), 4, "Y") @ Matrix.Rotation(math.radians(-18), 4, "X")
            add_to(head, m_plate, lambda bm, brow_c=brow_c, tilt=tilt: ellipsoid(
                bm, brow_c, (er[0] * 1.25, er[1] * 0.85, er[2] * 0.42), 6, 4, tilt))

    # ---- antenas cotoveladas
    an = cfg["antenna"]
    for side, sx in (("L", -1), ("R", 1)):
        base = head_c + Vector((sx * 0.08, hr[1] * 0.6, hr[2] * 0.8))
        elbow = base + Vector((sx * an["elbow"][0], an["elbow"][1], an["elbow"][2]))
        tip = elbow + Vector((sx * an["tip"][0], an["tip"][1], an["tip"][2]))

        def build_antenna(bm, o, base=base, elbow=elbow, tip=tip):
            tube(bm, [base - o, elbow - o, tip - o], an["radii"], 5)
            if an["ball"]:
                ellipsoid(bm, tip - o, (an["ball"],) * 3, 6, 4)

        ant = part(f"Antenna_{side}", head, base, build_antenna, m_dark)
        if an["ball"]:
            tip_local = tip - base
            paint(ant, m_light, lambda c, t=tip_local: (c - t).length < an["ball"] * 1.25)

    # ---- mandíbulas
    md = cfg["mandible"]
    for side, sx in (("L", -1), ("R", 1)):
        mouth = head_c + Vector((sx * md["mouth"][0], md["mouth"][1], md["mouth"][2]))
        pts = [mouth + Vector((sx * p[0], p[1], p[2])) for p in md["points"]]
        part(f"Mandible_{side}", head, mouth,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], md["radii"], md["sides"]),
             m_mand)

    # ---- abdômen com faixas
    waist = Vector((0, -0.17, hip_z))
    ay, az = cfg["abdomen_offset"]
    gaster_c = Vector((0, ay, hip_z + az))
    ar = cfg["abdomen_radii"]

    def build_abdomen(bm, o):
        tube(bm, [waist - o, waist + Vector((0, -0.07, 0.01)) - o], [0.055, 0.06], 6)  # pecíolo
        ellipsoid(bm, gaster_c - o, ar, 12, 8, Matrix.Rotation(math.radians(18), 4, "X"), poles_y=True)

    abdomen = part("Abdomen", body, waist, build_abdomen, m_body)

    def is_band(c):
        c = c + waist - gaster_c
        y = (c.y * math.cos(math.radians(18)) + c.z * math.sin(math.radians(18))) / ar[1]
        return -0.31 < y < -0.08 or -0.88 < y < -0.73  # 2 fileiras de faces na metade de trás

    paint(abdomen, m_light, is_band)

    # ---- 6 pernas: joelho sobe em arco para fora, canela desce até o chão
    lr = cfg["leg_radii"]
    for idx, hip_y, fy in cfg["legs"]:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * tr[0] * 0.85, hip_y, hip_z - 0.03))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.19, fy * 0.4, cfg["knee_lift"]))
                ankle = Vector((sx * cfg["leg_reach"], hip.y + fy * 1.1, 0.07))
                foot = ankle + Vector((sx * 0.06, fy * 0.12, -0.07))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], lr, 5)
                ellipsoid(bm, knee - o, (lr[0] * 1.05,) * 3, *cfg["knee_segs"])  # joelho

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)

    return root


# ---------------------------------------------------------------- medidas / escala
def triangle_count():
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in bpy.data.objects if o.type == "MESH")


def body_length():
    pts = [(o.matrix_world @ Vector(c)) for o in bpy.data.objects
           if o.name in ("Head", "Body", "Abdomen") for c in o.bound_box]
    return max(v.y for v in pts) - min(v.y for v in pts)


def rescale(factor):
    """Escala geometria e pivôs (sem deixar escala nos transforms)."""
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.transform(Matrix.Scale(factor, 4))
        o.location = o.location * factor
    bpy.context.view_layer.update()


# ---------------------------------------------------------------- export / preview
def to_unity_space(undo=False):
    """
    Prepara a hierarquia para o Unity (e desfaz depois do export).
    - Gira 180° em Z: o Unity lê a frente do FBX padrão como -Y do Blender.
    - Pré-gira peças e pivôs -90° em X e põe +90° em X na raiz: a conversão de eixos
      do importador do Unity (-90° em X na raiz) cancela, então a raiz chega sem
      rotação e as peças com eixos do Unity (X direita, Y cima, Z frente).
    O "Apply Transform" (bake_space_transform) do exportador quebra com hierarquia
    (gira filhos e erra posições), por isso esse caminho manual.
    """
    m = Matrix.Rotation(math.radians(-90), 4, "X") @ Matrix.Rotation(math.pi, 4, "Z")
    if undo:
        m = m.inverted()
    for o in bpy.data.objects:
        if o.type == "MESH":
            o.data.transform(m)
        if o.parent is not None:
            o.location = m @ o.location
        else:
            o.rotation_euler = (0, 0, 0) if undo else (math.radians(90), 0, 0)
    bpy.context.view_layer.update()


def export_fbx(path):
    """Frente +Y no Blender -> +Z no Unity, raiz e peças sem rotação. Testado no Unity 6."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    to_unity_space()
    try:
        bpy.ops.export_scene.fbx(
            filepath=path,
            use_selection=False,
            object_types={"EMPTY", "MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            use_mesh_modifiers=True,
            mesh_smooth_type="FACE",
            add_leaf_bones=False,
            bake_anim=False,
        )
    finally:
        to_unity_space(undo=True)


def render_preview(path, cam_pos, target, lens=70, ground_size=8):
    scene = bpy.context.scene
    engines = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items]
    scene.render.engine = "BLENDER_EEVEE" if "BLENDER_EEVEE" in engines else engines[0]
    scene.render.resolution_x = scene.render.resolution_y = 900
    scene.view_settings.view_transform = "Standard"

    world = bpy.data.worlds.new("PreviewWorld")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.62, 0.78, 0.92, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.7
    scene.world = world

    # chão verde: só no preview (o export já foi feito)
    bpy.ops.mesh.primitive_plane_add(size=ground_size, location=(0, 0, 0))
    bpy.context.active_object.data.materials.append(material("PreviewGround", (0.36, 0.62, 0.25), 0.9))

    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN"))
    sun.data.energy = 2.0
    sun.data.angle = math.radians(8)
    sun.rotation_euler = (math.radians(50), math.radians(10), math.radians(150))
    scene.collection.objects.link(sun)

    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = lens
    cam.location = cam_pos
    cam.rotation_euler = (Vector(target) - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(cam)
    scene.camera = cam

    os.makedirs(os.path.dirname(path), exist_ok=True)
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


def run(overrides, builder=None):
    """Monta (build_ant ou outro builder(cfg)), valida (triângulos), exporta o FBX
    e renderiza o preview."""
    cfg = {**DEFAULTS, **overrides}
    name = cfg["name"]
    fbx = os.path.join(ROOT, "Assets", "Art", "Models", "Characters", f"{name}.fbx")
    preview = os.path.join(HERE, "previews", f"{name}.png")

    (builder or build_ant)(cfg)
    bpy.context.view_layer.update()
    rescale(cfg["body_length"] / body_length())

    tris = triangle_count()
    pts = [(o.matrix_world @ Vector(c)) for o in bpy.data.objects if o.type == "MESH" for c in o.bound_box]
    total = max(v.y for v in pts) - min(v.y for v in pts)
    height = max(v.z for v in pts)
    print(f"[{name}] triangulos={tris} corpo={body_length():.2f}m total={total:.2f}m altura={height:.2f}m")
    print(f"[{name}] pecas=" + ", ".join(sorted(o.name for o in bpy.data.objects if o.parent is not None)))
    if tris > MAX_TRIS:
        print(f"[{name}] ERRO: {tris} triangulos, limite {MAX_TRIS}")
        sys.exit(1)
    export_fbx(fbx)
    render_preview(preview, cfg["cam_pos"], cfg["cam_target"], cfg["lens"])
    print(f"[{name}] FBX: {fbx}")
    print(f"[{name}] preview: {preview}")

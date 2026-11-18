"""Procedural Blender generator for stylized chests, shrines, pickups, pipe & backyard props (.glb)."""
import math
import os
import sys
import random

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import bpy
import bmesh
from mathutils import Vector, Euler, Matrix
from art_common import (
    reset_scene,
    make_material,
    create_mesh_object,
    add_ellipsoid_obj,
    add_tapered_limb,
    add_beveled_box,
    join_children_by_material,
    ground_hierarchy_to_z0,
    validate_and_export_glb,
    render_turntable_preview,
)

ITEMS_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "../../Assets/Art/Models/Items"))
ENV_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "../../Assets/Art/Models/Environment"))
PREVIEW_DIR = "/tmp/art_review"


def build_chest(out_name, scale=1.0, large=False):
    reset_scene()
    m_wood = make_material("ChestWood", (0.44, 0.24, 0.12) if not large else (0.36, 0.16, 0.10), roughness=0.58)
    m_plank = make_material("ChestPlankDark", (0.28, 0.14, 0.07), roughness=0.62)
    m_metal = make_material(
        "ChestMetal",
        (0.86, 0.66, 0.22) if large else (0.38, 0.42, 0.46),
        roughness=0.28,
        metallic=0.72,
    )
    m_glow = make_material(
        "ChestCoreGlow",
        (1.0, 0.72, 0.22) if large else (0.28, 0.88, 0.96),
        roughness=0.2,
        emission=(1.0, 0.68, 0.18) if large else (0.25, 0.85, 0.96),
        emission_strength=1.4,
    )

    w = 1.32 * scale
    d = 0.92 * scale
    bh = 0.56 * scale
    hinge_y = d * 0.46  # Rear hinge (+Y in Blender = -Z in Unity)

    base_parts = []
    # Main beveled wooden chest box grounded near Z=0
    base_parts.append(add_beveled_box("BaseCore", (0, 0, bh * 0.5 + 0.04 * scale), (w, d, bh), m_wood, bevel_width=0.03 * scale, segments=2))
    # Corner plank feet & side trim straps
    for sx in (-1, 1):
        for sy in (-1, 1):
            base_parts.append(
                add_beveled_box(
                    f"Corner_{sx}_{sy}",
                    (sx * (w * 0.48), sy * (d * 0.46), bh * 0.52 + 0.04 * scale),
                    (0.11 * scale, 0.11 * scale, bh + 0.06 * scale),
                    m_metal,
                    bevel_width=0.015 * scale,
                    segments=1,
                )
            )
    # Horizontal decorative wooden slats front & back
    for sy in (-1, 1):
        base_parts.append(
            add_beveled_box(
                f"TrimBar_{sy}",
                (0, sy * (d * 0.50), bh * 0.55),
                (w * 0.92, 0.04 * scale, 0.09 * scale),
                m_plank,
                bevel_width=0.01 * scale,
                segments=1,
            )
        )
    # Front clasp lockplate (-Y in Blender = +Z front in Unity!)
    base_parts.append(
        add_beveled_box(
            "LockPlate",
            (0, -d * 0.51, bh * 0.78),
            (0.24 * scale, 0.07 * scale, 0.24 * scale),
            m_metal,
            bevel_width=0.018 * scale,
            segments=1,
        )
    )
    base_parts.append(
        add_ellipsoid_obj(
            "LockGem",
            (0, -d * 0.55, bh * 0.80),
            (0.065 * scale, 0.035 * scale, 0.065 * scale),
            m_glow,
            segs=10,
            rings=6,
            smooth=False,
        )
    )
    base = join_children_by_material(base_parts, "Body")

    # Hinged Lid (arched barrel lid + riveted brass bands + bottlecap crest on top)
    lid_parts = []
    # Half-cylinder arched lid aligned along X
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=d * 0.50, radius2=d * 0.50, depth=w * 0.96)
    for v in bm.verts:
        # Rotate cone cylinder axis from Z to X, flatten bottom half cleanly at Z >= bh
        vx, vy, vz = v.co.z, v.co.x, v.co.y
        if vz < 0:
            vz = 0.0
        v.co = Vector((vx, vy, bh + 0.04 * scale + vz * 0.68))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    lid_parts.append(create_mesh_object("LidWood", bm, material=m_wood, smooth=False))

    # Metal straps arched over lid
    for sx in (-0.34 * w, 0.0 if large else None, 0.34 * w):
        if sx is None:
            continue
        strap = bmesh.new()
        bmesh.ops.create_cone(strap, cap_ends=True, cap_tris=False, segments=14, radius1=d * 0.525, radius2=d * 0.525, depth=0.09 * scale)
        for v in strap.verts:
            vx, vy, vz = v.co.z + sx, v.co.x, max(-0.02 * scale, v.co.y * 0.72)
            v.co = Vector((vx, vy, bh + 0.04 * scale + vz))
        bmesh.ops.recalc_face_normals(strap, faces=strap.faces)
        lid_parts.append(create_mesh_object(f"Strap_{sx:.2f}", strap, material=m_metal, smooth=False))

    # Front latch tongue hanging slightly over rim
    lid_parts.append(
        add_beveled_box(
            "LidClasp",
            (0, -d * 0.52, bh + 0.02 * scale),
            (0.16 * scale, 0.06 * scale, 0.13 * scale),
            m_metal,
            bevel_width=0.012 * scale,
            segments=1,
        )
    )

    lid = join_children_by_material(lid_parts, "Lid")
    pivot = Vector((0, hinge_y, bh + 0.04 * scale))
    for v in lid.data.vertices:
        v.co -= pivot
    lid.location = pivot

    ground_hierarchy_to_z0([base, lid])
    glb = os.path.join(ITEMS_DIR, f"{out_name}.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, f"{out_name}.png"), subject_center=(0, 0, 0.45 * scale), distance=2.4 * scale)
    print(f"Exported {out_name}.glb ({tris} tris)")


def build_chance_shrine():
    reset_scene()
    m_stone = make_material("ShrineStone", (0.42, 0.44, 0.45), roughness=0.78)
    m_moss = make_material("ShrineMoss", (0.28, 0.54, 0.24), roughness=0.72)
    m_gold = make_material("CapGold", (0.94, 0.74, 0.22), roughness=0.28, metallic=0.75)
    m_luck = make_material("LuckCore", (0.35, 0.96, 0.48), roughness=0.22, emission=(0.28, 0.94, 0.45), emission_strength=1.5)

    altar_parts = []
    # Stepped octagonal mossy stone base
    for i, (r, h, z, m) in enumerate([
        (0.74, 0.20, 0.10, m_stone),
        (0.58, 0.16, 0.28, m_moss),
        (0.42, 0.76, 0.74, m_stone),
        (0.52, 0.14, 1.18, m_stone),
    ]):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=8, radius1=r * 1.06, radius2=r * 0.96, depth=h)
        for v in bm.verts:
            v.co.z += z
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.025, segments=1, affect="EDGES")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        altar_parts.append(create_mesh_object(f"Tier_{i}", bm, material=m, smooth=False))

    altar = join_children_by_material(altar_parts, "Altar")

    # Separate rotating lucky bottlecap medallion crystal
    crys_parts = []
    # Crimped gold bottlecap rim
    bm = bmesh.new()
    segs = 20
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=0.36, radius2=0.33, depth=0.10)
    for v in bm.verts:
        ang = math.atan2(v.co.y, v.co.x)
        r = math.hypot(v.co.x, v.co.y)
        if r > 0.28:
            crimp = 1.0 + 0.09 * math.cos(ang * 10.0)
            v.co.x *= crimp
            v.co.y *= crimp
        v.co = Euler((math.radians(55), 0, 0)).to_matrix() @ v.co
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    crys_parts.append(create_mesh_object("CapRim", bm, material=m_gold, smooth=False))

    # Glowing lucky gem center
    gem = add_ellipsoid_obj("LuckGem", (0, 0, 0), (0.18, 0.18, 0.10), m_luck, segs=8, rings=4, smooth=False)
    gem.rotation_euler = Euler((math.radians(55), 0, 0))
    crys_parts.append(gem)

    crystal = join_children_by_material(crys_parts, "Crystal")
    crystal.location = (0, 0, 1.72)

    ground_hierarchy_to_z0([altar, crystal])
    glb = os.path.join(ITEMS_DIR, "ChanceShrine.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "ChanceShrine.png"), subject_center=(0, 0, 1.0), distance=3.4)
    print(f"Exported ChanceShrine.glb ({tris} tris)")


def build_blood_shrine():
    reset_scene()
    m_basalt = make_material("Basalt", (0.20, 0.18, 0.21), roughness=0.72)
    m_iron = make_material("BloodIron", (0.34, 0.14, 0.16), roughness=0.38, metallic=0.45)
    m_blood = make_material("BloodRuby", (0.94, 0.14, 0.16), roughness=0.18, emission=(0.95, 0.10, 0.12), emission_strength=1.6)

    altar_parts = []
    # Basalt stepped monolith base + 4 spiky sacrificial prongs
    for i, (r, h, z, m) in enumerate([
        (0.76, 0.22, 0.11, m_basalt),
        (0.56, 0.12, 0.28, m_blood),
        (0.40, 0.84, 0.74, m_basalt),
        (0.50, 0.14, 1.22, m_iron),
    ]):
        bm = bmesh.new()
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=6, radius1=r * 1.08, radius2=r * 0.94, depth=h)
        for v in bm.verts:
            v.co.z += z
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.02, segments=1, affect="EDGES")
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        altar_parts.append(create_mesh_object(f"Tier_{i}", bm, material=m, smooth=False))

    for k in range(4):
        ang = math.radians(45 + k * 90)
        cx, cy = math.cos(ang), math.sin(ang)
        altar_parts.append(
            add_tapered_limb(
                f"Prong_{k}",
                [(cx * 0.36, cy * 0.36, 1.18), (cx * 0.48, cy * 0.48, 1.48), (cx * 0.32, cy * 0.32, 1.82)],
                [0.075, 0.055, 0.015],
                m_iron,
                sides=5,
                smooth=False,
            )
        )
    altar = join_children_by_material(altar_parts, "Altar")

    # Separate faceted blood crystal
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.30)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.75, v.co.y * 0.75, v.co.z * 1.45))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    crystal = create_mesh_object("Crystal", bm, material=m_blood, smooth=False)
    crystal.location = (0, 0, 1.82)

    ground_hierarchy_to_z0([altar, crystal])
    glb = os.path.join(ITEMS_DIR, "BloodShrine.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "BloodShrine.png"), subject_center=(0, 0, 1.0), distance=3.4)
    print(f"Exported BloodShrine.glb ({tris} tris)")


def build_pickups():
    # 1. Crumb.glb (irregular toasted golden cookie/bread chunk!)
    reset_scene()
    m_crumb = make_material("CrumbGold", (0.92, 0.68, 0.30), roughness=0.68)
    m_crust = make_material("CrumbCrust", (0.64, 0.36, 0.14), roughness=0.72)

    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.45)
    rng = random.Random(42)
    for v in bm.verts:
        n = 1.0 + rng.uniform(-0.22, 0.24)
        v.co = Vector((v.co.x * n * 1.15, v.co.y * n * 0.95, v.co.z * n * 0.85))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts = [create_mesh_object("Chunk", bm, material=m_crumb, smooth=False)]
    for i, pos in enumerate([(0.18, -0.20, 0.15), (-0.16, 0.14, 0.18), (0.05, 0.22, -0.10)]):
        parts.append(add_ellipsoid_obj(f"Spec_{i}", pos, (0.14, 0.12, 0.11), m_crust, segs=6, rings=5, smooth=False))
    crumb = join_children_by_material(parts, "Crumb")
    validate_and_export_glb(os.path.join(ITEMS_DIR, "Crumb.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "Crumb.png"), subject_center=(0, 0, 0), distance=1.8)

    # 2. ItemPickup.glb (faceted jewel prism inside gyro ring)
    reset_scene()
    m_core = make_material("ItemCore", (1.0, 1.0, 1.0), roughness=0.22, emission=(0.75, 0.85, 1.0), emission_strength=0.6)
    m_ring = make_material("ItemRing", (0.94, 0.80, 0.34), roughness=0.26, metallic=0.75)

    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.30)
    for v in bm.verts:
        v.co.z *= 1.35
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts = [create_mesh_object("Jewel", bm, material=m_core, smooth=False)]

    # Closed seamless brass torus ring around jewel
    bm_ring = bmesh.new()
    segs_maj, segs_min = 24, 6
    R_maj, r_min = 0.44, 0.036
    rot = Euler((math.radians(32), math.radians(20), 0)).to_matrix()
    t_rings = []
    for i in range(segs_maj):
        a = 2.0 * math.pi * i / segs_maj
        ca, sa = math.cos(a), math.sin(a)
        center = Vector((ca * R_maj, sa * R_maj, 0.0))
        rad_dir = Vector((ca, sa, 0.0))
        up_dir = Vector((0.0, 0.0, 1.0))
        ring = []
        for j in range(segs_min):
            b = 2.0 * math.pi * j / segs_min
            p = center + rad_dir * (math.cos(b) * r_min) + up_dir * (math.sin(b) * r_min)
            ring.append(bm_ring.verts.new(rot @ p))
        t_rings.append(ring)
    for i in range(segs_maj):
        ni = (i + 1) % segs_maj
        for j in range(segs_min):
            nj = (j + 1) % segs_min
            bm_ring.faces.new((t_rings[i][j], t_rings[ni][j], t_rings[ni][nj], t_rings[i][nj]))
    bmesh.ops.recalc_face_normals(bm_ring, faces=bm_ring.faces)
    parts.append(create_mesh_object("GyroRing", bm_ring, material=m_ring, smooth=False))
    pickup = join_children_by_material(parts, "ItemPickup")
    tris = validate_and_export_glb(os.path.join(ITEMS_DIR, "ItemPickup.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "ItemPickup.png"), subject_center=(0, 0, 0), distance=1.9)
    print(f"Exported Crumb.glb & ItemPickup.glb ({tris} tris)")


def build_exit_pipe():
    reset_scene()
    m_copper = make_material("PipeMetal", (0.36, 0.42, 0.40), roughness=0.42, metallic=0.68)
    m_rust = make_material("PipeRust", (0.48, 0.28, 0.16), roughness=0.68, metallic=0.25)
    m_dark = make_material("PipeDark", (0.04, 0.05, 0.06), roughness=0.9)
    m_brass = make_material("PipeBolt", (0.76, 0.62, 0.28), roughness=0.34, metallic=0.72)

    parts = []
    # Main horizontal corrugated pipe extending from y=-0.2 (-Y facing +Z Unity mouth) back to y=+6.6
    # Outer shell ring segments
    def make_pipe_tube(name, r_outer, r_inner, y0, y1, mat):
        bm = bmesh.new()
        segs = 20
        ring_f_out, ring_b_out, ring_f_in, ring_b_in = [], [], [], []
        for s in range(segs):
            a = 2.0 * math.pi * s / segs
            cx, cz = math.cos(a), math.sin(a)
            ring_f_out.append(bm.verts.new((cx * r_outer, y0, 1.5 + cz * r_outer)))
            ring_b_out.append(bm.verts.new((cx * r_outer, y1, 1.5 + cz * r_outer)))
            ring_f_in.append(bm.verts.new((cx * r_inner, y0, 1.5 + cz * r_inner)))
            ring_b_in.append(bm.verts.new((cx * r_inner, y1, 1.5 + cz * r_inner)))
        for s in range(segs):
            ns = (s + 1) % segs
            bm.faces.new((ring_f_out[s], ring_f_out[ns], ring_b_out[ns], ring_b_out[s]))
            bm.faces.new((ring_b_in[s], ring_b_in[ns], ring_f_in[ns], ring_f_in[s]))
            bm.faces.new((ring_f_in[s], ring_f_in[ns], ring_f_out[ns], ring_f_out[s]))
            bm.faces.new((ring_b_out[s], ring_b_out[ns], ring_b_in[ns], ring_b_in[s]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        return create_mesh_object(name, bm, material=mat, smooth=False)

    parts.append(make_pipe_tube("Barrel", 2.22, 1.95, 0.0, 6.6, m_copper))
    parts.append(make_pipe_tube("FlangeRim", 2.58, 1.92, -0.25, 0.12, m_rust))
    for rib_y in (1.4, 3.0, 4.6):
        parts.append(make_pipe_tube(f"Rib_{rib_y}", 2.34, 2.15, rib_y, rib_y + 0.25, m_rust))

    # 8 brass flange bolts around mouth
    for k in range(8):
        a = math.radians(k * 45 + 22.5)
        bx, bz = math.cos(a) * 2.40, 1.5 + math.sin(a) * 2.40
        parts.append(add_ellipsoid_obj(f"Bolt_{k}", (bx, -0.27, bz), (0.11, 0.09, 0.11), m_brass, segs=6, rings=4, smooth=False))

    # Deep tunnel dark backing disk inside throat
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=18, radius1=1.96, radius2=1.96, depth=0.2)
    for v in bm.verts:
        v.co = Vector((v.co.x, 4.5 + v.co.z, 1.5 + v.co.y))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(create_mesh_object("DarkCore", bm, material=m_dark, smooth=False))

    root = join_children_by_material(parts, "ExitPipe")
    glb = os.path.join(ENV_DIR, "ExitPipe.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "ExitPipe.png"), subject_center=(0, 1.5, 1.5), distance=7.5)
    print(f"Exported ExitPipe.glb ({tris} tris)")


def build_environment_props():
    # Normalized 1x1x1 meshes (-0.5..+0.5) so MeshFilter swaps match ALL BoxCollider & scale cleanly!
    # 1. GardenRock.glb (1x1x1 boulder)
    reset_scene()
    m_rock = make_material("GardenRock", (0.44, 0.45, 0.44), roughness=0.84)
    m_moss = make_material("RockMoss", (0.28, 0.50, 0.22), roughness=0.78)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.5)
    rng = random.Random(101)
    for v in bm.verts:
        # Faceted stratified rock displacement clamped inside ~[-0.5, 0.5]
        disp = 0.86 + rng.uniform(-0.14, 0.16)
        v.co = Vector((v.co.x * disp, v.co.y * disp, v.co.z * (0.82 if v.co.z > 0 else 0.96) * disp))
    # Normalize exact bounding box to [-0.5, +0.5]
    xs = [v.co.x for v in bm.verts]; ys = [v.co.y for v in bm.verts]; zs = [v.co.z for v in bm.verts]
    cx, cy, cz = (min(xs) + max(xs)) * 0.5, (min(ys) + max(ys)) * 0.5, (min(zs) + max(zs)) * 0.5
    sx, sy, sz = max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)
    for v in bm.verts:
        v.co = Vector(((v.co.x - cx) / sx, (v.co.y - cy) / sy, (v.co.z - cz) / sz))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    rock = create_mesh_object("GardenRock", bm, material=m_rock, smooth=False)
    rock.data.materials.append(m_moss)
    for p in rock.data.polygons:
        if p.normal.z > 0.55 and p.center.z > 0.12:
            p.material_index = 1
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "GardenRock.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "GardenRock.png"), subject_center=(0, 0, 0), distance=1.9)
    print(f"Exported GardenRock.glb ({tris} tris)")

    # 2. GiantLeaf.glb (clean convex beveled leaf with raised central midrib, zero V-slits)
    reset_scene()
    m_leaf = make_material("LeafBlade", (0.34, 0.62, 0.24), roughness=0.58)
    m_vein = make_material("LeafVein", (0.52, 0.75, 0.32), roughness=0.54)
    bm = bmesh.new()
    segs_y = 14
    top_rows, bot_rows = [], []
    for i in range(segs_y + 1):
        t = i / segs_y
        y = -0.5 + t
        half_w = math.sin(t * math.pi) * 0.48 + 0.025
        arch = math.sin(t * math.pi) * 0.08 - 0.02
        xs = [-half_w, -half_w * 0.45, 0.0, half_w * 0.45, half_w]
        z_top = [arch + 0.015, arch + 0.045, arch + 0.075, arch + 0.045, arch + 0.015]
        z_bot = [arch - 0.015, arch - 0.055, arch - 0.095, arch - 0.055, arch - 0.015]
        top_rows.append([bm.verts.new((xs[j], y, z_top[j])) for j in range(5)])
        bot_rows.append([bm.verts.new((xs[j], y, z_bot[j])) for j in range(5)])

    for i in range(segs_y):
        for j in range(4):
            bm.faces.new((top_rows[i][j], top_rows[i + 1][j], top_rows[i + 1][j + 1], top_rows[i][j + 1]))
            bm.faces.new((bot_rows[i][j + 1], bot_rows[i + 1][j + 1], bot_rows[i + 1][j], bot_rows[i][j]))
        bm.faces.new((bot_rows[i][0], bot_rows[i + 1][0], top_rows[i + 1][0], top_rows[i][0]))
        bm.faces.new((top_rows[i][4], top_rows[i + 1][4], bot_rows[i + 1][4], bot_rows[i][4]))
    for j in range(4):
        bm.faces.new((bot_rows[0][j], top_rows[0][j], top_rows[0][j + 1], bot_rows[0][j + 1]))
        bm.faces.new((top_rows[-1][j], bot_rows[-1][j], bot_rows[-1][j + 1], top_rows[-1][j + 1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    leaf = create_mesh_object("GiantLeaf", bm, material=m_leaf, smooth=False)
    leaf.data.materials.append(m_vein)
    for p in leaf.data.polygons:
        if abs(p.center.x) < 0.09:
            p.material_index = 1
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "GiantLeaf.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "GiantLeaf.png"), subject_center=(0, 0, 0.05), distance=1.6)
    print(f"Exported GiantLeaf.glb ({tris} tris)")

    # 3. TerracottaPot.glb (matches Cylinder r=0.5, z=-1..+1 flipped upside down: wide lip at bottom z=-1!)
    reset_scene()
    m_clay = make_material("Terracotta", (0.74, 0.38, 0.22), roughness=0.72)
    m_rim = make_material("TerracottaRim", (0.62, 0.30, 0.17), roughness=0.76)
    bm = bmesh.new()
    segs = 18
    profile = [
        (-1.0, 0.50),
        (-0.76, 0.50),
        (-0.76, 0.45),
        (0.95, 0.34),
        (1.0, 0.31),
    ]
    rings = []
    for z, r in profile:
        rings.append([bm.verts.new((math.cos(2 * math.pi * s / segs) * r, math.sin(2 * math.pi * s / segs) * r, z)) for s in range(segs)])
    for i in range(len(rings) - 1):
        for s in range(segs):
            ns = (s + 1) % segs
            bm.faces.new((rings[i][s], rings[i][ns], rings[i + 1][ns], rings[i + 1][s]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    pot = create_mesh_object("TerracottaPot", bm, material=m_clay, smooth=False)
    pot.data.materials.append(m_rim)
    for p in pot.data.polygons:
        if p.center.z < -0.75:
            p.material_index = 1
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "TerracottaPot.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "TerracottaPot.png"), subject_center=(0, 0, 0), distance=2.8)
    print(f"Exported TerracottaPot.glb ({tris} tris)")

    # 4. SodaCan.glb (matches Cylinder r=0.5, z=-1..+1)
    reset_scene()
    m_red = make_material("CanRed", (0.78, 0.14, 0.14), roughness=0.28, metallic=0.55)
    m_silver = make_material("CanSilver", (0.82, 0.84, 0.86), roughness=0.22, metallic=0.88)
    bm = bmesh.new()
    segs = 18
    profile = [(-1.0, 0.42), (-0.88, 0.50), (0.72, 0.50), (0.90, 0.43), (1.0, 0.44)]
    rings = []
    for z, r in profile:
        rings.append([bm.verts.new((math.cos(2 * math.pi * s / segs) * r, math.sin(2 * math.pi * s / segs) * r, z)) for s in range(segs)])
    for i in range(len(rings) - 1):
        for s in range(segs):
            ns = (s + 1) % segs
            bm.faces.new((rings[i][s], rings[i][ns], rings[i + 1][ns], rings[i + 1][s]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    can = create_mesh_object("SodaCan", bm, material=m_red, smooth=False)
    can.data.materials.append(m_silver)
    for p in can.data.polygons:
        if p.center.z < -0.84 or p.center.z > 0.72:
            p.material_index = 1
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "SodaCan.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "SodaCan.png"), subject_center=(0, 0, 0), distance=2.6)
    print(f"Exported SodaCan.glb ({tris} tris)")

    # 5. GrassTuft.glb (clean non-twisting curved tapered grass tuft fitting [-0.5..+0.5])
    reset_scene()
    m_grass = make_material("GrassGreen", (0.28, 0.58, 0.20), roughness=0.65)
    parts = []
    for idx_b, (bx, lean_x, lean_y, h, w0) in enumerate([
        (-0.18, -0.18, -0.06, 0.90, 0.14),
        (0.0, 0.04, 0.10, 1.00, 0.15),
        (0.18, 0.20, -0.08, 0.84, 0.13),
    ]):
        bm = bmesh.new()
        steps = 4
        rings = []
        for s in range(steps + 1):
            t = s / steps
            w = w0 * (1.0 - t * 0.88)
            th = 0.045 * (1.0 - t * 0.82)
            cx = bx + lean_x * (t * t)
            cy = lean_y * (t * t)
            cz = -0.5 + h * t
            rings.append([
                bm.verts.new((cx - w, cy, cz)),
                bm.verts.new((cx, cy - th, cz)),
                bm.verts.new((cx + w, cy, cz)),
                bm.verts.new((cx, cy + th, cz)),
            ])
        for s in range(steps):
            for k in range(4):
                nk = (k + 1) % 4
                bm.faces.new((rings[s][k], rings[s + 1][k], rings[s + 1][nk], rings[s][nk]))
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        parts.append(create_mesh_object(f"Blade_{idx_b}", bm, material=m_grass, smooth=False))
    tuft = join_children_by_material(parts, "GrassTuft")
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "GrassTuft.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "GrassTuft.png"), subject_center=(0, 0, 0), distance=1.8)
    print(f"Exported GrassTuft.glb ({tris} tris)")

    # 5b. ToyBrickBase.glb (1x1x1 beveled brick)
    reset_scene()
    m_brick = make_material("ToyRed", (0.82, 0.16, 0.14), roughness=0.35)
    add_beveled_box("ToyBrickBase", (0, 0, 0), (1.0, 1.0, 1.0), m_brick, bevel_width=0.05, segments=2)
    validate_and_export_glb(os.path.join(ENV_DIR, "ToyBrickBase.glb"))

    # 6. Pebble.glb (1x1x1 faceted stone)
    reset_scene()
    m_peb = make_material("PebbleGray", (0.52, 0.50, 0.46), roughness=0.80)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=1, radius=0.5)
    for v in bm.verts:
        v.co = Vector((v.co.x * 1.05, v.co.y * 0.92, v.co.z * 0.72))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    create_mesh_object("Pebble", bm, material=m_peb, smooth=False)
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "Pebble.glb"))

    # 7. TermiteMound.glb (cracked earthen burrow ring)
    reset_scene()
    m_dirt = make_material("DirtMound", (0.42, 0.28, 0.16), roughness=0.88)
    parts = []
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=12, radius1=0.68, radius2=0.34, depth=0.32)
    rng2 = random.Random(7)
    for v in bm.verts:
        v.co.z += 0.16
        if v.co.z > 0.2:
            v.co.x *= rng2.uniform(0.86, 1.14)
            v.co.y *= rng2.uniform(0.86, 1.14)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(create_mesh_object("MoundCore", bm, material=m_dirt, smooth=False))
    for k in range(7):
        a = 2.0 * math.pi * k / 7.0
        parts.append(add_ellipsoid_obj(f"Clod_{k}", (math.cos(a) * 0.52, math.sin(a) * 0.52, 0.08), (0.16, 0.14, 0.09), m_dirt, segs=6, rings=5, smooth=False))
    join_children_by_material(parts, "TermiteMound")
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "TermiteMound.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "TermiteMound.png"), subject_center=(0, 0, 0.15), distance=2.0)
    print(f"Exported Pebble.glb & TermiteMound.glb ({tris} tris)")


if __name__ == "__main__":
    build_chest("ChestSmall", scale=1.0, large=False)
    build_chest("ChestLarge", scale=1.35, large=True)
    build_chance_shrine()
    build_blood_shrine()
    build_pickups()
    build_exit_pipe()
    build_environment_props()

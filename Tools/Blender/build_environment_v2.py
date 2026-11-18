"""Procedural Blender generator for backyard horizon, fence, flora, and landmarks (.glb)."""
import math
import os
import sys
import random

SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if SCRIPT_DIR not in sys.path:
    sys.path.insert(0, SCRIPT_DIR)

import bpy
import bmesh
from mathutils import Vector, Euler
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

ENV_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "../../Assets/Art/Models/Environment"))
PREVIEW_DIR = "/tmp/art_review_v2"


def build_fence_plank():
    # Normalized 1x1x1 beveled picket plank ([-0.5..+0.5]) so scale on Fence boards stays 1:1!
    reset_scene()
    m_wood = make_material("FenceWood", (0.46, 0.29, 0.16), roughness=0.72)
    m_top = make_material("FenceCap", (0.38, 0.23, 0.12), roughness=0.76)

    bm = bmesh.new()
    # Tapered picket top from z=+0.38 to z=+0.50
    bot = [
        bm.verts.new((-0.48, -0.45, -0.50)),
        bm.verts.new((0.48, -0.45, -0.50)),
        bm.verts.new((0.48, 0.45, -0.50)),
        bm.verts.new((-0.48, 0.45, -0.50)),
    ]
    mid = [
        bm.verts.new((-0.48, -0.45, 0.36)),
        bm.verts.new((0.48, -0.45, 0.36)),
        bm.verts.new((0.48, 0.45, 0.36)),
        bm.verts.new((-0.48, 0.45, 0.36)),
    ]
    top = [
        bm.verts.new((-0.26, -0.35, 0.50)),
        bm.verts.new((0.26, -0.35, 0.50)),
        bm.verts.new((0.26, 0.35, 0.50)),
        bm.verts.new((-0.26, 0.35, 0.50)),
    ]
    for i in range(4):
        ni = (i + 1) % 4
        bm.faces.new((bot[i], bot[ni], mid[ni], mid[i]))
        bm.faces.new((mid[i], mid[ni], top[ni], top[i]))
    bm.faces.new(list(reversed(bot)))
    bm.faces.new(top)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.03, segments=1, affect="EDGES")
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    obj = create_mesh_object("FencePlank", bm, material=m_wood, smooth=False)
    obj.data.materials.append(m_top)
    for p in obj.data.polygons:
        if p.center.z > 0.34:
            p.material_index = 1
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "FencePlank.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "FencePlank.png"), subject_center=(0, 0, 0), distance=1.8)
    print(f"Exported FencePlank.glb ({tris} tris)")


def build_tree_silhouette():
    reset_scene()
    m_bark = make_material("TreeBark", (0.34, 0.22, 0.13), roughness=0.84)
    m_canopy = make_material("TreeCanopy", (0.20, 0.48, 0.22), roughness=0.68)
    m_canopy_hi = make_material("TreeCanopyHi", (0.30, 0.60, 0.26), roughness=0.64)

    parts = []
    # Flared trunk + 3 main canopy limbs
    parts.append(
        add_tapered_limb(
            "Trunk",
            [(0, 0, 0), (0.2, 0.1, 4.5), (-0.1, -0.2, 9.5), (0, 0, 14.0)],
            [2.4, 1.45, 1.15, 0.75],
            m_bark,
            sides=9,
            smooth=False,
        )
    )
    for i, (bx, by, bz) in enumerate([(-3.5, 1.2, 14.5), (3.6, -1.0, 14.0), (0.4, 3.2, 15.2)]):
        parts.append(
            add_tapered_limb(
                f"Branch_{i}",
                [(0, 0, 10.5), (bx * 0.55, by * 0.55, 12.6), (bx, by, bz)],
                [0.75, 0.55, 0.32],
                m_bark,
                sides=7,
                smooth=False,
            )
        )
    for k, (cx, cy, cz, rx, ry, rz, mat) in enumerate([
        (0.0, 0.0, 18.0, 6.2, 5.8, 4.8, m_canopy),
        (-4.2, 1.5, 15.2, 4.2, 4.0, 3.4, m_canopy_hi),
        (4.2, -1.2, 14.8, 4.4, 4.2, 3.5, m_canopy),
        (0.5, 3.6, 16.0, 3.9, 3.9, 3.2, m_canopy_hi),
        (-1.2, -3.2, 15.6, 4.0, 3.8, 3.1, m_canopy_hi),
    ]):
        parts.append(add_ellipsoid_obj(f"Canopy_{k}", (cx, cy, cz), (rx, ry, rz), mat, segs=10, rings=7, smooth=False))

    root = join_children_by_material(parts, "TreeSilhouette")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "TreeSilhouette.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "TreeSilhouette.png"), subject_center=(0, 0, 11.0), distance=28.0)
    print(f"Exported TreeSilhouette.glb ({tris} tris)")


def build_bush_cluster():
    reset_scene()
    m_deep = make_material("BushDeep", (0.18, 0.44, 0.18), roughness=0.72)
    m_hi = make_material("BushHi", (0.30, 0.62, 0.24), roughness=0.66)
    parts = []
    for i, (cx, cy, cz, rx, ry, rz, m) in enumerate([
        (0.0, 0.0, 1.5, 2.2, 2.0, 1.6, m_deep),
        (-1.5, 0.6, 1.2, 1.5, 1.4, 1.25, m_hi),
        (1.4, -0.5, 1.25, 1.6, 1.5, 1.3, m_hi),
        (0.2, 1.1, 1.9, 1.4, 1.3, 1.2, m_hi),
    ]):
        parts.append(add_ellipsoid_obj(f"Puff_{i}", (cx, cy, cz), (rx, ry, rz), m, segs=10, rings=7, smooth=False))
    root = join_children_by_material(parts, "BushCluster")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "BushCluster.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "BushCluster.png"), subject_center=(0, 0, 1.4), distance=6.5)
    print(f"Exported BushCluster.glb ({tris} tris)")


def build_clover_patch():
    reset_scene()
    m_clover = make_material("CloverGreen", (0.24, 0.58, 0.22), roughness=0.58)
    m_stem = make_material("CloverStem", (0.34, 0.56, 0.20), roughness=0.65)
    parts = []
    for c_idx, (ox, oy, h) in enumerate([(-0.14, -0.05, 0.40), (0.15, 0.08, 0.30)]):
        parts.append(add_tapered_limb(f"Stem_{c_idx}", [(ox, oy, 0), (ox, oy, h + 0.02)], [0.028, 0.02], m_stem, sides=5, smooth=False))
        for k in range(3):
            a = c_idx * 0.7 + 2.0 * math.pi * k / 3.0
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=8, v_segments=4, radius=1.0)
            rot_m = Euler((0, 0.14, a)).to_matrix()
            for v in bm.verts:
                p = Vector((v.co.x * 0.11 + 0.095, v.co.y * 0.105, v.co.z * 0.025))
                v.co = Vector((ox, oy, h)) + (rot_m @ p)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            parts.append(create_mesh_object(f"Leaf_{c_idx}_{k}", bm, material=m_clover, smooth=False))
    root = join_children_by_material(parts, "CloverPatch")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "CloverPatch.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "CloverPatch.png"), subject_center=(0, 0, 0.22), distance=1.1)
    print(f"Exported CloverPatch.glb ({tris} tris)")


def build_daisy_flower():
    reset_scene()
    m_stem = make_material("FlowerStem", (0.28, 0.56, 0.20), roughness=0.62)
    m_petal = make_material("DaisyPetal", (0.96, 0.95, 0.90), roughness=0.45)
    m_core = make_material("DaisyCenter", (0.96, 0.74, 0.16), roughness=0.42)
    parts = []
    for b_idx, (ox, oy, h, tilt_x) in enumerate([(0.0, 0.0, 0.72, 0.10), (0.18, -0.10, 0.52, -0.12)]):
        parts.append(add_tapered_limb(f"Stem_{b_idx}", [(ox, oy, 0), (ox + tilt_x * 0.5, oy, h * 0.55), (ox + tilt_x, oy, h)], [0.028, 0.022, 0.018], m_stem, sides=5, smooth=False))
        cx, cy, cz = ox + tilt_x, oy, h
        parts.append(add_ellipsoid_obj(f"Core_{b_idx}", (cx, cy, cz + 0.015), (0.09, 0.09, 0.05), m_core, segs=8, rings=5, smooth=False))
        for k in range(8):
            a = 2.0 * math.pi * k / 8.0 + b_idx * 0.3
            bm = bmesh.new()
            bmesh.ops.create_uvsphere(bm, u_segments=6, v_segments=4, radius=1.0)
            rot_m = Euler((0, -0.14, a)).to_matrix()
            for v in bm.verts:
                p = Vector((v.co.x * 0.095 + 0.125, v.co.y * 0.045, v.co.z * 0.018))
                v.co = Vector((cx, cy, cz)) + (rot_m @ p)
            bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
            parts.append(create_mesh_object(f"Pet_{b_idx}_{k}", bm, material=m_petal, smooth=False))
    root = join_children_by_material(parts, "DaisyFlower")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "DaisyFlower.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "DaisyFlower.png"), subject_center=(0.05, 0, 0.42), distance=1.35)
    print(f"Exported DaisyFlower.glb ({tris} tris)")


def build_mushroom_cluster():
    reset_scene()
    m_stem = make_material("MushStem", (0.90, 0.85, 0.74), roughness=0.62)
    m_cap = make_material("MushCap", (0.82, 0.20, 0.16), roughness=0.48)
    m_spot = make_material("MushSpot", (0.96, 0.94, 0.88), roughness=0.52)
    parts = []
    for idx, (ox, oy, h, r) in enumerate([(0.0, 0.0, 0.95, 0.56), (-0.48, 0.24, 0.62, 0.36), (0.45, -0.22, 0.52, 0.30)]):
        parts.append(add_tapered_limb(f"Stalk_{idx}", [(ox, oy, 0), (ox * 1.05, oy * 1.05, h * 0.55), (ox, oy, h)], [r * 0.36, r * 0.26, r * 0.22], m_stem, sides=8, smooth=False))
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=7, radius=1.0)
        for v in bm.verts:
            if v.co.z < 0:
                v.co.z *= 0.18
            else:
                v.co.z *= 0.62
            v.co = Vector((ox + v.co.x * r, oy + v.co.y * r, h + v.co.z * r))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        parts.append(create_mesh_object(f"Cap_{idx}", bm, material=m_cap, smooth=False))
        for sp_k in range(5):
            a = sp_k * (2.0 * math.pi / 5.0) + idx * 0.6
            parts.append(
                add_ellipsoid_obj(
                    f"Spot_{idx}_{sp_k}",
                    (ox + math.cos(a) * r * 0.48, oy + math.sin(a) * r * 0.48, h + r * 0.52),
                    (r * 0.16, r * 0.16, r * 0.10),
                    m_spot,
                    segs=6,
                    rings=4,
                    smooth=False,
                )
            )
    root = join_children_by_material(parts, "MushroomCluster")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "MushroomCluster.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "MushroomCluster.png"), subject_center=(0, 0, 0.55), distance=2.2)
    print(f"Exported MushroomCluster.glb ({tris} tris)")


def build_trowel_prop():
    reset_scene()
    m_steel = make_material("TrowelSteel", (0.64, 0.68, 0.72), roughness=0.35, metallic=0.80)
    m_wood = make_material("TrowelHandle", (0.52, 0.30, 0.14), roughness=0.58)
    m_brass = make_material("TrowelCollar", (0.86, 0.66, 0.24), roughness=0.32, metallic=0.75)

    parts = []
    # Slanted giant garden hand trowel sticking out of soil at 35 deg
    tilt = Euler((math.radians(-32), math.radians(12), math.radians(20))).to_matrix()

    def tp(p):
        return tilt @ Vector(p)

    # Curved scoop blade stuck in soil (z = -0.8..3.4)
    bm = bmesh.new()
    rows = []
    for i in range(9):
        t = i / 8.0
        z = -0.6 + t * 4.0
        hw = math.sin(t * math.pi * 0.85 + 0.2) * 1.15 + 0.15
        rows.append([
            bm.verts.new(tp((-hw, 0.22, z))),
            bm.verts.new(tp((-hw * 0.45, 0.04, z))),
            bm.verts.new(tp((0.0, -0.06, z))),
            bm.verts.new(tp((hw * 0.45, 0.04, z))),
            bm.verts.new(tp((hw, 0.22, z))),
        ])
    for i in range(8):
        for j in range(4):
            bm.faces.new((rows[i][j], rows[i + 1][j], rows[i + 1][j + 1], rows[i][j + 1]))
    res = bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.14)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts.append(create_mesh_object("Blade", bm, material=m_steel, smooth=False))

    # Neck shaft + brass ferrule + turned wooden handle
    parts.append(add_tapered_limb("Neck", [tp((0, 0, 3.2)), tp((0, -0.35, 4.1)), tp((0, -0.35, 4.6))], [0.24, 0.18, 0.22], m_steel, sides=8, smooth=False))
    parts.append(add_tapered_limb("Ferrule", [tp((0, -0.35, 4.55)), tp((0, -0.35, 5.15))], [0.28, 0.34], m_brass, sides=10, smooth=False))
    parts.append(add_tapered_limb("Handle", [tp((0, -0.35, 5.10)), tp((0, -0.35, 6.4)), tp((0, -0.35, 7.6)), tp((0, -0.35, 8.2))], [0.34, 0.48, 0.52, 0.26], m_wood, sides=10, smooth=False))

    root = join_children_by_material(parts, "TrowelProp")
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "TrowelProp.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "TrowelProp.png"), subject_center=(0, -0.2, 3.6), distance=10.5)
    print(f"Exported TrowelProp.glb ({tris} tris)")


def build_bottle_cap_prop():
    reset_scene()
    m_red = make_material("CapRed", (0.86, 0.16, 0.16), roughness=0.34, metallic=0.35)
    m_white = make_material("CapWhite", (0.94, 0.94, 0.92), roughness=0.40)

    bm = bmesh.new()
    segs = 24
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=1.35, radius2=1.22, depth=0.36)
    for v in bm.verts:
        v.co.z += 0.18
        ang = math.atan2(v.co.y, v.co.x)
        r = math.hypot(v.co.x, v.co.y)
        if r > 0.95 and v.co.z < 0.24:
            crimp = 1.0 + 0.08 * math.cos(ang * 12.0)
            v.co.x *= crimp
            v.co.y *= crimp
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    parts = [create_mesh_object("CapBase", bm, material=m_red, smooth=False)]
    parts.append(add_ellipsoid_obj("Stripe", (0, 0, 0.35), (0.85, 0.25, 0.04), m_white, segs=8, rings=4, smooth=False))
    root = join_children_by_material(parts, "BottleCapProp")
    ground_hierarchy_to_z0([root])
    tris = validate_and_export_glb(os.path.join(ENV_DIR, "BottleCapProp.glb"))
    render_turntable_preview(os.path.join(PREVIEW_DIR, "BottleCapProp.png"), subject_center=(0, 0, 0.18), distance=3.4)
    print(f"Exported BottleCapProp.glb ({tris} tris)")


if __name__ == "__main__":
    build_fence_plank()
    build_tree_silhouette()
    build_bush_cluster()
    build_clover_patch()
    build_daisy_flower()
    build_mushroom_cluster()
    build_trowel_prop()
    build_bottle_cap_prop()

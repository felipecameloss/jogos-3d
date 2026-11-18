"""Procedural Blender generator for stylized low-poly insect enemies & allies (.glb)."""
import math
import os
import sys

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
    join_children_by_material,
    ground_hierarchy_to_z0,
    validate_and_export_glb,
    render_turntable_preview,
)

OUT_DIR = os.path.abspath(os.path.join(SCRIPT_DIR, "../../Assets/Art/Models/Characters"))
PREVIEW_DIR = "/tmp/art_review"


def build_jointed_leg(name, hip, knee, ankle, foot, radii, mat, claw_mat=None):
    parts = [
        add_tapered_limb(
            name,
            [hip, knee, ankle, foot],
            radii,
            mat,
            sides=7,
            smooth=False,
        )
    ]
    if claw_mat is not None:
        parts.append(add_ellipsoid_obj(f"{name}_Joint", knee, (radii[1] * 1.25,) * 3, claw_mat, segs=8, rings=6, smooth=False))
    return join_children_by_material(parts, name)


def add_eye_with_catchlight(parts, prefix, pos, radius, m_eye, m_pupil):
    x, y, z = pos
    parts.append(add_ellipsoid_obj(f"{prefix}", pos, (radius * 0.9, radius, radius * 0.95), m_eye, segs=10, rings=8, smooth=True))
    # Subtle crisp catchlight dot facing front-up-outward
    sx = 1 if x >= 0 else -1
    parts.append(
        add_ellipsoid_obj(
            f"{prefix}_Glint",
            (x + sx * radius * 0.38, y - radius * 0.68, z + radius * 0.35),
            (radius * 0.28, radius * 0.22, radius * 0.28),
            m_pupil,
            segs=8,
            rings=6,
            smooth=True,
        )
    )


def build_ant_style(
    out_name,
    scale=1.0,
    chitin_color=(0.15, 0.08, 0.06),
    shell_color=(0.36, 0.13, 0.08),
    accent_color=(0.52, 0.21, 0.11),
    eye_color=(0.04, 0.04, 0.05),
    mandible_color=(0.68, 0.48, 0.24),
    armored=False,
    horned=False,
):
    reset_scene()
    m_chitin = make_material("Chitin", chitin_color, roughness=0.52)
    m_shell = make_material("Shell", shell_color, roughness=0.46)
    m_accent = make_material("Accent", accent_color, roughness=0.48)
    m_eye = make_material("Eye", eye_color, roughness=0.14)
    m_pupil = make_material("Pupil", (0.95, 0.95, 0.98), roughness=0.2)
    m_mandible = make_material("Mandible", mandible_color, roughness=0.4, metallic=0.12)

    parts = []

    # Facing -Y (forward in Unity +Z)
    head_rx = (0.35 if armored else 0.25) * scale
    head_ry = (0.31 if armored else 0.27) * scale
    head_rz = (0.25 if armored else 0.21) * scale
    head_pos = (0, -0.58 * scale, 0.45 * scale)
    parts.append(add_ellipsoid_obj("Head", head_pos, (head_rx, head_ry, head_rz), m_shell, segs=12, rings=8, taper_y=-0.15, smooth=False))

    if armored:
        # Swept-back armored brow helmet ridges BEHIND the eyes (no eye occlusion!)
        for sx in (-1, 1):
            parts.append(
                add_tapered_limb(
                    f"BrowCrest_{sx}",
                    [
                        (sx * 0.10 * scale, -0.56 * scale, 0.65 * scale),
                        (sx * 0.22 * scale, -0.38 * scale, 0.75 * scale),
                        (sx * 0.26 * scale, -0.22 * scale, 0.66 * scale),
                    ],
                    [0.075 * scale, 0.095 * scale, 0.05 * scale],
                    m_accent,
                    sides=6,
                    smooth=False,
                )
            )
    elif horned:
        # Cute dorsal shield ridge for AllyAnt distinction
        parts.append(
            add_tapered_limb(
                "Crest",
                [(0, -0.52 * scale, 0.63 * scale), (0, -0.34 * scale, 0.71 * scale)],
                [0.055 * scale, 0.03 * scale],
                m_mandible,
                sides=6,
                smooth=False,
            )
        )

    for sx in (-1, 1):
        add_eye_with_catchlight(
            parts,
            f"Eye_{sx}",
            (sx * head_rx * 0.85, -0.68 * scale, 0.50 * scale),
            0.068 * scale,
            m_eye,
            m_pupil,
        )

    # Mandibles
    m_len = (0.48 if armored else 0.25) * scale
    m_rad = (0.068 if armored else 0.038) * scale
    for sx in (-1, 1):
        parts.append(
            add_tapered_limb(
                f"Mandible_{sx}",
                [
                    (sx * head_rx * 0.50, -0.78 * scale, 0.39 * scale),
                    (sx * (head_rx * 0.72 + (0.07 * scale if armored else 0)), -0.78 * scale - m_len * 0.52, 0.36 * scale),
                    (sx * 0.04 * scale, -0.78 * scale - m_len, 0.39 * scale),
                ],
                [m_rad, m_rad * 0.75, m_rad * 0.18],
                m_mandible,
                sides=6,
                smooth=False,
            )
        )

    # Elbowed antennae rooted cleanly on upper forehead inward of eyes
    for sx in (-1, 1):
        parts.append(
            add_tapered_limb(
                f"Antenna_{sx}",
                [
                    (sx * 0.08 * scale, -0.72 * scale, 0.59 * scale),
                    (sx * 0.22 * scale, -0.94 * scale, 0.80 * scale),
                    (sx * 0.35 * scale, -1.14 * scale, 0.69 * scale),
                ],
                [0.025 * scale, 0.018 * scale, 0.01 * scale],
                m_chitin,
                sides=6,
                smooth=False,
            )
        )

    # Neck + Sculpted Thorax + Petiole
    parts.append(add_ellipsoid_obj("Neck", (0, -0.33 * scale, 0.43 * scale), (0.11 * scale, 0.13 * scale, 0.11 * scale), m_chitin, segs=10, rings=6, smooth=False))
    parts.append(add_ellipsoid_obj("Thorax", (0, -0.08 * scale, 0.49 * scale), (0.23 * scale, 0.28 * scale, 0.21 * scale), m_shell, segs=12, rings=8, bend_z=0.05 * scale, smooth=False))
    if armored:
        parts.append(add_ellipsoid_obj("ThoraxPlate", (0, -0.06 * scale, 0.57 * scale), (0.20 * scale, 0.21 * scale, 0.13 * scale), m_accent, segs=10, rings=6, smooth=False))
        parts.append(
            add_tapered_limb(
                "DorsalHorn",
                [(0, -0.04 * scale, 0.66 * scale), (0, 0.12 * scale, 0.87 * scale)],
                [0.07 * scale, 0.016 * scale],
                m_mandible,
                sides=6,
                smooth=False,
            )
        )
    parts.append(add_ellipsoid_obj("Petiole", (0, 0.20 * scale, 0.44 * scale), (0.095 * scale, 0.12 * scale, 0.095 * scale), m_chitin, segs=8, rings=6, smooth=False))

    # Gaster with proud overlapping armor ribs so segments read clearly!
    parts.append(add_ellipsoid_obj("Gaster", (0, 0.62 * scale, 0.56 * scale), (0.33 * scale, 0.42 * scale, 0.30 * scale), m_shell, segs=14, rings=10, taper_y=-0.24, smooth=False))
    for i, (by, brx, bry, bz) in enumerate([
        (0.42 * scale, 0.31 * scale, 0.12 * scale, 0.55 * scale),
        (0.58 * scale, 0.355 * scale, 0.12 * scale, 0.565 * scale),
        (0.76 * scale, 0.31 * scale, 0.11 * scale, 0.55 * scale),
    ]):
        parts.append(
            add_ellipsoid_obj(
                f"GasterRing_{i}",
                (0, by, bz),
                (brx, bry, brx * 0.90),
                m_accent if (i % 2 == 0) else m_chitin,
                segs=12,
                rings=6,
                smooth=False,
            )
        )

    leg_configs = [
        (-0.20, -0.50, -0.58, -0.68),
        (0.0, -0.08, -0.02, 0.08),
        (0.14, 0.36, 0.58, 0.78),
    ]
    for idx, (hy, ky, ay, fy) in enumerate(leg_configs):
        for sx in (-1, 1):
            parts.append(
                build_jointed_leg(
                    f"Leg_{idx}_{sx}",
                    hip=(sx * 0.16 * scale, hy * scale, 0.40 * scale),
                    knee=(sx * 0.44 * scale, ky * scale, 0.58 * scale),
                    ankle=(sx * 0.62 * scale, ay * scale, 0.25 * scale),
                    foot=(sx * 0.72 * scale, fy * scale, 0.02 * scale),
                    radii=[0.045 * scale, 0.034 * scale, 0.024 * scale, 0.015 * scale],
                    mat=m_chitin,
                    claw_mat=m_accent,
                )
            )

    root = join_children_by_material(parts, "Body")
    ground_hierarchy_to_z0([root])
    glb = os.path.join(OUT_DIR, f"{out_name}.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, f"{out_name}.png"), subject_center=(0, 0, 0.45 * scale), distance=2.6 * scale)
    print(f"Exported {out_name}.glb ({tris} tris)")


def build_aphid():
    reset_scene()
    m_body = make_material("AphidLime", (0.42, 0.76, 0.22), roughness=0.52)
    m_belly = make_material("AphidMint", (0.62, 0.88, 0.40), roughness=0.54)
    m_stripe = make_material("AphidStripe", (0.28, 0.58, 0.16), roughness=0.50)
    m_dark = make_material("AphidDark", (0.16, 0.34, 0.12), roughness=0.52)
    m_eye = make_material("AphidEye", (0.04, 0.02, 0.03), roughness=0.14)
    m_pupil = make_material("AphidPupil", (0.95, 0.98, 0.95), roughness=0.2)

    parts = []
    # Plump segmented pear abdomen with leaf-ribbed dorsal stripes
    parts.append(add_ellipsoid_obj("Abdomen", (0, 0.16, 0.44), (0.36, 0.50, 0.33), m_body, segs=14, rings=10, taper_y=-0.26, smooth=False))
    for i, (sy, sr) in enumerate([(-0.02, 0.32), (0.16, 0.37), (0.34, 0.33)]):
        parts.append(add_ellipsoid_obj(f"Stripe_{i}", (0, sy, 0.45), (sr, 0.075, sr * 0.90), m_stripe, segs=12, rings=6, smooth=False))
    parts.append(add_ellipsoid_obj("Belly", (0, 0.14, 0.34), (0.30, 0.40, 0.20), m_belly, segs=12, rings=8, taper_y=-0.2, smooth=False))

    # Head with cute tapered mouth/rostrum
    parts.append(add_ellipsoid_obj("Head", (0, -0.40, 0.37), (0.21, 0.21, 0.18), m_belly, segs=12, rings=8, taper_y=-0.12, smooth=False))
    for sx in (-1, 1):
        add_eye_with_catchlight(parts, f"Eye_{sx}", (sx * 0.17, -0.50, 0.41), 0.052, m_eye, m_pupil)
        # Antennae cleanly arched UP and OUT from crown so they never cross eyes
        parts.append(
            add_tapered_limb(
                f"Antenna_{sx}",
                [(sx * 0.07, -0.46, 0.51), (sx * 0.16, -0.62, 0.72), (sx * 0.28, -0.52, 0.80)],
                [0.02, 0.015, 0.008],
                m_dark,
                sides=6,
                smooth=False,
            )
        )
        # Twin dorsal cornicles
        parts.append(
            add_tapered_limb(
                f"Cornicle_{sx}",
                [(sx * 0.18, 0.42, 0.58), (sx * 0.26, 0.60, 0.79)],
                [0.048, 0.024],
                m_dark,
                sides=7,
                smooth=False,
            )
        )

    for idx, (hy, ky, fy) in enumerate([(-0.22, -0.26, -0.32), (0.06, 0.08, 0.12), (0.30, 0.40, 0.52)]):
        for sx in (-1, 1):
            parts.append(
                build_jointed_leg(
                    f"Leg_{idx}_{sx}",
                    hip=(sx * 0.24, hy, 0.28),
                    knee=(sx * 0.42, ky, 0.34),
                    ankle=(sx * 0.48, fy, 0.15),
                    foot=(sx * 0.52, fy + 0.04, 0.02),
                    radii=[0.032, 0.024, 0.018, 0.012],
                    mat=m_dark,
                )
            )

    root = join_children_by_material(parts, "Body")
    ground_hierarchy_to_z0([root])
    glb = os.path.join(OUT_DIR, "Aphid.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "Aphid.png"), subject_center=(0, 0.05, 0.4), distance=2.1)
    print(f"Exported Aphid.glb ({tris} tris)")


def build_bombardier_beetle():
    reset_scene()
    m_carapace = make_material("BeetleCarapace", (0.10, 0.14, 0.28), roughness=0.30, metallic=0.16)
    m_pronotum = make_material("BeetlePronotum", (0.78, 0.36, 0.09), roughness=0.44)
    m_gold = make_material("BeetleTrim", (0.92, 0.62, 0.16), roughness=0.35, metallic=0.15)
    m_leg = make_material("BeetleDark", (0.14, 0.10, 0.08), roughness=0.48)
    m_eye = make_material("BeetleEye", (0.95, 0.45, 0.08), roughness=0.15, emission=(0.8, 0.3, 0.05), emission_strength=0.4)
    m_nozzle = make_material("BeetleNozzle", (1.0, 0.42, 0.05), roughness=0.25, emission=(1.0, 0.38, 0.04), emission_strength=1.5)

    body_parts = []
    for sx in (-1, 1):
        shell = add_ellipsoid_obj(f"Elytra_{sx}", (sx * 0.19, 0.26, 0.60), (0.42, 0.80, 0.44), m_carapace, segs=14, rings=10, taper_y=-0.12, smooth=False)
        body_parts.append(shell)
    # Prominent continuous dorsal golden-orange ridge above the split elytra seam!
    body_parts.append(
        add_tapered_limb(
            "Seam",
            [(0, -0.32, 0.90), (0, 0.0, 1.04), (0, 0.35, 1.04), (0, 0.70, 0.92), (0, 0.96, 0.72)],
            [0.052, 0.058, 0.058, 0.052, 0.038],
            m_gold,
            sides=6,
            smooth=False,
        )
    )
    # Pronotum collar + armored edge
    body_parts.append(add_ellipsoid_obj("Pronotum", (0, -0.52, 0.56), (0.40, 0.32, 0.30), m_pronotum, segs=12, rings=8, smooth=False))
    body_parts.append(add_ellipsoid_obj("Collar", (0, -0.34, 0.58), (0.42, 0.09, 0.31), m_gold, segs=12, rings=6, smooth=False))
    body_parts.append(add_ellipsoid_obj("Head", (0, -0.88, 0.50), (0.28, 0.26, 0.22), m_pronotum, segs=12, rings=8, smooth=False))

    for sx in (-1, 1):
        body_parts.append(add_ellipsoid_obj(f"Eye_{sx}", (sx * 0.23, -0.96, 0.55), (0.06, 0.07, 0.06), m_eye, segs=10, rings=8))
        body_parts.append(
            add_tapered_limb(
                f"Mandible_{sx}",
                [(sx * 0.14, -1.08, 0.44), (sx * 0.20, -1.24, 0.42), (sx * 0.05, -1.32, 0.44)],
                [0.05, 0.036, 0.012],
                m_leg,
                sides=6,
                smooth=False,
            )
        )
        body_parts.append(
            add_tapered_limb(
                f"Antenna_{sx}",
                [(sx * 0.11, -1.02, 0.60), (sx * 0.34, -1.24, 0.78), (sx * 0.48, -1.38, 0.66)],
                [0.025, 0.018, 0.01],
                m_leg,
                sides=6,
                smooth=False,
            )
        )

    for idx, (hy, ky, ay, fy) in enumerate([(-0.55, -0.68, -0.80, -0.88), (-0.05, 0.0, 0.06, 0.14), (0.45, 0.65, 0.85, 1.0)]):
        for sx in (-1, 1):
            body_parts.append(
                build_jointed_leg(
                    f"Leg_{idx}_{sx}",
                    hip=(sx * 0.34, hy, 0.42),
                    knee=(sx * 0.66, ky, 0.54),
                    ankle=(sx * 0.82, ay, 0.24),
                    foot=(sx * 0.90, fy, 0.02),
                    radii=[0.06, 0.045, 0.032, 0.02],
                    mat=m_leg,
                    claw_mat=m_gold,
                )
            )

    body = join_children_by_material(body_parts, "Body")
    nozzle = add_tapered_limb(
        "Nozzle",
        [(0, 0.92, 0.54), (0, 1.12, 0.60), (0, 1.30, 0.68)],
        [0.19, 0.15, 0.11],
        m_nozzle,
        sides=10,
        smooth=False,
    )
    ground_hierarchy_to_z0([body, nozzle])
    glb = os.path.join(OUT_DIR, "BombardierBeetle.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "BombardierBeetle.png"), subject_center=(0, 0, 0.55), distance=3.4)
    print(f"Exported BombardierBeetle.glb ({tris} tris)")


def build_termite():
    reset_scene()
    m_abdomen = make_material("TermiteAbdomen", (0.88, 0.80, 0.66), roughness=0.55)
    m_band = make_material("TermiteBand", (0.72, 0.58, 0.42), roughness=0.52)
    m_head = make_material("TermiteHead", (0.64, 0.30, 0.11), roughness=0.44)
    m_jaw = make_material("TermiteJaw", (0.22, 0.10, 0.06), roughness=0.38)
    m_leg = make_material("TermiteLeg", (0.76, 0.66, 0.50), roughness=0.55)
    m_eye = make_material("TermiteEye", (0.14, 0.06, 0.04), roughness=0.2)

    parts = []
    # Cohesive tapered abdomen with subtle dorsal segment bands (less bead-chain)
    parts.append(add_ellipsoid_obj("Abdomen", (0, 0.44, 0.36), (0.26, 0.42, 0.22), m_abdomen, segs=12, rings=10, taper_y=-0.22, smooth=False))
    for i, (by, br) in enumerate([(0.24, 0.23), (0.40, 0.27), (0.56, 0.25), (0.70, 0.19)]):
        parts.append(add_ellipsoid_obj(f"Band_{i}", (0, by, 0.37), (br, 0.065, br * 0.86), m_band, segs=12, rings=6, smooth=False))

    parts.append(add_ellipsoid_obj("Thorax", (0, -0.05, 0.36), (0.19, 0.18, 0.17), m_abdomen, segs=10, rings=6, smooth=False))
    parts.append(add_ellipsoid_obj("Head", (0, -0.40, 0.40), (0.25, 0.28, 0.21), m_head, segs=12, rings=8, taper_y=-0.1, smooth=False))

    for sx in (-1, 1):
        parts.append(add_ellipsoid_obj(f"Eye_{sx}", (sx * 0.21, -0.50, 0.44), (0.036, 0.036, 0.036), m_eye, segs=8, rings=6))
        parts.append(
            add_tapered_limb(
                f"Jaw_{sx}",
                [(sx * 0.10, -0.62, 0.36), (sx * 0.16, -0.78, 0.35), (sx * 0.02, -0.86, 0.37)],
                [0.044, 0.032, 0.012],
                m_jaw,
                sides=6,
                smooth=False,
            )
        )
        parts.append(
            add_tapered_limb(
                f"Antenna_{sx}",
                [(sx * 0.10, -0.56, 0.48), (sx * 0.22, -0.72, 0.59), (sx * 0.28, -0.82, 0.50)],
                [0.018, 0.012, 0.008],
                m_leg,
                sides=5,
                smooth=False,
            )
        )

    for idx, (hy, ky, fy) in enumerate([(-0.15, -0.22, -0.32), (0.05, 0.08, 0.12), (0.26, 0.36, 0.48)]):
        for sx in (-1, 1):
            parts.append(
                build_jointed_leg(
                    f"Leg_{idx}_{sx}",
                    hip=(sx * 0.15, hy, 0.28),
                    knee=(sx * 0.34, ky, 0.35),
                    ankle=(sx * 0.42, fy, 0.14),
                    foot=(sx * 0.46, fy + 0.04, 0.02),
                    radii=[0.032, 0.024, 0.018, 0.012],
                    mat=m_leg,
                )
            )

    root = join_children_by_material(parts, "Body")
    ground_hierarchy_to_z0([root])
    glb = os.path.join(OUT_DIR, "Termite.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "Termite.png"), subject_center=(0, 0, 0.36), distance=2.2)
    print(f"Exported Termite.glb ({tris} tris)")


def build_mantis_boss():
    reset_scene()
    m_emerald = make_material("MantisEmerald", (0.16, 0.58, 0.20), roughness=0.44)
    m_lime = make_material("MantisLime", (0.34, 0.76, 0.26), roughness=0.46)
    m_dark = make_material("MantisDark", (0.08, 0.24, 0.09), roughness=0.48)
    m_eye = make_material("MantisEye", (0.96, 0.68, 0.12), roughness=0.14, emission=(0.75, 0.45, 0.05), emission_strength=0.45)
    m_wing = make_material("MantisWing", (0.48, 0.84, 0.42), roughness=0.32)
    m_blade = make_material("MantisClawBlade", (0.82, 0.90, 0.36), roughness=0.35, metallic=0.12)

    body_parts = []
    # Abdomen + dorsal plates
    body_parts.append(add_ellipsoid_obj("Abdomen", (0, 0.85, 1.32), (0.52, 1.05, 0.45), m_emerald, segs=14, rings=10, taper_y=-0.28, smooth=False))
    for r_y, r_s in [(0.45, 0.48), (0.85, 0.54), (1.25, 0.46)]:
        body_parts.append(add_ellipsoid_obj(f"AbBand_{r_y}", (0, r_y, 1.32), (r_s, 0.09, r_s * 0.84), m_dark, segs=12, rings=6, smooth=False))

    # Swept dorsal wings hugged tight over abdomen (ZERO overlap with rear legs!)
    for sx in (-1, 1):
        w = add_ellipsoid_obj(f"Wing_{sx}", (sx * 0.18, 0.98, 1.76), (0.18, 1.10, 0.10), m_wing, segs=10, rings=8, taper_y=-0.25, smooth=False)
        w.rotation_euler = Euler((math.radians(-16), math.radians(sx * 14), math.radians(sx * 5)))
        body_parts.append(w)

    # Tall upright prothorax + shoulder collar
    body_parts.append(
        add_tapered_limb(
            "Prothorax",
            [(0, 0.15, 1.38), (0, -0.24, 2.15), (0, -0.56, 2.95), (0, -0.76, 3.52)],
            [0.30, 0.21, 0.19, 0.14],
            m_emerald,
            sides=8,
            smooth=False,
        )
    )
    body_parts.append(add_ellipsoid_obj("Shoulders", (0, -0.55, 2.86), (0.34, 0.22, 0.20), m_dark, segs=10, rings=6, smooth=False))

    # Triangular sculpted praying mantis head (inverted tapered crown -> pointed chin!)
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=2, radius=0.38)
    for v in bm.verts:
        z_norm = v.co.z / 0.38  # +1 top, -1 bottom
        width_scale = 1.25 if z_norm > 0 else max(0.32, 1.0 + z_norm * 0.7)
        v.co = Vector((v.co.x * width_scale * 0.95, v.co.y * 0.62 - 0.86, v.co.z * 0.92 + 3.74))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    body_parts.append(create_mesh_object("Head", bm, material=m_lime, smooth=False))

    for sx in (-1, 1):
        body_parts.append(add_ellipsoid_obj(f"Eye_{sx}", (sx * 0.38, -0.92, 3.88), (0.14, 0.14, 0.15), m_eye, segs=10, rings=8))
        body_parts.append(
            add_tapered_limb(
                f"Antenna_{sx}",
                [(sx * 0.12, -0.96, 4.02), (sx * 0.34, -1.24, 4.48), (sx * 0.56, -0.94, 4.74)],
                [0.03, 0.02, 0.01],
                m_dark,
                sides=6,
                smooth=False,
            )
        )

    # 4 tall walking legs planted cleanly OUTSIDE wings
    for idx, (hy, ky, ay, fy, hx) in enumerate([
        (-0.08, -0.45, -0.62, -0.72, 0.34),
        (0.38, 0.72, 1.02, 1.26, 0.38),
    ]):
        for sx in (-1, 1):
            body_parts.append(
                build_jointed_leg(
                    f"Leg_{idx}_{sx}",
                    hip=(sx * hx, hy, 1.24),
                    knee=(sx * 1.24, ky, 1.64),
                    ankle=(sx * 1.50, ay, 0.72),
                    foot=(sx * 1.66, fy, 0.04),
                    radii=[0.11, 0.085, 0.055, 0.035],
                    mat=m_dark,
                    claw_mat=m_lime,
                )
            )

    body = join_children_by_material(body_parts, "Body")

    # Separate ClawL and ClawR with clean faceted scythe blades
    claws = []
    for side, sx in [("ClawL", -1), ("ClawR", 1)]:
        pivot = Vector((sx * 0.28, -0.56, 2.86))
        arm_parts = []
        p1 = pivot + Vector((sx * 0.42, -0.56, -0.34))
        p2 = pivot + Vector((sx * 0.36, -1.08, 0.32))
        p3 = pivot + Vector((sx * 0.24, -1.48, -0.52))
        arm_parts.append(add_tapered_limb(f"{side}_Upper", [pivot, p1], [0.13, 0.11], m_emerald, sides=8, smooth=False))
        arm_parts.append(add_tapered_limb(f"{side}_Forearm", [p1, p2], [0.12, 0.14], m_lime, sides=8, smooth=False))
        arm_parts.append(add_tapered_limb(f"{side}_Scythe", [p2, (p2 + p3) * 0.5 + Vector((0, -0.08, -0.14)), p3], [0.10, 0.07, 0.022], m_blade, sides=6, smooth=False))
        for t in (0.32, 0.60):
            sp = p2.lerp(p3, t)
            arm_parts.append(add_tapered_limb(f"{side}_Spk_{t}", [sp, sp + Vector((0, 0.10, -0.16))], [0.035, 0.01], m_blade, sides=5, smooth=False))

        claw = join_children_by_material(arm_parts, side)
        for v in claw.data.vertices:
            v.co -= pivot
        claw.location = pivot
        claws.append(claw)

    ground_hierarchy_to_z0([body] + claws)
    glb = os.path.join(OUT_DIR, "MantisBoss.glb")
    tris = validate_and_export_glb(glb)
    render_turntable_preview(os.path.join(PREVIEW_DIR, "MantisBoss.png"), subject_center=(0, -0.1, 2.1), distance=6.8)
    print(f"Exported MantisBoss.glb ({tris} tris)")


if __name__ == "__main__":
    build_ant_style("WorkerAnt", scale=1.0, chitin_color=(0.12, 0.07, 0.06), shell_color=(0.28, 0.11, 0.07), accent_color=(0.42, 0.16, 0.09))
    build_ant_style("SoldierAnt", scale=1.42, chitin_color=(0.14, 0.05, 0.05), shell_color=(0.50, 0.12, 0.08), accent_color=(0.68, 0.22, 0.10), mandible_color=(0.78, 0.52, 0.24), armored=True)
    build_ant_style("AllyAnt", scale=1.0, chitin_color=(0.08, 0.16, 0.28), shell_color=(0.18, 0.58, 0.80), accent_color=(0.30, 0.72, 0.90), mandible_color=(0.92, 0.76, 0.24), horned=True)
    build_aphid()
    build_bombardier_beetle()
    build_termite()
    build_mantis_boss()

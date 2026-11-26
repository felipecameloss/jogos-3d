"""
Gafanhoto (salta de longe + coice): verde vivo com barriga amarelada, cabeça alta com
olhos grandes, asas dobradas nas costas e as patas traseiras enormes (coxa grossa com
listras escuras e canela vermelha), que são o que dá leitura ao salto e ao coice. ~1 m.

Uso:
    blender --background --python Tools/Blender/build_grasshopper.py

Gera Assets/Art/Models/Characters/Grasshopper.fbx e Tools/Blender/previews/Grasshopper.png.

Peças (pivô na articulação; ver ant_common.py):
    Grasshopper (raiz)
      Body                 tórax / "sela" (pivô no centro)
        Head               pivô no pescoço
          Eye_L / Eye_R, Antenna_L / Antenna_R, Mandible_L / Mandible_R
        Abdomen            pivô na cintura
        Wing_L / Wing_R    asas dobradas sobre o abdômen
        Leg_L1..L3 / Leg_R1..R3   (3 = patas traseiras de salto)
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402


def build_grasshopper(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.2, 0.62, 0.06), 0.5)      # verde vivo (dominante)
    m_belly = material(f"{name}_Belly", (0.62, 0.86, 0.16), 0.5)   # barriga/faixas amareladas
    m_dark = material(f"{name}_Dark", (0.05, 0.2, 0.02), 0.55)     # listras da coxa, asas
    m_wing = material(f"{name}_Wing", (0.12, 0.38, 0.05), 0.5)
    m_shin = material(f"{name}_Shin", (0.85, 0.12, 0.03), 0.45)    # canela vermelha
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)
    m_mouth = material(f"{name}_Mouth", (0.3, 0.12, 0.03), 0.5)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- tórax: "sela" alongada
    th_c = Vector((0, 0.06, 0.34))
    tr = (0.11, 0.16, 0.12)
    body = part("Body", root, th_c, lambda bm, o: ellipsoid(bm, th_c - o, tr, 9, 6), m_body)
    paint(body, m_belly, lambda c: c.z < -tr[2] * 0.35)

    # ---- cabeça alta e inclinada (cara de gafanhoto)
    neck = Vector((0, 0.2, 0.38))
    head_c = Vector((0, 0.3, 0.4))
    hr = (0.105, 0.1, 0.15)
    face_tilt = Matrix.Rotation(math.radians(-20), 4, "X")
    head = part("Head", body, neck, lambda bm, o: ellipsoid(bm, head_c - o, hr, 10, 7, face_tilt), m_body)
    paint(head, m_belly, lambda c: (c + neck - head_c).z < -hr[2] * 0.45)

    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.085, 0.03, 0.06))
        er = (0.055, 0.06, 0.07)
        eye = part(f"Eye_{side}", head, eye_c, lambda bm, o, e=eye_c: ellipsoid(bm, e - o, er, 9, 6), m_eye, smooth=True)
        pup_c = Vector((sx * er[0] * 0.45, er[1] * 0.62, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, (0.032, 0.03, 0.045), 7, 5), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(bm, pup_c + Vector((sx * -0.01, 0.025, 0.022)), (0.012, 0.01, 0.015), 5, 3),
               smooth=True)

        # antenas médias, para frente e para cima
        base = head_c + Vector((sx * 0.035, 0.07, 0.11))
        mid = base + Vector((sx * 0.07, 0.14, 0.16))
        tip = mid + Vector((sx * 0.1, 0.12, 0.02))
        part(f"Antenna_{side}", head, base,
             lambda bm, o, b=base, m=mid, t=tip: tube(bm, [b - o, m - o, t - o], [0.016, 0.012, 0.008], 4), m_dark)

        mouth = head_c + Vector((sx * 0.035, 0.06, -0.13))
        part(f"Mandible_{side}", head, mouth,
             lambda bm, o, m=mouth, sx=sx: tube(bm, [m - o, m + Vector((sx * -0.012, 0.03, -0.03)) - o], [0.024, 0.012], 4),
             m_mouth)

    # ---- abdômen comprido, afinando para trás, com faixas claras embaixo
    waist = Vector((0, -0.07, 0.32))
    ab_c = Vector((0, -0.33, 0.33))
    abr = (0.095, 0.29, 0.1)
    ab_tilt = Matrix.Rotation(math.radians(6), 4, "X")
    abdomen = part("Abdomen", body, waist,
                   lambda bm, o: ellipsoid(bm, ab_c - o, abr, 10, 9, ab_tilt, poles_y=True), m_body)

    def is_ring(c):
        c = c + waist - ab_c
        y = c.y / abr[1]
        return c.z < 0.02 and any(abs(y - r) < 0.07 for r in (-0.65, -0.35, -0.05, 0.25))

    paint(abdomen, m_belly, is_ring)

    # ---- asas dobradas por cima do abdômen
    for side, sx in (("L", -1), ("R", 1)):
        wing_root = Vector((sx * 0.04, -0.02, 0.43))
        wing_c = Vector((sx * 0.055, -0.33, 0.42))
        rot = Matrix.Rotation(math.radians(sx * -8), 4, "Y") @ Matrix.Rotation(math.radians(4), 4, "X")
        part(f"Wing_{side}", body, wing_root,
             lambda bm, o, c=wing_c, r=rot: ellipsoid(bm, c - o, (0.055, 0.33, 0.025), 8, 4, r), m_wing)

    # ---- pernas da frente e do meio: finas, para frente / para o lado
    for idx, hip_y, fy in ((1, 0.14, 0.32), (2, 0.04, 0.05)):
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.08, hip_y, 0.27))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.17, fy * 0.35, 0.13))
                foot = Vector((sx * 0.36, hip.y + fy * 1.1, 0.0))
                tube(bm, [hip - o, knee - o, foot - o], [0.026, 0.022, 0.014], 5)
                ellipsoid(bm, knee - o, (0.028,) * 3, 5, 4)

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_body)

    # ---- patas traseiras: coxa grossa subindo para trás, canela vermelha descendo
    for side, sx in (("L", -1), ("R", 1)):
        hip = Vector((sx * 0.09, -0.04, 0.27))
        knee = Vector((sx * 0.2, -0.5, 0.62))
        foot = Vector((sx * 0.22, -0.16, 0.0))

        ts = (0.0, 0.12, 0.25, 0.37, 0.5, 0.62, 0.75, 0.87, 1.0)

        def build_hind(bm, o, hip=hip, knee=knee):
            # coxa em vários anéis (para as listras): engrossa no meio e afina no joelho
            pts = [hip.lerp(knee, t) + Vector((sx * 0.025, 0, 0.035)) * math.sin(t * math.pi) - o for t in ts]
            radii = [0.045 + 0.04 * math.sin(min(1.0, t * 1.4) * math.pi * 0.85) - 0.012 * t for t in ts]
            tube(bm, pts, radii, 7)
            ellipsoid(bm, knee - o, (0.04,) * 3, 6, 4)

        leg = part(f"Leg_{side}3", body, hip, build_hind, m_body)
        # listras escuras em anéis na coxa
        h, k = hip - hip, knee - hip

        def is_stripe(c, h=h, k=k):
            t = (c - h).dot((k - h).normalized()) / (k - h).length
            return any(a < t < b for a, b in ((0.27, 0.35), (0.52, 0.6), (0.77, 0.85)))

        paint(leg, m_dark, is_stripe)
        add_to(leg, m_shin, lambda bm, o=hip, k=knee, f=foot: tube_verts(bm, [k - o, k.lerp(f, 0.5) - o, f - o],
                                                                          [0.026, 0.022, 0.016], 5))
    return root


def tube_verts(bm, points, radii, sides):
    """tube() devolvendo os vértices novos (para add_to pintar com outro material)."""
    before = set(bm.verts)
    tube(bm, points, radii, sides)
    return [v for v in bm.verts if v not in before]


ac.run({
    "name": "Grasshopper",
    "body_length": 1.0,
    "cam_pos": (1.9, 1.9, 1.25),
    "cam_target": (0, -0.12, 0.32),
}, build_grasshopper)

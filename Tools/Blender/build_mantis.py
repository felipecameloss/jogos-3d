"""
Louva-a-deus (chefe): verde-limão, alto, cabeça triangular com olhões nas pontas e
sobrancelhas bravas, pescoço (protórax) longo erguido, garras raptoriais dobradas
com espinhos creme, asas verde-escuro sobre o abdômen comprido. ~2,8 m.

Uso:
    blender --background --python Tools/Blender/build_mantis.py

Gera Assets/Art/Models/Characters/MantisBoss.fbx e Tools/Blender/previews/MantisBoss.png.

Peças (pivô na articulação; ver ant_common.py):
    MantisBoss (raiz)
      Body                 tórax + pescoço erguido (pivô no centro do tórax)
        Head               pivô no alto do pescoço
          Eye_L / Eye_R, Antenna_L / Antenna_R, Mandible_L / Mandible_R
        Claw_L / Claw_R    garras (pivô no ombro) -> MantisBoss.clawLeft/clawRight
        Abdomen            pivô na cintura
          Wings            asas fechadas (pivô na base; dá para abrir no pulo)
        Leg_L2, Leg_L3, Leg_R2, Leg_R3   (as "pernas 1" são as garras)
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402

S = 3.0  # escala do preview em relação à operária


def build_mantis(cfg):
    ac.reset_scene()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.55, 0.92, 0.04), 0.45)      # verde-limão (dominante)
    m_dark = material(f"{name}_Dark", (0.12, 0.36, 0.04), 0.5)       # asas, sobrancelhas
    m_light = material(f"{name}_Light", (0.85, 1.0, 0.35), 0.45)     # listras da barriga
    m_spike = material(f"{name}_Spike", (1.0, 0.93, 0.7), 0.4)       # espinhos das garras
    m_leg = material(f"{name}_Leg", (0.36, 0.7, 0.03), 0.5)          # pernas/antenas um tom abaixo
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- tórax + pescoço longo erguido para a frente
    thorax_c = Vector((0, 0.05, 0.42))
    neck_top = Vector((0, 0.3, 0.8))

    def build_body(bm, o):
        ellipsoid(bm, thorax_c - o, (0.11, 0.15, 0.1), 9, 6)
        tube(bm, [Vector((0, 0.1, 0.45)) - o, Vector((0, 0.2, 0.62)) - o, neck_top - o], [0.075, 0.065, 0.06], 6)

    body = part("Body", root, thorax_c, build_body, m_body)

    # ---- cabeça triangular (larga em cima, afina para a boca)
    head_c = Vector((0, 0.36, 0.86))

    def build_head(bm, o):
        ellipsoid(bm, head_c - o, (0.19, 0.1, 0.1), 10, 6)
        ellipsoid(bm, head_c + Vector((0, 0.03, -0.09)) - o, (0.075, 0.07, 0.08), 7, 5)

    head = part("Head", body, neck_top, build_head, m_body)

    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.17, 0.03, 0.04))
        er = (0.085, 0.075, 0.09)
        eye = part(f"Eye_{side}", head, eye_c,
                   lambda bm, o, eye_c=eye_c: ellipsoid(bm, eye_c - o, er, 9, 6), m_eye, smooth=True)
        pup_c = Vector((sx * er[0] * 0.1, er[1] * 0.72, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, (0.05, 0.035, 0.06), 7, 5), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(
            bm, pup_c + Vector((sx * -0.013, 0.032, 0.03)), (0.017, 0.012, 0.02), 5, 3), smooth=True)
        # sobrancelha brava (mais baixa no meio da cara)
        brow_c = eye_c + Vector((sx * -0.025, er[1] * 0.45, er[2] * 0.72)) - neck_top
        tilt = Matrix.Rotation(math.radians(sx * 24), 4, "Y") @ Matrix.Rotation(math.radians(-15), 4, "X")
        add_to(head, m_dark, lambda bm, c=brow_c, t=tilt: ellipsoid(
            bm, c, (er[0] * 0.95, er[1] * 0.5, er[2] * 0.26), 6, 4, t))

    # antenas longas, finas
    for side, sx in (("L", -1), ("R", 1)):
        base = head_c + Vector((sx * 0.05, 0.06, 0.08))
        pts = [base, base + Vector((sx * 0.08, 0.12, 0.18)), base + Vector((sx * 0.2, 0.32, 0.3))]
        part(f"Antenna_{side}", head, base,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.016, 0.012, 0.008], 4), m_leg)

    # mandíbulas pequenas
    for side, sx in (("L", -1), ("R", 1)):
        mouth = head_c + Vector((sx * 0.03, 0.09, -0.15))
        pts = [mouth, mouth + Vector((sx * 0.015, 0.035, -0.02)), mouth + Vector((sx * -0.02, 0.05, -0.03))]
        part(f"Mandible_{side}", head, mouth,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.02, 0.016, 0.006], 4), m_dark)

    # ---- garras raptoriais dobradas na frente do peito
    for side, sx in (("L", -1), ("R", 1)):
        shoulder = Vector((sx * 0.06, 0.25, 0.68))
        coxa_end = Vector((sx * 0.13, 0.36, 0.52))     # desce para a frente
        femur_end = Vector((sx * 0.13, 0.5, 0.8))      # sobe (a "lâmina")
        tibia_end = Vector((sx * 0.11, 0.42, 0.58))    # dobra para baixo, fechando
        hook = Vector((sx * 0.09, 0.47, 0.54))

        def build_claw(bm, o, sh=shoulder, c=coxa_end, f=femur_end, t=tibia_end, h=hook):
            tube(bm, [sh - o, c - o, f - o, t - o, h - o], [0.05, 0.048, 0.052, 0.03, 0.012], 6)

        claw = part(f"Claw_{side}", body, shoulder, build_claw, m_body)

        def spikes(bm, sx=sx, c=coxa_end, f=femur_end, o=shoulder):
            before = set(bm.verts)
            for k in (0.25, 0.5, 0.75):
                p = c.lerp(f, k)
                inward = Vector((-sx * 0.035, 0.05, -0.04))
                tube(bm, [p - o, p + inward - o], [0.022, 0.004], 4)
            return [v for v in bm.verts if v not in before]

        add_to(claw, m_spike, spikes)

    # ---- abdômen comprido, levemente erguido atrás, com listras e asas fechadas
    waist = Vector((0, -0.07, 0.42))
    abd_c = Vector((0, -0.48, 0.46))
    ar = (0.15, 0.45, 0.13)
    abdomen = part("Abdomen", body, waist,
                   lambda bm, o: ellipsoid(bm, abd_c - o, ar, 10, 8, Matrix.Rotation(math.radians(8), 4, "X"),
                                           poles_y=True),
                   m_body)
    paint(abdomen, m_light, lambda c: (c + waist - abd_c).z < -ar[2] * 0.5)  # barriga clara

    wing_base = Vector((0, -0.08, 0.53))
    part("Wings", abdomen, wing_base,
         lambda bm, o: ellipsoid(bm, Vector((0, -0.46, 0.55)) - o, (0.12, 0.46, 0.04), 7, 4,
                                 Matrix.Rotation(math.radians(8), 4, "X"), poles_y=True),
         m_dark)

    # ---- 4 pernas de andar, longas e finas
    for idx, hip_y, fy in [(2, 0.07, 0.25), (3, -0.03, -0.38)]:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.08, hip_y, 0.37))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.24, fy * 0.45, 0.16))
                ankle = Vector((sx * 0.5, hip.y + fy, 0.06))
                foot = ankle + Vector((sx * 0.04, fy * 0.1, -0.06))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.034, 0.028, 0.02, 0.016], 5)
                ellipsoid(bm, knee - o, (0.034,) * 3, 5, 4)

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_leg)


ac.run({
    "name": "MantisBoss",
    "body_length": 2.8,
    "cam_pos": (1.6 * S, 2.15 * S, 1.25 * S),
    "cam_target": (0, -0.05 * S, 0.42 * S),
}, build_mantis)

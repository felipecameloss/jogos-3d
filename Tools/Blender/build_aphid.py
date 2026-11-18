"""
Pulgão: o "porquinho" do quintal. Inofensivo, foge e solta muitas migalhas.
Verde-claro, corpo de gota/pera bem redondo, cabeça pequena com olhos enormes,
antenas longas penteadas para trás, dois "canudinhos" (sifões) nas costas,
perninhas finas e curtas. ~0,7 m.

Uso:
    blender --background --python Tools/Blender/build_aphid.py

Gera Assets/Art/Models/Characters/Aphid.fbx e Tools/Blender/previews/Aphid.png.

Peças (pivô na articulação; ver ant_common.py):
    Aphid (raiz)
      Body                 tórax (pivô no centro)
        Head               pivô no pescoço
          Eye_L / Eye_R, Antenna_L / Antenna_R, Proboscis (tromba, pivô na boca)
        Abdomen            barrigão de gota (pivô na junção; dá para "respirar"/balançar)
          Siphon_L / Siphon_R   canudinhos (pivô na base)
        Leg_L1..L3 / Leg_R1..R3
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402

S = 0.7  # escala em relação à operária (câmera do preview)


def build_aphid(cfg):
    ac.reset_scene()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.6, 1.0, 0.22), 0.45)        # verde-claro amarelado (dominante, destaca na grama)
    m_belly = material(f"{name}_Belly", (0.98, 1.0, 0.68), 0.45)      # barriga/listras creme
    m_dark = material(f"{name}_Dark", (0.1, 0.3, 0.05), 0.55)        # pernas, antenas, sifões
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- tórax pequeno
    thorax_c = Vector((0, 0.1, 0.24))
    body = part("Body", root, thorax_c,
                lambda bm, o: ellipsoid(bm, thorax_c - o, (0.14, 0.12, 0.13), 9, 6), m_body)

    # ---- cabeça pequena e baixa, olhos enormes
    neck = Vector((0, 0.18, 0.25))
    head_c = Vector((0, 0.29, 0.25))
    head = part("Head", body, neck,
                lambda bm, o: ellipsoid(bm, head_c - o, (0.13, 0.12, 0.12), 10, 6), m_body)
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.085, 0.07, 0.05))
        er = (0.07, 0.06, 0.075)
        eye = part(f"Eye_{side}", head, eye_c,
                   lambda bm, o, eye_c=eye_c: ellipsoid(bm, eye_c - o, er, 9, 6), m_eye, smooth=True)
        pup_c = Vector((sx * er[0] * 0.35, er[1] * 0.72, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, (0.045, 0.03, 0.055), 8, 6), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(
            bm, pup_c + Vector((sx * -0.011, 0.026, 0.026)), (0.014, 0.01, 0.017), 6, 4), smooth=True)

    # ---- antenas longas, penteadas para trás em arco
    for side, sx in (("L", -1), ("R", 1)):
        base = head_c + Vector((sx * 0.04, 0.07, 0.1))
        pts = [base, base + Vector((sx * 0.06, 0.04, 0.14)), base + Vector((sx * 0.13, -0.08, 0.23)),
               base + Vector((sx * 0.2, -0.26, 0.24))]
        part(f"Antenna_{side}", head, base,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.016, 0.013, 0.01, 0.007], 4), m_dark)

    # ---- tromba curtinha apontando para baixo
    mouth = head_c + Vector((0, 0.1, -0.06))
    part("Proboscis", head, mouth,
         lambda bm, o: tube(bm, [mouth - o, mouth + Vector((0, 0.03, -0.07)) - o], [0.022, 0.012], 5), m_dark)

    # ---- barrigão de gota (maior parte do corpo), listras claras, sifões nas costas
    waist = Vector((0, 0.02, 0.25))
    belly_c = Vector((0, -0.2, 0.3))
    br = (0.25, 0.27, 0.24)

    def build_abdomen(bm, o):
        ellipsoid(bm, belly_c - o, br, 12, 8, Matrix.Rotation(math.radians(-10), 4, "X"), poles_y=True)

    abdomen = part("Abdomen", body, waist, build_abdomen, m_body)
    # listras claras atravessando as costas + barriga clara
    paint(abdomen, m_belly, lambda c: (
        (c + waist - belly_c).z < -br[2] * 0.55
        or 0.1 < (c + waist - belly_c).y / br[1] < 0.3       # fileiras de faces da elipsoide
        or -0.65 < (c + waist - belly_c).y / br[1] < -0.42))

    for side, sx in (("L", -1), ("R", 1)):
        sbase = belly_c + Vector((sx * 0.1, -0.13, 0.16))
        stip = sbase + Vector((sx * 0.025, -0.05, 0.06))
        part(f"Siphon_{side}", abdomen, sbase,
             lambda bm, o, a=sbase, b=stip: tube(bm, [a - o, b - o], [0.036, 0.03], 5), m_dark)

    # ---- 6 perninhas finas e curtas
    for idx, hip_y, fy in [(1, 0.16, 0.16), (2, 0.08, 0.0), (3, 0.0, -0.16)]:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.1, hip_y, 0.17))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.12, fy * 0.4, 0.05))
                ankle = Vector((sx * 0.26, hip.y + fy * 0.9, 0.04))
                foot = ankle + Vector((sx * 0.03, fy * 0.08, -0.04))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.026, 0.022, 0.017, 0.015], 5)
                ellipsoid(bm, knee - o, (0.028,) * 3, 5, 4)

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)


ac.run({
    "name": "Aphid",
    "body_length": 0.7,
    "cam_pos": (1.6 * S, 2.15 * S, 1.2 * S),
    "cam_target": (0, -0.05 * S, 0.3 * S),
}, build_aphid)

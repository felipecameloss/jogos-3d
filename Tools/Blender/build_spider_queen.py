"""
Aranha-rainha (chefe, issue #32): a aranha comum maior e mais imponente. Roxo-real escuro
(a família da aranha é roxa), coroa dourada de três pontas na cabeça, abdômen enorme com
uma marca magenta em ampulheta, presas cor de osso e oito pernas com joelho bem alto.
Mesmas peças da aranha (o ProceduralInsectAnimator e o SpiderQueen usam Body/Abdomen/Spinner).

Uso:
    blender --background --python Tools/Blender/build_spider_queen.py

Gera Assets/Art/Models/Characters/SpiderQueen.fbx e Tools/Blender/previews/SpiderQueen.png.
No jogo o prefab é escalado (~2x a aranha comum); ver SpiderQueenBuilder.cs.

Peças (pivô na articulação; ver ant_common.py):
    SpiderQueen (raiz)
      Body                 cefalotórax (pivô no centro)
        Head               "rosto" com os olhos (pivô na frente do cefalotórax)
          Eye_L / Eye_R, EyeSmall_L / EyeSmall_R, Mandible_L / Mandible_R (presas)
          Crown            coroa (pivô na base)
        Abdomen            pivô na cintura
          Spinner          fiandeira (pivô na ponta = de onde sai a teia)
        Leg_L1..L4 / Leg_R1..R4
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402

S = 1.6


def eye_with_pupil(name, parent, center, radii, m_eye, m_pupil, sx, small=False):
    """small: olho simples (menos segmentos, sem brilho) para caber no limite de triângulos."""
    segs = (7, 5) if small else (9, 6)
    eye = part(name, parent, center, lambda bm, o: ellipsoid(bm, center - o, radii, *segs), m_eye, smooth=True)
    pup_c = Vector((sx * radii[0] * 0.2, radii[1] * 0.72, radii[2] * 0.05))
    pr = (radii[0] * 0.62, radii[1] * 0.5, radii[2] * 0.7)
    add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, pr, *((6, 4) if small else (7, 5))), smooth=True)
    if small:
        return eye
    add_to(eye, m_eye, lambda bm: ellipsoid(
        bm, pup_c + Vector((sx * -radii[0] * 0.15, radii[1] * 0.42, radii[2] * 0.33)),
        (radii[0] * 0.2, radii[1] * 0.16, radii[2] * 0.22), 5, 3), smooth=True)
    return eye


def build_queen(cfg):
    ac.reset_scene()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.28, 0.04, 0.5), 0.4)        # roxo-real (dominante)
    m_dark = material(f"{name}_Dark", (0.06, 0.01, 0.12), 0.5)       # pernas
    m_mark = material(f"{name}_Mark", (1.0, 0.25, 0.7), 0.4)         # ampulheta magenta, juntas
    m_gold = material(f"{name}_Gold", (1.0, 0.76, 0.14), 0.3, 0.5)   # coroa
    m_fang = material(f"{name}_Fang", (0.96, 0.9, 0.82), 0.4)
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- cefalotórax
    ct_c = Vector((0, 0.08, 0.3))
    body = part("Body", root, ct_c, lambda bm, o: ellipsoid(bm, ct_c - o, (0.2, 0.21, 0.14), 10, 6), m_body)

    # ---- "rosto": frente arredondada com os olhos
    face_pivot = Vector((0, 0.2, 0.32))
    face_c = Vector((0, 0.27, 0.34))
    head = part("Head", body, face_pivot,
                lambda bm, o: ellipsoid(bm, face_c - o, (0.16, 0.1, 0.13), 9, 5), m_body)
    for side, sx in (("L", -1), ("R", 1)):
        eye_with_pupil(f"Eye_{side}", head, face_c + Vector((sx * 0.078, 0.075, 0.025)),
                       (0.078, 0.062, 0.082), m_eye, m_pupil, sx)
        eye_with_pupil(f"EyeSmall_{side}", head, face_c + Vector((sx * 0.065, 0.035, 0.12)),
                       (0.036, 0.03, 0.038), m_eye, m_pupil, sx, small=True)

    # ---- coroa: aro dourado + três pontas (a do meio mais alta)
    crown_base = face_c + Vector((0, -0.03, 0.12))

    def build_crown(bm, o):
        ellipsoid(bm, crown_base - o, (0.13, 0.11, 0.045), 8, 3)
        for x, h in ((-0.085, 0.17), (0.0, 0.25), (0.085, 0.17)):
            b = crown_base + Vector((x, 0.0, 0.02))
            t = b + Vector((x * 0.35, 0.0, h))
            tube(bm, [b - o, t - o], [0.05, 0.008], 3)

    part("Crown", head, crown_base, build_crown, m_gold)

    # ---- presas (quelíceras) maiores que as da aranha comum
    for side, sx in (("L", -1), ("R", 1)):
        base = face_c + Vector((sx * 0.05, 0.065, -0.085))
        pts = [base, base + Vector((sx * 0.012, 0.05, -0.07)), base + Vector((sx * -0.03, 0.06, -0.14))]
        part(f"Mandible_{side}", head, base,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.042, 0.03, 0.007], 5), m_fang)

    # ---- abdômen enorme, com ampulheta magenta em cima
    waist = Vector((0, -0.12, 0.31))
    abd_c = Vector((0, -0.5, 0.46))
    ar = (0.36, 0.4, 0.33)
    abdomen = part("Abdomen", body, waist,
                   lambda bm, o: ellipsoid(bm, abd_c - o, ar, 11, 8, Matrix.Rotation(math.radians(-15), 4, "X")),
                   m_body)

    def is_mark(c):
        c = c + waist - abd_c
        if c.z < ar[2] * 0.25:
            return False
        # ampulheta: dois triângulos que se tocam no meio do abdômen
        y = (c.y + 0.02) / ar[1]
        return abs(c.x) / ar[0] < 0.08 + abs(y) * 0.55 and abs(y) < 0.62

    paint(abdomen, m_mark, is_mark)

    spin_tip = abd_c + Vector((0, -ar[1] - 0.03, -0.06))
    part("Spinner", abdomen, spin_tip,
         lambda bm, o: tube(bm, [spin_tip + Vector((0, 0.07, 0.02)) - o, spin_tip - o], [0.06, 0.042], 5), m_dark)

    # ---- 8 pernas longas: joelho bem acima do corpo, pés espalhados
    legs = [(1, 0.17, 0.5), (2, 0.11, 0.18), (3, 0.04, -0.12), (4, -0.02, -0.42)]
    for idx, hip_y, fy in legs:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.16, hip_y, 0.27))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.24, fy * 0.35, 0.34))
                ankle = Vector((sx * 0.6, hip.y + fy * 0.85, 0.12))
                foot = Vector((sx * 0.68, hip.y + fy, 0.0))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.045, 0.038, 0.029, 0.015], 5)
                ellipsoid(bm, knee - o, (0.046,) * 3, 5, 4)

            leg = part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)
            kn = hip + Vector((sx * 0.24, fy * 0.35, 0.34)) - hip
            paint(leg, m_mark, lambda c, k=kn: (c - k).length < 0.055)  # joelho magenta


ac.run({
    "name": "SpiderQueen",
    "body_length": 1.2,
    "cam_pos": (1.6 * S, 2.15 * S, 1.3 * S),
    "cam_target": (0, -0.08 * S, 0.3 * S),
}, build_queen)

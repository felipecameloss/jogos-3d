"""
Aranha (pulo + teia): roxa, abdômen redondo grande com marca clara no topo,
dois olhões + dois olhinhos em cima, presas cor de osso, 8 pernas com joelho bem
alto (pose de quem vai pular). ~1,2 m.

Uso:
    blender --background --python Tools/Blender/build_spider.py

Gera Assets/Art/Models/Characters/Spider.fbx e Tools/Blender/previews/Spider.png.

Peças (pivô na articulação; ver ant_common.py):
    Spider (raiz)
      Body                 cefalotórax (pivô no centro)
        Head               "rosto" com os olhos (pivô na frente do cefalotórax)
          Eye_L / Eye_R, EyeSmall_L / EyeSmall_R, Mandible_L / Mandible_R (presas)
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

S = 1.45


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


def build_spider(cfg):
    ac.reset_scene()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.42, 0.1, 0.72), 0.45)       # roxo (dominante)
    m_dark = material(f"{name}_Dark", (0.09, 0.02, 0.17), 0.5)       # pernas (bem mais escuras que o corpo)
    m_mark = material(f"{name}_Mark", (0.82, 0.55, 1.0), 0.45)       # marca no abdômen, juntas
    m_fang = material(f"{name}_Fang", (0.96, 0.9, 0.82), 0.4)
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- cefalotórax
    ct_c = Vector((0, 0.08, 0.3))
    body = part("Body", root, ct_c, lambda bm, o: ellipsoid(bm, ct_c - o, (0.19, 0.2, 0.13), 10, 6), m_body)

    # ---- "rosto": frente arredondada com os olhos
    face_pivot = Vector((0, 0.2, 0.32))
    face_c = Vector((0, 0.27, 0.33))
    head = part("Head", body, face_pivot,
                lambda bm, o: ellipsoid(bm, face_c - o, (0.15, 0.09, 0.12), 9, 5), m_body)
    for side, sx in (("L", -1), ("R", 1)):
        eye_with_pupil(f"Eye_{side}", head, face_c + Vector((sx * 0.075, 0.07, 0.03)),
                       (0.075, 0.06, 0.08), m_eye, m_pupil, sx)
        eye_with_pupil(f"EyeSmall_{side}", head, face_c + Vector((sx * 0.06, 0.03, 0.13)),
                       (0.038, 0.032, 0.04), m_eye, m_pupil, sx, small=True)

    # ---- presas (quelíceras): grossas na base, ponta curva para dentro
    for side, sx in (("L", -1), ("R", 1)):
        base = face_c + Vector((sx * 0.045, 0.06, -0.08))
        pts = [base, base + Vector((sx * 0.01, 0.04, -0.06)), base + Vector((sx * -0.025, 0.05, -0.12))]
        part(f"Mandible_{side}", head, base,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.035, 0.026, 0.006], 5), m_fang)

    # ---- abdômen grande e redondo, com losango claro em cima
    waist = Vector((0, -0.11, 0.31))
    abd_c = Vector((0, -0.42, 0.42))
    ar = (0.3, 0.33, 0.28)
    abdomen = part("Abdomen", body, waist,
                   lambda bm, o: ellipsoid(bm, abd_c - o, ar, 12, 8, Matrix.Rotation(math.radians(-15), 4, "X")),
                   m_body)

    def is_mark(c):
        c = c + waist - abd_c
        # mancha clara no alto do abdômen
        return c.z > ar[2] * 0.2 and abs(c.x) / ar[0] * 1.6 + abs(c.y + 0.02) / ar[1] * 0.7 < 0.62

    paint(abdomen, m_mark, is_mark)

    spin_tip = abd_c + Vector((0, -ar[1] - 0.03, -0.06))
    part("Spinner", abdomen, spin_tip,
         lambda bm, o: tube(bm, [spin_tip + Vector((0, 0.06, 0.02)) - o, spin_tip - o], [0.05, 0.035], 5), m_dark)

    # ---- 8 pernas: joelho bem acima do corpo (silhueta de aranha), pés espalhados
    legs = [(1, 0.17, 0.5), (2, 0.11, 0.18), (3, 0.04, -0.12), (4, -0.02, -0.42)]
    for idx, hip_y, fy in legs:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.15, hip_y, 0.27))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.22, fy * 0.35, 0.3))
                ankle = Vector((sx * 0.55, hip.y + fy * 0.85, 0.12))
                foot = Vector((sx * 0.62, hip.y + fy, 0.0))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.04, 0.034, 0.026, 0.014], 5)
                ellipsoid(bm, knee - o, (0.042,) * 3, 5, 4)

            leg = part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)
            kn = hip + Vector((sx * 0.22, fy * 0.35, 0.3)) - hip
            paint(leg, m_mark, lambda c, k=kn: (c - k).length < 0.05)  # joelho claro


ac.run({
    "name": "Spider",
    "body_length": 1.2,
    "cam_pos": (1.6 * S, 2.15 * S, 1.3 * S),
    "cam_target": (0, -0.08 * S, 0.3 * S),
}, build_spider)

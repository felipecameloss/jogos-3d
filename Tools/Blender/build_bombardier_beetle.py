"""
Besouro-bombardeiro: verde metálico, baixo e largo, carapaça (élitros) em domo com
costura clara no meio, cabeça pequena com olhos grandes, pernas curtas abertas e
um bocal laranja na ponta do abdômen (de onde sai a bola de ácido).

Uso:
    blender --background --python Tools/Blender/build_bombardier_beetle.py

Gera Assets/Art/Models/Characters/BombardierBeetle.fbx e
Tools/Blender/previews/BombardierBeetle.png.

Peças (pivô na articulação; ver ant_common.py e CLAUDE.md):
    BombardierBeetle (raiz, no chão)
      Body                 pronoto/tórax (pivô no centro)
        Head               pivô no pescoço
          Eye_L / Eye_R, Antenna_L / Antenna_R, Mandible_L / Mandible_R
        Abdomen            carapaça; pivô na junção com o tórax (abre/levanta)
          Nozzle           pivô NA PONTA = ponto de disparo (BombardierBeetle.nozzle)
        Leg_L1..L3 / Leg_R1..R3
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402

S = 1.15  # escala em relação à operária (só para a câmera do preview)


def build_beetle(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    name = cfg["name"]

    m_shell = material(f"{name}_Shell", (0.0, 0.34, 0.2), 0.25, 0.6)       # verde-esmeralda metálico (dominante)
    m_body = material(f"{name}_Body", (0.0, 0.19, 0.12), 0.3, 0.55)       # pronoto e cabeça, mais escuro
    m_seam = material(f"{name}_Seam", (0.5, 1.0, 0.55), 0.3, 0.1)         # costura/bordas claras
    m_dark = material(f"{name}_Dark", (0.01, 0.045, 0.035), 0.5)          # pernas, barriga, antenas
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)
    m_nozzle = material(f"{name}_Nozzle", (1.0, 0.55, 0.08), 0.4)         # laranja: lê como "perigo"

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- pronoto (tórax): escudo largo e achatado
    thorax_c = Vector((0, 0.12, 0.25))
    body = part("Body", root, thorax_c,
                lambda bm, o: ellipsoid(bm, thorax_c - o, (0.21, 0.13, 0.11), 10, 6,
                                        Matrix.Rotation(math.radians(-8), 4, "X")),
                m_body)
    # borda clara na frente do escudo
    paint(body, m_seam, lambda c: c.y > 0.095 and c.z > -0.02)

    # ---- cabeça pequena e redonda, olhos enormes
    neck = Vector((0, 0.22, 0.23))
    head_c = Vector((0, 0.33, 0.22))
    head = part("Head", body, neck,
                lambda bm, o: ellipsoid(bm, head_c - o, (0.15, 0.13, 0.12), 10, 6), m_body)
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.1, 0.08, 0.06))
        er = (0.075, 0.065, 0.08)
        eye = part(f"Eye_{side}", head, eye_c,
                   lambda bm, o, eye_c=eye_c: ellipsoid(bm, eye_c - o, er, 9, 6), m_eye, smooth=True)
        pup_c = Vector((sx * er[0] * 0.38, er[1] * 0.72, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, (0.045, 0.032, 0.055), 8, 6), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(
            bm, pup_c + Vector((sx * -0.011, 0.028, 0.027)), (0.015, 0.011, 0.018), 6, 4), smooth=True)

    # ---- antenas finas, para a frente e para fora, pontinha clara
    for side, sx in (("L", -1), ("R", 1)):
        # sai do alto da cabeça, por dentro dos olhos, e abre para cima/fora (não cruza a cara)
        base = head_c + Vector((sx * 0.04, 0.08, 0.1))
        mid = base + Vector((sx * 0.07, 0.05, 0.15))
        tip = mid + Vector((sx * 0.13, 0.12, 0.02))

        def build_antenna(bm, o, base=base, mid=mid, tip=tip):
            tube(bm, [base - o, mid - o, tip - o], [0.018, 0.015, 0.012], 5)
            ellipsoid(bm, tip - o, (0.03, 0.03, 0.03), 6, 4)

        ant = part(f"Antenna_{side}", head, base, build_antenna, m_dark)
        paint(ant, m_seam, lambda c, t=tip - base: (c - t).length < 0.04)

    # ---- mandíbulas pequenas
    for side, sx in (("L", -1), ("R", 1)):
        mouth = head_c + Vector((sx * 0.05, 0.11, -0.07))
        pts = [mouth, mouth + Vector((sx * 0.015, 0.05, -0.01)), mouth + Vector((sx * -0.03, 0.08, -0.015))]
        part(f"Mandible_{side}", head, mouth,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.025, 0.02, 0.008], 5), m_dark)

    # ---- carapaça (élitros) em domo, barriga escura, costura clara no meio
    hinge = Vector((0, 0.03, 0.27))
    dome_c = Vector((0, -0.22, 0.25))
    dr = (0.27, 0.34, 0.19)

    def build_abdomen(bm, o):
        ellipsoid(bm, dome_c - o, dr, 12, 8, Matrix.Rotation(math.radians(4), 4, "X"), poles_y=True)

    abdomen = part("Abdomen", body, hinge, build_abdomen, m_shell)
    # barriga e borda de baixo escuras (silhueta mais clara vista de cima)
    paint(abdomen, m_dark, lambda c: (c + hinge - dome_c).z < -dr[2] * 0.45)

    def seam(bm):
        pts = []
        for t in (0.9, 0.55, 0.15, -0.3, -0.7, -0.92):
            y = dr[1] * t
            z = dr[2] * math.sqrt(max(0.0, 1 - t * t)) * 1.01
            pts.append(dome_c + Vector((0, y, z)) - hinge)
        before = set(bm.verts)
        tube(bm, pts, [0.022, 0.028, 0.028, 0.028, 0.025, 0.016], 4)
        return [v for v in bm.verts if v not in before]

    add_to(abdomen, m_seam, seam)

    # ---- bocal na ponta de trás (pivô na boca do bocal = ponto de disparo)
    tip = dome_c + Vector((0, -dr[1] - 0.06, 0.05))
    base = tip + Vector((0, 0.09, -0.035))
    nozzle = part("Nozzle", abdomen, tip,
                  lambda bm, o: tube(bm, [base - o, (base + tip) / 2 - o, tip - o], [0.055, 0.05, 0.062], 6),
                  m_nozzle)

    # ---- 6 pernas curtas, bem abertas (base larga de besouro)
    for idx, hip_y, fy in [(1, 0.17, 0.2), (2, 0.04, 0.0), (3, -0.1, -0.22)]:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.15, hip_y, 0.17))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.16, fy * 0.4, 0.06))
                ankle = Vector((sx * 0.37, hip.y + fy * 0.9, 0.05))
                foot = ankle + Vector((sx * 0.05, fy * 0.1, -0.05))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.042, 0.036, 0.026, 0.022], 5)
                ellipsoid(bm, knee - o, (0.044,) * 3, 5, 4)

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)

    return root


ac.run({
    "name": "BombardierBeetle",
    "body_length": 1.15,
    "cam_pos": (1.6 * S, 2.15 * S, 1.2 * S),
    "cam_target": (0, -0.05 * S, 0.22 * S),
}, build_beetle)

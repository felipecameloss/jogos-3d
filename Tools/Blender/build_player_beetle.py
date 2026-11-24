"""
Besouro jogável (personagem "Besouro"): besouro-rinoceronte azul-royal metálico com um
chifre dourado enorme na cabeça, carapaça em domo com costura dourada, pernas grossas e
bem abertas (tanque). Cores diferentes do besouro-bombardeiro inimigo (verde).

Uso:
    blender --background --python Tools/Blender/build_player_beetle.py

Gera Assets/Art/Models/Characters/Resources/PlayerBeetle.fbx (o jogo carrega com
Resources.Load("PlayerBeetle"), ver PlayerCharacter.cs) e Tools/Blender/previews/PlayerBeetle.png.

Peças (pivô na articulação; ver ant_common.py e CLAUDE.md):
    PlayerBeetle (raiz, no chão)
      Body                 pronoto/tórax (pivô no centro)
        Head               pivô no pescoço
          Eye_L / Eye_R, Antenna_L / Antenna_R, Mandible_L / Mandible_R
          Horn             chifre; pivô na base
        Abdomen            carapaça; pivô na junção com o tórax
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

S = 1.2  # escala em relação à operária (só para a câmera do preview)


def build_beetle(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    name = cfg["name"]

    m_shell = material(f"{name}_Shell", (0.05, 0.2, 0.8), 0.25, 0.6)       # azul-royal metálico (dominante)
    m_body = material(f"{name}_Body", (0.03, 0.1, 0.42), 0.3, 0.55)       # pronoto e cabeça, mais escuro
    m_gold = material(f"{name}_Gold", (1.0, 0.72, 0.12), 0.3, 0.4)        # chifre, costura e bordas
    m_dark = material(f"{name}_Dark", (0.02, 0.03, 0.12), 0.5)            # pernas, barriga, antenas
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- pronoto (tórax): escudo largo e alto
    thorax_c = Vector((0, 0.12, 0.27))
    body = part("Body", root, thorax_c,
                lambda bm, o: ellipsoid(bm, thorax_c - o, (0.23, 0.14, 0.13), 10, 6,
                                        Matrix.Rotation(math.radians(-8), 4, "X")),
                m_body)
    paint(body, m_gold, lambda c: c.y > 0.105 and c.z > -0.02)  # borda dourada na frente

    # ---- cabeça redonda, olhos grandes
    neck = Vector((0, 0.23, 0.25))
    head_c = Vector((0, 0.34, 0.24))
    head = part("Head", body, neck,
                lambda bm, o: ellipsoid(bm, head_c - o, (0.16, 0.14, 0.13), 10, 6), m_body)
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.105, 0.085, 0.04))
        er = (0.08, 0.07, 0.085)
        eye = part(f"Eye_{side}", head, eye_c,
                   lambda bm, o, eye_c=eye_c: ellipsoid(bm, eye_c - o, er, 9, 6), m_eye, smooth=True)
        pup_c = Vector((sx * er[0] * 0.38, er[1] * 0.72, er[2] * 0.05))
        add_to(eye, m_pupil, lambda bm: ellipsoid(bm, pup_c, (0.048, 0.034, 0.058), 8, 6), smooth=True)
        add_to(eye, m_eye, lambda bm: ellipsoid(
            bm, pup_c + Vector((sx * -0.011, 0.028, 0.027)), (0.016, 0.012, 0.019), 6, 4), smooth=True)

    # ---- chifre: sai do alto da testa, vai para a frente e curva para cima (ponta dourada)
    horn_base = head_c + Vector((0, 0.07, 0.1))
    horn_pts = [
        horn_base,
        horn_base + Vector((0, 0.1, 0.07)),
        horn_base + Vector((0, 0.17, 0.18)),
        horn_base + Vector((0, 0.15, 0.3)),
    ]

    def build_horn(bm, o):
        tube(bm, [p - o for p in horn_pts], [0.07, 0.055, 0.04, 0.012], 6)

    part("Horn", head, horn_base, build_horn, m_gold)

    # ---- antenas curtas, para os lados (não cruzam o chifre)
    for side, sx in (("L", -1), ("R", 1)):
        base = head_c + Vector((sx * 0.09, 0.07, 0.08))
        mid = base + Vector((sx * 0.1, 0.04, 0.09))
        tip = mid + Vector((sx * 0.1, 0.08, 0.0))

        def build_antenna(bm, o, base=base, mid=mid, tip=tip):
            tube(bm, [base - o, mid - o, tip - o], [0.018, 0.015, 0.012], 5)
            ellipsoid(bm, tip - o, (0.03, 0.03, 0.03), 6, 4)

        ant = part(f"Antenna_{side}", head, base, build_antenna, m_dark)
        paint(ant, m_gold, lambda c, t=tip - base: (c - t).length < 0.04)

    # ---- mandíbulas pequenas
    for side, sx in (("L", -1), ("R", 1)):
        mouth = head_c + Vector((sx * 0.05, 0.11, -0.08))
        pts = [mouth, mouth + Vector((sx * 0.015, 0.05, -0.01)), mouth + Vector((sx * -0.03, 0.08, -0.015))]
        part(f"Mandible_{side}", head, mouth,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.026, 0.02, 0.008], 5), m_dark)

    # ---- carapaça (élitros) em domo alto, barriga escura, costura dourada no meio
    hinge = Vector((0, 0.03, 0.3))
    dome_c = Vector((0, -0.22, 0.27))
    dr = (0.29, 0.34, 0.22)

    def build_abdomen(bm, o):
        ellipsoid(bm, dome_c - o, dr, 12, 8, Matrix.Rotation(math.radians(4), 4, "X"), poles_y=True)

    abdomen = part("Abdomen", body, hinge, build_abdomen, m_shell)
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

    add_to(abdomen, m_gold, seam)

    # ---- 6 pernas grossas, bem abertas (base larga de tanque)
    for idx, hip_y, fy in [(1, 0.17, 0.22), (2, 0.04, 0.0), (3, -0.1, -0.24)]:
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.16, hip_y, 0.18))

            def build_leg(bm, o, hip=hip, sx=sx, fy=fy):
                knee = hip + Vector((sx * 0.17, fy * 0.4, 0.08))
                ankle = Vector((sx * 0.4, hip.y + fy * 0.9, 0.05))
                foot = ankle + Vector((sx * 0.05, fy * 0.1, -0.05))
                tube(bm, [hip - o, knee - o, ankle - o, foot - o], [0.052, 0.045, 0.034, 0.028], 5)
                ellipsoid(bm, knee - o, (0.054,) * 3, 5, 4)

            part(f"Leg_{side}{idx}", body, hip, build_leg, m_dark)

    return root


ac.run({
    "name": "PlayerBeetle",
    "fbx_subdir": "Resources",
    "body_length": 1.2,
    "cam_pos": (1.6 * S, 2.15 * S, 1.2 * S),
    "cam_target": (0, -0.05 * S, 0.26 * S),
}, build_beetle)

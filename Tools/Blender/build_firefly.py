"""
Vaga-lume (voa à noite, clarão que cega): besourinho comprido em pose de voo. Escudo
(pronoto) laranja com mancha preta cobrindo a cabeça, élitros escuros com borda amarela
abertos para cima, asas claras abertas dos lados, patinhas encolhidas e a lanterna
amarelo-esverdeada na ponta do abdômen (peça própria: o jogo liga a emissão nela). ~0,6 m.

Uso:
    blender --background --python Tools/Blender/build_firefly.py

Gera Assets/Art/Models/Characters/Firefly.fbx e Tools/Blender/previews/Firefly.png.

Peças (pivô na articulação; ver ant_common.py):
    Firefly (raiz)
      Body                 tórax + escudo (pivô no centro)
        Head               pivô no pescoço (embaixo do escudo)
          Eye_L / Eye_R, Antenna_L / Antenna_R
        Abdomen            pivô na cintura
          Lantern          ponta brilhante (pivô no meio dela: de onde sai o clarão)
        Elytra_L / Elytra_R   capas duras, abertas (pivô na base)
        Wing_L / Wing_R       asas que batem (pivô na base; o jogo anima)
        Limb_L1..L3 / Limb_R1..R3   patinhas encolhidas (não andam: ele voa)
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402


def build_firefly(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    import bpy
    name = cfg["name"]

    m_shield = material(f"{name}_Shield", (0.95, 0.32, 0.06), 0.45)  # pronoto laranja
    m_dark = material(f"{name}_Dark", (0.05, 0.04, 0.035), 0.5)     # cabeça, mancha, élitros
    m_edge = material(f"{name}_Edge", (0.95, 0.78, 0.2), 0.45)      # borda dos élitros
    m_belly = material(f"{name}_Belly", (0.3, 0.22, 0.12), 0.55)
    m_lantern = material(f"{name}_Lantern", (0.8, 1.0, 0.28), 0.3)  # lanterna (emissão no jogo)
    m_wing = material(f"{name}_Wing", (0.78, 0.86, 0.95), 0.3)
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    z0 = 0.3  # o modelo voa: tudo um pouco acima do chão (o agente ainda soma a altura do voo)

    # ---- tórax + escudo laranja (pronoto) avançando sobre a cabeça, com mancha preta
    th_c = Vector((0, 0.05, z0))
    body = part("Body", root, th_c, lambda bm, o: ellipsoid(bm, th_c - o, (0.1, 0.11, 0.08), 9, 6), m_belly)
    shield_c = Vector((0, 0.13, z0 + 0.05))
    sr = (0.13, 0.12, 0.05)
    add_to(body, m_shield, lambda bm: ellipsoid(bm, shield_c - th_c, sr, 10, 5, Matrix.Rotation(math.radians(10), 4, "X")))
    paint(body, m_dark, lambda c: c.z > 0.07 and abs(c.x) < 0.045 and 0.02 < c.y < 0.14)

    # ---- cabeça escondida embaixo do escudo, com olhões e antenas
    neck = Vector((0, 0.15, z0 - 0.01))
    head_c = Vector((0, 0.23, z0 - 0.02))
    head = part("Head", body, neck, lambda bm, o: ellipsoid(bm, head_c - o, (0.075, 0.065, 0.06), 8, 6), m_dark)
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.055, 0.035, 0.01))
        er = (0.042, 0.04, 0.045)
        eye = part(f"Eye_{side}", head, eye_c, lambda bm, o, e=eye_c: ellipsoid(bm, e - o, er, 8, 6), m_eye, smooth=True)
        pup = Vector((sx * er[0] * 0.4, er[1] * 0.65, 0.0))
        add_to(eye, m_pupil, lambda bm, p=pup: ellipsoid(bm, p, (0.026, 0.02, 0.03), 7, 5), smooth=True)
        add_to(eye, m_eye, lambda bm, p=pup: ellipsoid(bm, p + Vector((sx * -0.007, 0.016, 0.012)), (0.008, 0.006, 0.009), 5, 3),
               smooth=True)

        base = head_c + Vector((sx * 0.03, 0.05, 0.03))
        mid = base + Vector((sx * 0.06, 0.1, 0.05))
        tip = mid + Vector((sx * 0.08, 0.08, 0.06))
        part(f"Antenna_{side}", head, base,
             lambda bm, o, b=base, m=mid, t=tip: tube(bm, [b - o, m - o, t - o], [0.014, 0.011, 0.008], 4), m_dark)

    # ---- abdômen comprido com anéis, e a lanterna na ponta
    waist = Vector((0, -0.04, z0 - 0.01))
    ab_c = Vector((0, -0.2, z0 - 0.03))
    abr = (0.085, 0.17, 0.06)
    abdomen = part("Abdomen", body, waist,
                   lambda bm, o: ellipsoid(bm, ab_c - o, abr, 10, 8, Matrix.Rotation(math.radians(-8), 4, "X"), poles_y=True),
                   m_belly)
    paint(abdomen, m_dark, lambda c: math.sin((c + waist - ab_c).y * 55) > 0.6 and (c + waist - ab_c).z > 0)
    lantern_c = Vector((0, -0.4, z0 - 0.055))
    part("Lantern", abdomen, lantern_c,
         lambda bm, o: ellipsoid(bm, lantern_c - o, (0.092, 0.1, 0.078), 10, 7, poles_y=True), m_lantern, smooth=True)

    # ---- élitros abertos (capas duras) e asas claras abertas para os lados
    for side, sx in (("L", -1), ("R", 1)):
        e_root = Vector((sx * 0.03, 0.02, z0 + 0.06))
        e_c = Vector((sx * 0.1, -0.15, z0 + 0.16))
        rot = Matrix.Rotation(math.radians(sx * 35), 4, "Y") @ Matrix.Rotation(math.radians(-25), 4, "X")
        el = part(f"Elytra_{side}", body, e_root,
                  lambda bm, o, c=e_c, r=rot: ellipsoid(bm, c - o, (0.07, 0.19, 0.022), 8, 5, r), m_dark)
        paint(el, m_edge, lambda c, sx=sx, cc=e_c - e_root: (c - cc).x * sx > 0.035)

        w_root = Vector((sx * 0.05, 0.0, z0 + 0.05))
        w_c = Vector((sx * 0.3, -0.06, z0 + 0.1))
        wrot = Matrix.Rotation(math.radians(sx * -12), 4, "Y") @ Matrix.Rotation(math.radians(sx * 18), 4, "Z")
        part(f"Wing_{side}", body, w_root,
             lambda bm, o, c=w_c, r=wrot: ellipsoid(bm, c - o, (0.26, 0.09, 0.008), 10, 4, r), m_wing)

    # ---- patinhas encolhidas embaixo (voando)
    for idx, hip_y in ((1, 0.11), (2, 0.05), (3, -0.01)):
        for side, sx in (("L", -1), ("R", 1)):
            hip = Vector((sx * 0.06, hip_y, z0 - 0.06))
            knee = hip + Vector((sx * 0.08, 0.02, -0.05))
            foot = knee + Vector((sx * -0.02, -0.06, -0.07))
            part(f"Limb_{side}{idx}", body, hip,
                 lambda bm, o, h=hip, k=knee, f=foot: tube(bm, [h - o, k - o, f - o], [0.014, 0.012, 0.008], 4), m_dark)
    return root


ac.run({
    "name": "Firefly",
    "body_length": 0.6,
    "cam_pos": (1.15, 1.25, 0.95),
    "cam_target": (0, -0.05, 0.3),
}, build_firefly)

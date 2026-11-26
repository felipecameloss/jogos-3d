"""
Centopeia (corpo segmentado, cada segmento com vida própria): o FBX é UM segmento. A placa
do corpo é vermelho-escura com borda laranja e tem dois pares de pernas amarelo-alaranjadas;
a peça Head (olhos, antenas compridas e presas curvas) e a peça Tail (cercos compridos para
trás) vêm juntas e o jogo liga só no segmento que lidera / no último (Centipede.cs).
Cada segmento ~0,35 m antes da escala do prefab.

Uso:
    blender --background --python Tools/Blender/build_centipede.py

Gera Assets/Art/Models/Characters/Centipede.fbx e Tools/Blender/previews/Centipede.png.
cfg["chain"] > 0 monta segmentos extras só para o retrato do diário (build_enemy_icons.py).

Peças (pivô na articulação; ver ant_common.py):
    Centipede (raiz)
      Body                 placa do segmento (pivô no centro)
        Leg_L1, Leg_R1, Leg_L2, Leg_R2   (1 = par da frente)
        Head               pivô na frente da placa
          Eye_L / Eye_R, Antenna_L / Antenna_R, Mandible_L / Mandible_R (presas)
        Tail               pivô atrás da placa: Cercus_L / Cercus_R no mesmo objeto
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402

PLATE_Z = 0.17
PLATE_R = (0.19, 0.16, 0.085)


def build_centipede(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    import bpy
    name = cfg["name"]

    m = {
        "plate": material(f"{name}_Plate", (0.32, 0.04, 0.025), 0.45),   # vermelho-escuro (dominante)
        "rim": material(f"{name}_Rim", (0.95, 0.42, 0.04), 0.45),        # borda laranja
        "leg": material(f"{name}_Leg", (1.0, 0.62, 0.08), 0.5),          # pernas amarelo-alaranjadas
        "head": material(f"{name}_Head", (0.45, 0.06, 0.03), 0.45),
        "fang": material(f"{name}_Fang", (0.08, 0.03, 0.02), 0.35),
        "eye": material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25),
        "pupil": material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15),
    }

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    body = segment(root, "Body", Vector((0, 0, 0)), m)

    # ---- cabeça: achatada, presas curvas grossas e antenas bem compridas
    head_pivot = Vector((0, 0.14, PLATE_Z))
    head_c = Vector((0, 0.27, PLATE_Z - 0.01))
    hr = (0.17, 0.13, 0.08)
    head = part("Head", body, head_pivot, lambda bm, o: ellipsoid(bm, head_c - o, hr, 10, 6), m["head"])
    paint(head, m["rim"], lambda c: (c + head_pivot - head_c).z < -hr[2] * 0.4)
    for side, sx in (("L", -1), ("R", 1)):
        eye_c = head_c + Vector((sx * 0.085, 0.07, 0.055))
        er = (0.045, 0.042, 0.048)
        eye = part(f"Eye_{side}", head, eye_c, lambda bm, o, e=eye_c: ellipsoid(bm, e - o, er, 8, 6), m["eye"], smooth=True)
        pup = Vector((sx * er[0] * 0.35, er[1] * 0.66, 0.004))
        add_to(eye, m["pupil"], lambda bm, p=pup: ellipsoid(bm, p, (0.026, 0.02, 0.031), 7, 5), smooth=True)
        add_to(eye, m["eye"], lambda bm, p=pup: ellipsoid(bm, p + Vector((sx * -0.007, 0.016, 0.012)), (0.008, 0.006, 0.009), 5, 3),
               smooth=True)

        base = head_c + Vector((sx * 0.07, 0.11, 0.03))
        pts = [base, base + Vector((sx * 0.1, 0.16, 0.1)), base + Vector((sx * 0.24, 0.32, 0.12)),
               base + Vector((sx * 0.38, 0.46, 0.05))]
        part(f"Antenna_{side}", head, base,
             lambda bm, o, pts=pts: tube(bm, [p - o for p in pts], [0.022, 0.018, 0.013, 0.008], 5), m["leg"])

        fang0 = head_c + Vector((sx * 0.08, 0.1, -0.05))
        fang = [fang0, fang0 + Vector((sx * 0.05, 0.08, -0.01)), fang0 + Vector((sx * 0.0, 0.17, -0.02)),
                fang0 + Vector((sx * -0.06, 0.2, -0.025))]
        part(f"Mandible_{side}", head, fang0,
             lambda bm, o, pts=fang: tube(bm, [p - o for p in pts], [0.035, 0.03, 0.02, 0.006], 5), m["fang"])

    # ---- rabo: dois cercos compridos para trás
    tail_pivot = Vector((0, -0.14, PLATE_Z))

    def build_tail(bm, o):
        for sx in (-1, 1):
            c0 = Vector((sx * 0.06, -0.16, PLATE_Z))
            pts = [c0, c0 + Vector((sx * 0.1, -0.2, 0.08)), c0 + Vector((sx * 0.2, -0.42, 0.06)),
                   c0 + Vector((sx * 0.26, -0.58, -0.02))]
            tube(bm, [p - o for p in pts], [0.028, 0.022, 0.014, 0.007], 5)

    part("Tail", body, tail_pivot, build_tail, m["leg"])

    # ---- só para o retrato: mais segmentos atrás, numa curva
    # (arco em "C": cada segmento ~0,3 m atrás do anterior, virado para ele)
    radius = 0.58
    for i in range(cfg.get("chain", 0)):
        a = math.radians(30 * (i + 1))
        offset = Vector((radius - radius * math.cos(a), -radius * math.sin(a), 0))
        seg = segment(root, f"Chain{i + 1}", offset, m)
        seg.rotation_euler = (0, 0, a)
    return root


def segment(parent, name, offset, m):
    """Placa com borda laranja, sulco no meio e dois pares de pernas."""
    c = Vector((0, 0, PLATE_Z)) + offset

    def build_plate(bm, o):
        verts = ellipsoid(bm, c - o, PLATE_R, 12, 6)
        for v in verts:  # barriga reta
            if v.co.z + o.z < PLATE_Z - 0.02:
                v.co.z = PLATE_Z - 0.02 - o.z + (v.co.z + o.z - (PLATE_Z - 0.02)) * 0.4

    plate = part(name, parent, c, build_plate, m["plate"])
    paint(plate, m["rim"], lambda p: abs(p.x) > PLATE_R[0] * 0.72)
    paint(plate, m["head"], lambda p: abs(p.x) < 0.025 and p.z > 0.05)
    for idx, y in ((1, 0.065), (2, -0.065)):
        for side, sx in (("L", -1), ("R", 1)):
            hip = c + Vector((sx * 0.15, y, -0.03))
            knee = hip + Vector((sx * 0.17, 0.02, 0.1))
            foot = Vector((sx * 0.4, c.y + y - 0.04, 0.0))

            def build_leg(bm, o, h=hip, k=knee, f=foot):
                tube(bm, [h - o, k - o, f - o], [0.026, 0.02, 0.011], 5)

            leg_name = f"Leg_{side}{idx}" if name == "Body" else f"{name}Leg_{side}{idx}"
            part(leg_name, plate, hip, build_leg, m["leg"])
    return plate


ac.run({
    "name": "Centipede",
    "body_length": 0.6,
    "cam_pos": (1.25, 1.35, 0.95),
    "cam_target": (0, 0.0, 0.15),
}, build_centipede)

"""
Formiga-soldado: o tanque. Vermelho-escuro, cabeça enorme com "sobrancelhas" de
armadura (cara de brava, mas ainda fofa), mandíbulas grandes em foice cor de osso,
placa nas costas, pernas grossas e base larga. ~1,4 m (a operária tem 1 m).

Uso:
    blender --background --python Tools/Blender/build_soldier_ant.py

Gera Assets/Art/Models/Characters/SoldierAnt.fbx e Tools/Blender/previews/SoldierAnt.png.
Mesmas peças/pivôs da operária (ver ant_common.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common  # noqa: E402

S = 1.4  # escala em relação à operária (só para a câmera do preview)

ant_common.run({
    "name": "SoldierAnt",
    "body_length": 1.4,
    "color_body": (0.42, 0.03, 0.025),     # vermelho-escuro (cor dominante)
    "color_dark": (0.16, 0.015, 0.012),    # pernas e antenas quase pretas
    "color_light": (0.78, 0.14, 0.06),     # faixas: vermelho mais vivo
    "color_plate": (0.24, 0.02, 0.018),    # armadura: sobrancelhas e placa das costas
    "color_mandible": (0.93, 0.78, 0.52),  # osso/âmbar: a ameaça aparece de longe
    "hip_z": 0.29,
    "thorax_radii": (0.16, 0.2, 0.14),
    "thorax_plate": True,
    "head_offset": (0.4, 0.17),
    "head_radii": (0.27, 0.22, 0.23),      # cabeça maior e mais larga que a da operária
    "eye_offset": (0.15, 0.15, 0.04),
    "eye_radii": (0.075, 0.06, 0.08),
    "pupil_radii": (0.046, 0.03, 0.055),
    "brow": True,
    "antenna": {"elbow": (0.05, 0.02, 0.14), "tip": (0.09, 0.13, 0.02), "radii": (0.028, 0.024, 0.02), "ball": 0.0},
    "mandible": {
        # foice: sai para o lado, vai para a frente e a ponta curva para dentro
        "points": [(0, 0, 0), (0.07, 0.08, 0.01), (0.03, 0.18, 0.02), (-0.075, 0.22, 0.02)],
        "radii": [0.05, 0.045, 0.033, 0.01],
        "mouth": (0.09, 0.17, -0.07),
        "sides": 5,
    },
    "abdomen_offset": (-0.4, 0.06),
    "abdomen_radii": (0.21, 0.25, 0.19),
    "leg_radii": (0.062, 0.054, 0.042, 0.036),
    "knee_segs": (5, 4),
    "head_segs": (10, 7),
    "leg_reach": 0.46,
    "knee_lift": 0.14,
    "cam_pos": (1.6 * S, 2.15 * S, 1.2 * S),
    "cam_target": (0, -0.02 * S, 0.26 * S),
})

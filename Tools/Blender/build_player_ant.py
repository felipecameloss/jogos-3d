"""
Formiga jogável (personagem "Formiga"): azul-turquesa com detalhes amarelos, para nunca
ser confundida com as formigas inimigas (vermelhas). Olhos ainda maiores (heroína fofa),
corpo mais esguio (rápida e frágil) e pernas longas.

Uso:
    blender --background --python Tools/Blender/build_player_ant.py

Gera Assets/Art/Models/Characters/Resources/PlayerAnt.fbx (o jogo carrega com
Resources.Load("PlayerAnt"), ver PlayerCharacter.cs) e Tools/Blender/previews/PlayerAnt.png.
Mesmas peças/pivôs da operária (ver ant_common.py).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common  # noqa: E402

ant_common.run({
    "name": "PlayerAnt",
    "fbx_subdir": "Resources",
    "body_length": 1.0,
    "color_body": (0.05, 0.55, 0.85),      # azul-turquesa (cor dominante)
    "color_dark": (0.02, 0.17, 0.33),      # pernas e antenas azul-marinho
    "color_light": (1.0, 0.82, 0.18),      # faixas do abdômen e ponta das antenas: amarelo
    "color_mandible": (1.0, 0.82, 0.18),
    "thorax_radii": (0.12, 0.18, 0.11),
    "head_radii": (0.24, 0.21, 0.22),
    "eye_offset": (0.13, 0.14, 0.06),
    "eye_radii": (0.095, 0.078, 0.105),    # olhos maiores que os da operária
    "pupil_radii": (0.056, 0.04, 0.072),
    "abdomen_radii": (0.18, 0.25, 0.18),
    "leg_reach": 0.44,
    "knee_lift": 0.15,
})

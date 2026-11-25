"""
Lupa gigante de brinquedo (evento "criança com lupa", issue #34): aro amarelo grosso,
vidro azul-claro levemente abaulado e cabo vermelho de plástico. Fica no céu entre o sol
e o player, concentrando o raio de sol no chão (SunSpot.cs).

Uso:
    blender --background --python Tools/Blender/build_giant_magnifier.py

Gera Assets/Art/Models/Props/GiantMagnifier.fbx e Tools/Blender/previews/GiantMagnifier.png.

Peças (pivô no centro do vidro; o vidro fica no plano XY do Blender, normal em +Z, que
chega no Unity como +Y; o cabo aponta para +Y do Blender = +Z do Unity):
    GiantMagnifier (raiz)
      Rim      aro
      Glass    vidro
      Handle   cabo
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import ellipsoid, material, part, tube  # noqa: E402

NAME = "GiantMagnifier"
R = 4.0          # raio do vidro (m)
RIM = 0.45       # espessura do aro
SEGS = 20


def main():
    ac.reset_scene()
    ac.PIVOTS.clear()
    m_rim = material(f"{NAME}_Rim", (1.0, 0.78, 0.08), 0.35)        # amarelo brinquedo
    m_glass = material(f"{NAME}_Glass", (0.62, 0.86, 1.0), 0.05)    # vidro (no jogo fica translúcido)
    m_handle = material(f"{NAME}_Handle", (0.9, 0.12, 0.1), 0.35)   # cabo vermelho

    root = bpy.data.objects.new(NAME, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[NAME] = Vector((0, 0, 0))
    c = Vector((0, 0, 0))

    def build_rim(bm, o):
        pts = [Vector((math.cos(2 * math.pi * i / SEGS) * R, math.sin(2 * math.pi * i / SEGS) * R, 0)) - o
               for i in range(SEGS + 2)]  # um gomo a mais: as pontas se sobrepõem (sem emenda)
        tube(bm, pts, [RIM] * len(pts), 6)

    part("Rim", root, c, build_rim, m_rim)
    part("Glass", root, c, lambda bm, o: ellipsoid(bm, c - o, (R * 0.97, R * 0.97, 0.35), SEGS, 4), m_glass)

    def build_handle(bm, o):
        a = Vector((0, R + RIM * 0.6, 0))
        b = Vector((0, R + 1.2, 0))
        d = Vector((0, R + 5.5, 0))
        tube(bm, [a - o, b - o, d - o], [0.55, 0.7, 0.62], 8)
        ellipsoid(bm, d - o, (0.62, 0.62, 0.62), 8, 5)

    part("Handle", root, c, build_handle, m_handle)
    bpy.context.view_layer.update()

    print(f"[{NAME}] triangulos={ac.triangle_count()}")
    fbx = os.path.join(ac.ROOT, "Assets", "Art", "Models", "Props", f"{NAME}.fbx")
    ac.export_fbx(fbx)
    # preview: lupa de pé, inclinada, vista de 3/4
    root.rotation_euler = (math.radians(70), 0, math.radians(25))
    root.location = (0, 0, 7)
    bpy.context.view_layer.update()
    ac.render_preview(os.path.join(ac.HERE, "previews", f"{NAME}.png"), (24.0, -30.0, 16.0), (0, 0, 8.0), lens=50, ground_size=40)
    print(f"[{NAME}] FBX: {fbx}")


main()

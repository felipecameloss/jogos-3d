"""
Gota de chuva gigante (evento de chuva, issue #33): gota low-poly facetada, azul-clara.
No jogo a cor e a transparência vêm do material RainWater (RainBuilder.cs); aqui só a forma.
Pivô na ponta de baixo (é onde ela bate no chão). ~2,6 m de altura.

Uso:
    blender --background --python Tools/Blender/build_raindrop.py

Gera Assets/Art/Models/Props/RainDrop.fbx e Tools/Blender/previews/RainDrop.png.

Peças:
    RainDrop (raiz, no chão)
      Drop     a gota (pivô na base)
"""
import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import material, part  # noqa: E402

NAME = "RainDrop"
RADIUS = 1.1      # raio da parte redonda
HEIGHT = 2.6      # da base até a ponta de cima
SEGS = 12         # gomos em volta
RINGS = 8         # anéis da base até a ponta


def build_drop(bm, o):
    """Gota: metade de baixo é uma esfera; a de cima afina em curva até a ponta."""
    center_z = RADIUS
    rings = []
    for r in range(1, RINGS):
        t = r / RINGS                          # 0 = base, 1 = ponta
        if t <= 0.45:
            # esfera: ângulo de -90° (base) até ~0° (equador)
            a = -math.pi / 2 + (t / 0.45) * (math.pi / 2)
            rad = RADIUS * math.cos(a)
            z = center_z + RADIUS * math.sin(a)
        else:
            # afina em curva até a ponta
            k = (t - 0.45) / 0.55
            rad = RADIUS * (1 - k) ** 1.6
            z = center_z + (HEIGHT - center_z) * k
        ring = []
        for s in range(SEGS):
            ang = 2 * math.pi * s / SEGS
            ring.append(bm.verts.new(Vector((math.cos(ang) * rad, math.sin(ang) * rad, z)) - o))
        rings.append(ring)
    bottom = bm.verts.new(Vector((0, 0, 0)) - o)
    top = bm.verts.new(Vector((0, 0, HEIGHT)) - o)
    for s in range(SEGS):
        s2 = (s + 1) % SEGS
        bm.faces.new((bottom, rings[0][s2], rings[0][s]))
        bm.faces.new((top, rings[-1][s], rings[-1][s2]))
    for r0, r1 in zip(rings, rings[1:]):
        for s in range(SEGS):
            s2 = (s + 1) % SEGS
            bm.faces.new((r0[s], r0[s2], r1[s2], r1[s]))


def main():
    ac.reset_scene()
    ac.PIVOTS.clear()
    m_water = material(f"{NAME}_Water", (0.45, 0.75, 1.0), 0.1)
    root = bpy.data.objects.new(NAME, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[NAME] = Vector((0, 0, 0))
    part("Drop", root, Vector((0, 0, 0)), build_drop, m_water)
    bpy.context.view_layer.update()

    tris = ac.triangle_count()
    print(f"[{NAME}] triangulos={tris} altura={HEIGHT}m")
    fbx = os.path.join(ac.ROOT, "Assets", "Art", "Models", "Props", f"{NAME}.fbx")
    ac.export_fbx(fbx)
    ac.render_preview(os.path.join(ac.HERE, "previews", f"{NAME}.png"), (5.0, -6.0, 3.6), (0, 0, 1.2), lens=60)
    print(f"[{NAME}] FBX: {fbx}")


main()

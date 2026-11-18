"""
Renderiza os insetos lado a lado (mesma escala) para comparar estilo e tamanho.

Uso:
    blender --background --python Tools/Blender/render_lineup.py

Gera Tools/Blender/previews/Lineup.png. Não exporta nada.
"""
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ant_common as ac  # noqa: E402

# (script, x). A câmera olha de frente (de +Y), então +X fica à ESQUERDA da imagem.
MODELS = [
    ("build_aphid.py", 4.6),
    ("build_worker_ant.py", 3.25),
    ("build_soldier_ant.py", 1.55),
    ("build_mantis.py", -0.45),
    ("build_bombardier_beetle.py", -2.5),
    ("build_spider.py", -4.4),
]


def load_config(script):
    """Lê o script do modelo sem rodar o export: troca ac.run(...) por captura."""
    captured = {}
    src = open(os.path.join(HERE, script), encoding="utf-8").read()
    ns = {"__file__": os.path.join(HERE, script), "__name__": "lineup"}
    real_run = ac.run
    ac.run = lambda overrides, builder=None: captured.update(cfg=overrides, builder=builder)
    try:
        exec(src, ns)
    finally:
        ac.run = real_run
    return {**ac.DEFAULTS, **captured["cfg"]}, captured["builder"] or ac.build_ant


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    ac.RESET_SCENE = False  # os builders não limpam a cena: junta todos
    for script, x in MODELS:
        cfg, builder = load_config(script)
        before = set(bpy.data.objects)
        builder(cfg)
        new = [o for o in bpy.data.objects if o not in before]
        bpy.context.view_layer.update()
        k = cfg["body_length"] / ac.body_length()
        for o in new:
            if o.type == "MESH":
                o.data.transform(Matrix.Scale(k, 4))
            o.location = o.location * k
        # nomes únicos para o próximo modelo poder usar Body/Head/...
        for o in new:
            o.name = f"{cfg['name']}_{o.name}"
        root = next(o for o in new if o.parent is None)
        root.location = (x, 0, 0)
        bpy.context.view_layer.update()
        print(f"[lineup] {cfg['name']}: corpo {cfg['body_length']} m")

    ac.render_preview(os.path.join(HERE, "previews", "Lineup.png"),
                      cam_pos=(0.1, 12.5, 2.7), target=(0.1, -0.3, 0.85), lens=36, ground_size=80)
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 1600, 900
    bpy.ops.render.render(write_still=True)


main()

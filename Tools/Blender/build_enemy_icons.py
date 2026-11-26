"""
Retratos dos inimigos para o Diário de descobertas: mesmo render dos ícones de item
(256x256, fundo transparente, contorno escuro), com a câmera vendo o inseto de frente em 3/4.
No diário, inimigo ainda não visto aparece como silhueta (o retrato pintado de preto).

Uso:
    blender --background --python Tools/Blender/build_enemy_icons.py [-- NomeDoInimigo ...]

Gera:
    Assets/Art/Icons/Enemies/<Inimigo>.png   (nome = classe do inimigo: WorkerAnt, Spider...)
    Tools/Blender/previews/EnemyIcons.png    folha de contato com todos
"""
import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import ant_common as ac  # noqa: E402
import build_item_icons as icons  # noqa: E402

OUT = os.path.join(ac.ROOT, "Assets", "Art", "Icons", "Enemies")
SHEET = os.path.join(HERE, "previews", "EnemyIcons.png")
# Os insetos olham para +Y no Blender: câmera na frente, à direita e um pouco de cima
VIEW_DIR = Vector((0.85, 1.0, 0.6)).normalized()

# nome -> script do modelo (ant_common) ou .glb já exportado (+ giro para olhar para +Y)
ENEMIES = {
    "WorkerAnt": {"script": "build_worker_ant.py"},
    "SoldierAnt": {"script": "build_soldier_ant.py"},
    "Termite": {"glb": "Assets/Art/Models/Characters/Termite.glb", "yaw": 180},
    "Aphid": {"script": "build_aphid.py"},
    "BombardierBeetle": {"script": "build_bombardier_beetle.py"},
    "Spider": {"script": "build_spider.py"},
    "SpiderQueen": {"script": "build_spider_queen.py"},
    "MantisBoss": {"script": "build_mantis.py"},
    "Grasshopper": {"script": "build_grasshopper.py"},
    "Slug": {"script": "build_slug.py"},
    "Firefly": {"script": "build_firefly.py"},
    # o FBX é um segmento só; no retrato, uma fila deles em curva, vista mais de cima e com
    # contorno fino (as pernas são finas e sumiriam no contorno padrão)
    "Centipede": {"script": "build_centipede.py", "cfg": {"chain": 3}, "view": (0.55, 0.75, 1.5), "outline": 1.2},
}


def load_config(script):
    """Lê o script do modelo sem exportar nada (mesma ideia do render_lineup.py)."""
    captured = {}
    src = open(os.path.join(HERE, script), encoding="utf-8").read()
    ns = {"__file__": os.path.join(HERE, script), "__name__": "enemy_icons"}
    real_run = ac.run
    ac.run = lambda overrides, builder=None: captured.update(cfg=overrides, builder=builder)
    try:
        exec(src, ns)
    finally:
        ac.run = real_run
    return {**ac.DEFAULTS, **captured["cfg"]}, captured["builder"] or ac.build_ant


def build(spec):
    if "script" in spec:
        cfg, builder = load_config(spec["script"])
        builder({**cfg, **spec.get("cfg", {})})
        return
    bpy.ops.import_scene.gltf(filepath=os.path.join(ac.ROOT, spec["glb"]))
    roots = [o for o in bpy.context.scene.objects if o.parent is None]
    for o in roots:
        o.matrix_world = Matrix.Rotation(math.radians(spec.get("yaw", 0)), 4, "Z") @ o.matrix_world


def lights(scene):
    """Luz principal vindo de frente/cima (do lado da câmera) e um recorte por trás."""
    key = bpy.data.objects.new("Key", bpy.data.lights.new("Key", "SUN"))
    key.data.energy = 3.0
    key.rotation_euler = (-VIEW_DIR + Vector((-0.4, 0.0, -0.6))).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(key)
    rim = bpy.data.objects.new("Rim", bpy.data.lights.new("Rim", "SUN"))
    rim.data.energy = 1.2
    rim.rotation_euler = (VIEW_DIR * Vector((-1, -1, 1)) + Vector((0, 0, -1.2))).to_track_quat("-Z", "Y").to_euler()
    scene.collection.objects.link(rim)


def render_enemy(name, spec):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    icons.MATS.clear()
    ac.RESET_SCENE = False  # a cena já está vazia; o builder não pode apagar câmera/luzes depois
    scene = bpy.context.scene
    outline = icons.setup_render(scene)
    if outline and spec.get("outline"):
        scene.render.line_thickness = spec["outline"]
        scene.view_layers[0].freestyle_settings.linesets[0].linestyle.thickness = spec["outline"]
    build(spec)
    bpy.context.view_layer.update()
    tris = ac.triangle_count()
    lights(scene)
    icons.frame_camera(scene, Vector(spec["view"]).normalized() if "view" in spec else VIEW_DIR)
    path = os.path.join(OUT, f"{name}.png")
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    print(f"[enemy-icons] {name}: {tris} tris, contorno={'sim' if outline else 'nao'}")
    return path


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv or list(ENEMIES)
    os.makedirs(OUT, exist_ok=True)
    os.makedirs(os.path.dirname(SHEET), exist_ok=True)
    for n in names:
        render_enemy(n, ENEMIES[n])
    paths = [os.path.join(OUT, f"{n}.png") for n in ENEMIES if os.path.exists(os.path.join(OUT, f"{n}.png"))]
    icons.contact_sheet(paths, cols=4, out_path=SHEET)
    print(f"[enemy-icons] {len(names)} retratos em {OUT}; folha: {SHEET}")


main()

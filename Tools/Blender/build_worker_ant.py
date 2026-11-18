"""
Formiga operária: vermelha, cabeça e olhos grandes, mandíbulas pequenas (fofa).

Uso:
    blender --background --python Tools/Blender/build_worker_ant.py

Gera Assets/Art/Models/Characters/WorkerAnt.fbx e Tools/Blender/previews/WorkerAnt.png.
A geometria e as peças estão em ant_common.py; aqui só a configuração (os padrões
de ant_common.DEFAULTS já são a operária).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common  # noqa: E402

ant_common.run({"name": "WorkerAnt"})

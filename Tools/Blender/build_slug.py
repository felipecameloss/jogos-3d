"""
Lesma (lenta, rastro de gosma): laranja-avermelhada, corpo baixo e comprido afinando no
rabo, "manto" mais escuro nas costas, borda do pé clara e os olhos nas pontas das antenas
compridas (que o ProceduralInsectAnimator balança por se chamarem Antenna_). ~1,1 m.

Uso:
    blender --background --python Tools/Blender/build_slug.py

Gera Assets/Art/Models/Characters/Slug.fbx e Tools/Blender/previews/Slug.png.

Peças (pivô na articulação; ver ant_common.py):
    Slug (raiz)
      Body                 corpo/pé (pivô no centro)
        Head               frente do corpo (pivô no "pescoço")
          Antenna_L / Antenna_R   antenas dos olhos (pivô na base, olho na ponta)
          Mandible_L / Mandible_R antenas de baixo, curtinhas
        Abdomen            manto nas costas (pivô na frente: "respira")
"""
import math
import os
import sys

from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_common as ac  # noqa: E402
from ant_common import add_to, ellipsoid, material, paint, part, tube  # noqa: E402


def build_slug(cfg):
    ac.reset_scene()
    ac.PIVOTS.clear()
    import bpy
    name = cfg["name"]

    m_body = material(f"{name}_Body", (0.78, 0.19, 0.035), 0.35)     # laranja-avermelhado (dominante)
    m_foot = material(f"{name}_Foot", (1.0, 0.56, 0.24), 0.35)      # borda do pé, clara
    m_mantle = material(f"{name}_Mantle", (0.6, 0.12, 0.03), 0.35)  # manto, mais escuro
    m_dark = material(f"{name}_Dark", (0.25, 0.05, 0.02), 0.4)      # ranhuras, buraquinho
    m_eye = material(f"{name}_EyeWhite", (0.97, 0.97, 0.95), 0.25)
    m_pupil = material(f"{name}_Pupil", (0.03, 0.02, 0.03), 0.15)

    root = bpy.data.objects.new(name, None)
    bpy.context.scene.collection.objects.link(root)
    ac.PIVOTS[name] = Vector((0, 0, 0))

    # ---- corpo: comprido, baixo, barriga reta, afinando no rabo
    body_c = Vector((0, -0.12, 0.15))
    br = (0.2, 0.55, 0.16)

    def build_body(bm, o):
        verts = ellipsoid(bm, body_c - o, br, 12, 12, poles_y=True)
        for v in verts:
            p = v.co + o - body_c
            y = p.y / br[1]                      # -1 rabo .. 1 frente
            if p.z < 0:
                p.z *= 0.45                      # barriga achatada no chão
            if y < 0:
                k = 1 - 0.55 * (-y) ** 1.6       # afina para o rabo
                p.x *= k
                p.z = p.z * (0.6 + 0.4 * k)
            v.co = body_c + p - o

    body = part("Body", root, body_c, build_body, m_body)
    # borda clara do pé (faixa de baixo) e ranhuras escuras atrás do manto
    paint(body, m_foot, lambda c: c.z < -0.005)
    paint(body, m_dark, lambda c: c.z > 0.06 and c.y < -0.12 and abs(c.x) < 0.035)

    # ---- frente do corpo (cabeça), um pouco erguida
    neck = Vector((0, 0.3, 0.16))
    head_c = Vector((0, 0.38, 0.155))
    hr = (0.165, 0.18, 0.12)
    head = part("Head", body, neck, lambda bm, o: ellipsoid(bm, head_c - o, hr, 10, 7), m_body)
    paint(head, m_foot, lambda c: (c + neck - head_c).z < -hr[2] * 0.15)

    for side, sx in (("L", -1), ("R", 1)):
        # antenas dos olhos: compridas, para cima e para frente, olho na ponta
        base = head_c + Vector((sx * 0.06, 0.04, 0.08))
        mid = base + Vector((sx * 0.04, 0.05, 0.16))
        tip = mid + Vector((sx * 0.03, 0.04, 0.12))
        stalk = part(f"Antenna_{side}", head, base,
                     lambda bm, o, b=base, m=mid, t=tip: tube(bm, [b - o, m - o, t - o], [0.03, 0.024, 0.02], 6), m_body)
        eye_local = tip - base + Vector((0, 0.01, 0.03))
        er = (0.055, 0.05, 0.055)
        add_to(stalk, m_eye, lambda bm, e=eye_local: ellipsoid(bm, e, er, 9, 6), smooth=True)
        pup = eye_local + Vector((sx * 0.012, 0.035, 0.0))
        add_to(stalk, m_pupil, lambda bm, p=pup: ellipsoid(bm, p, (0.03, 0.022, 0.034), 7, 5), smooth=True)
        add_to(stalk, m_eye, lambda bm, p=pup: ellipsoid(bm, p + Vector((sx * -0.008, 0.02, 0.014)), (0.01, 0.008, 0.012), 5, 3),
               smooth=True)

        # antenas de baixo: tocos curtos virados para o chão
        low = head_c + Vector((sx * 0.07, 0.11, -0.04))
        part(f"Mandible_{side}", head, low,
             lambda bm, o, l=low, sx=sx: tube(bm, [l - o, l + Vector((sx * 0.03, 0.07, -0.03)) - o], [0.025, 0.016], 5),
             m_body)

    # ---- manto nas costas (com o buraquinho de respirar do lado direito)
    mantle_pivot = Vector((0, 0.24, 0.24))
    mantle_c = Vector((0, 0.06, 0.25))
    mr = (0.19, 0.24, 0.09)
    mantle = part("Abdomen", body, mantle_pivot,
                  lambda bm, o: ellipsoid(bm, mantle_c - o, mr, 12, 7, poles_y=True), m_mantle)
    hole = mantle_c + Vector((mr[0] * 0.92, 0.02, 0.0)) - mantle_pivot
    add_to(mantle, m_dark, lambda bm: ellipsoid(bm, hole, (0.02, 0.035, 0.03), 6, 4))
    # ruguinhas do manto (anéis concêntricos)
    paint(mantle, m_body, lambda c: abs(((c + mantle_pivot - mantle_c).x / mr[0]) ** 2
                                        + ((c + mantle_pivot - mantle_c).y / mr[1]) ** 2 - 0.55) < 0.06)
    return root


ac.run({
    "name": "Slug",
    "body_length": 1.1,
    "cam_pos": (1.9, 1.9, 1.15),
    "cam_target": (0, -0.05, 0.2),
}, build_slug)

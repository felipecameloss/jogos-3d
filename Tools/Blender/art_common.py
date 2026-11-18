"""Shared Blender 4.2+ procedural modeling, material, validation, and GLB export helpers."""
import math
import os
import bpy
import bmesh
from mathutils import Matrix, Vector, Euler


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def ensure_collection(name, parent=None):
    col = bpy.data.collections.get(name)
    if col is None:
        col = bpy.data.collections.new(name)
        target = parent if parent is not None else bpy.context.scene.collection
        target.children.link(col)
    return col


def make_material(
    name,
    color,
    roughness=0.45,
    metallic=0.0,
    emission=None,
    emission_strength=0.0,
    alpha=1.0,
):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name=name)
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    links = mat.node_tree.links
    nodes.clear()

    out = nodes.new("ShaderNodeOutputMaterial")
    bsdf = nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = float(roughness)
    bsdf.inputs["Metallic"].default_value = float(metallic)
    if "Specular IOR Level" in bsdf.inputs:
        bsdf.inputs["Specular IOR Level"].default_value = 0.45
    if emission is not None and emission_strength > 0.0:
        bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = float(emission_strength)
    if alpha < 0.999:
        bsdf.inputs["Alpha"].default_value = float(alpha)
        mat.blend_method = "BLEND"

    links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return mat


def create_mesh_object(name, bm, material=None, parent=None, location=(0, 0, 0), rotation=(0, 0, 0), smooth=True):
    mesh = bpy.data.meshes.new(name)
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.location = location
    obj.rotation_euler = rotation
    if material is not None:
        obj.data.materials.append(material)
    if parent is not None:
        obj.parent = parent
    if smooth:
        for poly in mesh.polygons:
            poly.use_smooth = True
    return obj


def make_ellipsoid(bm, center, radii, segs=14, rings=10, taper_z=0.0, bend_y=0.0):
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    cx, cy, cz = center
    rx, ry, rz = radii
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        # In Blender coords: X=right, Y=forward (+Z Unity after glTF export if -Y forward),
        # wait: glTF exports Blender +Y -> -Z Unity, -Y -> +Z Unity!
        # Let's author directly in Blender Z-up, -Y forward (or rotate root at export / author +Y = forward and rotate root?).
        # Wait: standard glTF exporter with export_yup=True maps:
        # Blender (x, y, z) -> glTF (-x? No: x, z, -y)!
        # Let's check: Blender +X -> glTF +X, Blender +Z -> glTF +Y, Blender -Y -> glTF +Z!
        # Authoring facing Blender -Y (so Unity +Z forward!) makes export 100% native with zero extra root rotation!
        taper = 1.0 + taper_z * y
        sx = x * rx * max(0.2, taper)
        sy = y * ry
        sz = z * rz * max(0.2, taper) + bend_y * (y * y)
        v.co = Vector((cx + sx, cy + sy, cz + sz))


def add_ellipsoid_obj(name, center, radii, material, segs=14, rings=10, taper_y=0.0, bend_z=0.0, parent=None, smooth=True):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=segs, v_segments=rings, radius=1.0)
    cx, cy, cz = center
    rx, ry, rz = radii
    for v in bm.verts:
        x, y, z = v.co.x, v.co.y, v.co.z
        taper = max(0.18, 1.0 + taper_y * y)
        v.co = Vector((cx + x * rx * taper, cy + y * ry, cz + z * rz * taper + bend_z * (y * y)))
    return create_mesh_object(name, bm, material=material, parent=parent, smooth=smooth)


def add_tapered_limb(
    name,
    points,
    radii,
    material,
    sides=8,
    cap_ends=True,
    parent=None,
    smooth=True,
):
    """Creates a smooth organic multi-segment limb/horn/antenna along 3D control points."""
    assert len(points) == len(radii) and len(points) >= 2
    bm = bmesh.new()
    rings = []
    pts = [Vector(p) for p in points]

    for i, p in enumerate(pts):
        if i == 0:
            tangent = (pts[1] - pts[0]).normalized()
        elif i == len(pts) - 1:
            tangent = (pts[-1] - pts[-2]).normalized()
        else:
            tangent = (pts[i + 1] - pts[i - 1]).normalized()

        if tangent.length < 1e-6:
            tangent = Vector((0, 0, 1))
        up_ref = Vector((0, 0, 1)) if abs(tangent.z) < 0.92 else Vector((1, 0, 0))
        side_a = tangent.cross(up_ref).normalized()
        side_b = tangent.cross(side_a).normalized()

        ring = []
        r = radii[i]
        for s in range(sides):
            ang = 2.0 * math.pi * s / sides
            offset = side_a * (math.cos(ang) * r) + side_b * (math.sin(ang) * r)
            ring.append(bm.verts.new(p + offset))
        rings.append(ring)

    for i in range(len(rings) - 1):
        r0, r1 = rings[i], rings[i + 1]
        for s in range(sides):
            ns = (s + 1) % sides
            bm.faces.new((r0[s], r0[ns], r1[ns], r1[s]))

    if cap_ends:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])

    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return create_mesh_object(name, bm, material=material, parent=parent, smooth=smooth)


def add_beveled_box(name, center, size, material, bevel_width=0.05, segments=2, parent=None, smooth=False):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    sx, sy, sz = size
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    if bevel_width > 0:
        bmesh.ops.bevel(
            bm,
            geom=list(bm.edges),
            offset=bevel_width,
            segments=segments,
            affect="EDGES",
        )
    for v in bm.verts:
        v.co += Vector(center)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return create_mesh_object(name, bm, material=material, parent=parent, smooth=smooth)


def join_children_by_material(objects, target_name, parent=None):
    """Joins multiple mesh objects cleanly into one mesh (preserving multi-material slots)."""
    meshes = [o for o in objects if o is not None and o.type == "MESH"]
    if not meshes:
        return None
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    joined = bpy.context.view_layer.objects.active
    joined.name = target_name
    joined.data.name = target_name
    if parent is not None:
        joined.parent = parent
    return joined


def ground_hierarchy_to_z0(root_objects):
    """Shifts root objects vertically in Blender Z so lowest world vertex rests cleanly on Z=0."""
    min_z = float("inf")
    mesh_objs = [o for o in bpy.data.objects if o.type == "MESH"]
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in mesh_objs:
        ev = obj.evaluated_get(depsgraph)
        for corner in ev.bound_box:
            wc = obj.matrix_world @ Vector(corner)
            if wc.z < min_z:
                min_z = wc.z
    if math.isfinite(min_z) and abs(min_z) > 1e-4:
        for obj in root_objects:
            obj.location.z -= min_z
        bpy.context.view_layer.update()


def validate_and_export_glb(filepath, export_selected=False):
    os.makedirs(os.path.dirname(filepath), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            # Apply scale on standalone meshes without distorting child local pivots if parented
            if obj.parent is None:
                bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    tris = 0
    for obj in bpy.data.objects:
        if obj.type == "MESH":
            mesh = obj.data
            mesh.calc_loop_triangles()
            tris += len(mesh.loop_triangles)

    bpy.ops.export_scene.gltf(
        filepath=filepath,
        export_format="GLB",
        export_yup=True,
        export_apply=True,
        export_materials="EXPORT",
        use_selection=export_selected,
    )
    return tris


def render_turntable_preview(out_png, subject_center=(0, 0, 0.5), distance=3.2):
    """Renders a 2x2 contact sheet preview (Hero 3/4, Front, Side, Top) via Cycles CPU."""
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 512
    scene.render.resolution_y = 512

    world = bpy.data.worlds.get("PreviewWorld") or bpy.data.worlds.new("PreviewWorld")
    scene.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.18, 0.22, 0.24, 1.0)
        bg.inputs[1].default_value = 0.9

    # Studio 3-point lighting
    key_data = bpy.data.lights.new("KeyLight", type="SUN")
    key_data.energy = 3.2
    key_obj = bpy.data.objects.new("KeyLight", key_data)
    key_obj.rotation_euler = (math.radians(48), math.radians(15), math.radians(-35))
    scene.collection.objects.link(key_obj)

    fill_data = bpy.data.lights.new("FillLight", type="AREA")
    fill_data.energy = 45.0
    fill_data.color = (0.8, 0.9, 1.0)
    fill_obj = bpy.data.objects.new("FillLight", fill_data)
    fill_obj.location = (distance * 1.4, -distance * 0.8, distance * 0.9)
    scene.collection.objects.link(fill_obj)

    cam_data = bpy.data.cameras.new("PreviewCam")
    cam_obj = bpy.data.objects.new("PreviewCam", cam_data)
    scene.collection.objects.link(cam_obj)
    scene.camera = cam_obj

    target = Vector(subject_center)
    cam_pos = target + Vector((distance * 0.72, -distance * 0.85, distance * 0.48))
    direction = (target - cam_pos).normalized()
    cam_obj.location = cam_pos
    cam_obj.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()

    os.makedirs(os.path.dirname(out_png), exist_ok=True)
    scene.render.filepath = out_png
    bpy.ops.render.render(write_still=True)

    # Clean up studio objects so they don't get exported
    for tmp in (key_obj, fill_obj, cam_obj):
        bpy.data.objects.remove(tmp, do_unlink=True)

"""Render transparent owned-model inventory icons with Blender's CPU renderer."""
import bpy
import sys
from mathutils import Vector
from pathlib import Path

repo = Path(__file__).resolve().parents[1]
sources = repo / "assets/LSDStudio/Blender"
output = repo / "src/LSDGameplay/Assets/Icons"
output.mkdir(parents=True, exist_ok=True)
only_icons=set(sys.argv[sys.argv.index("--")+1:]) if "--" in sys.argv else set()

for name, source, only in [
    ("Bench", "Bench", None), ("Cabinet", "Cabinet", None),
    ("Prism", "Parcel", None), ("Paper", "Sheet", "PaperBody"),
    ("Ink", "Bench", "InkCartridge"), ("Sleeve", "Packet", "PaperBody")
]:
    if only_icons and name not in only_icons: continue
    bpy.ops.wm.open_mainfile(filepath=str(sources / (source + ".blend")))
    scene = bpy.context.scene
    meshes = [o for o in scene.objects if o.type == "MESH"]
    for obj in meshes:
        obj.hide_render = only is not None and obj.name != only
    visible = [o for o in meshes if not o.hide_render]
    for mat in bpy.data.materials:
        mat.use_nodes = True
        shader = mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value = mat.diffuse_color
        shader.inputs["Roughness"].default_value = 0.65
    points = [o.matrix_world @ Vector(c) for o in visible for c in o.bound_box]
    lower = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    upper = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    centre = (lower + upper) / 2
    span = max(upper - lower)
    view = (2, -3, -2.3) if name == "Sleeve" else (2, -3, 2.3)
    bpy.ops.object.camera_add(location=centre + Vector(view) * span)
    camera = bpy.context.object
    camera.rotation_euler = (centre-camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = span * 1.45
    scene.camera = camera
    for offset, power, size in [((1,-2,3), 160, 2), ((-2,-1,1), 80, 2), ((0,2,2), 100, 1.5)]:
        light_offset = Vector((offset[0],offset[1],-offset[2] if name == "Sleeve" else offset[2]))
        bpy.ops.object.light_add(type="AREA", location=centre+light_offset*span)
        light = bpy.context.object
        light.rotation_euler = (centre-light.location).to_track_quat("-Z", "Y").to_euler()
        light.data.energy = power * span * span
        light.data.shape = "DISK"
        light.data.size = size * span
    if scene.world is None:
        scene.world = bpy.data.worlds.new("Icon World")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.75,0.8,0.9,1)
    scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = 0.5
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.use_denoising = False
    scene.cycles.seed = 0
    scene.render.resolution_x = scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.filepath = str(output / (name + ".png"))
    bpy.ops.render.render(write_still=True)
    print("LSD_STUDIO_ICON " + name)

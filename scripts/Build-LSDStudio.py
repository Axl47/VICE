"""Owned low-poly print studio props. Run with Blender --background --python this-file.

Produces editable .blend sources and FBX models for the matching Unity project.
Dimensions are metres; moving parts and artwork surfaces retain stable names.
"""
import bpy
import math
import sys
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "assets" / "LSDStudio"
output = root / "Assets" / "Models"
output.mkdir(parents=True, exist_ok=True)
sources = root / "Blender"
sources.mkdir(exist_ok=True)
only=set(sys.argv[sys.argv.index("--")+1:]) if "--" in sys.argv else set()

palette = {
    "Steel": (0.18, 0.23, 0.25, 1), "Wood": (0.31, 0.19, 0.12, 1),
    "Paper": (0.91, 0.88, 0.77, 1), "Ink": (0.07, 0.085, 0.10, 1),
    "Accent": (0.11, 0.72, 0.65, 1), "Lightbox": (0.61, 0.83, 0.75, 1),
    "Glass": (0.13, 0.24, 0.26, 1), "Artwork": (1, 1, 1, 1),
    "Prism": (0.59,0.24,0.85,1),
}

def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = palette[name]
    return mat

def box(name, location, size, mat="Steel", bevel=0.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(mat))
    if bevel:
        modifier = obj.modifiers.new("Soft manufactured edges", "BEVEL")
        modifier.width = bevel
        modifier.segments = 2
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj

def cylinder(name, location, radius, depth, rotation, mat="Steel"):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=depth, location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(mat))
    return obj

def art_surface(location, width, depth, name="ArtworkSurface"):
    x, y, z = location
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(x-width/2,y-depth/2,z), (x+width/2,y-depth/2,z),
                     (x+width/2,y+depth/2,z), (x-width/2,y+depth/2,z)], [], [(0,1,2,3)])
    mesh.uv_layers.new()
    for loop, uv in zip(mesh.uv_layers.active.data, [(0,0),(1,0),(1,1),(0,1)]):
        loop.uv = uv
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material("Artwork"))
    return obj

def start():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)

def export(name):
    if only and name not in only: return
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.wm.save_as_mainfile(filepath=str(sources / (name + ".blend")))
    bpy.ops.export_scene.fbx(filepath=str(output / (name + ".fbx")), use_selection=True,
        object_types={"MESH"}, apply_unit_scale=True, bake_anim=False, add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y", path_mode="AUTO")
    print("LSD_STUDIO_MODEL " + name)

start()
box("WorkbenchTop", (0,0,0.85), (1.4,0.75,0.075), "Wood")
for x in [-0.60,0.60]:
    for y in [-0.28,0.28]: box("SteelLeg", (x,y,0.41), (0.065,0.065,0.82))
box("LowerShelf", (0,0,0.21), (1.25,0.63,0.035), "Wood")
box("Lightbox", (-0.18,-0.05,0.905), (0.63,0.49,0.04), "Lightbox")
box("Paper", (-0.18,-0.05,0.932), (0.44,0.32,0.008), "Paper", 0)
art_surface((-0.18,-0.05,0.937),0.44,0.32)
for x in [-0.49,0.13]: box("RollerRail", (x,-0.02,0.96), (0.025,0.5,0.025))
cylinder("Roller", (-0.18,0.18,1.02),0.045,0.57,(0,math.pi/2,0),"Ink")
box("RollerHandle", (-0.18,0.18,1.115), (0.31,0.05,0.03), "Accent")
box("PreparationTray", (0.43,0.19,0.923), (0.32,0.23,0.035), "Steel")
for x in [0.36,0.50]: cylinder("CalibrationDial", (x,0.18,0.953),0.035,0.025,(0,0,0),"Accent")
box("CutterBase", (0.43,-0.18,0.919), (0.35,0.30,0.03), "Paper")
box("CutterArm", (0.58,-0.20,0.959), (0.018,0.10,0.006), "Steel")
box("CutterGrip", (0.58,-0.28,0.969), (0.04,0.09,0.03), "Accent")
box("InkCartridge", (-0.44,0.15,0.27),(0.18,0.17,0.10),"Accent")
export("Bench")

start()
box("CabinetBack", (0,0.25,0.68),(0.72,0.06,1.30))
for x in [-0.35,0.35]: box("CabinetSide", (x,0,0.68),(0.035,0.55,1.30))
for z in [0.055,1.315]: box("CabinetCap", (0,0,z),(0.72,0.55,0.04))
for z in [0.25,0.50,0.75,1.0]:
    box("DryingTray", (0,0,z),(0.64,0.47,0.026),"Paper")
    box("TrayLip", (0,-0.24,z+0.022),(0.65,0.02,0.07),"Accent")
for x in [-0.29,0.29]: box("WindowFrame", (x,-0.27,0.68),(0.032,0.025,1.16),"Accent")
for z in [0.12,1.24]: box("WindowFrame", (0,-0.27,z),(0.60,0.025,0.032),"Accent")
box("PrintedSheet", (0,0,0.79),(0.44,0.32,0.004),"Paper",0)
art_surface((0,0,0.793),0.44,0.32)
box("StatusLamp",(0.22,-0.30,1.28),(0.13,0.018,0.025),"Accent")
box("CabinetHandle",(0.32,-0.31,0.66),(0.023,0.06,0.22),"Ink")
export("Cabinet")

start()
box("PrismBrick", (0,0,0.022),(0.096,0.064,0.038),"Prism",0.002)
for x in [-0.032,0,0.032]:
    for y in [-0.016,0.016]: cylinder("BrickStud",(x,y,0.045),0.011,0.008,(0,0,0),"Prism")
export("Parcel")

for name,w,d in [("Sheet",0.44,0.32),("Tab",0.065,0.055),("Packet",0.15,0.11)]:
    start()
    box("PaperBody",(0,0,0.004),(w,d,0.008),"Paper",0.001)
    art_surface((0,0,0.009),w*0.96,d*0.96)
    export(name)

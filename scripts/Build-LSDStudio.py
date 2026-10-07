"""Original Off Register workshop art, authored in metres for Unity.

blender --background --factory-startup --python-exit-code 1 --python scripts/Build-LSDStudio.py
Optional positional names after -- select a subset: Bench Cabinet Parcel Packet Sheet Tab.
No external textures, fonts, downloaded meshes or game assets are used.
Runtime contracts are tested by Check-LSDStudio.py. See docs/lsd-art-polish.md.
"""
import bpy
import math
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "assets" / "LSDStudio"
OUTPUT = ROOT / "Assets" / "Models"
SOURCES = ROOT / "Blender"
OUTPUT.mkdir(parents=True, exist_ok=True)
SOURCES.mkdir(parents=True, exist_ok=True)
ONLY = set(sys.argv[sys.argv.index("--")+1:]) if "--" in sys.argv else set()
NAMES = {"Bench", "Cabinet", "Parcel", "Packet", "Sheet", "Tab"}
if ONLY - NAMES:
    raise ValueError("Unknown model(s): " + ", ".join(sorted(ONLY - NAMES)))

# Keep in sync with Assets/Editor/BuildStudio.cs. Runtime uses opaque URP/Unlit;
# silhouette, seams and color blocking must carry the design without PBR effects.
PALETTE = {
    "Steel": (0.25, 0.30, 0.32, 1), "Wood": (0.37, 0.29, 0.20, 1),
    "Paper": (0.91, 0.87, 0.74, 1), "Ink": (0.055, 0.070, 0.080, 1),
    "Accent": (0.19, 0.40, 0.47, 1), "Lightbox": (0.67, 0.82, 0.76, 1),
    "Glass": (0.13, 0.24, 0.26, 1), "Artwork": (1, 1, 1, 1),
    "Enamel": (0.62, 0.66, 0.63, 1), "Edge": (0.77, 0.80, 0.77, 1),
    "Kraft": (0.61, 0.39, 0.16, 1), "KraftEdge": (0.74, 0.52, 0.25, 1),
    "Label": (0.85, 0.80, 0.65, 1), "Warning": (0.87, 0.55, 0.18, 1),
}


def material(name):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.diffuse_color = PALETTE[name]
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = PALETTE[name]
    bsdf.inputs["Roughness"].default_value = 0.78
    bsdf.inputs["Metallic"].default_value = 0
    return mat


def box(name, location, size, mat="Steel", bevel=0.003):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(material(mat))
    if bevel:
        mod = obj.modifiers.new("Manufactured edge", "BEVEL")
        mod.width = min(bevel, min(size) * 0.22)
        mod.segments = 1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj


def mesh(name, vertices, faces, mat):
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    data.materials.append(material(mat))
    return obj


def cylinder(name, location, radius, depth, rotation=(0,0,0), mat="Steel", sides=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=sides, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(material(mat))
    return obj


def join_into(owner, parts):
    """One renderer is essential for animated tools and isolated supply views."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in [owner] + parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = owner
    bpy.ops.object.join()
    return owner


def line(name, a, b, width, mat, depth=None):
    from mathutils import Vector
    a, b = Vector(a), Vector(b)
    obj = box(name, (a+b)/2, (width, (b-a).length, depth or width), mat, 0)
    obj.rotation_euler = (b-a).to_track_quat('Y', 'Z').to_euler()
    return obj


def art_surface(location, width, depth, name="ArtworkSurface"):
    x,y,z = location
    obj = mesh(name, [(x-width/2,y-depth/2,z),(x+width/2,y-depth/2,z),
                      (x+width/2,y+depth/2,z),(x-width/2,y+depth/2,z)], [(0,1,2,3)], "Artwork")
    obj.data.uv_layers.new()
    for loop, uv in zip(obj.data.uv_layers.active.data, [(0,0),(1,0),(1,1),(0,1)]):
        loop.uv = uv
    return obj


def screw(name, x, y, z, front=False):
    return cylinder(name, (x,y,z), .005, .002,
                    (math.pi/2,0,0) if front else (0,0,0), "Ink", 8)


def start():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1


def export(name):
    # Batch static decoration into one renderer. Independently animated meshes,
    # artwork/toggle surfaces and isolated supplies must remain separate.
    keep = {
        "Bench": {"WorkbenchTop", "Paper", "Roller", "RollerHandle", "CutterBase",
                  "CutterArm", "CutterGrip", "InkCartridge", "ArtworkSurface"},
        "Cabinet": {"PrintedSheet", "StatusLamp", "ArtworkSurface"},
        "Parcel": {"WrappedParcel", "ParcelBand", "ArtworkSurface"},
        "Packet": {"PaperBody", "ArtworkSurface"},
        "Sheet": {"PaperBody", "ArtworkSurface"},
        "Tab": {"PaperBody", "ArtworkSurface"},
    }[name]
    static = [obj for obj in bpy.context.scene.objects if obj.type == "MESH" and obj.name not in keep]
    if static:
        combined = join_into(static[0], static[1:])
        combined.name = "StaticDetails"
    # Flat shading is deliberate; a small number of chamfer planes reads at game distance.
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH":
            for poly in obj.data.polygons:
                poly.use_smooth = False
            if not obj.data.uv_layers:
                bpy.context.view_layer.objects.active = obj
                bpy.ops.object.select_all(action="DESELECT")
                obj.select_set(True)
                bpy.ops.object.mode_set(mode='EDIT')
                bpy.ops.mesh.select_all(action='SELECT')
                bpy.ops.uv.smart_project(island_margin=0.02)
                bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action="SELECT")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCES / (name + ".blend")), compress=True)
    bpy.ops.export_scene.fbx(filepath=str(OUTPUT / (name + ".fbx")), use_selection=True,
        object_types={"MESH"}, apply_unit_scale=True, bake_anim=False, add_leaf_bones=False,
        axis_forward="-Z", axis_up="Y", path_mode="AUTO")
    print("LSD_STUDIO_MODEL " + name)


def bench():
    start()
    # Existing footprint/top plane retained; a dark perimeter and sheet-metal apron
    # replace the prototype's undifferentiated blocks.
    box("WorkbenchTop", (0,0,.85), (1.4,.75,.075), "Enamel", .006)
    box("TopEdgeFront", (0,-.378,.85), (1.38,.004,.045), "Edge", .001)
    box("FrontApron", (0,-.325,.785), (1.25,.045,.06), "Steel")
    box("RearApron", (0,.325,.785), (1.25,.045,.06), "Steel")
    for x in [-.60,.60]:
        for y in [-.28,.28]:
            box("SteelLeg", (x,y,.41), (.065,.065,.82))
            box("RubberFoot", (x,y,.016), (.073,.073,.032), "Ink", .004)
        box("SideBrace", (x,0,.24), (.055,.60,.04), "Steel")
    box("LowerShelf", (0,0,.21), (1.25,.63,.035), "Enamel")
    box("ShelfFrontLip", (0,-.312,.237), (1.23,.014,.025), "Steel", .001)
    box("Backstop", (0,.352,.965), (1.34,.028,.15), "Steel")
    box("BackstopFace", (0,.334,.965), (1.29,.006,.105), "Enamel", .001)
    for x in [-.63,.63]:
        screw("BackstopFastener",x,.327,.97,True)
    # Lightbox and sheet keep their exact runtime coordinate plane.
    box("Lightbox", (-.18,-.05,.905), (.63,.49,.04), "Lightbox")
    for x in [-.493,.133]:
        box("LightboxEdge",(x,-.05,.924),(.008,.488,.008),"Steel",.001)
    for y in [-.292,.192]:
        box("LightboxEdge",(-.18,y,.924),(.62,.008,.008),"Steel",.001)
    box("Paper", (-.18,-.05,.932), (.44,.32,.008), "Paper", 0)
    art_surface((-.18,-.05,.937),.44,.32)
    for x in [-.49,.13]:
        box("RollerRail",(x,-.02,.96),(.025,.5,.025),"Edge")
        for y in [-.255,.215]: screw("RailFixing",x,y,.974)
    roller=cylinder("Roller",(-.18,.18,1.02),.045,.57,(0,math.pi/2,0),"Ink",20)
    collars=[cylinder("RollerCollar",(x,.18,1.02),.044,.012,(0,math.pi/2,0),"Edge") for x in [-.455,.095]]
    join_into(roller,collars)
    handle=box("RollerHandle",(-.18,.18,1.115),(.31,.05,.03),"Accent",.006)
    supports=[box("HandleStem",(x,.18,1.08),(.018,.018,.055),"Steel") for x in [-.30,-.06]]
    join_into(handle,supports)
    # Dial controls stay clear of the lightbox and cutter workspace.
    box("PreparationTray",(.43,.19,.923),(.32,.23,.035),"Steel")
    box("ControlFace",(.43,.19,.942),(.305,.212,.004),"Enamel",.001)
    for x in [.36,.50]:
        cylinder("CalibrationDial",(x,.18,.953),.035,.025,mat="Ink",sides=16)
        box("DialIndex",(x,.164,.967),(.006,.018,.003),"Edge",0)
        for dx in [-.04,.04]: box("DialScale",(x+dx,.18,.945),(.004,.016,.002),"Ink",0)
    box("ControlBadge",(.43,.27,.946),(.12,.026,.002),"Accent",0)
    box("CutterBase",(.43,-.18,.919),(.35,.30,.03),"Paper")
    # All grooves are outside the active blade assembly; blade and grip are each
    # single meshes so the existing eleven-line animation moves their details.
    for x in [.29,.32,.35,.38,.41,.44,.47,.50,.53,.56]:
        box("CutMatGuide",(x,-.18,.935),(.0009,.27,.0005),"KraftEdge",0)
    # Tapered utility blade, exact original bounds: .018 x .100 x .006.
    cx,cy,cz=.58,-.20,.959
    outline=[(-.009,-.050),(.009,-.050),(.009,.034),(-.009,.050)]
    vertices=[(x,y,z) for z in [-.003,.003] for x,y in outline]
    blade=mesh("CutterArm",vertices,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],"Steel")
    blade.location=(cx,cy,cz)
    blade.data.materials.append(material("Edge"));blade.data.polygons[4].material_index=1
    grip=box("CutterGrip",(.58,-.28,.969),(.04,.09,.03),"Accent",.004)
    # Recess the top under ribs/pins to avoid coplanar z-fighting; details
    # retain the original complete tool bounds.
    for vertex in grip.data.vertices:
        if vertex.co.z > 0: vertex.co.z -= .0005
    details=[]
    for y in [-.305,-.292,-.279,-.266]:
        details.append(box("GripRib",(.58,y,.9835),(.033,.004,.001),"Ink",0))
    for y in [-.314,-.245]:
        details.append(cylinder("GripPin",(.58,y,.9835),.003,.001,mat="Edge",sides=8))
    join_into(grip,details)
    # Storage rendering selects precisely InkCartridge, so join its cap/label.
    cartridge=box("InkCartridge",(-.44,.15,.27),(.18,.17,.10),"Accent",.004)
    detail=[box("CartridgeLabel",(-.44,.064,.274),(.12,.002,.055),"Label",.001),
            box("CartridgeLid",(-.44,.15,.321),(.176,.166,.002),"Ink",0),
            box("CartridgeStripe",(-.44,.0625,.273),(.09,.0005,.007),"Accent",0)]
    join_into(cartridge,detail)
    box("StudioPlaque",(-.46,-.351,.785),(.20,.009,.035),"Accent",.001)
    for x in [-.64,.64]: screw("ApronFastener",x,-.35,.788,True)
    export("Bench")


def cabinet():
    start()
    box("CabinetBack",(0,.25,.68),(.72,.06,1.30),"Enamel")
    for x in [-.35,.35]:
        box("CabinetSide",(x,0,.68),(.035,.55,1.30),"Enamel")
        box("EdgeFold",(x,-.276,.68),(.037,.016,1.26),"Steel",.001)
    for z in [.055,1.315]: box("CabinetCap",(0,0,z),(.72,.55,.04),"Accent")
    box("KickPlate",(0,-.268,.081),(.67,.021,.068),"Ink",.002)
    box("CabinetRoofLip",(0,-.270,1.291),(.68,.018,.028),"Steel",.002)
    for x in [-.31,.31]:
        for y in [-.22,.22]: box("CabinetFoot",(x,y,.018),(.08,.08,.036),"Ink",.003)
    for z in [.25,.50,.75,1.0]:
        box("DryingTray",(0,0,z),(.64,.47,.026),"Enamel")
        box("TrayLip",(0,-.24,z+.022),(.65,.02,.07),"Accent")
        box("TrayPull",(0,-.254,z+.029),(.16,.007,.018),"Ink",.002)
        for x in [-.30,.30]:
            box("TrayRunner",(x,.015,z-.025),(.02,.43,.02),"Steel",.002)
        # Fine vents support readability without covering the artwork shelf.
        for x in [-.23,-.17,.17,.23]:
            box("TrayVent",(x,-.251,z+.021),(.028,.001,.006),"Ink",0)
    for x in [-.29,.29]: box("WindowFrame",(x,-.27,.68),(.032,.025,1.16),"Steel")
    for z in [.12,1.24]: box("WindowFrame",(0,-.27,z),(.60,.025,.032),"Steel")
    # Keep the open front: runtime Unlit cannot reproduce transparent glazing.
    box("PrintedSheet",(0,0,.79),(.44,.32,.004),"Paper",0)
    art_surface((0,0,.793),.44,.32)
    box("StatusLamp",(.22,-.30,1.28),(.13,.018,.025),"Accent",.003)
    box("LampBezel",(.22,-.287,1.28),(.15,.009,.036),"Ink",.003)
    handle=box("CabinetHandle",(.32,-.31,.66),(.023,.06,.22),"Ink",.004)
    for z in [.55,.77]: box("HandleMount",(.32,-.281,z),(.035,.032,.033),"Steel",.003)
    for z in [.20,.68,1.17]: box("CabinetHinge",(-.32,-.29,z),(.026,.018,.045),"Edge",.002)
    for z in [1.30,.07]:
        for x in [-.32,.32]: screw("CabinetFastener",x,-.281,z,True)
    box("CabinetBadge",(-.16,-.284,1.28),(.20,.004,.030),"Label",.001)
    for x in [-.22,-.20,-.18]: box("BadgeMark",(x,-.287,1.28),(.008,.001,.017),"Accent",0)
    export("Cabinet")


def parcel():
    start()
    box("WrappedParcel",(0,0,.085),(.23,.16,.17),"Kraft",.002)
    # Same top band and artwork plane; the formerly mint block is now recognisable
    # kraft packaging with wrap-around slate tape and folded end panels.
    box("ParcelBand",(0,0,.173),(.055,.164,.006),"Accent",0)
    for y in [-.0805,.0805]: box("TapeEnd",(0,y,.085),(.055,.001,.17),"Accent",0)
    for x in [-.1149,.1149]:
        line("FoldSeam",(x,-.075,.009),(x,0,.041),.001,"KraftEdge")
        line("FoldSeam",(x,.075,.009),(x,0,.041),.001,"KraftEdge")
        line("FoldSeam",(x,0,.041),(x,0,.164),.001,"KraftEdge")
    box("PaperLabel",(.055,0,.1715),(.071,.075,.003),"Label",.001)
    art_surface((.055,0,.177),.06,.065)
    # Restrained handling bars, original geometric marks rather than copied logos.
    for x in [-.085,-.075,-.060]:
        box("HandlingMark",(x,-.0808,.035),(.004,.0008,.016),"Ink",0)
    export("Parcel")


def paper_prop(name,w,d):
    start()
    body=box("PaperBody",(0,0,.004),(w,d,.008),"Paper",.001)
    # The upper face and UV plane remain unobstructed for player-created artwork.
    if name=="Packet":
        details=[]
        # Recess the body underside inside its original bounds so the flap and
        # seams are visible, rather than buried inside the paper block.
        for vertex in body.data.vertices:
            if vertex.co.z < 0: vertex.co.z += .0006
        z=.00025
        # Envelope back has an actual triangular folded flap and embossed seam.
        flap=mesh("EnvelopeFlap",[(-w*.46,d*.46,z),(w*.46,d*.46,z),(0,-d*.12,z)],[(0,2,1)],"Label")
        details.append(flap)
        for a,b in [((-w*.46,d*.46,z),(0,-d*.12,z)),((w*.46,d*.46,z),(0,-d*.12,z)),
                    ((-w*.46,-d*.46,z),(0,-d*.12,z)),((w*.46,-d*.46,z),(0,-d*.12,z))]:
            details.append(line("EnvelopeSeam",(a[0],a[1],.00015),(b[0],b[1],.00015),.0007,"KraftEdge",.0003))
        # No detail sticks beyond original PaperBody bounds; supply isolation still works.
        join_into(body,details)
    art_surface((0,0,.009),w*.96,d*.96)
    export(name)


for name, build in [("Bench",bench),("Cabinet",cabinet),("Parcel",parcel),
                    ("Sheet",lambda:paper_prop("Sheet",.44,.32)),
                    ("Tab",lambda:paper_prop("Tab",.065,.055)),
                    ("Packet",lambda:paper_prop("Packet",.15,.11))]:
    if not ONLY or name in ONLY:
        build()

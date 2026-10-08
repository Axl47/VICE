"""Read-only Blender/FBX runtime-contract checks for the Off Register props.

Run from any directory:
  blender --background --python-exit-code 1 --python scripts/Check-LSDStudio.py \
    -- --report /tmp/lsd-studio-check.json

The script opens each source and imports its exported FBX into an empty scene.
It never saves a .blend, exports/replaces a model, or edits a Unity asset.
The fixed measurements below come from the pre-polish c0c78e9 authored assets,
not the generator under test. FBX import is a Blender round-trip check, not a
substitute for a Unity prefab/bundle rebuild or the native gameplay diagnostic.
"""
import argparse
import ast
import hashlib
import json
import math
import os
import re
import sys
from pathlib import Path

import bpy
from mathutils import Vector


REPO = Path(__file__).resolve().parents[1]
MODELS = ("Bench", "Cabinet", "Packet", "Parcel", "Sheet", "Tab")
TOLERANCE = 0.00005  # 0.05 mm; permits FBX floating-point conversion noise.
REQUIRED = {
    "Bench": ("WorkbenchTop", "Paper", "Roller", "RollerHandle", "CutterBase",
              "CutterArm", "CutterGrip", "InkCartridge", "ArtworkSurface"),
    "Cabinet": ("PrintedSheet", "StatusLamp", "ArtworkSurface", "CabinetDoor", "CabinetHandle",
                "CabinetHinges", "ActiveDryingTray", "ActiveTrayLip"),
    "Parcel": ("WrappedParcel", "ParcelBand", "ArtworkSurface"),
    "Packet": ("PaperBody", "ArtworkSurface"),
    "Sheet": ("PaperBody", "ArtworkSurface"),
    "Tab": ("PaperBody", "ArtworkSurface"),
}
# Blender coordinates: metres, Z up. FBX -Z forward/Y up imports back to these.
ARTWORK = {
    "Bench": ((-0.18, -0.05, 0.937), (0.44, 0.32)),
    "Cabinet": ((0.0, 0.0, 0.793), (0.44, 0.32)),
    "Parcel": ((0.055, 0.0, 0.177), (0.06, 0.065)),
    "Sheet": ((0.0, 0.0, 0.009), (0.4224, 0.3072)),
    "Tab": ((0.0, 0.0, 0.009), (0.0624, 0.0528)),
    "Packet": ((0.0, 0.0, 0.009), (0.144, 0.1056)),
}
MOVING = ("Roller", "RollerHandle", "CutterArm", "CutterGrip")
# World-space mesh-bounds centre/size, not the Blender object origin. Runtime
# computes roller centring and knife endpoints from the actual mesh bounds.
BENCH_PARTS = {
    "Roller": ((-0.18, 0.18, 1.02), (0.57, 0.09, 0.09)),
    "CutterArm": ((0.58, -0.20, 0.959), (0.018, 0.10, 0.006)),
    "CutterGrip": ((0.58, -0.28, 0.969), (0.04, 0.09, 0.03)),
    "CutterBase": ((0.43, -0.18, 0.919), (0.35, 0.30, 0.03)),
    "Paper": ((-0.18, -0.05, 0.932), (0.44, 0.32, 0.008)),
}
PAPER_SIZE = {"Sheet": (0.44, 0.32, 0.008),
              "Tab": (0.065, 0.055, 0.008),
              "Packet": (0.15, 0.11, 0.008)}


def vector_list(value):
    return [round(float(v), 7) for v in value]


def box(points):
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return low, high


def world_box(obj):
    return box([obj.matrix_world @ Vector(corner) for corner in obj.bound_box])


def centre_size(obj):
    low, high = world_box(obj)
    return (low + high) * 0.5, high - low


def finite(values):
    return all(math.isfinite(float(value)) for value in values)


def stable_number(value):
    value = round(float(value), 6)
    return 0.0 if value == 0 else value


def semantic_digest(objects):
    """Stable authored content fingerprint, excluding paths/times/file headers.

    Each representation is compared with its own previous run. Source/FBX
    hashes need not agree because importers can legitimately split topology.
    Coordinates are quantized to a micrometre, far below gameplay tolerances.
    """
    content = []
    for name, obj in sorted(objects.items()):
        mesh = obj.data
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        points = [tuple(stable_number(v) for v in obj.matrix_world @ vertex.co)
                  for vertex in mesh.vertices]
        faces = []
        for polygon in mesh.polygons:
            material = (mesh.materials[polygon.material_index].name
                        if polygon.material_index < len(mesh.materials)
                        and mesh.materials[polygon.material_index] else None)
            normal = tuple(stable_number(v) for v in (normal_matrix @ polygon.normal).normalized())
            faces.append((sorted(points[i] for i in polygon.vertices), normal, material))
        uv_layers = []
        for layer in mesh.uv_layers:
            uv_layers.append(sorted((points[loop.vertex_index],
                                     tuple(stable_number(v) for v in layer.data[loop.index].uv))
                                    for loop in mesh.loops))
        content.append({"name": name, "parent": obj.parent.name if obj.parent else None,
                        "transform": [[stable_number(v) for v in row] for row in obj.matrix_world],
                        "vertices": sorted(points), "faces": sorted(faces, key=str),
                        "uv_layers": uv_layers,
                        "materials": sorted((mat.name, tuple(stable_number(v) for v in mat.diffuse_color))
                                            for mat in mesh.materials if mat)})
    return hashlib.sha256(json.dumps(content, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=REPO,
                        help="Repository root containing assets/LSDStudio")
    parser.add_argument("--report", type=Path, help="Optional JSON report path")
    parser.add_argument("--max-triangles", type=int, default=50000,
                        help="Broad safety budget per model, not exact topology")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:]
                             if "--" in sys.argv else [])
    root = args.root.resolve()
    studio = root / "assets/LSDStudio"
    report = {"baseline": "c0c78e9658cdd638f23fb04e07f2111e35e3ccf5",
              "blender": bpy.app.version_string, "units": "metres, Blender Z-up",
              "errors": [], "warnings": [], "models": {},
              "skipped": ["Unity import, actual prefab contents and Windows64 bundle rebuild",
                          "Live game rendering, furniture colliders and native packaging physics",
                          "In-game sheet/cutter/camera and save-load diagnostics"]}

    def check(condition, context, message):
        if not condition:
            report["errors"].append(f"{context}: {message}")
        return bool(condition)

    def close(actual, expected, context, message, tolerance=TOLERANCE):
        return check((Vector(actual) - Vector(expected)).length <= tolerance,
                     context, f"{message}: got {vector_list(actual)}, expected {list(expected)}")

    # New named colours are supported if the Unity material lookup supports
    # them too. Artwork intentionally falls through to white before runtime.
    builder = studio / "Assets/Editor/BuildStudio.cs"
    supported = {"Artwork"}
    unity_colours = {}
    authored_colours = {}
    builder_text = ""
    if builder.is_file():
        builder_text = builder.read_text()
        supported.update(re.findall(r'"([^"\n]+)"\s*=>', builder_text))
        for name, components in re.findall(r'"([^"\n]+)"\s*=>\s*new(?:\s+Color)?\s*\(([^)]+)\)', builder_text):
            try:
                colour = tuple(float(value.strip().rstrip("fF")) for value in components.split(","))
                unity_colours[name] = colour + (1.0,) if len(colour) == 3 else colour
            except ValueError:
                check(False, "material pipeline", f"Cannot parse Unity colour {name}")
        if re.search(r'_\s*=>\s*Color\.white', builder_text):
            unity_colours["Artwork"] = (1.0, 1.0, 1.0, 1.0)
    check(len(supported) > 1, "material pipeline", "Cannot read Unity's named-colour lookup")
    generator = root / "scripts/Build-LSDStudio.py"
    if generator.is_file():
        try:
            for node in ast.parse(generator.read_text()).body:
                if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name)
                        and target.id in ("PALETTE", "palette") for target in node.targets):
                    authored_colours = ast.literal_eval(node.value)
                    break
        except (SyntaxError, ValueError) as error:
            check(False, "material pipeline", f"Cannot parse authored palette: {error}")
    check(bool(authored_colours), "material pipeline", "Generator has no readable literal PALETTE")
    for name, colour in authored_colours.items():
        if check(name in unity_colours, "material pipeline", f"Unity lacks authored palette colour {name}"):
            check(len(colour) == len(unity_colours[name])
                  and all(abs(a - b) < 1e-6 for a, b in zip(colour, unity_colours[name])),
                  "material pipeline", f"{name} differs between Blender PALETTE and Unity colour lookup")
    report["palette_parity"] = {"authored_colours": authored_colours, "unity_colours": unity_colours}
    report["supported_materials"] = sorted(supported)

    def validate_artwork(model, obj, context):
        centre, size = ARTWORK[model]
        mesh = obj.data
        uv_layer = mesh.uv_layers.active
        if not check(uv_layer is not None, context, "ArtworkSurface lacks active UV0"):
            return
        check(mesh.uv_layers.active_index == 0, context, "Artwork UVs must be UV0, not a secondary map")
        check(len(mesh.polygons) > 0, context, "ArtworkSurface has no faces")
        corners = {}
        uv_values = {}
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        for polygon in mesh.polygons:
            normal = (normal_matrix @ polygon.normal).normalized()
            check(normal.z > 0.99999, context, "Artwork face normal must point upward, without curl or bevel")
            for index in polygon.loop_indices:
                loop = mesh.loops[index]
                point = obj.matrix_world @ mesh.vertices[loop.vertex_index].co
                uv = uv_layer.data[index].uv
                if not check(finite(uv), context, "Artwork UV has NaN/Inf"):
                    continue
                check(-1e-6 <= uv.x <= 1.000001 and -1e-6 <= uv.y <= 1.000001,
                      context, "Artwork UV is outside 0..1")
                expected = (centre[0] + (uv.x - 0.5) * size[0],
                            centre[1] + (uv.y - 0.5) * size[1], centre[2])
                close(point, expected, context, "Artwork UV/position contract changed")
                # SheetView chooses UV corners and normals[0]. Every vertex
                # must have the same affine mapping; no mirrored UV islands.
                key = loop.vertex_index
                if key in uv_values:
                    check((uv_values[key] - uv).length < 1e-6,
                          context, "Artwork has a split/seamed per-vertex UV")
                uv_values[key] = uv.copy()
                for target in ((0, 0), (1, 0), (0, 1), (1, 1)):
                    if (uv - Vector(target)).length < 1e-6:
                        corners[target] = point
        check(len(corners) == 4, context, "Artwork must expose all four exact UV corners")
        check(len(uv_values) == len(mesh.vertices), context, "Artwork has unused/non-UV vertices")
        for vertex in mesh.vertices:
            normal = (normal_matrix @ vertex.normal).normalized()
            check(normal.z > 0.99999, context, "Artwork vertex normal is not upward")
        if len(corners) == 4:
            x = corners[(1, 0)] - corners[(0, 0)]
            y = corners[(0, 1)] - corners[(0, 0)]
            check(abs(x.normalized().dot(y.normalized())) < 1e-5,
                  context, "SheetView requires orthogonal UV axes")

    def inspect(model, kind):
        context = f"{model}/{kind}"
        objects = {obj.name: obj for obj in bpy.context.scene.objects if obj.type == "MESH"}
        if not check(bool(objects), context, "No meshes"):
            return None
        for name in REQUIRED[model]:
            check(name in objects, context, f"Required exact mesh name missing: {name}")
        art = [obj for name, obj in objects.items() if name.startswith("ArtworkSurface")]
        check(len(art) == 1, context,
              "Keep exactly one ArtworkSurface; runtime selects the first matching mesh")
        if model == "Parcel":
            check(not any(name.startswith("BrickStud") for name in objects), context,
                  "Parcel diagnostic explicitly rejects BrickStud* meshes")
        total_triangles = 0
        materials = set()
        snapshots = {}
        all_points = []
        for name, obj in objects.items():
            local_context = f"{context}/{name}"
            mesh = obj.data
            check(len(mesh.vertices) > 0 and len(mesh.polygons) > 0, local_context, "Empty mesh")
            check(finite(value for row in obj.matrix_world for value in row), local_context,
                  "Transform contains NaN/Inf")
            check(obj.matrix_world.to_3x3().determinant() > 0, local_context,
                  "Reflected/zero transform can invert gameplay normals")
            if not check(all(finite(vertex.co) for vertex in mesh.vertices), local_context,
                         "Mesh has NaN/Inf coordinates"):
                continue
            # Nonuniform parent transforms/shear make the runtime's normal
            # and original-part calculations disagree with actual geometry.
            axes = [obj.matrix_world.to_3x3().col[i] for i in range(3)]
            check(all(abs(axes[i].normalized().dot(axes[j].normalized())) < 1e-5
                      for i in range(3) for j in range(i + 1, 3)), local_context,
                  "Sheared transform is unsafe for runtime plane/animation math")
            check(not any(mod.show_viewport or mod.show_render for mod in obj.modifiers),
                  local_context, "Apply geometry modifiers before saving/exporting for deterministic bounds")
            check(not obj.hide_render, local_context,
                  "Hidden source mesh would still affect runtime imported bounds/colliders")
            for material in mesh.materials:
                if material is not None:
                    materials.add(material.name)
                    check(material.name in supported, local_context,
                          f"Material {material.name!r} is not a Unity named colour (would turn white)")
                    if material.name in authored_colours:
                        check(all(abs(a - b) < 1e-5 for a, b in zip(material.diffuse_color,
                                                                 authored_colours[material.name])),
                              local_context, f"Material {material.name} colour differs from authored palette")
            check(len(mesh.materials) > 0, local_context, "No authored material")
            mesh.calc_loop_triangles()
            total_triangles += len(mesh.loop_triangles)
            material_surfaces = {}
            for triangle in mesh.loop_triangles:
                material = (mesh.materials[triangle.material_index].name
                            if triangle.material_index < len(mesh.materials)
                            and mesh.materials[triangle.material_index] else "<missing>")
                a, b, c = (obj.matrix_world @ mesh.vertices[i].co for i in triangle.vertices)
                area = (b - a).cross(c - a).length * 0.5
                values = material_surfaces.setdefault(material, {"triangles": 0, "area": 0.0})
                values["triangles"] += 1
                values["area"] += area
            low, high = world_box(obj)
            all_points.extend([low, high])
            snapshots[name] = {"min": vector_list(low), "max": vector_list(high),
                               "triangles": len(mesh.loop_triangles),
                               "materials": [mat.name if mat else None for mat in mesh.materials],
                               "material_surfaces": material_surfaces}
        check(total_triangles <= args.max_triangles, context,
              f"{total_triangles} triangles exceed broad {args.max_triangles} safety budget")
        check(len(materials) <= 32, context, "More than 32 materials would create excessive submesh draws")
        if total_triangles > 15000:
            report["warnings"].append(f"{context}: {total_triangles} triangles; review repeated-prop cost")
        if not all_points:
            return None
        low, high = box(all_points)
        check(low.z >= -0.002, context, "Geometry below model floor/pivot")
        if model in ("Bench", "Cabinet"):
            half_width = 0.75 if model == "Bench" else 0.5
            check(low.x >= -half_width - TOLERANCE and high.x <= half_width + TOLERANCE
                  and low.y >= -0.5 - TOLERANCE and high.y <= 0.5 + TOLERANCE,
                  context, "Geometry exceeds fixed furniture footprint")
            check(0.8 <= high.z <= 1.4, context, "Furniture height escapes tested placement/camera range")
            check(low.z <= 0.055, context, "Furniture floats above the established floor/base")
            # Each quadrant needs low supporting geometry. This also accepts
            # the cabinet's original continuous plinth, rather than four feet.
            feet = [obj.matrix_world @ vertex.co for obj in objects.values()
                    for vertex in obj.data.vertices if (obj.matrix_world @ vertex.co).z <= 0.08]
            for sx, sy in ((-1, -1), (-1, 1), (1, -1), (1, 1)):
                check(any(p.x * sx > 0.2 and p.y * sy > 0.15 for p in feet), context,
                      f"Missing low supporting geometry in quadrant {sx},{sy}")
        if model in PAPER_SIZE and "PaperBody" in objects:
            centre, size = centre_size(objects["PaperBody"])
            close(centre, (0, 0, 0.004), context, "PaperBody centre/pivot envelope changed")
            close(size, PAPER_SIZE[model], context, "PaperBody source dimensions changed")
            size = PAPER_SIZE[model]
            check(low.x >= -size[0] / 2 - TOLERANCE and high.x <= size[0] / 2 + TOLERANCE
                  and low.y >= -size[1] / 2 - TOLERANCE and high.y <= size[1] / 2 + TOLERANCE
                  and high.z <= 0.010 + TOLERANCE,
                  context, "Added detail expands paper/item bounds or native tab collider")
        if model == "Parcel":
            check(low.x >= -0.125 and high.x <= 0.125 and low.y >= -0.09
                  and high.y <= 0.09 and high.z <= 0.19, context,
                  "Parcel exceeds stored/held-item envelope")
        for obj in art:
            validate_artwork(model, obj, f"{context}/{obj.name}")
        if model == "Bench":
            for name, (expected_centre, expected_size) in BENCH_PARTS.items():
                if name in objects:
                    centre, size = centre_size(objects[name])
                    close(centre, expected_centre, context, f"{name} mesh-bounds centre changed")
                    close(size, expected_size, context, f"{name} mesh bounds changed")
            if "RollerHandle" in objects:
                # Runtime only translates this part's transform. Downward
                # joined supports may expand its mesh bounds safely.
                handle = objects["RollerHandle"]
                close(handle.matrix_world.translation, (-0.18, 0.18, 1.115), context,
                      "RollerHandle rest transform changed")
                lo, hi = world_box(handle)
                check(lo.x >= -0.335 - TOLERANCE and hi.x <= -0.025 + TOLERANCE
                      and lo.y >= 0.155 - TOLERANCE and hi.y <= 0.205 + TOLERANCE
                      and lo.z >= 0.975 - TOLERANCE and hi.z <= 1.13 + TOLERANCE,
                      context, "RollerHandle detail escapes original grip/roller support envelope")
            if "InkCartridge" in objects:
                # StoredSupply recentres the complete selected mesh. A shallow
                # joined front label and 2mm raised lid are safe; separate
                # detail meshes disappear. The lid clears the body face to
                # avoid coplanar surfaces after export/static batching.
                lo, hi = world_box(objects["InkCartridge"])
                check(lo.x >= -0.53 - TOLERANCE and hi.x <= -0.35 + TOLERANCE
                      and lo.y >= 0.060 - TOLERANCE and hi.y <= 0.235 + TOLERANCE
                      and lo.z >= 0.22 - TOLERANCE and hi.z <= 0.322 + TOLERANCE,
                      context, "InkCartridge escapes body plus 5mm front label/2mm lid allowances")
            for name in MOVING:
                if name not in objects:
                    continue
                parent = objects[name].parent
                while parent is not None:
                    check(parent.name not in MOVING, context,
                          f"{name} is parented under another independently animated part")
                    parent = parent.parent
            if "CutterArm" in objects and "CutterGrip" in objects:
                arm, grip = objects["CutterArm"], objects["CutterGrip"]
                local_low, local_high = box([Vector(c) for c in arm.bound_box])
                centre = (local_low + local_high) * 0.5
                extents = (local_high - local_low) * 0.5
                axis = max(range(3), key=lambda i: extents[i])
                extent = Vector((0, 0, 0)); extent[axis] = extents[axis]
                endpoints = [arm.matrix_world @ (centre + extent), arm.matrix_world @ (centre - extent)]
                handle = centre_size(grip)[0]
                pivot = max(endpoints, key=lambda p: (p - handle).length_squared)
                close(pivot, (0.58, -0.15, 0.959), context,
                      "Runtime longest-axis/farthest-from-grip cutter pivot changed")
        if model == "Cabinet" and "PrintedSheet" in objects:
            centre, size = centre_size(objects["PrintedSheet"])
            close(centre, (0, 0, 0.79), context, "Cabinet PrintedSheet centre changed")
            close(size, (0.44, 0.32, 0.004), context, "Cabinet PrintedSheet dimensions changed")
        return {"bounds": {"min": vector_list(low), "max": vector_list(high)},
                "mesh_count": len(objects), "triangles": total_triangles,
                "materials": sorted(materials), "meshes": snapshots,
                "semantic_sha256": semantic_digest(objects)}

    for model in MODELS:
        results = report["models"][model] = {}
        for kind, path in (("blend", studio / "Blender" / (model + ".blend")),
                           ("fbx", studio / "Assets/Models" / (model + ".fbx"))):
            if not check(path.is_file() and path.stat().st_size > 0, f"{model}/{kind}",
                         f"Missing or empty file: {path}"):
                continue
            try:
                # Clears stale materials as well as objects, so importer suffix
                # names cannot contaminate the next material/name comparison.
                bpy.ops.wm.read_factory_settings(use_empty=True)
                if kind == "blend":
                    bpy.ops.wm.open_mainfile(filepath=str(path))
                    check(bpy.context.scene.unit_settings.scale_length == 1,
                          f"{model}/{kind}", "Scene unit scale must remain one metre")
                else:
                    bpy.ops.import_scene.fbx(filepath=str(path))
                bpy.context.view_layer.update()
                results[kind] = inspect(model, kind)
            except Exception as error:
                check(False, f"{model}/{kind}", f"Cannot inspect: {type(error).__name__}: {error}")
        if results.get("blend") and results.get("fbx"):
            before, after = results["blend"]["meshes"], results["fbx"]["meshes"]
            context = f"{model}/FBX round-trip"
            check(before.keys() == after.keys(), context, "Export changed mesh names/count")
            for name in before.keys() & after.keys():
                for bound in ("min", "max"):
                    close(after[name][bound], before[name][bound], context, f"{name} {bound} bounds changed")
                check(sorted(before[name]["materials"]) == sorted(after[name]["materials"]), context,
                      f"{name} materials changed")
                # FBX can reorder slots, so compare material-to-surface use,
                # not slot indices. A reordering alone is harmless in Unity.
                first = before[name]["material_surfaces"]
                second = after[name]["material_surfaces"]
                check(first.keys() == second.keys(), context, f"{name} face materials changed")
                for material in first.keys() & second.keys():
                    check(first[material]["triangles"] == second[material]["triangles"]
                          and abs(first[material]["area"] - second[material]["area"]) < 0.00001,
                          context, f"{name}/{material} material-to-surface assignments changed")
                check(before[name]["triangles"] == after[name]["triangles"], context,
                      f"{name} export changed triangulated surface count")

    digests = {model: {kind: values["semantic_sha256"] for kind, values in results.items() if values}
               for model, results in report["models"].items()}
    report["assetset_sha256"] = hashlib.sha256(json.dumps(digests, sort_keys=True).encode()).hexdigest()
    report["passed"] = not report["errors"]
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, indent=2) + "\n")
    for error in report["errors"]:
        print("LSD_STUDIO_ERROR " + error)
    for warning in report["warnings"]:
        print("LSD_STUDIO_WARNING " + warning)
    for skipped in report["skipped"]:
        print("LSD_STUDIO_SKIPPED " + skipped)
    print("LSD_STUDIO_CONTRACTS_" + ("PASSED" if report["passed"] else "FAILED")
          + f" models={len(report['models'])}, errors={len(report['errors'])}"
          + (f", report={args.report}" if args.report else ""))
    if not report["passed"]:
        # Blender otherwise may exit zero for Python exceptions unless callers
        # remembered --python-exit-code. Fail reliably for build automation.
        sys.stdout.flush(); sys.stderr.flush()
        os._exit(1)


if __name__ == "__main__":
    main()

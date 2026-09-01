import bpy
import pathlib
import sys


def arguments():
    separator = sys.argv.index("--")
    source = pathlib.Path(sys.argv[separator + 1]).resolve()
    destination = pathlib.Path(sys.argv[separator + 2]).resolve()
    target_triangles = int(sys.argv[separator + 3])
    object_names = set(sys.argv[separator + 4 :])
    return source, destination, target_triangles, object_names


source_path, destination_path, triangle_budget, included_object_names = arguments()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(source_path))

mesh_objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
if included_object_names:
    mesh_objects = [obj for obj in mesh_objects if obj.name in included_object_names]

if not mesh_objects:
    raise RuntimeError("The source FBX does not contain any requested mesh objects.")

for obj in mesh_objects:
    obj.data.calc_loop_triangles()
triangle_count = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
ratio = min(1.0, triangle_budget / triangle_count)

for obj in mesh_objects:
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new(name="Game-ready decimation", type="DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = ratio
    modifier.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)

for obj in list(bpy.context.scene.objects):
    if obj not in mesh_objects:
        bpy.data.objects.remove(obj, do_unlink=True)

destination_path.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action="DESELECT")
for obj in mesh_objects:
    obj.select_set(True)

bpy.context.view_layer.objects.active = mesh_objects[0]
bpy.ops.export_scene.fbx(
    filepath=str(destination_path),
    use_selection=True,
    apply_unit_scale=True,
    axis_forward="-Z",
    axis_up="Y",
    add_leaf_bones=False,
    bake_anim=False,
    path_mode="COPY",
    embed_textures=True,
)

for obj in mesh_objects:
    obj.data.calc_loop_triangles()
optimized_triangle_count = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
print(
    f"Optimized {triangle_count} source triangles to {optimized_triangle_count} "
    f"(budget {triangle_budget}): {destination_path}"
)

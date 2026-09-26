"""Preview renders of the wreck cars from the game camera angle and from the reference (3/4) angle.
Run:  blender -b --factory-startup --python render_wrecks.py -- <out_dir>
"""
import bpy, math, os, sys
from mathutils import Vector

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else r"D:\zombi-caaar\models\Vehicles"
SRC = r"D:\zombi-caaar\models\Vehicles"

for name in ("Car_Pickup", "Car_Sedan"):
    bpy.ops.wm.open_mainfile(filepath=os.path.join(SRC, name + ".blend"))
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 900, 640
    world = bpy.data.worlds.new("W"); sc.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.33, 0.32, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN')); sc.collection.objects.link(sun)
    sun.data.energy = 3.5; sun.rotation_euler = (math.radians(45), 0, math.radians(-40))
    bpy.ops.mesh.primitive_plane_add(size=30)
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam); sc.camera = cam
    cam.data.lens = 50
    for view, (az, el, dist) in {"game": (0, 50, 13), "ref": (-35, 35, 11)}.items():
        # game: camera looks north like in Unity (car rotated by its map yaw); ref: 3/4 view like the references
        yaw = {"Car_Pickup": 90, "Car_Sedan": 235}[name] if view == "game" else (215 if name == "Car_Sedan" else 145)
        root = bpy.data.objects[name]
        root.rotation_euler = (0, 0, math.radians(-yaw))
        a, e = math.radians(az), math.radians(el)
        target = Vector((0, 0, 0.8))
        cam.location = target + dist * Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
        cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f"{name}_{view}.png")
        bpy.ops.render.render(write_still=True)
print("RENDER OK")

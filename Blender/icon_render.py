# "Forest to Fortune" game icon: renders the game's own models (a stump with the Lumberjack's Axe
# bitten into it, a small pine, logs and stacks of gold coins on a grassy mound) with a transparent
# background. Blender/icon_compose.py then puts it on the framed icon background.
#   blender -b --factory-startup -P Blender/icon_render.py -- <out.png>
import bpy, math, os, sys
from mathutils import Vector, Euler

out = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else os.path.join(os.getcwd(), "icon_render.png")
src = os.path.join(os.path.dirname(os.path.abspath(__file__)), "Source")

for o in list(bpy.data.objects):
    bpy.data.objects.remove(o, do_unlink=True)
scene = bpy.context.scene


def append(blend, loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    """Links every mesh of a model's .blend into the scene under one empty."""
    with bpy.data.libraries.load(os.path.join(src, blend + ".blend"), link=False) as (data_from, data_to):
        data_to.objects = [n for n in data_from.objects]
    root = bpy.data.objects.new(blend, None)
    scene.collection.objects.link(root)
    for o in data_to.objects:
        if o is None or o.type != 'MESH' or o.name.startswith("COL_"):
            continue
        scene.collection.objects.link(o)
        if o.parent is None:
            o.parent = root
    root.location = loc
    root.rotation_euler = Euler(rot)
    root.scale = scale
    return root


def mat(name, color, emission=None, metallic=0.0, rough=0.6):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*color, 1)
    b.inputs["Metallic"].default_value = metallic
    b.inputs["Roughness"].default_value = rough
    if emission is not None:
        key = "Emission Color" if "Emission Color" in b.inputs else "Emission"
        b.inputs[key].default_value = (*emission, 1)
        b.inputs["Emission Strength"].default_value = 2.0
    return m


def cyl(r, h, loc, material, rot=(0, 0, 0), verts=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.data.materials.append(material)
    bpy.ops.object.shade_smooth()
    return o


gold = mat("CoinGold", (1.0, 0.8, 0.32), metallic=0.85, rough=0.22)
gold_dark = mat("CoinRim", (0.95, 0.66, 0.2), metallic=0.85, rough=0.3)
grass = mat("Mound", (0.22, 0.42, 0.16), rough=0.9)
grass_dark = mat("MoundSide", (0.33, 0.24, 0.14), rough=0.95)

# Grassy mound with an earthy side.
cyl(1.45, 0.24, (0, 0, -0.12), grass_dark, verts=48)
cyl(1.47, 0.05, (0, 0, 0.0), grass, verts=48)

# Stump (unit model: radius 1, height 1) with the axe bitten into its top.
append("Stump_Bark", scale=(0.55, 0.55, 0.45))
theta = math.radians(-125)
append("Axe_2", loc=(0.0, -0.88, 1.55), rot=(theta, 0, 0), scale=(1.9, 1.9, 1.9))

# A small pine behind, a couple of logs and the money.
append("Tree_Pine", loc=(-0.75, -0.8, 0.0), rot=(0, 0, 0.6), scale=(0.2, 0.2, 0.2))
append("LogPiece", loc=(-0.85, 0.55, 0.12), rot=(0, 0, 0.9), scale=(0.9, 0.9, 0.9))
append("LogPiece", loc=(-0.6, 0.85, 0.12), rot=(0, 0, 1.25), scale=(0.9, 0.9, 0.9))
for (x, y, n) in [(0.62, 0.55, 7), (0.9, 0.15, 4), (0.35, 0.9, 3)]:
    for i in range(n):
        z = 0.02 + i * 0.045
        cyl(0.15, 0.04, (x + math.sin(i * 1.7) * 0.012, y + math.cos(i * 2.3) * 0.012, z), gold)
        cyl(0.115, 0.042, (x + math.sin(i * 1.7) * 0.012, y + math.cos(i * 2.3) * 0.012, z), gold_dark, verts=24)
for (x, y, a) in [(0.95, 0.75, 0.5), (0.2, 0.55, -0.4)]:
    cyl(0.15, 0.04, (x, y, 0.09), gold, rot=(a, 0.3, 0))

# Camera: three-quarter view from above.
target = Vector((0.0, 0.0, 0.62))
cam_data = bpy.data.cameras.new("Cam")
cam_data.lens = 58
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
cam.location = target + Vector((1.0, 1.15, 0.8)).normalized() * 6.1
cam.rotation_euler = (target - cam.location).to_track_quat('-Z', 'Y').to_euler()
scene.camera = cam

# Lights: warm key, cool fill, rim.
def light(name, kind, energy, loc, color, size=1.0):
    ld = bpy.data.lights.new(name, kind)
    ld.energy = energy
    ld.color = color
    if kind == 'AREA':
        ld.size = size
    lo = bpy.data.objects.new(name, ld)
    lo.location = loc
    lo.rotation_euler = (target - Vector(loc)).to_track_quat('-Z', 'Y').to_euler()
    scene.collection.objects.link(lo)

light("Key", 'AREA', 260, (3.0, 1.5, 3.5), (1.0, 0.86, 0.66), 2.5)
light("Fill", 'AREA', 70, (-2.5, 3.0, 2.0), (0.7, 0.85, 1.0), 3.0)
light("Rim", 'AREA', 320, (-1.5, -3.0, 2.5), (1.0, 0.72, 0.4), 2.0)
world = bpy.data.worlds.new("World")
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.08, 0.12, 0.09, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.35
scene.world = world

for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        continue
scene.render.film_transparent = True
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.image_settings.file_format = 'PNG'
scene.render.image_settings.color_mode = 'RGBA'
for vt in ("AgX", "Filmic", "Standard"):
    try:
        scene.view_settings.view_transform = vt
        break
    except TypeError:
        continue
try:
    scene.view_settings.look = "AgX - Medium High Contrast" if scene.view_settings.view_transform == "AgX" else "None"
except TypeError:
    pass
try:
    scene.eevee.taa_render_samples = 64
except Exception:
    pass
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("ICON RENDERED", out)

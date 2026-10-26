# "A Game About Chopping Trees" model generator for Blender 4.2+/5.x (run headless):
#   blender -b --factory-startup -P ct_models.py -- <output folder> [model names...]
# Builds every 3D model in the game from code: trees, stumps, axes, machines, buildings and props.
# Writes one FBX per model to the output folder and an editable .blend per model to Blender/Source.
# Conventions:
#   * metres, Z up, pivot on the ground (trees: at the cut, the top of the stump).
#   * "front" is +Y (Unity +Z after import).
#   * material slot names (M_*) match Unity materials; the importer remaps them.
#   * COL_* objects are collision boxes (unit cubes, transform = box). Unity turns them into BoxColliders.
#   * empties (WorkPoint, SignAnchor, EffectPoint, LightPoint) become plain transforms in Unity.
import bpy, bmesh, math, sys, os
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.getcwd(), "Models")
ONLY = set(argv[1:])
os.makedirs(OUT, exist_ok=True)


# ----------------------------------------------------------------- helpers


def reset():
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    for block in (bpy.data.meshes, bpy.data.curves):
        for b in list(block):
            if b.users == 0:
                block.remove(b)


def mat(name):
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
    return m


def assign(obj, name):
    obj.data.materials.clear()
    obj.data.materials.append(mat(name))


def activate(o):
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.context.view_layer.objects.active = o


def box(name, size, loc, material, bevel=0.0, rot=(0, 0, 0), segments=1):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    o.scale = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel > 0:
        b = o.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = segments
        b.limit_method = 'ANGLE'
    assign(o, material)
    return o


def span(name, x0, x1, y0, y1, z0, z1, material, bevel=0.0):
    """Box from min/max extents (easier for architecture)."""
    return box(name, (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), material, bevel)


def cyl(name, radius, depth, loc, material, rot=(0, 0, 0), verts=16, bevel=0.0):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=radius, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    if bevel > 0:
        b = o.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 1
        b.limit_method = 'ANGLE'
    assign(o, material)
    return o


def sphere(name, radius, loc, material, segs=20, scale=(1, 1, 1)):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segs, ring_count=max(8, segs // 2), radius=radius, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for p in o.data.polygons:
        p.use_smooth = True
    assign(o, material)
    return o


def prism(name, profile, length, material, axis='X', bevel=0.0):
    """Extrudes a 2D profile [(a, b), ...] (counter-clockwise) along an axis, centred.
    axis 'X': profile is (y, z); axis 'Y': profile is (x, z)."""
    me = bpy.data.meshes.new(name)
    bm = bmesh.new()
    h = length / 2
    front, back = [], []
    for a, b in profile:
        if axis == 'X':
            front.append(bm.verts.new((-h, a, b)))
            back.append(bm.verts.new((h, a, b)))
        else:
            front.append(bm.verts.new((a, -h, b)))
            back.append(bm.verts.new((a, h, b)))
    n = len(profile)
    bm.faces.new(front[::-1] if axis == 'X' else front)
    bm.faces.new(back if axis == 'X' else back[::-1])
    for i in range(n):
        j = (i + 1) % n
        if axis == 'X':
            bm.faces.new((front[i], front[j], back[j], back[i]))
        else:
            bm.faces.new((front[j], front[i], back[i], back[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new(name, me)
    bpy.context.collection.objects.link(o)
    if bevel > 0:
        b = o.modifiers.new("Bevel", 'BEVEL')
        b.width = bevel
        b.segments = 1
        b.limit_method = 'ANGLE'
    assign(o, material)
    return o


def tube(name, a, b, r0, r1, material, verts=10):
    """Tapered cylinder from point a to point b."""
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r0, radius2=r1, depth=d.length, location=(a + b) / 2)
    o = bpy.context.active_object
    o.name = name
    o.rotation_mode = 'QUATERNION'
    o.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)
    for p in o.data.polygons:
        p.use_smooth = True
    assign(o, material)
    return o


def empty(name, loc=(0, 0, 0)):
    o = bpy.data.objects.new(name, None)
    bpy.context.collection.objects.link(o)
    o.location = loc
    o.empty_display_size = 0.05
    return o


def col(name, size, loc, rot=(0, 0, 0)):
    """Collision box: a unit cube whose object transform is the box (kept unapplied)."""
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = "COL_" + name
    o.scale = size
    o.data.materials.clear()
    return o


def col_span(name, x0, x1, y0, y1, z0, z1):
    return col(name, (x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2))


def set_origin(o, p=(0, 0, 0)):
    bpy.context.scene.cursor.location = p
    activate(o)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.context.scene.cursor.location = (0, 0, 0)


def join(objs, name, origin=(0, 0, 0)):
    """Applies modifiers and joins meshes into one object (material slots kept)."""
    objs = [o for o in objs if o is not None]
    for o in objs:
        activate(o)
        for m in list(o.modifiers):
            bpy.ops.object.modifier_apply(modifier=m.name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1:
        bpy.ops.object.join()
    o = bpy.context.active_object
    o.name = name
    o.data.name = name
    if origin is not None:
        set_origin(o, origin)
    return o


def uv_box(o, scale=1.0):
    """Cube-projected UVs in world metres for consistent texel density on tiling materials."""
    activate(o)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=1.0 / max(scale, 1e-4), correct_aspect=True)
    bpy.ops.object.mode_set(mode='OBJECT')


def parent(child, par):
    child.parent = par
    child.matrix_parent_inverse = par.matrix_world.inverted()


def export(name):
    bpy.ops.object.select_all(action='SELECT')
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    print("EXPORTED", path)


def finish(name, visual, uv=1.0, origin=(0, 0, 0)):
    """Joins the visual parts into one mesh named <name>_Mesh, UV-projects it and exports."""
    m = join(visual, name + "_Mesh", origin)
    if uv:
        uv_box(m, uv)
    export(name)
    return m


MODELS = {}


def model(fn):
    MODELS[fn.__name__] = fn
    return fn



# ================================================================= Chopping Trees models
import random

SRC = os.path.join(os.path.dirname(os.path.abspath(sys.argv[sys.argv.index("-P") + 1])), "Source")
os.makedirs(SRC, exist_ok=True)

# Viewport / FBX colours. Unity remaps every M_* slot to its own material of the same name.
COLORS = {
    "M_Bark": (0.36, 0.24, 0.15), "M_BarkDark": (0.16, 0.13, 0.11), "M_BarkBirch": (0.88, 0.86, 0.80),
    "M_BarkRed": (0.52, 0.24, 0.16), "M_BarkIron": (0.27, 0.29, 0.33), "M_BarkGold": (0.78, 0.58, 0.22),
    "M_WoodCut": (0.90, 0.74, 0.50), "M_WoodCutDark": (0.62, 0.43, 0.25),
    "M_Leaf": (0.33, 0.58, 0.24), "M_LeafDark": (0.18, 0.40, 0.20), "M_LeafPine": (0.15, 0.36, 0.24),
    "M_LeafBirch": (0.55, 0.72, 0.30), "M_LeafMaple": (0.88, 0.38, 0.16), "M_LeafAutumn": (0.95, 0.66, 0.20),
    "M_LeafIron": (0.22, 0.52, 0.55), "M_LeafGold": (1.0, 0.80, 0.30),
    "M_Wood": (0.62, 0.42, 0.24), "M_WoodDark": (0.36, 0.23, 0.14), "M_Plank": (0.80, 0.62, 0.40),
    "M_Metal": (0.55, 0.56, 0.58), "M_MetalDark": (0.20, 0.21, 0.23), "M_Steel": (0.80, 0.82, 0.85),
    "M_Rust": (0.45, 0.27, 0.17), "M_Paint": (0.95, 0.70, 0.15), "M_PaintRed": (0.78, 0.20, 0.16),
    "M_PaintGreen": (0.27, 0.50, 0.30), "M_PaintBlue": (0.20, 0.38, 0.66), "M_Rubber": (0.10, 0.10, 0.10),
    "M_Stone": (0.55, 0.55, 0.52), "M_StoneDark": (0.38, 0.38, 0.38), "M_Grass": (0.40, 0.62, 0.25),
    "M_Canvas": (0.92, 0.88, 0.78), "M_Rope": (0.72, 0.62, 0.42), "M_Gold": (1.0, 0.78, 0.25),
    "M_Glow": (1.0, 0.62, 0.20), "M_Leather": (0.40, 0.24, 0.14), "M_Sign": (0.30, 0.20, 0.12),
    "M_Roof": (0.44, 0.22, 0.18), "M_Glass": (0.60, 0.80, 0.90), "M_Snow": (0.95, 0.96, 0.98),
    "M_Berry": (0.80, 0.10, 0.18), "M_FlowerYellow": (1.0, 0.85, 0.25), "M_FlowerPurple": (0.62, 0.42, 0.85),
    "M_Dirt": (0.45, 0.34, 0.22),
}


def mat(name):  # noqa: F811 - overrides the helper to add colours
    m = bpy.data.materials.get(name)
    if m is None:
        m = bpy.data.materials.new(name)
        c = COLORS.get(name, (0.8, 0.8, 0.8))
        m.diffuse_color = (c[0], c[1], c[2], 1.0)
        try:
            m.use_nodes = True
            bsdf = m.node_tree.nodes.get("Principled BSDF")
            if bsdf:
                bsdf.inputs["Base Color"].default_value = (c[0], c[1], c[2], 1.0)
        except Exception:
            pass
    return m


def assign(obj, name):  # noqa: F811
    obj.data.materials.clear()
    obj.data.materials.append(mat(name))


def flat(o):
    for p in o.data.polygons:
        p.use_smooth = False
    return o


def jitter(o, amount, seed):
    rnd = random.Random(seed)
    for v in o.data.vertices:
        v.co.x += rnd.uniform(-amount, amount)
        v.co.y += rnd.uniform(-amount, amount)
        v.co.z += rnd.uniform(-amount, amount) * 0.7
    return o


def ico(name, r, loc, material, subdiv=1, scale=(1, 1, 1), jit=0.0, seed=0):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=r, location=loc)
    o = bpy.context.active_object
    o.name = name
    o.scale = scale
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if jit:
        jitter(o, jit * r, seed)
    assign(o, material)
    return flat(o)


def cone(name, r1, r2, depth, loc, material, verts=8, rot=(0, 0, 0), jit=0.0, seed=0):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object
    o.name = name
    if jit:
        jitter(o, jit, seed)
    assign(o, material)
    return flat(o)


def ftube(name, a, b, r0, r1, material, verts=8):
    return flat(tube(name, a, b, r0, r1, material, verts))


def fcyl(name, r, depth, loc, material, rot=(0, 0, 0), verts=12):
    return flat(cyl(name, r, depth, loc, material, rot, verts))


def disc_x(name, r, thick, loc, material, verts=16):
    """Cylinder whose axis is X (a blade or wheel facing sideways)."""
    return fcyl(name, r, thick, loc, material, rot=(0, math.radians(90), 0), verts=verts)


def log_x(name, r, length, loc, bark, verts=9):
    """A log lying along X with cut-wood end caps."""
    parts = [fcyl(name, r, length, loc, bark, rot=(0, math.radians(90), 0), verts=verts)]
    for s in (-1, 1):
        parts.append(fcyl(name + "Cap", r * 0.9, 0.02, (loc[0] + s * length / 2, loc[1], loc[2]), "M_WoodCut", rot=(0, math.radians(90), 0), verts=verts))
        parts.append(fcyl(name + "Ring", r * 0.45, 0.03, (loc[0] + s * length / 2, loc[1], loc[2]), "M_WoodCutDark", rot=(0, math.radians(90), 0), verts=verts))
    return parts


def log_y(name, r, length, loc, bark, verts=9):
    parts = [fcyl(name, r, length, loc, bark, rot=(math.radians(90), 0, 0), verts=verts)]
    for s in (-1, 1):
        parts.append(fcyl(name + "Cap", r * 0.9, 0.02, (loc[0], loc[1] + s * length / 2, loc[2]), "M_WoodCut", rot=(math.radians(90), 0, 0), verts=verts))
    return parts


def export(name):  # noqa: F811 - also keeps an editable .blend per model
    bpy.ops.object.select_all(action='SELECT')
    path = os.path.join(OUT, name + ".fbx")
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        axis_forward='-Z', axis_up='Y', object_types={'MESH', 'EMPTY'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', add_leaf_bones=False, bake_anim=False, path_mode='STRIP')
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC, name + ".blend"), copy=True)
    print("EXPORTED", path)


def finish(name, visual, uv=0, origin=(0, 0, 0)):  # noqa: F811
    # Collision boxes stay separate objects (Unity turns them into colliders).
    m = join([o for o in visual if not o.name.startswith("COL_")], name + "_Mesh", origin)
    export(name)
    return m


def part(name, objs, origin):
    """Joins objects into one named moving part with its pivot at origin."""
    return join(objs, name, origin)


# ----------------------------------------------------------------- trees
# Tree models start at the cut (z = 0 is the top of the stump); Unity stacks them on a stump.

def crown(parts, centers, material_cycle, seed):
    rnd = random.Random(seed)
    for i, (x, y, z, r) in enumerate(centers):
        m = material_cycle[i % len(material_cycle)]
        parts.append(ico("Leaves", r, (x, y, z), m, 1, (1, 1, rnd.uniform(0.75, 0.95)), 0.12, seed + i))


@model
def Tree_Pine():
    parts = [ftube("Trunk", (0, 0, 0), (0, 0, 6.8), 0.28, 0.06, "M_Bark", 7)]
    tiers = [(1.4, 2.0, 2.7), (2.8, 1.65, 2.4), (4.1, 1.25, 2.1), (5.3, 0.85, 1.8), (6.3, 0.45, 1.3)]
    for i, (z, r, h) in enumerate(tiers):
        parts.append(cone("Tier", r, 0.04, h, (0, 0, z + h / 2), "M_LeafPine", 7, (0, 0, i * 0.5), 0.08, 11 + i))
    finish("Tree_Pine", parts)


@model
def Tree_Birch():
    parts = [ftube("Trunk", (0, 0, 0), (0.18, 0.05, 7.0), 0.22, 0.09, "M_BarkBirch", 8)]
    rnd = random.Random(5)
    for i in range(12):
        z = rnd.uniform(0.3, 5.5)
        a = rnd.uniform(0, math.tau)
        r = 0.22 - z * 0.016
        parts.append(box("Mark", (0.16, 0.04, 0.05), (math.cos(a) * r + z * 0.025, math.sin(a) * r, z), "M_BarkDark", rot=(0, 0, a + math.pi / 2)))
    parts.append(ftube("Branch", (0.05, 0, 4.3), (0.9, 0.4, 5.4), 0.06, 0.03, "M_BarkBirch", 5))
    parts.append(ftube("Branch", (0.1, 0, 5.0), (-0.7, -0.3, 6.0), 0.05, 0.03, "M_BarkBirch", 5))
    crown(parts, [(0.2, 0, 6.6, 1.2), (0.9, 0.4, 5.6, 0.9), (-0.6, -0.3, 6.0, 0.95), (0.3, 0.6, 7.3, 0.8), (-0.1, -0.5, 7.1, 0.75)],
          ["M_LeafBirch", "M_Leaf"], 20)
    finish("Tree_Birch", parts)


@model
def Tree_Oak():
    parts = [ftube("Trunk", (0, 0, 0), (0, 0, 3.6), 0.45, 0.34, "M_Bark", 9)]
    for (x, y, z) in [(1.7, 0.4, 4.8), (-1.4, 0.9, 5.0), (0.2, -1.6, 4.9), (0.1, 0.3, 5.6)]:
        parts.append(ftube("Branch", (0, 0, 3.0), (x, y, z), 0.24, 0.11, "M_Bark", 6))
    crown(parts, [(0, 0, 6.2, 2.0), (1.8, 0.5, 5.4, 1.5), (-1.6, 0.9, 5.6, 1.5), (0.2, -1.8, 5.5, 1.5), (0.5, 1.4, 6.6, 1.3), (-0.8, -0.8, 6.9, 1.3)],
          ["M_Leaf", "M_LeafDark"], 30)
    finish("Tree_Oak", parts)


@model
def Tree_Maple():
    parts = [ftube("Trunk", (0, 0, 0), (0.1, 0, 4.4), 0.5, 0.33, "M_Bark", 9)]
    for (x, y, z) in [(1.9, 0.3, 5.6), (-1.7, 0.6, 5.8), (0.3, -1.9, 5.7), (-0.4, 1.7, 6.2)]:
        parts.append(ftube("Branch", (0.05, 0, 3.6), (x, y, z), 0.26, 0.12, "M_Bark", 6))
    crown(parts, [(0, 0, 7.3, 2.3), (2.0, 0.4, 6.3, 1.7), (-1.9, 0.7, 6.5, 1.7), (0.3, -2.0, 6.4, 1.6), (-0.4, 1.9, 7.0, 1.6), (0.6, 0.2, 8.6, 1.4)],
          ["M_LeafMaple", "M_LeafAutumn", "M_LeafMaple"], 40)
    finish("Tree_Maple", parts)


@model
def Tree_Redwood():
    parts = [ftube("Trunk", (0, 0, 0), (0, 0, 16.5), 0.85, 0.22, "M_BarkRed", 10)]
    rnd = random.Random(7)
    for i in range(7):
        z = 6.0 + i * 1.55
        r = 2.8 - i * 0.3
        parts.append(cone("Tier", r, 0.3, 2.0, (0, 0, z + 1.0), "M_LeafDark", 9, (0, 0, i * 0.4), 0.12, 70 + i))
    parts.append(cone("Top", 0.9, 0.02, 2.2, (0, 0, 17.0), "M_LeafPine", 7))
    for i in range(5):
        a = rnd.uniform(0, math.tau)
        z = rnd.uniform(3.0, 5.5)
        parts.append(ftube("Stub", (0, 0, z), (math.cos(a) * 1.3, math.sin(a) * 1.3, z + 0.4), 0.12, 0.05, "M_BarkRed", 5))
    finish("Tree_Redwood", parts)


@model
def Tree_Ironwood():
    pts = [(0, 0, 0), (0.25, 0.1, 2.5), (-0.1, 0.25, 5.0), (0.15, -0.05, 7.3)]
    radii = [0.75, 0.62, 0.5, 0.38]
    parts = []
    for i in range(3):
        parts.append(ftube("Trunk", pts[i], pts[i + 1], radii[i], radii[i + 1], "M_BarkIron", 8))
    for (x, y, z) in [(2.0, 0.4, 8.4), (-1.9, 0.7, 8.6), (0.3, -2.0, 8.5)]:
        parts.append(ftube("Branch", (0.1, 0, 6.6), (x, y, z), 0.28, 0.12, "M_BarkIron", 6))
    rnd = random.Random(9)
    for i, (x, y, z, r) in enumerate([(0.2, 0, 9.4, 2.4), (2.2, 0.5, 8.6, 1.7), (-2.0, 0.8, 8.8, 1.7), (0.3, -2.2, 8.7, 1.6), (0.0, 1.6, 9.9, 1.5)]):
        parts.append(ico("Leaves", r, (x, y, z), "M_LeafIron", 0, (1, 1, rnd.uniform(0.7, 0.9)), 0.1, 90 + i))
    finish("Tree_Ironwood", parts)


@model
def Tree_Goldbark():
    parts = [ftube("Trunk", (0, 0, 0), (0, 0.1, 5.0), 0.6, 0.38, "M_BarkGold", 9)]
    for (x, y, z) in [(1.8, 0.2, 6.3), (-1.6, 0.6, 6.4), (0.2, -1.8, 6.2)]:
        parts.append(ftube("Branch", (0, 0.05, 4.2), (x, y, z), 0.26, 0.11, "M_BarkGold", 6))
    crown(parts, [(0, 0, 7.6, 2.1), (1.9, 0.3, 6.8, 1.5), (-1.7, 0.7, 6.9, 1.5), (0.2, -1.9, 6.7, 1.5), (0.2, 0.4, 8.9, 1.3)], ["M_LeafGold"], 110)
    rnd = random.Random(12)
    for i in range(10):
        a = rnd.uniform(0, math.tau)
        r = rnd.uniform(1.2, 2.4)
        parts.append(ico("Fruit", 0.16, (math.cos(a) * r, math.sin(a) * r, rnd.uniform(5.6, 6.4)), "M_Glow", 0))
    finish("Tree_Goldbark", parts)


def stump(name, bark):
    """Unit stump: radius 1, height 1 (Unity scales it to the trunk), cut top and root flares."""
    parts = [fcyl("Stump", 1.0, 1.0, (0, 0, 0.5), bark, verts=10)]
    parts.append(fcyl("Cut", 0.93, 0.03, (0, 0, 1.0), "M_WoodCut", verts=10))
    parts.append(fcyl("Ring", 0.55, 0.04, (0, 0, 1.005), "M_WoodCutDark", verts=10))
    parts.append(fcyl("Core", 0.2, 0.05, (0, 0, 1.01), "M_WoodCut", verts=8))
    for i in range(5):
        a = i * math.tau / 5 + 0.3
        parts.append(box("Root", (0.75, 0.32, 0.4), (math.cos(a) * 0.95, math.sin(a) * 0.95, 0.12), bark, rot=(0, -0.35, a)))
    finish(name, parts)


@model
def Stump_Bark():
    stump("Stump_Bark", "M_Bark")


@model
def Stump_Birch():
    stump("Stump_Birch", "M_BarkBirch")


@model
def Stump_Red():
    stump("Stump_Red", "M_BarkRed")


@model
def Stump_Iron():
    stump("Stump_Iron", "M_BarkIron")


@model
def Stump_Gold():
    stump("Stump_Gold", "M_BarkGold")


@model
def CutNotch():
    """Exposed-wood band wrapped on the front (+Y) of a unit trunk; scaled per tree in Unity."""
    me = bpy.data.meshes.new("CutNotch")
    bm = bmesh.new()
    steps = 8
    rows = []
    for zz, ro in ((-0.5, 1.0), (0.5, 1.0)):
        row = []
        for i in range(steps + 1):
            a = math.radians(-65 + 130 * i / steps)
            row.append(bm.verts.new((math.sin(a) * 1.04, math.cos(a) * 1.04, zz)))
        rows.append(row)
    for i in range(steps):
        bm.faces.new((rows[0][i], rows[0][i + 1], rows[1][i + 1], rows[1][i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # Make sure the band faces outward.
    for f in bm.faces:
        c = f.calc_center_median()
        if f.normal.dot(Vector((c.x, c.y, 0))) < 0:
            f.normal_flip()
    bm.to_mesh(me)
    bm.free()
    band = bpy.data.objects.new("Band", me)
    bpy.context.collection.objects.link(band)
    assign(band, "M_WoodCut")
    gash = box("Gash", (1.1, 0.12, 0.16), (0, 1.0, 0), "M_WoodCutDark")
    finish("CutNotch", [band, gash])


# ----------------------------------------------------------------- pickups and props

@model
def LogPiece():
    finish("LogPiece", log_x("Log", 0.17, 0.9, (0, 0, 0), "M_Bark"))


@model
def PlankPiece():
    parts = []
    for i in range(3):
        parts.append(box("Plank", (1.3, 0.22, 0.06), (0, (i - 1) * 0.24, 0.03), "M_Plank", 0.01))
    for i in range(2):
        parts.append(box("Plank", (1.3, 0.22, 0.06), (0.05, (i - 0.5) * 0.24, 0.09), "M_Plank", 0.01))
    finish("PlankPiece", parts)


@model
def Log_Fallen():
    parts = log_x("Log", 0.38, 4.5, (0, 0, 0.32), "M_Bark", 10)
    parts.append(ftube("Stub", (0.8, 0, 0.5), (1.2, 0.5, 1.1), 0.1, 0.05, "M_Bark", 5))
    for x in (-1.2, 0.2, 1.4):
        parts.append(box("Moss", (0.7, 0.4, 0.06), (x, 0.05, 0.68), "M_Grass"))
    finish("Log_Fallen", parts)


def rock(name, r, seed, scale=(1.3, 1.0, 0.7)):
    o = ico("Rock", r, (0, 0, r * 0.45), "M_Stone", 1, scale, 0.2, seed)
    finish(name, [o])


@model
def Rock_A():
    rock("Rock_A", 0.55, 1)


@model
def Rock_B():
    rock("Rock_B", 0.95, 2, (1.2, 1.1, 0.8))


@model
def Rock_C():
    rock("Rock_C", 1.7, 3, (1.4, 1.0, 0.65))


@model
def Bush_A():
    parts = []
    for i, (x, y, z, r) in enumerate([(0, 0, 0.45, 0.6), (0.5, 0.2, 0.35, 0.45), (-0.45, 0.15, 0.35, 0.45), (0.1, -0.4, 0.3, 0.4)]):
        parts.append(ico("Bush", r, (x, y, z), "M_LeafDark" if i % 2 else "M_Leaf", 1, (1, 1, 0.85), 0.15, 200 + i))
    finish("Bush_A", parts)


@model
def Bush_Berry():
    parts = []
    for i, (x, y, z, r) in enumerate([(0, 0, 0.5, 0.65), (0.55, 0.1, 0.38, 0.45), (-0.5, -0.2, 0.36, 0.45)]):
        parts.append(ico("Bush", r, (x, y, z), "M_LeafDark", 1, (1, 1, 0.85), 0.15, 210 + i))
    rnd = random.Random(4)
    for i in range(12):
        a = rnd.uniform(0, math.tau)
        parts.append(ico("Berry", 0.06, (math.cos(a) * 0.6, math.sin(a) * 0.6, rnd.uniform(0.3, 0.8)), "M_Berry", 0))
    finish("Bush_Berry", parts)


@model
def Grass_A():
    parts = []
    rnd = random.Random(8)
    for i in range(9):
        x, y = rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25)
        h = rnd.uniform(0.25, 0.5)
        parts.append(cone("Blade", 0.035, 0.0, h, (x, y, h / 2), "M_Grass", 3, (rnd.uniform(-0.25, 0.25), rnd.uniform(-0.25, 0.25), rnd.uniform(0, 3))))
    finish("Grass_A", parts)


def flower(name, petal_mat):
    parts = []
    rnd = random.Random(len(name))
    for k in range(3):
        x, y = rnd.uniform(-0.2, 0.2), rnd.uniform(-0.2, 0.2)
        h = rnd.uniform(0.25, 0.4)
        parts.append(ftube("Stem", (x, y, 0), (x, y, h), 0.012, 0.01, "M_Grass", 4))
        for i in range(5):
            a = i * math.tau / 5
            parts.append(box("Petal", (0.05, 0.025, 0.012), (x + math.cos(a) * 0.035, y + math.sin(a) * 0.035, h), petal_mat, rot=(0, 0, a)))
        parts.append(ico("Center", 0.02, (x, y, h + 0.01), "M_FlowerYellow" if petal_mat != "M_FlowerYellow" else "M_WoodCutDark", 0))
    finish(name, parts)


@model
def Flower_A():
    flower("Flower_A", "M_FlowerYellow")


@model
def Flower_B():
    flower("Flower_B", "M_FlowerPurple")


@model
def Mushrooms():
    parts = []
    for (x, y, h, r) in [(0, 0, 0.18, 0.11), (0.14, 0.08, 0.12, 0.08), (-0.1, 0.1, 0.1, 0.07)]:
        parts.append(fcyl("Stem", r * 0.35, h, (x, y, h / 2), "M_Canvas", verts=6))
        parts.append(cone("Cap", r, r * 0.3, r * 0.8, (x, y, h + r * 0.3), "M_PaintRed", 8))
    finish("Mushrooms", parts)


@model
def Branch_Fallen():
    parts = [ftube("Branch", (-1.0, 0, 0.08), (1.1, 0.2, 0.1), 0.08, 0.04, "M_Bark", 5),
             ftube("Fork", (0.2, 0.05, 0.09), (0.8, 0.6, 0.12), 0.05, 0.02, "M_Bark", 5),
             ftube("Fork", (-0.4, 0, 0.08), (-0.9, -0.45, 0.1), 0.04, 0.02, "M_Bark", 5)]
    finish("Branch_Fallen", parts)


@model
def Crate():
    parts = [box("Crate", (0.8, 0.8, 0.8), (0, 0, 0.4), "M_Plank", 0.02)]
    for s in (-1, 1):
        parts.append(box("Slat", (0.84, 0.06, 0.84), (0, s * 0.4, 0.4), "M_Wood"))
        parts.append(box("Slat", (0.06, 0.84, 0.84), (s * 0.4, 0, 0.4), "M_Wood"))
    parts.append(col("Crate", (0.84, 0.84, 0.8), (0, 0, 0.4)))
    finish("Crate", parts)


@model
def Lantern_Post():
    parts = [box("Post", (0.16, 0.16, 2.6), (0, 0, 1.3), "M_WoodDark"),
             box("Arm", (0.7, 0.1, 0.1), (0.3, 0, 2.5), "M_WoodDark"),
             box("Lamp", (0.22, 0.22, 0.3), (0.58, 0, 2.25), "M_MetalDark"),
             box("Light", (0.16, 0.16, 0.2), (0.58, 0, 2.25), "M_Glow"),
             col("Post", (0.2, 0.2, 2.6), (0, 0, 1.3))]
    empty("LightPoint", (0.58, 0, 2.2))
    finish("Lantern_Post", parts)


@model
def Signpost():
    parts = [box("Post", (0.14, 0.14, 2.2), (0, 0, 1.1), "M_Wood"),
             box("Board", (1.0, 0.06, 0.28), (0.35, 0, 1.85), "M_Sign", 0.01),
             box("Board", (0.9, 0.06, 0.26), (-0.3, 0, 1.5), "M_Sign", 0.01),
             col("Post", (0.16, 0.16, 2.2), (0, 0, 1.1))]
    finish("Signpost", parts)


@model
def SlotMarker():
    parts = []
    h = 1.2
    for (x, y) in ((-h, -h), (h, -h), (h, h), (-h, h)):
        parts.append(box("Stake", (0.08, 0.08, 0.5), (x, y, 0.2), "M_Wood"))
    for (a, b) in (((-h, -h), (h, -h)), ((h, -h), (h, h)), ((h, h), (-h, h)), ((-h, h), (-h, -h))):
        parts.append(ftube("Rope", (a[0], a[1], 0.4), (b[0], b[1], 0.4), 0.015, 0.015, "M_Rope", 4))
    parts.append(box("Flag", (0.02, 0.35, 0.22), (h, h + 0.17, 0.62), "M_PaintRed"))
    parts.append(box("Pole", (0.04, 0.04, 0.8), (h, h, 0.4), "M_Wood"))
    finish("SlotMarker", parts)


# ----------------------------------------------------------------- axes (grip at origin, handle +Z, edge +Y)

def axe_head(parts, profile, thick, metal, edge, x=0.0):
    head = prism("Head", profile, thick, metal, 'X')
    head.location.x = x
    parts.append(flat(head))
    # Bright bevelled edge strip along the front of the blade.
    ys = [p[0] for p in profile]
    zs = [p[1] for p in profile]
    front = max(ys)
    zlo = min(z for y, z in profile if y > front - 0.03)
    zhi = max(z for y, z in profile if y > front - 0.03)
    parts.append(box("Edge", (thick * 0.6, 0.02, zhi - zlo), (x, front - 0.004, (zlo + zhi) / 2), edge))


@model
def Axe_0():
    """Rusty Hatchet."""
    parts = [ftube("Handle", (0, 0, -0.12), (0, 0.01, 0.56), 0.021, 0.018, "M_Wood", 7)]
    axe_head(parts, [(-0.05, 0.47), (0.05, 0.48), (0.12, 0.44), (0.13, 0.6), (0.05, 0.56), (-0.05, 0.57)], 0.035, "M_Rust", "M_Metal")
    for z in (-0.06, -0.02, 0.02):
        parts.append(fcyl("Rope", 0.026, 0.03, (0, 0, z), "M_Rope", verts=7))
    finish("Axe_0", parts)


@model
def Axe_1():
    """Steel Axe."""
    parts = [ftube("Handle", (0, 0, -0.15), (0, -0.01, 0.3), 0.022, 0.019, "M_Wood", 8),
             ftube("Handle2", (0, -0.01, 0.3), (0, 0.015, 0.68), 0.019, 0.02, "M_Wood", 8),
             fcyl("Knob", 0.028, 0.04, (0, 0, -0.15), "M_WoodDark", verts=8)]
    axe_head(parts, [(-0.06, 0.58), (0.06, 0.59), (0.15, 0.53), (0.17, 0.75), (0.06, 0.69), (-0.06, 0.7)], 0.04, "M_Steel", "M_Steel")
    parts.append(box("Poll", (0.05, 0.05, 0.12), (0, -0.07, 0.64), "M_MetalDark"))
    finish("Axe_1", parts)


@model
def Axe_2():
    """Lumberjack's Axe: long handle, red head."""
    parts = [ftube("Handle", (0, 0, -0.2), (0, -0.015, 0.4), 0.024, 0.02, "M_Wood", 8),
             ftube("Handle2", (0, -0.015, 0.4), (0, 0.02, 0.8), 0.02, 0.022, "M_Wood", 8),
             fcyl("Band", 0.025, 0.08, (0, 0.0, 0.52), "M_PaintRed", verts=8)]
    axe_head(parts, [(-0.07, 0.68), (0.06, 0.69), (0.17, 0.6), (0.2, 0.88), (0.06, 0.8), (-0.07, 0.82)], 0.045, "M_PaintRed", "M_Steel")
    parts.append(box("Poll", (0.055, 0.06, 0.14), (0, -0.08, 0.75), "M_MetalDark"))
    finish("Axe_2", parts)


@model
def Axe_3():
    """Tempered Broadaxe: bearded blade, leather grip, gold rivets."""
    parts = [ftube("Handle", (0, 0, -0.2), (0, 0, 0.8), 0.023, 0.022, "M_WoodDark", 8)]
    for z in (-0.16, -0.1, -0.04, 0.02):
        parts.append(fcyl("Grip", 0.027, 0.05, (0, 0, z), "M_Leather", verts=8))
    axe_head(parts, [(-0.07, 0.7), (0.05, 0.7), (0.12, 0.52), (0.24, 0.5), (0.25, 0.9), (0.06, 0.84), (-0.07, 0.85)], 0.05, "M_MetalDark", "M_Steel")
    for z in (0.74, 0.8):
        parts.append(ico("Rivet", 0.012, (0.028, 0.02, z), "M_Gold", 0))
    finish("Axe_3", parts)


@model
def Axe_4():
    """Ironbreaker: double-bit head with glowing edges."""
    parts = [ftube("Handle", (0, 0, -0.22), (0, 0, 0.84), 0.025, 0.024, "M_WoodDark", 8)]
    for z in (-0.18, 0.35, 0.62):
        parts.append(fcyl("Band", 0.029, 0.04, (0, 0, z), "M_Gold", verts=8))
    prof = [(0.0, 0.66), (0.08, 0.68), (0.2, 0.6), (0.23, 0.96), (0.08, 0.88), (0.0, 0.9)]
    axe_head(parts, prof, 0.055, "M_MetalDark", "M_Glow")
    back = [(-p[0], p[1]) for p in reversed(prof)]
    head2 = flat(prism("Head2", back, 0.055, "M_MetalDark", 'X'))
    parts.append(head2)
    parts.append(box("Edge2", (0.035, 0.02, 0.34), (0, -0.226, 0.78), "M_Glow"))
    parts.append(box("Core", (0.07, 0.07, 0.26), (0, 0, 0.78), "M_Gold"))
    finish("Axe_4", parts)

# ----------------------------------------------------------------- machines
# Moving parts are separate objects with their pivot at the joint:
#   Arm (pivot at hinge, swings about X) > Blade/Blade2/Blade3 (pivot at centre, spin about X)
#   Log (slides along X), Rollers_* and Flywheel spin about X, Plank slides along Y.
#   WorkPoint marks where the cut happens (chips spray from there).

def saw_blade(name, r, loc, teeth=18):
    parts = [disc_x(name, r, 0.03, loc, "M_Steel", 20), disc_x(name + "Hub", r * 0.25, 0.07, loc, "M_MetalDark", 10)]
    for i in range(teeth):
        a = i * math.tau / teeth
        parts.append(box("Tooth", (0.032, 0.06, 0.06), (loc[0], loc[1] + math.cos(a) * r, loc[2] + math.sin(a) * r), "M_Steel", rot=(a + 0.6, 0, 0)))
    return part(name, parts, loc)


def chopper(name, paint, size, blades, extra):
    w, d = size
    s = w / 2.4
    base = [box("Skid", (w, d, 0.22), (0, 0, 0.11), "M_MetalDark"),
            box("RailL", (0.12, d, 0.18), (-w / 2 + 0.06, 0, 0.3), paint),
            box("RailR", (0.12, d, 0.18), (w / 2 - 0.06, 0, 0.3), paint),
            box("Engine", (0.95 * s, 0.85 * s, 0.75 * s), (-w / 2 + 0.6 * s, -d / 2 + 0.55 * s, 0.22 + 0.375 * s), paint, 0.03),
            box("Grille", (0.7 * s, 0.04, 0.4 * s), (-w / 2 + 0.6 * s, -d / 2 + 0.11 * s, 0.22 + 0.4 * s), "M_MetalDark"),
            ftube("Exhaust", (-w / 2 + 0.35 * s, -d / 2 + 0.4 * s, 0.9 * s), (-w / 2 + 0.35 * s, -d / 2 + 0.4 * s, 1.75 * s), 0.06 * s, 0.05 * s, "M_MetalDark", 8),
            box("Post", (0.2 * s, 0.2 * s, 1.4 * s), (0.45 * s, -0.75 * s, 0.22 + 0.7 * s), paint),
            box("Hinge", (0.34 * s, 0.26 * s, 0.26 * s), (0.45 * s, -0.75 * s, 1.5 * s), "M_MetalDark")]
    for x in (-0.65 * s, 0.75 * s):
        base.append(prism("Cradle", [(-0.32 * s, 0), (0.32 * s, 0), (0.32 * s, 0.4 * s), (0.18 * s, 0.25 * s), (-0.18 * s, 0.25 * s), (-0.32 * s, 0.4 * s)], 0.12, paint, 'X'))
        base[-1].location = (x, 0.6 * s, 0.22)
    for i in range(4):
        base.append(fcyl("Round", 0.24 * s, 0.14, (w / 2 + 0.2, 0.75 * s + (i % 2) * 0.3, 0.07 + (i // 2) * 0.14), "M_WoodCut", verts=9))
    base += extra(s)
    base.append(col("Body", (w, d, 1.7 * s), (0, 0, 0.85 * s)))
    join([o for o in base if not o.name.startswith("COL_")], "Base", (0, 0, 0))

    hinge = (0.45 * s, -0.75 * s, 1.5 * s)
    blade_y = 0.6 * s
    blade_z = 1.25 * s
    arm_parts = [box("Beam", (0.16 * s, abs(blade_y - hinge[1]) + 0.1, 0.16 * s), (hinge[0], (hinge[1] + blade_y) / 2, (hinge[2] + blade_z) / 2), paint, rot=(math.atan2(hinge[2] - blade_z, blade_y - hinge[1]), 0, 0))]
    guard = prism("Guard", [(math.cos(a) * 0.55 * s, math.sin(a) * 0.55 * s) for a in [i * math.pi / 8 for i in range(9)]], 0.12, paint, 'X')
    guard.location = (hinge[0], blade_y, blade_z)
    arm_parts.append(flat(guard))
    arm = part("Arm", arm_parts, hinge)
    for i, bx in enumerate(blades):
        b = saw_blade("Blade" + ("" if i == 0 else str(i + 1)), 0.46 * s, (bx * s, blade_y, blade_z))
        parent(b, arm)
        if i > 0:
            parts_axle = box("Axle", (abs(bx * s - blades[0] * s) + 0.1, 0.06, 0.06), ((bx * s + blades[0] * s) / 2, blade_y, blade_z), "M_MetalDark")
            parent(parts_axle, arm)
    log = part("Log", log_x("LogBody", 0.27 * s, 1.9 * s, (0, blade_y, 0.22 + 0.5 * s), "M_Bark"), (0, blade_y, 0.22 + 0.5 * s))
    empty("WorkPoint", (blades[0] * s, blade_y, 0.22 + 0.62 * s))
    export(name)


@model
def AutoChopper():
    chopper("AutoChopper", "M_Paint", (2.4, 2.4), [0.45], lambda s: [])


@model
def AutoChopper_Mk2():
    def extra(s):
        parts = []
        for x in (-1.45 * s, 1.45 * s):
            parts.append(box("Track", (0.3 * s, 2.7 * s, 0.4 * s), (x, 0, 0.2 * s), "M_Rubber", 0.04))
            for y in (-1.0, 0, 1.0):
                parts.append(disc_x("Wheel", 0.17 * s, 0.32 * s, (x, y * s, 0.2 * s), "M_MetalDark", 10))
        parts.append(ftube("Exhaust2", (-0.9 * s, -0.7 * s, 0.9 * s), (-0.9 * s, -0.7 * s, 1.9 * s), 0.07 * s, 0.06 * s, "M_MetalDark", 8))
        parts.append(box("Lamp", (0.2 * s, 0.1, 0.12 * s), (-0.5 * s, -0.68 * s, 1.1 * s), "M_Glow"))
        return parts
    chopper("AutoChopper_Mk2", "M_PaintRed", (3.0, 2.9), [0.45, -0.25], extra)


@model
def AutoChopper_Mk3():
    def extra(s):
        parts = []
        for x in (-1.35 * s, 1.35 * s):
            parts.append(box("Track", (0.34 * s, 2.8 * s, 0.45 * s), (x, 0, 0.22 * s), "M_Rubber", 0.04))
        parts.append(box("Cab", (0.9 * s, 0.8 * s, 0.9 * s), (-0.6 * s, -0.75 * s, 1.35 * s), "M_PaintBlue", 0.04))
        parts.append(box("Window", (0.92 * s, 0.6 * s, 0.35 * s), (-0.6 * s, -0.7 * s, 1.5 * s), "M_Glass"))
        for x in (-0.95 * s, -0.25 * s):
            parts.append(box("Light", (0.14 * s, 0.06, 0.1 * s), (x, -0.32 * s, 1.75 * s), "M_Glow"))
        return parts
    chopper("AutoChopper_Mk3", "M_PaintBlue", (3.3, 3.2), [0.5, -0.15, -0.8], extra)


@model
def Sawmill():
    w, d = 3.2, 6.0
    base = [box("Floor", (w, d, 0.2), (0, 0, 0.1), "M_Plank", 0.02)]
    for x in (-w / 2 + 0.15, w / 2 - 0.15):
        for y in (-d / 2 + 0.15, d / 2 - 0.15):
            base.append(box("Post", (0.2, 0.2, 3.0), (x, y, 1.6), "M_Wood"))
    for s in (-1, 1):
        base.append(box("Roof", (w / 2 + 0.5, d + 0.6, 0.12), (s * (w / 4 + 0.15), 0, 3.35), "M_Roof", rot=(0, s * 0.42, 0)))
    base.append(box("Ridge", (0.2, d + 0.6, 0.2), (0, 0, 3.72), "M_WoodDark"))
    base.append(box("Table", (0.9, d - 0.6, 0.12), (0, 0, 0.8), "M_MetalDark"))
    for x in (-0.5, 0.5):
        base.append(box("Rail", (0.08, d - 0.6, 0.2), (x, 0, 0.92), "M_PaintGreen"))
    for y in (-2.2, 0, 2.2):
        for x in (-0.4, 0.4):
            base.append(box("Leg", (0.1, 0.1, 0.7), (x, y, 0.45), "M_MetalDark"))
    base.append(box("Motor", (0.8, 1.1, 0.9), (1.05, 0.2, 0.65), "M_PaintGreen", 0.03))
    base.append(box("Guard", (0.15, 1.4, 0.75), (0.2, 0.2, 1.55), "M_PaintGreen"))
    base += log_x("Pile", 0.25, 1.2, (-0.9, -2.6, 0.45), "M_Bark") + log_x("Pile", 0.25, 1.2, (-0.9, -2.1, 0.45), "M_Bark") + log_x("Pile", 0.25, 1.2, (-0.9, -2.35, 0.88), "M_Bark")
    for i in range(4):
        base.append(box("Out", (1.2, 0.3, 0.06), (-1.0, 2.3 + (i % 2) * 0.32, 0.25 + (i // 2) * 0.07), "M_Plank"))
    base.append(col("Table", (1.2, d - 0.4, 1.0), (0, 0, 0.5)))
    base.append(col("Motor", (0.9, 1.2, 1.1), (1.05, 0.2, 0.55)))
    for x in (-w / 2 + 0.15, w / 2 - 0.15):
        for y in (-d / 2 + 0.15, d / 2 - 0.15):
            base.append(col("Post", (0.22, 0.22, 3.0), (x, y, 1.6)))
    join([o for o in base if not o.name.startswith("COL_")], "Base", (0, 0, 0))

    saw_blade("Blade", 0.6, (0, 0.2, 1.05), 22)
    for i, y in enumerate([-2.3, -1.6, -0.9, 1.0, 1.7, 2.4]):
        part("Roller_%d" % i, [disc_x("R", 0.08, 0.8, (0, y, 0.92), "M_Steel", 8)], (0, y, 0.92))
    fly = [disc_x("Fly", 0.45, 0.08, (1.5, 0.2, 0.75), "M_MetalDark", 14)]
    for k in range(4):
        a = k * math.pi / 4
        fly.append(box("Spoke", (0.1, 0.8, 0.06), (1.52, 0.2, 0.75), "M_Paint", rot=(a, 0, 0)))
    part("Flywheel", fly, (1.5, 0.2, 0.75))
    part("Log", log_y("LogBody", 0.26, 1.5, (0, -1.6, 1.25), "M_Bark"), (0, -1.6, 1.25))
    part("Plank", [box("Board", (0.5, 1.4, 0.08), (0, 1.4, 1.0), "M_Plank", 0.01)], (0, 1.4, 1.0))
    empty("WorkPoint", (0, 0.2, 1.25))
    export("Sawmill")


# ----------------------------------------------------------------- buildings

@model
def Market():
    parts = [box("Counter", (3.6, 0.9, 1.0), (0, 0.6, 0.5), "M_Wood", 0.02),
             box("Top", (3.8, 1.05, 0.08), (0, 0.6, 1.04), "M_Plank", 0.01),
             box("Back", (3.6, 0.15, 2.6), (0, -1.1, 1.3), "M_WoodDark"),
             box("Shelf", (3.4, 0.5, 0.06), (0, -0.85, 1.5), "M_Plank")]
    for x in (-1.8, 1.8):
        for y in (1.05, -1.1):
            parts.append(box("Post", (0.14, 0.14, 3.0), (x, y, 1.5), "M_WoodDark"))
    for i in range(8):
        parts.append(box("Awning", (0.48, 2.6, 0.06), (-1.68 + i * 0.48, 0.15, 3.1), "M_PaintRed" if i % 2 else "M_Canvas", rot=(-0.28, 0, 0)))
    for i in range(5):
        parts.append(fcyl("Round", 0.18, 0.4, (-1.3 + i * 0.6, -0.85, 1.75), "M_WoodCut", rot=(math.radians(90), 0, 0), verts=9))
    parts.append(box("Cashbox", (0.45, 0.35, 0.25), (1.2, 0.6, 1.2), "M_PaintGreen", 0.02))
    parts.append(box("ScaleBase", (0.5, 0.4, 0.06), (-1.0, 0.6, 1.11), "M_Metal"))
    parts.append(box("ScalePost", (0.05, 0.05, 0.5), (-1.0, 0.6, 1.36), "M_Metal"))
    parts.append(box("ScaleBeam", (0.6, 0.04, 0.04), (-1.0, 0.6, 1.6), "M_Metal"))
    parts.append(box("Sign", (2.6, 0.1, 0.6), (0, 1.2, 3.55), "M_Sign", 0.02))
    parts.append(box("SignFrame", (2.7, 0.08, 0.7), (0, 1.15, 3.55), "M_WoodDark"))
    for x in (-1.2, 1.2):
        parts.append(box("SignRope", (0.03, 0.03, 0.5), (x, 1.15, 3.1), "M_Rope"))
    parts.append(col("Counter", (3.8, 1.0, 1.1), (0, 0.6, 0.55)))
    parts.append(col("Back", (3.8, 0.4, 3.0), (0, -1.0, 1.5)))
    empty("SignAnchor", (0, 1.27, 3.55))
    empty("EffectPoint", (0, 0.6, 1.4))
    finish("Market", parts)


@model
def Workbench():
    parts = [box("Top", (2.4, 1.0, 0.12), (0, 0.3, 0.95), "M_Plank", 0.01)]
    for x in (-1.05, 1.05):
        for y in (-0.1, 0.7):
            parts.append(box("Leg", (0.12, 0.12, 0.9), (x, y, 0.45), "M_Wood"))
    parts.append(box("Shelf", (2.2, 0.8, 0.06), (0, 0.3, 0.3), "M_Wood"))
    parts.append(box("Board", (2.6, 0.12, 1.8), (0, -0.3, 1.9), "M_WoodDark"))
    for i, x in enumerate((-0.8, -0.2, 0.4)):
        parts.append(box("Handle", (0.05, 0.05, 0.75), (x, -0.22, 1.85), "M_Wood"))
        parts.append(box("HeadSil", (0.04, 0.2, 0.14), (x + 0.08, -0.22, 2.17), "M_Steel" if i != 1 else "M_PaintRed"))
    parts.append(box("Saw", (0.7, 0.02, 0.18), (0.9, -0.22, 1.7), "M_Steel"))
    parts.append(box("Vise", (0.25, 0.3, 0.2), (-0.95, 0.75, 1.1), "M_MetalDark"))
    parts.append(box("Hammer", (0.35, 0.05, 0.05), (0.3, 0.45, 1.04), "M_Wood"))
    parts.append(box("HammerHead", (0.06, 0.14, 0.07), (0.47, 0.45, 1.05), "M_MetalDark"))
    parts.append(fcyl("Stump", 0.35, 0.6, (1.75, 0.5, 0.3), "M_Bark", verts=9))
    parts.append(box("Anvil", (0.5, 0.22, 0.2), (1.75, 0.5, 0.72), "M_MetalDark", 0.02))
    parts.append(box("Horn", (0.2, 0.12, 0.1), (2.05, 0.5, 0.77), "M_MetalDark"))
    parts.append(disc_x("Grind", 0.35, 0.12, (-1.75, 0.45, 0.7), "M_Stone", 14))
    parts.append(box("GrindFrame", (0.2, 0.2, 0.7), (-1.75, 0.45, 0.35), "M_Wood"))
    parts.append(box("Sign", (1.8, 0.08, 0.45), (0, -0.2, 3.05), "M_Sign", 0.02))
    parts.append(col("Bench", (2.5, 1.1, 1.0), (0, 0.3, 0.5)))
    parts.append(col("Board", (2.6, 0.3, 3.3), (0, -0.3, 1.65)))
    parts.append(col("Anvil", (0.8, 0.8, 0.9), (1.75, 0.5, 0.45)))
    parts.append(col("Grind", (0.6, 0.8, 1.1), (-1.75, 0.45, 0.55)))
    empty("SignAnchor", (0, -0.15, 3.05))
    finish("Workbench", parts)


@model
def Fence_Segment():
    parts = []
    for x in (-2.0, 2.0):
        parts.append(box("Post", (0.18, 0.18, 1.5), (x, 0, 0.75), "M_WoodDark"))
    for z in (0.55, 1.1):
        parts.append(box("Rail", (4.0, 0.08, 0.16), (0, 0, z), "M_Wood", rot=(0, 0.01, 0)))
    parts.append(col("Fence", (4.1, 0.3, 1.6), (0, 0, 0.8)))
    finish("Fence_Segment", parts)


@model
def Gate():
    parts = []
    for x in (-2.2, 2.2):
        parts.append(box("Post", (0.32, 0.32, 3.6), (x, 0, 1.8), "M_WoodDark"))
        parts.append(col("Post", (0.4, 0.4, 3.6), (x, 0, 1.8)))
    parts.append(box("Beam", (5.0, 0.3, 0.3), (0, 0, 3.45), "M_WoodDark"))
    parts.append(box("Sign", (2.8, 0.1, 0.6), (0, 0.2, 2.95), "M_Sign", 0.02))
    join([o for o in parts if not o.name.startswith("COL_")], "Frame", (0, 0, 0))

    def door(name, hinge_x, sign):
        d = []
        for i in range(5):
            d.append(box("Plank", (0.38, 0.08, 2.1), (hinge_x + sign * (0.25 + i * 0.4), 0, 1.25), "M_Wood"))
        for z in (0.55, 1.95):
            d.append(box("Brace", (2.0, 0.1, 0.16), (hinge_x + sign * 1.03, 0.07, z), "M_WoodDark"))
        d.append(box("Diag", (2.3, 0.1, 0.14), (hinge_x + sign * 1.03, 0.07, 1.25), "M_WoodDark", rot=(0, sign * -0.6, 0)))
        return part(name, d, (hinge_x, 0, 0))

    door("Door_L", -2.05, 1)
    door("Door_R", 2.05, -1)
    empty("SignAnchor", (0, 0.27, 2.95))
    export("Gate")


@model
def Cabin():
    parts = []
    w, d, h = 5.0, 4.0, 2.6
    for i in range(8):
        z = 0.17 + i * 0.32
        parts.append(fcyl("LogF", 0.17, w + 0.4, (0, d / 2, z), "M_Bark", rot=(0, math.radians(90), 0), verts=8))
        parts.append(fcyl("LogB", 0.17, w + 0.4, (0, -d / 2, z), "M_Bark", rot=(0, math.radians(90), 0), verts=8))
        parts.append(fcyl("LogL", 0.17, d + 0.4, (-w / 2, 0, z + 0.16), "M_Bark", rot=(math.radians(90), 0, 0), verts=8))
        parts.append(fcyl("LogR", 0.17, d + 0.4, (w / 2, 0, z + 0.16), "M_Bark", rot=(math.radians(90), 0, 0), verts=8))
    parts.append(box("Fill", (w - 0.1, d - 0.1, h), (0, 0, h / 2), "M_WoodDark"))
    for s in (-1, 1):
        parts.append(box("Roof", (w + 1.0, d / 2 + 0.9, 0.16), (0, s * (d / 4 + 0.2), h + 0.85), "M_Roof", rot=(s * -0.6, 0, 0)))
    parts.append(prism("Gable", [(-d / 2, h), (d / 2, h), (0, h + 1.5)], w - 0.2, "M_WoodDark", 'X'))
    parts.append(box("Door", (1.0, 0.1, 1.9), (0.8, d / 2 + 0.18, 0.95), "M_WoodDark"))
    parts.append(box("Knob", (0.06, 0.06, 0.06), (1.15, d / 2 + 0.25, 0.95), "M_Gold"))
    parts.append(box("Window", (0.9, 0.08, 0.7), (-1.2, d / 2 + 0.18, 1.4), "M_Glass"))
    parts.append(box("Frame", (1.05, 0.06, 0.85), (-1.2, d / 2 + 0.15, 1.4), "M_Wood"))
    parts.append(box("Chimney", (0.6, 0.6, 2.2), (w / 2 - 0.8, -0.8, h + 1.0), "M_Stone"))
    parts.append(box("Step", (1.4, 0.6, 0.18), (0.8, d / 2 + 0.45, 0.09), "M_Stone"))
    parts.append(col("Cabin", (w + 0.4, d + 0.4, h + 1.5), (0, 0, (h + 1.5) / 2)))
    finish("Cabin", parts)


@model
def Mountain_A():
    m = cone("Mountain", 32, 3, 26, (0, 0, 12), "M_StoneDark", 9, jit=2.2, seed=301)
    snow = cone("Snow", 7.5, 0.5, 6, (0, 0, 23.2), "M_Snow", 9, jit=0.4, seed=302)
    base = cone("Foot", 36, 26, 5, (0, 0, 1.5), "M_Grass", 11, jit=1.0, seed=303)
    finish("Mountain_A", [m, snow, base])


@model
def Hill_A():
    finish("Hill_A", [ico("Hill", 14, (0, 0, -3), "M_Grass", 1, (1.4, 1.0, 0.45), 0.08, 304)])


def main():
    names = [n for n in MODELS if not ONLY or n in ONLY]
    for n in names:
        try:
            reset()
            for m in list(bpy.data.materials):
                bpy.data.materials.remove(m)
            MODELS[n]()
        except Exception as e:  # keep going so one broken model doesn't block the rest
            import traceback
            traceback.print_exc()
            print("FAILED", n, e)
    print("DONE", len(names))


main()

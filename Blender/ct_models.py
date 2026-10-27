# "Forest to Fortune" model generator for Blender 4.2+/5.x (run headless):
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
    """Lumber Market: a timber-framed stall on a plank floor with a shingled gable roof, a sales
    counter (register, scale, coins), stocked shelves, a log pile, crates, a chalk price board
    and lanterns. Front is +Y."""
    p = []
    rad = math.radians
    # Floor: boards along Y on a dark sill, plus a stone step at the front.
    p.append(span("Sill", -2.55, 2.55, -1.75, 1.75, 0.0, 0.14, "M_WoodDark"))
    for i in range(10):
        x0 = -2.5 + i * 0.5
        p.append(span("Board", x0 + 0.01, x0 + 0.49, -1.7, 1.7, 0.14, 0.2, "M_Plank" if i % 2 else "M_Wood", 0.01))
    p.append(span("Step", -1.1, 1.1, 1.75, 2.15, 0.0, 0.1, "M_StoneDark", 0.02))

    # Frame: four posts, beams and knee braces.
    for x in (-2.3, 2.3):
        for y in (-1.5, 1.5):
            p.append(box("Post", (0.2, 0.2, 3.0), (x, y, 1.7), "M_WoodDark", 0.02))
            p.append(box("PostFoot", (0.3, 0.3, 0.12), (x, y, 0.26), "M_StoneDark", 0.02))
            p.append(box("Brace", (0.09, 0.09, 0.85), (x - math.copysign(0.3, x), y, 2.88), "M_WoodDark", rot=(0, rad(45) * math.copysign(1, x), 0)))
    for y in (-1.5, 1.5):
        p.append(span("Beam", -2.45, 2.45, y - 0.12, y + 0.12, 3.12, 3.34, "M_WoodDark", 0.015))
    for x in (-2.3, 2.3):
        p.append(span("SideBeam", x - 0.11, x + 0.11, -1.6, 1.6, 3.12, 3.32, "M_WoodDark"))

    # Gable roof along X with shingle rows and a ridge cap.
    roof = prism("Roof", [(-2.1, 3.2), (0, 4.24), (2.1, 3.2), (2.1, 3.34), (0, 4.38), (-2.1, 3.34)], 5.4, "M_Roof")
    p.append(roof)
    slope = math.atan2(1.04, 2.1)
    for s in (-1, 1):
        for k in range(5):
            f = 0.1 + k * 0.19
            y = s * 2.1 * (1 - f)
            z = 3.34 + f * 1.04 + 0.03
            p.append(box("Shingle", (5.42, 0.42, 0.045), (0, y, z), "M_WoodDark" if k % 2 else "M_Roof", rot=(-slope * s, 0, 0)))
    p.append(box("Ridge", (5.5, 0.22, 0.12), (0, 0, 4.4), "M_WoodDark", 0.02))
    for x in (-2.32, 2.32):
        p.append(prism("Gable", [(-1.55, 3.34), (1.55, 3.34), (0, 4.1)], 0.08, "M_Plank"))
        p[-1].location.x = x
    # Coin emblem on both gables.
    for x in (-2.38, 2.38):
        p.append(fcyl("Coin", 0.3, 0.05, (x, 0, 3.72), "M_Gold", rot=(0, rad(90), 0), verts=16))
        p.append(fcyl("CoinRim", 0.22, 0.07, (x, 0, 3.72), "M_Paint", rot=(0, rad(90), 0), verts=16))
    # Striped valance under the front eave.
    for i in range(18):
        x = -2.55 + i * 0.3
        p.append(box("Valance", (0.29, 0.03, 0.26), (x + 0.15, 2.06, 3.08), "M_PaintRed" if i % 2 else "M_Canvas", rot=(rad(-8), 0, 0)))

    # Sales counter with plank front, register, scale and coins.
    p.append(span("Counter", -1.95, 1.95, 0.8, 1.3, 0.2, 1.14, "M_Wood"))
    for i in range(13):
        x = -1.9 + i * 0.3
        p.append(span("CounterSlat", x, x + 0.06, 1.3, 1.33, 0.25, 1.1, "M_WoodDark"))
    p.append(span("CounterTop", -2.05, 2.05, 0.74, 1.4, 1.14, 1.22, "M_Plank", 0.015))
    p.append(span("SideCounter", -1.95, -1.45, -0.2, 0.8, 0.2, 1.14, "M_Wood"))
    p.append(span("SideTop", -2.05, -1.38, -0.25, 0.8, 1.14, 1.22, "M_Plank", 0.015))
    # Cash register.
    p.append(box("Register", (0.55, 0.42, 0.28), (1.25, 1.02, 1.36), "M_PaintGreen", 0.02))
    p.append(box("Keys", (0.5, 0.22, 0.12), (1.25, 1.15, 1.52), "M_MetalDark", rot=(rad(-25), 0, 0)))
    p.append(box("Display", (0.32, 0.06, 0.16), (1.25, 0.88, 1.62), "M_MetalDark"))
    p.append(box("Trim", (0.57, 0.44, 0.03), (1.25, 1.02, 1.23), "M_Gold"))
    # Platform scale with a dial.
    p.append(box("ScaleBase", (0.5, 0.36, 0.06), (-0.95, 1.05, 1.25), "M_Metal", 0.01))
    p.append(box("ScalePost", (0.06, 0.06, 0.32), (-0.95, 0.92, 1.44), "M_Metal"))
    p.append(fcyl("Dial", 0.13, 0.04, (-0.95, 0.94, 1.66), "M_Canvas", rot=(rad(90), 0, 0), verts=16))
    p.append(fcyl("DialRim", 0.145, 0.03, (-0.95, 0.92, 1.66), "M_Metal", rot=(rad(90), 0, 0), verts=16))
    p.append(box("Needle", (0.015, 0.01, 0.1), (-0.93, 0.965, 1.69), "M_PaintRed", rot=(0, rad(-30), 0)))
    p.append(fcyl("Pan", 0.2, 0.03, (-0.95, 1.05, 1.32), "M_Steel", verts=16))
    p.extend(log_x("ScaleLog", 0.09, 0.32, (-0.95, 1.05, 1.42), "M_Bark", 8))
    for i, (x, h) in enumerate([(0.2, 0.08), (0.32, 0.12), (0.44, 0.06)]):
        p.append(fcyl("Coins", 0.06, h, (x, 1.1, 1.22 + h / 2), "M_Gold", verts=10))

    # Back wall with battens and two stocked shelves.
    p.append(span("BackWall", -2.25, 2.25, -1.58, -1.42, 0.2, 3.12, "M_WoodDark"))
    for i in range(10):
        x = -2.15 + i * 0.48
        p.append(span("Batten", x, x + 0.08, -1.42, -1.38, 0.2, 3.1, "M_Wood"))
    for z in (1.15, 2.05):
        p.append(span("Shelf", -2.1, 2.1, -1.4, -0.95, z, z + 0.06, "M_Plank", 0.01))
        for k in range(4):
            p.append(box("Bracket", (0.05, 0.4, 0.05), (-1.8 + k * 1.2, -1.18, z - 0.06), "M_MetalDark"))
    for i in range(6):  # log rounds, cut ends to the front
        x = -1.85 + i * 0.36
        r = 0.15 + 0.03 * (i % 2)
        p.append(fcyl("Round", r, 0.4, (x, -1.15, 1.21 + r), "M_Bark", rot=(rad(90), 0, 0), verts=10))
        p.append(fcyl("RoundCut", r * 0.88, 0.02, (x, -0.95, 1.21 + r), "M_WoodCut", rot=(rad(90), 0, 0), verts=10))
    for layer in range(4):  # plank stacks on the top shelf
        for side in (-1, 1):
            p.append(box("PlankStack", (1.3, 0.36, 0.07), (side * 1.0, -1.17, 2.15 + layer * 0.075), "M_Plank" if layer % 2 else "M_WoodCut", 0.008))

    # Hanging sign under the front beam.
    p.append(box("SignBoard", (2.8, 0.08, 0.6), (0, 1.62, 2.72), "M_Sign", 0.02))
    p.append(box("SignFrame", (2.92, 0.06, 0.72), (0, 1.57, 2.72), "M_WoodDark"))
    for x in (-1.2, 1.2):
        p.append(box("SignRope", (0.03, 0.03, 0.22), (x, 1.6, 3.06), "M_Rope"))

    # Lanterns at the front posts.
    for x in (-2.3, 2.3):
        lx = x - math.copysign(0.32, x)
        p.append(box("LanternArm", (0.36, 0.04, 0.04), (x - math.copysign(0.16, x), 1.62, 2.95), "M_MetalDark"))
        p.append(box("LanternChain", (0.02, 0.02, 0.16), (lx, 1.62, 2.85), "M_MetalDark"))
        p.append(box("LanternCap", (0.2, 0.2, 0.05), (lx, 1.62, 2.75), "M_MetalDark", 0.01))
        p.append(box("LanternGlow", (0.12, 0.12, 0.18), (lx, 1.62, 2.62), "M_Glow"))
        p.append(box("LanternBase", (0.18, 0.18, 0.04), (lx, 1.62, 2.51), "M_MetalDark"))

    # Log pile on the right, crates of planks on the left, a chalk price board in front.
    for row, (z, xs) in enumerate([(0.2, (2.95, 3.37, 3.79)), (0.55, (3.16, 3.58)), (0.9, (3.37,))]):
        for x in xs:
            p.extend(log_y("PileLog", 0.2, 1.7, (x, 0.05, z), "M_Bark", 9))
    for x, y, yaw in [(-3.05, 0.5, 8), (-3.05, -0.45, -5)]:
        p.append(box("Crate", (0.75, 0.75, 0.62), (x, y, 0.31), "M_Wood", 0.02, rot=(0, 0, rad(yaw))))
        for z in (0.1, 0.52):
            p.append(box("CrateBand", (0.78, 0.78, 0.07), (x, y, z), "M_WoodDark", rot=(0, 0, rad(yaw))))
    for k in range(3):
        p.append(box("CratePlank", (1.0, 0.16, 0.05), (-3.05, 0.5 + (k - 1) * 0.18, 0.66), "M_Plank", 0.005, rot=(0, 0, rad(8))))
    for s in (-1, 1):
        p.append(box("BoardLeg", (0.05, 0.05, 1.05), (-1.95 + s * 0.38, 2.55 + s * 0.0, 0.52), "M_Wood", rot=(rad(12) * s, 0, 0)))
    p.append(box("Chalkboard", (0.8, 0.04, 0.62), (-1.95, 2.6, 0.78), "M_MetalDark", rot=(rad(-12), 0, 0)))
    p.append(box("ChalkFrame", (0.88, 0.03, 0.7), (-1.95, 2.585, 0.78), "M_Wood", rot=(rad(-12), 0, 0)))
    for k in range(3):
        p.append(box("Chalk", (0.5 - k * 0.1, 0.01, 0.035), (-2.0 + k * 0.03, 2.635, 0.95 - k * 0.15), "M_Canvas", rot=(rad(-12), 0, 0)))
    p.append(fcyl("ChalkCoin", 0.06, 0.012, (-1.62, 2.64, 0.95), "M_Gold", rot=(rad(78), 0, 0), verts=10))

    # Collisions.
    p.append(col_span("Counter", -2.05, 2.05, 0.74, 1.4, 0.0, 1.25))
    p.append(col_span("SideCounter", -2.05, -1.38, -0.25, 0.8, 0.0, 1.25))
    p.append(col_span("Back", -2.4, 2.4, -1.75, -0.95, 0.0, 3.2))
    for x in (-2.3, 2.3):
        p.append(col_span("FrontPost", x - 0.15, x + 0.15, 1.35, 1.65, 0.0, 3.3))
    p.append(col_span("LogPile", 2.72, 4.02, -0.82, 0.92, 0.0, 1.1))
    p.append(col_span("Crates", -3.45, -2.65, -0.85, 0.9, 0.0, 0.7))
    p.append(col_span("Chalkboard", -2.4, -1.5, 2.35, 2.85, 0.0, 1.1))
    empty("SignAnchor", (0, 1.67, 2.72))
    empty("EffectPoint", (0, 1.05, 1.5))
    finish("Market", p)


@model
def Workbench():
    """Workbench: a covered workshop corner on a plank deck. Laminated bench with a vise and
    tools, a pegboard of tools and axes, a skill-tree blueprint, grindstone, anvil on a stump,
    a sawhorse with a log and a hanging lantern. Front is +Y."""
    p = []
    rad = math.radians
    # Deck.
    p.append(span("Sill", -2.25, 2.25, -1.35, 1.35, 0.0, 0.1, "M_WoodDark"))
    for i in range(9):
        y0 = -1.3 + i * 0.29
        p.append(span("Deck", -2.2, 2.2, y0 + 0.01, y0 + 0.28, 0.1, 0.16, "M_Plank" if i % 2 else "M_Wood", 0.01))

    # Shed roof on four posts, sloping down to the front.
    for x in (-2.05, 2.05):
        p.append(box("BackPost", (0.18, 0.18, 3.1), (x, -1.15, 1.7), "M_WoodDark", 0.02))
        p.append(box("FrontPost", (0.16, 0.16, 2.62), (x, 1.15, 1.46), "M_WoodDark", 0.02))
        p.append(span("SideRail", x - 0.08, x + 0.08, -1.2, 1.2, 2.62, 2.74, "M_WoodDark"))
    tilt = math.atan2(0.48, 2.9)
    p.append(box("Roof", (4.7, 3.1, 0.1), (0, 0, 3.0), "M_Roof", rot=(-tilt, 0, 0)))
    for k in range(6):
        y = -1.3 + k * 0.5
        z = 3.0 - math.tan(tilt) * y + 0.07
        p.append(box("Shingle", (4.72, 0.46, 0.04), (0, y + 0.1, z), "M_WoodDark" if k % 2 else "M_Roof", rot=(-tilt, 0, 0)))
    p.append(box("Fascia", (4.72, 0.06, 0.2), (0, 1.55, 2.72), "M_WoodDark"))

    # Pegboard wall with peg holes.
    p.append(span("Wall", -2.0, 2.0, -1.26, -1.12, 0.16, 2.75, "M_WoodDark"))
    p.append(span("Pegboard", -1.85, 1.85, -1.12, -1.08, 1.1, 2.55, "M_Plank"))
    for i in range(12):
        for j in range(5):
            p.append(box("Peg", (0.025, 0.012, 0.025), (-1.7 + i * 0.31, -1.072, 1.25 + j * 0.27), "M_WoodDark"))
    # Axe rack: three axes of rising quality.
    for k, (x, head) in enumerate([(-1.45, "M_Steel"), (-1.1, "M_PaintRed"), (-0.75, "M_Gold")]):
        p.append(box("AxeHandle", (0.05, 0.05, 0.85), (x, -1.03, 1.75), "M_Wood"))
        p.append(box("AxeHead", (0.05, 0.24, 0.16), (x + 0.05, -1.02, 2.12), head, 0.01, rot=(0, 0, rad(90))))
        p.append(box("AxeGrip", (0.06, 0.06, 0.16), (x, -1.03, 1.4), "M_Leather"))
    # Hand saw, hammer, chisels and a coil of rope.
    p.append(box("SawBlade", (0.62, 0.015, 0.16), (0.05, -1.04, 2.2), "M_Steel", rot=(0, rad(-8), 0)))
    p.append(box("SawHandle", (0.16, 0.04, 0.14), (-0.3, -1.04, 2.24), "M_Wood", 0.01))
    p.append(box("HammerHandle", (0.04, 0.04, 0.38), (0.6, -1.04, 1.7), "M_Wood"))
    p.append(box("HammerHead", (0.16, 0.06, 0.06), (0.6, -1.03, 1.9), "M_MetalDark"))
    for i in range(4):
        p.append(box("Chisel", (0.025, 0.025, 0.2 + i * 0.02), (0.85 + i * 0.09, -1.04, 1.45), "M_Steel"))
        p.append(box("ChiselGrip", (0.04, 0.04, 0.08), (0.85 + i * 0.09, -1.04, 1.6 + i * 0.01), "M_Wood"))
    p.append(fcyl("Rope", 0.17, 0.06, (1.55, -1.03, 1.8), "M_Rope", rot=(rad(90), 0, 0), verts=14))
    p.append(fcyl("RopeHole", 0.09, 0.07, (1.55, -1.02, 1.8), "M_WoodDark", rot=(rad(90), 0, 0), verts=12))
    # Blueprint of the skill tree, pinned on the board.
    p.append(box("Blueprint", (0.62, 0.012, 0.44), (1.2, -1.06, 2.3), "M_PaintBlue"))
    for a, b in [((1.2, 2.14), (1.2, 2.3)), ((1.2, 2.3), (1.02, 2.44)), ((1.2, 2.3), (1.38, 2.44))]:
        p.append(ftube("Line", (a[0], -1.05, a[1]), (b[0], -1.05, b[1]), 0.008, 0.008, "M_Canvas", 4))
    for x, z in [(1.2, 2.14), (1.2, 2.3), (1.02, 2.44), (1.38, 2.44)]:
        p.append(fcyl("Node", 0.03, 0.01, (x, -1.048, z), "M_Canvas", rot=(rad(90), 0, 0), verts=10))

    # The bench: laminated top, legs, stretchers, lower shelf, vise and dog holes.
    for i in range(7):
        y0 = -0.45 + i * 0.15
        p.append(span("Top", -1.45, 1.45, y0, y0 + 0.15, 0.94, 1.06, "M_Plank" if i % 2 else "M_Wood", 0.008))
    for x in (-1.3, 1.3):
        for y in (-0.38, 0.5):
            p.append(box("Leg", (0.14, 0.14, 0.8), (x, y, 0.54), "M_WoodDark", 0.015))
        p.append(span("Stretcher", x - 0.06, x + 0.06, -0.38, 0.5, 0.3, 0.4, "M_WoodDark"))
    p.append(span("Shelf", -1.3, 1.3, -0.4, 0.52, 0.4, 0.46, "M_Plank"))
    for k in range(3):
        p.append(box("ShelfBoard", (1.6, 0.22, 0.05), (-0.3, -0.1 + k * 0.03, 0.49 + k * 0.05), "M_WoodCut", 0.005))
    p.append(box("Toolbox", (0.55, 0.3, 0.26), (0.75, 0.1, 0.59), "M_PaintRed", 0.02))
    p.append(box("ToolboxHandle", (0.4, 0.04, 0.04), (0.75, 0.1, 0.76), "M_MetalDark"))
    for i in range(4):
        p.append(fcyl("DogHole", 0.025, 0.01, (-1.1 + i * 0.6, 0.45, 1.065), "M_WoodDark", verts=8))
    p.append(box("ViseJaw", (0.42, 0.1, 0.24), (-1.15, 0.67, 0.95), "M_Wood", 0.01))
    p.append(fcyl("ViseScrew", 0.03, 0.4, (-1.15, 0.72, 0.95), "M_Steel", rot=(rad(90), 0, 0), verts=8))
    p.append(box("ViseBar", (0.36, 0.035, 0.035), (-1.15, 0.9, 0.95), "M_Wood"))
    # Things on the bench: a plane, a mallet, an axe handle in progress, shavings.
    p.append(box("Plane", (0.32, 0.08, 0.08), (0.2, 0.15, 1.1), "M_Wood", 0.01))
    p.append(box("PlaneBlade", (0.04, 0.07, 0.1), (0.24, 0.15, 1.13), "M_Steel", rot=(0, rad(30), 0)))
    p.append(box("PlaneKnob", (0.06, 0.05, 0.08), (0.06, 0.15, 1.17), "M_WoodDark"))
    p.append(box("MalletHead", (0.18, 0.1, 0.1), (-0.5, 0.25, 1.11), "M_WoodDark", 0.01))
    p.append(box("MalletHandle", (0.04, 0.3, 0.04), (-0.5, 0.05, 1.08), "M_Wood"))
    p.append(ftube("NewHandle", (0.6, 0.35, 1.09), (1.25, 0.25, 1.09), 0.035, 0.028, "M_WoodCut", 7))
    for i, (x, y) in enumerate([(0.75, 0.35), (0.9, 0.2), (1.05, 0.4), (0.35, 0.4)]):
        p.append(box("Shaving", (0.09, 0.05, 0.015), (x, y, 1.07), "M_WoodCut", rot=(0, 0, rad(i * 37))))
    for i, (x, y) in enumerate([(0.4, 0.75), (0.7, 0.9), (-0.2, 0.85), (1.0, 0.7)]):
        p.append(box("FloorShaving", (0.12, 0.06, 0.02), (x, y, 0.17), "M_WoodCut", rot=(0, 0, rad(i * 53))))

    # Grindstone (right) and anvil on a stump (left).
    p.append(box("GrindFrameL", (0.07, 0.4, 0.75), (1.68, 0.6, 0.53), "M_Wood"))
    p.append(box("GrindFrameR", (0.07, 0.4, 0.75), (2.02, 0.6, 0.53), "M_Wood"))
    p.append(disc_x("Grind", 0.33, 0.16, (1.85, 0.6, 0.88), "M_Stone", 18))
    p.append(disc_x("GrindAxle", 0.05, 0.5, (1.85, 0.6, 0.88), "M_MetalDark", 8))
    p.append(box("Crank", (0.04, 0.04, 0.24), (2.1, 0.6, 0.98), "M_MetalDark"))
    p.append(box("Trough", (0.36, 0.5, 0.16), (1.85, 0.6, 0.36), "M_WoodDark", 0.01))
    p.append(fcyl("Stump", 0.3, 0.55, (-1.85, 0.55, 0.43), "M_Bark", verts=10))
    p.append(fcyl("StumpTop", 0.27, 0.02, (-1.85, 0.55, 0.71), "M_WoodCut", verts=10))
    p.append(box("Anvil", (0.42, 0.2, 0.16), (-1.85, 0.55, 0.88), "M_MetalDark", 0.02))
    p.append(box("AnvilWaist", (0.2, 0.14, 0.12), (-1.85, 0.55, 0.76), "M_MetalDark"))
    p.append(ftube("Horn", (-1.64, 0.55, 0.9), (-1.42, 0.55, 0.92), 0.08, 0.015, "M_MetalDark", 8))

    # Sawhorse with a log, just off the deck.
    for x in (2.75, 3.45):
        for s in (-1, 1):
            p.append(box("HorseLeg", (0.06, 0.06, 0.8), (x, 1.0 + s * 0.15, 0.38), "M_Wood", rot=(rad(18) * s, 0, 0)))
    p.append(span("HorseBeam", 2.65, 3.55, 0.94, 1.06, 0.74, 0.82, "M_Wood"))
    p.extend(log_x("HorseLog", 0.16, 1.2, (3.1, 1.0, 0.98), "M_Bark", 9))

    # Hanging lantern under the roof.
    p.append(box("LanternChain", (0.02, 0.02, 0.35), (0, 0.65, 2.62), "M_MetalDark"))
    p.append(box("LanternCap", (0.2, 0.2, 0.05), (0, 0.65, 2.43), "M_MetalDark", 0.01))
    p.append(box("LanternGlow", (0.12, 0.12, 0.18), (0, 0.65, 2.3), "M_Glow"))
    p.append(box("LanternBase", (0.18, 0.18, 0.04), (0, 0.65, 2.19), "M_MetalDark"))

    # Sign standing on the front edge of the roof, clear of the view into the workshop.
    p.append(box("SignBoard", (2.0, 0.07, 0.42), (0, 1.5, 3.08), "M_Sign", 0.02))
    p.append(box("SignFrame", (2.1, 0.05, 0.52), (0, 1.46, 3.08), "M_WoodDark"))
    for x in (-0.8, 0.8):
        p.append(box("SignPost", (0.06, 0.06, 0.5), (x, 1.42, 2.88), "M_WoodDark"))

    # Collisions.
    p.append(col_span("Bench", -1.5, 1.5, -0.5, 0.75, 0.0, 1.1))
    p.append(col_span("Wall", -2.2, 2.2, -1.4, -1.0, 0.0, 3.0))
    p.append(col_span("Grind", 1.6, 2.15, 0.3, 0.9, 0.0, 1.25))
    p.append(col_span("Anvil", -2.15, -1.4, 0.25, 0.85, 0.0, 1.0))
    p.append(col_span("Sawhorse", 2.6, 3.65, 0.7, 1.3, 0.0, 1.15))
    for x in (-2.05, 2.05):
        p.append(col_span("FrontPost", x - 0.12, x + 0.12, 1.03, 1.27, 0.0, 2.8))
    empty("SignAnchor", (0, 1.54, 3.08))
    finish("Workbench", p)


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


# ----------------------------------------------------------------- production chain: furniture and cabins

def rot2(x, y, yaw):
    c, s = math.cos(yaw), math.sin(yaw)
    return (x * c - y * s, x * s + y * c)


def chair(name, loc, yaw=0.0, wood="M_Wood", seat="M_Plank"):
    """A ladder-back chair (about 0.5 x 0.5 x 1.0), front +Y before yaw."""
    p = []
    ox, oy, oz = loc

    def at(x, y, z):
        rx, ry = rot2(x, y, yaw)
        return (ox + rx, oy + ry, oz + z)
    for (x, y) in ((-0.2, 0.2), (0.2, 0.2)):
        p.append(box(name + "Leg", (0.05, 0.05, 0.45), at(x, y, 0.225), wood, rot=(0, 0, yaw)))
    for x in (-0.2, 0.2):
        p.append(box(name + "Post", (0.05, 0.05, 1.0), at(x, -0.2, 0.5), wood, rot=(0, 0, yaw)))
    p.append(box(name + "Seat", (0.5, 0.48, 0.05), at(0, 0, 0.47), seat, 0.01, rot=(0, 0, yaw)))
    for z in (0.68, 0.82, 0.95):
        p.append(box(name + "Slat", (0.42, 0.03, 0.06), at(0, -0.2, z), seat, rot=(0, 0, yaw)))
    p.append(box(name + "Rung", (0.42, 0.03, 0.03), at(0, 0.2, 0.15), wood, rot=(0, 0, yaw)))
    return p


def table(name, loc, size=(0.95, 0.65), h=0.74, yaw=0.0, wood="M_Wood", top="M_Plank"):
    p = []
    ox, oy, oz = loc
    w, d = size

    def at(x, y, z):
        rx, ry = rot2(x, y, yaw)
        return (ox + rx, oy + ry, oz + z)
    p.append(box(name + "Top", (w, d, 0.05), at(0, 0, h - 0.025), top, 0.01, rot=(0, 0, yaw)))
    p.append(box(name + "Apron", (w - 0.12, d - 0.12, 0.08), at(0, 0, h - 0.09), wood, rot=(0, 0, yaw)))
    for (x, y) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p.append(box(name + "Leg", (0.06, 0.06, h - 0.05), at(x * (w / 2 - 0.07), y * (d / 2 - 0.07), (h - 0.05) / 2), wood, rot=(0, 0, yaw)))
    return p


def pallet(name, size, loc):
    w, d = size
    p = []
    for i in range(5):
        y = loc[1] - d / 2 + 0.08 + i * (d - 0.16) / 4
        p.append(box(name + "Top", (w, 0.14, 0.03), (loc[0], y, loc[2] + 0.115), "M_Plank"))
    for x in (-1, 0, 1):
        p.append(box(name + "Block", (0.12, d, 0.1), (loc[0] + x * (w / 2 - 0.08), loc[1], loc[2] + 0.05), "M_WoodDark"))
    return p


@model
def FurniturePiece():
    """Stock pile piece: a pallet with a table and two chairs on it."""
    p = pallet("Pallet", (1.3, 1.0), (0, 0, 0))
    p += table("Table", (0.0, 0.05, 0.13), (0.95, 0.6), 0.62)
    p += chair("ChairA", (-0.32, -0.2, 0.13), 0.2)
    p += chair("ChairB", (0.33, -0.18, 0.13), -0.25, "M_WoodDark")
    finish("FurniturePiece", p)


@model
def CabinKit():
    """Stock pile piece: a flat-packed cabin on a pallet: log wall panels, a door, roof panels and
    red straps."""
    p = pallet("Pallet", (1.7, 1.2), (0, 0, 0))
    z = 0.13
    for i in range(4):  # log wall panels lying flat
        for k in range(3):
            p.append(fcyl("Log", 0.06, 1.6, (0, -0.4 + k * 0.12 + (i % 2) * 0.06, z + 0.06 + i * 0.12), "M_Bark", rot=(0, math.radians(90), 0), verts=7))
        p.append(box("Panel", (1.55, 0.42, 0.1), (0, 0.32, z + 0.06 + i * 0.12), "M_Wood" if i % 2 else "M_Plank", 0.01))
    top = z + 0.5
    p.append(box("Door", (0.5, 0.9, 0.05), (-0.45, 0.0, top + 0.03), "M_WoodDark", 0.01))
    p.append(box("Knob", (0.05, 0.05, 0.04), (-0.27, 0.0, top + 0.07), "M_Gold"))
    p.append(box("Window", (0.45, 0.4, 0.05), (0.35, 0.18, top + 0.03), "M_Glass"))
    p.append(box("Frame", (0.53, 0.48, 0.04), (0.35, 0.18, top + 0.015), "M_Wood"))
    for k in range(2):
        p.append(box("Roof", (1.5, 0.5, 0.05), (0.05, -0.3 + k * 0.08, top + 0.07 + k * 0.05), "M_Roof", 0.01))
    for x in (-0.55, 0.55):
        p.append(box("Strap", (0.06, 1.24, 0.02), (x, 0, top + 0.17), "M_PaintRed"))
        for s in (-1, 1):
            p.append(box("Strap", (0.06, 0.02, 0.6), (x, s * 0.61, z + 0.33), "M_PaintRed"))
    p.append(box("Tag", (0.2, 0.02, 0.14), (0.0, 0.62, z + 0.35), "M_Canvas"))
    finish("CabinKit", p)


@model
def Workshop():
    """Carpentry Workshop (machine "workshop"): an open timber shed with a lean-to roof. Planks
    come in on the left, go through the planer and the lathe, and finished chairs and tables
    leave on the right. Moving parts: Spindle (lathe, spins about X), Wheel (band saw, about X),
    Board (slides along Y through the planer), Hammer (swings about X). Front is +Y."""
    w, d = 5.0, 4.2
    base = [span("Floor", -w / 2, w / 2, -d / 2, d / 2, 0.0, 0.18, "M_Plank", 0.02)]
    for i in range(9):
        x = -w / 2 + 0.3 + i * 0.55
        base.append(span("Seam", x, x + 0.02, -d / 2, d / 2, 0.18, 0.185, "M_WoodDark"))
    for x in (-w / 2 + 0.12, w / 2 - 0.12):
        base.append(box("PostB", (0.2, 0.2, 3.6), (x, -d / 2 + 0.12, 1.98), "M_WoodDark", 0.02))
        base.append(box("PostF", (0.2, 0.2, 2.8), (x, d / 2 - 0.12, 1.58), "M_WoodDark", 0.02))
        base.append(box("Brace", (0.08, 0.08, 1.0), (x, d / 2 - 0.5, 2.7), "M_WoodDark", rot=(math.radians(-45), 0, 0)))
    for y, z in ((-d / 2 + 0.12, 3.78), (d / 2 - 0.12, 2.98)):
        base.append(span("Beam", -w / 2, w / 2, y - 0.11, y + 0.11, z - 0.1, z + 0.1, "M_WoodDark"))
    slope = math.atan2(0.8, d)
    base.append(box("Roof", (w + 0.7, d + 0.9, 0.12), (0, 0.1, 3.5), "M_Roof", rot=(-slope, 0, 0)))
    for k in range(6):
        y = -d / 2 + 0.1 + k * 0.82
        base.append(box("Shingle", (w + 0.72, 0.4, 0.04), (0, y, 3.9 - (y + d / 2) * 0.8 / d + 0.03), "M_WoodDark" if k % 2 else "M_Roof", rot=(-slope, 0, 0)))
    # Back wall with a tool board.
    base.append(span("BackWall", -w / 2 + 0.2, w / 2 - 0.2, -d / 2 + 0.05, -d / 2 + 0.17, 0.18, 3.7, "M_Wood"))
    for i in range(8):
        x = -w / 2 + 0.45 + i * 0.58
        base.append(span("Batten", x, x + 0.07, -d / 2 + 0.17, -d / 2 + 0.2, 0.18, 3.65, "M_WoodDark"))
    base.append(span("ToolBoard", -1.6, 1.6, -d / 2 + 0.2, -d / 2 + 0.24, 1.45, 2.45, "M_Canvas"))
    for i, (x, h) in enumerate([(-1.3, 0.5), (-0.9, 0.42), (-0.5, 0.6), (0.0, 0.36), (0.45, 0.55), (0.9, 0.46), (1.3, 0.4)]):
        base.append(box("ToolHandle", (0.05, 0.04, h), (x, -d / 2 + 0.28, 1.95), "M_Wood"))
        base.append(box("ToolHead", (0.16 if i % 2 else 0.1, 0.04, 0.1), (x, -d / 2 + 0.28, 1.95 + h / 2), "M_Steel" if i % 3 else "M_MetalDark"))
    # Big saw blade hanging on the wall.
    base.append(fcyl("WallSaw", 0.35, 0.03, (1.9, -d / 2 + 0.26, 2.3), "M_Steel", rot=(math.radians(90), 0, 0), verts=18))
    # Workbench along the back with a vice and clamps.
    base.append(span("BenchTop", -2.1, 0.2, -1.55, -0.85, 0.85, 0.95, "M_Plank", 0.01))
    for x in (-2.0, 0.1):
        for y in (-1.5, -0.9):
            base.append(box("BenchLeg", (0.08, 0.08, 0.67), (x, y, 0.515), "M_WoodDark"))
    base.append(span("BenchShelf", -2.05, 0.15, -1.5, -0.9, 0.35, 0.39, "M_Wood"))
    base.append(box("Vice", (0.22, 0.18, 0.16), (-1.9, -0.8, 1.0), "M_PaintBlue", 0.01))
    base.append(box("Clamp", (0.06, 0.25, 0.2), (-0.6, -1.2, 1.05), "M_PaintRed"))
    # Planer (left, takes the planks in).
    base.append(box("Planer", (0.8, 1.3, 0.75), (-1.55, 0.75, 0.56), "M_PaintGreen", 0.03))
    base.append(box("PlanerTop", (0.86, 1.36, 0.06), (-1.55, 0.75, 0.96), "M_MetalDark"))
    base.append(box("PlanerHood", (0.7, 0.4, 0.3), (-1.55, 0.75, 1.14), "M_PaintGreen", 0.02))
    # Lathe (middle front).
    base.append(box("LatheBed", (1.8, 0.3, 0.16), (0.5, 0.95, 0.95), "M_MetalDark"))
    for x in (-0.25, 1.25):
        base.append(box("LatheLeg", (0.3, 0.36, 0.78), (x, 0.95, 0.48), "M_PaintBlue", 0.02))
    base.append(box("Headstock", (0.36, 0.34, 0.36), (-0.25, 0.95, 1.2), "M_PaintBlue", 0.02))
    base.append(box("Tailstock", (0.22, 0.26, 0.26), (1.3, 0.95, 1.15), "M_PaintBlue", 0.02))
    base.append(box("ToolRest", (0.6, 0.06, 0.1), (0.55, 1.18, 1.08), "M_Steel"))
    # Plank stack (input) and finished furniture (output).
    for layer in range(5):
        base.append(box("InPlank", (0.32, 1.4, 0.07), (-2.15, 1.25 - (layer % 2) * 0.02, 0.22 + layer * 0.075), "M_Plank" if layer % 2 else "M_WoodCut", 0.008))
    base += chair("OutChair", (1.75, 1.45, 0.18), -0.5)
    base += chair("OutChair2", (2.15, 0.6, 0.18), -1.3, "M_WoodDark")
    base += table("OutTable", (1.65, -0.6, 0.18), (0.9, 0.6), 0.72, 0.3)
    base += chair("OutChair3", (1.65, -0.6, 0.92), 0.3)
    # Sawdust heap.
    base.append(ico("Sawdust", 0.35, (-0.6, 1.6, 0.1), "M_WoodCut", 1, (1.3, 1.0, 0.35), 0.1, 411))
    # Band saw (back right) with its wheel.
    base.append(box("BandSaw", (0.5, 0.5, 1.6), (1.0, -1.4, 1.0), "M_Paint", 0.03))
    base.append(box("BandTable", (0.7, 0.7, 0.05), (1.0, -1.2, 1.12), "M_MetalDark"))
    base.append(col("Bench", (2.4, 0.8, 1.0), (-0.95, -1.2, 0.5)))
    base.append(col("Planer", (0.9, 1.4, 1.3), (-1.55, 0.75, 0.65)))
    base.append(col("Lathe", (2.0, 0.5, 1.3), (0.5, 0.95, 0.65)))
    base.append(col("Back", (w, 0.3, 3.7), (0, -d / 2 + 0.12, 1.85)))
    for x in (-w / 2 + 0.12, w / 2 - 0.12):
        base.append(col("PostF", (0.24, 0.24, 2.8), (x, d / 2 - 0.12, 1.4)))
    base.append(col("Output", (1.2, 2.6, 1.0), (1.85, 0.4, 0.5)))
    join([o for o in base if not o.name.startswith("COL_")], "Base", (0, 0, 0))

    spindle = [fcyl("Turning", 0.09, 1.2, (0.5, 0.95, 1.2), "M_WoodCut", rot=(0, math.radians(90), 0), verts=10)]
    for k in range(4):
        spindle.append(fcyl("Bead", 0.12, 0.08, (0.05 + k * 0.3, 0.95, 1.2), "M_Wood", rot=(0, math.radians(90), 0), verts=10))
    part("Spindle", spindle, (0.5, 0.95, 1.2))
    wheel = [disc_x("WheelRim", 0.3, 0.06, (1.0, -1.15, 1.55), "M_MetalDark", 16)]
    for k in range(3):
        wheel.append(box("Spoke", (0.04, 0.56, 0.05), (1.0, -1.15, 1.55), "M_Paint", rot=(k * math.pi / 3, 0, 0)))
    part("Wheel", wheel, (1.0, -1.15, 1.55))
    part("Board", [box("Board", (0.3, 1.2, 0.06), (-1.55, 0.75, 1.02), "M_Plank", 0.008)], (-1.55, 0.75, 1.02))
    hammer = [box("Handle", (0.04, 0.04, 0.4), (-1.0, -1.2, 1.17), "M_Wood"), box("Head", (0.18, 0.06, 0.06), (-1.0, -1.2, 1.37), "M_MetalDark")]
    part("Hammer", hammer, (-1.0, -1.2, 0.97))
    empty("WorkPoint", (0.5, 1.15, 1.25))
    export("Workshop")


@model
def CabinBuilder():
    """Cabin Builder (machine "builder"): a gantry crane over a cabin going up on a plank pad.
    Furniture comes in on the left, finished flat-pack cabin kits are stacked on the right.
    Moving parts: Trolley (slides along X on the gantry beam) > Hook (bobs up and down with a
    wall panel). Front is +Y."""
    w, d = 6.4, 6.0
    base = [span("Pad", -w / 2, w / 2, -d / 2, d / 2, 0.0, 0.14, "M_Dirt", 0.03)]
    base.append(span("Deck", -1.9, 1.9, -1.9, 1.5, 0.14, 0.3, "M_Plank", 0.02))
    # The cabin under construction: three walls of logs and bare rafters.
    cw, cd = 3.2, 2.8
    for i in range(6):
        z = 0.45 + i * 0.27
        base.append(fcyl("LogB", 0.14, cw + 0.4, (0, -cd / 2 - 0.2, z), "M_Bark", rot=(0, math.radians(90), 0), verts=8))
        for s in (-1, 1):
            base.append(fcyl("LogS", 0.14, cd + 0.4, (s * cw / 2, -0.2, z + 0.13), "M_Bark", rot=(math.radians(90), 0, 0), verts=8))
    for i in range(3):  # the front wall is half done
        z = 0.45 + i * 0.27
        for s in (-1, 1):
            base.append(fcyl("LogF", 0.14, 1.0, (s * (cw / 2 - 0.4), cd / 2 - 0.2, z), "M_Bark", rot=(0, math.radians(90), 0), verts=8))
    top = 0.45 + 6 * 0.27
    for k in range(4):  # rafters
        x = -cw / 2 + 0.1 + k * (cw - 0.2) / 3
        for s in (-1, 1):
            base.append(box("Rafter", (0.1, cd / 2 + 0.5, 0.12), (x, -0.2 + s * (cd / 4 + 0.15), top + 0.6), "M_WoodCut", rot=(s * -0.65, 0, 0)))
    base.append(box("RidgeBeam", (cw + 0.4, 0.14, 0.14), (0, -0.2, top + 1.15), "M_WoodCut"))
    # Gantry crane: two A-frame legs and a beam along X.
    H = 6.4
    for x in (-w / 2 + 0.3, w / 2 - 0.3):
        for y in (-1.6, 1.4):
            base.append(ftube("Leg", (x, y + (-0.45 if y < 0 else 0.45), 0.14), (x, -0.1, H), 0.12, 0.1, "M_Paint", 8))
        base.append(box("Foot", (0.4, 4.2, 0.2), (x, -0.1, 0.24), "M_MetalDark"))
        base.append(box("Cross", (0.12, 2.6, 0.12), (x, -0.1, 2.6), "M_Paint"))
    base.append(box("GantryBeam", (w - 0.2, 0.32, 0.4), (0, -0.1, H + 0.2), "M_Paint", 0.02))
    base.append(box("BeamCap", (w - 0.1, 0.36, 0.06), (0, -0.1, H + 0.43), "M_MetalDark"))
    for x in (-w / 2 + 0.3, w / 2 - 0.3):
        base.append(box("Stripe", (0.34, 0.36, 0.42), (x, -0.1, H + 0.2), "M_MetalDark"))
    # Winch box on the right leg.
    base.append(box("Winch", (0.6, 0.6, 0.5), (w / 2 - 0.6, 0.9, 0.6), "M_PaintRed", 0.02))
    base.append(fcyl("Drum", 0.18, 0.5, (w / 2 - 0.6, 0.9, 1.0), "M_Steel", rot=(0, math.radians(90), 0), verts=12))
    # Input: furniture on the left; output: cabin kits on the right.
    base += table("InTable", (-1.9, 2.0, 0.14), (0.9, 0.6), 0.72, 1.57)
    base += chair("InChair", (-1.9, 2.0, 0.86), 0.4)
    base += chair("InChair2", (-1.0, 2.5, 0.14), 2.6, "M_WoodDark")
    for k in range(2):
        z = 0.14 + k * 0.62
        base += pallet("KitPallet", (1.5, 1.1), (1.75, 2.1, z))
        for j in range(3):
            base.append(box("KitPanel", (1.4, 1.0, 0.13), (1.75, 2.1, z + 0.2 + j * 0.13), "M_Wood" if j % 2 else "M_Plank", 0.01))
        base.append(box("KitStrap", (0.06, 1.12, 0.45), (1.75, 2.1, z + 0.33), "M_PaintRed"))
    # Sawhorse and spare logs at the back left.
    base.append(box("Sawhorse", (1.2, 0.1, 0.1), (-2.2, 0.4, 0.75), "M_Wood"))
    for x in (-2.7, -1.7):
        base.append(box("SawhorseLeg", (0.08, 0.5, 0.65), (x, 0.4, 0.44), "M_Wood"))
    base += log_x("Spare", 0.15, 1.6, (-2.2, -2.4, 0.3), "M_Bark") + log_x("Spare", 0.15, 1.6, (-2.2, -2.05, 0.3), "M_Bark")
    base += log_x("Spare", 0.15, 1.6, (-2.2, -2.22, 0.56), "M_Bark")
    base.append(col("Cabin", (cw + 0.5, cd + 0.4, top), (0, -0.2, top / 2)))
    for x in (-w / 2 + 0.3, w / 2 - 0.3):
        base.append(col("Leg", (0.5, 4.2, 2.0), (x, -0.1, 1.0)))
    base.append(col("Kits", (1.6, 1.2, 1.5), (1.75, 2.1, 0.75)))
    base.append(col("Furniture", (1.6, 1.4, 1.2), (-1.6, 2.2, 0.6)))
    join([o for o in base if not o.name.startswith("COL_")], "Base", (0, 0, 0))

    trolley = part("Trolley", [box("Carriage", (0.5, 0.5, 0.3), (0, -0.1, H - 0.1), "M_MetalDark", 0.02),
                               disc_x("Pulley", 0.12, 0.08, (0.0, -0.1, H - 0.3), "M_Steel", 10)], (0, -0.1, H - 0.1))
    hook = part("Hook", [ftube("Cable", (0, -0.1, H - 0.3), (0, -0.1, 4.9), 0.02, 0.02, "M_MetalDark", 4),
                         box("HookBlock", (0.16, 0.12, 0.22), (0, -0.1, 4.85), "M_Paint"),
                         box("Spreader", (1.6, 0.08, 0.08), (0, -0.1, 4.7), "M_MetalDark"),
                         box("Panel", (1.5, 0.14, 0.9), (0, -0.1, 4.2), "M_Plank", 0.01)], (0, -0.1, H - 0.3))
    parent(hook, trolley)
    empty("WorkPoint", (0, 1.1, 1.5))
    export("CabinBuilder")


# ----------------------------------------------------------------- yard buildings

@model
def Warehouse():
    """A big red lumber barn: board-and-batten walls, white trim, a tin gable roof with a cupola,
    big X-braced double doors, a hay loft door and a lean-to full of lumber. 10 x 7 m. Front is +Y."""
    w, d, h = 10.0, 7.0, 4.2
    p = [span("Base", -w / 2 - 0.1, w / 2 + 0.1, -d / 2 - 0.1, d / 2 + 0.1, 0.0, 0.3, "M_StoneDark", 0.03)]
    p.append(span("Walls", -w / 2, w / 2, -d / 2, d / 2, 0.3, h, "M_PaintRed"))
    for i in range(int(w / 0.5) + 1):  # battens on front and back
        x = -w / 2 + i * 0.5
        for y in (-d / 2 - 0.03, d / 2 + 0.03):
            p.append(span("Batten", x - 0.03, x + 0.03, y - 0.02, y + 0.02, 0.3, h, "M_Roof"))
    for i in range(int(d / 0.5) + 1):
        y = -d / 2 + i * 0.5
        for x in (-w / 2 - 0.03, w / 2 + 0.03):
            p.append(span("Batten", x - 0.02, x + 0.02, y - 0.03, y + 0.03, 0.3, h, "M_Roof"))
    # Gable ends (the roof runs along X).
    roof_h = 2.6
    for x in (-w / 2 + 0.1, w / 2 - 0.1):
        g = prism("Gable", [(-d / 2, h), (d / 2, h), (0, h + roof_h)], 0.2, "M_PaintRed")
        g.location.x = x
        p.append(g)
    slope = math.atan2(roof_h, d / 2)
    L = math.hypot(roof_h, d / 2) + 0.6
    for s in (-1, 1):
        p.append(box("Roof", (w + 0.8, L, 0.16), (0, s * d / 4, h + roof_h / 2 + 0.08), "M_MetalDark", rot=(-s * slope, 0, 0)))
        for k in range(7):
            f = (k + 0.5) / 7
            p.append(box("Rib", (w + 0.82, 0.05, 0.05), (0, s * (d / 2) * (1 - f), h + roof_h * f + 0.2), "M_Metal", rot=(-s * slope, 0, 0)))
    p.append(box("RidgeCap", (w + 0.9, 0.3, 0.12), (0, 0, h + roof_h + 0.12), "M_Metal"))
    # Cupola with a weather vane.
    p.append(span("Cupola", -0.6, 0.6, -0.6, 0.6, h + roof_h - 0.2, h + roof_h + 0.9, "M_PaintRed"))
    for s in (-1, 1):
        p.append(span("Louvre", -0.45, 0.45, s * 0.61 - 0.02, s * 0.61 + 0.02, h + roof_h + 0.15, h + roof_h + 0.75, "M_Canvas"))
    p.append(prism("CupolaRoof", [(-0.85, h + roof_h + 0.9), (0.85, h + roof_h + 0.9), (0, h + roof_h + 1.5)], 1.6, "M_MetalDark"))
    p.append(box("Vane", (0.04, 0.04, 0.8), (0, 0, h + roof_h + 1.9), "M_MetalDark"))
    p.append(box("VaneArrow", (0.6, 0.03, 0.12), (0.1, 0, h + roof_h + 2.1), "M_MetalDark"))
    # Trim.
    for x in (-w / 2 - 0.06, w / 2 + 0.06):
        for y in (-d / 2 - 0.06, d / 2 + 0.06):
            p.append(span("Corner", x - 0.1, x + 0.1, y - 0.1, y + 0.1, 0.3, h, "M_Canvas"))
    for y in (-d / 2 - 0.08, d / 2 + 0.08):
        p.append(span("Eave", -w / 2 - 0.1, w / 2 + 0.1, y - 0.05, y + 0.05, h - 0.2, h, "M_Canvas"))
    # Big double doors (front) with X braces.
    dy = d / 2 + 0.1
    for s in (-1, 1):
        cx = s * 1.0
        p.append(span("Door", cx - 1.0, cx + 1.0, dy - 0.05, dy + 0.05, 0.3, 3.4, "M_PaintRed"))
        for (a, b) in (((cx - 0.9, 0.4), (cx + 0.9, 3.3)), ((cx + 0.9, 0.4), (cx - 0.9, 3.3))):
            p.append(ftube("DoorX", (a[0], dy + 0.07, a[1]), (b[0], dy + 0.07, b[1]), 0.07, 0.07, "M_Canvas", 4))
        p.append(span("DoorFrame", cx - 1.0, cx + 1.0, dy + 0.02, dy + 0.12, 3.3, 3.45, "M_Canvas"))
        p.append(span("DoorFrame", cx - 1.0, cx + 1.0, dy + 0.02, dy + 0.12, 0.3, 0.42, "M_Canvas"))
        p.append(span("DoorFrame", cx + s * 0.92, cx + s * 1.02, dy + 0.02, dy + 0.12, 0.3, 3.45, "M_Canvas"))
    p.append(span("Track", -2.6, 2.6, dy + 0.1, dy + 0.18, 3.5, 3.62, "M_MetalDark"))
    p.append(span("Loft", -0.7, 0.7, dy - 0.02, dy + 0.06, 4.3, 5.5, "M_WoodDark"))
    p.append(span("LoftFrame", -0.8, 0.8, dy - 0.04, dy + 0.08, 5.45, 5.6, "M_Canvas"))
    p.append(box("Pulley", (0.12, 1.0, 0.12), (0, dy + 0.4, 6.05), "M_WoodDark"))
    # Windows.
    for x in (-3.6, 3.6):
        for y in (-d / 2 - 0.06, d / 2 + 0.06):
            p.append(span("Window", x - 0.45, x + 0.45, y - 0.03, y + 0.03, 2.0, 2.9, "M_Glass"))
            p.append(span("WinFrame", x - 0.52, x + 0.52, y - 0.02, y + 0.02, 1.93, 2.97, "M_Canvas"))
    # Lean-to with lumber on the right side.
    p.append(box("LeanRoof", (2.1, 4.6, 0.1), (w / 2 + 1.0, 0, 2.7), "M_MetalDark", rot=(0, math.radians(14), 0)))
    for y in (-2.1, 2.1):
        p.append(box("LeanPost", (0.15, 0.15, 2.5), (w / 2 + 1.8, y, 1.25), "M_WoodDark"))
    for layer in range(6):
        p.append(box("Lumber", (1.2, 3.8, 0.12), (w / 2 + 0.9, 0, 0.12 + layer * 0.13), "M_Plank" if layer % 2 else "M_WoodCut", 0.01))
    p.append(col("Barn", (w + 0.2, d + 0.2, h + roof_h), (0, 0, (h + roof_h) / 2)))
    p.append(col("Lean", (2.0, 4.6, 1.0), (w / 2 + 1.0, 0, 0.5)))
    finish("Warehouse", p)


@model
def WaterTower():
    """A wooden water tank on four splayed timber legs with a ladder and a spout."""
    p = []
    top = 5.2
    for (x, y) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p.append(ftube("Leg", (x * 1.7, y * 1.7, 0), (x * 1.05, y * 1.05, top), 0.13, 0.11, "M_WoodDark", 6))
        p.append(span("Footing", x * 1.7 - 0.25, x * 1.7 + 0.25, y * 1.7 - 0.25, y * 1.7 + 0.25, -0.1, 0.25, "M_Stone", 0.02))
    for z, r in ((1.6, 1.53), (3.4, 1.3)):
        for (a, b) in (((-r, -r), (r, -r)), ((r, -r), (r, r)), ((r, r), (-r, r)), ((-r, r), (-r, -r))):
            p.append(ftube("Rail", (a[0], a[1], z), (b[0], b[1], z), 0.06, 0.06, "M_Wood", 4))
    p.append(span("Deck", -1.5, 1.5, -1.5, 1.5, top - 0.1, top + 0.05, "M_Plank", 0.02))
    p.append(fcyl("Tank", 1.35, 2.4, (0, 0, top + 1.25), "M_Wood", verts=20))
    for z in (top + 0.35, top + 1.25, top + 2.15):
        p.append(fcyl("Hoop", 1.38, 0.07, (0, 0, z), "M_MetalDark", verts=20))
    p.append(cone("Roof", 1.6, 0.1, 1.0, (0, 0, top + 2.95), "M_Roof", 16))
    p.append(fcyl("Finial", 0.06, 0.4, (0, 0, top + 3.6), "M_MetalDark", verts=6))
    for k in range(int(top / 0.35)):  # ladder up the front
        p.append(box("Rung", (0.5, 0.04, 0.04), (0, 1.75, 0.3 + k * 0.35), "M_Wood"))
    for x in (-0.25, 0.25):
        p.append(box("Stile", (0.06, 0.06, top + 0.2), (x, 1.75, (top + 0.2) / 2), "M_WoodDark"))
    p.append(ftube("Spout", (1.3, 0, top + 0.6), (2.2, 0, top + 0.2), 0.12, 0.1, "M_MetalDark", 8))
    p.append(ftube("Chain", (2.2, 0, top + 0.2), (2.2, 0, 2.6), 0.015, 0.015, "M_MetalDark", 4))
    for (x, y) in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p.append(col("Leg", (0.4, 0.4, 2.4), (x * 1.6, y * 1.6, 1.2)))
    finish("WaterTower", p)


@model
def LumberRack():
    """An outdoor drying rack: three stands, plank layers on stickers and a small tin roof."""
    L = 4.0
    p = []
    for x in (-L / 2 + 0.2, 0, L / 2 - 0.2):
        for y in (-0.6, 0.6):
            p.append(box("Post", (0.12, 0.12, 2.3), (x, y, 1.15), "M_WoodDark"))
        for z in (0.35, 1.05, 1.75):
            p.append(box("Arm", (0.1, 1.4, 0.1), (x, 0, z), "M_Wood"))
    for z0 in (0.4, 1.1, 1.8):
        for layer in range(3):
            z = z0 + 0.05 + layer * 0.1
            p.append(box("Plank", (L - 0.1, 1.15, 0.06), (0, 0, z), "M_Plank" if layer % 2 else "M_WoodCut", 0.008))
            if layer < 2:
                for x in (-1.2, 0, 1.2):
                    p.append(box("Sticker", (0.05, 1.15, 0.04), (x, 0, z + 0.05), "M_WoodDark"))
    p.append(box("Roof", (L + 0.4, 1.8, 0.06), (0, 0, 2.38), "M_MetalDark", rot=(math.radians(8), 0, 0)))
    p.append(col("Rack", (L, 1.4, 2.3), (0, 0, 1.15)))
    finish("LumberRack", p)


def wheel_y(name, r, loc, spokes=8):
    """A spoked cart wheel facing sideways (axle along Y)."""
    rx = (math.radians(90), 0, 0)
    parts = [fcyl(name + "Rim", r, 0.08, loc, "M_WoodDark", rot=rx, verts=16),
             fcyl(name + "Tyre", r + 0.02, 0.05, loc, "M_MetalDark", rot=rx, verts=16),
             fcyl(name + "Hub", r * 0.22, 0.14, loc, "M_MetalDark", rot=rx, verts=8)]
    for k in range(spokes // 2):
        parts.append(box(name + "Spoke", (r * 1.9, 0.04, 0.04), loc, "M_Wood", rot=(0, k * math.tau / spokes, 0)))
    return parts


@model
def Wagon():
    """A four-wheeled timber wagon loaded with logs. Long axis X."""
    p = [span("Bed", -1.3, 1.3, -0.65, 0.65, 0.7, 0.82, "M_Plank", 0.01)]
    for y in (-0.68, 0.68):
        p.append(span("Side", -1.3, 1.3, y - 0.04, y + 0.04, 0.82, 1.12, "M_Wood"))
        for x in (-1.25, -0.4, 0.4, 1.25):
            p.append(box("Stake", (0.08, 0.08, 0.7), (x, y, 1.05), "M_WoodDark"))
    for x in (-0.85, 0.85):
        p.append(box("Axle", (0.1, 1.6, 0.1), (x, 0, 0.45), "M_MetalDark"))
        for y in (-0.78, 0.78):
            p += wheel_y("Wheel", 0.42, (x, y, 0.45))
    p.append(ftube("Tongue", (1.3, 0, 0.6), (2.6, 0, 0.35), 0.05, 0.04, "M_WoodDark", 6))
    p.append(box("Yoke", (0.08, 0.9, 0.08), (2.6, 0, 0.35), "M_WoodDark"))
    for k, (y, z) in enumerate([(-0.33, 1.0), (0.0, 1.0), (0.33, 1.0), (-0.17, 1.3), (0.17, 1.3)]):
        p += log_x("Load", 0.16, 2.3 - 0.1 * (k % 2), (0, y, z), "M_Bark")
    p.append(col("Wagon", (2.7, 1.5, 1.5), (0, 0, 0.75)))
    finish("Wagon", p)


@model
def Well():
    """A stone well with a little shingled roof, a crank and a bucket."""
    p = [fcyl("Ring", 0.85, 0.9, (0, 0, 0.45), "M_Stone", verts=14), fcyl("Water", 0.66, 0.05, (0, 0, 0.6), "M_Glass", verts=14)]
    for k in range(14):
        a = k * math.tau / 14
        p.append(box("Cap", (0.36, 0.22, 0.12), (math.cos(a) * 0.78, math.sin(a) * 0.78, 0.94), "M_StoneDark", rot=(0, 0, a + math.pi / 2)))
    for x in (-0.75, 0.75):
        p.append(box("Post", (0.12, 0.12, 2.0), (x, 0, 1.2), "M_WoodDark"))
    for s in (-1, 1):
        p.append(box("Roof", (1.9, 0.85, 0.08), (0, s * 0.35, 2.35), "M_Roof", rot=(-s * 0.55, 0, 0)))
    p.append(box("Ridge", (2.0, 0.1, 0.1), (0, 0, 2.58), "M_WoodDark"))
    p.append(fcyl("Crank", 0.08, 1.6, (0, 0, 1.75), "M_Wood", rot=(0, math.radians(90), 0), verts=8))
    p.append(box("Handle", (0.06, 0.06, 0.35), (0.86, 0, 1.6), "M_MetalDark"))
    p.append(ftube("Rope", (0, 0, 1.7), (0, 0, 1.15), 0.02, 0.02, "M_Rope", 4))
    p.append(cone("Bucket", 0.14, 0.18, 0.25, (0, 0, 1.02), "M_Wood", 10))
    p.append(col("Well", (1.8, 1.8, 1.0), (0, 0, 0.5)))
    finish("Well", p)


# ----------------------------------------------------------------- the far forests (2.1)
# Frostpine Peaks, Blossom Vale and the Crystal Wilds: their trees, stumps, scenery and the three
# axes that cut them.

COLORS.update({
    "M_LeafFrost": (0.74, 0.84, 0.88), "M_BarkCherry": (0.38, 0.21, 0.19), "M_LeafBlossom": (0.98, 0.66, 0.80),
    "M_LeafBlossomLight": (1.0, 0.84, 0.90), "M_BarkEmber": (0.17, 0.11, 0.09), "M_EmberGlow": (1.0, 0.45, 0.15),
    "M_LeafEmber": (0.96, 0.38, 0.12), "M_BarkCrystal": (0.45, 0.50, 0.64), "M_LeafCrystal": (0.52, 0.78, 0.92),
    "M_Crystal": (0.60, 0.88, 1.0), "M_CrystalViolet": (0.74, 0.56, 1.0), "M_Ice": (0.72, 0.88, 1.0),
    "M_Starsteel": (0.88, 0.90, 0.98),
})


@model
def Tree_Frostpine():
    """A tall pine with frosted needles and snow on every tier."""
    parts = [ftube("Trunk", (0, 0, 0), (0, 0, 9.2), 0.42, 0.08, "M_Bark", 8)]
    tiers = [(1.6, 2.6, 2.9), (3.0, 2.2, 2.7), (4.4, 1.8, 2.4), (5.7, 1.35, 2.1), (6.9, 0.95, 1.8), (8.0, 0.55, 1.5)]
    for i, (z, r, h) in enumerate(tiers):
        parts.append(cone("Tier", r, 0.05, h, (0, 0, z + h / 2), "M_LeafFrost" if i % 2 == 0 else "M_LeafPine", 8, (0, 0, i * 0.45), 0.08, 300 + i))
        parts.append(cone("Snow", r * 0.72, 0.03, h * 0.55, (0, 0, z + h * 0.62), "M_Snow", 8, (0, 0, i * 0.45 + 0.2), 0.05, 320 + i))
    finish("Tree_Frostpine", parts)


@model
def Tree_Cherry():
    """A gnarled cherry tree in full pink blossom."""
    pts = [(0, 0, 0), (0.3, 0.1, 1.6), (0.1, 0.3, 3.2), (0.2, 0.1, 4.2)]
    radii = [0.42, 0.36, 0.3, 0.24]
    parts = [ftube("Trunk", pts[i], pts[i + 1], radii[i], radii[i + 1], "M_BarkCherry", 8) for i in range(3)]
    for (x, y, z) in [(2.1, 0.5, 5.0), (-1.9, 0.9, 5.2), (0.3, -2.0, 5.1), (-0.5, 1.8, 5.6), (1.2, -1.2, 5.8)]:
        parts.append(ftube("Branch", (0.2, 0.1, 3.6), (x, y, z), 0.2, 0.08, "M_BarkCherry", 6))
    crown(parts, [(0, 0, 6.0, 2.0), (2.1, 0.5, 5.4, 1.5), (-1.9, 0.9, 5.6, 1.5), (0.3, -2.1, 5.5, 1.4), (-0.5, 1.9, 6.1, 1.4),
                  (1.2, -1.2, 6.5, 1.3), (0.2, 0.3, 7.2, 1.3)], ["M_LeafBlossom", "M_LeafBlossomLight"], 400)
    finish("Tree_Cherry", parts)


@model
def Tree_Emberwood():
    """A charred trunk with glowing cracks under a crown of fiery leaves."""
    parts = [ftube("Trunk", (0, 0, 0), (0.1, 0, 5.5), 0.55, 0.36, "M_BarkEmber", 9)]
    rnd = random.Random(13)
    for i in range(10):
        a = rnd.uniform(0, math.tau)
        z = rnd.uniform(0.4, 4.8)
        r = 0.55 - z * 0.035
        parts.append(box("Crack", (0.06, 0.08, rnd.uniform(0.5, 1.1)), (math.cos(a) * r + z * 0.018, math.sin(a) * r, z), "M_EmberGlow",
                         rot=(rnd.uniform(-0.2, 0.2), 0, a)))
    for (x, y, z) in [(2.0, 0.3, 6.8), (-1.9, 0.7, 7.0), (0.3, -2.0, 6.9), (-0.3, 1.9, 7.3)]:
        parts.append(ftube("Branch", (0.05, 0, 4.6), (x, y, z), 0.28, 0.12, "M_BarkEmber", 6))
    crown(parts, [(0, 0, 8.0, 2.3), (2.1, 0.3, 7.2, 1.7), (-2.0, 0.7, 7.4, 1.7), (0.3, -2.1, 7.3, 1.6), (-0.3, 2.0, 7.8, 1.6), (0.4, 0.2, 9.4, 1.4)],
          ["M_LeafEmber", "M_LeafAutumn", "M_LeafMaple"], 500)
    finish("Tree_Emberwood", parts)


@model
def Tree_Crystalwood():
    """A pale trunk whose crown grows glowing crystal shards."""
    pts = [(0, 0, 0), (0.15, 0.05, 3.0), (-0.1, 0.15, 6.0), (0.1, 0, 7.6)]
    radii = [0.55, 0.45, 0.34, 0.22]
    parts = [ftube("Trunk", pts[i], pts[i + 1], radii[i], radii[i + 1], "M_BarkCrystal", 8) for i in range(3)]
    for (x, y, z) in [(2.0, 0.4, 8.2), (-1.9, 0.6, 8.4), (0.3, -2.0, 8.3), (-0.2, 1.8, 8.8)]:
        parts.append(ftube("Branch", (0.05, 0.05, 6.2), (x, y, z), 0.24, 0.1, "M_BarkCrystal", 6))
    rnd = random.Random(21)
    centers = [(0, 0, 9.2, 2.0), (2.0, 0.4, 8.4, 1.5), (-1.9, 0.6, 8.6, 1.5), (0.3, -2.0, 8.5, 1.4), (-0.2, 1.8, 9.0, 1.4)]
    for i, (x, y, z, r) in enumerate(centers):
        parts.append(ico("Leaves", r, (x, y, z), "M_LeafCrystal", 1, (1, 1, 0.8), 0.12, 600 + i))
        for k in range(3):
            a = rnd.uniform(0, math.tau)
            d = r * 0.75
            parts.append(ico("Shard", r * 0.28, (x + math.cos(a) * d, y + math.sin(a) * d, z + rnd.uniform(-0.2, 0.6)),
                             "M_Crystal" if (i + k) % 2 else "M_CrystalViolet", 0, (0.55, 0.55, 1.7)))
    finish("Tree_Crystalwood", parts)


@model
def Stump_Cherry():
    stump("Stump_Cherry", "M_BarkCherry")


@model
def Stump_Ember():
    stump("Stump_Ember", "M_BarkEmber")


@model
def Stump_Crystal():
    stump("Stump_Crystal", "M_BarkCrystal")


@model
def Axe_5():
    """Frostbite Axe: an ice-blue bearded head with a frosted edge."""
    parts = [ftube("Handle", (0, 0, -0.22), (0, 0, 0.86), 0.025, 0.024, "M_WoodDark", 8)]
    for z in (-0.18, -0.12, -0.06):
        parts.append(fcyl("Grip", 0.029, 0.045, (0, 0, z), "M_Leather", verts=8))
    for z in (0.4, 0.62):
        parts.append(fcyl("Band", 0.029, 0.035, (0, 0, z), "M_Steel", verts=8))
    axe_head(parts, [(-0.07, 0.7), (0.05, 0.7), (0.13, 0.5), (0.27, 0.46), (0.28, 0.94), (0.06, 0.86), (-0.07, 0.87)], 0.055, "M_Ice", "M_Crystal")
    parts.append(box("Poll", (0.06, 0.07, 0.16), (0, -0.09, 0.8), "M_Ice"))
    finish("Axe_5", parts)


@model
def Axe_6():
    """Emberforge Axe: a double-bit head forged around a glowing ember core."""
    parts = [ftube("Handle", (0, 0, -0.24), (0, 0, 0.88), 0.026, 0.025, "M_BarkEmber", 8)]
    for z in (-0.2, 0.3, 0.6):
        parts.append(fcyl("Band", 0.03, 0.04, (0, 0, z), "M_Gold", verts=8))
    prof = [(0.0, 0.66), (0.09, 0.68), (0.26, 0.58), (0.28, 0.98), (0.09, 0.9), (0.0, 0.92)]
    axe_head(parts, prof, 0.058, "M_MetalDark", "M_EmberGlow")
    back = [(-p[0] * 0.7, p[1]) for p in reversed(prof)]
    parts.append(flat(prism("Head2", back, 0.058, "M_MetalDark", 'X')))
    parts.append(box("Edge2", (0.036, 0.02, 0.3), (0, -0.19, 0.79), "M_EmberGlow"))
    parts.append(box("Core", (0.075, 0.075, 0.28), (0, 0, 0.79), "M_EmberGlow"))
    finish("Axe_6", parts)


@model
def Axe_7():
    """Starsteel Axe: a silver crescent with a crystal edge and violet star gems."""
    parts = [ftube("Handle", (0, 0, -0.24), (0, 0.01, 0.9), 0.025, 0.024, "M_Starsteel", 8)]
    for z in (-0.2, -0.14, -0.08, -0.02):
        parts.append(fcyl("Grip", 0.029, 0.05, (0, 0, z), "M_PaintBlue", verts=8))
    prof = [(-0.06, 0.72), (0.06, 0.7), (0.16, 0.56), (0.32, 0.52), (0.33, 0.74), (0.32, 0.98), (0.16, 0.94), (0.06, 0.88), (-0.06, 0.88)]
    axe_head(parts, prof, 0.05, "M_Starsteel", "M_Crystal")
    for x in (0.03, -0.03):
        parts.append(ico("Gem", 0.035, (x, 0.04, 0.8), "M_CrystalViolet", 0))
    parts.append(cone("Spike", 0.03, 0.0, 0.12, (0, 0, 0.96), "M_Starsteel", 6))
    finish("Axe_7", parts)


@model
def Rock_Snow():
    parts = [ico("Rock", 0.9, (0, 0, 0.4), "M_StoneDark", 1, (1.25, 1.05, 0.75), 0.2, 31),
             ico("Snow", 0.8, (0.05, 0.0, 0.72), "M_Snow", 1, (1.25, 1.05, 0.35), 0.12, 32)]
    finish("Rock_Snow", parts)


@model
def Snow_Drift():
    parts = [ico("Drift", r, (x, y, 0), "M_Snow", 1, (1.6, 1.0, 0.28), 0.15, 40 + i)
             for i, (x, y, r) in enumerate([(0, 0, 0.9), (0.9, 0.3, 0.6), (-0.8, -0.2, 0.55)])]
    finish("Snow_Drift", parts)


@model
def Bush_Blossom():
    parts = []
    for i, (x, y, z, r) in enumerate([(0, 0, 0.5, 0.62), (0.55, 0.15, 0.38, 0.45), (-0.5, 0.1, 0.38, 0.45), (0.1, -0.45, 0.32, 0.4)]):
        parts.append(ico("Bush", r, (x, y, z), "M_LeafBlossom" if i % 2 == 0 else "M_LeafBlossomLight", 1, (1, 1, 0.85), 0.15, 70 + i))
    finish("Bush_Blossom", parts)


@model
def Petals():
    rnd = random.Random(50)
    parts = []
    for i in range(22):
        a = rnd.uniform(0, math.tau)
        d = rnd.uniform(0, 0.7)
        parts.append(box("Petal", (0.07, 0.05, 0.008), (math.cos(a) * d, math.sin(a) * d, 0.01), "M_LeafBlossom" if i % 3 else "M_LeafBlossomLight",
                         rot=(0, 0, rnd.uniform(0, math.tau))))
    finish("Petals", parts)


@model
def Crystal_Cluster():
    parts = [ico("Base", 0.45, (0, 0, 0.1), "M_StoneDark", 1, (1.3, 1.1, 0.5), 0.15, 60)]
    rnd = random.Random(61)
    for i in range(7):
        a = rnd.uniform(0, math.tau)
        d = rnd.uniform(0, 0.35)
        h = rnd.uniform(0.5, 1.2)
        t = rnd.uniform(0.1, 0.45)
        o = ico("Shard", 0.16, (math.cos(a) * d, math.sin(a) * d, h * 0.45), "M_Crystal" if i % 2 else "M_CrystalViolet", 0, (0.8, 0.8, h / 0.32))
        o.rotation_euler = (-math.sin(a) * t, math.cos(a) * t, 0)  # leans outwards
        parts.append(o)
    finish("Crystal_Cluster", parts)


@model
def Glowcap():
    parts = []
    for (x, y, h, r) in [(0, 0, 0.22, 0.12), (0.16, 0.1, 0.15, 0.09), (-0.12, 0.12, 0.12, 0.07), (0.05, -0.15, 0.1, 0.06)]:
        parts.append(fcyl("Stem", r * 0.32, h, (x, y, h / 2), "M_Canvas", verts=6))
        parts.append(cone("Cap", r, r * 0.25, r * 0.75, (x, y, h + r * 0.25), "M_Crystal", 8))
    finish("Glowcap", parts)


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

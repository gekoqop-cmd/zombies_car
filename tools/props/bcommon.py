"""Shared Blender helpers for prop sheets: atlas UV builder, materials, Props_All.blend, per-model FBX export."""
import bpy, bmesh, math, os, json, random
from mathutils import Vector, Matrix, Euler

ROOT = r"D:\zombi-caaar\models"
TEX = os.path.join(ROOT, "textures")
SRC = os.path.join(ROOT, "source")
BLEND = os.path.join(SRC, "Props_All.blend")
os.makedirs(SRC, exist_ok=True)


# ------------------------------------------------------------------ scene / file
def open_all():
    if os.path.exists(BLEND):
        bpy.ops.wm.open_mainfile(filepath=BLEND)
    else:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        sc = bpy.context.scene
        world = bpy.data.worlds.new("World"); sc.world = world
        world.use_nodes = True
        bg = world.node_tree.nodes.get("Background")
        bg.inputs[0].default_value = (0.22, 0.25, 0.32, 1); bg.inputs[1].default_value = 0.7
        for nm, en, col, rot in (("Sun", 4.0, (1.0, 0.93, 0.84), (50, 0, -35)), ("Fill", 0.8, (0.6, 0.7, 1.0), (60, 0, 150))):
            ld = bpy.data.lights.new(nm, 'SUN'); ld.energy = en; ld.color = col; ld.angle = math.radians(3)
            ob = bpy.data.objects.new(nm, ld); sc.collection.objects.link(ob)
            ob.rotation_euler = [math.radians(a) for a in rot]
    return bpy.context.scene


def fresh_collection(name):
    old = bpy.data.collections.get(name)
    if old:
        for ob in list(old.all_objects):
            data = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if data is not None and getattr(data, "users", 1) == 0:
                if isinstance(data, bpy.types.Mesh):
                    bpy.data.meshes.remove(data)
        bpy.data.collections.remove(old)
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    return c


def save_all():
    bpy.ops.wm.save_as_mainfile(filepath=BLEND, relative_remap=True)
    b1 = BLEND + "1"
    if os.path.exists(b1):
        os.remove(b1)


def material(name, tex, emit=0.0, rough=0.7, metal=0.0, vcol=False, alpha=False):
    old = bpy.data.materials.get(name)
    if old:
        bpy.data.materials.remove(old)
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    bs = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    bs.inputs["Roughness"].default_value = rough
    bs.inputs["Metallic"].default_value = metal
    img = bpy.data.images.load(os.path.join(TEX, tex), check_existing=True)
    img.reload()
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; n.location = (-500, 250)
    if vcol:
        vc = nt.nodes.new("ShaderNodeVertexColor"); vc.layer_name = "Col"; vc.location = (-500, -50)
        mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = 'RGBA'; mix.blend_type = 'MULTIPLY'
        mix.inputs[0].default_value = 1.0
        nt.links.new(n.outputs["Color"], mix.inputs[6]); nt.links.new(vc.outputs["Color"], mix.inputs[7])
        nt.links.new(mix.outputs[2], bs.inputs["Base Color"])
    else:
        nt.links.new(n.outputs["Color"], bs.inputs["Base Color"])
    if emit:
        nt.links.new(n.outputs["Color"], bs.inputs["Emission Color"])
        bs.inputs["Emission Strength"].default_value = emit
    if alpha:   # cutout foliage: texture alpha -> BSDF alpha (Unity: enable Alpha Clipping)
        nt.links.new(n.outputs["Alpha"], bs.inputs["Alpha"])
        for attr, val in (("surface_render_method", 'DITHERED'), ("blend_method", 'CLIP')):
            try:
                setattr(m, attr, val)
            except Exception:
                pass
    m.use_backface_culling = False
    return m


# ------------------------------------------------------------------ UV helpers
class Atlas:
    def __init__(self, reg, size, emit_size=256):
        self.reg, self.size, self.esize = reg, size, emit_size

    def uv(self, key, fx, fy, pad=3):
        if key == "__emit__":
            return (0.1 + 0.8 * min(max(fx, 0), 1), 0.1 + 0.8 * min(max(fy, 0), 1))
        x, y, w, h = self.reg[key]
        fx = min(max(fx, 0.0), 1.0); fy = min(max(fy, 0.0), 1.0)
        px = x + pad + fx * (w - 2 * pad)
        py = y + pad + (1 - fy) * (h - 2 * pad)
        return (px / self.size, 1 - py / self.size)


def face_fit(f):
    n = f.normal
    ax = max(range(3), key=lambda i: abs(n[i]))
    pts = []
    for l in f.loops:
        c = l.vert.co
        if ax == 0:
            pts.append((c.y if n.x > 0 else -c.y, c.z))
        elif ax == 1:
            pts.append((-c.x if n.y > 0 else c.x, c.z))
        else:
            pts.append((c.x, c.y if n.z > 0 else -c.y))
    us = [p[0] for p in pts]; vs = [p[1] for p in pts]
    du = (max(us) - min(us)) or 1; dv = (max(vs) - min(vs)) or 1
    return [((p[0] - min(us)) / du, (p[1] - min(vs)) / dv) for p in pts]


class B:
    """Geometry accumulator for one mesh part (local coords = model coords)."""

    def __init__(self, atlas):
        self.A = atlas
        self.bm = bmesh.new()
        self.bm.loops.layers.uv.verify()
        self.vcol = None

    def add(self, t, spec, xform=None, color=None, post=None):
        """spec: ('fit', reg) | ('vert', reg, z0, z1) | ('planar', reg, axis, (u0,u1,v0,v1)) |
                 ('remap', reg) | ('emit',) | ('along', reg, axis, a0, a1)"""
        if xform is not None:
            bmesh.ops.transform(t, matrix=xform, verts=t.verts)
        t.normal_update()
        uv = t.loops.layers.uv.verify()
        A = self.A
        spec_of = spec if callable(spec) else (lambda f, s=spec: s)     # per-face spec supported
        for f in t.faces:
            spec = spec_of(f)
            kind = spec[0]
            emit = kind == 'emit'
            f.material_index = 1 if emit else 0
            if kind in ('fit', 'emit'):
                key = "__emit__" if emit else spec[1]
                for l, (a, b) in zip(f.loops, face_fit(f)):
                    l[uv].uv = A.uv(key, a, b)
            elif kind == 'vert':
                _, key, z0, z1 = spec
                n = f.normal
                if abs(n.z) > 0.7:
                    for l, (a, b) in zip(f.loops, face_fit(f)):
                        l[uv].uv = A.uv(key, a, 0.96 + 0.04 * b)
                else:
                    for l, (a, b) in zip(f.loops, face_fit(f)):
                        l[uv].uv = A.uv(key, a, (l.vert.co.z - z0) / (z1 - z0))
            elif kind == 'along':         # v along a world axis (for horizontal beams)
                _, key, axis, a0, a1 = spec
                for l, (a, b) in zip(f.loops, face_fit(f)):
                    l[uv].uv = A.uv(key, b if abs(f.normal[axis]) < 0.7 else a, (l.vert.co[axis] - a0) / (a1 - a0))
            elif kind == 'planar':
                _, key, axis, (u0, u1, v0, v1) = spec
                ia, ib = [(1, 2), (0, 2), (0, 1)][axis]
                for l in f.loops:
                    c = l.vert.co
                    l[uv].uv = A.uv(key, (c[ia] - u0) / (u1 - u0), (c[ib] - v0) / (v1 - v0))
            elif kind == 'remap':
                flip = len(spec) > 2 and spec[2] == 'flipv'
                for l in f.loops:
                    a, b = l[uv].uv
                    l[uv].uv = A.uv(spec[1], a, 1 - b if flip else b)
            elif kind == 'dir':           # region chosen by face direction: keys -x +x -y +y -z +z *
                n = f.normal
                ax = max(range(3), key=lambda i: abs(n[i]))
                k = ('+' if n[ax] > 0 else '-') + 'xyz'[ax]
                key = spec[1].get(k, spec[1]['*'])
                if abs(n[ax]) < 0.9 and 'bevel' in spec[1]:      # chamfer strips get painted edge colors
                    key = spec[1]['bevel'] if n.z > -0.3 else spec[1].get('bevel_lo', spec[1]['bevel'])
                for l, (a, b) in zip(f.loops, face_fit(f)):
                    l[uv].uv = A.uv(key, a, b)
        if post is not None:
            bmesh.ops.transform(t, matrix=post, verts=t.verts)
        if color is not None:
            cl = t.loops.layers.color.get("Col") or t.loops.layers.color.new("Col")
            for f in t.faces:
                for l in f.loops:
                    l[cl] = color(l.vert.co) if callable(color) else color
        me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
        self.bm.from_mesh(me); bpy.data.meshes.remove(me)

    # --- primitives
    def grass(self, c, n, h, seed, spec, spread=0.08, width=0.05, segs=2, color=None, lean=(0.2, 0.55), splay=0.0):
        """Tuft of curved flat blades (UV: u across, v base->tip), double-sided by duplicated flipped faces."""
        r = random.Random(seed)
        t = bmesh.new(); uvl = t.loops.layers.uv.verify()
        for i in range(n):
            a = r.uniform(0, 2 * math.pi)
            base = Vector(c) + Vector((r.uniform(-spread, spread), r.uniform(-spread, spread), 0))
            out = Vector((math.cos(a), math.sin(a), 0)); side = Vector((-math.sin(a), math.cos(a), 0))
            hh = h * r.uniform(0.6, 1.1); bend = r.uniform(*lean); w = width * r.uniform(0.8, 1.25)
            rows = []
            for k in range(segs + 1):
                s = k / segs
                p = base + out * (bend * hh * (splay * s + (1 - splay) * s * s)) + Vector((0, 0, hh * s * (1 - 0.2 * s)))
                if k == segs:
                    rows.append([(t.verts.new(p), (0.5, 1.0))])
                else:
                    ww = w * (1 - 0.75 * s)
                    rows.append([(t.verts.new(p - side * ww / 2), (0.0, s)), (t.verts.new(p + side * ww / 2), (1.0, s))])
            for k in range(segs):
                lo, hi = rows[k], rows[k + 1]
                loop = [lo[0], lo[1]] + ([hi[0]] if len(hi) == 1 else [hi[1], hi[0]])
                for vs in (loop, list(reversed(loop))):
                    if vs is not loop:
                        vs = [(t.verts.new(v.co), uvv) for v, uvv in vs]
                    f = t.faces.new([v for v, _ in vs]); f.smooth = True
                    for l, (_, uvv) in zip(f.loops, vs):
                        l[uvl].uv = uvv
        self.add(t, spec, color=color)

    def box(self, size, center, spec, bevel=0.0, rot=(0, 0, 0), taper=None, uvlocal=False, bevel_axis=None):
        """taper: (sx, sy) scale of the top face relative to the bottom."""
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            sx, sy = (taper if (taper and v.co.z > 0) else (1, 1))
            v.co = Vector((v.co.x * size[0] * sx, v.co.y * size[1] * sy, v.co.z * size[2]))
        if bevel > 0:
            edges = list(t.edges)
            if bevel_axis is not None:      # only edges running along this axis (0=x, 1=y, 2=z)
                edges = [e for e in edges if abs((e.verts[0].co - e.verts[1].co).normalized()[bevel_axis]) > 0.9]
            bmesh.ops.bevel(t, geom=edges, offset=min(bevel, min(size) * 0.45), segments=1,
                            affect='EDGES', profile=0.5, clamp_overlap=True)
        m = Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4()
        if uvlocal:
            self.add(t, spec, post=m)
        else:
            self.add(t, spec, m)

    def box_between(self, p0, p1, spec, bevel=0.0):
        self.box([abs(b - a) for a, b in zip(p0, p1)], [(a + b) / 2 for a, b in zip(p0, p1)], spec, bevel)

    def lathe(self, profile, segs, spec, center=(0, 0, 0), rot=(0, 0, 0), smooth=True, color=None):
        t = bmesh.new(); uvl = t.loops.layers.uv.verify()
        lens = [0.0]
        for i in range(1, len(profile)):
            a, b = profile[i - 1], profile[i]
            lens.append(lens[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        tot = lens[-1] or 1
        rings = []
        for (r, z) in profile:
            if r < 1e-6:
                c = t.verts.new((0, 0, z)); rings.append([c] * (segs + 1))
            else:
                ring = [t.verts.new((r * math.cos(2 * math.pi * j / segs), r * math.sin(2 * math.pi * j / segs), z))
                        for j in range(segs)]
                rings.append(ring + [ring[0]])
        for i in range(len(rings) - 1):
            for j in range(segs):
                q = [rings[i][j], rings[i][j + 1], rings[i + 1][j + 1], rings[i + 1][j]]
                uvs = [(j / segs, lens[i] / tot), ((j + 1) / segs, lens[i] / tot),
                       ((j + 1) / segs, lens[i + 1] / tot), (j / segs, lens[i + 1] / tot)]
                vv, uu = [], []
                for a, b in zip(q, uvs):
                    if a not in vv:
                        vv.append(a); uu.append(b)
                if len(vv) < 3:
                    continue
                try:
                    f = t.faces.new(vv)
                except ValueError:
                    continue
                f.smooth = smooth
                for l, u in zip(f.loops, uu):
                    l[uvl].uv = u
        self.add(t, spec, Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4(), color)

    def cyl(self, r, h, center, spec, segs=8, rot=(0, 0, 0), smooth=False):
        self.lathe([(0, -h / 2), (r, -h / 2), (r, h / 2), (0, h / 2)], segs, spec, center, rot, smooth=smooth)

    def pipe(self, pts, radius, spec, segs=6, fillet=0.1, caps=True, sag=None):
        P = [Vector(p) for p in pts]
        path = [P[0]]
        for i in range(1, len(P) - 1):
            a, p, b = P[i - 1], P[i], P[i + 1]
            rf = min(fillet, (a - p).length * 0.45, (b - p).length * 0.45)
            if rf < 1e-4:
                path.append(p); continue
            p0 = p + (a - p).normalized() * rf; p2 = p + (b - p).normalized() * rf
            for k in range(4):
                s = k / 3
                path.append((1 - s) ** 2 * p0 + 2 * (1 - s) * s * p + s * s * p2)
        path.append(P[-1])
        t = bmesh.new(); rings = []; prev = None
        for i, p in enumerate(path):
            if i == 0:
                d = (path[1] - p).normalized()
            elif i == len(path) - 1:
                d = (p - path[i - 1]).normalized()
            else:
                d = ((path[i + 1] - p).normalized() + (p - path[i - 1]).normalized()).normalized()
            if prev is None:
                up = Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))
                nn = d.cross(up).normalized()
            else:
                nn = (prev - d * prev.dot(d)).normalized()
            prev = nn; bn = d.cross(nn)
            rings.append([t.verts.new(p + radius * (math.cos(2 * math.pi * j / segs) * nn +
                                                    math.sin(2 * math.pi * j / segs) * bn)) for j in range(segs)])
        for i in range(len(rings) - 1):
            for j in range(segs):
                f = t.faces.new((rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]))
                f.smooth = True
        if caps:
            t.faces.new(rings[0]); t.faces.new(rings[-1])
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self.add(t, spec)

    def catenary(self, a, b, sag, n=8):
        a, b = Vector(a), Vector(b)
        return [a.lerp(b, i / n) - Vector((0, 0, sag * 4 * (i / n) * (1 - i / n))) for i in range(n + 1)]

    def prism(self, poly2d, depth, plane_axis, center, spec_face, spec_side, spec_back=None, post=None):
        """Extrude a 2D polygon (u, v) along plane_axis ('y' = polygon in XZ, extruded in Y)."""
        t = bmesh.new()
        if plane_axis == 'y':
            to3 = lambda u, v, d: Vector((u, d, v))
        else:
            to3 = lambda u, v, d: Vector((d, u, v))
        front = [t.verts.new(to3(u, v, -depth / 2)) for u, v in poly2d]
        back = [t.verts.new(to3(u, v, depth / 2)) for u, v in poly2d]
        ff = t.faces.new(front); fb = t.faces.new(list(reversed(back)))
        n = len(poly2d)
        sides = [t.faces.new((front[i], back[i], back[(i + 1) % n], front[(i + 1) % n])) for i in range(n)]
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        bmesh.ops.translate(t, vec=Vector(center), verts=t.verts)
        # split into separately-mapped pieces
        tf = bmesh.new(); ts = bmesh.new(); tb = bmesh.new()
        me = bpy.data.meshes.new("_p"); t.to_mesh(me); t.free()
        for part, keep in ((tf, lambda f: f.index == 0), (tb, lambda f: f.index == 1), (ts, lambda f: f.index > 1)):
            part.from_mesh(me)
            part.faces.index_update()
            bmesh.ops.delete(part, geom=[f for f in part.faces if not keep(f)], context='FACES')
        bpy.data.meshes.remove(me)
        self.add(tf, spec_face, post=post); self.add(ts, spec_side, post=post); self.add(tb, spec_back or spec_side, post=post)

    def finish(self, name, mats, parent, collection, origin=None, wn=False):
        me = bpy.data.meshes.new(name)
        if origin is not None:
            bmesh.ops.translate(self.bm, vec=-Vector(origin), verts=self.bm.verts)
        if wn:   # hard corners (> 60 deg) stay sharp; chamfers and cylinders stay soft
            for e in self.bm.edges:
                if e.is_manifold and e.calc_face_angle(0) > math.radians(60):
                    e.smooth = False
        self.bm.to_mesh(me); self.bm.free()
        me.materials.append(mats[0])
        if any(p.material_index == 1 for p in me.polygons):
            me.materials.append(mats[1])
        ob = bpy.data.objects.new(name, me)
        collection.objects.link(ob)
        if wn:   # weighted normals: flat big faces, soft chamfers (cartoon hard-surface look)
            for p in me.polygons:
                p.use_smooth = True
            md = ob.modifiers.new("WeightedNormal", 'WEIGHTED_NORMAL')
            md.mode = 'FACE_AREA'; md.keep_sharp = True; md.weight = 80
        ob.parent = parent
        if origin is not None:
            ob.location = origin
        return ob


# ------------------------------------------------------------------ models / export
class Sheet:
    def __init__(self, name, atlas, atlas_file, emit_file=None, row_y=0.0, rough=0.7, vcol=False, wn=False,
                 alpha=False):
        self.name = name
        self.wn = wn
        self.col = fresh_collection("Sheet_" + name)
        self.A = atlas
        self.mats = (material(f"M_{name}_Atlas", atlas_file, rough=rough, vcol=vcol, alpha=alpha),
                     material(f"M_{name}_Emissive", emit_file, emit=3.5, rough=0.3) if emit_file else None)
        self.models = []
        self.row_y = row_y
        self.cursor = 0.0

    def model(self, name, group, width):
        root = bpy.data.objects.new(name, None)
        self.col.objects.link(root)
        root.empty_display_type = 'PLAIN_AXES'; root.empty_display_size = 0.3
        self.cursor += width / 2
        root.location = (self.cursor, self.row_y, 0)
        self.cursor += width / 2 + 0.8
        m = {"name": name, "group": group, "root": root, "parts": []}
        self.models.append(m)
        return m

    def part(self, model, name, builder, origin=None):
        ob = builder.finish(name, self.mats, model["root"], self.col, origin, self.wn)
        model["parts"].append(ob)
        return ob

    def new(self):
        return B(self.A)

    def export(self, stats_path):
        stats = {}
        for m in self.models:
            root = m["root"]
            loc = root.location.copy(); root.location = (0, 0, 0)
            bpy.context.view_layer.update()
            out_dir = os.path.join(ROOT, m["group"]); os.makedirs(out_dir, exist_ok=True)
            bpy.ops.object.select_all(action='DESELECT')
            objs = [root] + list(root.children_recursive)
            for o in objs:
                o.hide_set(False); o.select_set(True)
            bpy.context.view_layer.objects.active = root
            bpy.ops.export_scene.fbx(filepath=os.path.join(out_dir, m["name"] + ".fbx"), use_selection=True,
                                     object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                                     apply_unit_scale=True, bake_space_transform=True, axis_forward='-Z',
                                     axis_up='Y', mesh_smooth_type='FACE', path_mode='RELATIVE',
                                     embed_textures=False, add_leaf_bones=False, bake_anim=False,
                                     colors_type=getattr(self, "colors_type", 'LINEAR'))
            root.location = loc
            tris = 0; mats = set(); part_tris = {}
            for o in objs:
                if o.type == 'MESH':
                    o.data.calc_loop_triangles(); part_tris[o.name] = len(o.data.loop_triangles)
                    tris += part_tris[o.name]
                    mats |= {mm.name for mm in o.data.materials}
            stats[m["name"]] = {"group": m["group"], "tris": tris, "materials": sorted(mats),
                                "parts": [o.name for o in objs if o.type == 'MESH'], "part_tris": part_tris}
            print("EXPORTED", m["name"], tris)
        with open(stats_path, "w", encoding="utf8") as fh:
            json.dump(stats, fh, ensure_ascii=False, indent=1)
        return stats

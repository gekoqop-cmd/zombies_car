"""Builds the two parked wreck cars (referens/pickup.png, referens/sedan.png) in Blender and exports FBX.
Run:  blender -b --factory-startup --python build_wrecks.py
Cars face -Y in Blender (= +Z forward in Unity). Units: metres, origin on the ground under the body centre.
Two materials per car: tileable paint (box-projected, repeats every PAINT_TILE m) + the shared parts atlas.
"""
import bpy, bmesh, math, os, sys, random
from mathutils import Vector, Matrix, Euler

sys.path.append(os.path.dirname(os.path.abspath(__file__)))
from wreck_layout import *  # noqa

ROOT = r"D:\zombi-caaar"
OUT = os.path.join(ROOT, "models", "Vehicles")
TEX = os.path.join(OUT, "Textures")


# ------------------------------------------------------------------ materials
def make_mat(name, tex, rough=0.75, metal=0.2):
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
    n = nt.nodes.new("ShaderNodeTexImage"); n.image = img; n.location = (-400, 200)
    nt.links.new(n.outputs["Color"], bs.inputs["Base Color"])
    return m


# ------------------------------------------------------------------ UV helpers
def reg_uv(key, fx, fy, pad=3):
    x, y, w, h = REG[key]
    fx = min(max(fx, 0.0), 1.0); fy = min(max(fy, 0.0), 1.0)
    return ((x + pad + fx * (w - 2 * pad)) / PARTS, 1 - (y + pad + (1 - fy) * (h - 2 * pad)) / PARTS)


def box_uv(co, n):
    ax = max(range(3), key=lambda i: abs(n[i]))
    if ax == 0:
        u, v = (co.y if n.x > 0 else -co.y), co.z
    elif ax == 1:
        u, v = (-co.x if n.y > 0 else co.x), co.z
    else:
        u, v = co.x, co.y
    return (u / PAINT_TILE, v / PAINT_TILE)


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
            pts.append((c.x, c.y))
    us = [p[0] for p in pts]; vs = [p[1] for p in pts]
    du = (max(us) - min(us)) or 1; dv = (max(vs) - min(vs)) or 1
    return [((p[0] - min(us)) / du, (p[1] - min(vs)) / dv) for p in pts]


class B:
    """Accumulates one mesh. spec: 'paint' | ('fit', region) | ('remap', region)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.bm.loops.layers.uv.verify()

    def add(self, t, spec, xform=None):
        if xform is not None:
            bmesh.ops.transform(t, matrix=xform, verts=t.verts)
        t.normal_update()
        uv = t.loops.layers.uv.verify()
        for f in t.faces:
            f.material_index = 0 if spec == 'paint' else 1
            if spec == 'paint':
                for l in f.loops:
                    l[uv].uv = box_uv(l.vert.co, f.normal)
            elif spec[0] == 'fit':
                for l, (a, b) in zip(f.loops, face_fit(f)):
                    l[uv].uv = reg_uv(spec[1], a, b)
            else:   # remap: keep the generated 0..1 uv inside a region
                for l in f.loops:
                    a, b = l[uv].uv
                    l[uv].uv = reg_uv(spec[1], a, b)
        me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
        self.bm.from_mesh(me); bpy.data.meshes.remove(me)

    def box(self, size, center, spec, bevel=0.0, rot=(0, 0, 0)):
        t = bmesh.new()
        bmesh.ops.create_cube(t, size=1.0)
        for v in t.verts:
            v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
        if bevel > 0:
            bmesh.ops.bevel(t, geom=list(t.edges), offset=min(bevel, min(size) * 0.45), segments=1,
                            affect='EDGES', profile=0.5, clamp_overlap=True)
        self.add(t, spec, Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4())

    def lathe(self, profile, segs, spec, center=(0, 0, 0), rot=(0, 0, 0), smooth=True):
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
        self.add(t, spec, Matrix.Translation(Vector(center)) @ Euler(rot).to_matrix().to_4x4())

    def quad(self, pts, spec):
        t = bmesh.new()
        t.faces.new([t.verts.new(p) for p in pts])
        self.add(t, spec)

    def solid(self, verts, faces, spec):
        t = bmesh.new()
        vs = [t.verts.new(v) for v in verts]
        for fi in faces:
            t.faces.new([vs[i] for i in fi])
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        self.add(t, spec)

    def prism(self, prof, hw, spec, bevel=0.04):
        """Side profile [(s, z)] extruded across the car from -hw to +hw, side edges bevelled."""
        t = bmesh.new()
        L = [t.verts.new(P(s, -hw, z)) for s, z in prof]
        R = [t.verts.new(P(s, hw, z)) for s, z in prof]
        t.faces.new(L); t.faces.new(list(reversed(R)))
        n = len(prof)
        for i in range(n):
            j = (i + 1) % n
            t.faces.new((L[i], L[j], R[j], R[i]))
        bmesh.ops.recalc_face_normals(t, faces=t.faces)
        caps = [e for e in t.edges if all(abs(abs(v.co.x) - hw) < 1e-4 for v in e.verts)
                and abs(e.verts[0].co.x - e.verts[1].co.x) < 1e-4]
        bmesh.ops.bevel(t, geom=caps, offset=bevel, segments=1, affect='EDGES', profile=0.5, clamp_overlap=True)
        self.add(t, spec)

    def finish(self, name, mats, parent, loc=(0, 0, 0)):
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me); self.bm.free()
        for m in mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = loc
        ob.parent = parent
        return ob


def P(s, x, z):
    """car coords: s forward (front = -Y), x right, z up."""
    return (x, -s, z)


# ------------------------------------------------------------------ shared car parts
def arch(cs, c, n=8):
    """Wheel-arch arc from rear to front of the wheel at axle s = cs (profile runs front -> rear along the bottom)."""
    a0 = math.degrees(math.asin((c["sill"] - c["axle_z"]) / c["arch_r"]))
    out = []
    for k in range(n + 1):
        a = math.radians(a0 + (180 - 2 * a0) * k / n)
        out.append((cs + c["arch_r"] * math.cos(a), c["axle_z"] + c["arch_r"] * math.sin(a)))
    return out


def lower_profile(c, upper):
    """Closed side profile: front bottom, sill with both arches, rear bottom, then `upper` from rear to front."""
    prof = [(c["front"], c["sill"] + 0.08), (c["front"] - 0.05, c["sill"])]
    prof += arch(c["axle_f"], c) + arch(c["axle_r"], c)
    prof += [(c["rear"] + 0.05, c["sill"]), (c["rear"], c["sill"] + 0.08)]
    return prof + upper


def cabin(b, c):
    """Tapered cab hull + roof, returns helpers for the windows."""
    bf, br, tf, tr = c["cab_bf"], c["cab_br"], c["cab_tf"], c["cab_tr"]
    zb, zt, hb, ht = c["belt"], c["roof"], c["cab_hwb"], c["cab_hwt"]
    v = [P(bf, -hb, zb - 0.01), P(bf, hb, zb - 0.01), P(br, hb, zb - 0.01), P(br, -hb, zb - 0.01),
         P(tf, -ht, zt), P(tf, ht, zt), P(tr, ht, zt), P(tr, -ht, zt)]
    b.solid(v, [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)], 'paint')
    b.box((2 * ht + 0.06, tf - tr + 0.06, 0.06), P((tf + tr) / 2, 0, zt + 0.015), 'paint', bevel=0.025)

    def hw(z):
        return hb + (ht - hb) * (z - zb) / (zt - zb)

    def s_front(z):
        return bf + (tf - bf) * (z - zb) / (zt - zb)

    def s_rear(z):
        return br + (tr - br) * (z - zb) / (zt - zb)
    return hw, s_front, s_rear


def side_windows(b, c, hw, s_front, s_rear, wins, glass):
    z0, z1 = c["belt"] + 0.06, c["roof"] - 0.07
    for side in (-1, 1):
        for i, (sa, sb) in enumerate(wins):
            def edge(z, s_, front):
                if s_ is not None:
                    return s_
                return s_front(z) - 0.09 if front else s_rear(z) + 0.09
            for off, spec, grow in ((0.004, ('fit', 'rubber'), 0.03), (0.010, ('fit', glass[side][i]), 0.0)):
                pts = []
                for (z, front) in ((z0 - grow, True), (z0 - grow, False), (z1 + grow, False), (z1 + grow, True)):
                    s = edge(z, sa if front else sb, front) + (grow if front else -grow)
                    pts.append(P(s, side * (hw(z) + off), z))
                if side < 0:
                    pts = [pts[1], pts[0], pts[3], pts[2]]
                b.quad(pts, spec)


def slanted_pane(b, c, front, t0, t1, u0, u1, off, spec):
    sb = c["cab_bf"] if front else c["cab_br"]
    st = c["cab_tf"] if front else c["cab_tr"]
    pb = Vector(P(sb, 0, c["belt"])); pt = Vector(P(st, 0, c["roof"]))
    nrm = Vector((0, -1, 0)) if front else Vector((0, 1, 0))
    d = pt - pb
    nrm = (nrm - d * nrm.dot(d) / d.length_squared).normalized()
    pts = []
    for (u, tt) in ((u0, t0), (u1, t0), (u1, t1), (u0, t1)):
        p = pb + d * tt
        hw = c["cab_hwb"] + (c["cab_hwt"] - c["cab_hwb"]) * tt
        pts.append(tuple(Vector((u * hw, p.y, p.z)) + nrm * off))
    if not front:
        pts = [pts[1], pts[0], pts[3], pts[2]]
    b.quad(pts, spec)


def windshield_and_rear(b, c, front_glass, rear_glass):
    slanted_pane(b, c, True, 0.05, 0.92, -0.92, 0.92, 0.004, ('fit', 'rubber'))
    slanted_pane(b, c, True, 0.09, 0.88, -0.86, 0.86, 0.010, ('fit', front_glass))
    slanted_pane(b, c, False, 0.07, 0.90, -0.86, 0.86, 0.004, ('fit', 'rubber'))
    slanted_pane(b, c, False, 0.11, 0.86, -0.80, 0.80, 0.010, ('fit', rear_glass))


def interior(b, c):
    """Dark seats + dashboard visible through the (broken) glass."""
    zb = c["belt"]
    b.box((2 * c["cab_hwb"] - 0.1, c["cab_bf"] - c["cab_br"] - 0.1, 0.05), P((c["cab_bf"] + c["cab_br"]) / 2, 0, zb - 0.03),
          ('fit', 'interior'))
    b.box((2 * c["cab_hwb"] - 0.12, 0.30, 0.14), P(c["cab_bf"] - 0.2, 0, zb + 0.05), ('fit', 'interior'), bevel=0.03)
    for x in (-0.4, 0.4):
        for s in c["seats"]:
            b.box((0.5, 0.14, 0.52), P(s, x, zb + 0.2), ('fit', 'interior'), bevel=0.05, rot=(math.radians(-10), 0, 0))
    # steering wheel (driver on the left)
    b.lathe([(0.15, -0.015), (0.17, 0.0), (0.15, 0.015)], 10, ('fit', 'rubber'),
            P(c["cab_bf"] - 0.34, -0.4, zb + 0.18), rot=(math.radians(60), 0, 0))


def wheel(b, R, W, side):
    rin = R * 0.62
    prof = [(rin, -W / 2), (R - 0.05, -W / 2), (R, -W / 2 + 0.05), (R, W / 2 - 0.05), (R - 0.05, W / 2), (rin, W / 2),
            (rin, -W / 2)]
    b.lathe(prof, 14, ('remap', 'tire'), rot=(0, math.pi / 2, 0))
    t = bmesh.new(); uvl = t.loops.layers.uv.verify()
    segs = 14
    xo = side * (W / 2 - 0.03)
    rings = []
    for r, dx in ((rin, 0.0), (rin - 0.03, -0.04), (rin * 0.4, -0.05), (0.0, -0.03)):
        if r == 0:
            rings.append([t.verts.new((xo + side * dx, 0, 0))] * segs)
        else:
            rings.append([t.verts.new((xo + side * dx, r * math.cos(2 * math.pi * j / segs), r * math.sin(2 * math.pi * j / segs)))
                          for j in range(segs)])
    for i in range(3):
        for j in range(segs):
            vv = []
            for v in (rings[i][j], rings[i][(j + 1) % segs], rings[i + 1][(j + 1) % segs], rings[i + 1][j]):
                if v not in vv:
                    vv.append(v)
            f = t.faces.new(vv)
            f.material_index = 1
            for l in f.loops:
                co = l.vert.co
                l[uvl].uv = reg_uv("rim", 0.5 + co.y / (2 * rin) * side, 0.5 + co.z / (2 * rin))
    inner = [t.verts.new((-side * (W / 2 - 0.02), rin * math.cos(2 * math.pi * j / segs), rin * math.sin(2 * math.pi * j / segs)))
             for j in range(segs)]
    f = t.faces.new(inner)
    f.material_index = 1
    for l in f.loops:
        l[uvl].uv = reg_uv("rim", 0.5, 0.5)
    bmesh.ops.recalc_face_normals(t, faces=t.faces)
    me = bpy.data.meshes.new("_t"); t.to_mesh(me); t.free()
    b.bm.from_mesh(me); bpy.data.meshes.remove(me)


def wheels(c, mats, root, flat=None):
    """Four wheels; `flat` names a wheel that sits deflated (lower and tilted) like an abandoned car."""
    for nm, s, side in (("Wheel_FL", c["axle_f"], -1), ("Wheel_FR", c["axle_f"], 1),
                        ("Wheel_RL", c["axle_r"], -1), ("Wheel_RR", c["axle_r"], 1)):
        wb = B(); wheel(wb, c["wheel_r"], c["wheel_w"], side)
        z = c["axle_z"] - (0.06 if nm == flat else 0.0)
        ob = wb.finish(f"{c['name']}_{nm}", mats, root, loc=P(s, side * c["track"], z))
        if nm == flat:
            ob.rotation_euler = (0, math.radians(8 * side), 0)


def common_details(b, c):
    F, Rr, hw = c["front"], c["rear"], c["hw"]
    # chassis / floor pan hides the underside between the wheels
    b.box((2 * hw - 0.3, F - Rr - 0.6, 0.14), P((F + Rr) / 2, 0, c["sill"] - 0.04), ('fit', 'trim'))
    # bumpers
    b.box((2 * hw + 0.06, 0.20, 0.20), P(F + 0.07, 0, c["bumper_z"]), ('fit', 'trim'), bevel=0.04)
    b.box((2 * hw + 0.06, 0.20, 0.20), P(Rr - 0.07, 0, c["bumper_z"]), ('fit', 'trim'), bevel=0.04)
    b.box((0.44, 0.03, 0.12), P(Rr - 0.18, 0, c["bumper_z"] + 0.02), ('fit', 'plate'))
    # grille + head lights + indicators
    gz = c["lamp_z"]
    b.box((c["grille_w"], 0.05, 0.24), P(F + 0.005, 0, gz), ('fit', 'trim'), bevel=0.015)
    for k in range(3):
        b.box((c["grille_w"] - 0.08, 0.03, 0.03), P(F + 0.03, 0, gz - 0.07 + k * 0.07), ('fit', 'rubber'))
    for x in (-1, 1):
        b.box((0.30, 0.05, 0.18), P(F + 0.01, x * (hw - 0.24), gz), ('fit', 'head'), bevel=0.02)
        b.box((0.14, 0.05, 0.10), P(F + 0.005, x * (hw - 0.05), gz - 0.12), ('fit', 'ind'))
    # tail lights
    for x in (-1, 1):
        b.box((0.20, 0.05, c["tail_h"]), P(Rr - 0.005, x * (hw - 0.12), c["tail_z"]), ('fit', 'tail'), bevel=0.015)
    # mirrors + door handles
    for x in (-1, 1):
        b.box((0.05, 0.10, 0.05), P(c["cab_bf"] - 0.06, x * (hw + 0.03), c["belt"] + 0.08), ('fit', 'trim'))
        b.box((0.07, 0.16, 0.14), P(c["cab_bf"] - 0.08, x * (hw + 0.10), c["belt"] + 0.14), 'paint', bevel=0.02)
        for s in c["handles"]:
            b.box((0.03, 0.16, 0.04), P(s, x * (hw + 0.008), c["belt"] - 0.10), ('fit', 'chrome'), bevel=0.008)
    # door seams (thin dark strips on the sides)
    for x in (-1, 1):
        for s in c["seams"]:
            b.box((0.012, 0.018, c["belt"] - c["sill"] - 0.12), P(s, x * (hw + 0.002), (c["belt"] + c["sill"]) / 2 + 0.02),
                  ('fit', 'rubber'))


def export(name, objs, root):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs:
        o.select_set(True)
    root.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, f"{name}.fbx"), use_selection=True,
                             object_types={'MESH', 'EMPTY'}, apply_scale_options='FBX_SCALE_ALL',
                             apply_unit_scale=True, bake_space_transform=True, axis_forward='-Z', axis_up='Y',
                             mesh_smooth_type='FACE', path_mode='RELATIVE', embed_textures=False,
                             add_leaf_bones=False, bake_anim=False)


def tri_count(objs):
    t = 0
    for o in objs:
        if o.type == 'MESH':
            o.data.calc_loop_triangles(); t += len(o.data.loop_triangles)
    return t


# ------------------------------------------------------------------ PICKUP
PICKUP = dict(name="Car_Pickup", front=2.45, rear=-2.50, hw=0.92, sill=0.44, axle_f=1.55, axle_r=-1.62, axle_z=0.40,
              wheel_r=0.40, wheel_w=0.28, track=0.80, arch_r=0.50, belt=1.06, roof=1.74,
              cab_bf=0.78, cab_br=-0.58, cab_tf=0.30, cab_tr=-0.50, cab_hwb=0.90, cab_hwt=0.80,
              bumper_z=0.52, lamp_z=0.84, grille_w=1.0, tail_z=0.96, tail_h=0.26, seats=[-0.30],
              handles=[0.02], seams=[0.76, -0.56], bed_floor=0.80, bed_top=1.14)


def build_pickup(paint_mat, parts_mat):
    c = PICKUP
    mats = [paint_mat, parts_mat]
    root = bpy.data.objects.new(c["name"], None); bpy.context.scene.collection.objects.link(root)
    b = B()
    F, Rr, hw = c["front"], c["rear"], c["hw"]
    bed_s = c["cab_br"] - 0.04
    # lower body: bed floor level at the back, cab belt in the middle, hood sloping to the front
    upper = [(Rr, c["bed_floor"]), (bed_s, c["bed_floor"]), (bed_s, c["belt"]), (c["cab_bf"] + 0.02, c["belt"]),
             (F - 0.10, c["belt"] - 0.03), (F, c["belt"] - 0.10)]
    b.prism(lower_profile(c, upper), hw, 'paint')
    hw_f, s_front, s_rear = cabin(b, c)
    side_windows(b, c, hw_f, s_front, s_rear, [(None, None)],
                 {-1: ['glass'], 1: ['glass']})
    windshield_and_rear(b, c, 'glass', 'broken')
    interior(b, c)
    common_details(b, c)
    # hood seam + raised hood centre
    b.box((2 * hw - 0.22, F - c["cab_bf"] - 0.2, 0.04), P((F + c["cab_bf"]) / 2 + 0.02, 0, c["belt"] - 0.005), 'paint',
          bevel=0.015)
    # bed: side walls, front wall, tailgate, wood floor, toolbox, wheel-well humps
    wall = 0.07
    top = c["bed_top"]
    for x in (-1, 1):
        # walls start exactly on the floor: below it they would share the body's side plane (z-fighting)
        b.box((wall, bed_s - Rr, top - c["bed_floor"]), P((bed_s + Rr) / 2, x * (hw - wall / 2), (top + c["bed_floor"]) / 2),
              'paint', bevel=0.02)
        b.box((0.10, bed_s - Rr + 0.02, 0.05), P((bed_s + Rr) / 2, x * (hw - 0.04), top + 0.01), 'paint', bevel=0.015)  # rail cap
        b.box((0.34, 0.44, 0.24), P(c["axle_r"], x * (hw - wall - 0.2), c["bed_floor"] + 0.10), 'paint', bevel=0.04)
    b.box((2 * hw, wall, top - c["bed_floor"] + 0.02), P(bed_s - wall / 2, 0, (top + c["bed_floor"]) / 2), 'paint', bevel=0.02)
    b.box((2 * hw - 0.02, wall + 0.02, top - c["bed_floor"] + 0.04), P(Rr + wall / 2, 0, (top + c["bed_floor"]) / 2 + 0.02),
          'paint', bevel=0.025)
    b.box((0.36, 0.03, 0.06), P(Rr - 0.005, 0, top - 0.12), ('fit', 'trim'))                          # tailgate handle
    b.box((2 * hw - 2 * wall, bed_s - Rr - 2 * wall, 0.04), P((bed_s + Rr) / 2, 0, c["bed_floor"] + 0.005), ('fit', 'wood'))
    b.box((0.42, 1.0, 0.30), P(bed_s - wall - 0.30, 0, c["bed_floor"] + 0.16), 'paint', bevel=0.06)   # toolbox
    body = b.finish(f"{c['name']}_Body", mats, root)
    wheels(c, mats, root, flat="Wheel_FR")
    return root, [body] + [o for o in bpy.context.scene.objects if o.name.startswith(c["name"] + "_Wheel")]


# ------------------------------------------------------------------ SEDAN
SEDAN = dict(name="Car_Sedan", front=2.25, rear=-2.25, hw=0.88, sill=0.38, axle_f=1.38, axle_r=-1.38, axle_z=0.34,
             wheel_r=0.34, wheel_w=0.24, track=0.76, arch_r=0.44, belt=0.96, roof=1.44,
             cab_bf=0.92, cab_br=-1.28, cab_tf=0.22, cab_tr=-0.80, cab_hwb=0.86, cab_hwt=0.74,
             bumper_z=0.46, lamp_z=0.76, grille_w=0.78, tail_z=0.82, tail_h=0.18, seats=[0.05, -0.75],
             handles=[0.30, -0.52], seams=[0.90, -0.12, -1.10])


def build_sedan(paint_mat, parts_mat):
    c = SEDAN
    mats = [paint_mat, parts_mat]
    root = bpy.data.objects.new(c["name"], None); bpy.context.scene.collection.objects.link(root)
    b = B()
    F, Rr, hw = c["front"], c["rear"], c["hw"]
    upper = [(Rr - 0.02, c["belt"] - 0.06), (Rr + 0.06, c["belt"] + 0.01), (c["cab_br"] - 0.02, c["belt"] + 0.01),
             (c["cab_bf"] + 0.02, c["belt"]), (F - 0.10, c["belt"] - 0.04), (F, c["belt"] - 0.12)]
    b.prism(lower_profile(c, upper), hw, 'paint')
    hw_f, s_front, s_rear = cabin(b, c)
    split = -0.18   # B-pillar
    side_windows(b, c, hw_f, s_front, s_rear, [(None, split + 0.05), (split - 0.05, None)],
                 {-1: ['broken', 'glass'], 1: ['broken', 'broken']})
    windshield_and_rear(b, c, 'broken', 'glass')
    interior(b, c)
    common_details(b, c)
    # hood / trunk seams (thin dark lines) and the hood bulge from the reference
    b.box((2 * hw - 0.2, F - c["cab_bf"] - 0.2, 0.03), P((F + c["cab_bf"]) / 2 + 0.03, 0, c["belt"] - 0.02), 'paint',
          bevel=0.012)
    b.box((0.02, F - c["cab_bf"] - 0.18, 0.02), P((F + c["cab_bf"]) / 2 + 0.03, 0, c["belt"] + 0.012), ('fit', 'rubber'))
    b.box((2 * hw - 0.2, c["cab_br"] - Rr - 0.25, 0.03), P((c["cab_br"] + Rr) / 2 - 0.05, 0, c["belt"] + 0.015), 'paint',
          bevel=0.012)
    body = b.finish(f"{c['name']}_Body", mats, root)
    wheels(c, mats, root, flat="Wheel_RL")
    return root, [body] + [o for o in bpy.context.scene.objects if o.name.startswith(c["name"] + "_Wheel")]


# ------------------------------------------------------------------ main
os.makedirs(OUT, exist_ok=True)
parts = None
for name, builder, paint_tex in (("Car_Pickup", build_pickup, "wreck_paint_pickup.png"),
                                 ("Car_Sedan", build_sedan, "wreck_paint_sedan.png")):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    paint_mat = make_mat("M_Wreck_Paint" + name[3:], paint_tex)
    parts_mat = make_mat("M_Wreck_Parts", "wreck_parts.png", rough=0.8, metal=0.1)
    random.seed(5)
    root, objs = builder(paint_mat, parts_mat)
    print(name, "TRIS", tri_count(objs))
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, f"{name}.blend"), relative_remap=True)
    export(name, objs, root)
print("EXPORT OK")

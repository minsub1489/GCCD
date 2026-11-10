"""OUTPOST 07 original hard-surface presentation models, authored in Blender.
No third-party geometry or textures. Scene units: meters. Blender -Y is front.
"""
import bpy, math, json
from mathutils import Vector
from pathlib import Path
ROOT = next(p for p in Path(__file__).resolve().parents if (p/'GCCD_Project').is_dir())
DEST = ROOT/'GCCD_Project/Assets/Resources/VisualUpgrade/Models'
PREV = Path(__file__).resolve().parent
DEST.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials): bpy.data.materials.remove(m)

def material(name, color, metallic=0., roughness=.5, emission=None):
    mat=bpy.data.materials.new(name);mat.diffuse_color=(*color,1);mat.use_nodes=True
    bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'), None)
    if bs is None:
        bs=mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        output=mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(bs.outputs['BSDF'],output.inputs['Surface'])
    bs.inputs['Base Color'].default_value=(*color,1)
    bs.inputs['Metallic'].default_value=metallic;bs.inputs['Roughness'].default_value=roughness
    if emission:
        bs.inputs['Emission Color'].default_value=(*color,1);bs.inputs['Emission Strength'].default_value=emission
    if name in ['Hull','Steel','Rubber']:
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=145;tex.inputs['Detail'].default_value=2
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.16;bump.inputs['Distance'].default_value=.0015
        links.new(tex.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return mat
M={
'Hull':material('Hull',(.105,.135,.14),.75,.38),
'Steel':material('Steel',(.37,.41,.42),.86,.29),
'Rubber':material('Rubber',(.018,.025,.028),.08,.6),
'Amber':material('Amber',(.97,.38,.055),.4,.32),
'Cyan':material('Cyan',(.04,.83,.96),.35,.2,4.),
'Lens':material('Lens',(.005,.055,.065),.8,.12),
}
roots=[]
def empty(name):
    obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);roots.append(obj);return obj

def finish(obj,name,mat,parent,bevel=0):
    obj.name=name
    if parent:obj.parent=parent
    obj.data.materials.append(M[mat])
    if bevel:
        mod=obj.modifiers.new('Machined edges','BEVEL');mod.width=bevel;mod.segments=2
        mod=obj.modifiers.new('Weighted surface normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=40
    return obj

def box(name,at,scale,mat,parent,bevel=.012,rot=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=at,rotation=rot)
    o=bpy.context.object;o.dimensions=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat,parent,bevel)

def cylinder(name,at,radius,depth,mat,parent,verts=24,rot=(0,0,0),bevel=.003):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=radius,depth=depth,location=at,rotation=rot)
    o=finish(bpy.context.object,name,mat,parent,bevel)
    for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
    return o

def rod(name,a,b,r,mat,parent,verts=12):
    center=(Vector(a)+Vector(b))*.5;direction=Vector(b)-Vector(a)
    o=cylinder(name,center,r,direction.length,mat,parent,verts,bevel=.002)
    o.rotation_euler=direction.to_track_quat('Z','Y').to_euler();return o

def ring(name,at,outer,inner,depth,mat,parent,n=48):
    v=[];f=[]
    for z in [-depth*.5,depth*.5]:
        for r in [outer,inner]:
            for i in range(n):
                a=2*math.pi*i/n;v.append((r*math.cos(a),r*math.sin(a),z))
    for i in range(n):
        j=(i+1)%n
        f.extend([(i,j,n+j,n+i),(2*n+i,3*n+i,3*n+j,2*n+j),(i,2*n+i,2*n+j,j),(n+i,n+j,3*n+j,3*n+i)])
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(v,[],f);mesh.update()
    o=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(o);o.location=at
    finish(o,name,mat,parent,.002)
    return o

def screw(at,parent,mat='Steel',radius=.009):
    o=cylinder('Captive hex fastener',at,radius,.008,mat,parent,verts=6,bevel=.001)
    return o

def label(text,at,size,parent,rot=(0,0,0),mat='Steel'):
    bpy.ops.object.text_add(location=at,rotation=rot)
    o=bpy.context.object;o.data.body=text;o.data.size=size;o.data.extrude=.0002;o.data.align_x='CENTER';o.data.align_y='CENTER'
    bpy.ops.object.convert(target='MESH');return finish(bpy.context.object,'Etched '+text,mat,parent)

# Hero quad duct drone; rotor geometry is separate so runtime can animate transforms.
drone=empty('AegisSecurityDrone')
box('Isolated avionics core',(0,0,.015),(.60,.48,.21),'Rubber',drone,.055)
box('Upper armored clamshell',(0,.005,.105),(.58,.46,.105),'Hull',drone,.045)
box('Lower armored keel',(0,.0,-.09),(.46,.38,.10),'Hull',drone,.03)
box('Top inset spine',(0,.015,.167),(.28,.31,.025),'Steel',drone,.018)
box('Top spine detail',(0,.03,.183),(.19,.21,.012),'Hull',drone,.01)
box('Amber identification flash',(0,.085,.193),(.17,.043,.008),'Amber',drone,.004)
for x in [-.21,.21]:
    box('Side impact rail',(x,0,.025),(.10,.52,.11),'Steel',drone,.022)
    box('Side panel',(x*1.35,0,.035),(.022,.23,.088),'Hull',drone,.01)
    for y in [-.16,.16]:screw((x,y,.169),drone)
for y in [-.175,-.13,-.085,-.04,.005,.05,.095,.14]:
    box('Avionics cooling fin',(0,y,.191),(.155,.009,.017),'Rubber',drone,.003)
# Front gimbal/lens housing, -Y front.
box('Optical turret housing',(0,-.274,-.005),(.28,.13,.16),'Hull',drone,.033)
cylinder('Optical bezel',(0,-.348,-.004),.071,.032,'Steel',drone,48,(math.pi/2,0,0),.004)
cylinder('Optical black surround',(0,-.369,-.004),.058,.015,'Rubber',drone,48,(math.pi/2,0,0),.003)
cylinder('Optical glass',(0,-.379,-.004),.046,.009,'Lens',drone,48,(math.pi/2,0,0),.002)
cylinder('Optical emitter',(0,-.385,-.004),.018,.007,'Cyan',drone,32,(math.pi/2,0,0),.001)
for x in [-.104,.104]:
    cylinder('Optical side pivot',(x,-.28,-.015),.026,.022,'Steel',drone,24,(0,math.pi/2,0))
    box('Front sensor slit',(x,-.345,.073),(.042,.016,.015),'Cyan',drone,.004)
# Underbody skids kept within hitbox height.
for x in [-.16,.16]:
    rod('Landing shock strut',(x,.14,-.09),(x,.16,-.205),.012,'Steel',drone)
    rod('Landing shock strut',(x,-.14,-.09),(x,-.16,-.205),.012,'Steel',drone)
    box('Landing skid',(x,0,-.215),(.043,.42,.028),'Rubber',drone,.012)
# Ducts, crossbraced cages, blades.
for ix,x in enumerate([-.395,.395]):
    for iy,y in enumerate([-.285,.285]):
        name=('L' if x<0 else 'R')+('F' if y<0 else 'B')
        rod('Duct arm '+name,(math.copysign(.19,x),math.copysign(.13,y),.02),(x,y,.02),.036,'Hull',drone,12)
        ring('Duct inner shroud '+name,(x,y,.025),.17,.151,.095,'Hull',drone)
        ring('Duct upper rim '+name,(x,y,.079),.176,.153,.020,'Steel',drone)
        ring('Duct lower rim '+name,(x,y,-.032),.174,.154,.014,'Rubber',drone)
        # Guard spars visibly protect the rotors.
        for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
            a=(x+.161*math.cos(angle),y+.161*math.sin(angle),.087)
            b=(x+.027*math.cos(angle),y+.027*math.sin(angle),.073)
            rod('Rotor guard '+name,a,b,.005,'Steel',drone,8)
        cylinder('Motor stator '+name,(x,y,.016),.027,.075,'Steel',drone,24,bevel=.004)
        rotor=empty('Rotor_'+name);roots.remove(rotor);rotor.parent=drone;rotor.location=(x,y,.035)
        cylinder('Rotor hub '+name,(0,0,0),.035,.025,'Rubber',rotor,24,bevel=.004)
        for i in range(5):
            a=i*2*math.pi/5
            o=box('Rotor blade '+name,(math.cos(a)*.086,math.sin(a)*.086,.0),(.115,.026,.006),'Rubber',rotor,.007,rot=(0,.10,a+.12))
        for angle in [math.pi*.25,math.pi*1.25]:
            box('Duct hazard tab '+name,(x+math.cos(angle)*.166,y+math.sin(angle)*.166,.093),(.036,.028,.011),'Amber',drone,.003,rot=(0,0,angle))
        screw((x,y,.089),drone,radius=.012)
# Rear antenna and identification.
rod('Antenna socket',(.17,.20,.135),(.17,.20,.21),.013,'Rubber',drone)
rod('Flexible aerial',(.17,.20,.21),(.17,.235,.255),.005,'Rubber',drone,8)
box('Rear optical strip',(0,.251,.075),(.22,.015,.023),'Cyan',drone,.004)
label('AEGIS / 07',(0,-.025,.201),.032,drone,mat='Steel')

# Wall mounted engineering module, mount plane Blender Y=.0, front -Y.
service=empty('IndustrialServiceUnit')
box('Service mounting frame',(0,.025,0),(.86,.13,1.34),'Steel',service,.026)
box('Service cabinet shell',(0,-.095,0),(.79,.22,1.25),'Hull',service,.035)
box('Service upper cover',(0,-.217,.29),(.71,.035,.53),'Steel',service,.016)
box('Service upper inset',(0,-.24,.29),(.59,.025,.42),'Hull',service,.009)
for z in [.11,.16,.21,.26,.31,.36,.41,.46]:
    box('Angled ventilation louver',(0,-.266,z),(.51,.025,.022),'Rubber',service,.005,rot=(.22,0,0))
box('Warning stripe',(0,-.243,-.05),(.71,.024,.05),'Amber',service,.004)
box('Service lower hatch',(0,-.228,-.35),(.70,.045,.46),'Hull',service,.014)
box('Diagnostic screen',(-.17,-.258,-.30),(.22,.018,.115),'Rubber',service,.007)
for i in range(5):
    box('Diagnostic activity bar',(-.24+i*.036,-.27,-.30),(.014,.007,.038+i*.007),'Cyan',service,.002)
for z in [-.27,-.36]:
    cylinder('Status pilot light',(.22,-.267,z),.022,.016,'Cyan' if z==-.27 else 'Amber',service,24,(math.pi/2,0,0))
for x in [-.31,.31]:
    for z in [-.54,.53]:
        o=screw((x,-.262,z),service,radius=.012);o.rotation_euler=(math.pi/2,0,0)
# Conduit connectors on top and pipes descending to an offset junction.
for x in [-.22,.0,.22]:
    cylinder('Compression cable gland',(x,-.05,.68),.038,.07,'Rubber',service,16)
    rod('Riser conduit',(x,-.05,.72),(x,-.05,.96),.022,'Steel',service)
box('Lower junction block',(.28,-.06,-.76),(.23,.17,.17),'Rubber',service,.016)
rod('Lower cabinet feed',(.22,-.06,-.58),(.22,-.06,-.73),.025,'Steel',service)
label('POWER / AUX',(0,-.274,-.49),.037,service,rot=(math.pi/2,0,0),mat='Steel')
# Collapse static geometry by material to keep Unity draw calls low. Preserve rotor pivots.
def combine_meshes(objects,name,parent):
    if not objects:return
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        bpy.context.view_layer.objects.active=obj
        obj.select_set(True)
        for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
        obj.select_set(False)
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join();joined=bpy.context.object;joined.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    return joined
for root in [drone,service]:
    groups={mat:[o for o in root.children if o.type=='MESH' and o.data.materials[0].name==mat] for mat in M}
    for mat,objects in groups.items():
        combine_meshes(objects,root.name+'_'+mat,root)
    for rotor in [o for o in root.children if o.type=='EMPTY' and o.name.startswith('Rotor_')]:
        combine_meshes([o for o in rotor.children if o.type=='MESH'],'Fan_'+rotor.name,rotor)
# Models exported independently, with only intended root tree.

def descendants(root):
    return [root]+list(root.children_recursive)

def export(root,file):
    bpy.ops.object.select_all(action='DESELECT')
    for o in descendants(root):o.select_set(True)
    bpy.context.view_layer.objects.active=root
    bpy.ops.export_scene.fbx(filepath=str(DEST/file),use_selection=True,object_types={'EMPTY','MESH'},apply_unit_scale=True,global_scale=1.,axis_forward='-Z',axis_up='Y',bake_space_transform=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,bake_anim=False,path_mode='AUTO')

export(drone,'AegisSecurityDrone.fbx');export(service,'IndustrialServiceUnit.fbx')
# Record evaluated triangle count, dimensions and semantics before arranging preview.
info={}
deps=bpy.context.evaluated_depsgraph_get()
for root in [drone,service]:
    tris=0;verts=[]
    for o in descendants(root):
        if o.type!='MESH':continue
        ev=o.evaluated_get(deps);mesh=ev.to_mesh();mesh.calc_loop_triangles();tris+=len(mesh.loop_triangles)
        verts.extend(o.matrix_world@v.co for v in mesh.vertices);ev.to_mesh_clear()
    lo=Vector(tuple(min(v[i] for v in verts) for i in range(3)));hi=Vector(tuple(max(v[i] for v in verts) for i in range(3)))
    info[root.name]={'triangles':tris,'blender_bounds_min':list(lo),'blender_bounds_max':list(hi),'unity_size_xyz':[hi.x-lo.x,hi.z-lo.z,hi.y-lo.y],'objects':len(descendants(root)),'materials':list(M.keys())}
(PREV/'model_manifest.json').write_text(json.dumps(info,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(PREV/'outpost_hard_surface.blend'))
# Separate composed beauty image. Native file above contains models at origin.
service.location=(1.1,.5,.45);service.scale=(.7,.7,.7)
drone.location=(-.1,0,.28);drone.rotation_euler=(0,.05,-.17)
# Ground + lighting for local art check.
mat=material('Preview backdrop',(.027,.04,.05),.25,.7)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));ground=bpy.context.object;ground.data.materials.append(mat)
world=bpy.context.scene.world;world.color=(.04,.05,.06);world.use_nodes=True
bg=next((n for n in world.node_tree.nodes if n.type=='BACKGROUND'),None)
if bg is None:
    bg=world.node_tree.nodes.new('ShaderNodeBackground');out=world.node_tree.nodes.new('ShaderNodeOutputWorld');world.node_tree.links.new(bg.outputs['Background'],out.inputs['Surface'])
bg.inputs['Color'].default_value=(.11,.16,.19,1);bg.inputs['Strength'].default_value=.3

def area(name,at,energy,color,size,target):
    bpy.ops.object.light_add(type='AREA',location=at);o=bpy.context.object;o.name=name;o.data.energy=energy;o.data.color=color;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
area('Cool softbox',(-2,-3,4),550,(.69,.87,1),3,(0,0,.2))
area('Warm edge',(2,1.5,3),750,(1,.57,.26),2,(0,0,.2))
area('Cyan fill',(-1,2,1),200,(.18,.76,1),2,(0,0,.2))
bpy.ops.object.camera_add(location=(2.25,-3.3,2.05));camera=bpy.context.object;camera.rotation_euler=(Vector((.35,.1,.35))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=2.7
scene=bpy.context.scene;scene.camera=camera;scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG';scene.render.filepath=str(PREV/'custom-models-preview.png')
bpy.ops.render.render(write_still=True)
print('MODEL_AUTHORING_COMPLETE '+json.dumps(info))

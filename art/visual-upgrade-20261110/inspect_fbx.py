import bpy, json
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[1]
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(root/'GCCD_Project/Assets/Resources/VisualUpgrade/Models/AegisSecurityDrone.fbx'))
objects=list(bpy.context.scene.objects)
meshes=[o for o in objects if o.type=='MESH']
verts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
lo=[min(v[i] for v in verts) for i in range(3)];hi=[max(v[i] for v in verts) for i in range(3)]
report={'meshes':len(meshes),'objects':len(objects),'min':lo,'max':hi,'materials':sorted(set(m.name for o in meshes for m in o.data.materials)), 'rotors':{o.name:{'local_rotation_degrees':[x*180/3.141592653589793 for x in o.rotation_euler],'local_location':list(o.location),'world_axis_z':list(o.matrix_world.to_3x3()@Vector((0,0,1))), 'world_axis_y':list(o.matrix_world.to_3x3()@Vector((0,1,0)))} for o in objects if o.name.startswith('Rotor_')}}
print('FBX_ROUNDTRIP '+json.dumps(report))
(root/'authoring/fbx_roundtrip.json').write_text(json.dumps(report,indent=2))
assert len(meshes)==10
assert len(report['rotors'])==4
assert max(hi[0]-lo[0],hi[1]-lo[1])<1.2
assert hi[2]-lo[2]<.55

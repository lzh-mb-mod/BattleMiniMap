using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace BattleMiniMap.View.MapGeometry
{
    // Reads only the currently loaded scene. Owns copies, never source meshes/materials.
    internal sealed class MapLiveSceneGeometry : IDisposable
    {
        private readonly Scene _source, _target;
        private readonly Material _material;
        private readonly MapLiveGeometryRules _rules;
        private readonly bool _collision;
        private bool _collisionVerified;
        private sealed class CollisionResource
        {
            public PhysicsShape Source;
            public readonly List<Mesh> Meshes=new List<Mesh>();
            public int Triangles;
        }
        private readonly Dictionary<ulong,CollisionResource> _collisionResources=new Dictionary<ulong,CollisionResource>();
        private int _unsupportedShapes,_primitives,_collisionTriangles;
        private int _primitiveTriangles,_collisionMeshCount;
        private sealed class Copy
        {
            public GameEntity Entity; public Mesh Mesh; public int Seen;
        }
        private readonly Dictionary<string,Copy> _copies=new Dictionary<string,Copy>();
        private readonly List<Mesh> _terrainMeshes=new List<Mesh>();
        private readonly List<GameEntity> _terrainEntities=new List<GameEntity>();
        private IEnumerator<int> _steps;
        private readonly Stopwatch _wall=Stopwatch.StartNew();
        private double _workMs,_maxTickMs,_maxStepMs;
        private int _ticks,_generation;
        private bool _disposed,_terrainDone;
        private readonly bool _hasTerrain;
        private float _refresh;
        private int _terrainRead,_terrainInvalid;
        public int EntityCount { get; private set; }
        public int CopiedMeshes => _copies.Count;
        public bool IsCollision => _collision;
        public int Skipped { get; private set; }
        public int Errors { get; private set; }
        public long SourceFaces { get; private set; }
        public bool Partial { get; private set; }
        public bool InitialFinished { get; private set; }
        public int TerrainPercent => _terrainRead*100/(_rules.GridX*_rules.GridY);
        public string Failure { get; private set; }
        public MapLiveSceneGeometry(Scene source,Scene target,Material material,MapLiveGeometryRules rules,bool hasTerrain=true,bool collision=false)
        {
            _source=source??throw new ArgumentNullException(nameof(source));
            _target=target; _material=material; _rules=rules;
            _hasTerrain=hasTerrain; _terrainDone=!hasTerrain;
            _collision=collision;
            _steps=Extract().GetEnumerator();
            Report("started: source="+(collision?"runtime-collision":"runtime-render")+" terrainAvailable="+hasTerrain+" terrain=runtime-height budgetMs=2 maxSteps=64 domain="+
                rules.MinX+","+rules.MinY+","+rules.MaxX+","+rules.MaxY+" grid="+rules.GridX+"x"+rules.GridY);
        }
        public void Update(float dt)
        {
            if (_disposed || Failure!=null) return;
            if (_steps==null)
            {
                if (MapLiveGeometryRules.Finite(dt) && dt>0) _refresh+=Math.Min(dt,1);
                if (_refresh<2) return;
                _refresh=0; _steps=Extract().GetEnumerator();
            }
            var tick=Stopwatch.StartNew(); var count=0;
            try
            {
                while(count<64 && tick.Elapsed.TotalMilliseconds<2)
                {
                    var step=Stopwatch.StartNew(); var more=_steps.MoveNext();
                    _maxStepMs=Math.Max(_maxStepMs,step.Elapsed.TotalMilliseconds); ++count;
                    if (!more) { _steps.Dispose(); _steps=null; break; }
                }
            }
            catch(Exception ex)
            {
                Failure=ex.Message; Partial=true; var failedSteps=_steps; _steps=null;
                MapGeometryCleanup.Run(null,()=>failedSteps?.Dispose()); Report("failed: "+ex.Message);
            }
            _workMs+=tick.Elapsed.TotalMilliseconds; _maxTickMs=Math.Max(_maxTickMs,tick.Elapsed.TotalMilliseconds);
            if (++_ticks%120==0 || _steps==null) Report(Status());
        }
        private IEnumerable<int> Extract()
        {
            ++_generation;
            if (_collision && !_collisionVerified)
            { MapLiveCollisionGuard.VerifyEngine(); _collisionVerified=true; yield return 0; }
            var all=new List<GameEntity>();
            _source.GetEntities(ref all); EntityCount=all.Count;
            yield return 0; // GetEntities is atomic: its time is included in maxStepMs.
            if (all.Count>200000) { Partial=true; throw new InvalidOperationException("Runtime entity limit exceeded"); }
            var scanSkipped=0; long faces=0;
            foreach(var entity in all)
            {
                if (entity==null || entity.Pointer==UIntPtr.Zero) continue;
                if (_collision)
                {
                    foreach(var step in Collision(entity)) yield return step;
                    continue;
                }
                // Strong wrappers are held only during this scan; no raw pointers are dereferenced.
                if (entity.GetScenePointer()!=_source.Pointer || !entity.IsVisibleIncludeParents()) { ++scanSkipped; yield return 0; continue; }
                if (entity.Skeleton!=null) { ++scanSkipped; Partial=true; yield return 0; continue; }
                var global=entity.GetGlobalFrame(); var components=entity.MultiMeshComponentCount;
                yield return 0;
                if (components<0 || components>256) { ++scanSkipped; Partial=true; continue; }
                for(var m=0;m<components;++m)
                {
                    var meta=entity.GetMetaMesh(m); yield return 0;
                    if (meta==null || !meta.IsValid) { ++scanSkipped; Partial=true; continue; }
                    var n=meta.MeshCount; var metaFrame=meta.Frame; yield return 0;
                    if (n<0 || n>1024) { ++scanSkipped; Partial=true; continue; }
                    var masks=new int[n]; var combined=0;
                    for(var i=0;i<n;++i) { masks[i]=meta.GetLodMaskForMeshAtIndex(i); combined|=masks[i]; yield return 0; }
                    var selected=MapLiveGeometryRules.CoarsestLod(combined);
                    if (selected==0) { ++scanSkipped; Partial=true; continue; }
                    for(var i=0;i<n;++i)
                    {
                        if ((masks[i]&selected)==0) continue;
                        var mesh=meta.GetMeshAtIndex(i); yield return 0;
                        if (mesh==null || !mesh.IsValid) { ++scanSkipped; Partial=true; continue; }
                        var key=entity.Pointer.ToUInt64().ToString("X")+":"+meta.Pointer.ToUInt64().ToString("X")+":"+mesh.Pointer.ToUInt64().ToString("X")+":"+i;
                        var frame=global*metaFrame*mesh.GetLocalFrame();
                        if (!Valid(frame)) { ++scanSkipped; Partial=true; continue; }
                        frame=Normalize(frame); faces+=mesh.GetFaceCount(); yield return 0;
                        if (!_copies.TryGetValue(key,out var copy))
                        {
                            if (_copies.Count>=40000) { ++scanSkipped; Partial=true; continue; }
                            var owned=mesh.CreateCopy();
                            if (owned==null || !owned.IsValid) { ++Errors; Partial=true; continue; }
                            // Register ownership before native setters can fail.
                            copy=new Copy { Mesh=owned }; _copies.Add(key,copy);
                            owned.SetMaterial(_material); owned.Color=uint.MaxValue; owned.SetLocalFrame(MatrixFrame.Identity);
                            copy.Entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                            if (copy.Entity==null) throw new InvalidOperationException("No runtime map entity");
                            copy.Entity.AddMesh(owned,recomputeBoundingBox:false);
                            copy.Entity.SetReadyToRender(true);
                        }
                        copy.Seen=_generation; copy.Entity.SetFrame(ref frame);
                        copy.Entity.SetVisibilityExcludeParents(true); yield return 0;
                    }
                    GC.KeepAlive(meta);
                }
                GC.KeepAlive(entity);
            }
            // Hide disappeared/replaced copies. Keep native resources until target teardown.
            foreach(var copy in _copies.Values)
            { if (copy.Seen!=_generation && copy.Entity!=null) copy.Entity.SetVisibilityExcludeParents(false); yield return 0; }
            Skipped=scanSkipped; SourceFaces=faces;
            if (!_terrainDone)
            {
                foreach(var step in Terrain()) yield return step;
                _terrainDone=true;
            }
            InitialFinished=true;
            GC.KeepAlive(all);
        }
        private IEnumerable<int> Collision(GameEntity entity)
        {
            if (entity.GetScenePointer()!=_source.Pointer) yield break;
            var sourceShape=entity.GetBodyShape(); yield return 0;
            if (sourceShape==null || !sourceShape.IsValid) yield break;
            var id=sourceShape.Pointer.ToUInt64();
            if (!_collisionResources.TryGetValue(id,out var resource))
            {
                resource=new CollisionResource { Source=sourceShape }; _collisionResources.Add(id,resource);
                if (_collisionResources.Count>20000) throw new InvalidOperationException("Collision resource limit exceeded");
                var shape=sourceShape.CreateCopy();
                if (shape==null || !shape.IsValid || shape.Pointer==sourceShape.Pointer)
                { ++_unsupportedShapes; Partial=true; yield break; }
                try
                {
                    shape.Prepare(); yield return 0;
                    var count=shape.TriangleMeshCount(); var spheres=shape.SphereCount(); var capsules=shape.CapsuleCount();
                    if(spheres<0 || capsules<0 || spheres+(long)capsules>4096)
                    { ++_unsupportedShapes; Partial=true; yield break; }
                    for(var p=0;p<spheres+capsules;++p)
                    {
                        if(_collisionMeshCount>=40000 || _collisionTriangles+(long)_primitiveTriangles>=4000000)
                        { Partial=true;break; }
                        Vec3 a,b; float radius;
                        if (p<spheres)
                        { var data=new SphereData();shape.GetSphere(ref data,p);a=b=data.Origin;radius=data.Radius; }
                        else
                        { var data=new CapsuleData();shape.GetCapsule(ref data,p-spheres);a=data.P1;b=data.P2;radius=data.Radius; }
                        if(!Valid(a)||!Valid(b)||!MapLiveGeometryRules.Finite(radius)||radius<=0||radius>100000)
                        { ++Errors;Partial=true;continue; }
                        var builder=new MeshBuilder();var n=0;
                        foreach(var face in MapLivePrimitiveGeometry.Capsule(new GeometryPoint(a.x,a.y,a.z),new GeometryPoint(b.x,b.y,b.z),radius))
                        {
                            Add(builder,new Vec3(face[0].X,face[0].Y,face[0].Z),new Vec3(face[1].X,face[1].Y,face[1].Z),new Vec3(face[2].X,face[2].Y,face[2].Z));
                            ++n;yield return 0;
                        }
                        if(n>0)
                        {
                            if(_collisionTriangles+(long)_primitiveTriangles+n>4000000) { Partial=true;continue; }
                            var mesh=builder.Finalize();if(mesh==null)throw new InvalidOperationException("No collision primitive mesh");
                            resource.Meshes.Add(mesh);mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;resource.Triangles+=n;++_primitives;
                            _primitiveTriangles+=n;++_collisionMeshCount;
                        }
                        yield return 0;
                    }
                    if (count>0 && !MapLiveCollisionGuard.StorageReady(shape)) { ++_unsupportedShapes; Partial=true; count=0; }
                    if (count<0 || count>1024) { ++_unsupportedShapes; Partial=true; count=0; }
                    yield return 0;
                    var triangle=new Vec3[3];
                    for(var m=0;m<count;++m)
                    {
                        var triangles=shape.TriangleCountInTriangleMesh(m); yield return 0;
                        if (triangles<0 || triangles>1000000 || _collisionTriangles+(long)_primitiveTriangles+triangles>4000000)
                        { ++_unsupportedShapes; Partial=true; continue; }
                        // Chunking bounds individual uploads and permits visible incremental progress.
                        for(var start=0;start<triangles;start+=2048)
                        {
                            if(_collisionMeshCount>=40000) { Partial=true;break; }
                            var builder=new MeshBuilder(); var valid=0;
                            for(var t=start;t<Math.Min(triangles,start+2048);++t)
                            {
                                if (!MapLiveCollisionGuard.StorageReady(shape)) throw new InvalidOperationException("Prepared collision storage disappeared");
                                shape.GetTriangle(triangle,m,t);
                                if (ValidTriangle(triangle[0],triangle[1],triangle[2]))
                                { Add(builder,triangle[0],triangle[1],triangle[2]); ++valid; }
                                else { ++Errors; Partial=true; }
                                ++_collisionTriangles; yield return 0;
                            }
                            if (valid==0) continue;
                            var mesh=builder.Finalize();
                            if(mesh==null) throw new InvalidOperationException("No collision map mesh");
                            resource.Meshes.Add(mesh); resource.Triangles+=valid;
                            ++_collisionMeshCount;
                            mesh.SetMaterial(_material);mesh.Color=uint.MaxValue; yield return 0;
                        }
                    }
                }
                finally { shape.ManualInvalidate(); GC.KeepAlive(sourceShape); }
            }
            var frame=entity.GetBodyWorldTransform();
            if(!Valid(frame)) { ++Errors; Partial=true; yield break; }
            frame=Normalize(frame); yield return 0;
            for(var m=0;m<resource.Meshes.Count;++m)
            {
                var key="collision:"+entity.Pointer.ToUInt64().ToString("X")+":"+id.ToString("X")+":"+m;
                if (!_copies.TryGetValue(key,out var copy))
                {
                    if (_copies.Count>=40000) { Partial=true; continue; }
                    copy=new Copy { Mesh=resource.Meshes[m] }; _copies.Add(key,copy);
                    copy.Entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                    if (copy.Entity==null) throw new InvalidOperationException("No collision map instance");
                    copy.Entity.AddMesh(copy.Mesh,recomputeBoundingBox:false); copy.Entity.SetReadyToRender(true);
                }
                copy.Seen=_generation; copy.Entity.SetFrame(ref frame);
                copy.Entity.SetVisibilityExcludeParents(true); yield return 0;
            }
        }
        private IEnumerable<int> Terrain()
        {
            var nx=_rules.GridX; var ny=_rules.GridY; var heights=new float[nx*ny];
            for(var y=0;y<ny;++y) for(var x=0;x<nx;++x)
            {
                var z=_source.GetTerrainHeight(new Vec2(_rules.X(x),_rules.Y(y)),checkHoles:true);
                if (!MapLiveGeometryRules.Finite(z) || Math.Abs(z)>100000) { z=float.NaN; ++_terrainInvalid; Partial=true; }
                heights[y*nx+x]=z; ++_terrainRead; yield return 0;
            }
            for(var by=0;by<ny-1;by+=16) for(var bx=0;bx<nx-1;bx+=16)
            {
                var builder=new MeshBuilder(); var count=0;
                for(var y=by;y<Math.Min(by+16,ny-1);++y) for(var x=bx;x<Math.Min(bx+16,nx-1);++x)
                {
                    var a=Point(x,y,heights[y*nx+x]); var b=Point(x+1,y,heights[y*nx+x+1]);
                    var c=Point(x,y+1,heights[(y+1)*nx+x]); var d=Point(x+1,y+1,heights[(y+1)*nx+x+1]);
                    if (MapLiveGeometryRules.Finite(a.z)&&MapLiveGeometryRules.Finite(b.z)&&MapLiveGeometryRules.Finite(c.z)&&MapLiveGeometryRules.Finite(d.z))
                    { Add(builder,a,b,c); Add(builder,b,d,c); count+=2; }
                    yield return 0;
                }
                if (count==0) continue;
                var mesh=builder.Finalize();
                if (mesh==null) throw new InvalidOperationException("No runtime terrain mesh");
                _terrainMeshes.Add(mesh); mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;
                var entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                if(entity==null) throw new InvalidOperationException("No runtime terrain entity");
                _terrainEntities.Add(entity); entity.AddMesh(mesh,recomputeBoundingBox:false);
                entity.SetVisibilityExcludeParents(true); entity.SetReadyToRender(true); yield return 0;
            }
        }
        private Vec3 Point(int x,int y,float z)
        { var p=_rules.Normalize(new GeometryPoint(_rules.X(x),_rules.Y(y),z)); return new Vec3(p.X,p.Y,p.Z); }
        private static void Add(MeshBuilder builder,Vec3 a,Vec3 b,Vec3 c)
        {
            var i=builder.AddFaceCorner(a,Vec3.Up,new Vec2(0,0),0xffb5b09bu);
            var j=builder.AddFaceCorner(b,Vec3.Up,new Vec2(1,0),0xffb5b09bu);
            var k=builder.AddFaceCorner(c,Vec3.Up,new Vec2(0,1),0xffb5b09bu); builder.AddFace(i,j,k);
        }
        private static bool Valid(MatrixFrame f) => Valid(f.origin)&&Valid(f.rotation.s)&&Valid(f.rotation.f)&&Valid(f.rotation.u);
        private static bool Valid(Vec3 p) => MapLiveGeometryRules.Finite(p.x)&&MapLiveGeometryRules.Finite(p.y)&&MapLiveGeometryRules.Finite(p.z);
        private static bool ValidTriangle(Vec3 a,Vec3 b,Vec3 c)
        {
            if(!Valid(a)||!Valid(b)||!Valid(c))return false;
            var x=b-a;var y=c-a;
            var nx=(double)x.y*y.z-(double)x.z*y.y;var ny=(double)x.z*y.x-(double)x.x*y.z;var nz=(double)x.x*y.y-(double)x.y*y.x;
            return nx*nx+ny*ny+nz*nz>1e-20;
        }
        private MatrixFrame Normalize(MatrixFrame frame)
        {
            var origin=_rules.Normalize(new GeometryPoint(frame.origin.x,frame.origin.y,frame.origin.z));
            frame.origin=new Vec3(origin.X,origin.Y,origin.Z);
            frame.rotation.s=Axis(frame.rotation.s); frame.rotation.f=Axis(frame.rotation.f); frame.rotation.u=Axis(frame.rotation.u);
            return frame;
        }
        private Vec3 Axis(Vec3 p) => new Vec3(p.y/_rules.Scale,-p.x/_rules.Scale,p.z/_rules.Scale);
        private string Status() => "generation="+_generation+" initialFinished="+InitialFinished+" partial="+Partial+
            " entities="+EntityCount+" copies="+CopiedMeshes+" skipped="+Skipped+" errors="+Errors+" sourceFaces="+SourceFaces+
            " collisionResources="+_collisionResources.Count+" collisionTriangles="+_collisionTriangles+" primitiveTriangles="+_primitiveTriangles+" unsupportedShapes="+_unsupportedShapes+" primitivesMeshed="+_primitives+
            " terrain="+_terrainRead+"/"+(_rules.GridX*_rules.GridY)+" terrainInvalid="+_terrainInvalid+
            " wallMs="+Ms(_wall.Elapsed.TotalMilliseconds)+" workMs="+Ms(_workMs)+" maxTickMs="+Ms(_maxTickMs)+" maxStepMs="+Ms(_maxStepMs);
        private static string Ms(double ms) => ms.ToString("F3",CultureInfo.InvariantCulture);
        private static void Report(string text) { TaleWorlds.Library.Debug.Print("BattleMiniMap live extraction "+text); }
        public void Dispose()
        {
            if(_disposed)return; _disposed=true; var steps=_steps; _steps=null;
            MapGeometryCleanup.Run(null,()=>steps?.Dispose()); Report("cancelled: "+Status());
            var meshes=new HashSet<Mesh>();
            foreach(var copy in _copies.Values) if(copy.Mesh!=null) meshes.Add(copy.Mesh);
            foreach(var resource in _collisionResources.Values) foreach(var mesh in resource.Meshes) meshes.Add(mesh);
            foreach(var mesh in meshes) MapGeometryCleanup.Run(null,()=>mesh.ManualInvalidate());
            foreach(var mesh in _terrainMeshes) MapGeometryCleanup.Run(null,()=>mesh.ManualInvalidate());
            _copies.Clear(); _collisionResources.Clear(); _terrainMeshes.Clear(); _terrainEntities.Clear();
        }
    }
}

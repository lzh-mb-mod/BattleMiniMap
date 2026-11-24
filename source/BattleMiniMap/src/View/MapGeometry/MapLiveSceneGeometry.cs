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
        private readonly uint _terrainColor, _objectColor, _renderColor;
        private readonly bool _rawTerrain;
        private readonly bool _simplifyTerrain;
        private readonly Dictionary<string,int> _errorReasons=new Dictionary<string,int>();
        private readonly HashSet<ulong> _errorShapes=new HashSet<ulong>();
        private int _errorExamples,_resourceExamples;
        private readonly Dictionary<string,double> _phaseMax=new Dictionary<string,double>();
        public int ErrorCount(string reason)=>_errorReasons.TryGetValue(reason,out var n)?n:0;
        private int _patchAccepted,_patchExact,_patchHoles;
        private long _rawSourceTriangles;
        private double _terrainError,_terrainWallMs,_terrainWorkMs,_activeWorkMs;
        private string _phase="initialization",_slowestPhase="none";
        private readonly MapExtractionDiagnostics _diagnostics;
        private readonly MapObjectExtractionProfiler _objectProfile;
        private int _tileIndex=-1,_chunkX=-1,_chunkY=-1,_patchX=-1,_patchY=-1;
        private readonly Func<IMapTerrainMemory> _rawMemory;
        private readonly Func<Action<IMapTerrainMemory>,IEnumerable<int>> _rawInitialization;
        private MapTerrainVerification _verification;
        private bool _collisionVerified;
        private sealed class CollisionResource
        {
            public PhysicsShape Source;
            public readonly Dictionary<string,int> ErrorReasons=new Dictionary<string,int>();
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
        private bool _terrainVisible=true;
        // Only owned terrain instances are affected; source/object state and
        // extraction coverage/counts are unchanged. New chunks inherit this state.
        public bool SetTerrainVisible(bool visible)
        {
            if(_disposed || _stopped || _terrainVisible==visible)return false;
            _terrainVisible=visible;
            foreach(var entity in _terrainEntities)entity.SetVisibilityExcludeParents(visible);
            ++ContentVersion;
            return true;
        }
        private IEnumerator<int> _steps;
        private readonly Stopwatch _wall=Stopwatch.StartNew();
        private double _workMs,_maxTickMs,_maxStepMs;
        private Stopwatch _activeTick;
        private int _ticks,_generation;
        private int _terrainUpdates,_terrainStepLimited,_terrainTimeLimited,_terrainWaitTicks,_terrainMaxSteps;
        public int TerrainStepLimitedTicks=>_terrainStepLimited;
        public int TerrainTimeLimitedTicks=>_terrainTimeLimited;
        public int TerrainWaitTicks=>_terrainWaitTicks;
        public int TerrainMaxStepsUsed=>_terrainMaxSteps;
        private bool _disposed,_stopped,_terrainDone,_terrainUnavailable,_terrainHasGaps;
        private readonly bool _hasTerrain;
        private float _refresh;
        private int _terrainRead,_terrainInvalid;
        private int _rawTilesTotal,_rawTilesDone,_rawSamples,_rawTriangles,_rawHoles,_rawChecks,_rawFailures,_nodeFloatBuffersMissing;
        private int _rawAttemptedSamples;
        private int _rawTileCellsDone,_rawTileCells;
        private float _rawMaxError;
        public int EntityCount { get; private set; }
        public int TerrainTriangles => _rawTriangles;
        public long SourceTerrainTriangles => _rawSourceTriangles;
        public int CopiedMeshes => _copies.Count;
        public int DisplayMeshes => _copies.Count+_terrainMeshes.Count;
        public bool IsCollision => _collision;
        public int Skipped { get; private set; }
        public int Errors { get; private set; }
        public long SourceFaces { get; private set; }
        public bool Partial { get; private set; }
        public bool InitialFinished { get; private set; }
        // Owners redraw through the final mutating step, including refresh scans.
        public bool IsActive => !_disposed && !_stopped && Failure == null && _steps != null;
        public long ContentVersion { get; private set; }
        public int TerrainPercent => _rawTerrain ? (_rawTilesTotal==0?0:Math.Min(100,(int)(100f*(_rawTilesDone+(_rawTileCells==0?0:_rawTileCellsDone/(float)_rawTileCells))/_rawTilesTotal))) : _terrainRead*100/(_rules.GridX*_rules.GridY);
        public string TerrainSource => _rawTerrain?"raw-physx":"sampled";
        public string Failure { get; private set; }
        public MapPreviewStatus Coverage => new MapPreviewStatus {
            TerrainEnded=_terrainDone, TerrainUnavailable=_terrainUnavailable || (!_hasTerrain && !_rawTerrain),
            TerrainHasGaps=_terrainHasGaps,
            TerrainPercent=TerrainPercent, InitialFinished=InitialFinished, Partial=Partial,
            Rejected=Errors, Skipped=Skipped, Unsupported=_unsupportedShapes, TerrainFailures=_rawFailures+_terrainInvalid
        };
        public MapLiveSceneGeometry(Scene source,Scene target,Material material,MapLiveGeometryRules rules,bool hasTerrain=true,bool collision=false,bool rawTerrain=false,Func<IMapTerrainMemory> rawMemory=null,bool simplifyTerrain=false,Func<Action<IMapTerrainMemory>,IEnumerable<int>> rawInitialization=null,bool profile=false,uint terrainColor=0xffb5b09bu,uint objectColor=0xffb5b09bu,uint renderColor=uint.MaxValue)
        {
            _terrainColor=terrainColor; _objectColor=objectColor; _renderColor=renderColor;
            _diagnostics=profile?new MapExtractionDiagnostics():null;
            _objectProfile=profile?new MapObjectExtractionProfiler():null;
            _source=source??throw new ArgumentNullException(nameof(source));
            _target=target; _material=material; _rules=rules;
            _hasTerrain=hasTerrain; _terrainDone=!hasTerrain && !rawTerrain;
            _collision=collision;
            _simplifyTerrain=simplifyTerrain;_rawTerrain=rawTerrain;_rawMemory=rawMemory;_rawInitialization=rawInitialization;
            _steps=Extract().GetEnumerator();
            Report("started: source="+(collision?"runtime-collision":"runtime-render")+" terrainAvailable="+hasTerrain+" terrain="+TerrainSource+" simplifyTerrain="+_simplifyTerrain+" budgetMs=2 terrainSteps=2048 objectSteps=64 domain="+
                rules.MinX+","+rules.MinY+","+rules.MaxX+","+rules.MaxY+" grid="+rules.GridX+"x"+rules.GridY);
        }
        public void Update(float dt)
        {
            if (_disposed || _stopped || Failure!=null) return;
            if (_steps==null)
            {
                if (MapLiveGeometryRules.Finite(dt) && dt>0) _refresh+=Math.Min(dt,1);
                if (_refresh<2) return;
                _refresh=0; _steps=Extract().GetEnumerator();
            }
            // Conservative change marker also covers a complete refresh within one tick.
            ++ContentVersion;
            var terrainTick=_rawTerrain&&!_terrainDone;var waiting=false;var stepLimit=terrainTick?2048:64;
            var tick=Stopwatch.StartNew(); var count=0; _activeWorkMs=_workMs; _activeTick=tick;
            _diagnostics?.BeginTick();
            try
            {
                while(count<(_rawTerrain&&!_terrainDone?2048:64) && tick.Elapsed.TotalMilliseconds<2)
                {
                    _objectProfile?.BeginStep();
                    var started=Stopwatch.GetTimestamp();bool more;
                    try { more=_steps.MoveNext(); }
                    finally { _objectProfile?.EndStep(); }
                    var stepMs=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency;
                    _phaseMax[_phase]=Math.Max(_phaseMax.TryGetValue(_phase,out var phaseMax)?phaseMax:0,stepMs);
                    if(stepMs>_maxStepMs){_maxStepMs=stepMs;_slowestPhase=_phase;}++count;
                    if(_diagnostics!=null&&stepMs>=MapExtractionDiagnostics.SlowStepMs)
                    {
                        var context=_diagnostics.SlowStep(stepMs,tick.Elapsed.TotalMilliseconds);
                        if(context!=null)Report("slow step: phase="+_phase+" tile="+_tileIndex+" chunk="+_chunkX+","+_chunkY+" patch="+_patchX+","+_patchY+" stepsInTick="+count+" generatedFaces="+_rawTriangles+" "+context+" "+_objectProfile.StepContext);
                    }
                    if (!more) { _steps.Dispose(); _steps=null; break; }
                    if(_steps.Current==MapTerrainVerification.Waiting){waiting=true;break;}
                }
            }
            catch(Exception ex)
            {
                Failure=ex.Message; Partial=true; var failedSteps=_steps; _steps=null;
                MapGeometryCleanup.Run(null,()=>failedSteps?.Dispose()); Report("failed: "+ex.Message);
            }
            if(_diagnostics!=null)
            {
                _diagnostics.EndTick();var context=_diagnostics.SlowTick(tick.Elapsed.TotalMilliseconds);
                if(context!=null)Report("slow tick: phase="+_phase+" tile="+_tileIndex+" chunk="+_chunkX+","+_chunkY+" patch="+_patchX+","+_patchY+" stepsInTick="+count+" generatedFaces="+_rawTriangles+" "+context);
            }
            if(terrainTick)
            {
                ++_terrainUpdates;_terrainMaxSteps=Math.Max(_terrainMaxSteps,count);
                if(waiting)++_terrainWaitTicks;
                else if(_steps!=null&&!_terrainDone)
                {if(tick.Elapsed.TotalMilliseconds>=2)++_terrainTimeLimited;else if(count>=stepLimit)++_terrainStepLimited;}
            }
            _workMs+=tick.Elapsed.TotalMilliseconds; _maxTickMs=Math.Max(_maxTickMs,tick.Elapsed.TotalMilliseconds);
            if (++_ticks%120==0 || _steps==null) Report(Status());
        }
        private IEnumerable<int> Extract()
        {
            ++_generation;
            if (_collision && !_collisionVerified)
            { _phase="collision-version-guard";MapLiveCollisionGuard.VerifyEngine(); _collisionVerified=true; yield return 0; }
            if (!_terrainDone)
            {
                _phase="terrain-init";
                foreach(var step in Terrain()) yield return step;
                _terrainDone=true;_terrainWallMs=_wall.Elapsed.TotalMilliseconds;_terrainWorkMs=_activeWorkMs+(_activeTick?.Elapsed.TotalMilliseconds??0);
                Report("terrain completed: "+Status());
            }
            _tileIndex=_chunkX=_chunkY=_patchX=_patchY=-1;
            _phase="object-enumeration";_objectProfile?.Mark(MapObjectStage.Enumerate);
            var all=new List<GameEntity>();
            _source.GetEntities(ref all); EntityCount=all.Count;
            yield return 0; // GetEntities is atomic: its time is included in maxStepMs.
            if (all.Count>200000) { Partial=true; throw new InvalidOperationException("Runtime entity limit exceeded"); }
            Skipped=0; long faces=0;
            _phase="objects";
            foreach(var entity in all)
            {
                _objectProfile?.Mark(MapObjectStage.Lookup,entity==null?0:entity.Pointer.ToUInt64());
                if (entity==null || entity.Pointer==UIntPtr.Zero) continue;
                if (_collision)
                {
                    foreach(var step in Collision(entity)) yield return step;
                    continue;
                }
                // Strong wrappers are held only during this scan; no raw pointers are dereferenced.
                if (entity.GetScenePointer()!=_source.Pointer || !entity.IsVisibleIncludeParents()) { ++Skipped; yield return 0; continue; }
                if (entity.Skeleton!=null) { ++Skipped; Partial=true; yield return 0; continue; }
                _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64());
                var global=entity.GetGlobalFrame(); var components=entity.MultiMeshComponentCount;
                yield return 0;
                if (components<0 || components>256) { ++Skipped; Partial=true; continue; }
                for(var m=0;m<components;++m)
                {
                    _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64(),mesh:m);
                    var meta=entity.GetMetaMesh(m); yield return 0;
                    if (meta==null || !meta.IsValid) { ++Skipped; Partial=true; continue; }
                    _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64(),mesh:m);
                    var n=meta.MeshCount; var metaFrame=meta.Frame; yield return 0;
                    if (n<0 || n>1024) { ++Skipped; Partial=true; continue; }
                    var masks=new int[n]; var combined=0;
                    for(var i=0;i<n;++i) { _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64(),mesh:m,item:i); masks[i]=meta.GetLodMaskForMeshAtIndex(i); combined|=masks[i]; yield return 0; }
                    var selected=MapLiveGeometryRules.CoarsestLod(combined);
                    if (selected==0) { ++Skipped; Partial=true; continue; }
                    for(var i=0;i<n;++i)
                    {
                        _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64(),mesh:m,item:i);
                        if ((masks[i]&selected)==0) continue;
                        var mesh=meta.GetMeshAtIndex(i); yield return 0;
                        _objectProfile?.Mark(MapObjectStage.RenderMetadata,entity.Pointer.ToUInt64(),mesh:m,item:i);
                        if (mesh==null || !mesh.IsValid) { ++Skipped; Partial=true; continue; }
                        var key=entity.Pointer.ToUInt64().ToString("X")+":"+meta.Pointer.ToUInt64().ToString("X")+":"+mesh.Pointer.ToUInt64().ToString("X")+":"+i;
                        var frame=global*metaFrame*mesh.GetLocalFrame();
                        if (!Valid(frame)) { ++Skipped; Partial=true; continue; }
                        frame=Normalize(frame);
                        if (!Valid(frame)) { RejectCollision("render_normalized_frame_nonfinite",entity,0,-1,-1,""); continue; }
                        faces+=mesh.GetFaceCount(); yield return 0;
                        _objectProfile?.Mark(MapObjectStage.InstanceLookup,entity.Pointer.ToUInt64(),mesh:m,item:i);
                        if (!_copies.TryGetValue(key,out var copy))
                        {
                            if (_copies.Count>=40000) { ++Skipped; Partial=true; continue; }
                            _objectProfile?.Mark(MapObjectStage.RenderCopy,entity.Pointer.ToUInt64(),mesh:m,item:i);
                            var owned=mesh.CreateCopy();
                            if (owned==null || !owned.IsValid) { RejectCollision("render_copy_invalid",entity,0,-1,-1,""); continue; }
                            // Register ownership before native setters can fail.
                            copy=new Copy { Mesh=owned }; _copies.Add(key,copy);
                            owned.SetMaterial(_material); owned.Color=_renderColor; owned.SetLocalFrame(MatrixFrame.Identity);
                            _objectProfile?.Mark(MapObjectStage.InstanceCreate,entity.Pointer.ToUInt64(),mesh:m,item:i);
                            copy.Entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                            if (copy.Entity==null) throw new InvalidOperationException("No runtime map entity");
                            copy.Entity.AddMesh(owned,recomputeBoundingBox:false);
                            copy.Entity.SetReadyToRender(true);
                        }
                        _objectProfile?.Mark(MapObjectStage.InstanceUpdate,entity.Pointer.ToUInt64(),mesh:m,item:i);
                        copy.Seen=_generation; copy.Entity.SetFrame(ref frame);
                        copy.Entity.SetVisibilityExcludeParents(true); yield return 0;
                    }
                    GC.KeepAlive(meta);
                }
                GC.KeepAlive(entity);
            }
            // Hide disappeared/replaced copies. Keep native resources until target teardown.
            foreach(var copy in _copies.Values)
            { _objectProfile?.Mark(MapObjectStage.HideStale,copy.Entity==null?0:copy.Entity.Pointer.ToUInt64()); if (copy.Seen!=_generation && copy.Entity!=null) copy.Entity.SetVisibilityExcludeParents(false); yield return 0; }
            _objectProfile?.Mark(MapObjectStage.Complete);
            SourceFaces=faces;
            InitialFinished=true;
            GC.KeepAlive(all);
        }
        private IEnumerable<int> Collision(GameEntity entity)
        {
            _objectProfile?.Mark(MapObjectStage.Lookup,entity.Pointer.ToUInt64());
            if (entity.GetScenePointer()!=_source.Pointer) yield break;
            var sourceShape=entity.GetBodyShape(); yield return 0;
            _objectProfile?.Mark(MapObjectStage.Lookup,entity.Pointer.ToUInt64());
            if (sourceShape==null || !sourceShape.IsValid) yield break;
            var id=sourceShape.Pointer.ToUInt64();
            if (!_collisionResources.TryGetValue(id,out var resource))
            {
                resource=new CollisionResource { Source=sourceShape }; _collisionResources.Add(id,resource);
                if (_collisionResources.Count>20000) throw new InvalidOperationException("Collision resource limit exceeded");
                _objectProfile?.Mark(MapObjectStage.CopyShape,entity.Pointer.ToUInt64(),id);
                var shape=sourceShape.CreateCopy();
                if (shape==null || !shape.IsValid || shape.Pointer==sourceShape.Pointer)
                { ++_unsupportedShapes; Partial=true; yield break; }
                try
                {
                    _objectProfile?.Mark(MapObjectStage.PrepareShape,entity.Pointer.ToUInt64(),id);
                    shape.Prepare(); yield return 0;
                    _objectProfile?.Mark(MapObjectStage.ShapeCounts,entity.Pointer.ToUInt64(),id);
                    var count=shape.TriangleMeshCount(); var spheres=shape.SphereCount(); var capsules=shape.CapsuleCount();
                    if(spheres<0 || capsules<0 || spheres+(long)capsules>4096)
                    { ++_unsupportedShapes; Partial=true; yield break; }
                    for(var p=0;p<spheres+capsules;++p)
                    {
                        _objectProfile?.Mark(MapObjectStage.PrimitiveRead,entity.Pointer.ToUInt64(),id,item:p);
                if(_collisionMeshCount>=40000 || _collisionTriangles+(long)_primitiveTriangles>=4000000)
                        { Partial=true;break; }
                        Vec3 a,b; float radius;
                        if (p<spheres)
                        { var data=new SphereData();shape.GetSphere(ref data,p);a=b=data.Origin;radius=data.Radius; }
                        else
                        { var data=new CapsuleData();shape.GetCapsule(ref data,p-spheres);a=data.P1;b=data.P2;radius=data.Radius; }
                        if(!Valid(a)||!Valid(b)||!MapLiveGeometryRules.Finite(radius)||radius<=0||radius>100000)
                        { RejectCollision(!Valid(a)||!Valid(b)?"primitive_endpoint_nonfinite":!MapLiveGeometryRules.Finite(radius)?"primitive_radius_nonfinite":radius<=0?"primitive_radius_nonpositive":"primitive_radius_out_of_range",entity,id,-1,p,"a="+Point(a)+" b="+Point(b)+" radius="+radius.ToString("R",CultureInfo.InvariantCulture));continue; }
                        _objectProfile?.Mark(MapObjectStage.PrimitiveBuild,entity.Pointer.ToUInt64(),id,item:p);
                        var builder=new MeshBuilder();var n=0;
                        foreach(var face in MapLivePrimitiveGeometry.Capsule(new GeometryPoint(a.x,a.y,a.z),new GeometryPoint(b.x,b.y,b.z),radius))
                        {
                            Add(builder,new Vec3(face[0].X,face[0].Y,face[0].Z),new Vec3(face[1].X,face[1].Y,face[1].Z),new Vec3(face[2].X,face[2].Y,face[2].Z),_objectColor);
                            ++n;yield return 0;
                            _objectProfile?.Mark(MapObjectStage.PrimitiveBuild,entity.Pointer.ToUInt64(),id,item:p);
                        }
                        _objectProfile?.Mark(MapObjectStage.PrimitiveUpload,entity.Pointer.ToUInt64(),id,item:p);
                        if(n>0)
                        {
                            if(_collisionTriangles+(long)_primitiveTriangles+n>4000000) { Partial=true;continue; }
                            var mesh=builder.Finalize();if(mesh==null)throw new InvalidOperationException("No collision primitive mesh");
                            resource.Meshes.Add(mesh);mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;resource.Triangles+=n;++_primitives;
                            _primitiveTriangles+=n;++_collisionMeshCount;
                        }
                        yield return 0;
                    }
                    _objectProfile?.Mark(MapObjectStage.StorageGuard,entity.Pointer.ToUInt64(),id);
                    if (count>0 && !MapLiveCollisionGuard.StorageReady(shape)) { ++_unsupportedShapes; Partial=true; count=0; }
                    if (count<0 || count>1024) { ++_unsupportedShapes; Partial=true; count=0; }
                    yield return 0;
                    var triangle=new Vec3[3];
                    for(var m=0;m<count;++m)
                    {
                        _objectProfile?.Mark(MapObjectStage.TriangleCount,entity.Pointer.ToUInt64(),id,m);
                        var triangles=shape.TriangleCountInTriangleMesh(m); yield return 0;
                        _objectProfile?.Mark(MapObjectStage.TriangleBuild,entity.Pointer.ToUInt64(),id,m);
                        if (triangles<0 || triangles>1000000 || _collisionTriangles+(long)_primitiveTriangles+triangles>4000000)
                        { ++_unsupportedShapes; Partial=true; continue; }
                        // Chunking bounds individual uploads and permits visible incremental progress.
                        for(var start=0;start<triangles;start+=2048)
                        {
                            if(_collisionMeshCount>=40000) { Partial=true;break; }
                            var builder=new MeshBuilder(); var valid=0;
                            for(var t=start;t<Math.Min(triangles,start+2048);++t)
                            {
                                _objectProfile?.Mark(MapObjectStage.TriangleBuild,entity.Pointer.ToUInt64(),id,m,t);
                                if (!MapLiveCollisionGuard.StorageReady(shape)) throw new InvalidOperationException("Prepared collision storage disappeared");
                                shape.GetTriangle(triangle,m,t);
                                if (ValidTriangle(triangle[0],triangle[1],triangle[2]))
                                { Add(builder,triangle[0],triangle[1],triangle[2],_objectColor); ++valid; }
                                else { RejectCollision(!Valid(triangle[0])||!Valid(triangle[1])||!Valid(triangle[2])?"triangle_nonfinite":"triangle_degenerate",entity,id,m,t,"a="+Point(triangle[0])+" b="+Point(triangle[1])+" c="+Point(triangle[2])); }
                                ++_collisionTriangles; yield return 0;
                            }
                            _objectProfile?.Mark(MapObjectStage.TriangleUpload,entity.Pointer.ToUInt64(),id,m,start);
                            if (valid==0) continue;
                            var mesh=builder.Finalize();
                            if(mesh==null) throw new InvalidOperationException("No collision map mesh");
                            resource.Meshes.Add(mesh); resource.Triangles+=valid;
                            ++_collisionMeshCount;
                            mesh.SetMaterial(_material);mesh.Color=uint.MaxValue; yield return 0;
                        }
                    }
                }
                finally { _objectProfile?.Mark(MapObjectStage.ReleaseShape,entity.Pointer.ToUInt64(),id); shape.ManualInvalidate(); GC.KeepAlive(sourceShape); }
                _objectProfile?.Mark(MapObjectStage.ResourceReport,entity.Pointer.ToUInt64(),id);
                if(resource.ErrorReasons.Count>0 && _resourceExamples<64)
                { ++_resourceExamples;Report("collision resource errors: shape="+id.ToString("X")+" reasons="+Reasons(resource.ErrorReasons)); }
            }
            _objectProfile?.Mark(MapObjectStage.BodyFrame,entity.Pointer.ToUInt64(),id);
            var frame=entity.GetBodyWorldTransform();
            if(!Valid(frame)) { RejectCollision(!Valid(frame.origin)?"body_origin_nonfinite":"body_axes_nonfinite",entity,id,-1,-1,"origin="+Point(frame.origin)+" s="+Point(frame.rotation.s)+" f="+Point(frame.rotation.f)+" u="+Point(frame.rotation.u));yield break; }
            frame=Normalize(frame);
            if(!Valid(frame)) { RejectCollision("body_normalized_frame_nonfinite",entity,id,-1,-1,"");yield break; }
            yield return 0;
            for(var m=0;m<resource.Meshes.Count;++m)
            {
                _objectProfile?.Mark(MapObjectStage.InstanceLookup,entity.Pointer.ToUInt64(),id,m);
                var key="collision:"+entity.Pointer.ToUInt64().ToString("X")+":"+id.ToString("X")+":"+m;
                if (!_copies.TryGetValue(key,out var copy))
                {
                    if (_copies.Count>=40000) { Partial=true; continue; }
                    _objectProfile?.Mark(MapObjectStage.InstanceCreate,entity.Pointer.ToUInt64(),id,m);
                    copy=new Copy { Mesh=resource.Meshes[m] }; _copies.Add(key,copy);
                    copy.Entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                    if (copy.Entity==null) throw new InvalidOperationException("No collision map instance");
                    copy.Entity.AddMesh(copy.Mesh,recomputeBoundingBox:false); copy.Entity.SetReadyToRender(true);
                }
                _objectProfile?.Mark(MapObjectStage.InstanceUpdate,entity.Pointer.ToUInt64(),id,m);
                copy.Seen=_generation; copy.Entity.SetFrame(ref frame);
                copy.Entity.SetVisibilityExcludeParents(true); yield return 0;
            }
        }
        private IEnumerable<int> Terrain()
        {
            if(_rawTerrain) { foreach(var step in RawTerrain()) yield return step; }
            else { foreach(var step in SampledTerrain()) yield return step; }
        }
        private IEnumerable<int> InitializeRawMemory(Action<IMapTerrainMemory> complete)
        {
            if(_rawInitialization!=null){foreach(var step in _rawInitialization(complete))yield return step;}
            else if(_rawMemory!=null)complete(_rawMemory());
            else {foreach(var step in MapSelfTerrainMemory.CreateSteps(complete,phase=>_phase=phase,verification=>_verification=verification))yield return step;}
        }
        private IEnumerable<int> StartRawTerrain(Action<MapRuntimeTerrainReader> complete)
        {
            IMapTerrainMemory memory=null;
            using(var steps=InitializeRawMemory(value=>memory=value).GetEnumerator())
            while(true)
            {
                bool more;
                try {more=steps.MoveNext();}
                catch(Exception ex){++_rawFailures;_terrainUnavailable=true;Partial=true;Report("raw terrain unavailable: "+ex.Message);yield break;}
                if(!more)break;
                yield return steps.Current;
            }
            try
            {
                _verification=(memory as MapSelfTerrainMemory)?.Verification;
                _phase="terrain-reader-bind";var bind=Stopwatch.StartNew();
                var reader=new MapRuntimeTerrainReader(memory,_source.Pointer.ToUInt64());
                if(_verification!=null)
                {
                    _verification.ReaderBindMs=bind.Elapsed.TotalMilliseconds;
                    Report("terrain verification completed: "+VerificationStatus());
                }
                complete(reader);
            }
            catch(Exception ex){++_rawFailures;_terrainUnavailable=true;Partial=true;Report("raw terrain unavailable: "+ex.Message);}
        }
        private IEnumerable<int> ReadRawTileSteps(MapRuntimeTerrainReader reader,int index,Action<MapTerrainHeightfield> complete)
        {
            using(var steps=reader.ReadTileSteps(index,complete).GetEnumerator())
            {
                while(true)
                {
                    bool more;
                    try { more=steps.MoveNext(); }
                    catch(Exception ex) { ++_rawFailures;Partial=true;Report("raw terrain tile skipped: tile="+index+" reason="+ex.Message);yield break; }
                    if(!more)yield break;
                    yield return 0;
                }
            }
        }
        private IEnumerable<int> RawTerrain()
        {
            _phase="terrain-reader-init";
            MapRuntimeTerrainReader reader=null;
            foreach(var step in StartRawTerrain(value=>reader=value))yield return step;
            yield return 0;
            if(reader==null)yield break;
            _rawTilesTotal=reader.TileCount;
            Report("raw terrain layout: mode="+reader.Mode+" tiles="+reader.TileCount+" scalarDataSource=False");
            for(var index=0;index<reader.TileCount;++index)
            {
                _tileIndex=index;_phase="terrain-copy";
                MapTerrainHeightfield tile=null;
                foreach(var step in ReadRawTileSteps(reader,index,value=>tile=value))yield return step;
                _nodeFloatBuffersMissing=reader.NodeFloatBuffersMissing;_rawAttemptedSamples=reader.AttemptedSamples;yield return 0;
                if(tile==null){++_rawTilesDone;if(reader.BudgetExhausted)yield break;continue;}
                if(_rawSamples+(long)tile.SampleCount>8000000){_terrainHasGaps=true;Partial=true;Report("raw terrain sample budget reached");yield break;}
                _rawSamples+=tile.SampleCount;
                var valid=true;
                // Coordinates are checked using five queries; geometry is solely from raw samples.
                var probes=new[]{0,tile.Columns-1,(tile.Rows-1)*tile.Columns,tile.SampleCount-1,(tile.Rows/2)*tile.Columns+tile.Columns/2};
                foreach(var probe in probes)
                {
                    // Validate the underlying field, not a hole-filtered replacement height.
                    _phase="terrain-coordinate-validation";
                    var p=tile.Vertex(probe);var actual=_source.GetTerrainHeight(new Vec2(p.X,p.Y),checkHoles:false);
                    var error=Math.Abs(actual-p.Z);++_rawChecks;
                    if(!MapLiveGeometryRules.Finite(actual)||!MapLiveGeometryRules.Finite(error)||error>Math.Max(.02f,tile.HeightScale*1.1f))valid=false;
                    if(MapLiveGeometryRules.Finite(error))_rawMaxError=Math.Max(_rawMaxError,error);
                    yield return 0;
                }
                if(!valid){++_rawFailures;Partial=true;++_rawTilesDone;Report("raw terrain validation rejected: tile="+index+" maxError="+_rawMaxError);continue;}
                Report("raw terrain tile: index="+index+" rows="+tile.Rows+" columns="+tile.Columns+" step="+tile.Step+" heightScale="+tile.HeightScale);
                _rawTileCells=(tile.Rows-1)*(tile.Columns-1);_rawTileCellsDone=0;
                for(var by=0;by<tile.Rows-1;by+=64) for(var bx=0;bx<tile.Columns-1;bx+=64)
                {
                    var w=Math.Min(64,tile.Columns-1-bx);var h=Math.Min(64,tile.Rows-1-by);
                    _chunkX=bx;_chunkY=by;
                    var corners=new int[(w+1)*(h+1)];var builder=new MeshBuilder();var count=0;var work=0;var indices=new int[3];
                    var selected=new List<MapTerrainPatch>(4);Action<MapTerrainPatch> collect=selected.Add;
                    for(var py=by;py<by+h;py+=8)for(var px=bx;px<bx+w;px+=8)
                    {
                        var pw=Math.Min(8,bx+w-px);var ph=Math.Min(8,by+h-py);
                        _patchX=px;_patchY=py;selected.Clear();
                        _phase="terrain-simplification";
                        foreach(var step in MapTerrainPatch.Select(tile,px,py,pw,ph,_simplifyTerrain,collect))yield return step;
                        foreach(var patch in selected)
                        {
                            if(patch.Simplified){++_patchAccepted;_terrainError=Math.Max(_terrainError,patch.Error);}
                            else {++_patchExact;if(patch.HasHoles)++_patchHoles;}
                            _phase="terrain-topology";
                            foreach(var face in patch.Faces())
                            {
                                if(_rawTriangles>=12000000){_terrainHasGaps=true;Partial=true;Report("raw terrain face budget reached");yield break;}
                                // Validate all normalized corners before uploading any
                                // part of this face, including finite / tiny-scale overflow.
                                var finiteFace=true;
                                for(var k=0;k<3;++k)
                                {
                                    var p=_rules.Normalize(tile.Vertex(face[k]));
                                    if(!Valid(new Vec3(p.X,p.Y,p.Z))) finiteFace=false;
                                }
                                if(!finiteFace)
                                {
                                    ++Errors; ++_rawFailures; Partial=true; _terrainHasGaps=true;
                                    if(_rawFailures==1)Report("raw terrain face rejected: normalized_nonfinite");
                                    continue;
                                }
                                for(var k=0;k<3;++k)
                                {
                                    var sampleIndex=face[k];
                                    var local=(sampleIndex/tile.Columns-by)*(w+1)+sampleIndex%tile.Columns-bx;
                                    var corner=corners[local]-1;
                                    if(corner<0)
                                    {
                                        var p=_rules.Normalize(tile.Vertex(sampleIndex));
                                        corner=builder.AddFaceCorner(new Vec3(p.X,p.Y,p.Z),Vec3.Up,new Vec2(0,0),_terrainColor);
                                        if(corner<0)throw new InvalidOperationException("No raw terrain vertex");
                                        corners[local]=corner+1;
                                    }
                                    indices[k]=corner;
                                }
                                builder.AddFace(indices[0],indices[1],indices[2]);++count;++_rawTriangles;
                                if(++work%32==0)yield return 0;
                            }
                        }
                        // Count input independently from emitted faces; hole flags are never filled.
                        for(var y=py;y<py+ph;++y)for(var x=px;x<px+pw;++x)for(var half=0;half<2;++half)
                        {
                            if(tile.Hole(y,x,half))++_rawHoles;else ++_rawSourceTriangles;
                            if(++work%32==0)yield return 0;
                        }
                        _rawTileCellsDone+=pw*ph;yield return 0;
                    }
                    if(count==0)continue;
                    _phase="terrain-upload";
                    var mesh=builder.Finalize();if(mesh==null)throw new InvalidOperationException("No raw terrain map mesh");
                    _terrainMeshes.Add(mesh);mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;
                    var entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                    if(entity==null)throw new InvalidOperationException("No raw terrain map entity");
                    _terrainEntities.Add(entity);entity.AddMesh(mesh,recomputeBoundingBox:false);
                    entity.SetVisibilityExcludeParents(_terrainVisible);entity.SetReadyToRender(true);yield return 0;
                }
                ++_rawTilesDone;_rawTileCells=_rawTileCellsDone=0;
            }
        }
        private IEnumerable<int> SampledTerrain()
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
                    if (Valid(a)&&Valid(b)&&Valid(c)&&Valid(d))
                    { Add(builder,a,b,c,_terrainColor); Add(builder,b,d,c,_terrainColor); count+=2; }
                    else { ++Errors; Partial=true; _terrainHasGaps=true; }
                    yield return 0;
                }
                if (count==0) continue;
                var mesh=builder.Finalize();
                if (mesh==null) throw new InvalidOperationException("No runtime terrain mesh");
                _terrainMeshes.Add(mesh); mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;
                var entity=GameEntity.CreateEmpty(_target,createPhysics:false,callScriptCallbacks:false);
                if(entity==null) throw new InvalidOperationException("No runtime terrain entity");
                _terrainEntities.Add(entity); entity.AddMesh(mesh,recomputeBoundingBox:false);
                entity.SetVisibilityExcludeParents(_terrainVisible); entity.SetReadyToRender(true); yield return 0;
            }
        }
        private Vec3 Point(int x,int y,float z)
        { var p=_rules.Normalize(new GeometryPoint(_rules.X(x),_rules.Y(y),z)); return new Vec3(p.X,p.Y,p.Z); }
        private static void Add(MeshBuilder builder,Vec3 a,Vec3 b,Vec3 c,uint color)
        {
            var i=builder.AddFaceCorner(a,Vec3.Up,new Vec2(0,0),color);
            var j=builder.AddFaceCorner(b,Vec3.Up,new Vec2(1,0),color);
            var k=builder.AddFaceCorner(c,Vec3.Up,new Vec2(0,1),color); builder.AddFace(i,j,k);
        }
        private static string Point(Vec3 p)=>p.x.ToString("R",CultureInfo.InvariantCulture)+","+p.y.ToString("R",CultureInfo.InvariantCulture)+","+p.z.ToString("R",CultureInfo.InvariantCulture);
        private void RejectCollision(string reason,GameEntity entity,ulong shape,int mesh,int item,string detail)
        {
            ++Errors;Partial=true;_errorReasons[reason]=ErrorCount(reason)+1;if(shape!=0)_errorShapes.Add(shape);
            if(shape!=0&&_collisionResources.TryGetValue(shape,out var resource))
                resource.ErrorReasons[reason]=(resource.ErrorReasons.TryGetValue(reason,out var old)?old:0)+1;
            if(_errorExamples<32&&ErrorCount(reason)<=4)
            {
                ++_errorExamples;
                Report("object rejected: reason="+reason+" entity="+entity.Pointer.ToUInt64().ToString("X")+" shape="+shape.ToString("X")+" mesh="+mesh+" item="+item+" "+detail);
            }
        }
        private static string Reasons(Dictionary<string,int> reasons)
        {
            var keys=new List<string>(reasons.Keys);keys.Sort(StringComparer.Ordinal);
            var values=new List<string>();foreach(var key in keys)values.Add(key+":"+reasons[key]);
            return values.Count==0?"none":string.Join(",",values);
        }
        private string PhaseTimes()
        {
            var keys=new List<string>(_phaseMax.Keys);keys.Sort(StringComparer.Ordinal);var parts=new List<string>();
            foreach(var key in keys)parts.Add(key+":"+Ms(_phaseMax[key]));return string.Join(",",parts);
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
            " terrainSource="+TerrainSource+" rawTiles="+_rawTilesDone+"/"+_rawTilesTotal+" rawSamples="+_rawSamples+" rawTriangles="+_rawTriangles+
            " rawAttemptedSamples="+_rawAttemptedSamples+" rawSourceTriangles="+_rawSourceTriangles+" simplifiedPatches="+_patchAccepted+" exactPatches="+_patchExact+" holePatches="+_patchHoles+" acceptedMaxError="+_terrainError.ToString("R",CultureInfo.InvariantCulture)+
            " rawCells="+_rawTileCellsDone+"/"+_rawTileCells+" terrainMeshes="+_terrainMeshes.Count+
            " rawHoles="+_rawHoles+" validationQueries="+_rawChecks+" validationMaxError="+_rawMaxError.ToString("R",CultureInfo.InvariantCulture)+
            " "+VerificationStatus()+" terrainUpdateTicks="+_terrainUpdates+" terrainStepLimited="+_terrainStepLimited+" terrainTimeLimited="+_terrainTimeLimited+" terrainWaitTicks="+_terrainWaitTicks+" terrainMaxStepsUsed="+_terrainMaxSteps+
            " rawFailures="+_rawFailures+" nodeFloatBuffersMissing="+_nodeFloatBuffersMissing+
            " "+(_diagnostics?.Summary??"slowDiagnostics=off")+" "+(_objectProfile?.Summary??"objectSegments=off")+" errorReasons="+Reasons(_errorReasons)+" errorResourceSummaries="+_resourceExamples+" stepMaxByPhase="+PhaseTimes()+" errorShapes="+_errorShapes.Count+" errorExamples="+_errorExamples+" phase="+_phase+" slowestPhase="+_slowestPhase+" terrainWallMs="+Ms(_terrainWallMs)+" terrainWorkMs="+Ms(_terrainWorkMs)+
            " wallMs="+Ms(_wall.Elapsed.TotalMilliseconds)+" workMs="+Ms(_workMs)+" maxTickMs="+Ms(_maxTickMs)+" maxStepMs="+Ms(_maxStepMs);
        private string VerificationStatus()=>"verificationState="+(_verification?.State??"none")+" verificationWorkerMs="+Ms(_verification?.WorkerMs??0)+" verificationWaitMs="+Ms(_verification?.WaitMs??0)+" verificationPolls="+(_verification?.Polls??0)+
            " discoveryMs="+Ms(_verification?.DiscoveryMs??0)+" dispatchMs="+Ms(_verification?.DispatchMs??0)+" recheckMs="+Ms(_verification?.RecheckMs??0)+" readerBindMs="+Ms(_verification?.ReaderBindMs??0);
        private static string Ms(double ms) => ms.ToString("F3",CultureInfo.InvariantCulture);
        private static void Report(string text) { TaleWorlds.Library.Debug.Print("BattleMiniMap live extraction "+text); }
        // Stop reads immediately; owned display resources remain until the target is detached.
        public void Stop()
        {
            if(_stopped)return; _stopped=true; var steps=_steps; _steps=null;
            MapGeometryCleanup.Run(null,()=>steps?.Dispose()); Report("cancelled: "+Status());
        }
        public void Dispose()
        {
            if(_disposed)return; _disposed=true; Stop();
            var meshes=new HashSet<Mesh>();
            foreach(var copy in _copies.Values) if(copy.Mesh!=null) meshes.Add(copy.Mesh);
            foreach(var resource in _collisionResources.Values) foreach(var mesh in resource.Meshes) meshes.Add(mesh);
            foreach(var mesh in meshes) MapGeometryCleanup.Run(null,()=>mesh.ManualInvalidate());
            foreach(var mesh in _terrainMeshes) MapGeometryCleanup.Run(null,()=>mesh.ManualInvalidate());
            _copies.Clear(); _collisionResources.Clear(); _terrainMeshes.Clear(); _terrainEntities.Clear();
        }
    }
}

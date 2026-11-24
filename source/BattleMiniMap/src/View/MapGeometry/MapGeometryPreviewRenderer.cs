using System;
using System.Collections.Generic;
using System.Threading;
using BattleMiniMap.Config;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapGeometry
{
    // Interactive preview is intentionally isolated from the frozen main-map Tableau.
    internal sealed class MapGeometryPreviewRenderer : IDisposable
    {
        internal static MapGeometryPreviewRenderer Active { get; private set; }
        internal readonly MapGeometryPreviewCamera Orbit = new MapGeometryPreviewCamera();
        private sealed class Binding
        {
            public Scene Scene; public Camera Camera; public volatile bool Retired;
            public string Name, Failure; public int Paints;
        }
        private static int _nextId;
        private Binding _binding;
        private Scene _scene; private Camera _camera; private TableauView _view;
        private Material _material; private Texture _white, _target;
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameEntity> _entities = new List<GameEntity>();
        private readonly List<Tuple<int,Action>> _retired = new List<Tuple<int,Action>>();
        private int _tick; private bool _proxy, _failed, _open;
        private float? _ceiling;
        private float _aspect = 960f/608;
        private readonly Scene _runtimeSource;
        private readonly float _minX,_minY,_maxX,_maxY;
        private MapLiveSceneGeometry _live;
        private bool _liveMode;
        private bool _collisionMode;
        private bool _rawTerrainMode,_simplifyTerrainMode;
        public UiTexture Texture { get; private set; }
        public bool Available => Texture != null && !_failed;
        public MapGeometryPreviewRenderer(Scene runtimeSource=null,float minX=0,float minY=0,float maxX=1,float maxY=1)
        { _runtimeSource=runtimeSource; _minX=minX; _minY=minY; _maxX=maxX; _maxY=maxY; Active = this; }
        public MapLiveSceneGeometry Live => _live;
        public void SetAspect(float aspect) { if (aspect > .1f && aspect < 10) _aspect = aspect; }
        public void Update(MapGeometryRenderer source,float dt=0)
        {
            ++_tick;
            for (var i=_retired.Count-1;i>=0;--i) if (_retired[i].Item1<=_tick)
            { var release=_retired[i].Item2; _retired.RemoveAt(i); release(); }
            var config=BattleMiniMapConfig.Get();
            if (!config.ShowGeometryPreview)
            {
                if (_open) { Clear(true); _failed=false; _open=false; }
                return;
            }
            if (!_open) { _open=true; Orbit.Reset(); }
            if (_failed) return;
            try
            {
                var live=config.UseLiveScenePreview;
                var data=live ? null : source?.EnsureVerifiedCache();
                if (live && _runtimeSource==null) return;
                if (!live && data==null) return;
                var ceiling=MapGeometryHeight.Ceiling(config.CutMapGeometryByHeight,config.MapGeometryCeiling);
                if (_view==null || live!=_liveMode || (live && (_collisionMode!=config.UseCollisionScenePreview || _rawTerrainMode!=config.UseRawRuntimeTerrain || (_rawTerrainMode&&_simplifyTerrainMode!=config.SimplifyRuntimeTerrain))) || (!live && (_proxy!=config.UseMapGeometryProxy || _ceiling!=ceiling)))
                {
                    Clear(true); _liveMode=live; _collisionMode=config.UseCollisionScenePreview; _rawTerrainMode=config.UseRawRuntimeTerrain; _simplifyTerrainMode=config.SimplifyRuntimeTerrain; _proxy=config.UseMapGeometryProxy; _ceiling=ceiling; Build(data);
                }
                _live?.Update(dt);
                if (_live?.Failure!=null) throw new InvalidOperationException(_live.Failure);
                if (_binding.Failure!=null) throw new InvalidOperationException(_binding.Failure);
                var eye=Orbit.Eye; var at=Orbit.Target;
                _camera.LookAt(ToVec(eye),ToVec(at),Vec3.Up);
                _camera.SetFovVertical((float)Math.PI/4,_aspect,.05f,64);
                _view.SetDoNotRenderThisFrame(false); _view.SetEnable(true);
            }
            catch (Exception ex)
            { _failed=true; Clear(true); Report("disabled; main map retained: "+ex.Message); }
        }
        private static Vec3 ToVec(GeometryPoint p) => new Vec3(p.X,p.Y,p.Z);
        private void Build(MapGeometryData data)
        {
            _scene=Scene.CreateNewScene(initialize_physics:false,enable_decals:false);
            if (_scene==null) throw new InvalidOperationException("No preview scene");
            _scene.DisableStaticShadows(true); _scene.SetName("BattleMiniMap.GeometryPreview"); _scene.EnsurePostfxSystem();
            _camera=Camera.CreateCamera();
            if (_camera==null) throw new InvalidOperationException("No preview camera");
            // Initialize before registering a callback; Update applies the current pose.
            _camera.LookAt(ToVec(Orbit.Eye),ToVec(Orbit.Target),Vec3.Up);
            _camera.SetFovVertical((float)Math.PI/4,_aspect,.05f,64);
            _white=TaleWorlds.Engine.Texture.CreateFromByteArray(new byte[]{255,255,255,255},1,1);
            if (_white==null || !_white.IsValid) throw new InvalidOperationException("No preview white texture");
            _material=Material.GetDefaultMaterial().CreateCopy(); MapGeometryShader.Bind(_material);
            _material.SetShaderFlags(0); _material.SetTexture(Material.MBTextureType.DiffuseMap,_white);
            _material.Flags=MaterialFlags.TwoSided|MaterialFlags.DontCastShadow|MaterialFlags.RequiresForwardRendering;
            _material.SetAlphaBlendMode(Material.MBAlphaBlendMode.NoAlphaBlend); _material.SetAlphaTestValue(0);
            _scene.SetShadow(false);
            if (_liveMode)
            {
                _runtimeSource.GetTerrainData(out var dimensions,out var nodeSize,out var layers,out var version);
                var hasTerrain=dimensions.X>0 && dimensions.Y>0 && nodeSize>0 && MapLiveGeometryRules.Finite(nodeSize);
                var rules=hasTerrain ? new MapLiveGeometryRules(0,0,dimensions.X*nodeSize,dimensions.Y*nodeSize)
                    : new MapLiveGeometryRules(_minX,_minY,_maxX,_maxY);
                _live=new MapLiveSceneGeometry(_runtimeSource,_scene,_material,rules,hasTerrain,_collisionMode,_rawTerrainMode,simplifyTerrain:_simplifyTerrainMode);
                BuildTarget();
                Report("build: target="+_binding.Name+" source="+(_collisionMode?"runtime-collision":"runtime-render")+" staticFiles=False policy=interactive");
                return;
            }
            var normalization=new MapGpuDiagnosticGeometry(data); var builder=new MeshBuilder(); var count=0;
            foreach (var layer in data.Layers)
                foreach (var triangle in MapGeometryData.Triangles(data.Select(layer,_proxy),_ceiling))
                {
                    var a=ToVec(normalization.Normalize(triangle[0])); var b=ToVec(normalization.Normalize(triangle[1])); var c=ToVec(normalization.Normalize(triangle[2]));
                    var u=b-a; var v=c-a;
                    var n=new Vec3(u.y*v.z-u.z*v.y,u.z*v.x-u.x*v.z,u.x*v.y-u.y*v.x);
                    if (n.Length<1e-9f) continue;
                    n.Normalize();
                    var color=layer.Resource=="stone_bridge_wide" ? 0xffceb88fu : 0xff82a6b7u;
                    var i=builder.AddFaceCorner(a,n,new Vec2(.5f,.5f),color);
                    var j=builder.AddFaceCorner(b,n,new Vec2(.5f,.5f),color);
                    var k=builder.AddFaceCorner(c,n,new Vec2(.5f,.5f),color);
                    builder.AddFace(i,j,k); ++count;
                }
            if (count>0)
            {
                var mesh=builder.Finalize();
                if (mesh==null) throw new InvalidOperationException("No preview mesh");
                _meshes.Add(mesh); mesh.UpdateBoundingBox(); mesh.SetMaterial(_material); mesh.Color=uint.MaxValue;
                var entity=GameEntity.CreateEmpty(_scene,createPhysics:false,callScriptCallbacks:false);
                if (entity==null) throw new InvalidOperationException("No preview entity");
                _entities.Add(entity); entity.AddMesh(mesh,recomputeBoundingBox:true);
                entity.SetVisibilityExcludeParents(true); entity.SetReadyToRender(true);
            }
            BuildTarget();
            Report("build: target="+_binding.Name+" triangles="+count+" proxy="+_proxy+" ceiling="+(_ceiling?.ToString(System.Globalization.CultureInfo.InvariantCulture)??"none")+" policy=interactive");
        }
        private void BuildTarget()
        {
            _binding=new Binding { Scene=_scene,Camera=_camera,Name="BattleMiniMap.GeometryPreview."+Interlocked.Increment(ref _nextId) };
            _target=TableauView.AddTableau(_binding.Name,Paint,_binding,960,608);
            if (_target==null || !_target.IsValid) throw new InvalidOperationException("No preview target");
            _view=_target.TableauView;
            if (_view==null) throw new InvalidOperationException("No preview view");
            Configure(_view,_binding); _view.SetDoNotRenderThisFrame(false);
            Texture=new UiTexture(new EngineTexture(_target));
        }
        private static void Configure(TableauView view,Binding binding)
        {
            binding.Scene.EnsurePostfxSystem(); view.SetScene(binding.Scene); view.SetCamera(binding.Camera);
            view.SetClearColor(new Color(.07f,.09f,.12f,1).ToUnsignedInteger());
            view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearColor,true);
            view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearDepth,true);
            view.SetCleanScreenUntilLoadingDone(false); view.SetClearAndDisableAfterSucessfullRender(false);
            view.SetRenderWithPostfx(false); view.SetSceneUsesSkybox(false); view.SetSceneUsesShadows(false); view.SetSceneUsesContour(false);
            view.SetAcceptGlobalDebugRenderObjects(false); view.SetRenderOrder(-1);
            view.SetDeleteAfterRendering(false); view.SetContinuousRendering(true); view.SetEnable(true);
        }
        private static void Paint(Texture sender,EventArgs args)
        {
            Binding binding=null;
            try
            {
                if (sender==null || sender.IsReleased || !sender.IsValid) return;
                binding=sender.UserData as Binding;
                if (binding==null || binding.Retired) return;
                var view=sender.TableauView; Configure(view,binding); view.SetDoNotRenderThisFrame(true);
                if (Interlocked.Increment(ref binding.Paints)==1) Report("callback: target="+binding.Name);
            }
            catch (Exception ex) { if (binding!=null && !binding.Retired) binding.Failure=ex.Message; }
        }
        private static void Report(string message) { TaleWorlds.Library.Debug.Print("BattleMiniMap geometry preview "+message); }
        private void Clear(bool defer)
        {
            if (_binding!=null)
            {
                _binding.Retired=true; _binding.Scene=null; _binding.Camera=null;
                Report("retired: target="+_binding.Name+" paints="+_binding.Paints); _binding=null;
            }
            Texture=null;
            var view=_view; var scene=_scene; var camera=_camera; var material=_material;
            var white=_white; var target=_target; var meshes=_meshes.ToArray(); var entities=_entities.ToArray(); var live=_live;
            _live=null;
            _view=null; _scene=null; _camera=null; _material=null; _white=null; _target=null; _meshes.Clear(); _entities.Clear();
            Action<Exception> report=ex=>Report("cleanup: "+ex.Message);
            if (view!=null) MapGeometryCleanup.Run(report,()=>view.SetDoNotRenderThisFrame(true),()=>view.SetContinuousRendering(false),
                ()=>view.SetEnable(false),()=>view.AddClearTask(clearOnlySceneview:true));
            Action release=()=>
            {
                MapGeometryCleanup.Run(report,()=>scene?.ClearAll(),()=>scene?.ManualInvalidate());
                MapGeometryCleanup.Run(report,()=>live?.Dispose());
                foreach(var mesh in meshes) MapGeometryCleanup.Run(report,()=>mesh.ManualInvalidate());
                MapGeometryCleanup.Run(report,()=>material?.ManualInvalidate(),()=>white?.Release(),()=>target?.Release(),()=>camera?.ReleaseCamera());
                GC.KeepAlive(entities); GC.KeepAlive(view);
            };
            if (defer && (scene!=null || target!=null)) _retired.Add(Tuple.Create(_tick+2,release)); else release();
        }
        public void Dispose()
        {
            if (Active==this) Active=null;
            BattleMiniMapConfig.Get().ShowGeometryPreview=false;
            Clear(false); foreach(var release in _retired) release.Item2(); _retired.Clear();
        }
    }
}

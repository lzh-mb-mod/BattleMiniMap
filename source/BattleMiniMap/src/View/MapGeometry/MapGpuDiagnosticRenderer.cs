using System;
using System.Collections.Generic;
using System.Globalization;
using BattleMiniMap.Config;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using UiTexture = TaleWorlds.TwoDimension.Texture;
namespace BattleMiniMap.View.MapGeometry
{
    // Optional private-scene diagnostic. Never changes the working CPU map texture.
    internal sealed class MapGpuDiagnosticRenderer : IDisposable
    {
        internal static MapGpuDiagnosticRenderer Active { get; private set; }
        private Scene _scene; private SceneView _view; private Camera _camera;
        private Material _material; private Texture _white, _target, _control;
        public UiTexture ControlTexture { get; private set; }
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<GameEntity> _entities = new List<GameEntity>();
        private int _mode, _ticks, _reports; private bool _depth, _proxy, _failed;
        private readonly List<Tuple<int,Action>> _retired = new List<Tuple<int,Action>>();
        public UiTexture Texture { get; private set; }
        public MapGpuDiagnosticRenderer(){Active=this;}
        public void Update(MapGeometryData data)
        {
            ++_ticks;
            for(var i=_retired.Count-1;i>=0;--i)if(_retired[i].Item1<=_ticks){var action=_retired[i].Item2;_retired.RemoveAt(i);action();}
            var config=BattleMiniMapConfig.Get();var mode=Math.Max(0,Math.Min(3,config.MapGpuDiagnosticMode));
            if(mode==0 || data==null){if(_view!=null)Clear(true);_mode=0;return;}
            if(ControlTexture==null)
            {
                _control=TaleWorlds.Engine.Texture.CreateFromByteArray(new byte[]{230,70,220,255},1,1);
                ControlTexture=new UiTexture(new EngineTexture(_control));
            }
            if(_failed)return;
            try
            {
                if(_view==null || mode!=_mode || config.MapGpuDiagnosticDepth!=_depth || config.UseMapGeometryProxy!=_proxy)
                {
                    Clear(true);_mode=mode;_depth=config.MapGpuDiagnosticDepth;_proxy=config.UseMapGeometryProxy;
                    Build(data);_reports=0;
                }
                _view.SetEnable(true);
                if(_reports<4 && _ticks%60==0)
                {
                    ++_reports;Debug.Print("BattleMiniMap GPU diagnostic ready: mode="+_mode+" view="+_view.ReadyToRender()+
                        " scene="+_view.CheckSceneReadyToRender()+" loading="+_scene.GetLoadingStateName()+" frame="+_camera.Frame);
                }
            }
            catch(Exception ex){_failed=true;Clear(true);Debug.Print("BattleMiniMap GPU diagnostic disabled; CPU retained: "+ex.Message);}
        }
        private void Build(MapGeometryData data)
        {
            _scene=Scene.CreateNewScene(initialize_physics:false,enable_decals:false);
            // Like BannerTableau: disabling view shadows alone does not skip scene shadow preparation.
            _scene.DisableStaticShadows(true);
            _scene.SetName("BattleMiniMap.GpuDiagnostic");
            _scene.EnsurePostfxSystem();
            _camera=Camera.CreateCamera();var frame=MatrixFrame.Identity;frame.origin.z=400;_camera.Frame=frame;
            _camera.LookAt(new Vec3(0,0,400),Vec3.Zero,Vec3.Forward);
            _camera.SetViewVolume(false,-1.25f,1.25f,-1.25f,1.25f,.1f,500);
            _white=TaleWorlds.Engine.Texture.CreateFromByteArray(new byte[]{255,255,255,255},1,1);
            _material=Material.GetDefaultMaterial().CreateCopy();_material.SetShader(Shader.GetFromResource("simple_shading"));
            _material.SetShaderFlags(0);_material.SetTexture(Material.MBTextureType.DiffuseMap,_white);
            _material.Flags=MaterialFlags.TwoSided|MaterialFlags.DontCastShadow|MaterialFlags.RequiresForwardRendering;
            if(!_depth)_material.Flags|=MaterialFlags.NoDepthTest|MaterialFlags.NoModifyDepthBuffer;
            _material.SetAlphaBlendMode(Material.MBAlphaBlendMode.NoAlphaBlend);_material.SetAlphaTestValue(0);
            var builder=new MeshBuilder();var count=0;var normalizer=new MapGpuDiagnosticGeometry(data);
            if(_mode==2){Add(builder,MapGpuDiagnosticGeometry.Triangle,0xffffd060);count=1;}
            if(_mode==3)foreach(var layer in data.Layers)foreach(var t in MapGeometryData.Triangles(data.Select(layer,_proxy),null))
            {Add(builder,new[]{normalizer.Normalize(t[0]),normalizer.Normalize(t[1]),normalizer.Normalize(t[2])},layer.Resource=="stone_bridge_wide"?0xffceb88f:0xff82a6b7);++count;}
            if(count>0)
            {
                var mesh=builder.Finalize();mesh.UpdateBoundingBox();mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;
                var entity=GameEntity.CreateEmpty(_scene,createPhysics:false,callScriptCallbacks:false);
                entity.AddMesh(mesh,recomputeBoundingBox:true);entity.SetVisibilityExcludeParents(true);entity.SetReadyToRender(true);
                _meshes.Add(mesh);_entities.Add(entity);
            }
            _target=TaleWorlds.Engine.Texture.CreateRenderTarget("BattleMiniMap.GpuDiagnostic",256,256,autoMipmaps:false,isTableau:false);
            Texture=new UiTexture(new EngineTexture(_target));_view=SceneView.CreateSceneView();_view.SetScene(_scene);_view.SetCamera(_camera);_view.SetRenderTarget(_target);
            _view.SetAutoDepthTargetCreation(true);_view.SetClearColor(new Color(.12f,.22f,.35f,1f).ToUnsignedInteger());
            _view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearColor,true);_view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearDepth,true);
            _view.SetRenderOnDemand(false);_view.SetCleanScreenUntilLoadingDone(false);_view.SetClearAndDisableAfterSucessfullRender(false);
            _view.SetRenderWithPostfx(false);_view.SetSceneUsesSkybox(false);_view.SetSceneUsesShadows(false);_view.SetSceneUsesContour(false);
            _view.SetAcceptGlobalDebugRenderObjects(false);_view.SetRenderOrder(-1);_view.SetEnable(true);
            var cameraPoint=new Vec3(0,0,0);var actual=_camera.WorldPointToViewPortPoint(ref cameraPoint);
            var expected=MapGpuDiagnosticGeometry.ExpectedViewport(new GeometryPoint(0,0,0));
            Debug.Print("BattleMiniMap GPU diagnostic build: staticShadowsDisabled=True mode="+_mode+" depth="+_depth+" triangles="+count+" proxy="+_proxy+
                " center="+normalizer.Center.X+","+normalizer.Center.Y+","+normalizer.Center.Z+" scale="+normalizer.Scale+
                " cameraOrigin="+_camera.Position+" cameraViewport="+actual+" expectedXY="+expected.X+","+expected.Y+" near="+_camera.Near+" far="+_camera.Far);
            foreach(var point in MapGpuDiagnosticGeometry.Triangle)
            {
                var vertex=new Vec3(point.X,point.Y,point.Z);var projected=_camera.WorldPointToViewPortPoint(ref vertex);
                var want=MapGpuDiagnosticGeometry.ExpectedViewport(point);
                Debug.Print("BattleMiniMap GPU diagnostic camera: point="+vertex+" viewport="+projected+" expectedXY="+want.X.ToString("R",CultureInfo.InvariantCulture)+","+want.Y.ToString("R",CultureInfo.InvariantCulture));
            }
        }
        private static void Add(MeshBuilder builder,GeometryPoint[] triangle,uint color)
        {
            var a=new Vec3(triangle[0].X,triangle[0].Y,triangle[0].Z);var b=new Vec3(triangle[1].X,triangle[1].Y,triangle[1].Z);var c=new Vec3(triangle[2].X,triangle[2].Y,triangle[2].Z);
            var x=b-a;var y=c-a;var n=new Vec3(x.y*y.z-x.z*y.y,x.z*y.x-x.x*y.z,x.x*y.y-x.y*y.x);if(n.Length<1e-9f)return;n.Normalize();
            var i=builder.AddFaceCorner(a,n,new Vec2(.5f,.5f),color);var j=builder.AddFaceCorner(b,n,new Vec2(.5f,.5f),color);var k=builder.AddFaceCorner(c,n,new Vec2(.5f,.5f),color);builder.AddFace(i,j,k);
        }
        private void Clear(bool defer)
        {
            Texture=null;var view=_view;var scene=_scene;var camera=_camera;var material=_material;var white=_white;var target=_target;var meshes=_meshes.ToArray();var entities=_entities.ToArray();
            _view=null;_scene=null;_camera=null;_material=null;_white=null;_target=null;_meshes.Clear();_entities.Clear();
            if(view!=null){view.SetEnable(false);view.AddClearTask(clearOnlySceneview:true);}
            Action release=()=>{if(scene!=null){scene.ClearAll();scene.ManualInvalidate();}foreach(var mesh in meshes)mesh.ManualInvalidate();material?.ManualInvalidate();white?.Release();target?.Release();camera?.ReleaseCamera();GC.KeepAlive(entities);GC.KeepAlive(view);};
            if(defer && scene!=null)_retired.Add(Tuple.Create(_ticks+2,release));else release();
        }
        public void Dispose(){if(Active==this)Active=null;Clear(false);foreach(var entry in _retired)entry.Item2();_retired.Clear();ControlTexture=null;_control?.Release();_control=null;}
    }
}

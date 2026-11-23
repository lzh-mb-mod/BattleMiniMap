using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapGeometryRenderer : IDisposable
    {
        internal static MapGeometryRenderer Active { get; private set; }
        private readonly string _sceneName;
        private MapGeometryData _data;
        private bool _attempted, _failed, _lastProxy, _proxyNativeFailed;
        private float? _lastCeiling;
        private Vec2 _min, _max;
        private int _width, _height;
        private float _resolution;
        private Scene _scene;
        private Camera _camera;
        private SceneView _view;
        private Material _material;
        private Texture _white, _target;
        private readonly List<GameEntity> _entities = new List<GameEntity>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Tuple<int, Action>> _retired = new List<Tuple<int, Action>>();
        private int _frame;
        private Texture _displayFallback;
        private UiTexture _fallbackUi;
        internal MapGeometryData CachedData => _data;
        public UiTexture Texture { get; private set; }
        public MapGeometryRenderer(string sceneName) { _sceneName = sceneName; Active = this; }

        public void Update(IMiniMap map, bool visible)
        {
            ++_frame; DrainRetired(false);
            var config = BattleMiniMapConfig.Get();
            if (_failed || !visible || !config.EnableMapGeometry || !map.IsValid) { _view?.SetEnable(false); return; }
            try
            {
                if (!_attempted) Load();
                if (_data == null) return;
                var ceiling = Ceiling(config);
                var proxy = config.UseMapGeometryProxy && !_proxyNativeFailed;
                var width = Math.Max(1, Math.Min(2048, map.BitmapWidth)); var height = Math.Max(1, Math.Min(2048, map.BitmapHeight));
                if (Texture == null || _min != map.MapBoundMin || _max != map.MapBoundMax || _width != width || _height != height ||
                    _lastProxy != proxy || _lastCeiling != ceiling || _resolution != map.Resolution)
                {
                    ReleaseNative();
                    _min = map.MapBoundMin; _max = map.MapBoundMax; _width = width; _height = height;
                    _lastProxy = proxy; _lastCeiling = ceiling; _resolution = map.Resolution;
                    BuildDisplayFallback(map, proxy, ceiling);
                }
                _view?.SetEnable(true);
            }
            catch (Exception ex)
            {
                ReleaseNative();
                if (_data != null && config.UseMapGeometryProxy && !_proxyNativeFailed)
                {
                    _proxyNativeFailed = true; _lastProxy = false;
                    try
                    {
                        BuildDisplayFallback(map, false, Ceiling(config));
                        _view?.SetEnable(true);
                        TaleWorlds.Library.Debug.Print("BattleMiniMap geometry: original mesh fallback: " + ex.Message);
                        return;
                    }
                    catch (Exception fallback) { ReleaseNative(); ex = fallback; }
                }
                _failed = true;
                TaleWorlds.Library.Debug.Print("BattleMiniMap geometry disabled: " + ex.Message);
            }
        }
        private static float? Ceiling(BattleMiniMapConfig config) =>
            config.CutMapGeometryByHeight && !float.IsNaN(config.MapGeometryCeiling) && !float.IsInfinity(config.MapGeometryCeiling)
                ? (float?)Math.Max(-50, Math.Min(1000, config.MapGeometryCeiling)) : null;

        private void Load()
        {
            _attempted = true;
            if (_sceneName != MapGeometryData.SceneName) return;
            var root = BasePath.Name;
            var cache = Path.Combine(root, "Modules", BattleMiniMapSubModule.ModuleId, "ModuleData", "MapGeometry", "empire_town_g.xml");
            var scene = Path.Combine(root, "Modules", "SandBoxCore", "SceneObj", "empire_town_g", "scene.xscene");
            if (!File.Exists(cache) || !File.Exists(scene)) return;
            if (MapGeometryData.HashFile(scene) != MapGeometryData.SceneHash || MapGeometryData.HashFile(cache) != MapGeometryManifest.CacheSha256)
                throw new InvalidDataException("Scene/cache fingerprint mismatch.");
            _data = MapGeometryData.Load(cache);
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry loaded: scene=" + _sceneName + " instances=" + _data.Layers.Count + " proxy=218 original=3242");
            if (!_data.HasCompleteProxy) TaleWorlds.Library.Debug.Print("BattleMiniMap geometry: using original mesh: " + _data.ProxyFallbackReason);
        }
        private void BuildDisplayFallback(IMiniMap map, bool proxy, float? ceiling)
        {
            var started = System.Diagnostics.Stopwatch.StartNew();
            var raster = MapGeometryRasterizer.Render(_data, proxy, ceiling, map.MapBoundMin.x, map.MapBoundMin.y,
                map.Resolution, map.BitmapWidth, map.BitmapHeight, _width, _height);
            _displayFallback = TaleWorlds.Engine.Texture.CreateFromByteArray(raster.Pixels, raster.Width, raster.Height);
            _fallbackUi = new UiTexture(new EngineTexture(_displayFallback));
            Texture = _fallbackUi;
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry raster: pixels=" + raster.CoveredPixels +
                " triangles=" + raster.Triangles + " target=" + raster.Width + "x" + raster.Height +
                " variant=" + (proxy && _data.HasCompleteProxy ? "proxy" : "original") +
                " ceiling=" + (ceiling.HasValue ? ceiling.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none") +
                " cpu_ms=" + started.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }

        private void Build(IMiniMap map, bool proxy, float? ceiling)
        {
            _scene = Scene.CreateNewScene(initialize_physics: false, enable_decals: false); _scene.SetName("BattleMiniMap.MapGeometry");
            _camera = Camera.CreateCamera();
            var image = new ImageRGBA(1, 1); image.SetRGBA(0, 0, 255, 255, 255, 255);
            var uiWhite = image.CreateTexture(); _white = ((EngineTexture)uiWhite.PlatformTexture).Texture;
            _material = Material.GetDefaultMaterial().CreateCopy(); _material.SetShader(Shader.GetFromResource("simple_shading"));
            _material.SetShaderFlags(0); _material.SetTexture(Material.MBTextureType.DiffuseMap, _white);
            _material.Flags = MaterialFlags.TwoSided | MaterialFlags.DontCastShadow | MaterialFlags.RequiresForwardRendering;
            _material.SetAlphaBlendMode(Material.MBAlphaBlendMode.NoAlphaBlend); _material.SetAlphaTestValue(0);
            var submitted = 0;
            foreach (var layer in _data.Layers)
            {
                var builder = new MeshBuilder(); var faces = 0;
                foreach (var triangle in MapGeometryData.Triangles(_data.Select(layer, proxy), ceiling))
                {
                    var p = triangle.Select(v => MapGeometryData.MapPoint(v, map.MapBoundMin.x, map.MapBoundMin.y)).ToArray();
                    var a = new Vec3(p[0].X, p[0].Y, p[0].Z); var b = new Vec3(p[1].X, p[1].Y, p[1].Z); var c = new Vec3(p[2].X, p[2].Y, p[2].Z);
                    var u = b - a; var v = c - a;
                    var n = new Vec3(u.y * v.z - u.z * v.y, u.z * v.x - u.x * v.z, u.x * v.y - u.y * v.x);
                    var length = n.Length; if (length < 1e-8f) continue; n /= length;
                    var shade = .65f + .3f * Math.Abs(n.z);
                    var color = layer.Resource == "stone_bridge_wide" ? Color(shade, 206, 184, 143) : Color(shade, 130, 166, 183);
                    var ia = builder.AddFaceCorner(a, n, Vec2.Zero, color); var ib = builder.AddFaceCorner(b, n, Vec2.Zero, color);
                    var ic = builder.AddFaceCorner(c, n, Vec2.Zero, color); builder.AddFace(ia, ib, ic); faces++;
                }
                if (faces == 0) continue;
                submitted += faces;
                var mesh = builder.Finalize(); mesh.SetMaterial(_material); mesh.Color = uint.MaxValue;
                var entity = GameEntity.CreateEmpty(_scene); entity.AddMesh(mesh); _entities.Add(entity); _meshes.Add(mesh);
            }
            var highest = _data.Layers.Max(l => l.MaximumZ); var lowest = _data.Layers.Min(l => l.MinimumZ);
            _camera.LookAt(new Vec3(0, 0, highest + 10), new Vec3(0, 0, lowest - 10), Vec3.Forward);
            // Existing map pixels are u=worldY, v=worldX, including the final +1 pixel.
            _camera.SetViewVolume(false, 0, map.BitmapWidth * map.Resolution, -map.BitmapHeight * map.Resolution, 0, .1f, highest - lowest + 30);
            _target = TaleWorlds.Engine.Texture.CreateRenderTarget("BattleMiniMap.MapGeometryTarget", _width, _height, autoMipmaps: false, isTableau: false);
            Texture = new UiTexture(new EngineTexture(_target));
            _view = SceneView.CreateSceneView(); _view.SetScene(_scene); _view.SetCamera(_camera); _view.SetRenderTarget(_target);
            _view.SetAutoDepthTargetCreation(true); _view.SetClearColor(0);
            _view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearColor, true); _view.SetRenderOption(TaleWorlds.Engine.View.ViewRenderOptions.ClearDepth, true);
            _view.SetRenderWithPostfx(false); _view.SetSceneUsesSkybox(false); _view.SetSceneUsesShadows(false); _view.SetSceneUsesContour(false);
            _view.SetAcceptGlobalDebugRenderObjects(false); _view.SetRenderOrder(-2);
            var first = _data.Layers[0].Original.Vertices[0];
            var mapped = MapGeometryData.MapPoint(first, map.MapBoundMin.x, map.MapBoundMin.y);
            var screen = _view.WorldPointToScreenPoint(new Vec3(mapped.X, mapped.Y, mapped.Z));
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry projection: world=" + first.X + "," + first.Y + "," + first.Z +
                " map=" + mapped.X + "," + mapped.Y + " screen=" + screen +
                " bounds=" + map.MapBoundMin + ":" + map.MapBoundMax + " target=" + _width + "x" + _height +
                " depth=true opaque=true");
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry submitted: triangles=" + submitted +
                " variant=" + (proxy && _data.HasCompleteProxy ? "proxy" : "original") +
                " ceiling=" + (ceiling.HasValue ? ceiling.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none"));
        }
        private static uint Color(float shade, int r, int g, int b) => 0xff000000 | ((uint)(r * shade) << 16) | ((uint)(g * shade) << 8) | (uint)(b * shade);
        private void ReleaseNative(bool defer = true)
        {
            Texture = null;
            var fallback = _displayFallback; _displayFallback = null; _fallbackUi = null;
            if (fallback != null)
            {
                if (defer) _retired.Add(Tuple.Create(_frame + 2, (Action)(() => fallback.Release())));
                else fallback.Release();
            }
            var view = _view; var scene = _scene; var material = _material; var white = _white;
            var target = _target; var camera = _camera; var meshes = _meshes.ToArray();
            _view = null; _scene = null; _material = null; _white = null; _target = null; _camera = null;
            _meshes.Clear(); _entities.Clear();
            if (view != null) { view.SetEnable(false); view.AddClearTask(clearOnlySceneview: true); }
            Action release = () =>
            {
                if (scene != null) { scene.ClearAll(); scene.ManualInvalidate(); }
                foreach (var mesh in meshes) mesh.ManualInvalidate();
                material?.ManualInvalidate(); white?.Release(); target?.Release(); camera?.ReleaseCamera();
                GC.KeepAlive(view);
            };
            // UI and native render commands can still reference the previous target.
            if (defer && scene != null) _retired.Add(Tuple.Create(_frame + 2, release)); else release();
        }
        private void DrainRetired(bool all)
        {
            for (var i = _retired.Count - 1; i >= 0; --i)
                if (all || _retired[i].Item1 <= _frame) { var release = _retired[i].Item2; _retired.RemoveAt(i); release(); }
        }
        public void Dispose() { if (Active == this) Active = null; ReleaseNative(false); DrainRetired(true); _data = null; }
    }
}

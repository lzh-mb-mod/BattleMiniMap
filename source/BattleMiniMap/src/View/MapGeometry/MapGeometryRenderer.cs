using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using BattleMiniMap.Config;
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
        private bool _attempted, _failed, _lastProxy, _gpuFailed;
        private float? _lastCeiling;
        private Vec2 _min, _max;
        private int _width, _height;
        private float _resolution;
        private Scene _scene;
        private Camera _camera;
        private MapGeometryTableau _gpu;
        private readonly MapGeometryBackendState _gpuState = new MapGeometryBackendState();
        private string _selectedBackend;
        private bool _lastTint;
        private int _profileGeneration;
        private readonly MapGeometryPerformance _performance = new MapGeometryPerformance();
        private bool _performanceFailed, _hasProfileContext;
        private MapGeometryPerformanceContext _profileContext;
        private string _profileLabel;
        private static readonly Func<BattleMiniMapConfig, bool> MarkerMeshRequested =
            MapGeometryOptionalFlag<BattleMiniMapConfig>.Resolve("UseMeshMarkerBatching");
        private static readonly Func<BattleMiniMapConfig, bool> MarkerProfileRequested =
            MapGeometryOptionalFlag<BattleMiniMapConfig>.Resolve("ProfileMarkerRendering");
        private Material _material;
        private Texture _white;
        private readonly List<GameEntity> _entities = new List<GameEntity>();
        private readonly List<Mesh> _meshes = new List<Mesh>();
        private readonly List<Tuple<int, Action>> _retired = new List<Tuple<int, Action>>();
        private int _frame;
        private Texture _displayFallback;
        private UiTexture _fallbackUi;
        internal MapGeometryData CachedData => _data;
        public UiTexture Texture { get; private set; }
        public MapGeometryRenderer(string sceneName) { _sceneName = sceneName; Active = this; }

        public void Update(IMiniMap map, bool visible, float dt = 0, long tickEntryTimestamp = 0)
        {
            var config = BattleMiniMapConfig.Get();
            var started = config.ProfileMapGeometry ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            try { UpdateCore(map, visible); }
            finally
            {
                try { RecordPerformance(config, visible, dt, started, tickEntryTimestamp); }
                catch (Exception ex)
                {
                    _performanceFailed = true; _performance.Reset();
                    try { TaleWorlds.Library.Debug.Print("BattleMiniMap map profiling disabled: " + ex.Message); }
                    catch { }
                }
            }
        }
        private void RecordPerformance(BattleMiniMapConfig config, bool visible, float dt, long started, long tickEntryTimestamp)
        {
            if (tickEntryTimestamp <= 0 || _performanceFailed || !config.ProfileMapGeometry || !visible || _failed || _data == null || !config.EnableMapGeometry ||
                _selectedBackend == null || (_selectedBackend == "CPU" && config.UseGpuMapGeometry && !_gpuFailed))
                _performance.Reset();
            else
            {
                var end = System.Diagnostics.Stopwatch.GetTimestamp();
                var context = new MapGeometryPerformanceContext {
                    Gpu = _selectedBackend == "GPU", Proxy = _lastProxy && _data.HasCompleteProxy,
                    Tint = _selectedBackend == "GPU" && _lastTint, Generation = _profileGeneration,
                    Cached = _selectedBackend == "GPU" && _gpu != null && _gpu.HasSubmittedCache,
                    Follow = config.FollowMode, Zoom = config.FollowModeScale, Dynamic = config.EnableDynamicScale,
                    Width = config.WidgetWidth, Diagnostic = config.MapGpuDiagnosticMode,
                    DiagnosticDepth = config.MapGpuDiagnosticMode != 0 && config.MapGpuDiagnosticDepth,
                    MarkersRequested = MarkerMeshRequested(config), MarkerProfile = MarkerProfileRequested(config) };
                if (!_hasProfileContext || !context.Equals(_profileContext))
                {
                    _profileContext = context; _hasProfileContext = true;
                    _profileLabel = context.ToString() + " ceiling=" + (_lastCeiling?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none");
                }
                var row = _performance.Record(_profileLabel, tickEntryTimestamp / (double)System.Diagnostics.Stopwatch.Frequency,
                    (end - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency,
                    dt <= 0 || TaleWorlds.MountAndBlade.MBCommon.IsPaused || TaleWorlds.MountAndBlade.MissionState.Current?.Paused == true,
                    context.Gpu ? "GPU" : "CPU");
                if (row != null) TaleWorlds.Library.Debug.Print("BattleMiniMap map profile: " + row +
                    " paints=" + (_gpu?.PaintCount ?? 0) + " callbacks=" + (_gpu?.CallbackCount ?? 0));
            }
        }
        private void UpdateCore(IMiniMap map, bool visible)
        {
            ++_frame; DrainRetired(false);
            var config = BattleMiniMapConfig.Get();
            // Apply manual CPU selection even while the map is hidden.
            if (!config.UseGpuMapGeometry)
            {
                Texture = _fallbackUi; _gpuFailed = false;
                ReleaseGpu(); SelectBackend(false, "manual CPU");
            }
            if (_failed || !visible || !config.EnableMapGeometry || !map.IsValid)
            {
                try { _gpu?.SetEnabled(false); }
                catch (Exception ex) { FailGpu(ex); }
                return;
            }
            try
            {
                if (!_attempted) Load();
                if (_data == null) return;
                var ceiling = Ceiling(config);
                var proxy = config.UseMapGeometryProxy;
                var width = Math.Max(1, Math.Min(2048, map.BitmapWidth));
                var height = Math.Max(1, Math.Min(2048, map.BitmapHeight));
                if (_fallbackUi == null || _min != map.MapBoundMin || _max != map.MapBoundMax ||
                    _width != width || _height != height || _lastProxy != proxy || _lastCeiling != ceiling || _resolution != map.Resolution)
                {
                    ReleaseNative();
                    _min = map.MapBoundMin; _max = map.MapBoundMax; _width = width; _height = height;
                    _lastProxy = proxy; _lastCeiling = ceiling; _resolution = map.Resolution;
                    BuildDisplayFallback(map, proxy, ceiling);
                }
                Texture = _fallbackUi;
                if (!config.UseGpuMapGeometry)
                {
                    SelectBackend(false, "manual CPU");
                    return;
                }
                if (_gpuFailed) { SelectBackend(false, "GPU failure latched; CPU retained"); return; }
                try
                {
                    if (_gpu != null && _lastTint != config.TintGpuMapGeometry) ReleaseGpu();
                    if (_gpu == null) { _lastTint = config.TintGpuMapGeometry; Build(map, proxy, ceiling); _gpuState.Reset(); }
                    _gpu.SetEnabled(true);
                    var valid = _gpu.Texture != null && _gpu.Texture.IsValid;
                    var paints = _gpu.PaintCount;
                    var cached = paints >= MapGeometryTableau.RequiredPaints;
                    var useGpu = _gpuState.Update(paints, valid, _gpu.Failure, cached);
                    if (_gpuState.Failure != null) throw new InvalidOperationException(_gpuState.Failure);
                    if (useGpu) Texture = _gpu.Texture;
                    SelectBackend(useGpu, useGpu ? "cached Tableau submissions" : "GPU warmup");
                }
                catch (Exception ex)
                {
                    FailGpu(ex);
                }
            }
            catch (Exception ex)
            {
                ReleaseNative(); _failed = true;
                TaleWorlds.Library.Debug.Print("BattleMiniMap geometry disabled: " + ex.Message);
            }
        }
        private void FailGpu(Exception error)
        {
            Texture = _fallbackUi; _gpuFailed = true;
            ReleaseGpu();
            SelectBackend(false, "GPU fallback: " + error.Message, true);
        }
        private void SelectBackend(bool gpu, string reason, bool force = false)
        {
            var backend = gpu ? "GPU" : "CPU";
            if (!force && _selectedBackend == backend) return;
            _selectedBackend = backend;
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry backend: " + backend + " reason=" + reason +
                " target=" + (_gpu?.Name ?? "none") + " paints=" + (_gpu?.PaintCount ?? 0));
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
            if (_displayFallback == null || !_displayFallback.IsValid)
                throw new InvalidOperationException("CPU geometry texture creation failed");
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
            var buildTimer = System.Diagnostics.Stopwatch.StartNew();
            ++_profileGeneration;
            _scene = Scene.CreateNewScene(initialize_physics: false, enable_decals: false);
            _scene.SetName("BattleMiniMap.MapGeometry"); _scene.DisableStaticShadows(true); _scene.EnsurePostfxSystem();
            _camera = Camera.CreateCamera();
            _white = TaleWorlds.Engine.Texture.CreateFromByteArray(new byte[] { 255, 255, 255, 255 }, 1, 1);
            if (_white == null || !_white.IsValid) throw new InvalidOperationException("GPU white texture creation failed");
            _material = Material.GetDefaultMaterial().CreateCopy(); MapGeometryShader.Bind(_material);
            _material.SetShaderFlags(0); _material.SetTexture(Material.MBTextureType.DiffuseMap, _white);
            _material.Flags = MaterialFlags.TwoSided | MaterialFlags.DontCastShadow | MaterialFlags.RequiresForwardRendering;
            _material.SetAlphaBlendMode(Material.MBAlphaBlendMode.NoAlphaBlend); _material.SetAlphaTestValue(0);
            _scene.SetShadow(false);
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
                    var main = layer.Resource == "stone_bridge_wide";
                    var color = _lastTint ? (main ? Color(0, 235, 255) : Color(255, 70, 230)) :
                        (main ? Color(206, 184, 143) : Color(130, 166, 183));
                    var ia = builder.AddFaceCorner(a, n, Vec2.Zero, color); var ib = builder.AddFaceCorner(b, n, Vec2.Zero, color);
                    var ic = builder.AddFaceCorner(c, n, Vec2.Zero, color); builder.AddFace(ia, ib, ic); faces++;
                }
                if (faces == 0) continue;
                submitted += faces;
                var mesh = builder.Finalize() ?? throw new InvalidOperationException("GPU geometry mesh creation failed");
                _meshes.Add(mesh);
                mesh.UpdateBoundingBox(); mesh.SetMaterial(_material); mesh.Color = uint.MaxValue;
                var entity = GameEntity.CreateEmpty(_scene, createPhysics: false, callScriptCallbacks: false);
                _entities.Add(entity); entity.AddMesh(mesh, recomputeBoundingBox: true);
                entity.SetVisibilityExcludeParents(true); entity.SetReadyToRender(true);
            }
            var highest = _data.Layers.Max(l => l.MaximumZ); var lowest = _data.Layers.Min(l => l.MinimumZ);
            var projection = new MapGeometryProjection(_min.x, _min.y, map.Resolution, map.BitmapWidth, map.BitmapHeight, lowest, highest);
            var frame = MatrixFrame.Identity; frame.origin.z = projection.CameraZ; _camera.Frame = frame;
            _camera.LookAt(frame.origin, new Vec3(0, 0, lowest - 10), Vec3.Forward);
            _camera.SetViewVolume(false, 0, projection.Right, projection.Bottom, 0, MapGeometryProjection.Near, projection.Far);
            _gpu = new MapGeometryTableau(_scene, _camera);
            _gpu.Initialize(_width, _height);
            var first = _data.Layers[0].Original.Vertices[0];
            var mapped = MapGeometryData.MapPoint(first, _min.x, _min.y);
            var point = new Vec3(mapped.X, mapped.Y, mapped.Z);
            var viewport = _camera.WorldPointToViewPortPoint(ref point);
            var expected = projection.TextureUv(first);
            TaleWorlds.Library.Debug.Print("BattleMiniMap main geometry projection: viewport=" + viewport +
                " expectedTextureUV=" + expected.X + "," + expected.Y + " bounds=" + _min + ":" + _max +
                " target=" + _width + "x" + _height + " depth=true twoSided=true clearAlpha=0");
            TaleWorlds.Library.Debug.Print("BattleMiniMap geometry submitted: triangles=" + submitted +
                " variant=" + (proxy && _data.HasCompleteProxy ? "proxy" : "original") +
                " ceiling=" + (ceiling.HasValue ? ceiling.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "none") +
                " tint=" + _lastTint + " build_cpu_ms=" + buildTimer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
        }
        private static uint Color(int r, int g, int b) => new TaleWorlds.Library.Color(r / 255f, g / 255f, b / 255f, 1).ToUnsignedInteger();
        private void ReleaseNative(bool defer = true)
        {
            ++_profileGeneration;
            _performance.Reset();
            Texture = null;
            var fallback = _displayFallback; _displayFallback = null; _fallbackUi = null;
            if (fallback != null)
            {
                if (defer) _retired.Add(Tuple.Create(_frame + 2, (Action)(() => fallback.Release())));
                else MapGeometryCleanup.Run(ReportCleanupError, () => fallback.Release());
            }
            ReleaseGpu(defer);
            _selectedBackend = null;
        }
        private void ReleaseGpu(bool defer = true)
        {
            if (_gpu == null && _scene == null && _camera == null && _material == null && _white == null && _meshes.Count == 0) return;
            var gpu = _gpu; var scene = _scene; var material = _material; var white = _white;
            var camera = _camera; var meshes = _meshes.ToArray(); var entities = _entities.ToArray();
            Texture = _fallbackUi;
            _gpu = null; _scene = null; _material = null; _white = null; _camera = null;
            _meshes.Clear(); _entities.Clear();
            Action release = () =>
            {
                MapGeometryCleanup.Run(ReportCleanupError, () => scene?.ClearAll(), () => scene?.ManualInvalidate());
                foreach (var mesh in meshes) MapGeometryCleanup.Run(ReportCleanupError, () => mesh?.ManualInvalidate());
                MapGeometryCleanup.Run(ReportCleanupError, () => material?.ManualInvalidate(), () => white?.Release(),
                    () => gpu?.Release(), () => camera?.ReleaseCamera());
                GC.KeepAlive(entities);
            };
            try { gpu?.Retire(); }
            catch (Exception ex) { ReportCleanupError(ex); }
            finally
            {
                if (defer && scene != null) _retired.Add(Tuple.Create(_frame + 2, release));
                else MapGeometryCleanup.Run(ReportCleanupError, release);
            }
        }
        private void DrainRetired(bool all)
        {
            for (var i = _retired.Count - 1; i >= 0; --i)
                if (all || _retired[i].Item1 <= _frame)
                {
                    var release = _retired[i].Item2; _retired.RemoveAt(i);
                    MapGeometryCleanup.Run(ReportCleanupError, release);
                }
        }
        private static void ReportCleanupError(Exception error)
        {
            try { TaleWorlds.Library.Debug.Print("BattleMiniMap geometry cleanup: " + error.Message); }
            catch { } // Logging during shutdown must not interrupt remaining releases.
        }
        public void Dispose()
        {
            if (Active == this) Active = null;
            try { ReleaseNative(false); }
            finally { DrainRetired(true); _data = null; }
        }
    }
}

using System;
using System.Collections.Generic;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using UiTexture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapGeometry
{
    // Owns every display resource; the mission scene is a borrowed read-only source.
    internal sealed class MapRuntimeMainMap : IDisposable
    {
        private readonly Scene _source;
        private Scene _scene;
        private Camera _camera;
        private Material _material;
        private Texture _white;
        private MapLiveSceneGeometry _collector;
        private MapLiveGeometryRules _rules;
        private MapGeometryTableau _tableau;
        private readonly MapGeometryBackendState _backend = new MapGeometryBackendState();
        private readonly List<Tuple<int, Action>> _retired = new List<Tuple<int, Action>>();
        private int _tick, _statusTick, _publication;
        private bool _disposed, _failed, _ready, _enabled, _gpu, _collision, _raw, _simplify;
        private bool _configured, _tint;
        private float _minX, _minY, _maxX, _maxY, _resolution, _lowest, _highest;
        private int _width, _height;
        private float? _ceiling;
        // Design default, not calibrated. GPU pixel density is independent of the
        // CPU bitmap sampling domain used by the projection and marker UVs.
        private const int TextureLongestEdge = 2048;
        internal static void GetTextureSize(int width, int height, out int textureWidth, out int textureHeight)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            var scale = TextureLongestEdge / (double)Math.Max(width, height);
            textureWidth = Math.Max(1, Math.Min(TextureLongestEdge, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero)));
            textureHeight = Math.Max(1, Math.Min(TextureLongestEdge, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero)));
        }
        public UiTexture Texture { get; private set; }
        public MapRuntimeMainMap(Scene source) { _source = source; }
        private string Source => _collision ? "runtime-collision" : "runtime-render";
        private void Report(string message) => Debug.Print("BattleMiniMap runtime main " + message +
            " target=" + (_tableau?.Name ?? "none") + " source=" + Source);

        public void Update(IMiniMap map, bool visible, float dt)
        {
            if (_disposed) return;
            AdvanceRetired();
            var c = BattleMiniMapConfig.Get();
            var ceiling = MapGeometryHeight.Ceiling(c.CutMapGeometryByHeight, c.MapGeometryCeiling);
            var changed = !_configured || _enabled != c.EnableMapGeometry || _gpu != c.UseGpuMapGeometry ||
                _tint != c.TintGpuMapGeometry || _collision != c.UseCollisionScenePreview || _raw != c.UseRawRuntimeTerrain || _simplify != c.SimplifyRuntimeTerrain ||
                !_minX.Equals(map.MapBoundMin.x) || !_minY.Equals(map.MapBoundMin.y) || !_maxX.Equals(map.MapBoundMax.x) || !_maxY.Equals(map.MapBoundMax.y) ||
                _width != map.BitmapWidth || _height != map.BitmapHeight || !_resolution.Equals(map.Resolution);
            if (changed)
            {
                Clear(true); _failed = false; _configured = true;
                _tint = c.TintGpuMapGeometry; _enabled = c.EnableMapGeometry; _gpu = c.UseGpuMapGeometry;
                _collision = c.UseCollisionScenePreview; _raw = c.UseRawRuntimeTerrain; _simplify = c.SimplifyRuntimeTerrain;
                _minX = map.MapBoundMin.x; _minY = map.MapBoundMin.y; _maxX = map.MapBoundMax.x; _maxY = map.MapBoundMax.y;
                _width = map.BitmapWidth; _height = map.BitmapHeight; _resolution = map.Resolution;
            }
            // A deliberate clipping change may retry a failed owner, but ordinary
            // visible updates and hiding/reopening do not reset a failure latch.
            if (_ceiling != ceiling && _failed) { Clear(true); _failed = false; }
            if (!_enabled || !_gpu)
            {
                if (changed) Report("fallback reason=" + (!_enabled ? "geometry-disabled" : "manual-CPU") + " base=GDI-terrain+markers staticFiles=False");
                Texture = null; return;
            }
            if (!visible || !map.IsValid)
            {
                Texture = null;
                try { _tableau?.SetEnabled(false); } catch (Exception ex) { Fail(ex); }
                return; // No collector progress or visible timeout age while hidden.
            }
            if (_failed) return;
            try
            {
                if (_tableau == null) { _ceiling = ceiling; Build(c); }
                if (_ceiling != ceiling)
                {
                    _ceiling = ceiling; ApplyProjection(); _publication = _tableau.InvalidateContent();
                    Texture = null;
                }
                var active = _collector.IsActive;
                var version = _collector.ContentVersion;
                _collector.Update(dt);
                if (_collector.Failure != null) throw new InvalidOperationException("collector: " + _collector.Failure);
                _tableau.SetContentActive(active || _collector.IsActive || version != _collector.ContentVersion);
                _tableau.SetEnabled(true);
                var ready = _backend.Update(_tableau.PaintCount, _tableau.Texture != null && _tableau.Texture.IsValid,
                    _tableau.Failure, _tableau.HasSubmittedCache);
                if (_backend.Failure != null) throw new InvalidOperationException(_backend.Failure);
                ready = ready && _tableau.IsContentSubmitted(_publication);
                Texture = ready ? _tableau.Texture : null;
                if (ready && !_ready)
                    Report("ready paints=" + _tableau.PaintCount + " evidence=Tableau-submissions coverage=unverified");
                _ready = ready;
                if (++_statusTick % 120 == 0)
                    Report("status active=" + _collector.IsActive + " initialFinished=" + _collector.InitialFinished +
                        " partial=" + _collector.Partial + " meshes=" + _collector.DisplayMeshes +
                        " terrainPercent=" + _collector.TerrainPercent + " terrainUnavailable=" + _collector.Coverage.TerrainUnavailable +
                        " paints=" + _tableau.PaintCount + " cached=" + _tableau.HasSubmittedCache + " coverage=unverified");
            }
            catch (Exception ex) { Fail(ex); }
        }
        private void Build(BattleMiniMapConfig c)
        {
            if (_source == null) throw new InvalidOperationException("Mission scene unavailable");
            _rules = new MapLiveGeometryRules(_minX, _minY, _maxX, _maxY);
            _source.GetBoundingBox(out var min, out var max);
            var hasTerrainRange = _source.GetTerrainMinMaxHeight(out var terrainMin, out var terrainMax);
            var sceneRange = MapLiveGeometryRules.Finite(min.x) && MapLiveGeometryRules.Finite(min.y) && MapLiveGeometryRules.Finite(min.z) &&
                MapLiveGeometryRules.Finite(max.x) && MapLiveGeometryRules.Finite(max.y) && MapLiveGeometryRules.Finite(max.z) &&
                min.x <= max.x && min.y <= max.y && min.z <= max.z;
            var terrainRange = hasTerrainRange && MapLiveGeometryRules.Finite(terrainMin) && MapLiveGeometryRules.Finite(terrainMax) && terrainMin <= terrainMax;
            // Without a valid scene range, building heights cannot be claimed covered.
            if (!sceneRange) throw new InvalidOperationException("Invalid public scene height bounds");
            _lowest = terrainRange ? Math.Min(min.z, terrainMin) : min.z;
            _highest = terrainRange ? Math.Max(max.z, terrainMax) : max.z;
            var projection = new MapRuntimeMainProjection(_rules, _minX, _minY, _resolution, _width, _height, _lowest, _highest, _ceiling);
            _scene = Scene.CreateNewScene(initialize_physics: false, enable_decals: false);
            if (_scene == null) throw new InvalidOperationException("No runtime main scene");
            _scene.SetName("BattleMiniMap.RuntimeMain"); _scene.DisableStaticShadows(true); _scene.EnsurePostfxSystem(); _scene.SetShadow(false);
            _camera = Camera.CreateCamera();
            if (_camera == null) throw new InvalidOperationException("No runtime main camera");
            ApplyProjection(projection);
            _white = TaleWorlds.Engine.Texture.CreateFromByteArray(new byte[] { 255, 255, 255, 255 }, 1, 1);
            if (_white == null || !_white.IsValid) throw new InvalidOperationException("No runtime main white texture");
            _material = Material.GetDefaultMaterial().CreateCopy(); MapGeometryShader.Bind(_material);
            _material.SetShaderFlags(0); _material.SetTexture(Material.MBTextureType.DiffuseMap, _white);
            _material.Flags = MaterialFlags.TwoSided | MaterialFlags.DontCastShadow | MaterialFlags.RequiresForwardRendering;
            _material.SetAlphaBlendMode(Material.MBAlphaBlendMode.NoAlphaBlend); _material.SetAlphaTestValue(0);
            // Raw=false means objects-only on the main map. Never sample heights here;
            // the existing GDI base remains available beneath the transparent overlay.
            _collector = new MapLiveSceneGeometry(_source, _scene, _material, _rules,
                hasTerrain: false, collision: _collision, rawTerrain: _raw, simplifyTerrain: _simplify, profile: c.ProfileMapGeometry,
                terrainColor: _tint ? 0xff00ebffu : 0xff8a9184u,
                objectColor: _tint ? 0xffff46e6u : 0xffded4beu,
                renderColor: _tint ? 0xffff46e6u : 0xffded4beu);
            _tableau = new MapGeometryTableau(_scene, _camera);
            GetTextureSize(_width, _height, out var textureWidth, out var textureHeight);
            _tableau.Initialize(textureWidth, textureHeight);
            _backend.Reset();
            _publication = 0;
            Report("build staticFiles=False terrain=" + (_raw ? "raw-physx" : "GDI-base-only") +
                " texture=" + textureWidth + "x" + textureHeight +
                " heights=" + _lowest + ":" + _highest + " ceiling=" + CeilingText +
                " tint=" + _tint + " palette=terrain/object proxy=cache-only base=GDI-terrain+markers");
        }
        private string CeilingText => _ceiling?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        private void ApplyProjection(MapRuntimeMainProjection projection = null)
        {
            var p = projection ?? new MapRuntimeMainProjection(_rules, _minX, _minY, _resolution, _width, _height, _lowest, _highest, _ceiling);
            var eye = new Vec3(p.Eye.X, p.Eye.Y, p.Eye.Z);
            _camera.LookAt(eye, eye - Vec3.Up, Vec3.Forward);
            _camera.SetViewVolume(false, 0, p.Right, p.Bottom, 0, p.Near, p.Far);
            if (_tableau != null) Report("projection ceiling=" + CeilingText + " near=" + p.Near + " far=" + p.Far);
        }
        private void Fail(Exception error)
        {
            _failed = true;
            Report("fallback reason=" + error.Message + " latched=True base=GDI-terrain+markers staticFiles=False");
            Clear(true);
        }
        public void Deactivate() { Clear(true); _configured = false; _failed = false; }
        private void Clear(bool defer)
        {
            Texture = null; _ready = false; _statusTick = 0;
            var tableau = _tableau; var live = _collector; var scene = _scene;
            var camera = _camera; var material = _material; var white = _white;
            _tableau = null; _collector = null; _scene = null; _camera = null; _material = null; _white = null;
            MapGeometryCleanup.Run(CleanupError, () => live?.Stop(), () => tableau?.Retire());
            Action release = () => MapGeometryCleanup.Run(CleanupError,
                () => scene?.ClearAll(), () => scene?.ManualInvalidate(), () => live?.Dispose(),
                () => material?.ManualInvalidate(), () => white?.Release(), () => tableau?.Release(), () => camera?.ReleaseCamera());
            if (defer && (scene != null || tableau != null)) _retired.Add(Tuple.Create(_tick + 2, release));
            else release();
        }
        private static void CleanupError(Exception ex) { try { Debug.Print("BattleMiniMap runtime main cleanup: " + ex.Message); } catch { } }
        public void AdvanceRetired()
        {
            ++_tick;
            for (var i = _retired.Count - 1; i >= 0; --i)
                if (_retired[i].Item1 <= _tick)
                { var release = _retired[i].Item2; _retired.RemoveAt(i); release(); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            Clear(true);
            // Mission ticks end here. Preserve a fresh two application-tick grace
            // for both the current target and resources retired earlier this frame.
            foreach (var item in _retired) MapGeometryRetirementQueue.Enqueue(item.Item2);
            _retired.Clear();
        }
    }
}

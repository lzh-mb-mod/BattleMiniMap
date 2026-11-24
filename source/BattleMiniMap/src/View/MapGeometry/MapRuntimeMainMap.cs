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
        private readonly List<Mesh> _diagnosticMeshes = new List<Mesh>();
        private readonly List<GameEntity> _diagnosticEntities = new List<GameEntity>();
        private int _diagnostic;
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
        internal bool SuppressGdiSelfRender => _diagnostic != 0 &&
            _diagnostic == BattleMiniMapConfig.Get().MapGpuDiagnosticMode && Texture != null;
        public MapRuntimeMainMap(Scene source) { _source = source; }
        private string Source => _diagnostic != 0 ? "main-depth-probe" : (_collision ? "runtime-collision" : "runtime-render");
        private void Report(string message) => Debug.Print("BattleMiniMap runtime main " + message +
            " target=" + (_tableau?.Name ?? "none") + " source=" + Source);

        public void Update(IMiniMap map, bool visible, float dt)
        {
            if (_disposed) return;
            AdvanceRetired();
            var c = BattleMiniMapConfig.Get();
            var diagnostic = c.MapGpuDiagnosticMode == 4 || c.MapGpuDiagnosticMode == 5 ? c.MapGpuDiagnosticMode : 0;
            var ceiling = MapGeometryHeight.Ceiling(c.CutMapGeometryByHeight, c.MapGeometryCeiling);
            var changed = !_configured || _diagnostic != diagnostic || _enabled != c.EnableMapGeometry || _gpu != c.UseGpuMapGeometry ||
                _tint != c.TintGpuMapGeometry || _collision != c.UseCollisionScenePreview || _raw != c.UseRawRuntimeTerrain || _simplify != c.SimplifyRuntimeTerrain ||
                !_minX.Equals(map.MapBoundMin.x) || !_minY.Equals(map.MapBoundMin.y) || !_maxX.Equals(map.MapBoundMax.x) || !_maxY.Equals(map.MapBoundMax.y) ||
                _width != map.BitmapWidth || _height != map.BitmapHeight || !_resolution.Equals(map.Resolution);
            if (changed)
            {
                Clear(true); _failed = false; _configured = true;
                _diagnostic = diagnostic;
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
                    _ceiling = ceiling;
                    if (_diagnostic == 0)
                    {
                        ApplyProjection(); _publication = _tableau.InvalidateContent();
                        Texture = null;
                    }
                }
                var active = _collector?.IsActive ?? false;
                var version = _collector?.ContentVersion ?? 0;
                _collector?.Update(dt);
                if (_collector?.Failure != null) throw new InvalidOperationException("collector: " + _collector.Failure);
                _tableau.SetContentActive(active || (_collector?.IsActive ?? false) || version != (_collector?.ContentVersion ?? 0));
                _tableau.SetEnabled(true);
                var ready = _backend.Update(_tableau.PaintCount, _tableau.Texture != null && _tableau.Texture.IsValid,
                    _tableau.Failure, _tableau.HasSubmittedCache);
                if (_backend.Failure != null) throw new InvalidOperationException(_backend.Failure);
                ready = ready && _tableau.IsContentSubmitted(_publication);
                Texture = ready ? _tableau.Texture : null;
                if (ready && !_ready)
                    Report("ready paints=" + _tableau.PaintCount + " evidence=Tableau-submissions coverage=unverified probe=" + _diagnostic + " depth=requires-visual-validation");
                _ready = ready;
                if (++_statusTick % 120 == 0)
                    Report("status active=" + (_collector?.IsActive ?? false) + " initialFinished=" + _collector?.InitialFinished +
                        " partial=" + _collector?.Partial + " meshes=" + (_collector?.DisplayMeshes ?? _diagnosticMeshes.Count) +
                        " terrainPercent=" + _collector?.TerrainPercent + " terrainUnavailable=" + _collector?.Coverage.TerrainUnavailable +
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
            var projection = new MapRuntimeMainProjection(_rules, _minX, _minY, _resolution, _width, _height, _lowest, _highest, ProjectionCeiling);
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
            _tableau = new MapGeometryTableau(_scene, _camera);
            if (_diagnostic != 0) BuildDepthDiagnostic();
            else _collector = new MapLiveSceneGeometry(_source, _scene, _material, _rules,
                hasTerrain: false, collision: _collision, rawTerrain: _raw, simplifyTerrain: _simplify, profile: c.ProfileMapGeometry,
                terrainColor: _tint ? 0xff00ebffu : 0xff8a9184u,
                objectColor: _tint ? 0xffff46e6u : 0xffded4beu,
                renderColor: _tint ? 0xffff46e6u : 0xffded4beu);
            GetTextureSize(_width, _height, out var textureWidth, out var textureHeight);
            _tableau.Initialize(textureWidth, textureHeight);
            _backend.Reset();
            _publication = 0;
            Report("build staticFiles=False terrain=" + (_diagnostic != 0 ? "controlled-probe" : (_raw ? "raw-physx" : "GDI-base-only")) +
                " texture=" + textureWidth + "x" + textureHeight +
                " heights=" + _lowest + ":" + _highest + " ceiling=" + CeilingText +
                " tint=" + _tint + " palette=" + (_diagnostic != 0 ? "gray/yellow/blue tintIgnored=True" : "terrain/object") +
                " proxy=cache-only base=GDI-terrain+markers" +
                " probe=" + _diagnostic + " ceilingIgnored=" + (_diagnostic != 0));
        }
        private float? ProjectionCeiling => _diagnostic != 0 ? null : _ceiling;
        private void BuildDepthDiagnostic()
        {
            var planes=MapMainDepthDiagnosticGeometry.Create(_minX,_minY,_resolution,_width,_height,_lowest,_highest,_diagnostic==5);
            var order=new List<string>();
            foreach(var plane in planes)
            {
                var builder=new MeshBuilder();var corners=new int[4];
                for(var i=0;i<4;++i)
                {
                    var p=_rules.Normalize(plane.Corners[i]);
                    if(!MapLiveGeometryRules.Finite(p.X)||!MapLiveGeometryRules.Finite(p.Y)||!MapLiveGeometryRules.Finite(p.Z))
                        throw new InvalidOperationException("Nonfinite normalized depth probe");
                    corners[i]=builder.AddFaceCorner(new Vec3(p.X,p.Y,p.Z),Vec3.Up,Vec2.Zero,plane.Color);
                }
                builder.AddFace(corners[0],corners[1],corners[2]);builder.AddFace(corners[0],corners[2],corners[3]);
                var mesh=builder.Finalize()??throw new InvalidOperationException("No main depth probe mesh");
                _diagnosticMeshes.Add(mesh);mesh.UpdateBoundingBox();mesh.SetMaterial(_material);mesh.Color=uint.MaxValue;
                var entity=GameEntity.CreateEmpty(_scene,createPhysics:false,callScriptCallbacks:false);
                if(entity==null)throw new InvalidOperationException("No main depth probe entity");
                _diagnosticEntities.Add(entity);entity.AddMesh(mesh,recomputeBoundingBox:true);
                entity.SetVisibilityExcludeParents(true);entity.SetReadyToRender(true);
                order.Add(plane.Name+"@worldZ="+plane.Corners[0].Z.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            Report("probe mode="+_diagnostic+" order="+string.Join(",",order)+
                " UV=ground[.25,.75;.25,.75],high[.50,.90;.45,.65],low[.10,.50;.35,.55] ceilingIgnored=True");
        }
        private string CeilingText => _ceiling?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none";
        private void ApplyProjection(MapRuntimeMainProjection projection = null)
        {
            var p = projection ?? new MapRuntimeMainProjection(_rules, _minX, _minY, _resolution, _width, _height, _lowest, _highest, ProjectionCeiling);
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
            var meshes=_diagnosticMeshes.ToArray();var entities=_diagnosticEntities.ToArray();
            _diagnosticMeshes.Clear();_diagnosticEntities.Clear();
            _tableau = null; _collector = null; _scene = null; _camera = null; _material = null; _white = null;
            MapGeometryCleanup.Run(CleanupError, () => live?.Stop(), () => tableau?.Retire());
            Action release = () => {
                MapGeometryCleanup.Run(CleanupError,
                () => scene?.ClearAll(), () => scene?.ManualInvalidate(), () => live?.Dispose(),
                () => material?.ManualInvalidate(), () => white?.Release(), () => tableau?.Release(), () => camera?.ReleaseCamera());
                foreach(var mesh in meshes)MapGeometryCleanup.Run(CleanupError,()=>mesh.ManualInvalidate());
                GC.KeepAlive(entities);
            };
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

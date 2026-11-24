using System;
using System.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Engine;
using TaleWorlds.Library;

class MainChecks
{
    static int checks;
    static bool NoCacheRead(MapGeometryRenderer r) => !(bool)typeof(MapGeometryRenderer)
        .GetField("_attempted", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(r);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); ++checks; Console.WriteLine("PASS main: " + message); }
    sealed class Map : IMiniMap
    {
        public bool IsValid { get; set; } = true;
        public int BitmapWidth { get; set; } = 120;
        public int BitmapHeight { get; set; } = 80;
        public float Resolution { get; set; } = 2.5f;
        public Vec2 MapBoundMin { get; set; } = new Vec2(30, -70);
        public Vec2 MapBoundMax { get; set; } = new Vec2(230, 230);
    }
    static void Tick(MapGeometryRenderer renderer, Map map, int n = 12, float dt = .01f)
    {
        for (var i = 0; i < n; ++i) { renderer.Update(map, true, dt); TableauView.Last?.Pump(); }
    }
    static Scene Source(int count = 1)
    {
        var scene = new Scene();
        for (var i = 0; i < count; ++i)
        {
            var e = GameEntity.CreateEmpty(scene, false, false);
            e.Shape = new PhysicsShape { SphereN = 1 };
            var meta = new MetaMesh(); meta.Meshes.Add(new Mesh()); e.Metas.Add(meta);
            e.Frame.origin = new Vec3(40 + i, -50, 10);
        }
        return scene;
    }
    static void Defaults()
    {
        var c = BattleMiniMapConfig.Get();
        c.EnableMapGeometry = c.UseGpuMapGeometry = c.UseRuntimeMainMap = true;
        c.ShowRuntimeTerrain=true;
        c.UseLiveScenePreview = c.ShowGeometryPreview = c.UseCollisionScenePreview = c.UseRawRuntimeTerrain = c.SimplifyRuntimeTerrain = false;
        c.CutMapGeometryByHeight = c.TintGpuMapGeometry = false;
        Texture.FailWhite = MapLiveCollisionGuard.Reject = false;
    }
    static void Main()
    {
        TextureSizeChecks();
        Defaults(); var c = BattleMiniMapConfig.Get(); var map = new Map(); var source = Source();
        var renderer = new MapGeometryRenderer("arbitrary_scene_without_cache", source);
        Tick(renderer, map);
        var target = TableauView.Last; var camera = target.NativeView.Camera; var mainScene = target.NativeView.Scene;
        Check(target.NativeView.Width==2048 && target.NativeView.Height==1365 &&
            Math.Abs(camera.Right-2)<1e-6 && Math.Abs(camera.Bottom+200f/150)<1e-6 && map.BitmapWidth==120 && map.BitmapHeight==80,
            "Production owner allocates 2048x1365 RT while retaining original bitmap world projection and dimensions");
        Check(Debug.Messages.Any(s=>s.Contains("runtime main build") && s.Contains("texture=2048x1365")),
            "Main build log identifies actual GPU texture dimensions");
        Check(renderer.Texture != null && renderer.CachedData == null && NoCacheRead(renderer) && source.Reads > 0 && source.HeightReads == 0,
            "Actual renderer routes arbitrary scene without cache/preview to real owner, collector and Tableau without sampling");
        var rules = new MapLiveGeometryRules(map.MapBoundMin.x, map.MapBoundMin.y, map.MapBoundMax.x, map.MapBoundMax.y);
        foreach (var world in new[] { new GeometryPoint(30, -70, 10), new GeometryPoint(130, 80, 10), new GeometryPoint(230, 230, 10) })
        {
            var p = rules.Normalize(world); var point = new Vec3(p.X, p.Y, p.Z);
            var uv = camera.WorldPointToViewPortPoint(ref point);
            Check(Math.Abs(uv.x - (world.Y + 70) / 300) < 1e-6 && Math.Abs(uv.y - (world.X - 30) / 200) < 1e-6,
                "Normalized camera matches marker UV for nonzero min, nonsquare map and resolution 2.5");
        }
        Check(Math.Abs(camera.Near - .1f / rules.Scale) < 1e-6 && Math.Abs(camera.Far - 130f / rules.Scale) < 1e-6,
            "Public scene/terrain height union uses normalized near/far");
        Check(mainScene.Entities.Single().Added.Color == 0xffded4beu && source.Entities.Single().Metas.Single().Meshes.Single().Color == 0,
            "Main render copy receives neutral object color without changing source");
        Check(mainScene.Entities.Single().Added.Material.GetShader().Name=="notexture",
            "Normal production owner requests notexture through explicit runtime-main binding");
        Check(target.NativeView.Skip && renderer.Texture != null, "Idle main parks only after submitted paints and becomes ready");
        source.Entities[0].Frame.origin.x += 20;
        Tick(renderer, map, 2, 1); // Starts refresh, even if the scan fits in a single tick.
        Check(!target.NativeView.Skip && TableauView.Last == target, "Refresh resumes existing Tableau after idle cache");
        Tick(renderer, map, 8);
        Check(target.NativeView.Skip && Math.Abs(mainScene.Entities.Single().Frame.origin.y - 70f / 150) < 1e-5,
            "Refresh changes real copied frame and returns to idle cache");
        c.FollowMode = true; c.FollowModeScale = .8f; c.EnableDynamicScale = true; Tick(renderer, map);
        Check(TableauView.Last == target, "Follow and zoom settings do not rebuild target or collector");
        var reads = source.Reads; for (var i = 0; i < 250; ++i) renderer.Update(map, false, 1);
        Check(source.Reads == reads && !target.NativeView.Enabled && renderer.Texture == null, "Hidden pauses collection/rendering and visible timeout");
        Tick(renderer, map);
        Check(TableauView.Last == target && renderer.Texture != null, "Showing map resumes same healthy owner");
        c.CutMapGeometryByHeight = true; c.MapGeometryCeiling = -1000;
        renderer.Update(map, true);
        Check(renderer.Texture == null && camera.Near > (90 - (-20)) / rules.Scale && camera.Near < camera.Far,
            "All-clipped camera detaches stale image and requests clear submissions without collecting geometry again");
        Tick(renderer, map, 4);
        Check(TableauView.Last == target && renderer.Texture != null && target.NativeView.Skip, "All-clipped target submits and parks without rebuild");
        c.CutMapGeometryByHeight = false; Tick(renderer, map, 4);
        c.ShowGeometryPreview = c.UseLiveScenePreview = true;
        var preview = new MapGeometryPreviewRenderer(source, 30, -70, 230, 230); preview.Update(renderer);
        var previewTarget = TableauView.Last;
        Check(previewTarget != target && previewTarget.NativeView.Scene != mainScene, "Main and preview own distinct target scenes");
        previewTarget.Pump(); c.ShowGeometryPreview = false; preview.Update(renderer);
        renderer.Update(map, true);
        Check(!target.IsReleased && target.NativeView.Enabled && renderer.Texture != null, "Closing preview does not retire main target");
        c.ShowGeometryPreview = true; preview.Update(renderer); previewTarget = TableauView.Last;
        c.TintGpuMapGeometry = true; renderer.Update(map, true); var tinted = TableauView.Last;
        Check(tinted != target && !target.NativeView.Enabled && !target.IsReleased && !previewTarget.IsReleased,
            "Palette change retires main immediately with deferred release, retaining preview");
        var gets = target.ViewGets; target.Paint(); Check(gets == target.ViewGets, "Retired main callback rejects native access before delayed release");
        Tick(renderer, map);
        Check(target.IsReleased && tinted.NativeView.Scene.Entities.Single().Added.Color == 0xffff46e6u, "Tint applies validation color to main render copy");
        MeshBuilder.RecordColors = true; MeshBuilder.UploadedColors.Clear();
        c.UseCollisionScenePreview = true; renderer.Update(map, true); var collisionTarget = TableauView.Last; Tick(renderer, map, 30);
        Check(!tinted.NativeView.Enabled && collisionTarget != tinted && MeshBuilder.UploadedColors.All(color => color == 0xffff46e6u) && MeshBuilder.UploadedColors.Count > 0,
            "Collision mesh and primitive uploads use main object palette on source switch");
        c.TintGpuMapGeometry = false; MeshBuilder.UploadedColors.Clear(); renderer.Update(map, true); Tick(renderer, map, 30);
        Check(MeshBuilder.UploadedColors.All(color => color == 0xffded4beu) && MeshBuilder.UploadedColors.Count > 0, "Neutral collision objects differ from terrain category");
        MeshBuilder.RecordColors = false;
        var beforeRaw = TableauView.Last; c.UseRawRuntimeTerrain = true; renderer.Update(map, false);
        Check(!beforeRaw.NativeView.Enabled && renderer.Texture == null, "Raw terrain source change retires old main even while hidden");
        c.UseRawRuntimeTerrain = false; c.SimplifyRuntimeTerrain = true; renderer.Update(map, true);
        Check(TableauView.Last != beforeRaw, "Simplification/source selection receives a new independent main lifetime");
        var beforeBounds = TableauView.Last; map.MapBoundMin = new Vec2(20, -80); renderer.Update(map, true);
        Check(TableauView.Last != beforeBounds && !beforeBounds.NativeView.Enabled, "Bounds change replaces main target immediately");
        var resized = TableauView.Last; map.BitmapWidth = 150; map.Resolution = 2; renderer.Update(map, true);
        Check(TableauView.Last != resized && !resized.NativeView.Enabled, "Pixel size and resolution changes rebuild normalized domain");
        var current = TableauView.Last;
        c.UseGpuMapGeometry = false; renderer.Update(map, true);
        Check(renderer.Texture == null && renderer.CachedData == null && NoCacheRead(renderer) && !current.NativeView.Enabled && !previewTarget.IsReleased,
            "Manual CPU removes runtime overlay without bridge cache or releasing preview");
        c.UseGpuMapGeometry = true; Tick(renderer, map); var reopened = TableauView.Last;
        Check(reopened != current && renderer.Texture != null, "Explicit GPU selection retries owner");
        c.CutMapGeometryByHeight = true; c.MapGeometryCeiling = 20; renderer.Update(map, true);
        reopened.NativeView.Throw = true; reopened.Paint(); renderer.Update(map, true);
        Check(renderer.Texture == null && renderer.CachedData == null && NoCacheRead(renderer) && !reopened.NativeView.Enabled, "Callback failure latches GDI-only fallback without static cache");
        var builds = Scene.Created; Tick(renderer, map, 220);
        Check(Scene.Created == builds, "Ordinary updates cannot rebuild failed runtime repeatedly");
        c.EnableMapGeometry = false; renderer.Update(map, false); c.EnableMapGeometry = true; Tick(renderer, map);
        Check(renderer.Texture != null && Scene.Created > builds, "Explicit geometry toggle retries failed owner while preserving CPU base");
        renderer.Dispose(); renderer.Dispose(); builds = Scene.Created; renderer.Update(map, true);
        Check(preview.Available && !previewTarget.IsReleased && Scene.Created == builds, "Main exit is idempotent, does not release preview and cannot reopen");
        preview.Dispose();
        FailureChecks(); ProjectionChecks(); PaletteChecks(); StaticTableau();
        IdleResumeChecks(); CallbackOrderingChecks(); RetirementChecks(); NormalizationChecks();
        DepthDiagnosticChecks();
        TerrainVisibilityChecks();
        Check(Debug.Messages.Any(s => s.Contains("runtime main ready") && s.Contains("target=BattleMiniMap.MainGeometry.") && s.Contains("source=runtime-")) &&
            Debug.Messages.Any(s => s.Contains("runtime main status")) && Debug.Messages.Any(s => s.Contains("runtime main fallback")),
            "Main build/ready/status/fallback evidence carries its own target and source identity");
        Console.WriteLine("PASS: " + checks + " production main renderer/owner/collector/Tableau checks; fake native boundary, no GPU pixel proof.");
    }
    static void FailureChecks()
    {
        Defaults(); var c = BattleMiniMapConfig.Get(); var map = new Map(); var source = Source(200);
        var r = new MapGeometryRenderer("no_cache", source);
        r.Update(map, true); var target = TableauView.Last;
        Tick(r, map, 4);
        Check(r.Texture != null && !target.NativeView.Skip, "Active multi-frame collector becomes ready without resetting total paint progress");
        c.UseCollisionScenePreview = true; r.Update(map, false); var reads = source.Reads;
        for (var i = 0; i < 5; ++i) r.Update(map, false, 1);
        Check(!target.NativeView.Enabled && source.Reads == reads, "Source switch while hidden immediately stops old collector");
        r.Dispose(); var gets = target.ViewGets; target.Paint(); Check(gets == target.ViewGets, "Exit callback cannot reach released main native view");
        Defaults(); source = Source(); source.BoundMin.z = float.NaN;
        r = new MapGeometryRenderer(MapGeometryData.SceneName, source); Tick(r, map, 5); var builds = Scene.Created;
        Check(r.Texture == null && r.CachedData == null && NoCacheRead(r) && source.Reads == 0, "Invalid public height bounds keep GDI without static bridge read even in bridge scene");
        Tick(r, map, 200); Check(builds == Scene.Created, "Invalid bounds failure remains latched"); r.Dispose();
        source.BoundMin.z = -20; r = new MapGeometryRenderer("next_mission", source); Tick(r, map);
        Check(r.Texture != null, "New mission creates a fresh retry lifetime"); r.Dispose();
        Defaults(); r = new MapGeometryRenderer("stall", Source()); r.Update(map, true); target = TableauView.Last;
        for (var i = 0; i < 250; ++i) r.Update(map, false);
        Tick(r, map, 4); Check(r.Texture != null && TableauView.Last == target, "Hidden warmup does not count toward timeout"); r.Dispose();
        r = new MapGeometryRenderer("stall", Source()); for (var i = 0; i < 200; ++i) r.Update(map, true);
        builds = Scene.Created; Tick(r, map, 10);
        Check(r.Texture == null && Scene.Created == builds && r.CachedData == null && NoCacheRead(r), "Missing paint progress times out once without automatic retry or cache fallback"); r.Dispose();
        Defaults(); c.UseRuntimeMainMap = false;
        r = new MapGeometryRenderer("explicit_old_cache", Source()); Tick(r, map, 2);
        Check(r.Texture == null, "Explicit old-cache route retains scene gating");
        c.UseRuntimeMainMap = true; Tick(r, map); Check(r.Texture != null, "Runtime toggle routes same renderer into independent scene owner"); r.Dispose();
    }
    static void PaletteChecks()
    {
        // Exercises production collector upload paths; default preview palette remains covered by its own suite.
        MeshBuilder.RecordColors = true; MeshBuilder.UploadedColors.Clear();
        var s = Source(); var terrain = new MapLiveSceneGeometry(s, new Scene(), new Material(), new MapLiveGeometryRules(0,0,8,8),
            terrainColor: 0xff8a9184u, objectColor: 0xffded4beu, renderColor: 0xffded4beu);
        for (var i = 0; i < 100 && !terrain.InitialFinished; ++i) terrain.Update(.01f);
        Check(MeshBuilder.UploadedColors.Count > 0 && MeshBuilder.UploadedColors.All(color => color == 0xff8a9184u), "Sampled terrain upload uses independent terrain palette when explicitly requested by collector caller");
        terrain.Dispose(); MeshBuilder.UploadedColors.Clear();
        s = new Scene { Height = v => v.x + 10 * v.y };
        var memory = new RawTerrainChecks.Memory(s.Pointer.ToUInt64(), false);
        terrain = new MapLiveSceneGeometry(s, new Scene(), new Material(), new MapLiveGeometryRules(0,0,2,2),
            false, false, true, () => memory, terrainColor: 0xff00ebffu, objectColor: 0xffff46e6u);
        for (var i = 0; i < 100 && !terrain.InitialFinished; ++i) terrain.Update(.01f);
        Check(terrain.InitialFinished && !terrain.Partial && MeshBuilder.UploadedColors.Count > 0 && MeshBuilder.UploadedColors.All(color => color == 0xff00ebffu),
            "Raw terrain uploads distinct validation terrain color without changing validation queries");
        terrain.Dispose(); MeshBuilder.RecordColors = false;
    }
    static void StaticTableau()
    {
        var t = new MapGeometryTableau(new Scene(), Camera.CreateCamera()); t.Initialize(100, 100); var target = TableauView.Last;
        target.Pump(); target.Pump(); t.SetEnabled(true);
        Check(t.PaintCount == 2 && t.HasSubmittedCache && target.NativeView.Skip, "Legacy bridge Tableau still parks after two paints");
        target.Paint(); Check(t.PaintCount == 2, "Legacy extra callback does not repaint frozen bridge");
        var generation = t.InvalidateContent(); t.SetEnabled(true);
        // Callbacks finish before the owner can read counts or publication state.
        target.Paint(); target.Paint();
        Check(t.IsContentSubmitted(generation) && t.HasSubmittedCache && target.NativeView.Skip,
            "Invalidated generation publishes when callbacks complete before owner's next observation");
        generation = t.InvalidateContent(); t.SetContentActive(true); t.SetEnabled(true);
        target.Paint(); target.Paint();
        Check(t.IsContentSubmitted(generation) && !t.HasSubmittedCache,
            "Publication barrier completes during active collection independently of idle cache tail");
        t.SetContentActive(false); target.Paint(); target.Paint();
        Check(t.HasSubmittedCache, "Active generation parks after two idle submissions");
        t.Retire(); var gets = target.ViewGets; target.Paint(); Check(gets == target.ViewGets, "Legacy retired callback remains safe"); t.Release();
    }
    static void ProjectionChecks()
    {
        var tiny = new MapLiveGeometryRules(0,0,.002f,.002f);
        foreach (var make in new Action[] {
            () => new MapRuntimeMainProjection(tiny,0,0,1,10,10,1e38f,1e38f,null),
            () => new MapRuntimeMainProjection(tiny,0,0,1e36f,120,80,0,10,null),
            () => new MapRuntimeMainProjection(tiny,0,0,1,10,10,-1e38f,1e38f,null),
            () => new MapRuntimeMainProjection(tiny,0,0,0,10,10,0,10,null),
            () => new MapRuntimeMainProjection(tiny,0,0,1,10,10,10,0,null)
        })
        {
            var rejected = false; try { make(); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "Projection rejects overflow or unordered output before native camera setters");
        }
        Defaults(); var map = new Map(); var source = Source();
        source.BoundMin.z = 10; source.BoundMax.z = 20; source.TerrainMin = -100; source.TerrainMax = 100; source.TerrainRangeAvailable = false;
        var r = new MapGeometryRenderer("no-terrain-metadata", source); Tick(r, map);
        Check(r.Texture != null && Math.Abs(TableauView.Last.NativeView.Camera.Far - 40f/150) < 1e-6,
            "False terrain range result is ignored while valid scene height bounds remain usable"); r.Dispose();
        source.BoundMin.z = source.BoundMax.z = 1e38f;
        map.MapBoundMin = new Vec2(0,0); map.MapBoundMax = new Vec2(.002f,.002f);
        var builds = Scene.Created; r = new MapGeometryRenderer("overflow", source); Tick(r, map);
        Check(r.Texture == null && Scene.Created == builds && r.CachedData == null && NoCacheRead(r), "Normalized overflow falls back before allocating native target"); r.Dispose();
        Defaults(); Texture.FailWhite = true; r = new MapGeometryRenderer("allocation-failure", Source()); Tick(r, new Map(), 8);
        builds = Scene.Created; Tick(r, new Map());
        Check(r.Texture == null && Scene.Created == builds && r.CachedData == null && NoCacheRead(r), "Partial native allocation failure releases once and remains latched without static read");
        Texture.FailWhite = false; r.Dispose();
        Defaults(); BattleMiniMapConfig.Get().UseCollisionScenePreview = true; MapLiveCollisionGuard.Reject = true;
        r = new MapGeometryRenderer("collector-failure", Source()); Tick(r, new Map()); builds = Scene.Created; Tick(r, new Map());
        Check(r.Texture == null && Scene.Created == builds && r.CachedData == null && NoCacheRead(r), "Collector guard failure latches GDI-only fallback without changing native read gates"); r.Dispose(); MapLiveCollisionGuard.Reject = false;
    }
    static void TextureSizeChecks()
    {
        MapRuntimeMainMap.GetTextureSize(17,17,out var width,out var height);
        Check(width==2048 && height==2048,"Square GPU RT upscales independently of small CPU bitmap");
        MapRuntimeMainMap.GetTextureSize(120,80,out width,out height);
        Check(width==2048 && height==1365,"Landscape RT preserves aspect with rounded short edge");
        MapRuntimeMainMap.GetTextureSize(80,120,out width,out height);
        Check(width==1365 && height==2048,"Portrait RT preserves aspect with rounded short edge");
        MapRuntimeMainMap.GetTextureSize(3,1,out width,out height);
        Check(width==2048 && height==683,"Short edge rounds up rather than truncating");
        MapRuntimeMainMap.GetTextureSize(int.MaxValue,1,out width,out height);
        Check(width==2048 && height==1,"Extreme short edge clamps to one without integer overflow");
    }
    static void DepthDiagnosticChecks()
    {
        Defaults();var c=BattleMiniMapConfig.Get();var map=new Map();var source=Source();
        c.MapGpuDiagnosticMode=4;c.CutMapGeometryByHeight=true;c.MapGeometryCeiling=-50;c.ShowRuntimeTerrain=false;
        c.UseRawRuntimeTerrain=true;c.UseCollisionScenePreview=true;c.TintGpuMapGeometry=true;c.FollowMode=true;
        MapLiveCollisionGuard.Reject=true; // An accidental collector start would fail here.
        var r=new MapGeometryRenderer("arbitrary-depth-probe",source);
        r.Update(map,true);var first=TableauView.Last;var scene=first.NativeView.Scene;var camera=first.NativeView.Camera;
        var runtime=typeof(MapGeometryRenderer).GetField("_runtime",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(r);
        var collectorField=typeof(MapRuntimeMainMap).GetField("_collector",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Check(collectorField.GetValue(runtime)==null && source.Reads==0 && source.HeightReads==0 &&
            source.BoundsReads==1 && source.TerrainRangeReads==1 && NoCacheRead(r),
            "Depth probe production route reads only public bounds, creates no collector and never reads cache/raw/source geometry");
        Check(scene.Entities.Count==3 && scene.Entities.All(e=>e.Visible && e.Added.UploadFaces==2 && e.Added.UploadVertices==4) &&
            first.NativeView.Width==2048 && first.NativeView.Height==1365,
            "Depth probe uses main owner scene/Tableau/2048 RT with exactly three separate opaque quads");
        Check(r.Texture==null && !r.SuppressGdiSelfRender,"Depth target warmup keeps GDI self rendering");
        Tick(r,map,5);
        Check(r.Texture!=null && r.SuppressGdiSelfRender && collectorField.GetValue(runtime)==null,
            "Submitted diagnostic target alone activates managed GDI self-render suppression without collector");
        var tableauField=typeof(MapRuntimeMainMap).GetField("_tableau",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var forwardTableau=(MapGeometryTableau)tableauField.GetValue(runtime);var paints=forwardTableau.PaintCount;
        Tick(r,map,8,0);
        Check(forwardTableau.PaintCount==paints+8 && !first.NativeView.Skip && !forwardTableau.HasSubmittedCache,
            "Visible forward probe keeps painting beyond initial submissions rather than parking");
        paints=forwardTableau.PaintCount;
        for(var i=0;i<8;++i){r.Update(map,false,0);first.Pump();}
        Check(forwardTableau.PaintCount==paints && !first.NativeView.Enabled && first.NativeView.Skip && !r.SuppressGdiSelfRender,
            "Hidden continuous probe stops callbacks and restores GDI self-render state");
        r.Update(map,true,0);first.Pump();
        Check(forwardTableau.PaintCount==paints+1 && first.NativeView.Enabled && !first.NativeView.Skip && r.SuppressGdiSelfRender,
            "Showing continuous probe resumes its existing target without resetting publication");
        Check(c.MapGeometryCeiling==-50 && c.CutMapGeometryByHeight && c.FollowMode && Math.Abs(camera.Near-.1f/150)<1e-6,
            "Depth probe locally ignores clipping while preserving ceiling and follow configuration");
        var rules=new MapLiveGeometryRules(30,-70,230,230);
        var forward=MapMainDepthDiagnosticGeometry.Create(30,-70,2.5f,120,80,-20,80,false);
        var reverse=MapMainDepthDiagnosticGeometry.Create(30,-70,2.5f,120,80,-20,80,true);
        var narrow=MapMainDepthDiagnosticGeometry.Create(30,-70,2.5f,120,80,0,.04f,false);
        Check(narrow[2].Corners[0].Z<narrow[0].Corners[0].Z && narrow[0].Corners[0].Z<narrow[1].Corners[0].Z,
            "Representable narrow height range is accepted without an arbitrary minimum span");
        Check(forward.Select(p=>p.Name).SequenceEqual(reverse.Reverse().Select(p=>p.Name)) &&
            forward.Zip(reverse.Reverse(),(a,b)=>a.Color==b.Color && a.Corners.Zip(b.Corners,(x,y)=>x.X==y.X && x.Y==y.Y && x.Z==y.Z).All(v=>v)).All(v=>v),
            "Forward/reverse helper preserves exact world geometry and colors, changing only creation order");
        var ground=forward[0];var high=forward[1];var low=forward[2];
        Check(ground.Color==0xff808080 && high.Color==0xffffff00 && low.Color==0xff0070ff &&
            rules.Normalize(low.Corners[0]).Z<rules.Normalize(ground.Corners[0]).Z && rules.Normalize(ground.Corners[0]).Z<rules.Normalize(high.Corners[0]).Z,
            "Gray ground, yellow high and blue low have strictly ordered normalized depths");
        Check(low.Corners.Min(p=>p.Y)<ground.Corners.Min(p=>p.Y) && low.Corners.Max(p=>p.Y)>ground.Corners.Min(p=>p.Y) &&
            high.Corners.Max(p=>p.Y)>ground.Corners.Max(p=>p.Y) && high.Corners.Min(p=>p.Y)<ground.Corners.Max(p=>p.Y) &&
            low.Corners.Min(p=>p.X)>=ground.Corners.Min(p=>p.X) && high.Corners.Max(p=>p.X)<=ground.Corners.Max(p=>p.X),
            "Low crosses ground left UV edge and high crosses right edge, both partly over ground");
        var signatures=scene.Entities.Select(e=>e.Added.UploadSignature).ToArray();
        var simpleFlags=scene.Entities[0].Added.Material.Flags;
        Check(scene.Entities[0].Added.Material.GetShader().Name=="simple_shader",
            "Modes 4/5 retain original simple_shader control material");
        c.MapGpuDiagnosticMode=5;
        Check(!r.SuppressGdiSelfRender,"Changing probe order restores GDI immediately before next update");
        r.Update(map,true);var second=TableauView.Last;
        Check(second!=first && !first.NativeView.Enabled && !first.IsReleased && !r.SuppressGdiSelfRender &&
            second.NativeView.Scene.Entities.Select(e=>e.Added.UploadSignature).SequenceEqual(signatures.Reverse()),
            "Real reverse owner uploads identical meshes in reverse order and retires old target before publication");
        Tick(r,map,5);
        var reverseTableau=(MapGeometryTableau)tableauField.GetValue(runtime);paints=reverseTableau.PaintCount;Tick(r,map,8,0);
        Check(reverseTableau.PaintCount==paints+8 && !second.NativeView.Skip && !reverseTableau.HasSubmittedCache,
            "Visible reverse probe also keeps painting instead of using two-paint cache");
        Check(first.IsReleased && r.SuppressGdiSelfRender && source.Reads==0 && source.HeightReads==0 &&
            second.NativeView.Scene.Entities[0].Added.Material.GetShader().Name=="simple_shader" &&
            second.NativeView.Camera.Right==camera.Right && second.NativeView.Camera.Bottom==camera.Bottom && second.NativeView.Camera.Near==camera.Near,
            "Reverse diagnostic retains main projection and delayed retirement without source reads");
        c.MapGeometryCeiling=69.15933f;r.Update(map,true);
        Check(TableauView.Last==second && r.SuppressGdiSelfRender && second.NativeView.Camera.Near==camera.Near,
            "Changing configured ceiling does not clip or invalidate diagnostic publication");
        Tick(r,map,125);
        Check(source.Reads==0 && source.HeightReads==0 && collectorField.GetValue(runtime)==null,
            "Nullable-collector diagnostic remains stable through status updates");
        var lastProbe=second;
        foreach(var mode in new[]{6,7})
        {
            c.MapGpuDiagnosticMode=mode;r.Update(map,true);lastProbe=TableauView.Last;
            var material=lastProbe.NativeView.Scene.Entities[0].Added.Material;
            Check(lastProbe.NativeView.Scene.Entities.Select(e=>e.Added.UploadSignature).SequenceEqual(mode==6?signatures:signatures.Reverse()) &&
                material.GetShader().Name=="notexture" && material.Flags==simpleFlags && material.GetShaderFlags()==0 &&
                material.GetAlphaBlendMode()==Material.MBAlphaBlendMode.NoAlphaBlend &&
                lastProbe.NativeView.Camera.Near==camera.Near && lastProbe.NativeView.Camera.Right==camera.Right && lastProbe.NativeView.Width==2048,
                "Vertex-color mode "+mode+" changes only shader profile while preserving corresponding geometry/order, opaque flags, camera and RT");
            var probe=(MapGeometryTableau)tableauField.GetValue(runtime);paints=probe.PaintCount;Tick(r,map,5,0);
            Check(probe.PaintCount==paints+5 && !lastProbe.NativeView.Skip && r.SuppressGdiSelfRender && collectorField.GetValue(runtime)==null,
                "Vertex-color mode "+mode+" remains continuous without source extraction");
            paints=probe.PaintCount;r.Update(map,false,0);lastProbe.Pump();
            Check(probe.PaintCount==paints && !lastProbe.NativeView.Enabled && !r.SuppressGdiSelfRender,
                "Vertex-color mode "+mode+" pauses when hidden and retains GDI state");
            r.Update(map,true,0);lastProbe.Pump();
            Check(material.ShaderFlagsReads==2 && material.AlphaBlendReads==2 &&
                Debug.Messages.Count(s=>s.Contains("material probe="+mode+" shader=notexture") && s.Contains("target="+probe.Name))==1,
                "Target material flags and alpha are actually read and logged once at initialization, never per tick");
        }
        c.MapGpuDiagnosticMode=0;
        Check(!r.SuppressGdiSelfRender,"Leaving diagnostic restores GDI before route update");
        c.UseRawRuntimeTerrain=false;MapLiveCollisionGuard.Reject=false;r.Update(map,true);var normal=TableauView.Last;Tick(r,map);
        Check(normal!=lastProbe && !lastProbe.NativeView.Enabled && collectorField.GetValue(runtime)!=null && source.Reads>0 &&
            !r.SuppressGdiSelfRender && c.MapGeometryCeiling==69.15933f && normal.NativeView.Skip &&
            ((MapGeometryTableau)tableauField.GetValue(runtime)).HasSubmittedCache && normal.NativeView.Scene.Entities[0].Added.Material.GetShader().Name=="notexture",
            "Diagnostic exit restores demand parking, normal collector and GDI policy/configured ceiling");
        Check(Debug.Messages.Any(s=>s.Contains("source=main-depth-probe policy=continuous")) &&
            Debug.Messages.Any(s=>s.Contains("source=runtime-collision policy=demand")),
            "Main logs explicitly distinguish continuous probe policy from normal demand policy");
        Check(Debug.Messages.Any(s=>s.Contains("probe mode=4 order=ground-gray@worldZ=30,high-yellow@worldZ=42.5,low-blue@worldZ=17.5") && s.Contains("target=BattleMiniMap.MainGeometry.")) &&
            Debug.Messages.Any(s=>s.Contains("probe mode=5 order=low-blue@worldZ=17.5,high-yellow@worldZ=42.5,ground-gray@worldZ=30")),
            "Probe logs identify target, mode, order, world heights and UV ranges for visual acceptance");
        r.Dispose();Defaults();c.MapGpuDiagnosticMode=4;
        r=new MapGeometryRenderer("broken-probe",Source());r.Update(map,true);var broken=TableauView.Last;
        broken.NativeView.Throw=true;broken.Paint();r.Update(map,true);
        Check(r.Texture==null && !r.SuppressGdiSelfRender && NoCacheRead(r),"Failed depth probe retains GDI and never substitutes static cache");
        r.Dispose();Defaults();c.MapGpuDiagnosticMode=0;
    }
    static void IdleResumeChecks()
    {
        Defaults(); var c=BattleMiniMapConfig.Get(); var map=new Map(); var source=Source();
        var r=new MapGeometryRenderer("idle-resume",source); Tick(r,map);
        var target=TableauView.Last;
        for(var i=0;i<220;++i)r.Update(map,true,0);
        Check(target.NativeView.Skip && r.Texture!=null,"Production main stays cached through more than 180 visible idle updates");
        r.Update(map,true,1);r.Update(map,true,1); // Callback deliberately arrives AFTER the requesting Update.
        Check(target.NativeView.Enabled && !target.NativeView.Skip && r.Texture!=null,
            "Long-idle refresh request does not inherit cached stall age before its callback");
        target.Pump();Tick(r,map);
        for(var i=0;i<220;++i)r.Update(map,true,0);
        c.CutMapGeometryByHeight=true;c.MapGeometryCeiling=69.15933f;r.Update(map,true,0);
        Check(target.NativeView.Enabled && !target.NativeView.Skip && r.Texture==null,
            "Long-idle clip requests a new publication without latching CPU before callback");
        target.Pump();Tick(r,map,4);
        Check(r.Texture!=null && TableauView.Last==target && target.NativeView.Skip && NoCacheRead(r),
            "Post-Update callbacks publish clipped main target after long cache idle");r.Dispose();
    }
    static void TerrainVisibilityChecks()
    {
        Defaults();var c=BattleMiniMapConfig.Get();var map=new Map();var source=Source();source.Height=v=>v.x+10*v.y;
        var renderer=new MapGeometryRenderer("terrain-visibility",source);Tick(renderer,map);
        var target=TableauView.Last;var scene=target.NativeView.Scene;var camera=target.NativeView.Camera;
        const System.Reflection.BindingFlags fields=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var owner=typeof(MapGeometryRenderer).GetField("_runtime",fields).GetValue(renderer);
        var collectorField=typeof(MapRuntimeMainMap).GetField("_collector",fields);
        // Inject the existing synthetic raw-memory boundary into a production
        // collector; owner/router/Tableau remain real. No readonly state is changed.
        var initial=(MapLiveSceneGeometry)collectorField.GetValue(owner);initial.Stop();initial.Dispose();scene.ClearAll();
        var memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
        var collector=new MapLiveSceneGeometry(source,scene,new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,()=>memory);
        collectorField.SetValue(owner,collector);Tick(renderer,map,30,0);
        var terrain=(System.Collections.Generic.List<GameEntity>)typeof(MapLiveSceneGeometry).GetField("_terrainEntities",fields).GetValue(collector);
        var objects=scene.Entities.Except(terrain).ToArray();var copied=source.Entities[0].Metas[0].Meshes[0].Copies;
        var reads=source.Reads;var heights=source.HeightReads;var builds=Scene.Created;var triangles=collector.TerrainTriangles;
        var version=collector.ContentVersion;var tableau=(MapGeometryTableau)typeof(MapRuntimeMainMap).GetField("_tableau",fields).GetValue(owner);
        var paints=tableau.PaintCount;
        c.ShowRuntimeTerrain=false;renderer.Update(map,true,0);
        Check(terrain.Count==1 && terrain.All(e=>!e.Visible) && objects.Length==1 && objects.All(e=>e.Visible) &&
            renderer.Texture==null && collector.ContentVersion==version+1 && source.Reads==reads && source.HeightReads==heights,
            "Idle main terrain toggle hides only owned terrain and invalidates publication without source reads");
        Tick(renderer,map,3,0);
        Check(ReferenceEquals(collector,collectorField.GetValue(owner)) && TableauView.Last==target && Scene.Created==builds &&
            camera==target.NativeView.Camera && tableau.PaintCount==paints+2 && tableau.HasSubmittedCache && renderer.Texture!=null &&
            source.Entities[0].Metas[0].Meshes[0].Copies==copied && collector.TerrainTriangles==triangles && !collector.Partial && !renderer.SuppressGdiSelfRender,
            "Terrain visibility republishes idle cache with same collector/scene/camera, unchanged objects and coverage, retaining GDI policy");
        c.ShowRuntimeTerrain=true;renderer.Update(map,true,0);Tick(renderer,map,3,0);
        Check(terrain.All(e=>e.Visible) && objects.All(e=>e.Visible) && source.Reads==reads && source.HeightReads==heights && TableauView.Last==target,
            "Showing terrain restores same entities without re-extraction or object changes");renderer.Dispose();

        var rawSource=new Scene{Height=v=>v.x+10*v.y};var rawTarget=new Scene();
        memory=new RawTerrainChecks.Memory(rawSource.Pointer.ToUInt64(),false,66);
        var raw=new MapLiveSceneGeometry(rawSource,rawTarget,new Material(),new MapLiveGeometryRules(0,0,65,65),false,false,true,()=>memory);
        for(var i=0;i<1000 && rawTarget.Entities.Count==0 && raw.Failure==null;++i)raw.Update(0);
        Check(rawTarget.Entities.Count>0 && !raw.InitialFinished,"Raw visibility fixture reaches a published chunk before extraction ends");
        raw.SetTerrainVisible(false);
        for(var i=0;i<1000&&!raw.InitialFinished&&raw.Failure==null;++i)raw.Update(0);
        Check(raw.InitialFinished && !raw.Partial && rawTarget.Entities.Count==4 && rawTarget.Entities.All(e=>!e.Visible),
            "Already-published and later raw chunks inherit hidden terrain state without changing coverage");raw.Dispose();
        var scalarTarget=new Scene();var scalar=new MapLiveSceneGeometry(new Scene(),scalarTarget,new Material(),new MapLiveGeometryRules(0,0,4,4));
        scalar.SetTerrainVisible(false);for(var i=0;i<100&&!scalar.InitialFinished;++i)scalar.Update(0);
        Check(scalar.InitialFinished && !scalar.Partial && scalarTarget.Entities.Count==1 && !scalarTarget.Entities[0].Visible,
            "New scalar terrain chunk inherits visibility without adding a sampling path");scalar.Dispose();

        Defaults();c.ShowGeometryPreview=c.UseLiveScenePreview=true;source=Source();source.Height=v=>v.x+10*v.y;
        source.TerrainDimensions=new Vec2i{X=2,Y=2};source.TerrainNodeSize=4;
        var preview=new MapGeometryPreviewRenderer(source,0,0,8,8);
        for(var i=0;i<20;++i)preview.Update(null,0);
        target=TableauView.Last;camera=target.NativeView.Camera;var live=preview.Live;
        terrain=(System.Collections.Generic.List<GameEntity>)typeof(MapLiveSceneGeometry).GetField("_terrainEntities",fields).GetValue(live);
        objects=target.NativeView.Scene.Entities.Except(terrain).ToArray();
        preview.Orbit.Orbit(30,10);preview.Update(null,0);var eye=camera.Eye;var distance=preview.Orbit.Distance;
        reads=source.Reads;heights=source.HeightReads;builds=Scene.Created;
        c.ShowRuntimeTerrain=false;preview.Update(null,0);
        Check(preview.Available && preview.Live==live && TableauView.Last==target && Scene.Created==builds && terrain.Count>0 &&
            terrain.All(e=>!e.Visible) && objects.All(e=>e.Visible) && camera.Eye.x==eye.x && camera.Eye.y==eye.y && camera.Eye.z==eye.z && preview.Orbit.Distance==distance &&
            source.Reads==reads && source.HeightReads==heights && objects[0].Added.Material.GetShader().Name=="notexture" && objects[0].Added.Color==0xffded4beu &&
            Debug.Messages.Any(s=>s.Contains("geometry preview terrain visible=False target=BattleMiniMap.GeometryPreview.")),
            "Runtime preview applies terrain visibility immediately with fixed neutral palette/notexture, preserving camera, source and collector");
        c.ShowRuntimeTerrain=true;preview.Update(null,0);
        Check(terrain.All(e=>e.Visible) && preview.Live==live && TableauView.Last==target && source.Reads==reads && source.HeightReads==heights,
            "Preview terrain restoration retains scene and orbit without source reread");preview.Dispose();Defaults();
    }
    static void CallbackOrderingChecks()
    {
        var t=new MapGeometryTableau(new Scene(),Camera.CreateCamera());t.Initialize(16,16);
        var target=TableauView.Last;var view=target.NativeView;
        using(var entered=new System.Threading.ManualResetEventSlim())
        using(var finish=new System.Threading.ManualResetEventSlim())
        using(var hideStarted=new System.Threading.ManualResetEventSlim())
        using(var hideDone=new System.Threading.ManualResetEventSlim())
        {
            view.BeforeSetScene=()=>{entered.Set();if(!finish.Wait(5000))throw new Exception("controlled callback timeout");};
            var paint=System.Threading.Tasks.Task.Run(()=>target.Paint());
            Check(entered.Wait(5000),"Controlled callback enters native Configure before hide");
            var hide=System.Threading.Tasks.Task.Run(()=>{hideStarted.Set();t.SetEnabled(false);hideDone.Set();});
            try
            {
                Check(hideStarted.Wait(5000) && !hideDone.Wait(50),"Hide waits for previously admitted callback's native switch sequence");
            }
            finally {finish.Set();}
            Check(System.Threading.Tasks.Task.WaitAll(new[]{paint,hide},5000) && !view.Enabled && view.Skip,
                "Hide returns disabled after earlier Configure tried SetEnable(true)");
            view.BeforeSetScene=null;
        }
        var native=target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets;
        target.Paint();
        Check(target.UserGets==0 && native==target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets,
            "Hidden callback checks managed binding before ANY sender native getter");
        t.Retire();native=target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets;target.Paint();
        Check(native==target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets,
            "Retired callback touches neither UserData nor validity nor native view getters");
        t.SetEnabled(true);Check(!view.Enabled,"Retired lifetime cannot be reenabled");
        t.Release();native=target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets;target.Paint();
        Check(native==target.UserGets+target.ValidGets+target.ReleasedGets+target.ViewGets && target.Releases==1,
            "Released callback rejects before touching a native getter or stale pointer");
    }
    static void RetirementChecks()
    {
        MapGeometryRetirementQueue.OnApplicationTick();MapGeometryRetirementQueue.OnApplicationTick();
        Defaults();var c=BattleMiniMapConfig.Get();var map=new Map();var source=Source();
        var r=new MapGeometryRenderer("exit-grace",source);Tick(r,map);var old=TableauView.Last;
        c.UseCollisionScenePreview=true;r.Update(map,true);var current=TableauView.Last;
        var reads=source.Reads;var cameras=Camera.Releases;
        r.Dispose();r.Dispose();
        Check(!old.IsReleased && !current.IsReleased && !old.NativeView.Enabled && !current.NativeView.Enabled && r.Texture==null,
            "Source switch and same-frame Dispose retire both targets without immediate release");
        MapGeometryRetirementQueue.OnApplicationTick();
        Check(!old.IsReleased && !current.IsReleased && source.Reads==reads,"First menu/application tick preserves both resource grace periods and stopped reads");
        MapGeometryRetirementQueue.OnApplicationTick();
        Check(old.IsReleased && current.IsReleased && old.Releases==1 && current.Releases==1 && Camera.Releases==cameras+2,
            "Second application tick releases current and pending target/camera resources once without Mission ticks");
        MapGeometryRetirementQueue.OnApplicationTick();
        Check(old.Releases==1 && current.Releases==1 && source.Entities.Count==1,
            "Further application ticks neither double release nor invalidate borrowed source scene");
        MapGeometryRetirementQueue.Enqueue(()=>{throw new Exception("controlled release failure");});var completed=false;
        MapGeometryRetirementQueue.Enqueue(()=>completed=true);
        MapGeometryRetirementQueue.OnApplicationTick();MapGeometryRetirementQueue.OnApplicationTick();
        Check(completed,"Application retirement continues after an individual cleanup failure");
    }
    static void NormalizationChecks()
    {
        Defaults();var tiny=new MapLiveGeometryRules(0,0,.002f,.002f);
        foreach(var collision in new[]{false,true})
        foreach(var extremeOrigin in new[]{false,true})
        {
            var source=Source();
            if(extremeOrigin)source.Entities[0].Frame.origin=new Vec3(1e38f,0,0);
            else source.Entities[0].Frame.rotation.s=new Vec3(1e38f,0,0);
            var target=new Scene();var collector=new MapLiveSceneGeometry(source,target,new Material(),tiny,false,collision);
            for(var i=0;i<200&&!collector.InitialFinished&&collector.Failure==null;++i)collector.Update(.01f);
            Check(collector.InitialFinished && collector.Failure==null && collector.Partial && collector.Errors>0 && target.Entities.Count==0,
                (collision?"Collision":"Render")+" rejects finite source "+(extremeOrigin?"origin":"axes")+" overflowing normalization before native SetFrame");
            Check(collector.ErrorCount(collision?"body_normalized_frame_nonfinite":"render_normalized_frame_nonfinite")==1,
                "Normalized frame rejection has explicit diagnostic reason");collector.Dispose();
        }
        // The existing scalar input guard caps production query heights. Exercise
        // Point itself with a finite extreme argument, without changing rules or
        // snapshots, then check the same finite predicate used before upload.
        var scalarSource=new Scene{Height=v=>1e38f};var scalarTarget=new Scene();
        var scalar=new MapLiveSceneGeometry(scalarSource,scalarTarget,new Material(),tiny);
        var point=(Vec3)typeof(MapLiveSceneGeometry).GetMethod("Point",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
            .Invoke(scalar,new object[]{0,0,1e38f});
        var finite=(bool)typeof(MapLiveSceneGeometry).GetMethod("Valid",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic,
            null,new[]{typeof(Vec3)},null).Invoke(null,new object[]{point});
        Check(float.IsInfinity(point.z) && !finite,"Scalar Point finite extreme height overflows normalization and fails shared pre-upload finite predicate");
        for(var i=0;i<200&&!scalar.InitialFinished&&scalar.Failure==null;++i)scalar.Update(.01f);
        Check(scalar.InitialFinished && scalar.Failure==null && scalar.Partial && scalarTarget.Entities.Count==0 && scalarSource.HeightReads==tiny.GridX*tiny.GridY,
            "Existing scalar collector rejects extreme fake heights without uploading or adding any queries");scalar.Dispose();
        // Raw constructor/rules range guards remain intact; normal raw uploads,
        // format rejection and budgets are covered by the raw terrain regression.

    }
}

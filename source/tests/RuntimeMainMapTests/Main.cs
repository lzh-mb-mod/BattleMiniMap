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

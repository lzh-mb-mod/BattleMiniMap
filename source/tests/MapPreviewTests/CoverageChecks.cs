using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class CoverageChecks
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);++checks;Console.WriteLine("PASS coverage: "+message);}
    static void Finish(MapLiveSceneGeometry live){for(var i=0;i<2000&&!live.InitialFinished&&live.Failure==null;++i)live.Update(.01f);}
    public static void Run(string cache)
    {
        var rules=new MapLiveGeometryRules(0,0,8,8);
        var source=new Scene();
        for(var i=0;i<1000;++i)GameEntity.CreateEmpty(source,false,false);
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules))
        {
            for(var i=0;i<1000&&!live.Coverage.TerrainEnded;++i)live.Update(0);
            var status=live.Coverage;
            Check(status.TerrainPercent==100&&status.TerrainText=="terrain_ended"&&!status.InitialFinished&&status.ObjectsText=="objects_scanning","Terrain 100 percent still reports unfinished object scan");
            Finish(live);status=live.Coverage;
            Check(!live.Partial&&status.ObjectsText=="objects_ended"&&status.CoverageText=="coverage_unverified","Clean scan end promises only unverified coverage");
            var reads=source.HeightReads;for(var i=0;i<20;++i)live.Update(1);
            Check(source.HeightReads==reads&&live.Coverage.TerrainText=="terrain_ended","Object refresh cannot restart terrain stage");
        }
        source=new Scene();GameEntity.CreateEmpty(source,false,false).Shape=new PhysicsShape{MeshN=208,TriangleKind=1};
        GameEntity.CreateEmpty(source,false,false).Shape=new PhysicsShape{Ready=false};
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules,true,true))
        {
            Finish(live);var status=live.Coverage;
            Check(status.InitialFinished&&status.Rejected==208&&status.Unsupported==1&&status.CoverageText=="coverage_gaps","208 rejected geometry items and unsupported shape remain item counts after scan end");
        }
        source=new Scene();GameEntity.CreateEmpty(source,false,false).Skeleton=new object();
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules))
        {
            Finish(live);Check(live.Coverage.Skipped==1&&live.Partial&&live.Coverage.CoverageText=="coverage_gaps","Skeleton skip remains an explicit partial-coverage gap");
        }
        source=new Scene();GameEntity.CreateEmpty(source,false,false).Visible=false;
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules))
        {
            Finish(live);var status=live.Coverage;
            Check(status.InitialFinished&&status.Skipped==1&&!live.Partial&&status.CoverageText=="coverage_unverified","Hidden render entity is counted as skipped without claiming a coverage gap");
        }
        foreach(var sampleBudget in new[]{true,false})
        {
            source=new Scene{Height=p=>p.x+10*p.y};var target=new Scene();
            var memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
            MapLiveSceneGeometry live=null;
            // Seed prior accounting through the existing lazy memory injection. The real
            // collector then crosses its unchanged production limit using just one 3x3 tile.
            live=new MapLiveSceneGeometry(source,target,new Material(),rules,true,false,true,()=>{
                var field=typeof(MapLiveSceneGeometry).GetField(sampleBudget?"_rawSamples":"_rawTriangles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                field.SetValue(live,sampleBudget?7999999:12000000);
                return memory;
            });
            using(live)
            {
                Finish(live);var status=live.Coverage;
                Check(live.Failure==null&&status.InitialFinished&&status.TerrainEnded&&status.TerrainHasGaps&&status.TerrainText=="terrain_gaps"&&status.CoverageText=="coverage_gaps","Raw "+(sampleBudget?"sample":"face")+" budget truncation ends terrain with a distinct gap");
                Check(live.Partial&&status.TerrainFailures==0&&status.Rejected==0&&target.Entities.Count==0&&memory.SampleReads==1&&source.HeightReads==(sampleBudget?0:5),"Budget gap uses one tiny raw tile without fabricated failures or scalar replacement "+sampleBudget);
                var reads=memory.Reads;for(var i=0;i<20;++i)live.Update(1);
                Check(memory.Reads==reads&&live.Coverage.TerrainText=="terrain_gaps"&&live.Coverage.TerrainFailures==0,"Object refresh preserves terrain budget gap without retry "+sampleBudget);
            }
        }
        source=new Scene();var attempts=0;
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules,true,false,true,()=>{++attempts;throw new InvalidOperationException("private native detail");}))
        {
            Finish(live);var status=live.Coverage;
            Check(status.TerrainText=="terrain_unavailable"&&status.TerrainFailures==1&&status.ObjectsText=="objects_ended"&&status.HasKnownGaps,"Raw unavailability preserves separate object scan result and terrain gap");
            for(var i=0;i<20;++i)live.Update(1);
            Check(attempts==1&&source.HeightReads==0,"Unavailable raw has no automatic retry or scalar replacement on refresh");
        }
        source=new Scene{Height=p=>float.NaN};
        using(var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),rules))
        {
            Finish(live);Check(live.Coverage.TerrainFailures==9&&live.Coverage.TerrainText=="terrain_gaps","Invalid terrain samples are counted separately from terrain stage end");
        }

        var config=BattleMiniMapConfig.Get();config.ShowGeometryPreview=true;config.UseLiveScenePreview=true;
        config.UseCollisionScenePreview=false;config.UseRawRuntimeTerrain=false;config.SimplifyRuntimeTerrain=false;
        var provider=new MapGeometryRenderer{Data=MapGeometryData.Load(cache)};
        using(var missing=new MapGeometryPreviewRenderer())
        {
            missing.Update(provider);var created=Scene.Created;
            for(var i=0;i<4;++i)missing.Update(provider);
            Check(missing.Problem==MapPreviewProblem.SourceUnavailable&&!missing.Available&&Scene.Created==created&&provider.Reads==0,"Missing runtime source is classified without cache fallback or rebuild loop");
        }
        config.ShowGeometryPreview=true;config.UseCollisionScenePreview=true;MapLiveCollisionGuard.Reject=true;
        source=new Scene();
        using(var preview=new MapGeometryPreviewRenderer(source,0,0,8,8))
        {
            for(var i=0;i<100&&preview.Problem==MapPreviewProblem.None;++i)preview.Update(provider);
            Check(preview.Problem==MapPreviewProblem.CollectionFailed&&preview.Live==null&&!preview.Available,"Collector failure classification survives clearing its disposed live source");
            var created=Scene.Created;MapLiveCollisionGuard.Reject=false;
            for(var i=0;i<5;++i)preview.Update(provider);
            Check(preview.Problem==MapPreviewProblem.CollectionFailed&&Scene.Created==created,"Ordinary updates cannot retry a failed collector");
            config.ShowGeometryPreview=false;preview.Update(provider);config.ShowGeometryPreview=true;preview.Update(provider);
            Check(preview.Available&&preview.Problem==MapPreviewProblem.None,"Explicit close and reopen starts a fresh collector lifetime");
            preview.Orbit.Orbit(37,12);preview.Orbit.Pan(15,7,608);preview.Orbit.Zoom(2);
            var yaw=preview.Orbit.Yaw;var pitch=preview.Orbit.Pitch;var distance=preview.Orbit.Distance;var at=preview.Orbit.Target;
            foreach(var mode in new[]{0,1,2,3})
            {
                var oldTarget=TableauView.Last;var oldLive=preview.Live;
                if(mode==0)config.UseCollisionScenePreview=false;
                if(mode==1)config.UseRawRuntimeTerrain=true;
                if(mode==2)config.SimplifyRuntimeTerrain=true;
                if(mode==3)config.UseLiveScenePreview=false;
                preview.Update(provider);
                Check(TableauView.Last!=oldTarget&&!oldTarget.NativeView.Enabled&&!oldTarget.NativeView.Continuous,"Source mode "+mode+" retires previous render target");
                Check(preview.Orbit.Yaw==yaw&&preview.Orbit.Pitch==pitch&&preview.Orbit.Distance==distance&&preview.Orbit.Target.X==at.X&&preview.Orbit.Target.Y==at.Y&&preview.Orbit.Target.Z==at.Z,"Source mode "+mode+" preserves entire orbit pose");
                var reads=source.Reads;var heights=source.HeightReads;for(var i=0;i<50;++i)oldLive.Update(1);
                Check(source.Reads==reads&&source.HeightReads==heights,"Retired collector stops before deferred resource release "+mode);
                var gets=oldTarget.ViewGets;oldTarget.Paint();Check(oldTarget.ViewGets==gets,"Late retired callback cannot access native target "+mode);
                preview.Update(provider);preview.Update(provider);Check(oldTarget.IsReleased,"Retired target eventually releases "+mode);
            }
            Check(preview.Live==null&&provider.Reads>0,"Cache is an independent manual diagnostic source");
        }
        config.UseLiveScenePreview=false;config.UseCollisionScenePreview=false;config.UseRawRuntimeTerrain=false;config.SimplifyRuntimeTerrain=false;
        Console.WriteLine("PASS: "+checks+" coverage/lifecycle checks through production collector, status and renderer; fake native boundary");
    }
}

using System;
using System.Linq;
using System.Threading;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class ObjectProfilingChecks
{
    static int checks;
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);++checks;Console.WriteLine("PASS object profiling: "+message);}
    static void Finish(MapLiveSceneGeometry live){for(var i=0;i<1000&&!live.InitialFinished&&live.Failure==null;++i)live.Update(.1f);}
    public static void Run()
    {
        long now=0;var reads=0;
        var p=new MapObjectExtractionProfiler(()=>{++reads;return now;},1000);
        p.BeginStep();now=900;p.Mark(MapObjectStage.CopyShape,1,2);now=912;p.Mark(MapObjectStage.PrepareShape,3,4);now=915;p.EndStep();
        Check(p.StepContext.Contains("objectSegment=CopyShape")&&p.StepContext.Contains("objectSegmentMs=12.000")&&p.StepContext.Contains("objectEntity=1")&&p.StepContext.Contains("objectShape=2"),"Longest segment retains earlier entity rather than last entity in step");
        Check(p.Summary.Contains("CopyShape:1,12.000,12.000")&&p.Summary.Contains("PrepareShape:1,3.000,3.000"),"Counts and wall totals separate synchronous groups and exclude dispatch before first mark");
        now=999999;p.BeginStep();p.Mark(MapObjectStage.PrepareShape);now+=2;p.EndStep();
        Check(p.Summary.Contains("PrepareShape:2,5.000,3.000"),"Yield interval is excluded while repeated stage totals accumulate");
        var before=reads;p.Mark(MapObjectStage.ReleaseShape);p.EndStep();
        Check(reads==before,"Dispose outside executing MoveNext does not reopen timer or read clock");
        p.BeginStep();p.EndStep();Check(p.StepContext=="objectSegment=none","No object mark cannot inherit preceding step identity");
        Check(p.Summary.Contains("CopyShape:1,12.000,12.000"),"Empty terrain steps do not add object segments");
        foreach(var pair in new[]{
            new[]{"CopyShape","CopyShape"},new[]{"PrepareShape","PrepareShape"},
            new[]{"TriangleBuild","TriangleBuild"},new[]{"Upload","TriangleUpload"},
            new[]{"BodyFrame","BodyFrame"},new[]{"CreateInstance","InstanceCreate"},new[]{"UpdateInstance","InstanceUpdate"}})
        {
            var source=new Scene();var target=new Scene();var shape=new PhysicsShape();
            GameEntity.CreateEmpty(source,false,false).Shape=shape;
            GameEntity.CreateEmpty(source,false,false).Shape=shape;
            var start=TaleWorlds.Library.Debug.Messages.Count;var delayed=false;var prepareCalls=0;
            var live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,8,8),false,true,profile:true);
            try
            {
                NativeProbe.Before=name=>{if(name=="PrepareShape")++prepareCalls;if(name==pair[0]&&!delayed){delayed=true;Thread.Sleep(12);}};
                Finish(live);
                var logs=TaleWorlds.Library.Debug.Messages.Skip(start).Where(s=>s.Contains("slow step:"));
                Check(live.InitialFinished&&live.Failure==null&&logs.Any(s=>s.Contains("objectSegment="+pair[1])&&s.Contains("tile=-1 chunk=-1,-1 patch=-1,-1")),"Delayed fake "+pair[0]+" maps to its measured group, without stale terrain coordinates");
                Check(shape.Copies==1&&shape.Reads==1&&prepareCalls==1&&target.Entities.Count==2&&ReferenceEquals(target.Entities[0].Added,target.Entities[1].Added),"Profiling preserves shared-shape reuse for "+pair[0]);
            }
            finally{NativeProbe.Before=null;live.Dispose();}
        }
        foreach(var fail in new[]{false,true})
        {
            var source=new Scene();var shape=new PhysicsShape();GameEntity.CreateEmpty(source,false,false).Shape=shape;
            var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,8,8),false,true,profile:true);
            var released=0;
            try
            {
                NativeProbe.Before=name=>{if(name=="ReleaseShape")++released;if(name=="PrepareShape"){if(fail)throw new InvalidOperationException("deliberate fake prepare failure");Thread.Sleep(12);}};
                for(var i=0;i<1000&&shape.Copies==0&&live.Failure==null;++i)live.Update(.1f);
                Check(!live.InitialFinished&&(fail?live.Failure!=null:live.Failure==null),"Prepared step remains cancellable or isolates prepare failure "+fail);
                live.Dispose();
                Check(released==1&&shape.IsValid&&shape.Released==0,"Cancel/failure releases owned shape once without touching source "+fail);
            }
            finally{NativeProbe.Before=null;live.Dispose();}
        }
        string expected=null;
        foreach(var enabled in new[]{false,true})
        {
            var source=new Scene();var target=new Scene();var entity=GameEntity.CreateEmpty(source,false,false);var meta=new MetaMesh();meta.Meshes.Add(new Mesh());entity.Metas.Add(meta);
            var start=TaleWorlds.Library.Debug.Messages.Count;
            using(var live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,8,8),false,profile:enabled))
            {
                Finish(live);var snapshot=live.CopiedMeshes+":"+live.SourceFaces+":"+target.Entities.Count+":"+live.Partial;
                if(!enabled)expected=snapshot;
                Check(live.InitialFinished&&snapshot==expected,"Render geometry and coverage preserved with profiling "+enabled);
                var logs=string.Join("\n",TaleWorlds.Library.Debug.Messages.Skip(start));
                Check(logs.Contains(enabled?"objectSegments=wall-count,totalMs,maxMs:":"objectSegments=off"),"Object timing switch explicitly labels "+enabled);
            }
        }
        Console.WriteLine("PASS: "+checks+" object segment checks; fake native delays locate groups, not game latency causes");
    }
}

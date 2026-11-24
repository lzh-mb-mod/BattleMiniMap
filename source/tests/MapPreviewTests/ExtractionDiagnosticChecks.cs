using System;
using System.Threading;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class ExtractionDiagnosticChecks
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);++checks;Console.WriteLine("PASS extraction diagnostics: "+message);}
    static void Finish(MapLiveSceneGeometry live){for(var i=0;i<1000&&!live.InitialFinished;++i)live.Update(.1f);}
    public static void Run()
    {
        var reads=0;
        var diagnostics=new MapExtractionDiagnostics(()=>++reads==1?new MapExtractionSample(100000,1024,2,3,4):new MapExtractionSample(120000,4096,3,3,4));
        diagnostics.BeginTick();
        Check(diagnostics.SlowStep(7.9,8)==null&&reads==1,"Fast step does not capture CPU or heap again");
        var text=diagnostics.SlowStep(54,55);
        Check(text.Contains("scope=tick-start-to-sample")&&text.Contains("tickThreadCpuMs=2.000")&&text.Contains("stepWallMs=54.000"),"Slow report explicitly separates step wall and tick-scoped CPU");
        Check(text.Contains("gc0Delta=1")&&text.Contains("gc1Delta=0")&&text.Contains("heapDeltaBytes=3072"),"GC overlap and heap estimate delta are reported without invented allocation count");
        for(var i=0;i<12;++i)diagnostics.SlowStep(9,10);
        Check(diagnostics.SlowSteps==13&&diagnostics.LoggedRecords==8&&reads==9,"Report limit caps expensive snapshots but retains total slow-step count");
        diagnostics=new MapExtractionDiagnostics(()=>new MapExtractionSample(-1,2000,0,0,0));diagnostics.BeginTick();
        Check(diagnostics.SlowStep(8,9).Contains("tickThreadCpuMs=unavailable"),"Unavailable current-thread CPU is explicit");
        reads=0;diagnostics=new MapExtractionDiagnostics(()=>++reads==1?new MapExtractionSample(0,4000,0,0,0):new MapExtractionSample(0,1000,0,0,0));diagnostics.BeginTick();
        Check(diagnostics.SlowStep(8,9).Contains("heapDeltaBytes=-3000"),"Heap shrinkage remains a signed occupancy delta");
        diagnostics=new MapExtractionDiagnostics(()=>new MapExtractionSample(0,1000,0,0,0));diagnostics.BeginTick();
        Check(diagnostics.SlowTick(54).Contains("stepWallMs=unavailable")&&diagnostics.SlowTicksWithoutStep==1,"Slow whole tick without a slow MoveNext is not falsely attributed to a step");
        diagnostics.BeginTick();diagnostics.SlowStep(9,10);
        Check(diagnostics.SlowTick(12)==null,"Slow step and whole tick do not produce duplicate context records");
        var before=MapExtractionSample.Capture();Thread.Sleep(2);var after=MapExtractionSample.Capture();
        Check(before.ThreadCpu100Ns>=0&&after.ThreadCpu100Ns>=before.ThreadCpu100Ns&&after.HeapBytes>=0,"Actual own-thread CPU and managed heap probes are usable and monotonic on Windows");
        foreach(var enabled in new[]{false,true})
        {
            var source=new Scene{Height=p=>p.x+10*p.y};var target=new Scene();var memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
            var begin=TaleWorlds.Library.Debug.Messages.Count;
            var live=new MapLiveSceneGeometry(source,target,new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,()=>memory,false,profile:enabled);
            Finish(live);live.Dispose();
            var output=string.Join("\n",TaleWorlds.Library.Debug.Messages.GetRange(begin,TaleWorlds.Library.Debug.Messages.Count-begin));
            Check(live.InitialFinished&&!live.Partial&&target.Entities.Count==1&&output.Contains(enabled?"slowDiagnostics=on":"slowDiagnostics=off"),"Profiling toggle preserves successful raw geometry and labels diagnostic mode "+enabled);
        }
        Console.WriteLine("PASS: "+checks+" extraction diagnostic checks; controlled context and own thread, no game slow-step attribution");
    }
}

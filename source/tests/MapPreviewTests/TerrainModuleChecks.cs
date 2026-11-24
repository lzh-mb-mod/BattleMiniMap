using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class TerrainModuleChecks
{
    static int checks;
    static void Check(bool value,string message){if(!value)throw new Exception(message);++checks;Console.WriteLine("PASS terrain modules: "+message);}
    static MapSelfTerrainMemory.ModuleIdentity[] Enumerated()
    {
        var result=new MapSelfTerrainMemory.ModuleIdentity[2];
        using(var process=Process.GetCurrentProcess())foreach(ProcessModule module in process.Modules)
        {
            var index=string.Equals(module.ModuleName,"kernel32.dll",StringComparison.OrdinalIgnoreCase)?0:string.Equals(module.ModuleName,"kernelbase.dll",StringComparison.OrdinalIgnoreCase)?1:-1;
            if(index>=0)result[index]=new MapSelfTerrainMemory.ModuleIdentity(module.FileName,unchecked((ulong)module.BaseAddress.ToInt64()),module.ModuleMemorySize);
        }
        return result;
    }
    static IEnumerable<int> Busy(Action<IMapTerrainMemory> complete,IMapTerrainMemory memory,bool slow)
    {
        if(slow)Thread.Sleep(4); // Deliberately unpreemptible operation proves the time-budget path.
        for(var i=0;i<12000;++i)yield return 0;
        complete(memory);
    }
    public static void Run(string reportPath)
    {
        var expected=Enumerated();var pair=MapTerrainModuleLease.Pair.Acquire("kernel32.dll","kernelbase.dll");
        Check(expected[0]!=null&&expected[1]!=null&&expected[0].Matches(pair.Native.Identity)&&expected[1].Matches(pair.Physx.Identity),"Actual direct Windows lookup matches Process.Modules path/base/image size");
        pair.Dispose();pair.Dispose();Check(pair.Native.IsClosed&&pair.Physx.IsClosed,"Both module references close and repeated Dispose is harmless");
        var rejected=false;
        try{using(var missing=MapTerrainModuleLease.Acquire("BattleMiniMap.missing."+Guid.NewGuid().ToString("N")+".dll")){} }
        catch(InvalidDataException){rejected=true;}
        Check(rejected,"Missing module is rejected without loading a DLL");
        MapTerrainModuleLease first=null;rejected=false;
        try
        {
            MapTerrainModuleLease.Pair.Acquire("first","second",name=>
            {if(name=="first")return first=MapTerrainModuleLease.Acquire("kernel32.dll");throw new InvalidDataException("second module missing");});
        }
        catch(InvalidDataException){rejected=true;}
        Check(rejected&&first!=null&&first.IsClosed,"Second-module failure immediately releases the first real module reference");
        var cancelled=false;
        try{using(var lease=MapTerrainModuleLease.Pair.Acquire("kernel32.dll","kernelbase.dll")){pair=lease;throw new OperationCanceledException();}}
        catch(OperationCanceledException){cancelled=true;}
        Check(cancelled&&pair.Native.IsClosed&&pair.Physx.IsClosed,"Owner cancellation scope releases both module references");
        // Same current-process system modules, warmed calls, no game process or geometry read.
        var baseline=new List<double>();var direct=new List<double>();
        for(var run=0;run<7;++run)
        {
            var clock=Stopwatch.StartNew();var old=Enumerated();baseline.Add(clock.Elapsed.TotalMilliseconds);
            clock.Restart();using(var found=MapTerrainModuleLease.Pair.Acquire("kernel32.dll","kernelbase.dll"))
                if(!old[0].Matches(found.Native.Identity)||!old[1].Matches(found.Physx.Identity))throw new Exception("Module lookup mismatch");
            direct.Add(clock.Elapsed.TotalMilliseconds);
        }
        var oldMedian=baseline.OrderBy(x=>x).ElementAt(3);var newMedian=direct.OrderBy(x=>x).ElementAt(3);
        Console.WriteLine("MODULE LOOKUP median oldMs="+oldMedian.ToString("F6",System.Globalization.CultureInfo.InvariantCulture)+" directMs="+newMedian.ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        if(reportPath!=null)File.WriteAllText(reportPath,System.Text.Json.JsonSerializer.Serialize(new {scope="current net9 process kernel32/kernelbase; no game lookup or FPS proof",baselineMs=baseline,directMs=direct,baselineMedianMs=oldMedian,directMedianMs=newMedian},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
        foreach(var slow in new[]{false,true})
        {
            var source=new Scene();var memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
            var collector=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,rawInitialization:done=>Busy(done,memory,slow));
            collector.Update(.1f);
            Check(collector.TerrainMaxStepsUsed<=2048&&collector.TerrainMaxStepsUsed>0&&memory.Reads==0,"Bounded raw work stays below 2048 steps before startup finishes");
            Check(collector.TerrainStepLimitedTicks+collector.TerrainTimeLimitedTicks==1&&collector.TerrainWaitTicks==0,"Active raw tick records a quota/time reason rather than a worker wait");
            if(slow)Check(collector.TerrainTimeLimitedTicks==1,"Unpreemptible startup over 2 ms is reported as time-limited");
            collector.Dispose();collector.Update(1);Check(memory.Reads==0,"Cancelled quota-limited collector never begins raw memory access");
        }
        Console.WriteLine("PASS: "+checks+" direct module ownership and terrain scheduling checks; own process only");
    }
}

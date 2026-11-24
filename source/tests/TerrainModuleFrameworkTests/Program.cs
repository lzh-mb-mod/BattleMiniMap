using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using BattleMiniMap.View.MapGeometry;

class Program
{
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);++checks;Console.WriteLine("PASS framework: "+text);}
    static void Main()
    {
        Check(IntPtr.Size==8,"Same net472 x64 target as game mod");
        using(var pair=MapTerrainModuleLease.Pair.Acquire("kernel32.dll","kernelbase.dll"))
        using(var process=Process.GetCurrentProcess())
        {
            foreach(var lease in new[]{pair.Native,pair.Physx})
            {
                var path=lease.Identity.Path;
                var module=process.Modules.Cast<ProcessModule>().Single(m=>string.Equals(m.FileName,path,StringComparison.OrdinalIgnoreCase));
                Check(lease.Identity.Base==unchecked((ulong)module.BaseAddress.ToInt64())&&lease.Identity.Size==module.ModuleMemorySize,"Actual Framework SafeHandle/PInvoke identity matches own module enumeration");
            }
        }
        MapTerrainModuleLease first=null;var failed=false;
        try{MapTerrainModuleLease.Pair.Acquire("first","second",name=>{if(name=="first")return first=MapTerrainModuleLease.Acquire("kernel32.dll");throw new InvalidDataException("missing");});}
        catch(InvalidDataException){failed=true;}
        Check(failed&&first.IsClosed,"Framework partial-acquisition failure releases first handle");
        var pathToFile=Path.GetTempFileName();
        try
        {
            File.WriteAllText(pathToFile,"abc");
            MapSelfTerrainMemory.VerifyFile(pathToFile,"ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",CancellationToken.None);
            Check(true,"Framework chunked SHA verification matches known abc digest");
        }
        finally{File.Delete(pathToFile);}
        var published=false;var owner=Thread.CurrentThread.ManagedThreadId;var worker=owner;
        using(var steps=new MapTerrainVerification().Run(token=>{worker=Thread.CurrentThread.ManagedThreadId;return new Memory();},memory=>published=true).GetEnumerator())
        {
            var clock=Stopwatch.StartNew();
            while(steps.MoveNext()){if(clock.Elapsed.TotalSeconds>5)throw new Exception("Timed out");Thread.Sleep(1);}
        }
        Check(published&&worker!=owner,"Framework verification dispatch and completion use expected threads");
        var sample=MapExtractionSample.Capture();
        Check(sample.ThreadCpu100Ns>=0&&sample.HeapBytes>=0,"Framework own-thread CPU/heap/GC snapshot succeeds");
        var probeClock=Stopwatch.StartNew();for(var i=0;i<1000;++i)MapExtractionSample.Capture();
        Console.WriteLine("FRAMEWORK PROBE averageMs="+(probeClock.Elapsed.TotalMilliseconds/1000).ToString("F6",System.Globalization.CultureInfo.InvariantCulture));
        Console.WriteLine("PASS: "+checks+" net472 x64 module/verification checks; system modules only, no game extraction");
    }
    private class Memory:IMapTerrainMemory
    { public ulong NativeBase=>0;public ulong PhysxBase=>0;public byte[] Read(ulong address,int size)=>throw new InvalidOperationException("Must not read native memory"); }
}
namespace BattleMiniMap.View.MapGeometry
{
    // Test boundary only: same contract as MapRuntimeTerrainReader, with no engine dependency.
    interface IMapTerrainMemory { byte[] Read(ulong address,int size);ulong NativeBase{get;}ulong PhysxBase{get;} }
}

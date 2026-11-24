using System;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BattleMiniMap.View.MapGeometry
{
    internal readonly struct MapExtractionSample
    {
        public readonly long ThreadCpu100Ns,HeapBytes;
        public readonly int Gc0,Gc1,Gc2;
        public MapExtractionSample(long cpu,long heap,int g0,int g1,int g2)
        {ThreadCpu100Ns=cpu;HeapBytes=heap;Gc0=g0;Gc1=g1;Gc2=g2;}
        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {public uint Low,High;public long Ticks=>unchecked((long)(((ulong)High<<32)|Low));}
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadTimes(IntPtr thread,out FileTime created,out FileTime exited,out FileTime kernel,out FileTime user);
        private static long CurrentThreadCpu()
        {
            try
            {
                // -2 is the calling-thread pseudo handle. Never opens or samples another thread.
                return GetThreadTimes(new IntPtr(-2),out var created,out var exited,out var kernel,out var user)?kernel.Ticks+user.Ticks:-1;
            }
            catch(DllNotFoundException){return -1;}
            catch(EntryPointNotFoundException){return -1;}
        }
        public static MapExtractionSample Capture()
        {
            var cpu=CurrentThreadCpu();var g0=GC.CollectionCount(0);var g1=GC.CollectionCount(1);var g2=GC.CollectionCount(2);
            return new MapExtractionSample(cpu,GC.GetTotalMemory(false),g0,g1,g2);
        }
    }
    internal sealed class MapExtractionDiagnostics
    {
        public const double SlowStepMs=8;
        private readonly Func<MapExtractionSample> _capture;
        private MapExtractionSample _begin;
        public int SlowSteps {get;private set;}
        public int LoggedRecords {get;private set;}
        public int SlowTicksWithoutStep {get;private set;}
        private bool _tickHadSlowStep;
        public int GcTicks {get;private set;}
        public MapExtractionDiagnostics(Func<MapExtractionSample> capture=null){_capture=capture??MapExtractionSample.Capture;}
        public void BeginTick(){_begin=_capture();_tickHadSlowStep=false;}
        public void EndTick()
        {
            if(GC.CollectionCount(0)!=_begin.Gc0||GC.CollectionCount(1)!=_begin.Gc1||GC.CollectionCount(2)!=_begin.Gc2)++GcTicks;
        }
        public string SlowStep(double stepWallMs,double tickWallMs)
        {
            if(stepWallMs<SlowStepMs)return null;
            ++SlowSteps;_tickHadSlowStep=true;return Context(stepWallMs,tickWallMs);
        }
        public string SlowTick(double tickWallMs)
        {
            if(tickWallMs<SlowStepMs||_tickHadSlowStep)return null;
            ++SlowTicksWithoutStep;return Context(null,tickWallMs);
        }
        private string Context(double? stepWallMs,double tickWallMs)
        {
            if(LoggedRecords>=8)return null;++LoggedRecords;
            var started=Stopwatch.GetTimestamp();var now=_capture();var probeMs=(Stopwatch.GetTimestamp()-started)*1000d/Stopwatch.Frequency;
            var cpu=_begin.ThreadCpu100Ns>=0&&now.ThreadCpu100Ns>=_begin.ThreadCpu100Ns?
                ((now.ThreadCpu100Ns-_begin.ThreadCpu100Ns)/10000d).ToString("F3",CultureInfo.InvariantCulture):"unavailable";
            return "scope=tick-start-to-sample stepWallMs="+(stepWallMs.HasValue?Ms(stepWallMs.Value):"unavailable")+" tickWallMs="+Ms(tickWallMs)+" tickThreadCpuMs="+cpu+" contextProbeMs="+Ms(probeMs)+
                " gc0Delta="+Math.Max(0,now.Gc0-_begin.Gc0)+" gc1Delta="+Math.Max(0,now.Gc1-_begin.Gc1)+" gc2Delta="+Math.Max(0,now.Gc2-_begin.Gc2)+
                " heapBeforeBytes="+_begin.HeapBytes+" heapAfterBytes="+now.HeapBytes+" heapDeltaBytes="+(now.HeapBytes-_begin.HeapBytes);
        }
        public string Summary=>"slowDiagnostics=on slowSteps="+SlowSteps+" slowTicksWithoutStep="+SlowTicksWithoutStep+" slowRecords="+LoggedRecords+" gcOverlapTicks="+GcTicks;
        private static string Ms(double value)=>value.ToString("F3",CultureInfo.InvariantCulture);
    }
}

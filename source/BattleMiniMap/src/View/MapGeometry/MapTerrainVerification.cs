using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace BattleMiniMap.View.MapGeometry
{
    // The worker must contain only managed file verification, never engine/scene access.
    internal sealed class MapTerrainVerification
    {
        public const int Waiting=1;
        public string State {get;private set;}="not-started";
        public double WorkerMs { get; private set; }
        public double WaitMs { get; private set; }
        public int Polls { get; private set; }
        public double DiscoveryMs { get; internal set; }
        public double DispatchMs { get; private set; }
        public double RecheckMs { get; internal set; }
        public double ReaderBindMs { get; internal set; }
        public IEnumerable<int> Run(Func<CancellationToken,IMapTerrainMemory> verify,Action<IMapTerrainMemory> complete)
        {
            using(var cancellation=new CancellationTokenSource())
            {
                var token=cancellation.Token;var waiting=Stopwatch.StartNew();
                State="dispatching";
                var task=Task.Run(()=>
                {
                    var work=Stopwatch.StartNew();
                    try { token.ThrowIfCancellationRequested();return verify(token); }
                    finally { WorkerMs=work.Elapsed.TotalMilliseconds; }
                });
                // Retired tasks must not leave an unobserved failure after iterator disposal.
                task.ContinueWith(t=>{ var ignored=t.Exception; },CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                DispatchMs=waiting.Elapsed.TotalMilliseconds;
                try
                {
                    yield return 0; // Separate task dispatch from polling/recheck in the owner profiler.
                    while(!task.IsCompleted){State="waiting";++Polls;yield return Waiting;}
                    IMapTerrainMemory memory;
                    try{memory=task.GetAwaiter().GetResult();}
                    catch{State="failed";throw;}
                    State="hash-verified";
                    WaitMs=waiting.Elapsed.TotalMilliseconds;
                    complete(memory);
                }
                finally { WaitMs=waiting.Elapsed.TotalMilliseconds;if(State!="hash-verified"&&State!="failed")State="cancelled";cancellation.Cancel(); }
            }
        }
    }
}

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
        public double WorkerMs { get; private set; }
        public double WaitMs { get; private set; }
        public int Polls { get; private set; }
        public IEnumerable<int> Run(Func<CancellationToken,IMapTerrainMemory> verify,Action<IMapTerrainMemory> complete)
        {
            using(var cancellation=new CancellationTokenSource())
            {
                var token=cancellation.Token;var waiting=Stopwatch.StartNew();
                var task=Task.Run(()=>
                {
                    var work=Stopwatch.StartNew();
                    try { token.ThrowIfCancellationRequested();return verify(token); }
                    finally { WorkerMs=work.Elapsed.TotalMilliseconds; }
                });
                // Retired tasks must not leave an unobserved failure after iterator disposal.
                task.ContinueWith(t=>{ var ignored=t.Exception; },CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted|TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
                try
                {
                    while(!task.IsCompleted){++Polls;yield return Waiting;}
                    var memory=task.GetAwaiter().GetResult();
                    WaitMs=waiting.Elapsed.TotalMilliseconds;
                    complete(memory);
                }
                finally { cancellation.Cancel(); }
            }
        }
    }
}

using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace BattleMiniMap.View.MapGeometry
{
    // Synchronous groups, not native call counts. Never carries a running timer across a yield.
    internal enum MapObjectStage
    {
        Enumerate, Lookup, CopyShape, PrepareShape, ShapeCounts, PrimitiveRead,
        PrimitiveBuild, PrimitiveUpload, StorageGuard, TriangleCount, TriangleBuild,
        TriangleUpload, ReleaseShape, ResourceReport, BodyFrame, InstanceLookup, InstanceCreate,
        InstanceUpdate, RenderMetadata, RenderCopy, HideStale, Complete, Count
    }
    internal sealed class MapObjectExtractionProfiler
    {
        private readonly Func<long> _clock;
        private readonly double _toMs;
        private readonly long[] _counts=new long[(int)MapObjectStage.Count];
        private readonly long[] _total=new long[(int)MapObjectStage.Count],_max=new long[(int)MapObjectStage.Count];
        private bool _running,_marked;
        private long _started,_stepMax;
        private MapObjectStage _stage,_longestStage;
        private ulong _entity,_shape,_longestEntity,_longestShape;
        private int _mesh,_item,_longestMesh,_longestItem;
        public MapObjectExtractionProfiler(Func<long> clock=null,long frequency=0)
        {_clock=clock??Stopwatch.GetTimestamp;_toMs=1000d/(frequency>0?frequency:Stopwatch.Frequency);}
        public void BeginStep(){_running=true;_marked=false;_stepMax=0;}
        public void Mark(MapObjectStage stage,ulong entity=0,ulong shape=0,int mesh=-1,int item=-1)
        {
            if(!_running)return;
            var now=_clock();FinishSegment(now);
            _stage=stage;_entity=entity;_shape=shape;_mesh=mesh;_item=item;_started=now;_marked=true;
        }
        private void FinishSegment(long now)
        {
            if(!_marked)return;
            var elapsed=Math.Max(0,now-_started);var i=(int)_stage;
            ++_counts[i];_total[i]+=elapsed;_max[i]=Math.Max(_max[i],elapsed);
            if(elapsed>_stepMax)
            {_stepMax=elapsed;_longestStage=_stage;_longestEntity=_entity;_longestShape=_shape;_longestMesh=_mesh;_longestItem=_item;}
        }
        public void EndStep(){if(_running&&_marked)FinishSegment(_clock());_running=false;_marked=false;}
        private string Ms(long ticks)=>(ticks*_toMs).ToString("F3",CultureInfo.InvariantCulture);
        public string StepContext=>_stepMax==0?"objectSegment=none":
            "objectSegment="+_longestStage+" objectSegmentMs="+Ms(_stepMax)+" objectEntity="+_longestEntity.ToString("X")+
            " objectShape="+_longestShape.ToString("X")+" objectMesh="+_longestMesh+" objectItem="+_longestItem;
        public string Summary
        {
            get
            {
                var s=new StringBuilder("objectSegments=wall-count,totalMs,maxMs:");
                for(var i=0;i<_counts.Length;++i)
                {if(_counts[i]==0)continue;s.Append((MapObjectStage)i).Append(':').Append(_counts[i]).Append(',').Append(Ms(_total[i])).Append(',').Append(Ms(_max[i])).Append(';');}
                return s.ToString();
            }
        }
    }
}

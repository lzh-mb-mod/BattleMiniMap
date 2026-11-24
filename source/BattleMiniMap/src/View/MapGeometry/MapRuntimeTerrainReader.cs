using System;
using System.IO;
using System.Collections.Generic;

namespace BattleMiniMap.View.MapGeometry
{
    internal interface IMapTerrainMemory
    {
        byte[] Read(ulong address,int size);
        ulong NativeBase { get; }
        ulong PhysxBase { get; }
    }
    // Fixed-build layout, derived from the live scalar-height path and PhysX exports.
    // No native function pointers are called. Failed reads are ordinary managed errors.
    internal sealed class MapRuntimeTerrainReader
    {
        private readonly IMapTerrainMemory _memory;
        private readonly ulong _scene,_terrain;
        private readonly byte[] _header;
        private readonly bool _global;
        private readonly int _nx,_ny;
        private readonly float _nodeSize;
        private readonly int _sampleBudget;
        public int AttemptedSamples { get; private set; }
        public bool BudgetExhausted { get; private set; }
        public int TileCount { get; }
        public int NodeFloatBuffersMissing { get; private set; }
        public string Mode => _global?"global-physx":"node-physx";
        public MapRuntimeTerrainReader(IMapTerrainMemory memory,ulong scene,int sampleBudget=8000000)
        {
            if(sampleBudget<=0||sampleBudget>8000000)throw new ArgumentOutOfRangeException(nameof(sampleBudget));
            _sampleBudget=sampleBudget;
            _memory=memory;_scene=scene;_terrain=Pointer(scene+0x2bf0);
            _header=memory.Read(_terrain,0x70);_global=_header[0x68]!=0;
            _nodeSize=F(_header,0);_nx=I(_header,0xc);_ny=I(_header,0x10);
            if(!_global && (_nx<=0||_ny<=0||_nx>256||_ny>256||(long)_nx*_ny>4096||!MapLiveGeometryRules.Finite(_nodeSize)||_nodeSize<=0))
                throw new InvalidDataException("Unsupported runtime terrain layout");
            TileCount=_global?1:_nx*_ny;
        }
        public MapTerrainHeightfield ReadTile(int index)
        {
            MapTerrainHeightfield result=null;
            foreach(var step in ReadTileSteps(index,tile=>result=tile)) { }
            return result;
        }
        // The production collector drives this iterator under its frame budget.
        public IEnumerable<int> ReadTileSteps(int index,Action<MapTerrainHeightfield> complete)
        {
            if(index<0||index>=TileCount)throw new ArgumentOutOfRangeException(nameof(index));
            if(Pointer(_scene+0x2bf0)!=_terrain)throw new InvalidDataException("Terrain changed before snapshot");
            var x=_global?0:index/_ny;var y=_global?0:index%_ny;byte[] node=null;ulong nodeAddress=0;
            if(!_global)
            {
                var rows=U(_header,0x40);nodeAddress=Pointer(rows+(ulong)x*8)+(ulong)y*0x78;
                node=_memory.Read(nodeAddress,0x78);
                if(U(node,0)==0 && I(node,0xc)>0)++NodeFloatBuffersMissing;
            }
            var provider=_global?U(_header,0x50):U(node,0x68);
            if(provider==0)throw new InvalidDataException("Runtime heightfield provider is absent");
            var rgl=_memory.Read(provider,0x18);
            if(U(rgl,0)!=_memory.NativeBase+0xb089b8)throw new InvalidDataException("Unknown terrain heightfield provider type");
            var physx=U(rgl,8);var h=_memory.Read(physx,0x80);
            if(U(h,0)!=_memory.PhysxBase+0x129ea0 || I(h,0x60)!=1)
                throw new InvalidDataException("Unknown PhysX heightfield type or format");
            var rowsCount=I(h,0x38);var columns=I(h,0x3c);
            if(rowsCount<2||columns<2||rowsCount>4097||columns>4097||(long)rowsCount*columns>4500000)
                throw new InvalidDataException("Raw heightfield dimensions exceed budget");
            var samples=U(h,0x50);
            if(samples==0)throw new InvalidDataException("Raw heightfield samples are absent");
            var sampleCount=checked(rowsCount*columns);
            if(AttemptedSamples+(long)sampleCount>_sampleBudget)
            { BudgetExhausted=true;throw new InvalidDataException("Raw terrain read budget exhausted"); }
            // Reserve BEFORE allocation/read; rejected snapshots still consume read budget.
            AttemptedSamples+=sampleCount;
            var data=new byte[checked(sampleCount*4)];
            for(var byteOffset=0;byteOffset<data.Length;)
            {
                var size=Math.Min(1048576,data.Length-byteOffset);
                var part=_memory.Read(checked(samples+(ulong)byteOffset),size);
                if(part==null||part.Length!=size)throw new InvalidDataException("Incomplete raw terrain sample read");
                Buffer.BlockCopy(part,0,data,byteOffset,size);byteOffset+=size;
                yield return 0;
            }
            var after=_memory.Read(physx,0x80);var providerAfter=_memory.Read(provider,0x18);
            if(U(after,0)!=U(h,0)||I(after,0x38)!=rowsCount||I(after,0x3c)!=columns||U(after,0x50)!=samples||I(after,0x60)!=1||I(after,0x78)!=I(h,0x78)||
                U(providerAfter,0)!=U(rgl,0)||U(providerAfter,8)!=physx||I(providerAfter,0x10)!=I(rgl,0x10)||Pointer(_scene+0x2bf0)!=_terrain)
                throw new InvalidDataException("Raw heightfield changed during snapshot");
            var rootAfter=_memory.Read(_terrain,0x70);
            if(rootAfter[0x68]!=_header[0x68]||(!_global && (I(rootAfter,0xc)!=_nx||I(rootAfter,0x10)!=_ny||I(rootAfter,0)!=I(_header,0))))
                throw new InvalidDataException("Terrain layout changed during snapshot");
            float step,originX,originY,offset;
            if(_global)
            {
                var cells=I(_header,0x60);
                if(cells<=0 || U(rootAfter,0x50)!=provider || I(rootAfter,0x58)!=I(_header,0x58)||I(rootAfter,0x5c)!=I(_header,0x5c)||I(rootAfter,0x60)!=cells)
                    throw new InvalidDataException("Global heightfield mapping changed or is invalid");
                step=F(_header,0x58)/cells;originX=originY=0;offset=F(_header,0x5c);
            }
            else
            {
                var nodeAfter=_memory.Read(nodeAddress,0x78);
                if(U(rootAfter,0x40)!=U(_header,0x40)||Pointer(U(_header,0x40)+(ulong)x*8)+(ulong)y*0x78!=nodeAddress||
                    U(nodeAfter,0x68)!=provider||I(nodeAfter,0x10)!=I(node,0x10)||I(nodeAfter,0xc)!=I(node,0xc))
                    throw new InvalidDataException("Node heightfield mapping changed");
                if(rowsCount!=I(node,0xc)||columns!=I(node,0xc))throw new InvalidDataException("Node and PhysX dimensions disagree");
                step=F(node,0x10);originX=x*_nodeSize;originY=y*_nodeSize;offset=0;
            }
            complete(new MapTerrainHeightfield(rowsCount,columns,data,originX,originY,step,F(rgl,0x10),offset));
        }
        private ulong Pointer(ulong address)
        {var p=U(_memory.Read(address,8),0);if(p==0)throw new InvalidDataException("Runtime terrain pointer is absent");return p;}
        private static int I(byte[] b,int offset)=>BitConverter.ToInt32(b,offset);
        private static ulong U(byte[] b,int offset)=>BitConverter.ToUInt64(b,offset);
        private static float F(byte[] b,int offset)=>BitConverter.ToSingle(b,offset);
    }
}

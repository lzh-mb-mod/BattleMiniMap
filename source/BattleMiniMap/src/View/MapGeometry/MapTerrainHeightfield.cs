using System;

namespace BattleMiniMap.View.MapGeometry
{
    // An immutable snapshot of PhysX S16_TM samples, not a GPU terrain mesh.
    internal sealed class MapTerrainHeightfield
    {
        private readonly byte[] _samples;
        public readonly int Rows, Columns;
        public readonly float OriginX,OriginY,Step,HeightScale,HeightOffset;
        public int SampleCount => Rows*Columns;
        public MapTerrainHeightfield(int rows,int columns,byte[] samples,float x,float y,float step,float scale,float offset)
        {
            if(rows<2 || columns<2 || rows>4097 || columns>4097 || (long)rows*columns>4500000 || samples==null || samples.LongLength!=(long)rows*columns*4)
                throw new ArgumentException("Invalid raw heightfield dimensions or byte count");
            if(!Finite(x)||!Finite(y)||!Finite(step)||!Finite(scale)||!Finite(offset)||step<=0||step>100000||scale<=0||scale>10000)
                throw new ArgumentException("Invalid raw heightfield transform");
            if(!Finite(x+(columns-1)*step)||!Finite(y+(rows-1)*step)||Math.Abs(x+(columns-1)*step)>1000000||Math.Abs(y+(rows-1)*step)>1000000||
                Math.Abs(offset)+32768d*scale>1000000)
                throw new ArgumentException("Raw heightfield world range is unreasonable");
            Rows=rows;Columns=columns;_samples=samples;OriginX=x;OriginY=y;Step=step;HeightScale=scale;HeightOffset=offset;
        }
        private static bool Finite(float n)=>MapLiveGeometryRules.Finite(n);
        public GeometryPoint Vertex(int index)
        {
            if(index<0||index>=SampleCount)throw new ArgumentOutOfRangeException(nameof(index));
            var raw=(short)(_samples[index*4]|(_samples[index*4+1]<<8));
            return new GeometryPoint(OriginX+(index%Columns)*Step,OriginY+(index/Columns)*Step,raw*HeightScale+HeightOffset);
        }
        // Retains the original per-cell diagonal and each triangle's hole bit.
        internal bool Hole(int row,int col,int half)=>(_samples[(row*Columns+col)*4+2+half]&127)==127;
        internal bool AlternateDiagonal(int row,int col)=>(_samples[(row*Columns+col)*4+2]&128)!=0;
        public bool Triangle(int row,int col,int half,out int a,out int b,out int c)
        {
            a=b=c=0;
            if(row<0||col<0||row>=Rows-1||col>=Columns-1||(half!=0&&half!=1))return false;
            var cell=row*Columns+col;
            if((_samples[cell*4+2+half]&127)==127)return false;
            var diagonal=(_samples[cell*4+2]&128)!=0;
            var right=cell+1;var below=cell+Columns;var opposite=below+1;
            if(diagonal)
            {
                if(half==0){a=below;b=cell;c=opposite;}
                else {a=right;b=opposite;c=cell;}
            }
            else
            {
                if(half==0){a=cell;b=right;c=below;}
                else {a=opposite;b=below;c=right;}
            }
            return true;
        }
    }
}

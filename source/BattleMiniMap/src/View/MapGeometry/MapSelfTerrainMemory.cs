using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapSelfTerrainMemory : IMapTerrainMemory
    {
        public ulong NativeBase { get; private set; }
        public ulong PhysxBase { get; private set; }
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr process,IntPtr address,[Out] byte[] buffer,UIntPtr size,out UIntPtr read);
        public MapSelfTerrainMemory()
        {
            if(IntPtr.Size!=8 || !BitConverter.IsLittleEndian)throw new InvalidOperationException("Raw terrain requires verified x64 layout");
            using(var process=Process.GetCurrentProcess())
            foreach(ProcessModule module in process.Modules)
            {
                if(string.Equals(module.ModuleName,"TaleWorlds.Native.dll",StringComparison.OrdinalIgnoreCase))
                {
                    if(MapGeometryData.HashFile(module.FileName)!="27df81f6a94555bed79d9bd9a61bf523b64066e5fdb0dd9ac69d9eded20bbc26")
                        throw new InvalidOperationException("Unverified terrain engine build");
                    NativeBase=unchecked((ulong)module.BaseAddress.ToInt64());
                }
                if(string.Equals(module.ModuleName,"PhysXCommon_64.dll",StringComparison.OrdinalIgnoreCase))
                {
                    if(MapGeometryData.HashFile(module.FileName)!="0ba29c719dcb0934fc4248af1f2fece794d11c293ee1b389a148b04962c692c1")
                        throw new InvalidOperationException("Unverified terrain PhysX build");
                    PhysxBase=unchecked((ulong)module.BaseAddress.ToInt64());
                }
            }
            if(NativeBase==0 || PhysxBase==0)throw new InvalidOperationException("Runtime terrain modules unavailable");
        }
        public byte[] Read(ulong address,int size)
            => ReadCurrentProcess(address,size);
        internal static byte[] ReadCurrentProcess(ulong address,int size)
        {
            if(address<0x10000 || address>long.MaxValue || size<=0 || size>18000000 || address+(ulong)size<address)
                throw new InvalidDataException("Invalid raw terrain read range");
            var buffer=new byte[size];
            // -1 is the current-process pseudo handle: never opens or reads another process.
            if(!ReadProcessMemory(new IntPtr(-1),new IntPtr((long)address),buffer,new UIntPtr((uint)size),out var count)||count.ToUInt64()!=(ulong)size)
                throw new InvalidDataException("Runtime terrain memory unavailable ("+Marshal.GetLastWin32Error()+")");
            return buffer;
        }
    }
}

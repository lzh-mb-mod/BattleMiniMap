using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Threading;
using System.Security.Cryptography;

namespace BattleMiniMap.View.MapGeometry
{
    internal sealed class MapSelfTerrainMemory : IMapTerrainMemory
    {
        public ulong NativeBase { get; private set; }
        public ulong PhysxBase { get; private set; }
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(IntPtr process,IntPtr address,[Out] byte[] buffer,UIntPtr size,out UIntPtr read);
        internal sealed class ModuleIdentity
        {
            public readonly string Path;public readonly ulong Base;public readonly int Size;
            public ModuleIdentity(string path,ulong address,int size){Path=path;Base=address;Size=size;}
            public bool Matches(ModuleIdentity other)=>other!=null&&Base==other.Base&&Size==other.Size&&string.Equals(Path,other.Path,StringComparison.OrdinalIgnoreCase);
        }
        public MapTerrainVerification Verification { get; private set; }
        private MapSelfTerrainMemory(ModuleIdentity native,ModuleIdentity physx)
        { NativeBase=native.Base;PhysxBase=physx.Base; }
        private static ModuleIdentity[] LoadedModules()
        {
            var result=new ModuleIdentity[2];
            using(var process=Process.GetCurrentProcess())
            foreach(ProcessModule module in process.Modules)
            {
                var index=string.Equals(module.ModuleName,"TaleWorlds.Native.dll",StringComparison.OrdinalIgnoreCase)?0:
                    string.Equals(module.ModuleName,"PhysXCommon_64.dll",StringComparison.OrdinalIgnoreCase)?1:-1;
                if(index>=0)result[index]=new ModuleIdentity(module.FileName,unchecked((ulong)module.BaseAddress.ToInt64()),module.ModuleMemorySize);
            }
            if(result[0]==null||result[1]==null)throw new InvalidOperationException("Runtime terrain modules unavailable");
            return result;
        }
        internal static void VerifyFile(string path,string expected,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,FileOptions.SequentialScan))
            using(var hash=SHA256.Create())
            {
                var buffer=new byte[65536];int count;
                while((count=file.Read(buffer,0,buffer.Length))!=0)
                {token.ThrowIfCancellationRequested();hash.TransformBlock(buffer,0,count,buffer,0);}
                token.ThrowIfCancellationRequested();hash.TransformFinalBlock(new byte[0],0,0);
                var actual=BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant();
                if(actual!=expected)throw new InvalidDataException("Unverified runtime terrain module: "+System.IO.Path.GetFileName(path));
            }
        }
        public static IEnumerable<int> CreateSteps(Action<IMapTerrainMemory> complete)
        {
            if(IntPtr.Size!=8||!BitConverter.IsLittleEndian)throw new InvalidOperationException("Raw terrain requires verified x64 layout");
            var modules=LoadedModules();var verification=new MapTerrainVerification();IMapTerrainMemory verified=null;
            foreach(var step in verification.Run(token=>
            {
                VerifyFile(modules[0].Path,"27df81f6a94555bed79d9bd9a61bf523b64066e5fdb0dd9ac69d9eded20bbc26",token);
                VerifyFile(modules[1].Path,"0ba29c719dcb0934fc4248af1f2fece794d11c293ee1b389a148b04962c692c1",token);
                return new MapSelfTerrainMemory(modules[0],modules[1]){Verification=verification};
            },value=>verified=value))yield return step;
            // Executed back on the owning thread, after file verification and before memory reads.
            var current=LoadedModules();
            if(!modules[0].Matches(current[0])||!modules[1].Matches(current[1]))throw new InvalidDataException("Runtime terrain modules changed during verification");
            complete(verified);
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

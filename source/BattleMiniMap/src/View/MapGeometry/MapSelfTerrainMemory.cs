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
        public static IEnumerable<int> CreateSteps(Action<IMapTerrainMemory> complete,Action<string> phase=null,Action<MapTerrainVerification> started=null)
        {
            if(IntPtr.Size!=8||!BitConverter.IsLittleEndian)throw new InvalidOperationException("Raw terrain requires verified x64 layout");
            var verification=new MapTerrainVerification();started?.Invoke(verification);
            phase?.Invoke("terrain-module-discovery");var discovery=Stopwatch.StartNew();
            using(var modules=MapTerrainModuleLease.Pair.Acquire())
            {
                verification.DiscoveryMs=discovery.Elapsed.TotalMilliseconds;
                var native=modules.Native.Identity;var physx=modules.Physx.Identity;
                yield return 0;
                IMapTerrainMemory verified=null;phase?.Invoke("terrain-verification-dispatch");
                foreach(var step in verification.Run(token=>
                {
                    VerifyFile(native.Path,"27df81f6a94555bed79d9bd9a61bf523b64066e5fdb0dd9ac69d9eded20bbc26",token);
                    VerifyFile(physx.Path,"0ba29c719dcb0934fc4248af1f2fece794d11c293ee1b389a148b04962c692c1",token);
                    return new MapSelfTerrainMemory(native,physx){Verification=verification};
                },value=>verified=value))
                {yield return step;phase?.Invoke("terrain-verification-wait");}
                // Only the owner calls Windows module APIs; worker uses immutable file names.
                phase?.Invoke("terrain-module-recheck");var recheck=Stopwatch.StartNew();
                using(var current=MapTerrainModuleLease.Pair.Acquire())
                    if(!native.Matches(current.Native.Identity)||!physx.Matches(current.Physx.Identity))
                        throw new InvalidDataException("Runtime terrain modules changed during verification");
                verification.RecheckMs=recheck.Elapsed.TotalMilliseconds;
                yield return 0;
                complete(verified);
            }
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

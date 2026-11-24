using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace BattleMiniMap.View.MapGeometry
{
    // References only already-loaded modules. No LoadLibrary, no permanent PIN flag.
    internal sealed class MapTerrainModuleLease : IDisposable
    {
        private sealed class ModuleHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public ModuleHandle():base(true) { }
            protected override bool ReleaseHandle()=>FreeLibrary(handle);
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ModuleInfo { public IntPtr Base;public uint Size;public IntPtr Entry; }
        [DllImport("kernel32.dll",EntryPoint="GetModuleHandleExW",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetModuleHandleEx(uint flags,string name,out ModuleHandle module);
        [DllImport("kernel32.dll",EntryPoint="GetModuleFileNameW",CharSet=CharSet.Unicode,ExactSpelling=true,SetLastError=true)]
        private static extern uint GetModuleFileName(ModuleHandle module,StringBuilder path,uint size);
        [DllImport("kernel32.dll",EntryPoint="K32GetModuleInformation",ExactSpelling=true,SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetModuleInformation(IntPtr process,ModuleHandle module,out ModuleInfo info,uint size);
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr module);
        private readonly ModuleHandle _handle;
        public readonly MapSelfTerrainMemory.ModuleIdentity Identity;
        public bool IsClosed=>_handle.IsClosed;
        private MapTerrainModuleLease(ModuleHandle handle,MapSelfTerrainMemory.ModuleIdentity identity)
        { _handle=handle;Identity=identity; }
        public static MapTerrainModuleLease Acquire(string name)
        {
            if(!GetModuleHandleEx(0,name,out var handle))
            {var error=Marshal.GetLastWin32Error();handle?.Dispose();throw new InvalidDataException("Runtime module unavailable: "+name+" ("+error+")");}
            try
            {
                var capacity=512;string path;
                while(true)
                {
                    var buffer=new StringBuilder(capacity);var length=GetModuleFileName(handle,buffer,(uint)capacity);
                    if(length==0)throw new InvalidDataException("Cannot read runtime module path ("+Marshal.GetLastWin32Error()+")");
                    if(length<capacity){path=buffer.ToString();break;}
                    if(capacity>=32768)throw new InvalidDataException("Truncated runtime module path");
                    capacity=Math.Min(32768,capacity*2);
                }
                if(!GetModuleInformation(new IntPtr(-1),handle,out var info,(uint)Marshal.SizeOf(typeof(ModuleInfo))))
                    throw new InvalidDataException("Cannot read runtime module information ("+Marshal.GetLastWin32Error()+")");
                if(info.Base==IntPtr.Zero||info.Size==0||info.Size>int.MaxValue||info.Base!=handle.DangerousGetHandle())
                    throw new InvalidDataException("Invalid runtime module identity");
                return new MapTerrainModuleLease(handle,new MapSelfTerrainMemory.ModuleIdentity(path,unchecked((ulong)info.Base.ToInt64()),(int)info.Size));
            }
            catch {handle.Dispose();throw;}
        }
        public void Dispose()=>_handle.Dispose();
        internal sealed class Pair : IDisposable
        {
            public readonly MapTerrainModuleLease Native,Physx;
            private Pair(MapTerrainModuleLease native,MapTerrainModuleLease physx){Native=native;Physx=physx;}
            public static Pair Acquire(string native="TaleWorlds.Native.dll",string physx="PhysXCommon_64.dll",Func<string,MapTerrainModuleLease> acquire=null)
            {
                acquire=acquire??MapTerrainModuleLease.Acquire;
                var first=acquire(native);
                try{return new Pair(first,acquire(physx));}
                catch{first.Dispose();throw;}
            }
            public void Dispose(){Physx.Dispose();Native.Dispose();}
        }
    }
}

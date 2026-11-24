using System;
using System.IO;
using Path = System.IO.Path;
using System.Runtime.InteropServices;
using TaleWorlds.Engine;

namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapLiveCollisionGuard
    {
        // This field is NOT a public ABI. Never read it on an unverified engine.
        private const string NativeSha256="27df81f6a94555bed79d9bd9a61bf523b64066e5fdb0dd9ac69d9eded20bbc26";
        private static bool _verified;
        public static void VerifyEngine()
        {
            if (_verified) return;
            var path=Path.Combine(Path.GetDirectoryName(typeof(Scene).Assembly.Location),"TaleWorlds.Native.dll");
            if (MapGeometryData.HashFile(path)!=NativeSha256)
                throw new InvalidOperationException("Collision reader requires the verified engine build. Render source remains available.");
            _verified=true;
        }
        public static bool StorageReady(PhysicsShape shape)
        {
            if (!_verified) throw new InvalidOperationException("Collision storage gate was not verified");
            return shape!=null && shape.IsValid && Marshal.ReadIntPtr(new IntPtr(unchecked((long)shape.Pointer.ToUInt64())),0xB8)!=IntPtr.Zero;
        }
    }
}

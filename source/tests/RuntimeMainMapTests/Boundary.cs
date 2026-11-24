using System;
using TaleWorlds.Engine;
namespace BattleMiniMap.View.MapGeometry
{
    // The real version-pinned native memory check is separately reviewed, not executed here.
    static class MapLiveCollisionGuard
    {
        public static bool Reject;
        public static void VerifyEngine(){if(Reject)throw new Exception("fake unverified engine");}
        public static bool StorageReady(PhysicsShape shape)=>shape.Ready;
    }
}
namespace TaleWorlds.Library
{
    public struct Mat3
    {
        public Vec3 s,f,u;
        public Vec3 Transform(Vec3 p)=>s*p.x+f*p.y+u*p.z;
    }
    public struct MatrixFrame
    {
        public Vec3 origin; public Mat3 rotation;
        public static MatrixFrame Identity=>new MatrixFrame{rotation=new Mat3{s=new Vec3(1,0,0),f=new Vec3(0,1,0),u=new Vec3(0,0,1)}};
        public static MatrixFrame operator *(MatrixFrame a,MatrixFrame b)=>new MatrixFrame{origin=a.origin+a.rotation.Transform(b.origin),rotation=new Mat3{s=a.rotation.Transform(b.rotation.s),f=a.rotation.Transform(b.rotation.f),u=a.rotation.Transform(b.rotation.u)}};
    }
}

namespace TaleWorlds.Library { public static class BasePath { public static string Name = "Z:/runtime-main-tests-no-cache"; } }
namespace TaleWorlds.MountAndBlade { public static class MBCommon { public static bool IsPaused; } public class MissionState { public static MissionState Current; public bool Paused; } }
namespace BattleMiniMap { public static class BattleMiniMapSubModule { public const string ModuleId="BattleMiniMap"; } }
namespace BattleMiniMap.View.MapTerrain {
    public interface IMiniMap { bool IsValid {get;} int BitmapWidth {get;} int BitmapHeight {get;} float Resolution {get;} TaleWorlds.Library.Vec2 MapBoundMin {get;} TaleWorlds.Library.Vec2 MapBoundMax {get;} }
}

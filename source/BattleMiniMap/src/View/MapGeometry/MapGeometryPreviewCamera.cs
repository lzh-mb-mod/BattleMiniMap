using System;

namespace BattleMiniMap.View.MapGeometry
{
    // Pure orbit state, in normalized bridge coordinates; independent of the mission camera.
    internal sealed class MapGeometryPreviewCamera
    {
        public float Yaw { get; private set; }
        public float Pitch { get; private set; }
        public float Distance { get; private set; }
        public GeometryPoint Target { get; private set; }
        public MapGeometryPreviewCamera() { Reset(); }
        public void Reset() { Yaw = (float)Math.PI / 4; Pitch = 35 * (float)Math.PI / 180; Distance = 4.5f; Target = new GeometryPoint(0,0,0); }
        private static bool Finite(float x) => !float.IsNaN(x) && !float.IsInfinity(x);
        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
        public void Orbit(float dx, float dy)
        {
            if (!Finite(dx) || !Finite(dy)) return;
            Yaw = (Yaw + Clamp(dx,-2000,2000) * .006f) % (2 * (float)Math.PI);
            Pitch = Clamp(Pitch + Clamp(dy,-2000,2000) * .006f, 15*(float)Math.PI/180, 80*(float)Math.PI/180);
        }
        public GeometryPoint Right => new GeometryPoint(-(float)Math.Sin(Yaw), (float)Math.Cos(Yaw), 0);
        public GeometryPoint Up => new GeometryPoint(-(float)(Math.Cos(Yaw)*Math.Sin(Pitch)), -(float)(Math.Sin(Yaw)*Math.Sin(Pitch)), (float)Math.Cos(Pitch));
        public GeometryPoint Eye => new GeometryPoint(Target.X + Distance*(float)(Math.Cos(Pitch)*Math.Cos(Yaw)),
            Target.Y + Distance*(float)(Math.Cos(Pitch)*Math.Sin(Yaw)), Target.Z + Distance*(float)Math.Sin(Pitch));
        public void Pan(float dx, float dy, float viewportHeight)
        {
            if (!Finite(dx) || !Finite(dy) || !Finite(viewportHeight) || viewportHeight <= 0) return;
            dx=Clamp(dx,-2000,2000); dy=Clamp(dy,-2000,2000);
            var scale = Distance * 2 * (float)Math.Tan(Math.PI/8) / viewportHeight;
            var right = Right; var up = Up;
            Target = new GeometryPoint(Clamp(Target.X+(-dx*right.X+dy*up.X)*scale,-3,3),
                Clamp(Target.Y+(-dx*right.Y+dy*up.Y)*scale,-3,3), Clamp(Target.Z+dy*up.Z*scale,-3,3));
        }
        public void Zoom(float direction)
        {
            if (Finite(direction)) Distance = Clamp(Distance*(float)Math.Exp(-Clamp(direction,-10,10)*.15f),1.8f,16);
        }
    }
}

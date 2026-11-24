using System;

namespace BattleMiniMap.View.MapGeometry
{
    // Physical pixels, shared by the actual draw path and pointer/camera calculations.
    internal readonly struct MapPreviewLayout
    {
        public const float HeaderHeight = 64, FooterHeight = 60;
        public readonly float Width, Top, Height;
        public MapPreviewLayout(float width, float height, float scale)
        {
            scale = Math.Max(.01f, scale);
            Width = Math.Max(0, width);
            Top = Math.Min(Math.Max(0, height), HeaderHeight * scale);
            Height = Math.Max(0, height - Top - FooterHeight * scale);
        }
        public bool Contains(float x, float y) => Width > 0 && Height > 0 && x >= 0 && x < Width && y >= Top && y < Top + Height;
    }
}

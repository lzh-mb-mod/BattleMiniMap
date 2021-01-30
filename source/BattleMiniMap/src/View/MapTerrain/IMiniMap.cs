using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.MapTerrain
{
    public interface IMiniMap
    {
        public Texture MapTexture { get; }
        public int BitmapWidth { get; }
        public int BitmapHeight { get; }

        public Vec2 MapBoundMin { get; }
        public Vec2 MapBoundMax { get; }
        public float Resolution { get; }
        public float EdgeOpacityFactor { get; }
        bool IsEnabled { get; }
        void UpdateMapImage(Mission mission);
        void UpdateEdgeOpacity();
    }

    public static class MiniMap
    {
        private static IMiniMap _instance;
        public static IMiniMap Instance => _instance ??= new GdiMiniMap();
    }
}
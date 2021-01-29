using System.Drawing;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Library;

namespace BattleMiniMap
{
    public static class Utility
    {
        public static int WorldToMap(float actualPos, float resolution, float actualStart)
        {
            return (int)((actualPos - actualStart) / resolution);
        }
        public static float WorldToMapF(float actualPos, float resolution, float actualStart)
        {
            return (actualPos - actualStart) / resolution;
        }

        public static float MapToWorld(int mapPos, float resolution, float actualStart)
        {
            return mapPos * resolution + actualStart;
        }

        public static float MapFToWorld(float mapPos, float resolution, float actualStart)
        {
            return mapPos * resolution + actualStart;
        }

        public static Point WorldToMap(this IMiniMap miniMap, Vec2 position)
        {
            return new Point(WorldToMap(position.y, miniMap.Resolution, miniMap.MapBoundMin.y),
                WorldToMap(position.x, miniMap.Resolution, miniMap.MapBoundMin.x));
        }

        public static Vec2 WorldToMapF(this IMiniMap miniMap, Vec2 position)
        {
            return new Vec2(WorldToMapF(position.y, miniMap.Resolution, miniMap.MapBoundMin.y),
                WorldToMapF(position.x, miniMap.Resolution, miniMap.MapBoundMin.x));
        }

        public static Vec2 MapToWorld(this IMiniMap miniMap, Point point)
        {
            return new Vec2(MapToWorld(point.Y, miniMap.Resolution, miniMap.MapBoundMin.x),
                MapToWorld(point.X, miniMap.Resolution, miniMap.MapBoundMin.y));
        }

        public static Vec2 MapFToWorld(this IMiniMap miniMap, Vec2 point)
        {
            return new Vec2(MapFToWorld(point.y, miniMap.Resolution, miniMap.MapBoundMin.x),
                MapFToWorld(point.x, miniMap.Resolution, miniMap.MapBoundMin.y));
        }

        public static Vec2 MapToWidget(this IMiniMap miniMap, Vec2 p)
        {
            var config = BattleMiniMapConfig.Get();
            var scale = (float)config.WidgetWidth / miniMap.BitmapWidth;
            return new Vec2((p.X * scale), (p.Y * scale));
        }

        public static Vec2 WidgetToMap(this IMiniMap miniMap, Vec2 p)
        {
            var config = BattleMiniMapConfig.Get();
            var scale = (float)config.WidgetWidth / miniMap.BitmapWidth;
            return new Vec2(p.X / scale, p.Y / scale);
        }
    }
}

using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System.Drawing;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

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

        public static float MapFToWidget(this IMiniMap miniMap, float f)
        {
            var config = BattleMiniMapConfig.Get();
            var scale = (float)config.WidgetWidth / miniMap.BitmapWidth;
            return f * scale;
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

        public static Vec2 WorldToWidget(this IMiniMap miniMap, Vec2 p)
        {
            var config = BattleMiniMapConfig.Get();
            if (config.FollowMode)
            {
                var camera = (MissionState.Current.Listener as MissionScreen).CombatCamera;
                var position = camera.Position.AsVec2;
                var direction = camera.Direction.AsVec2.Normalized();
                var relativePosition = direction.TransformToLocalUnitF(p - position);
                return new Vec2(relativePosition.x, -relativePosition.y) * config.GetFollowModeScale() / 100f * config.WidgetWidth + new Vec2(config.WidgetWidth / 2f, config.WidgetWidth / 2f);
            }
            else
            {
                return MapToWidget(miniMap, miniMap.WorldToMapF(p));
            }
        }

        public static Vec2 WidgetToWorld(this IMiniMap miniMap, Vec2 p)
        {
            var config = BattleMiniMapConfig.Get();
            if (config.FollowMode)
            {
                p -= new Vec2(config.WidgetWidth / 2f, config.WidgetWidth / 2f);
                p = p * 100f / config.GetFollowModeScale() / config.WidgetWidth;
                var relativePosition = new Vec2(p.x, -p.y);

                var camera = (MissionState.Current.Listener as MissionScreen).CombatCamera;
                var position = camera.Position.AsVec2;
                var direction = camera.Direction.AsVec2.Normalized();
                return direction.TransformToParentUnitF(relativePosition) + position;
            }
            else
            {
                return MapFToWorld(miniMap, miniMap.WidgetToMap(p));
            }
        }
    }
}

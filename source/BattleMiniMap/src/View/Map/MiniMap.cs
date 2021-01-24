using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.Map
{
    public class MiniMap
    {
        public static MiniMap Instance { get; set; }
        public ImageRGBA MapImage { get; private set; }

        public Texture MapTexture { get; private set; }

        public Vec2 MapBoundMin { get; private set; }
        public Vec2 MapBoundMax { get; private set; }
        public float Resolution { get; private set; }

        public bool IsEnabled { get; private set; }

        public void UpdateMapImage(Mission mission)
        {
            var scene = mission.Scene;
            var boundaries = mission.Boundaries;
            Vec2 boundMin = new Vec2(float.MaxValue, float.MaxValue);
            Vec2 boundMax = new Vec2(float.MinValue, float.MinValue);
            foreach (var boundary in boundaries)
            {
                foreach (var vec2 in boundary.Value)
                {
                    boundMin.x = Math.Min(boundMin.x, vec2.x);
                    boundMin.y = Math.Min(boundMin.y, vec2.y);
                    boundMax.x = Math.Max(boundMax.x, vec2.x);
                    boundMax.y = Math.Max(boundMax.y, vec2.y);
                }
            }

            if (boundaries.Count == 0)
            {
                MapBoundMin = new Vec2(0, 0);
                MapBoundMax = new Vec2(0, 0);
                MapImage = new ImageRGBA(1, 1);
                MapTexture = null;
                IsEnabled = false;
            }
            MapBoundMin = boundMin + new Vec2(-50f, -50f);
            MapBoundMax = boundMax + new Vec2(50f, 50f);


            Resolution = BattleMiniMapConfig.Get().Resolution;
            if (Resolution == 0)
                Resolution = 1;
            int mapWidth = (int) Math.Abs((MapBoundMax.y - MapBoundMin.y) / Resolution) + 1;
            int mapHeight = (int) Math.Abs((MapBoundMax.x - MapBoundMin.x) / Resolution) + 1;
            ImageRGBA newImage = new ImageRGBA(mapWidth, mapHeight);
            SampleTerrainHeight(scene, newImage, mapWidth, mapHeight, MapBoundMin.x, MapBoundMin.y, Resolution);
            SampleBoundaries(mission, newImage, mapWidth, mapHeight, MapBoundMin.x, MapBoundMin.y, Resolution);

            MapImage = newImage;
            MapTexture = MapImage.CreateTexture();
            IsEnabled = true;
        }

        private static void SampleTerrainHeight(Scene scene, ImageRGBA image, float mapWidth, float mapHeight,
            float xStart, float yStart, float resolution)
        {
            int minR = 92, minG = 77, minB = 10;
            int maxR = 200, maxG = 240, maxB = 180;
            //scene.GetTerrainMinMaxHeight(out var minHeight, out var maxHeight);
            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;
            for (int w = 0; w < mapWidth; w++)
            {
                for (int h = 0; h < mapHeight; h++)
                {
                    var x = MapToActual(h, resolution, xStart);
                    var y = MapToActual(w, resolution, yStart);
                    var terrainHeight = scene.GetTerrainHeight(new Vec2(x, y));
                    minHeight = Math.Min(minHeight, terrainHeight);
                    maxHeight = Math.Max(maxHeight, terrainHeight);
                }
            }
            for (int w = 0; w < mapWidth; w++)
            {
                for (int h = 0; h < mapHeight; h++)
                {
                    var x = MapToActual(h, resolution, xStart);
                    var y = MapToActual(w, resolution, yStart);
                    var terrainHeight = scene.GetTerrainHeight(new Vec2(x, y));
                    var factor = (terrainHeight - minHeight) / (Math.Abs(maxHeight - minHeight) + 1);
                    image.SetRGBA(w, h,
                        (byte)(factor * (maxR - minR) + minR),
                        (byte)(factor * (maxG - minG) + minG),
                        (byte)(factor * (maxB - minB) + minB),
                        255);
                }
            }
        }

        private static void SampleBoundaries(Mission mission, ImageRGBA image, float mapWidth, float mapHeight,
            float xStart, float yStart, float resolution)
        {
            foreach (var boundary in mission.Boundaries)
            {
                Vec2 previousPos = Vec2.Invalid;
                Vec2 firstPos = Vec2.Invalid;
                int previousPointH = 0, previousPointW = 0;
                foreach (var vec2 in boundary.Value)
                {
                    if (!previousPos.IsValid)
                    {
                        previousPos = vec2;
                        firstPos = vec2;
                        previousPointH = ActualToMap(previousPos.x, resolution, xStart);
                        previousPointW = ActualToMap(previousPos.y, resolution, yStart);
                        continue;
                    }

                    var currentPointH = ActualToMap(vec2.x, resolution, xStart);
                    var currentPointW = ActualToMap(vec2.y, resolution, yStart);
                    image.DrawLine(previousPointW, previousPointH, currentPointW, currentPointH, 179, 30, 9, 255);
                    previousPos = vec2;
                    previousPointH = currentPointH;
                    previousPointW = currentPointW;
                }

                var finalPointH = ActualToMap(firstPos.x, resolution, xStart);
                var finalPointW = ActualToMap(firstPos.y, resolution, yStart);
                image.DrawLine(previousPointW, previousPointH, finalPointW, finalPointH, 179, 30, 9, 255);
            }
        }

        private static int ActualToMap(float actualPos, float resolution, float actualStart)
        {
            return (int)((actualPos - actualStart) / resolution);
        }

        private static float MapToActual(int mapPos, float resolution, float actualStart)
        {
            return mapPos * resolution + actualStart;
        }
    }
}

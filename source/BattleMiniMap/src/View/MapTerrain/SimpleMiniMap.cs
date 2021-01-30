using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapTerrain
{
    public class SimpleMiniMap : IMiniMap
    {
        public ImageRGBA MapImage { get; private set; }

        public Texture MapTexture { get; private set; }
        public int BitmapWidth { get; private set; }
        public int BitmapHeight { get; private set; }

        public Vec2 MapBoundMin { get; private set; }
        public Vec2 MapBoundMax { get; private set; }
        public float Resolution { get; private set; }
        public float EdgeOpacityFactor { get; set; }

        public bool IsEnabled { get; private set; }
        public bool ExcludeUnwalkableTerrain { get; private set; }

        public void InitializeMapRange(Mission mission, bool updateMap = false)
        {
            var boundaries = mission.Boundaries;
            var boundMin = new Vec2(float.MaxValue, float.MaxValue);
            var boundMax = new Vec2(float.MinValue, float.MinValue);
            foreach (var boundary in boundaries)
                foreach (var vec2 in boundary.Value)
                {
                    boundMin.x = Math.Min(boundMin.x, vec2.x);
                    boundMin.y = Math.Min(boundMin.y, vec2.y);
                    boundMax.x = Math.Max(boundMax.x, vec2.x);
                    boundMax.y = Math.Max(boundMax.y, vec2.y);
                }

            if (boundaries.Count == 0)
            {
                MapBoundMin = new Vec2(0, 0);
                MapBoundMax = new Vec2(0, 0);
                MapImage = new ImageRGBA(1, 1);
                BitmapWidth = BitmapHeight = 1;
                MapTexture = null;
                IsEnabled = false;
            }

            MapBoundMin = boundMin + new Vec2(-50f, -50f);
            MapBoundMax = boundMax + new Vec2(50f, 50f);

            if (updateMap)
                UpdateMapSize(mission, true);
        }

        public void UpdateMapSize(Mission mission, bool updateMap = false)
        {
            Resolution = BattleMiniMapConfig.Get().Resolution;
            if (Resolution == 0)
                Resolution = 1;
            var mapWidth = (int)Math.Abs((MapBoundMax.y - MapBoundMin.y) / Resolution) + 1;
            var mapHeight = (int)Math.Abs((MapBoundMax.x - MapBoundMin.x) / Resolution) + 1;
            BitmapWidth = mapWidth;
            BitmapHeight = mapHeight;

            if (updateMap)
                UpdateMapImage(mission);
        }

        public void UpdateMapImage(Mission mission)
        {
            var scene = mission.Scene;
            var newImage = new ImageRGBA(BitmapWidth, BitmapHeight);
            SampleTerrainHeight(scene, newImage, BitmapWidth, BitmapHeight, MapBoundMin.x, MapBoundMin.y, Resolution);
            SampleBoundaries(mission, newImage, BitmapWidth, BitmapHeight, MapBoundMin.x, MapBoundMin.y, Resolution);

            MapImage = newImage;
            MapTexture = MapImage.CreateTexture();
            IsEnabled = true;
        }

        public void UpdateEdgeOpacity()
        {
        }

        public int GetEdgeAlpha(int w, int h, float edgeOpacityFactor)
        {
            return 255;
        }

        private static void SampleTerrainHeight(Scene scene, ImageRGBA image, float mapWidth, float mapHeight,
            float xStart, float yStart, float resolution)
        {
            int minR = 92, minG = 77, minB = 10;
            int maxR = 200, maxG = 240, maxB = 180;
            //scene.GetTerrainMinMaxHeight(out var minHeight, out var maxHeight);
            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;
            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    var x = MapToActual(h, resolution, xStart);
                    var y = MapToActual(w, resolution, yStart);
                    var terrainHeight = scene.GetTerrainHeight(new Vec2(x, y));
                    minHeight = Math.Min(minHeight, terrainHeight);
                    maxHeight = Math.Max(maxHeight, terrainHeight);
                }

            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
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

        private static void SampleBoundaries(Mission mission, ImageRGBA image, float mapWidth, float mapHeight,
            float xStart, float yStart, float resolution)
        {
            foreach (var boundary in mission.Boundaries)
            {
                var previousPos = Vec2.Invalid;
                var firstPos = Vec2.Invalid;
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
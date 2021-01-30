using System;
using System.Drawing;
using System.Drawing.Imaging;
using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Color = System.Drawing.Color;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapTerrain
{
    public class GdiMiniMap : IMiniMap
    {
        public static ColorGenerator AboveWater = new ColorGenerator(new Color[]
            {
                Color.FromArgb(255, 0, 90, 0),
                Color.FromArgb(255, 0, 158, 0),
                Color.FromArgb(255, 0, 225, 0),
                Color.FromArgb(255, 105, 235, 60),
                Color.FromArgb(255, 165, 230, 90),
                Color.FromArgb(255, 180, 200, 100),
                Color.FromArgb(255, 250, 180, 0),
                Color.FromArgb(255, 202, 135, 0),
                Color.FromArgb(255, 187, 108, 0),
                Color.FromArgb(255, 169, 84, 0),
                Color.FromArgb(255, 145, 61, 40),
                Color.FromArgb(255, 125, 51, 90),
                Color.FromArgb(255, 125, 40, 120),
                Color.FromArgb(255, 145, 40, 130),
                Color.FromArgb(255, 165, 60, 150),
                Color.FromArgb(255, 190, 80, 180),
                Color.FromArgb(255, 220, 100, 200),
                Color.FromArgb(255, 250, 130, 240),
                Color.FromArgb(255, 255, 190, 255),
                Color.FromArgb(255, 255, 220, 255),
            });

        public static ColorGenerator BelowWater = new ColorGenerator(new Color[]
        {
            Color.FromArgb(255, 202, 248, 255),
            Color.FromArgb(255, 99, 248, 255),
            Color.FromArgb(255, 73, 182, 255),
            Color.FromArgb(255, 26, 125, 198),
            Color.FromArgb(255, 48, 96, 198),
        });
        public Bitmap MapImage { get; private set; }

        public Texture MapTexture { get; private set; }
        public int BitmapWidth { get; private set; }
        public int BitmapHeight { get; private set; }

        public Vec2 MapBoundMin { get; private set; }
        public Vec2 MapBoundMax { get; private set; }
        public float Resolution { get; private set; }
        public float EdgeOpacityFactor { get; set; }

        public bool IsEnabled { get; private set; }

        public event Action OnTextureSizeChanged;

        public void UpdateMapImage(Mission mission)
        {
            var scene = mission.Scene;
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
                MapImage = new Bitmap(1, 1);
                BitmapWidth = BitmapHeight = 1;
                MapTexture = null;
                IsEnabled = false;
                return;
            }

            MapBoundMin = boundMin + new Vec2(-50f, -50f);
            MapBoundMax = boundMax + new Vec2(50f, 50f);


            Resolution = BattleMiniMapConfig.Get().Resolution;
            if (Resolution == 0)
                Resolution = 1;
            EdgeOpacityFactor = BattleMiniMapConfig.Get().EdgeOpacityFactor;
            var mapWidth = (int)Math.Abs((MapBoundMax.y - MapBoundMin.y) / Resolution) + 1;
            var mapHeight = (int)Math.Abs((MapBoundMax.x - MapBoundMin.x) / Resolution) + 1;
            BitmapWidth = mapWidth;
            BitmapHeight = mapHeight;
            var bitmap = new Bitmap(mapWidth, mapHeight, PixelFormat.Format32bppArgb);

            SampleTerrainHeight(scene, bitmap, mapWidth, mapHeight);

            MapImage = bitmap;
            MapTexture = MapImage.CreateTexture();
            IsEnabled = true;

            OnTextureSizeChanged?.Invoke();
        }

        public void UpdateEdgeOpacity()
        {
            if (!IsEnabled)
                return;

            EdgeOpacityFactor = BattleMiniMapConfig.Get().EdgeOpacityFactor;
            for (var w = 0; w < BitmapWidth; w++)
            for (var h = 0; h < BitmapHeight; h++)
            {
                var color = MapImage.GetPixel(w, h);
                MapImage.SetPixel(w, h, Color.FromArgb(GetAlpha(BitmapWidth, BitmapHeight, w, h, EdgeOpacityFactor), color));
            }

            MapTexture = MapImage.CreateTexture();
        }

        private void SampleTerrainHeight(Scene scene, Bitmap image, float mapWidth, float mapHeight)
        {
            //int minR = 92, minG = 77, minB = 10;
            //int maxR = 200, maxG = 240, maxB = 180;
            // r = 255 * cos(height)
            // g = 255 * sin(height)
            // b = 255 * (1 - cos(height))
            //scene.GetTerrainMinMaxHeight(out var minHeight, out var maxHeight);
            var waterLevel = scene.GetWaterLevel();
            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;
            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    Vec2 position = this.MapToWorld(new Point(w, h));
                    var terrainHeight = scene.GetTerrainHeight(position);
                    minHeight = Math.Min(minHeight, terrainHeight);
                    maxHeight = Math.Max(maxHeight, terrainHeight);
                }

            if (waterLevel < minHeight)
                waterLevel = minHeight;

            var edgeOpacityFactor = BattleMiniMapConfig.Get().EdgeOpacityFactor;

            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    var pos = this.MapToWorld(new Point(w, h));
                    var terrainHeight = scene.GetTerrainHeight(pos);
                    if (scene.GetNavMeshFaceIndex(pos.ToVec3(terrainHeight), true) != -1)
                    {
                        SetPixel(image, BitmapWidth, BitmapHeight, w, h, terrainHeight, waterLevel, edgeOpacityFactor);

                        continue;
                    }
                    var groundHeight = waterLevel;
                    scene.GetHeightAtPoint(pos, BodyFlags.CommonCollisionExcludeFlags, ref groundHeight);
                    if (BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain && groundHeight >= waterLevel &&
                        Math.Abs(groundHeight - terrainHeight) < 0.1f &&
                        scene.GetNavMeshFaceIndex(pos.ToVec3(groundHeight), true) == -1)
                    {
                        image.SetPixel(w, h,
                            Color.FromArgb(0, 0, 0, 0));
                        continue;
                    }

                    SetPixel(image, BitmapWidth, BitmapHeight, w, h, groundHeight, waterLevel, edgeOpacityFactor);
                }
        }

        private void SetPixel(Bitmap image, int mapWidth, int mapHeight, int w, int h, float height, float waterLevel, float edgeOpacityFactor)
        {
            var color = height >= waterLevel
                ? AboveWater.GetColor((height - waterLevel) / 190)
                : BelowWater.GetColor((waterLevel - height) / 8);
            var x = Math.Abs(edgeOpacityFactor - 1) < 0.01f
                ? 0
                : MathF.Clamp((Math.Abs((float) w / mapWidth - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor),
                    0, 1);
            var y = Math.Abs(edgeOpacityFactor - 1) < 0.01f
                ? 0
                : MathF.Clamp(
                    (Math.Abs((float) h / mapHeight - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor), 0, 1);
            var alpha = MathF.Sqrt(Math.Max(1 - x * x - y * y, 0));
            image.SetPixel(w, h,
                Color.FromArgb(GetAlpha(mapWidth, mapHeight, w, h, edgeOpacityFactor), color));
        }

        private int GetAlpha(int mapWidth, int mapHeight, int w, int h, float edgeOpacityFactor)
        {
            var x = Math.Abs(edgeOpacityFactor - 1) < 0.01f
                ? 0
                : MathF.Clamp((Math.Abs((float)w / mapWidth - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor),
                    0, 1);
            var y = Math.Abs(edgeOpacityFactor - 1) < 0.01f
                ? 0
                : MathF.Clamp(
                    (Math.Abs((float)h / mapHeight - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor), 0, 1);
            return (int) (MathF.Sqrt(Math.Max(1 - x * x - y * y, 0)) * 255);
        }
    }
}
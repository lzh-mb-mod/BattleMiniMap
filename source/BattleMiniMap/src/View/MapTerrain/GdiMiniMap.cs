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
        public Bitmap MapImage { get; private set; }

        public Texture MapTexture { get; private set; }
        public int BitmapWidth { get; private set; }
        public int BitmapHeight { get; private set; }

        public Vec2 MapBoundMin { get; private set; }
        public Vec2 MapBoundMax { get; private set; }
        public float Resolution { get; private set; }

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

        private void SampleTerrainHeight(Scene scene, Bitmap image, float mapWidth, float mapHeight)
        {
            //int minR = 92, minG = 77, minB = 10;
            //int maxR = 200, maxG = 240, maxB = 180;
            // r = 255 * cos(height)
            // g = 255 * sin(height)
            // b = 255 * (1 - cos(height))
            //scene.GetTerrainMinMaxHeight(out var minHeight, out var maxHeight);
            var waterLevel = scene.GetWaterLevel();
            var aboveWater = new ColorGenerator(new Color[]
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
            var belowWater = new ColorGenerator(new Color[]
            {
                Color.FromArgb(255, 202, 248, 255),
                Color.FromArgb(255, 99, 248, 255),
                Color.FromArgb(255, 73, 182, 255),
                Color.FromArgb(255, 26, 125, 198),
                Color.FromArgb(255, 48, 96, 198),
            });
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

            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    var pos = this.MapToWorld(new Point(w, h));
                    var terrainHeight = scene.GetTerrainHeight(pos);
                    PathFaceRecord faceRecord = PathFaceRecord.NullFaceRecord;
                    scene.GetNavMeshFaceIndex(ref faceRecord, pos.ToVec3(terrainHeight), true);
                    if (faceRecord.IsValid())
                    {
                        image.SetPixel(w, h,
                            terrainHeight >= waterLevel
                                ? aboveWater.GetColor((terrainHeight - waterLevel) / 190)
                                : belowWater.GetColor((waterLevel - terrainHeight) / 8));
                        continue;
                    }
                    var groundHeight = waterLevel;
                    scene.GetHeightAtPoint(pos, BodyFlags.CommonCollisionExcludeFlags, ref groundHeight);
                    if (BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain && groundHeight >= waterLevel &&
                        Math.Abs(groundHeight - terrainHeight) < 0.1f)
                    {
                        scene.GetNavMeshFaceIndex(ref faceRecord, pos.ToVec3(terrainHeight), true);
                        if (!faceRecord.IsValid())
                        {
                            image.SetPixel(w, h,
                                Color.FromArgb(0, 0, 0, 0));
                            continue;
                        }
                    }

                    image.SetPixel(w, h,
                        groundHeight >= waterLevel
                                ? aboveWater.GetColor((groundHeight - waterLevel) / 190)
                                : belowWater.GetColor((waterLevel - groundHeight) / 8));
                }
        }
    }
}
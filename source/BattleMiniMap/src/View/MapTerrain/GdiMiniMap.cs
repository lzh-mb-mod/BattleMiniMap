using BattleMiniMap.Config;
using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain.ColorConfigs;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using TaleWorlds.Engine;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using Color = System.Drawing.Color;
using Texture = TaleWorlds.TwoDimension.Texture;

namespace BattleMiniMap.View.MapTerrain
{
    public class GdiMiniMap : IMiniMap
    {
        public static TerrainColorConfig ColorConfig = TerrainColorConfig.LowSaturation4;

        public static ColorGenerator AboveWater = ColorConfig.TerrainColor;

        public static ColorGenerator BelowWater = ColorConfig.WaterColor;

        private Texture _mapTexture;
        public Bitmap MapImage { get; private set; }

        public Texture MapTexture
        {
            get => _mapTexture;
            private set
            {
                (_mapTexture?.PlatformTexture as EngineTexture)?.Texture.Release();
                _mapTexture = value;
            }
        }

        public int BitmapWidth { get; private set; }
        public int BitmapHeight { get; private set; }

        public Vec2 MapBoundMin { get; private set; }
        public Vec2 MapBoundMax { get; private set; }
        public float WaterLevel { get; private set; }
        public float Resolution { get; private set; }
        public float EdgeOpacityFactor { get; private set; }

        public bool IsValid { get; private set; }
        public bool ExcludeUnwalkableTerrain { get; private set; }

        public event Action OnTextureSizeChanged;

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
                MapImage = new Bitmap(1, 1);
                BitmapWidth = BitmapHeight = 1;
                MapTexture = null;
                IsValid = false;
                return;
            }

            IsValid = true;
            MapBoundMin = boundMin + new Vec2(-50f, -50f);
            MapBoundMax = boundMax + new Vec2(50f, 50f);

            if (updateMap)
                UpdateMapSize(mission, true);
        }

        public void UpdateMapSize(Mission mission, bool updateMap = false)
        {
            if (!IsValid)
                return;

            Resolution = BattleMiniMapConfig.Get().Resolution;
            if (Resolution == 0)
                Resolution = 1;
            var mapWidth = (int)Math.Abs((MapBoundMax.y - MapBoundMin.y) / Resolution) + 1;
            var mapHeight = (int)Math.Abs((MapBoundMax.x - MapBoundMin.x) / Resolution) + 1;
            bool changed = BitmapWidth != mapWidth || BitmapHeight != mapHeight;

            BitmapWidth = mapWidth;
            BitmapHeight = mapHeight;
            if (changed)
                OnTextureSizeChanged?.Invoke();

            if (updateMap)
                UpdateMapImage(mission);
        }

        public void UpdateMapImage(Mission mission)
        {
            if (!IsValid)
                return;

            var scene = mission.Scene;
            var bitmap = new Bitmap(BitmapWidth, BitmapHeight, PixelFormat.Format32bppArgb);

            EdgeOpacityFactor = BattleMiniMapConfig.Get().EdgeOpacityFactor;
            ExcludeUnwalkableTerrain = BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain;
            SampleTerrainHeight(mission, bitmap, BitmapWidth, BitmapHeight);

            MapImage = bitmap;
            MapTexture = MapImage.CreateTexture();
        }

        public void UpdateEdgeOpacity()
        {
            if (!IsValid)
                return;

            EdgeOpacityFactor = BattleMiniMapConfig.Get().EdgeOpacityFactor;
            for (var w = 0; w < BitmapWidth; w++)
                for (var h = 0; h < BitmapHeight; h++)
                {
                    var color = MapImage.GetPixel(w, h);
                    if (color != Color.FromArgb(0, 0, 0, 0))
                        MapImage.SetPixel(w, h,
                            Color.FromArgb(GetEdgeAlpha(w, h, EdgeOpacityFactor), color.R,
                                color.G, color.B));
                }

            MapTexture = MapImage.CreateTexture();
        }

        private void SampleTerrainHeight(Mission mission, Bitmap image, float mapWidth, float mapHeight)
        {
            //int minR = 92, minG = 77, minB = 10;
            //int maxR = 200, maxG = 240, maxB = 180;
            // r = 255 * cos(height)
            // g = 255 * sin(height)
            // b = 255 * (1 - cos(height))
            //scene.GetTerrainMinMaxHeight(out var minHeight, out var maxHeight);
            var scene = mission.Scene;
            WaterLevel = scene.GetWaterLevel();
            var minHeight = float.MaxValue;
            var maxHeight = float.MinValue;
            var minHeightInsideBoundary = float.MaxValue;
            var maxHeightInsideBoundary = float.MinValue;
            var minGroundHeight = float.MaxValue;
            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    Vec2 position = this.MapToWorld(new Point(w, h));
                    var terrainHeight = scene.GetTerrainHeight(position);
                    float groundHeight = float.MinValue;
                    scene.GetHeightAtPoint(position, BodyFlags.CommonCollisionExcludeFlags, ref groundHeight);
                    minGroundHeight = Math.Min(minGroundHeight, groundHeight);
                    minHeight = Math.Min(minHeight, terrainHeight);
                    maxHeight = Math.Max(maxHeight, terrainHeight);
                    if (mission.IsPositionInsideBoundaries(position))
                    {
                        minHeightInsideBoundary = Math.Min(minHeightInsideBoundary, terrainHeight);
                        maxHeightInsideBoundary = Math.Max(maxHeightInsideBoundary, terrainHeight);
                    }
                }

            if (WaterLevel == 0 && minGroundHeight == 2)
            {
                WaterLevel = minGroundHeight;
            }

            if (WaterLevel < minHeight)
                WaterLevel = minHeight;

            maxHeight = Math.Min(maxHeight, 120 + WaterLevel);

            for (var w = 0; w < mapWidth; w++)
                for (var h = 0; h < mapHeight; h++)
                {
                    var pos = this.MapToWorld(new Point(w, h));
                    var terrainHeight = scene.GetTerrainHeight(pos);
                    PathFaceRecord faceRecord = PathFaceRecord.NullFaceRecord;
                    scene.GetNavMeshFaceIndex(ref faceRecord, pos.ToVec3(terrainHeight), true);
                    var groundHeight = terrainHeight;
                    if (faceRecord.IsValid() || !scene.GetHeightAtPoint(pos, BodyFlags.CommonCollisionExcludeFlags, ref groundHeight))
                    {
                        SetPixel(image, w, h, terrainHeight, WaterLevel, maxHeight, minHeightInsideBoundary, maxHeightInsideBoundary, EdgeOpacityFactor);

                        continue;
                    }
                    if (ExcludeUnwalkableTerrain && groundHeight >= WaterLevel &&
                        Math.Abs(groundHeight - terrainHeight) < 0.5f)
                    {
                        scene.GetNavMeshFaceIndex(ref faceRecord, pos.ToVec3(terrainHeight), true);
                        if (!faceRecord.IsValid())
                        {
                            image.SetPixel(w, h,
                                Color.FromArgb(0, 0, 0, 0));
                            continue;
                        }
                    }

                    SetPixel(image, w, h, groundHeight, WaterLevel, maxHeight, minHeightInsideBoundary, maxHeightInsideBoundary, EdgeOpacityFactor);
                }
        }

        private void SetPixel(Bitmap image, int w, int h, float groundHeight, float waterLevel, float maxHeight, float minHeightInsideBoundary, float maxHeightInsideBoundary, float edgeOpacityFactor)
        {
            Color color;
            float maxDepth = 8;
            if (IsAboveWater(waterLevel, groundHeight))
            {
                minHeightInsideBoundary = MathF.Max(minHeightInsideBoundary, waterLevel);
                maxHeightInsideBoundary =
                    maxHeightInsideBoundary < waterLevel ? maxHeight : maxHeightInsideBoundary;
                float partInsideBoundary =
                    (maxHeightInsideBoundary - minHeightInsideBoundary) / (maxHeight - waterLevel);
                float partOutsideBoundary = 1 - partInsideBoundary;
                float adjustedPartInsideBoundary = MathF.Sqrt(partInsideBoundary);
                float adjustedPartOutsideBoundary = 1 - adjustedPartInsideBoundary;
                float minHeightInsideBoundaryProgress =
                    (minHeightInsideBoundary - waterLevel) / (maxHeight - waterLevel);
                float maxHeightInsideBoundaryProgress =
                    (maxHeightInsideBoundary - waterLevel) / (maxHeight - waterLevel);
                float progress = (groundHeight - waterLevel) / (maxHeight - waterLevel);
                float adjustedMinHeightInsideBoundaryProgress = minHeightInsideBoundaryProgress *
                    adjustedPartOutsideBoundary / partOutsideBoundary;
                float adjustedMaxHeightInsideBoundaryProgress =
                    adjustedMinHeightInsideBoundaryProgress + adjustedPartInsideBoundary;
                float adjustedProgress = progress <= minHeightInsideBoundaryProgress
                    ? progress * adjustedPartOutsideBoundary / partOutsideBoundary
                    : progress <= maxHeightInsideBoundaryProgress
                        ? adjustedMinHeightInsideBoundaryProgress + (progress - minHeightInsideBoundaryProgress) *
                        adjustedPartInsideBoundary / partInsideBoundary
                        : adjustedMaxHeightInsideBoundaryProgress + (progress - maxHeightInsideBoundaryProgress) *
                        adjustedPartOutsideBoundary / partOutsideBoundary;
                color = AboveWater.GetColor(adjustedProgress);
            }
            else
            {
                color = BelowWater.GetColor((waterLevel - groundHeight) / 8);
            }
            image.SetPixel(w, h,
                Color.FromArgb(Math.Min(GetEdgeAlpha(w, h, edgeOpacityFactor), color.A), color.R, color.G, color.B));
        }

        private static bool IsAboveWater(float waterLevel, float groundHeight)
        {
            return waterLevel == 2 ? groundHeight > waterLevel : groundHeight >= waterLevel;
        }

        public int GetEdgeAlpha(int w, int h, float edgeOpacityFactor)
        {
            //var x = Math.Abs(edgeOpacityFactor - 1) < 0.01f
            //    ? 0
            //    : MathF.Clamp((Math.Abs((float)w / BitmapWidth - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor),
            //        0, 1);
            //var y = Math.Abs(edgeOpacityFactor - 1) < 0.01f
            //    ? 0
            //    : MathF.Clamp(
            //        (Math.Abs((float)h / BitmapHeight - 0.5f) * 2 - edgeOpacityFactor) / (1 - edgeOpacityFactor), 0, 1);
            //return (int)(MathF.Sqrt(Math.Max(1 - x * x - y * y, 0)) * 255);
            edgeOpacityFactor = MathF.Clamp(edgeOpacityFactor, 0, 1);
            var factor = edgeOpacityFactor + 2;
            var x = MathF.Clamp(Math.Abs((float)w / BitmapWidth - 0.5f) * 5 - 1.5f, 0, 1);
            var y = MathF.Clamp(Math.Abs((float)h / BitmapHeight - 0.5f) * 5 - 1.5f, 0, 1);
            return (int)(MathF.Pow(MathF.Max(1 - MathF.Pow(x, factor) - MathF.Pow(y, factor), 0f), (1 - edgeOpacityFactor) * 2) * 255);
        }
    }
}
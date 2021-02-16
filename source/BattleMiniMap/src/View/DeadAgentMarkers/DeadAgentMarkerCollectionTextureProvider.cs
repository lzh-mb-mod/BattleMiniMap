using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers;
using BattleMiniMap.View.AgentMarkers.Colors;
using BattleMiniMap.View.AgentMarkers.TextureProviders;
using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace BattleMiniMap.View.DeadAgentMarkers
{
    public class BattleMiniMap_DeadAgentMarkerCollectionTextureProvider : TextureProvider
    {
        private static Bitmap _deadAgentMarkerCollectionBitmap;
        private static Graphics _graphics;
        private static Texture _deadAgentMarkerCollectionTexture;
        private static Task<Texture> _createTextureTask;
        private static Texture _textureToBeReleased;

        public static Texture DeadAgentMarkerCollectionTexture
        {
            get => _deadAgentMarkerCollectionTexture;
            private set
            {
                if (_deadAgentMarkerCollectionTexture == value)
                    return;
                TextureToBeReleased = _deadAgentMarkerCollectionTexture;
                _deadAgentMarkerCollectionTexture = value;
            }
        }

        public static Texture TextureToBeReleased
        {
            get => _textureToBeReleased;
            set
            {
                if (_textureToBeReleased == value)
                    return;
                (_textureToBeReleased?.PlatformTexture as EngineTexture)?.Texture.Release();
                _textureToBeReleased = value;
            }
        }

        public static bool IsGeneratingTexture => _createTextureTask != null;

        public static void Initialize()
        {
            var miniMap = MiniMap.Instance;
            var config = BattleMiniMapConfig.Get();
            var widgetWidth = BattleMiniMapConfig.Get().WidgetWidth;
            var width = (int)MathF.Max(miniMap.BitmapWidth, widgetWidth, 1000 / Math.Max(config.AgentMarkerScale, 0.1f));
            var scale = (float)width / Math.Max(miniMap.BitmapWidth, 1);
            var height = (int)(scale * miniMap.BitmapHeight);
            _deadAgentMarkerCollectionBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            _graphics = Graphics.FromImage(_deadAgentMarkerCollectionBitmap);
            _deadAgentMarkerCollectionBitmap.SetPixel(0, 0, Color.FromArgb(255, 255, 255, 255));
            DeadAgentMarkerCollectionTexture = _deadAgentMarkerCollectionBitmap.CreateTexture();
        }

        public override void Clear()
        {
            base.Clear();

            DeadAgentMarkerCollectionTexture = null;
        }

        public static void Update()
        {
            if (IsGeneratingTexture)
            {
                if (!_createTextureTask.IsCompleted)
                    return;

                if (_createTextureTask.IsCanceled || _createTextureTask.IsFaulted)
                    return;

                DeadAgentMarkerCollectionTexture = _createTextureTask.Result;
                _createTextureTask = null;
            }
        }

        public static void AddDeadAgentMarkers(List<AgentMarker> agentMarkers)
        {
            if (IsGeneratingTexture)
                return;
            if (agentMarkers.Count == 0)
                return;
            if (_deadAgentMarkerCollectionBitmap == null)
                return;

            var bitmapWidth = _deadAgentMarkerCollectionBitmap.Width;
            var scale = BattleMiniMapConfig.Get().AgentMarkerScale;
            _createTextureTask = Task.Run(() =>
            {
                try
                {
                    var scale = (float) _deadAgentMarkerCollectionBitmap.Width /
                                Math.Max(MiniMap.Instance.BitmapWidth, 1);
                    var width = Math.Max(bitmapWidth * 0.01f * scale, 1);
                    var height = Math.Max(bitmapWidth * 0.01f * scale, 1);
                    var types = agentMarkers.First().AgentMarkerType.GetColorAndTextureType();
                    var color = types.Item1.GetColor();
                    var bitmap = types.Item2.GetBitmap();
                    var imageAttributes = new ImageAttributes();
                    float[][] colorMatrixElements =
                    {
                        new[] {color.Red, 0, 0, 0, 0},
                        new[] {0, color.Green, 0, 0, 0},
                        new[] {0, 0, color.Blue, 0, 0},
                        new[] {0, 0, 0, color.Alpha, 0},
                        new[] {0, 0, 0, 0, 1f}
                    };
                    ColorMatrix colorMatrix = new ColorMatrix(colorMatrixElements);
                    imageAttributes.SetColorMatrix(colorMatrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                    foreach (var agentMarker in agentMarkers)
                    {
                        var x = agentMarker.PositionInMap.x * scale - width / 2f;
                        var y = agentMarker.PositionInMap.y * scale - height / 2f;
                        _graphics.DrawImage(bitmap, new Rectangle((int) x, (int) y, (int) width, (int) height), 0, 0,
                            bitmap.Width, bitmap.Height, GraphicsUnit.Pixel, imageAttributes);
                    }

                    return _deadAgentMarkerCollectionBitmap.CreateTexture();
                }
                catch (Exception e)
                {
                    MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(e.ToString());
                }

                return _deadAgentMarkerCollectionTexture;
            });
        }

        public override Texture GetTexture(TwoDimensionContext twoDimensionContext, string name)
        {
            return DeadAgentMarkerCollectionTexture;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMap_MapWidget : TextureWidget
    {
        public BattleMiniMap_MapWidget(UIContext context) : base(context)
        {
            TextureProviderName = nameof(BattleMiniMap_MiniMapTextureProvider);
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (MiniMap.Instance != null)
            {
                var config = BattleMiniMapConfig.Get();
                SuggestedWidth = config.WidgetWidth;
                if (config.FollowMode)
                {
                    SuggestedHeight = SuggestedWidth;
                }
                else
                {
                    var width = MiniMap.Instance.BitmapWidth;
                    var height = MiniMap.Instance.BitmapHeight;
                    SuggestedHeight = height / (float)width * SuggestedWidth;
                }
                IsEnabled = MiniMap.Instance.IsValid;

                HorizontalAlignment = config.HorizontalAlignment;
                VerticalAlignment = config.VerticalAlignment;
                if (HorizontalAlignment == HorizontalAlignment.Left ||
                    HorizontalAlignment == HorizontalAlignment.Center)
                {
                    MarginLeft = config.PositionX;
                    MarginRight = 0;
                }
                else
                {
                    MarginLeft = 0;
                    MarginRight = config.PositionX;
                }

                if (VerticalAlignment == VerticalAlignment.Top || VerticalAlignment == VerticalAlignment.Center)
                {
                    MarginTop = config.PositionY;
                    MarginBottom = 0;
                }
                else
                {
                    MarginTop = 0;
                    MarginBottom = config.PositionY;
                }

                if (config.FollowMode)
                {
                    var size = Widgets.Utility.GetSize(this);
                    CircularClipEnabled = true;
                    CircularClipRadius = 0.3f * size.x;
                    CircularClipSmoothingRadius = 0.2f * size.x;
                }
                else
                {
                    CircularClipEnabled = false;
                }
            }
            else
            {
                IsEnabled = false;
            }
        }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            var config = BattleMiniMapConfig.Get();
            if (!config.FollowMode || this.TextureProvider == null)
            {
                base.OnRender(twoDimensionContext, drawContext);
            }
            else
            {
                this.Texture = this.TextureProvider.GetTexture(twoDimensionContext, string.Empty);
                var camera = (MissionState.Current.Listener as MissionScreen).CombatCamera;
                var position = camera.Position.AsVec2;
                var direction = camera.Direction.AsVec2.Normalized().LeftVec();
                var size = Widgets.Utility.GetSize(this);
                var scale = config.FollowModeScale / 100f * size.x;
                var midPoint = new Vec2(size.X / 2, size.Y / 2);
                var offset = (midPoint - MiniMap.Instance.MapToWidget(MiniMap.Instance.WorldToMapF(position)) * (scale * (MiniMap.Instance.MapBoundMax.y - MiniMap.Instance.MapBoundMin.y) / size.x) * ScaledSuggestedWidth / Math.Max(SuggestedWidth, 1));
                var mesh = Widgets.Utility.CreateDrawObject2D(scale * (MiniMap.Instance.MapBoundMax.y - MiniMap.Instance.MapBoundMin.y), scale * (MiniMap.Instance.MapBoundMax.x - MiniMap.Instance.MapBoundMin.x), offset, midPoint,
                    direction.AngleBetween(-Vec2.Forward));


                var globalPosition = Widgets.Utility.GetGlobalPosition(this);
                var material = Widgets.Utility.CreateMaterial(drawContext, this);
                material.Texture = this.Texture;

                twoDimensionContext.Draw(globalPosition.X, globalPosition.Y, material, mesh);
            }
        }
    }
}
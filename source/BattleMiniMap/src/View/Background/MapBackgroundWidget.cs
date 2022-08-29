using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Background
{
    public class MapBackgroundWidget : TextureWidget
    {
        public MapBackgroundWidget(UIContext context) : base(context)
        {
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
                var percentageInMapPerMeterInWorld = config.GetFollowModeScale() / 100f;
                // In non-follow mode the map bound occupies the whole map, that is, with non scaled ui size of 1 * widget size;
                // In follow mode the map bound occupies more, that is, with non scaled ui size of percentageInMapPerMeterInWorld * scene size * widget size.
                var scaleFromNonFollowModeToFollowMode = percentageInMapPerMeterInWorld *
                                                         (MiniMap.Instance.MapBoundMax.y -
                                                          MiniMap.Instance.MapBoundMin.y);
                var uiScale = ScaledSuggestedWidth / Math.Max(SuggestedWidth, 1);
                var midPoint = new Vec2(SuggestedWidth / 2, SuggestedHeight / 2);
                var mapHeightWidthScale = (float)MiniMap.Instance.BitmapHeight / MathF.Max(MiniMap.Instance.BitmapWidth, 1);
                var offset = (midPoint - MiniMap.Instance.MapToWidget(MiniMap.Instance.WorldToMapF(position)) * scaleFromNonFollowModeToFollowMode) * uiScale;
                var mesh = Widgets.Utility.CreateDrawObject2D(scaleFromNonFollowModeToFollowMode * size.x, scaleFromNonFollowModeToFollowMode * size.x * mapHeightWidthScale, offset, midPoint * uiScale,
                    direction.AngleBetween(-Vec2.Forward));

                var globalPosition = Widgets.Utility.GetGlobalPosition(this);
                var material = Widgets.Utility.CreateMaterial(drawContext, this);
                material.Texture = this.Texture;

                twoDimensionContext.Draw(globalPosition.X, globalPosition.Y, material, mesh);
            }
        }
    }
}

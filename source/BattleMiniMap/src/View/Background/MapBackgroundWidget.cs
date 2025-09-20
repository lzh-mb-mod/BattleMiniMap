using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using BattleMiniMap.Widgets;
using System.Drawing;
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
            if (TextureProvider == null)
            {
                base.OnRender(twoDimensionContext, drawContext);
            }
            else if (!config.FollowMode)
            {
                var simpleMaterial = Widgets.Utility.CreateMaterial2(drawContext, this);
                Texture = TextureProvider.GetTextureForRender(twoDimensionContext, useHashcodeAsName: true);
                simpleMaterial.Texture = Texture;
                var size = Widgets.Utility.GetSize(this);
                var drawObject = Widgets.Utility.CreateDrawObject2D(this, 0, 0, size.x, size.y);
                drawObject.Scale = _scaleToUse;
                drawContext.Draw(simpleMaterial, in drawObject);
            }
            else
            {
                Texture = TextureProvider.GetTextureForRender(twoDimensionContext, useHashcodeAsName: true);
                var camera = MissionState.Current.GetListenerOfType<MissionScreen>().CombatCamera;
                var position = camera.Position.AsVec2;
                var direction = camera.Direction.AsVec2.Normalized().LeftVec();
                var size = Widgets.Utility.GetSize(this);
                var percentageInMapPerMeterInWorld = config.GetFollowModeScale() / 100f;
                // In non-follow mode the map bound occupies the whole map, that is, with non scaled ui size of 1 * widget size;
                // In follow mode the map bound occupies more, that is, with non scaled ui size of percentageInMapPerMeterInWorld * scene size * widget size.
                var scaleFromNonFollowModeToFollowMode = percentageInMapPerMeterInWorld *
                                                         (MiniMap.Instance.MapBoundMax.y -
                                                          MiniMap.Instance.MapBoundMin.y);
                var mapHeightWidthScale = (float)MiniMap.Instance.BitmapHeight / MathF.Max(MiniMap.Instance.BitmapWidth, 1);
                var offset = (new Vec2(SuggestedWidth / 2, SuggestedHeight / 2) - MiniMap.Instance.MapToWidget(MiniMap.Instance.WorldToMapF(position)) * scaleFromNonFollowModeToFollowMode) * _scaleToUse;
                var width = scaleFromNonFollowModeToFollowMode * SuggestedWidth * _scaleToUse;
                var height = scaleFromNonFollowModeToFollowMode * SuggestedWidth * _scaleToUse * mapHeightWidthScale;
                var pivot = MiniMap.Instance.MapToWidget(MiniMap.Instance.WorldToMapF(position));
                pivot = new Vec2(pivot.x / SuggestedWidth, pivot.y / (SuggestedWidth * mapHeightWidthScale));
                var rotation = -direction.AngleBetween(-Vec2.Forward);
                var mesh = Widgets.Utility.CreateDrawObject2D(this,
                    offset,
                    width, height,
                    pivot,
                    rotation);
                mesh.Scale = _scaleToUse;

                var material = Widgets.Utility.CreateMaterial2(drawContext, this);
                material.Texture = Texture;

                mesh.Rectangle.CalculateVisualMatrixFrame();
                twoDimensionContext.DrawImage(material, mesh);
            }
        }
    }
}

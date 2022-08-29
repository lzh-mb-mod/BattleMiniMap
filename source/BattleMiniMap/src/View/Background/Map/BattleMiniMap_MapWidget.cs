using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;

namespace BattleMiniMap.View.Background.Map
{
    public class BattleMiniMap_MapWidget : MapBackgroundWidget
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
                    CircularClipEnabled = true;
                    CircularClipRadius = 0.4f * SuggestedWidth;
                    CircularClipSmoothingRadius = 0.2f * SuggestedWidth;
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
    }
}
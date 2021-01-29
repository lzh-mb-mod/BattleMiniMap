using System.Collections.Specialized;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade;

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
                var width = MiniMap.Instance.BitmapWidth;
                var height = MiniMap.Instance.BitmapHeight;

                var config = BattleMiniMapConfig.Get();
                SuggestedWidth = config.WidgetWidth;
                SuggestedHeight = height / (float) width * SuggestedWidth;
                IsEnabled = MiniMap.Instance.IsEnabled;

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
            }
            else
            {
                IsEnabled = false;
            }
        }
    }
}
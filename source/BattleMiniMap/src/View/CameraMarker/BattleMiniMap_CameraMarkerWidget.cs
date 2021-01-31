using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using BattleMiniMap.Widgets;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.CameraMarker
{
    public class BattleMiniMap_CameraMarkerWidget : RotatedMapItemWidget
    {
        private static readonly CameraMarkerTextureProvider _provider = new CameraMarkerTextureProvider();
        public override Texture Texture => _provider.GetTexture();

        public BattleMiniMap_CameraMarkerWidget(UIContext context) : base(context)
        {
        }

        protected override void OnDisconnectedFromRoot()
        {
            base.OnDisconnectedFromRoot();

            _provider.Clear();
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (MiniMap.Instance != null)
            {
                var width = BattleMiniMapConfig.Get().WidgetWidth;

                SuggestedWidth = Math.Max(width * 0.1f, 1);
                SuggestedHeight = Math.Max(width * 0.1f, 1);
                IsEnabled = MiniMap.Instance.IsValid;
            }
            else
            {
                IsEnabled = false;
            }
        }
    }
}

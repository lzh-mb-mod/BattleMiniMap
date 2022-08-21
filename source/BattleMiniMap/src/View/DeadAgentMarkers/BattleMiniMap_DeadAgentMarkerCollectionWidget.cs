using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace BattleMiniMap.View.DeadAgentMarkers
{
    public class BattleMiniMap_DeadAgentMarkerCollectionWidget : TextureWidget
    {
        public BattleMiniMap_DeadAgentMarkerCollectionWidget(UIContext context) : base(context)
        {
            TextureProviderName = nameof(BattleMiniMap_DeadAgentMarkerCollectionTextureProvider);
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (MiniMap.Instance != null)
            {
                var width = MiniMap.Instance.BitmapWidth;
                var height = MiniMap.Instance.BitmapHeight;

                SuggestedWidth = BattleMiniMapConfig.Get().WidgetWidth;
                SuggestedHeight = height / (float)width * SuggestedWidth;
                IsEnabled = MiniMap.Instance.IsValid;
            }
            else
            {
                IsEnabled = false;
            }
        }
    }
}

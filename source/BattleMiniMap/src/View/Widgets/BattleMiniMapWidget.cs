using BattleMiniMap.Config;
using BattleMiniMap.View.Map;
using TaleWorlds.GauntletUI;

namespace BattleMiniMap.View.Widgets
{
    public class BattleMiniMapWidget : TextureWidget
    {
        public BattleMiniMapWidget(UIContext context) : base(context)
        {
            this.TextureProviderName = "MiniMapTextureProvider";
        }

        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            if (MiniMap.Instance != null)
            {
                var image = MiniMap.Instance.MapImage;

                SuggestedWidth = BattleMiniMapConfig.Get().MapWidth;
                SuggestedHeight = image.Height / (float)image.Width * SuggestedWidth;
                IsEnabled = MiniMap.Instance.IsEnabled;
            }
            else
            {
                IsEnabled = false;
            }
        }
    }
}

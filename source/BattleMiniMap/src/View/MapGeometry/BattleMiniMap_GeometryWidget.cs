using BattleMiniMap.Config;
using BattleMiniMap.View.Background;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.MapGeometry
{
    public sealed class BattleMiniMap_GeometryTextureProvider : TextureProvider
    {
        protected override Texture OnGetTextureForRender(TwoDimensionContext context, string name) => MapGeometryRenderer.Active?.Texture;
    }
    public sealed class BattleMiniMap_GeometryWidget : MapBackgroundWidget
    {
        public BattleMiniMap_GeometryWidget(UIContext context) : base(context)
        {
            TextureProviderName = nameof(BattleMiniMap_GeometryTextureProvider);
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }
        protected override void OnUpdate(float dt)
        {
            base.OnUpdate(dt);
            var map = MiniMap.Instance; var config = BattleMiniMapConfig.Get();
            SuggestedWidth = config.WidgetWidth;
            SuggestedHeight = config.FollowMode ? SuggestedWidth : SuggestedWidth * map.BitmapHeight / (float)System.Math.Max(1, map.BitmapWidth);
            IsEnabled = config.EnableMapGeometry && MapGeometryRenderer.Active?.Texture != null;
        }
        protected override void OnRender(TwoDimensionContext context, TwoDimensionDrawContext drawContext)
        {
            if (BattleMiniMapConfig.Get().EnableMapGeometry && MapGeometryRenderer.Active?.Texture != null)
                base.OnRender(context, drawContext);
        }
    }
}

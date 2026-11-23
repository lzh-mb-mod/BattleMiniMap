using BattleMiniMap.Config;
using BattleMiniMap.View.Background;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.MapGeometry
{
    public sealed class BattleMiniMap_GeometryWidget : MapBackgroundWidget
    {
        public BattleMiniMap_GeometryWidget(UIContext context) : base(context)
        {
            TextureProviderName = null;
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }
        private Texture _lastDrawnTexture;
        protected override bool UsesDirectTexture => true;
        protected override Texture GetMapTexture(TwoDimensionContext context) => MapGeometryRenderer.Active?.Texture;

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
            var texture = MapGeometryRenderer.Active?.Texture;
            if (!BattleMiniMapConfig.Get().EnableMapGeometry || texture == null) return;
            if (!ReferenceEquals(_lastDrawnTexture, texture))
            {
                _lastDrawnTexture = texture;
                TaleWorlds.Library.Debug.Print("BattleMiniMap geometry UI draw: valid=" + texture.IsValid +
                    " suggested=" + SuggestedWidth + "x" + SuggestedHeight + " direct=true follow=" + BattleMiniMapConfig.Get().FollowMode);
            }
            base.OnRender(context, drawContext);
        }
    }
}

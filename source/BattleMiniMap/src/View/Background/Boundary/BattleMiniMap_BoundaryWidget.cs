using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System.Collections.Specialized;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.View.Background.Boundary
{
    public class BattleMiniMap_BoundaryWidget : MapBackgroundWidget
    {
        public BattleMiniMap_BoundaryWidget(UIContext context) : base(context)
        {
            TextureProviderName = nameof(BattleMiniMap_BoundaryTextureProvider);
            WidthSizePolicy = HeightSizePolicy = SizePolicy.Fixed;
        }

        protected override void OnConnectedToRoot()
        {
            base.OnConnectedToRoot();

            Mission.Current.Boundaries.CollectionChanged += BoundariesOnCollectionChanged;
        }

        protected override void OnDisconnectedFromRoot()
        {
            base.OnDisconnectedFromRoot();

            Mission.Current.Boundaries.CollectionChanged -= BoundariesOnCollectionChanged;
        }

        private void BoundariesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            TextureProvider?.Clear(false);
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
            }
            else
            {
                IsEnabled = false;
            }
        }
    }
}

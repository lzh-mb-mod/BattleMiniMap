using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System.Collections.Specialized;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Boundary
{
    public class BattleMiniMap_BoundaryWidget : TextureWidget
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
            TextureProvider?.Clear();
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
                var scale = config.FollowModeScale / 100f * size.x;
                var midPoint = new Vec2(size.X / 2, size.Y / 2);
                var offset = (midPoint - MiniMap.Instance.MapToWidget(MiniMap.Instance.WorldToMapF(position)) * (scale * (MiniMap.Instance.MapBoundMax.y - MiniMap.Instance.MapBoundMin.y) / size.x) * ScaledSuggestedWidth / Math.Max(SuggestedWidth, 1));
                var mesh = Widgets.Utility.CreateDrawObject2D(scale * (MiniMap.Instance.MapBoundMax.y - MiniMap.Instance.MapBoundMin.y), scale * (MiniMap.Instance.MapBoundMax.x - MiniMap.Instance.MapBoundMin.x), offset, midPoint,
                    direction.AngleBetween(-Vec2.Forward));
                
                var globalPosition = Widgets.Utility.GetGlobalPosition(this);
                var material = Widgets.Utility.CreateMaterial(drawContext, this);
                material.Texture = this.Texture;

                twoDimensionContext.Draw(globalPosition.X, globalPosition.Y, material, mesh);
            }
        }
    }
}

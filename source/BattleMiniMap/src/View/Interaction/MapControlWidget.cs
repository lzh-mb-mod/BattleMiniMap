﻿using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using MissionLibrary.Controller.Camera;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace BattleMiniMap.View.Interaction
{
    public class MapControlWidget : ButtonWidget
    {
        public MapControlWidget(UIContext context) : base(context)
        {
            EventFire += OnEventFire;
        }

        protected override void OnDisconnectedFromRoot()
        {
            base.OnDisconnectedFromRoot();

            EventFire -= OnEventFire;
        }

        protected override bool OnPreviewMouseScroll()
        {
            return true;
        }

        protected override void OnMouseScroll()
        {
            base.OnMouseScroll();
            
            float num = this.EventManager.DeltaMouseScroll * 0.001f;
            BattleMiniMapConfig.Get().FollowModeScale = MathF.Clamp(BattleMiniMapConfig.Get().FollowModeScale * (1 + num), 0.1f, 3f);
        }

        private void OnEventFire(Widget widget, string eventName, object[] args)
        {
            if (MiniMap.Instance == null)
                return;
            if (widget == this && eventName == "DoubleClick")
            {
                var globalMousePosition = Widgets.Utility.GetMousePosition(EventManager);
                var widgetLocalPosition = globalMousePosition - Widgets.Utility.GetGlobalPosition(this);
                var worldPosition = MiniMap.Instance.WidgetToWorld(widgetLocalPosition);
                ACameraControllerManager.Get().Instance?.RequestCameraGoTo(worldPosition);
            }
        }
    }
}

using BattleMiniMap.Config;
using BattleMiniMap.Config.HotKey;
using BattleMiniMap.View.MapTerrain;
using System.Collections.Specialized;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMapView : MissionView
    {
        private BattleMiniMapViewModel _dataSource;
        private GauntletLayer _layer;
        private MissionTimer _timer;
        private bool _boundaryChanged;
        private bool _isOrderViewOpened;

        public override void OnBehaviorInitialize()
        {
            base.OnBehaviorInitialize();

            MiniMap.Instance.InitializeMapRange(Mission.Current, true);
            _dataSource = new BattleMiniMapViewModel(MissionScreen);
            Mission.Current.Boundaries.CollectionChanged += BoundariesOnCollectionChanged;
            Game.Current.EventManager.RegisterEvent<MissionPlayerToggledOrderViewEvent>(OnOrderViewToggled);
        }

        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();

            _layer = new GauntletLayer(20);
            _layer.LoadMovie(nameof(BattleMiniMapView), _dataSource);
            MissionScreen.AddLayer(_layer);
            _timer = new MissionTimer(0.03f);
        }

        public override void OnMissionScreenTick(float dt)
        {
            base.OnMissionScreenTick(dt);

            if (_boundaryChanged)
            {
                _boundaryChanged = false;
                if (!MiniMap.Instance.IsValid)
                {
                    MiniMap.Instance.InitializeMapRange(Mission.Current, true);
                }
            }

            bool toggleMapKeyDown = false;
            var toggleMapLongPressKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMapLongPress);
            var toggleMapKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMap);
            if (BattleMiniMapConfig.Get().EnableToggleMapLongPressKey && toggleMapLongPressKey.IsKeyDown(Input))
            {
                toggleMapKeyDown = true;
            }
            else if(toggleMapKey.IsKeyPressed(Input))
            {
                BattleMiniMapConfig.Get().ShowMap = !BattleMiniMapConfig.Get().ShowMap;
            }

            _dataSource.UpdateEnabled(dt, MiniMap.Instance.IsValid &&
                                          ((BattleMiniMapConfig.Get().ShowMap ^ toggleMapKeyDown) ||
                                           _isOrderViewOpened && BattleMiniMapConfig.Get().ShowMapWhenCommanding));

            _dataSource.UpdateCamera();

            if (_timer.Check(true))
                _dataSource.UpdateData();
            else if (BattleMiniMapConfig.Get().FollowMode)
                _dataSource.UpdateRenderData();
        }

        public override void OnAgentBuild(Agent agent, Banner banner)
        {
            base.OnAgentBuild(agent, banner);

            _dataSource.AddAgent(agent);
        }

        public override void OnMissionScreenFinalize()
        {
            base.OnMissionScreenFinalize();

            MissionScreen.RemoveLayer(_layer);
            _dataSource.OnFinalize();
            _layer = null;
            Mission.Current.Boundaries.CollectionChanged -= BoundariesOnCollectionChanged;
            Game.Current.EventManager.UnregisterEvent<MissionPlayerToggledOrderViewEvent>(OnOrderViewToggled);
        }

        private void BoundariesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _boundaryChanged = true;
        }

        private void OnOrderViewToggled(MissionPlayerToggledOrderViewEvent obj)
        {
            _isOrderViewOpened = obj.IsOrderEnabled;
        }
    }
}
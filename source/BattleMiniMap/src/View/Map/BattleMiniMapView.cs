using System.Collections.Specialized;
using BattleMiniMap.Config;
using BattleMiniMap.Config.HotKey;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.Missions;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMapView : MissionView
    {
        private BattleMiniMapViewModel _dataSource;
        private GauntletLayer _layer;
        private MissionTimer _timer;
        private bool _boundaryChanged;
        private bool _isOrderViewOpened;

        public override void OnBehaviourInitialize()
        {
            base.OnBehaviourInitialize();

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
            _timer = new MissionTimer(0.05f);
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

            if (Input.IsKeyPressed(InputKey.F))
            {
                BattleMiniMapConfig.Get().Deserialize();
                MiniMap.Instance?.UpdateMapImage(Mission);
            }

            bool toggleMapKeyDown = false;
            var toggleMapLongPressKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMapLongPress);
            var toggleMapKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMap);
            if (BattleMiniMapConfig.Get().EnableToggleMapLongPressKey)
            {
                if (Input.IsKeyDown(toggleMapLongPressKey))
                {
                    toggleMapKeyDown = true;
                }
            }

            if (!BattleMiniMapConfig.Get().EnableToggleMapLongPressKey || toggleMapKey != toggleMapLongPressKey)
            {
                if (Input.IsKeyPressed(toggleMapKey))
                {
                    BattleMiniMapConfig.Get().ShowMap = !BattleMiniMapConfig.Get().ShowMap;
                }
            }

            _dataSource.UpdateEnabled(dt, MiniMap.Instance.IsValid &&
                                          ((BattleMiniMapConfig.Get().ShowMap ^ toggleMapKeyDown) ||
                                           _isOrderViewOpened && BattleMiniMapConfig.Get().ShowMapWhenCommanding));
            
            if (_timer.Check(true))
                _dataSource.UpdateData();
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
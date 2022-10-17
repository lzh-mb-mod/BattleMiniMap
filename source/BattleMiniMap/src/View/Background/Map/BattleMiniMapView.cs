using BattleMiniMap.Config;
using BattleMiniMap.Config.HotKey;
using BattleMiniMap.View.MapTerrain;
using System.Collections.Specialized;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace BattleMiniMap.View.Background.Map
{
    public class BattleMiniMapView : MissionView
    {
        private BattleMiniMapViewModel _dataSource;
        private GauntletLayer _layer;
        private MissionTimer _timer;
        private bool _boundaryChanged;
        private bool _isOrderViewEnabled;
        private float _targetDynamicScale = 1;

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

            _layer = new GauntletLayer(12);
            _layer.LoadMovie(nameof(BattleMiniMapView), _dataSource);
            _layer.InputRestrictions.SetInputRestrictions(false, InputUsageMask.Mouse);
            MissionScreen.AddLayer(_layer);
            _timer = new MissionTimer(0.05f);
            BattleMiniMapConfig.DynamicOpacityExponent = MathF.Clamp(MathF.Abs(Mission.Scene.TimeOfDay - 12f) / 12f * 2f + 0.5f, 0.7f, 2.5f);
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
            bool instantlyUpdateScale = false;
            var toggleMapLongPressKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMapLongPress);
            var toggleMapKey = BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMap);
            if (BattleMiniMapConfig.Get().EnableToggleMapLongPressKey && toggleMapLongPressKey.IsKeyDown(Input))
            {
                toggleMapKeyDown = true;
            }
            else if (toggleMapKey.IsKeyPressed(Input))
            {
                BattleMiniMapConfig.Get().ShowMap = !BattleMiniMapConfig.Get().ShowMap;
                if (BattleMiniMapConfig.Get().ShowMap ^ (_isOrderViewEnabled && BattleMiniMapConfig.Get().ToggleMapWhenCommanding))
                {
                    BattleMiniMapConfig.Get().FollowMode = !BattleMiniMapConfig.Get().FollowMode;
                    instantlyUpdateScale = true;
                }
            }

            _dataSource.UpdateEnabled(dt, MiniMap.Instance.IsValid &&
                                          (BattleMiniMapConfig.Get().ShowMap ^ toggleMapKeyDown ^
                                          (_isOrderViewEnabled && BattleMiniMapConfig.Get().ToggleMapWhenCommanding)));

            _dataSource.UpdateCamera();
            UpdateDynamicScale(dt, instantlyUpdateScale);

            if (_timer.Check(true))
                _dataSource.UpdateData();
            else if (BattleMiniMapConfig.Get().FollowMode)
                _dataSource.UpdateRenderData();
        }

        private void UpdateDynamicScale(float dt, bool instantly)
        {
            if (!_dataSource.IsEnabled)
                return;

            if (!BattleMiniMapConfig.Get().FollowMode || !BattleMiniMapConfig.Get().EnableDynamicScale)
            {
                _targetDynamicScale = 1;
            }
            else
            {
                if (!MissionSharedLibrary.Utilities.Utility.IsAgentDead(MissionScreen.LastFollowedAgent))
                {
                    var speed = MissionScreen.LastFollowedAgent.Velocity.Length;
                    _targetDynamicScale = 1 / MathF.Lerp(1f, 3f, speed / 50);
                }
                else
                {
                    _targetDynamicScale = 1 / MathF.Lerp(1f, 3f,
                        (MissionScreen.CombatCamera.Position.z - MissionScreen.Mission.Scene.GetGroundHeightAtPosition(MissionScreen.CombatCamera.Position + new Vec3(z: 100f))) / 120);
                }
            }

            BattleMiniMapConfig.DynamicScale = MathF.Lerp(_targetDynamicScale, BattleMiniMapConfig.DynamicScale,
                instantly ? 0 : MathF.Pow(0.4f, dt));
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
            _isOrderViewEnabled = obj.IsOrderEnabled;
        }
    }
}
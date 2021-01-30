using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System.Collections.Specialized;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Missions;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMapView : MissionView
    {
        private BattleMiniMapViewModel _dataSource;
        private GauntletLayer _layer;
        private MissionTimer _timer;
        private bool _boundaryChanged = false;

        public override void OnBehaviourInitialize()
        {
            base.OnBehaviourInitialize();

            MiniMap.Instance.InitializeMapRange(Mission.Current, true);
            _dataSource = new BattleMiniMapViewModel(MissionScreen);
            Mission.Current.Boundaries.CollectionChanged += BoundariesOnCollectionChanged;
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

            _dataSource.IsEnabled = MiniMap.Instance.IsEnabled && BattleMiniMapConfig.Get().ShowMap;
            if (!MiniMap.Instance.IsEnabled && _boundaryChanged)
            {
                MiniMap.Instance.InitializeMapRange(Mission.Current, BattleMiniMapConfig.Get().ShowMap);
            }

            if (_timer.Check(true))
                _dataSource.Update();
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
        }

        private void BoundariesOnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            _boundaryChanged = true;
        }
    }
}
using BattleMiniMap.View.Map;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.MountAndBlade.View.Missions;

namespace BattleMiniMap.View
{
    public class BattleMiniMapView : MissionView
    {
        private GauntletLayer _layer;
        private BattleMiniMapViewModel _dataSource;
        public override void OnMissionScreenInitialize()
        {
            base.OnMissionScreenInitialize();

            MiniMap.Instance = new MiniMap();
            MiniMap.Instance.UpdateMapImage(Mission);

            _dataSource = new BattleMiniMapViewModel();
            _layer = new GauntletLayer(22);
            _layer.LoadMovie(nameof(BattleMiniMapView), _dataSource);
            MissionScreen.AddLayer(_layer);
        }

        public override void OnMissionScreenFinalize()
        {
            base.OnMissionScreenFinalize();

            MissionScreen.RemoveLayer(_layer);
            _dataSource.OnFinalize();
            _layer = null;
        }
    }
}

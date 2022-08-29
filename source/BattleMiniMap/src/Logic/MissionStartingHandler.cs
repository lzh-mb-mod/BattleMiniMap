using BattleMiniMap.View.Background.Map;
using MissionLibrary.Controller;
using MissionSharedLibrary.Controller;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;

namespace BattleMiniMap.Logic
{
    public class MissionStartingHandler : AMissionStartingHandler
    {
        public override void OnCreated(MissionView entranceView)
        {
            var list = new List<MissionBehavior>
            {
                new BattleMiniMapView()
            };


            foreach (var missionBehavior in list)
                MissionStartingManager.AddMissionBehavior(entranceView, missionBehavior);
        }

        public override void OnPreMissionTick(MissionView entranceView, float dt)
        {
        }
    }
}
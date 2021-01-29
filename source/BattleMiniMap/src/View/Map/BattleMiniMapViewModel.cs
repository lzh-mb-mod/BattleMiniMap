using System;
using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarker;
using BattleMiniMap.View.CameraMarker;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screen;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMapViewModel : ViewModel
    {
        private float _alphaFactor;
        private bool _isEnabled;

        [DataSourceProperty]
        public float AlphaFactor
        {
            get => _alphaFactor;
            set
            {
                if (Math.Abs(_alphaFactor - value) < 0.01f)
                    return;
                _alphaFactor = value;
                OnPropertyChanged(nameof(AlphaFactor));
            }
        }

        [DataSourceProperty]
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;
                _isEnabled = value;
                OnPropertyChanged(nameof(IsEnabled));
            }
        }

        public CameraMarkerViewModel CameraMarkerLeft { get; }
        public CameraMarkerViewModel CameraMarkerRight { get; }

        public MBBindingList<AgentMarkerViewModel> AgentMarkerViewModels { get; }
        public MBBindingList<AgentMarkerViewModel> DeadAgentMarkerViewModels { get; }

        public BattleMiniMapViewModel(MissionScreen missionScreen)
        {
            CameraMarkerLeft = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Left);
            CameraMarkerRight = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Right);
            AgentMarkerViewModels = new MBBindingList<AgentMarkerViewModel>();
            DeadAgentMarkerViewModels = new MBBindingList<AgentMarkerViewModel>();
        }

        public void Update()
        {
            UpdateAgentMarkers();
            AlphaFactor = BattleMiniMapConfig.Get().BackgroundOpacity;
            if (!IsEnabled)
                return;
            CameraMarkerLeft.Update();
            CameraMarkerRight.Update();
        }

        public void AddAgent(Agent agent)
        {
            if (agent.IsActive())
            {
                AgentMarkerViewModels.Add(new AgentMarkerViewModel(agent));
            }
            else
            {
                DeadAgentMarkerViewModels.Add(new AgentMarkerViewModel(agent));
            }
        }

        private void UpdateAgentMarkers()
        {
            int count = AgentMarkerViewModels.Count;
            int lastOne = count - 1;
            for (int i = 0; i <= lastOne; i++)
            {
                var current = AgentMarkerViewModels[i];
                if (AgentMarkerViewModels[i].AgentMarkerType == AgentMarkerType.Dead)
                {
                    DeadAgentMarkerViewModels.Add(current);
                    if (i < lastOne)
                    {
                        AgentMarkerViewModels[i] = AgentMarkerViewModels[lastOne];
                    }
                    --lastOne;
                }
                current.Update();
            }

            if (lastOne < count - 1)
            {
                for (int i = count - 1; i > lastOne ; i--)
                {
                    AgentMarkerViewModels.RemoveAt(i);
                }
            }
        }
    }
}
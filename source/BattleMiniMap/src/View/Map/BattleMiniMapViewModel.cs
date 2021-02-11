using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers;
using BattleMiniMap.View.CameraMarker;
using BattleMiniMap.View.DeadAgentMarkers;
using BattleMiniMap.View.MapTerrain;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screen;

namespace BattleMiniMap.View.Map
{
    public class BattleMiniMapViewModel : ViewModel
    {
        private float _backgroundAlphaFactor;
        private bool _isEnabled;
        private float _foregroundAlphaFactor;
        private readonly BasicTimer _timer;

        [DataSourceProperty]
        public float BackgroundAlphaFactor
        {
            get => _backgroundAlphaFactor;
            set
            {
                if (Math.Abs(_backgroundAlphaFactor - value) < 0.01f)
                    return;
                _backgroundAlphaFactor = value;
                OnPropertyChanged(nameof(BackgroundAlphaFactor));
            }
        }

        [DataSourceProperty]
        public float ForegroundAlphaFactor
        {
            get => _foregroundAlphaFactor;
            set
            {
                if (Math.Abs(_foregroundAlphaFactor - value) < 0.01f)
                    return;
                _foregroundAlphaFactor = value;
                OnPropertyChanged(nameof(ForegroundAlphaFactor));
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

        public List<AgentMarker> AgentMarkerViewModels { get; }
        //public List<AgentMarker> DeadAgentMarkerViewModels { get; set; }

        public BattleMiniMapViewModel(MissionScreen missionScreen)
        {
            CameraMarkerLeft = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Left);
            CameraMarkerRight = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Right);
            AgentMarkerViewModels = new List<AgentMarker>();
            //DeadAgentMarkerViewModels = new List<AgentMarker>();
            _timer = new BasicTimer(MBCommon.TimeType.Application);
        }

        public void UpdateEnabled(float dt, bool isEnabled)
        {
            bool isFadingCompleted = MiniMap.IsFadingCompleted();
            if (IsEnabled)
            {
                if (isEnabled)
                {
                    if (MiniMap.IsFadingOut())
                    {
                        MiniMap.SetFadeIn();
                    }
                }
                else
                {
                    if (isFadingCompleted)
                    {
                        IsEnabled = false;
                    }
                    else if (!MiniMap.IsFadingOut())
                    {
                        MiniMap.SetFadeOut();
                    }
                }
            }
            else
            {
                if (isEnabled)
                {
                    IsEnabled = true;
                    MiniMap.SetFadeIn();
                }
            }

            MiniMap.UpdateFading(dt);
            BackgroundAlphaFactor = BattleMiniMapConfig.Get().BackgroundOpacity * MiniMap.FadeInOutAlphaFactor;
            ForegroundAlphaFactor = BattleMiniMapConfig.Get().ForegroundOpacity * MiniMap.FadeInOutAlphaFactor;
        }

        public void UpdateData()
        {
            UpdateAgentMarkers();
            if (!IsEnabled)
                return;
            CameraMarkerLeft.Update();
            CameraMarkerRight.Update();
        }

        public void AddAgent(Agent agent)
        {
            if (agent.IsActive())
            {
                AgentMarkerViewModels.Add(new AgentMarker(agent));
            }
            //else
            //{
            //    DeadAgentMarkerViewModels.Add(new AgentMarker(agent));
            //}
        }

        private void UpdateAgentMarkers()
        {
            int count = AgentMarkerViewModels.Count;
            int lastOne = count - 1;
            for (int i = 0; i <= lastOne;)
            {
                var current = AgentMarkerViewModels[i];
                current.Update();
                if (current.AgentMarkerType == AgentMarkerType.Inactive)
                {
                    //DeadAgentMarkerViewModels.Add(new AgentMarker(current));
                    if (i < lastOne)
                    {
                        current.MoveFrom(AgentMarkerViewModels[lastOne]);
                    }
                    --lastOne;
                }
                else
                {
                    ++i;
                }
            }

            if (lastOne < count - 1)
            {
                for (int i = count - 1; i > lastOne ; i--)
                {
                    AgentMarkerViewModels.RemoveAt(i);
                }
            }

            if (BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.IsGeneratingTexture)
                BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.Update();
            //else
            //{
            //    if (DeadAgentMarkerViewModels.Count > 50 ||
            //        DeadAgentMarkerViewModels.Count > 0 && _timer.ElapsedTime > 1f)
            //    {
            //        _timer.Reset();
            //        BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.AddDeadAgentMarkers(
            //            DeadAgentMarkerViewModels);
            //        DeadAgentMarkerViewModels = new List<AgentMarker>();
            //    }
            //}
        }
    }
}
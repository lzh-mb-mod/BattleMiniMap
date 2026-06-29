using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarkers;
using BattleMiniMap.View.CameraMarker;
using BattleMiniMap.View.MapTerrain;
using System;
using System.Collections.Generic;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.Screens;

namespace BattleMiniMap.View.Background.Map
{
    public class BattleMiniMapViewModel : ViewModel
    {
        private float _backgroundAlphaFactor;
        private bool _isEnabled;
        private float _foregroundAlphaFactor;
        //private AgentMarkerCollection _deadAgentMarkers;
        //private AgentMarkerCollection _deadAgentMarkersBackup;

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

        public AgentMarkerCollection NonHeroAgentMarkers { get; private set; }
        public AgentMarkerCollection HeroAgentMarkers { get; private set; }

        //[DataSourceProperty]
        //public AgentMarkerCollection DeadAgentMarkers
        //{
        //    get => _deadAgentMarkers;
        //    set
        //    {
        //        if (_deadAgentMarkers == value)
        //            return;
        //        _deadAgentMarkers = value;
        //        OnPropertyChanged(nameof(DeadAgentMarkers));
        //    }
        //}

        public BattleMiniMapViewModel(MissionScreen missionScreen)
        {
            CameraMarkerLeft = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Left);
            CameraMarkerRight = new CameraMarkerViewModel(missionScreen, CameraMarkerSide.Right);
            NonHeroAgentMarkers = new AgentMarkerCollection
            {
                AgentMarkers = new List<AgentMarker>()
            };
            HeroAgentMarkers = new AgentMarkerCollection
            {
                AgentMarkers = new List<AgentMarker>()
            };
            //DeadAgentMarkers = new AgentMarkerCollection
            //{
            //    AgentMarkers = new List<AgentMarker>()
            //};
            //_deadAgentMarkersBackup = new AgentMarkerCollection
            //{
            //    AgentMarkers = new List<AgentMarker>()
            //};
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
            BackgroundAlphaFactor = BattleMiniMapConfig.Get().GetBackgroundOpacity() * MiniMap.FadeInOutAlphaFactor;
            ForegroundAlphaFactor = BattleMiniMapConfig.Get().GetForegroundOpacity() * MiniMap.FadeInOutAlphaFactor;
        }

        public void UpdateData()
        {
            UpdateNonHeroAgentMarkers();
            UpdateHeroAgentMarkers();
        }

        public void UpdateRenderData()
        {
            foreach (var agentMarker in NonHeroAgentMarkers.AgentMarkers)
            {
                agentMarker.RenderUpdate();
            }
            foreach (var agentMarker in HeroAgentMarkers.AgentMarkers)
            {
                agentMarker.RenderUpdate();
            }
        }

        public void UpdateCamera()
        {
            CameraMarkerLeft.Update();
            CameraMarkerRight.Update();
        }

        public void AddAgent(Agent agent)
        {
            if (agent.IsActive())
            {
                if (agent.IsHero)
                    HeroAgentMarkers.Add(agent);
                else
                    NonHeroAgentMarkers.Add(agent);
            }
            //else
            //{
            //    DeadAgentMarkers.Add(new AgentMarker(agent));
            //}
        }

        private void UpdateNonHeroAgentMarkers()
        {
            try
            {
                int count = NonHeroAgentMarkers.CountOfAgentMarkers;
                int lastOne = count - 1;
                for (int i = 0; i <= lastOne;)
                {
                    var current = NonHeroAgentMarkers.AgentMarkers[i];
                    current.UpdateAsNonHero();
                    if (current.AgentMarkerType.ColorType == View.AgentMarkers.Colors.AgentMarkerColorType.Inactive)
                    {
                        //DeadAgentMarkers.Add(current);
                        if (i < lastOne)
                        {
                            NonHeroAgentMarkers.AgentMarkers[i].CopyFrom(NonHeroAgentMarkers.AgentMarkers[lastOne]);
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
                    for (int i = count - 1; i > lastOne; i--)
                    {
                        NonHeroAgentMarkers.AgentMarkers[i].Clear();
                    }

                    NonHeroAgentMarkers.CountOfAgentMarkers = lastOne + 1;
                }
            }
            catch (Exception e)
            {
                MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(e.ToString());
            }
        }
        private void UpdateHeroAgentMarkers()
        {
            try
            {
                int count = HeroAgentMarkers.CountOfAgentMarkers;
                int lastOne = count - 1;
                for (int i = 0; i <= lastOne;)
                {
                    var current = HeroAgentMarkers.AgentMarkers[i];
                    current.UpdateAsHero();
                    if (current.AgentMarkerType.ColorType == View.AgentMarkers.Colors.AgentMarkerColorType.Inactive)
                    {
                        //DeadAgentMarkers.Add(current);
                        if (i < lastOne)
                        {
                            HeroAgentMarkers.AgentMarkers[i].CopyFrom(HeroAgentMarkers.AgentMarkers[lastOne]);
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
                    for (int i = count - 1; i > lastOne; i--)
                    {
                        HeroAgentMarkers.AgentMarkers[i].Clear();
                    }

                    HeroAgentMarkers.CountOfAgentMarkers = lastOne + 1;
                }
            }
            catch (Exception e)
            {
                MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(e.ToString());
            }
        }
    }
}
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

        public AgentMarkerCollection AgentMarkers { get; private set; }

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
            AgentMarkers = new AgentMarkerCollection
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
            BackgroundAlphaFactor = BattleMiniMapConfig.Get().BackgroundOpacity * MiniMap.FadeInOutAlphaFactor;
            ForegroundAlphaFactor = BattleMiniMapConfig.Get().ForegroundOpacity * MiniMap.FadeInOutAlphaFactor;
        }

        public void UpdateData()
        {
            UpdateAgentMarkers();
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
                AgentMarkers.Add(agent);
            }
            //else
            //{
            //    DeadAgentMarkers.Add(new AgentMarker(agent));
            //}
        }

        private void UpdateAgentMarkers()
        {
            try
            {
                int count = AgentMarkers.CountOfAgentMarkers;
                int lastOne = count - 1;
                for (int i = 0; i <= lastOne;)
                {
                    var current = AgentMarkers.AgentMarkers[i];
                    current.Update();
                    if (current.AgentMarkerType == AgentMarkerType.Inactive)
                    {
                        //DeadAgentMarkers.Add(current);
                        if (i < lastOne)
                        {
                            AgentMarkers.AgentMarkers[i].CopyFrom(AgentMarkers.AgentMarkers[lastOne]);
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
                        AgentMarkers.AgentMarkers[i].Clear();
                    }

                    AgentMarkers.CountOfAgentMarkers = lastOne + 1;
                }

                //if (BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.IsGeneratingTexture)
                //    BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.Update();
                //else
                //{
                //    if (DeadAgentMarkers.CountOfAgentMarkers > 50 ||
                //        DeadAgentMarkers.CountOfAgentMarkers > 0 && _timer.ElapsedTime > 10f)
                //    {
                //        _timer.Reset();
                //        var backup = _deadAgentMarkersBackup;
                //        backup.CountOfAgentMarkers = 0;
                //        _deadAgentMarkersBackup = DeadAgentMarkers;
                //        BattleMiniMap_DeadAgentMarkerCollectionTextureProvider.AddDeadAgentMarkers(DeadAgentMarkers
                //            .AgentMarkers);
                //        DeadAgentMarkers = backup;
                //    }
                //}
            }
            catch (Exception e)
            {
                MissionSharedLibrary.Utilities.Utility.DisplayMessageForced(e.ToString());
            }
        }
    }
}
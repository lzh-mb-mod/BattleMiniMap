using BattleMiniMap.Config;
using BattleMiniMap.View.MapTerrain;
using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade.View.Screen;

namespace BattleMiniMap.View.CameraMarker
{
    public enum CameraMarkerSide
    {
        Left, Right
    }
    public class CameraMarkerViewModel : ViewModel
    {
        private readonly MissionScreen _missionScreen;
        private readonly CameraMarkerSide _side;
        private float _rotateAngleInRadians;
        private Vec2 _rotateCenter;
        private Vec2 _basePosition;
        private float _alphaFactor;

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

        public CameraMarkerViewModel(MissionScreen missionScreen, CameraMarkerSide side)
        {
            _missionScreen = missionScreen;
            _side = side;
        }

        [DataSourceProperty]
        public float RotateAngleInRadians
        {
            get => _rotateAngleInRadians;
            set
            {
                _rotateAngleInRadians = value;
                OnPropertyChanged(nameof(RotateAngleInRadians));
            }
        }

        [DataSourceProperty]
        public Vec2 RotateCenter
        {
            get => _rotateCenter;
            set
            {
                if (_rotateCenter == value)
                    return;
                _rotateCenter = value;
                OnPropertyChanged(nameof(RotateCenter));
            }
        }

        [DataSourceProperty]
        public Vec2 BasePosition
        {
            get => _basePosition;
            set
            {
                if (_basePosition == value)
                    return;
                _basePosition = value;
                OnPropertyChanged(nameof(BasePosition));
            }
        }

        public override void RefreshValues()
        {
            base.RefreshValues();

            Update();
        }

        public void Update()
        {
            AlphaFactor = BattleMiniMapConfig.Get().ForegroundOpacity * MiniMap.FadeInOutAlphaFactor;
            var direction = _missionScreen.CombatCamera.Direction;
            var fov = _missionScreen.CombatCamera.HorizontalFov;
            var right = direction.AsVec2.Normalized().RightVec().ToVec3();
            var up = Vec3.CrossProduct(right, direction);
            var edgeDir =
                direction.RotateAboutAnArbitraryVector(up, fov / 2 * (_side == CameraMarkerSide.Left ? 1 : -1));
            var cameraPosition = _missionScreen.CombatCamera.Position;
            var markerLength = BattleMiniMapConfig.Get().WidgetWidth * 0.1f;
            var end = cameraPosition + edgeDir * markerLength;
            var cameraPosInMap = MiniMap.Instance.WorldToMapF(cameraPosition.AsVec2);
            Vec2 cameraPosInWidget =
                MiniMap.Instance.MapToWidget(cameraPosInMap);
            var endPosInMap = MiniMap.Instance.WorldToMapF(end.AsVec2);
            Vec2 endPositionInWidget = MiniMap.Instance.MapToWidget(endPosInMap);
            BasePosition = cameraPosInWidget;
            RotateCenter = Vec2.Zero;
            RotateAngleInRadians = (endPositionInWidget - cameraPosInWidget).AngleBetween(new Vec2(1, 1));
        }
    }
}

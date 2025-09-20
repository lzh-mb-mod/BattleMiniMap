using System.Numerics;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.Widgets
{
    public abstract class RotatedMapItemWidget : MapItemWidget
    {
        private Vec2 _basePosition;
        private float _rotateAngleInRadians;

        public float RotateAngleInRadians
        {
            get => _rotateAngleInRadians;
            set
            {
                _rotateAngleInRadians = value;
                UpdateDrawObject2D();
            }
        }

        public Vec2 RotateCenter { get; set; }

        public Vec2 ActualRotateCenter => new Vec2(RotateCenter.x * SuggestedWidth, RotateCenter.y * SuggestedHeight);

        public virtual Vec2 BasePosition
        {
            get => _basePosition;
            set
            {
                _basePosition = value;
                PositionXOffset = _basePosition.x;
                PositionYOffset = _basePosition.y;
            }
        }

        protected RotatedMapItemWidget(UIContext context) : base(context)
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            Layer = 6;
        }

        protected override void UpdateDrawObject2D()
        {
            CachedMesh = CreateDrawObject2D();
            CachedMesh.Rectangle.CalculateVisualMatrixFrame();
        }

        protected new ImageDrawObject CreateDrawObject2D()
        {
            var rectangle = Rectangle2D.Create();
            rectangle.LocalPosition = new Vector2(LocalPosition.X, LocalPosition.Y);
            rectangle.LocalScale = new Vector2(Size.X, Size.Y);
            rectangle.LocalPivot = new Vector2(ActualRotateCenter.x / Size.X, ActualRotateCenter.y / Size.Y);
            rectangle.LocalRotation = RotateAngleInRadians * 180 / Mathf.PI;
            rectangle.CalculateMatrixFrame(ParentWidget == null ? EventManager.AreaRectangle : ParentWidget.AreaRect);
            return ImageDrawObject.Create(in rectangle, new Vec2(0, 0), new Vec2(1, 1));
        }
    }
}

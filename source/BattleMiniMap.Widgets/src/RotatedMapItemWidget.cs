using System.Collections.Generic;
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
        }

        protected override void UpdateDrawObject2D()
        {
            if (CachedMesh == null)
                CachedMesh = CreateDrawObject2D();
            else
            {
                Vec2 topLeft = Rotate(new Vec2(0, 0));
                Vec2 bottomLeft = Rotate(new Vec2(0, Size.Y));
                Vec2 bottomRight = Rotate(new Vec2(Size.X, Size.Y));
                Vec2 topRight = Rotate(new Vec2(Size.X, 0));
                CachedMesh.Vertices[0] = CachedMesh.Vertices[6] = topLeft.x;
                CachedMesh.Vertices[1] = CachedMesh.Vertices[7] = topLeft.y;
                CachedMesh.Vertices[2] = bottomLeft.x;
                CachedMesh.Vertices[3] = bottomLeft.y;
                CachedMesh.Vertices[4] = CachedMesh.Vertices[8] = bottomRight.x;
                CachedMesh.Vertices[5] = CachedMesh.Vertices[9] = bottomRight.y;
                CachedMesh.Vertices[10] = topRight.x;
                CachedMesh.Vertices[11] = topRight.y;
            }
        }

        protected new DrawObject2D CreateDrawObject2D()
        {
            Vec2 topLeft = Rotate(new Vec2(0, 0));
            Vec2 bottomLeft = Rotate(new Vec2(0, Size.Y));
            Vec2 bottomRight = Rotate(new Vec2(Size.X, Size.Y));
            Vec2 topRight = Rotate(new Vec2(Size.X, 0));
            DrawObject2D polygonCoordinates = DrawObject2D.CreateTriangleTopologyMeshWithPolygonCoordinates(new List<Vector2>()
            {
                new Vector2(topLeft.x, topLeft.y),
                new Vector2(bottomLeft.x, bottomLeft.y),
                new Vector2(bottomRight.x, bottomRight.y),
                new Vector2(topRight.x, topRight.y)
            });
            polygonCoordinates.TextureCoordinates[0] = 0.0f;
            polygonCoordinates.TextureCoordinates[1] = 0.0f;
            polygonCoordinates.TextureCoordinates[2] = 0.0f;
            polygonCoordinates.TextureCoordinates[3] = 1f;
            polygonCoordinates.TextureCoordinates[4] = 1f;
            polygonCoordinates.TextureCoordinates[5] = 1f;
            polygonCoordinates.TextureCoordinates[6] = 0.0f;
            polygonCoordinates.TextureCoordinates[7] = 0.0f;
            polygonCoordinates.TextureCoordinates[8] = 1f;
            polygonCoordinates.TextureCoordinates[9] = 1f;
            polygonCoordinates.TextureCoordinates[10] = 1f;
            polygonCoordinates.TextureCoordinates[11] = 0.0f;
            polygonCoordinates.Width = Size.X;
            polygonCoordinates.Height = Size.Y;
            polygonCoordinates.MinU = 0.0f;
            polygonCoordinates.MaxU = 1f;
            polygonCoordinates.MinV = 0.0f;
            polygonCoordinates.MaxV = 1f;
            return polygonCoordinates;
        }

        private Vec2 Rotate(Vec2 pos)
        {
            var actualRotateCenter = ActualRotateCenter;
            var vec = pos - actualRotateCenter;
            vec.RotateCCW(-RotateAngleInRadians);
            return vec + actualRotateCenter;
        }
    }
}

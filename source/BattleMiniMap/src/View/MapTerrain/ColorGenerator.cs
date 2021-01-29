using TaleWorlds.Library;
using Color = System.Drawing.Color;

namespace BattleMiniMap.View.MapTerrain
{
    public class ColorGenerator
    {
        private readonly Color[] _colors;
        public ColorGenerator(Color[] colors)
        {
            _colors = colors;
        }

        public Color GetColor(float progress)
        {
            var count = _colors.Length;
            if (progress < 0)
                return _colors[0];
            if (progress >= 1)
                return _colors[count - 1];
            var x = progress * (count - 1);
            int floor = MathF.Floor(x);
            int ceiling = MathF.Floor(x + 1);
            var colorFloor = _colors[floor];
            var colorCeiling = _colors[ceiling];
            return Color.FromArgb((byte) (colorFloor.A * (ceiling - x) + colorCeiling.A * (x - floor)),
                (byte) (colorFloor.R * (ceiling - x) + colorCeiling.R * (x - floor)),
                (byte) (colorFloor.G * (ceiling - x) + colorCeiling.G * (x - floor)),
                (byte) (colorFloor.B * (ceiling - x) + colorCeiling.B * (x - floor)));
        }
    }
}

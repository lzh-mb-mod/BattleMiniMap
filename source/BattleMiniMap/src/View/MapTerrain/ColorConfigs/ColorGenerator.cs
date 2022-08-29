using System.Linq;
using TaleWorlds.Library;
using Color = System.Drawing.Color;

namespace BattleMiniMap.View.MapTerrain.ColorConfigs
{
    public class ColorPoint
    {
        public Color Color { get; }
        public float Weight { get; }

        public ColorPoint(Color color, float weight = 1)
        {
            Color = color;
            Weight = weight;
        }
    }

    public class ColorGenerator
    {
        private readonly ColorPoint[] _colors;
        private readonly float _weightSum;
        public ColorGenerator(ColorPoint[] colors)
        {
            _colors = colors;
            _weightSum = colors.Sum(color => color.Weight);
        }

        public Color GetColor(float progress)
        {
            var count = _colors.Length;
            if (progress < 0)
                return _colors[0].Color;
            if (progress >= 1)
                return _colors[count - 1].Color;
            var remainingWeight = progress * _weightSum;
            for (int i = 0; i < _colors.Length - 1; ++i)
            {
                var weight = _colors[i].Weight;
                if (remainingWeight < weight)
                {
                    var t = remainingWeight / weight;
                    var colorFloor = _colors[i];
                    var colorCeiling = _colors[i + 1];
                    return Color.FromArgb((byte)(colorFloor.Color.A * (1 - t) + colorCeiling.Color.A * t),
                        (byte)(colorFloor.Color.R * (1 - t) + colorCeiling.Color.R * t),
                        (byte)(colorFloor.Color.G * (1 - t) + colorCeiling.Color.G * t),
                        (byte)(colorFloor.Color.B * (1 - t) + colorCeiling.Color.B * t));
                }

                remainingWeight -= weight;
            }

            return _colors[count - 1].Color;
        }
    }
}

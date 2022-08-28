using System;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.MapTerrain
{
    public interface IMiniMap
    {
        public Texture MapTexture { get; }
        public int BitmapWidth { get; }
        public int BitmapHeight { get; }

        public Vec2 MapBoundMin { get; }
        public Vec2 MapBoundMax { get; }
        public float WaterLevel { get; }
        public float Resolution { get; }
        public float EdgeOpacityFactor { get; }
        bool IsValid { get; }
        bool ExcludeUnwalkableTerrain { get; }
        void InitializeMapRange(Mission mission, bool updateMap = false);
        void UpdateMapSize(Mission mission, bool updateMap = false);
        void UpdateMapImage(Mission mission);
        void UpdateEdgeOpacity();
        int GetEdgeAlpha(int w, int h, float edgeOpacityFactor);
    }

    public static class MiniMap
    {
        private static IMiniMap _instance;

        private static bool _isFading = false;
        private static bool _isFadeIn = false;
        private static float _fadeInOutInvertedProgress = 0;
        private static bool _isFadingCompleted;

        public static IMiniMap Instance => _instance ??= new GdiMiniMap();

        public static float FadeInOutAlphaFactor = 1;

        public static void SetFadeIn()
        {
            if (!_isFading)
            {
                _fadeInOutInvertedProgress = 1;
                _isFading = true;
                _isFadeIn = true;
                _isFadingCompleted = false;
            }
            else
            {
                if (!_isFadeIn)
                {
                    _isFadeIn = true;
                    _fadeInOutInvertedProgress = 1 - _fadeInOutInvertedProgress;
                    _isFadingCompleted = false;
                }
            }
        }

        public static void SetFadeOut()
        {
            if (!_isFading)
            {
                _fadeInOutInvertedProgress = 1;
                _isFading = true;
                _isFadeIn = false;
                _isFadingCompleted = false;
            }
            else
            {
                if (_isFadeIn)
                {
                    _isFadeIn = false;
                    _fadeInOutInvertedProgress = 1 - _fadeInOutInvertedProgress;
                    _isFadingCompleted = false;
                }
            }
        }

        public static bool IsFadingCompleted()
        {
            var result = _isFadingCompleted;
            _isFadingCompleted = false;
            return result;
        }

        public static bool IsFadingOut()
        {
            return _isFading && !_isFadeIn;
        }

        public static void UpdateFading(float dt)
        {
            if (_isFading)
            {
                _fadeInOutInvertedProgress *= (float)Math.Pow(0.001, dt);
                if (_fadeInOutInvertedProgress < 0.001f)
                {
                    _fadeInOutInvertedProgress = 0;
                    _isFading = false;
                    _isFadingCompleted = true;
                }

                FadeInOutAlphaFactor = _isFadeIn ? 1 - _fadeInOutInvertedProgress : _fadeInOutInvertedProgress;
            }
        }
    }
}
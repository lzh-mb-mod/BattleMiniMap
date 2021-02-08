using System;
using System.Collections.Generic;
using BattleMiniMap.View.MapTerrain;
using MissionSharedLibrary.Config;
using System.IO;
using System.Linq;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.Config
{
    public enum KeyMode
    {
        Toggle, LongPress, Count
    }

    public struct ConfigColor
    {
        public ConfigColor(int alpha, int red, int green, int blue)
        {
            Alpha = alpha;
            Red = red;
            Green = green;
            Blue = blue;
        }

        public int Alpha { get; set; }
        public int Red { get; set; }
        public int Green { get; set; }
        public int Blue { get; set; }
    }

    public class BattleMiniMapConfig : MissionConfigBase<BattleMiniMapConfig>
    {
        private static ConfigColor CreateColor(int alpha, int red, int green, int blue)
        {
            return new ConfigColor(alpha, red, green, blue);
        }

        protected static Version BinaryVersion => new Version(1, 0);
        public string ConfigVersion { get; set; } = BinaryVersion.ToString();

        public static List<ConfigColor> DefaultAboveColor = new List<ConfigColor>
        {
            CreateColor(255, 60, 90, 60),
            CreateColor(255, 80, 130, 80),
            CreateColor(255, 105, 150, 80),
            CreateColor(255, 130, 170, 80),
            CreateColor(255, 160, 180, 90),
            CreateColor(255, 200, 180, 90),
            CreateColor(255, 230, 180, 90),
            CreateColor(255, 202, 135, 80),
            CreateColor(255, 187, 108, 80),
            CreateColor(255, 169, 84, 80),
            CreateColor(255, 145, 80, 70),
            CreateColor(255, 125, 80, 90),
            CreateColor(255, 105, 80, 120),
            CreateColor(255, 125, 80, 120),
            CreateColor(255, 145, 90, 150),
            CreateColor(255, 160, 90, 180),
            CreateColor(255, 180, 100, 200),
            CreateColor(255, 180, 120, 220),
            CreateColor(255, 200, 150, 240),
            CreateColor(255, 220, 190, 240),
        };

        public static List<ConfigColor> DefaultBelowColor = new List<ConfigColor>
        {
            CreateColor(255, 202, 248, 255),
            CreateColor(255, 99, 248, 255),
            CreateColor(255, 73, 182, 255),
            CreateColor(255, 26, 125, 198),
            CreateColor(255, 48, 96, 198),
        };

        public List<ConfigColor> AboveWater;

        public List<ConfigColor> BelowWater;

        public bool ShowMap { get; set; } = true;

        public bool EnableToggleMapLongPressKey { get; set; } = true;

        public bool ShowMapWhenCommanding { get; set; } = true;
        public int WidgetWidth { get; set; } = 400;

        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        public int PositionX { get; set; } = 10;

        public int PositionY { get; set; } = 10;

        public float Resolution { get; set; } = 1;

        public float AgentMarkerScale { get; set; } = 1;

        public float EdgeOpacityFactor { get; set; } = 0.5f;

        public float BackgroundOpacity { get; set; } = 0.5f;

        public float ForegroundOpacity { get; set; } = 0.8f;

        public bool ExcludeUnwalkableTerrain { get; set; } = false;

        protected override string SaveName { get; } = Path.Combine(ConfigPath.ConfigDir,
            BattleMiniMapSubModule.ModuleId, nameof(BattleMiniMapConfig) + ".xml");

        public override bool Deserialize()
        {
            var result = base.Deserialize();

            Serialize();

            return result;
        }

        public override bool Serialize()
        {
            if (AboveWater == null || AboveWater.Count == 0)
            {
                AboveWater = DefaultAboveColor.ToList();
            }

            if (BelowWater == null || AboveWater.Count == 0)
            {
                BelowWater = DefaultBelowColor.ToList();
            }
            return base.Serialize();
        }

        protected override void CopyFrom(BattleMiniMapConfig other)
        {
            ConfigVersion = other.ConfigVersion;
            AboveWater = other.AboveWater;
            BelowWater = other.BelowWater;
            ShowMap = other.ShowMap;
            EnableToggleMapLongPressKey = other.EnableToggleMapLongPressKey;
            ShowMapWhenCommanding = other.ShowMapWhenCommanding;
            WidgetWidth = other.WidgetWidth;
            HorizontalAlignment = other.HorizontalAlignment;
            VerticalAlignment = other.VerticalAlignment;
            PositionX = other.PositionX;
            PositionY = other.PositionY;
            Resolution = other.Resolution;
            AgentMarkerScale = other.AgentMarkerScale;
            EdgeOpacityFactor = other.EdgeOpacityFactor;
            BackgroundOpacity = other.BackgroundOpacity;
            ForegroundOpacity = other.ForegroundOpacity;
            ExcludeUnwalkableTerrain = other.ExcludeUnwalkableTerrain;
        }

        protected override void UpgradeToCurrentVersion()
        {
        }

        public static void OnMenuClosed()
        {
            if (Math.Abs(MiniMap.Instance.Resolution - Get().Resolution) > 0.001f)
                MiniMap.Instance.UpdateMapSize(Mission.Current, true);
            else if (MiniMap.Instance.ExcludeUnwalkableTerrain != Get().ExcludeUnwalkableTerrain)
                MiniMap.Instance.UpdateMapImage(Mission.Current);
            else if (Math.Abs(MiniMap.Instance.EdgeOpacityFactor - Get().EdgeOpacityFactor) > 0.001f)
                MiniMap.Instance.UpdateEdgeOpacity();
            Get().Serialize();
        }
    }
}
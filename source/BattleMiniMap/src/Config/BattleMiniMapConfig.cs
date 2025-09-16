﻿using BattleMiniMap.View.MapTerrain;
using MissionSharedLibrary.Config;
using System;
using System.IO;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.Config
{
    public enum KeyMode
    {
        Toggle, LongPress, Count
    }

    public class BattleMiniMapConfig : MissionConfigBase<BattleMiniMapConfig>
    {
        public static float DynamicScale = 1;

        public static float DynamicOpacityExponent = 1;

        protected static Version BinaryVersion => new Version(1, 1);
        public string ConfigVersion { get; set; } = BinaryVersion.ToString();

        public bool ShowMap { get; set; } = true;

        public bool EnableToggleMapLongPressKey { get; set; } = true;

        public bool ToggleMapWhenCommanding { get; set; } = true;
        public int WidgetWidth { get; set; } = 400;

        public bool FollowMode { get; set; } = true;

        public float FollowModeScale { get; set; } = 0.5f;

        public bool EnableDynamicScale { get; set; } = true;

        public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Left;

        public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Top;

        public int PositionX { get; set; } = 10;

        public int PositionY { get; set; } = 10;

        public float Resolution { get; set; } = 1;

        public float AgentMarkerScale { get; set; } = 1;
        public float HeroMarkerScale { get; set; } = 1;

        public float EdgeOpacityFactor { get; set; } = 0.5f;

        public float BackgroundOpacity { get; set; } = 0.7f;

        public float ForegroundOpacity { get; set; } = 0.5f;

        public bool ExcludeUnwalkableTerrain { get; set; } = false;

        public float GetBackgroundOpacity()
        {
            return MathF.Clamp(MathF.Pow(BackgroundOpacity, DynamicOpacityExponent), 0f, 1f);
        }

        public float GetForegroundOpacity()
        {
            return MathF.Clamp(MathF.Pow(ForegroundOpacity, DynamicOpacityExponent), 0f, 1f);
        }

        public float GetFollowModeScale()
        {
            return MathF.Clamp(FollowModeScale * DynamicScale, 0.1f, 3f);
        }

        protected override string SaveName { get; } = Path.Combine(ConfigPath.ConfigDir,
            BattleMiniMapSubModule.ModuleId, nameof(BattleMiniMapConfig) + ".xml");

        protected override void CopyFrom(BattleMiniMapConfig other)
        {
            ConfigVersion = other.ConfigVersion;
            ShowMap = other.ShowMap;
            EnableToggleMapLongPressKey = other.EnableToggleMapLongPressKey;
            ToggleMapWhenCommanding = other.ToggleMapWhenCommanding;
            WidgetWidth = other.WidgetWidth;
            FollowMode = other.FollowMode;
            FollowModeScale = other.FollowModeScale;
            EnableDynamicScale = other.EnableDynamicScale;
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
            switch (ConfigVersion)
            {
                default:
                    ResetToDefault();
                    Serialize();
                    goto case "1.0";
                case "1.0":
                    if (ShowMap)
                        FollowMode = true;
                    break;
                case "1.1":
                    break;
            }
            
            ConfigVersion = BinaryVersion.ToString(2);
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
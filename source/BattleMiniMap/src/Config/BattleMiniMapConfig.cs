using System;
using System.IO;
using BattleMiniMap.View.Map;
using MissionSharedLibrary.Config;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap.Config
{
    public class BattleMiniMapConfig : MissionConfigBase<BattleMiniMapConfig>
    {
        protected static Version BinaryVersion => new Version(1, 0);
        public string ConfigVersion { get; set; } = BinaryVersion.ToString();

        public float Resolution { get; set; } = 1;
        public int MapWidth { get; set; } = 400;

        protected override void CopyFrom(BattleMiniMapConfig other)
        {
            ConfigVersion = other.ConfigVersion;
            Resolution = other.Resolution;
            MapWidth = other.MapWidth;
        }

        protected override void UpgradeToCurrentVersion()
        {
        }

        public static void OnMenuClosed()
        {
            if (Math.Abs(MiniMap.Instance.Resolution - Get().Resolution) > 0.001f) 
                MiniMap.Instance.UpdateMapImage(Mission.Current);
            Get().Serialize();
        }

        protected override string SaveName { get; } = Path.Combine(ConfigPath.ConfigDir,
            BattleMiniMapSubModule.ModuleId, nameof(BattleMiniMapConfig) + ".xml");
    }
}

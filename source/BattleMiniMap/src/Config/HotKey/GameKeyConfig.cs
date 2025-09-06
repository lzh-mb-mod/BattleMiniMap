using MissionSharedLibrary.Config;
using MissionSharedLibrary.Config.HotKey;
using System;
using System.IO;

namespace BattleMiniMap.Config.HotKey
{
    public class GameKeyConfig : GameKeyConfigBase<GameKeyConfig>
    {
        protected override string SaveName { get; } =
            Path.Combine(ConfigPath.ConfigDir, BattleMiniMapSubModule.ModuleId, nameof(GameKeyConfig) + ".xml");

        protected static Version BinaryVersion => new Version(1, 1);

        public string ConfigVersion = BinaryVersion.ToString(2);

        protected override void CopyFrom(GameKeyConfig other)
        {
            base.CopyFrom(other);
            ConfigVersion = other.ConfigVersion;
        }

        protected override void UpgradeToCurrentVersion()
        {
            switch (ConfigVersion)
            {
                default:
                    ResetToDefault();
                    Serialize();
                    goto case "1.1";
                case "1.1":
                    break;
            }

            ConfigVersion = BinaryVersion.ToString(2);
        }
    }
}

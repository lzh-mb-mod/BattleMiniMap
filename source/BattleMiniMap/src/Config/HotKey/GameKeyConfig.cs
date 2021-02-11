using MissionSharedLibrary.Config;
using MissionSharedLibrary.Config.HotKey;
using System.IO;

namespace BattleMiniMap.Config.HotKey
{
    public class GameKeyConfig : GameKeyConfigBase<GameKeyConfig>
    {
        protected override string SaveName { get; } =
            Path.Combine(ConfigPath.ConfigDir, BattleMiniMapSubModule.ModuleId, nameof(GameKeyConfig) + ".xml");
    }
}

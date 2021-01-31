using System.IO;
using MissionSharedLibrary.Config;
using MissionSharedLibrary.Config.HotKey;

namespace BattleMiniMap.Config.HotKey
{
    public class GameKeyConfig : GameKeyConfigBase<GameKeyConfig>
    {
        protected override string SaveName { get; } =
            Path.Combine(ConfigPath.ConfigDir, BattleMiniMapSubModule.ModuleId, nameof(GameKeyConfig) + ".xml");
    }
}

using MissionLibrary.HotKey;
using MissionLibrary.Provider;
using MissionSharedLibrary.Config.HotKey;
using MissionSharedLibrary.HotKey;
using MissionSharedLibrary.Provider;
using System;
using System.Collections.Generic;
using TaleWorlds.InputSystem;

namespace BattleMiniMap.Config.HotKey
{
    public enum GameKeyEnum
    {
        ToggleMap,
        ToggleMapLongPress,
        NumberOfGameKeyEnums
    }
    public class BattleMiniMapGameKeyCategory
    {
        public const string CategoryId = "BattleMiniMapHotKey";

        public static AGameKeyCategory Category => AGameKeyCategoryManager.Get().GetItem(CategoryId);

        public static void RegisterGameKeyCategory()
        {
            AGameKeyCategoryManager.Get()?.RegisterItem(CreateCategory());
        }

        public static IProvider<AGameKeyCategory> CreateCategory()
        {
            return ProviderCreator.Create(() =>
            {
                var result = new GameKeyCategory(CategoryId,
                    (int)GameKeyEnum.NumberOfGameKeyEnums, GameKeyConfig.Get());
                result.AddGameKeySequence(new GameKeySequence((int)GameKeyEnum.ToggleMap, nameof(GameKeyEnum.ToggleMap),
                    CategoryId, new List<InputKey>
                    {
                    InputKey.M
                    }));
                result.AddGameKeySequence(new GameKeySequence((int)GameKeyEnum.ToggleMapLongPress,
                    nameof(GameKeyEnum.ToggleMapLongPress),
                    CategoryId, new List<InputKey>
                    {
                    InputKey.LeftAlt
                    }));
                return result;
            }, CategoryId, new Version(1, 0));
        }

        public static IGameKeySequence GetKey(GameKeyEnum key)
        {
            return Category?.GetGameKeySequence((int) key);
        }
    }
}

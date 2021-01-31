using System;
using MissionLibrary.HotKey;
using MissionSharedLibrary.HotKey.Category;
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

        public static AGameKeyCategory Category => AGameKeyCategoryManager.Get().GetCategory(CategoryId);

        public static void RegisterGameKeyCategory()
        {
            AGameKeyCategoryManager.Get()?.AddCategory(CreateCategory, new Version(1, 0));
        }

        public static GameKeyCategory CreateCategory()
        {
            var result = new GameKeyCategory(CategoryId,
                (int)GameKeyEnum.NumberOfGameKeyEnums, GameKeyConfig.Get());
            result.AddGameKey(new GameKey((int)GameKeyEnum.ToggleMap, nameof(GameKeyEnum.ToggleMap),
                CategoryId, InputKey.M, CategoryId));
            result.AddGameKey(new GameKey((int)GameKeyEnum.ToggleMapLongPress, nameof(GameKeyEnum.ToggleMapLongPress),
                CategoryId, InputKey.LeftAlt, CategoryId));
            return result;
        }

        public static InputKey GetKey(GameKeyEnum key)
        {
            return Category?.GetKey((int) key) ?? InputKey.Invalid;
        }
    }
}

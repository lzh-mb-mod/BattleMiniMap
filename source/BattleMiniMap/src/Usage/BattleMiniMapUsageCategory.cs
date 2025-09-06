using BattleMiniMap.Config.HotKey;
using MissionLibrary.Usage;
using MissionSharedLibrary.HotKey;
using MissionSharedLibrary.Usage;
using System;
using System.Collections.Generic;
using TaleWorlds.Core;

namespace BattleMiniMap.Usage
{
    public class BattleMiniMapUsageCategory
    {
        public const string CategoryId = "BattleMiniMapUsage";

        public static AUsageCategory Category => AUsageCategoryManager.Get().GetItem(CategoryId);

        public static void RegisterUsageCategory()
        {
            AUsageCategoryManager.Get()?.RegisterItem(CreateCategory, CategoryId, new Version(1, 0));
        }

         public static UsageCategory CreateCategory()
        {
            var usageCategoryData = new UsageCategoryData(
                GameTexts.FindText("str_battle_mini_map_option_class"),
                new List<TaleWorlds.Localization.TextObject>
                {
                    GameTexts.FindText("str_mission_library_open_menu_hint").SetTextVariable("KeyName",
                        GeneralGameKeyCategory.GetKey(GeneralGameKey.OpenMenu).ToSequenceString()),
                    GameTexts.FindText("str_battle_mini_map_toggle_map_usage").SetTextVariable("KeyName",
                        BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMap).ToSequenceString()),
                    GameTexts.FindText("str_battle_mini_map_toggle_map_holding_usage").SetTextVariable("KeyName",
                        BattleMiniMapGameKeyCategory.GetKey(GameKeyEnum.ToggleMapHolding ).ToSequenceString()),
                });

            return new UsageCategory(CategoryId, usageCategoryData);
        }
    }
}

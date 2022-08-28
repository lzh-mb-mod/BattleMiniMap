using MissionLibrary.Provider;
using MissionLibrary.View;
using MissionSharedLibrary.Provider;
using MissionSharedLibrary.View.ViewModelCollection;
using MissionSharedLibrary.View.ViewModelCollection.Options;
using MissionSharedLibrary.View.ViewModelCollection.Options.Selection;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.Localization;

namespace BattleMiniMap.Config
{
    public class BattleMiniMapOptionClassFactory
    {
        public static IIdProvider<AOptionClass> CreateOptionClassProvider(IMenuClassCollection menuClassCollection)
        {
            return IdProviderCreator.Create(() =>
            {
                var optionClass = new OptionClass(BattleMiniMapSubModule.ModuleId,
                    GameTexts.FindText("str_battle_mini_map_option_class"), menuClassCollection);

                var optionCategory = new OptionCategory("Map", GameTexts.FindText("str_battle_mini_map_map_options"));
                optionCategory.AddOption(new BoolOptionViewModel(GameTexts.FindText("str_battle_mini_map_show_map"),
                    null, () => BattleMiniMapConfig.Get().ShowMap, b => BattleMiniMapConfig.Get().ShowMap = b));
                optionCategory.AddOption(new BoolOptionViewModel(GameTexts.FindText("str_battle_mini_map_enable_toggle_map_long_press_key"),
                    null, () => BattleMiniMapConfig.Get().EnableToggleMapLongPressKey, b => BattleMiniMapConfig.Get().EnableToggleMapLongPressKey = b));
                optionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_toggle_map_when_commanding"), null,
                    () => BattleMiniMapConfig.Get().ToggleMapWhenCommanding,
                    b => BattleMiniMapConfig.Get().ToggleMapWhenCommanding = b));
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_map_width"),
                    null,
                    () =>
                        BattleMiniMapConfig.Get().WidgetWidth, f => { BattleMiniMapConfig.Get().WidgetWidth = (int) f; }, 50,
                    2000, true, true));

                optionCategory.AddOption(new SelectionOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_horizontal_alignment"), null, new SelectionOptionData(
                        i => BattleMiniMapConfig.Get().HorizontalAlignment = (HorizontalAlignment) i,
                        () => (int) BattleMiniMapConfig.Get().HorizontalAlignment, 3, new SelectionItem[]
                        {
                            new SelectionItem(true, "str_battle_min_map_left"),
                            new SelectionItem(true, "str_battle_min_map_center"),
                            new SelectionItem(true, "str_battle_min_map_right")
                        }), false));
                optionCategory.AddOption(new SelectionOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_vertical_alignment"), null, new SelectionOptionData(
                        i => BattleMiniMapConfig.Get().VerticalAlignment = (VerticalAlignment) i,
                        () => (int) BattleMiniMapConfig.Get().VerticalAlignment, 3, new SelectionItem[]
                        {
                            new SelectionItem(true, "str_battle_min_map_top"),
                            new SelectionItem(true, "str_battle_min_map_center"),
                            new SelectionItem(true, "str_battle_min_map_bottom")
                        }), false));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_position_x"), null,
                    () => BattleMiniMapConfig.Get().PositionX, f => BattleMiniMapConfig.Get().PositionX = (int) f, 0,
                    1000, true, true));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_position_y"), null,
                    () => BattleMiniMapConfig.Get().PositionY, f => BattleMiniMapConfig.Get().PositionY = (int)f, 0,
                    1000, true, true));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_map_resolution"), null,
                    () => BattleMiniMapConfig.Get().Resolution,
                    f => { BattleMiniMapConfig.Get().Resolution = f; }, 0.5f, 50, false, true));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_map_scale"), null,
                    () => BattleMiniMapConfig.Get().FollowModeScale,
                    f => BattleMiniMapConfig.Get().FollowModeScale = f, 0.1f, 3, false, true));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_agent_marker_scale"), null,
                    () => BattleMiniMapConfig.Get().AgentMarkerScale,
                    f => BattleMiniMapConfig.Get().AgentMarkerScale = f, 0.2f, 5f, false, true));
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_edge_opacity_factor"),
                    null, () => BattleMiniMapConfig.Get().EdgeOpacityFactor, f => BattleMiniMapConfig.Get().EdgeOpacityFactor = f,
                    0, 1, false, true));
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_background_opacity"),
                    null, () => BattleMiniMapConfig.Get().BackgroundOpacity, f => BattleMiniMapConfig.Get().BackgroundOpacity = f,
                    0, 1, false, true));
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_foreground_opacity"),
                    null, () => BattleMiniMapConfig.Get().ForegroundOpacity, f => BattleMiniMapConfig.Get().ForegroundOpacity = f,
                    0, 1, false, true));
                optionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_exclude_unwalkable_terrain"), null,
                    () => BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain,
                    b =>
                    {
                        BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain = b;
                    }));
                optionClass.AddOptionCategory(0, optionCategory);
                return optionClass;
            }, BattleMiniMapSubModule.ModuleId);
        }
    }
}
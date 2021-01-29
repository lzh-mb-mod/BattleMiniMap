using BattleMiniMap.View.MapTerrain;
using MissionLibrary.Provider;
using MissionLibrary.View;
using MissionSharedLibrary.Provider;
using MissionSharedLibrary.View.ViewModelCollection;
using MissionSharedLibrary.View.ViewModelCollection.Options;
using MissionSharedLibrary.View.ViewModelCollection.Options.Selection;
using TaleWorlds.Core;
using TaleWorlds.GauntletUI;
using TaleWorlds.MountAndBlade;

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
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_opacity"),
                    null, () => BattleMiniMapConfig.Get().Opacity, f => BattleMiniMapConfig.Get().Opacity = f,
                    0, 1, false, true));
                optionCategory.AddOption(new BoolOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_exclude_unwalkable_terrain"), null,
                    () => BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain,
                    b =>
                    {
                        BattleMiniMapConfig.Get().ExcludeUnwalkableTerrain = b;
                        MiniMap.Instance.UpdateMapImage(Mission.Current);
                    }));
                optionClass.AddOptionCategory(0, optionCategory);
                return optionClass;
            }, BattleMiniMapSubModule.ModuleId);
        }
    }
}
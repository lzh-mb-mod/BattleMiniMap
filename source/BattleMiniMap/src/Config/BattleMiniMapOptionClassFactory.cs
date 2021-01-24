using BattleMiniMap.View.Map;
using MissionLibrary.Provider;
using MissionLibrary.View;
using MissionSharedLibrary.Provider;
using MissionSharedLibrary.View.ViewModelCollection;
using MissionSharedLibrary.View.ViewModelCollection.Options;
using TaleWorlds.Core;
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
                optionCategory.AddOption(new NumericOptionViewModel(GameTexts.FindText("str_battle_mini_map_map_width"),
                    null,
                    () =>
                        BattleMiniMapConfig.Get().MapWidth, f =>
                    {
                        BattleMiniMapConfig.Get().MapWidth = (int)f;
                    }, 50, 1000, true, true));
                optionCategory.AddOption(new NumericOptionViewModel(
                    GameTexts.FindText("str_battle_mini_map_map_resolution"), null,
                    () => BattleMiniMapConfig.Get().Resolution,
                    f =>
                    {
                        BattleMiniMapConfig.Get().Resolution = f;
                    }, 0.5f, 50, false, true));
                optionClass.AddOptionCategory(0, optionCategory);
                return optionClass;
            }, BattleMiniMapSubModule.ModuleId);
        }
    }
}

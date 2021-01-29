using BattleMiniMap.Config;
using BattleMiniMap.Logic;
using MissionLibrary;
using MissionLibrary.Controller;
using MissionLibrary.View;
using MissionSharedLibrary;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace BattleMiniMap
{
    public class BattleMiniMapSubModule : MBSubModuleBase
    {
        public static readonly string ModuleId = "BattleMiniMap";

        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();


            Initialize();
            Module.CurrentModule.GlobalTextManager.LoadGameTexts(
                BasePath.Name + $"Modules/{ModuleId}/ModuleData/module_strings.xml");
            Module.CurrentModule.GlobalTextManager.LoadGameTexts(
                BasePath.Name + $"Modules/{ModuleId}/ModuleData/MissionLibrary.xml");
        }

        private void Initialize()
        {
            if (!Initializer.Initialize(ModuleId))
                return;
        }

        protected override void OnBeforeInitialModuleScreenSetAsRoot()
        {
            base.OnBeforeInitialModuleScreenSetAsRoot();

            if (!SecondInitialize())
                return;
        }

        private bool SecondInitialize()
        {
            if (!Initializer.SecondInitialize())
                return false;

            Global.GetProvider<AMissionStartingManager>().AddHandler(new MissionStartingHandler());
            var menuClassCollection = AMenuManager.Get().MenuClassCollection;
            AMenuManager.Get().OnMenuClosedEvent += BattleMiniMapConfig.OnMenuClosed;
            menuClassCollection.AddOptionClass(
                BattleMiniMapOptionClassFactory.CreateOptionClassProvider(menuClassCollection));
            return true;
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            game.GameTextManager.LoadGameTexts(BasePath.Name + $"Modules/{ModuleId}/ModuleData/module_strings.xml");
            game.GameTextManager.LoadGameTexts(BasePath.Name + $"Modules/{ModuleId}/ModuleData/MissionLibrary.xml");
        }
    }
}
using BattleMiniMap.Config;
using BattleMiniMap.Config.HotKey;
using BattleMiniMap.Logic;
using MissionLibrary;
using MissionLibrary.Controller;
using MissionLibrary.View;
using MissionSharedLibrary;
using TaleWorlds.Core;
using TaleWorlds.ModuleManager;
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
                ModuleHelper.GetXmlPath(ModuleId, "module_strings"));
            Module.CurrentModule.GlobalTextManager.LoadGameTexts(
                ModuleHelper.GetXmlPath(ModuleId, "MissionLibrary"));
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
            BattleMiniMapGameKeyCategory.RegisterGameKeyCategory();
            var menuClassCollection = AMenuManager.Get().MenuClassCollection;
            AMenuManager.Get().OnMenuClosedEvent += BattleMiniMapConfig.OnMenuClosed;
            menuClassCollection.AddOptionClass(
                BattleMiniMapOptionClassFactory.CreateOptionClassProvider(menuClassCollection));
            return true;
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            game.GameTextManager.LoadGameTexts(ModuleHelper.GetXmlPath(ModuleId, "module_strings"));
            game.GameTextManager.LoadGameTexts(ModuleHelper.GetXmlPath(ModuleId, "MissionLibrary"));
        }
    }
}
﻿using BattleMiniMap.Config;
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

            Module.CurrentModule.GlobalTextManager.LoadGameTexts();
            Initialize();
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

            Global.GetInstance<AMissionStartingManager>().AddHandler(new MissionStartingHandler());
            BattleMiniMapGameKeyCategory.RegisterGameKeyCategory();
            var menuClassCollection = AMenuManager.Get().MenuClassCollection;
            AMenuManager.Get().OnMenuClosedEvent += BattleMiniMapConfig.OnMenuClosed;
            menuClassCollection.RegisterItem(
                BattleMiniMapOptionClassFactory.CreateOptionClassProvider(menuClassCollection));
            return true;
        }

        protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
        {
            base.OnGameStart(game, gameStarterObject);

            game.GameTextManager.LoadGameTexts();
        }
    }
}
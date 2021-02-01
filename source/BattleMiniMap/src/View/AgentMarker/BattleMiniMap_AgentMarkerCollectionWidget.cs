﻿using System;
using System.Collections.Generic;
using System.Linq;
using BattleMiniMap.Config;
using BattleMiniMap.View.AgentMarker.Colors;
using BattleMiniMap.View.AgentMarker.TextureProviders;
using TaleWorlds.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.AgentMarker
{
    public class BattleMiniMap_AgentMarkerCollectionWidget : BrushWidget
    {
        private DrawObject2D _cachedMesh;

        public BattleMiniMap_AgentMarkerCollectionWidget(UIContext context) : base(context)
        {
            WidthSizePolicy = HeightSizePolicy = SizePolicy.StretchToParent;
        }

        public List<AgentMarkerViewModel> AgentMakers { get; set; }

        protected override void OnRender(TwoDimensionContext twoDimensionContext, TwoDimensionDrawContext drawContext)
        {
            base.OnRender(twoDimensionContext, drawContext);

            var size = Widgets.Utility.GetSize(this);
            var width = Math.Max(size.x * 0.01f, 1);
            var height = Math.Max(size.x * 0.01f, 1);
            UpdateDrawObject2D(width, height);
            var materials = new SimpleMaterial[(int)AgentMarkerType.Count];
            var globalPosition = Widgets.Utility.GetGlobalPosition(this);
            
            foreach (var agentMaker in AgentMakers)
            {
                var type = agentMaker.AgentMarkerType;
                twoDimensionContext.Draw(globalPosition.x + agentMaker.Position.x - width * 0.5f, globalPosition.y + agentMaker.Position.y - height * 0.5f, materials[(int)type] ??= CreateMaterial(drawContext, type), _cachedMesh, type.GetLayer());
            }
        }

        private SimpleMaterial CreateMaterial(TwoDimensionDrawContext drawContext, AgentMarkerType type)
        {
            var types = type.GetColorAndTextureType();
            var material = Widgets.Utility.CreateMaterial(drawContext, this);
            material.Texture = types.Item2.GetTexture();
            material.Color *= types.Item1.GetColor();
            return material;
        }

        private void UpdateDrawObject2D(float width, float height)
        {
            if (_cachedMesh == null || Math.Abs(_cachedMesh.Width - width) > 0.01f && Math.Abs(_cachedMesh.Height - height) > 0.01f)
            {
                _cachedMesh = Widgets.Utility.CreateDrawObject2D(width, height);
            }
        }
    }
}
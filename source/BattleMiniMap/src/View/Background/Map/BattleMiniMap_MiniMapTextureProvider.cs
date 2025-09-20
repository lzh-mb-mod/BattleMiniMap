﻿using BattleMiniMap.View.Image;
using BattleMiniMap.View.MapTerrain;
using TaleWorlds.GauntletUI;
using TaleWorlds.TwoDimension;

namespace BattleMiniMap.View.Background.Map
{
    public class BattleMiniMap_MiniMapTextureProvider : TextureProvider
    {

        protected override Texture OnGetTextureForRender(TwoDimensionContext twoDimensionContext, string name)
        {
            //return SimpleMiniMap.Instance?.MapTexture ?? new ImageRGBA(100, 100).CreateTexture();
            return MiniMap.Instance?.MapTexture ?? new ImageRGBA(100, 100).CreateTexture();
        }
    }
}
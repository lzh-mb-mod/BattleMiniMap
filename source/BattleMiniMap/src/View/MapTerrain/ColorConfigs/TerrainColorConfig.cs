using System.Drawing;

namespace BattleMiniMap.View.MapTerrain.ColorConfigs
{
    public class TerrainColorConfig
    {
        //public static TerrainColorConfig Default = new TerrainColorConfig()
        //{
        //    TerrainColor = new ColorGenerator(new Color[]
        //    {
        //        Color.FromArgb(255, 60, 90, 60),
        //        Color.FromArgb(255, 80, 130, 80),
        //        Color.FromArgb(255, 105, 150, 80),
        //        Color.FromArgb(255, 130, 170, 80),
        //        Color.FromArgb(255, 160, 180, 90),
        //        Color.FromArgb(255, 200, 180, 90),
        //        Color.FromArgb(255, 230, 180, 90),
        //        Color.FromArgb(255, 202, 135, 80),
        //        Color.FromArgb(255, 187, 108, 80),
        //        Color.FromArgb(255, 169, 84, 80),
        //        Color.FromArgb(255, 145, 80, 70),
        //        Color.FromArgb(255, 125, 80, 90),
        //        Color.FromArgb(255, 105, 80, 120),
        //        Color.FromArgb(255, 125, 80, 120),
        //        Color.FromArgb(255, 145, 90, 150),
        //        Color.FromArgb(255, 160, 90, 180),
        //        Color.FromArgb(255, 180, 100, 200),
        //        Color.FromArgb(255, 180, 120, 220),
        //        Color.FromArgb(255, 200, 150, 240),
        //        Color.FromArgb(255, 220, 190, 240),
        //    }),
        //    WaterColor = new ColorGenerator(new Color[]
        //    {
        //        Color.FromArgb(255, 202, 248, 255),
        //        Color.FromArgb(255, 99, 248, 255),
        //        Color.FromArgb(255, 73, 182, 255),
        //        Color.FromArgb(255, 26, 125, 198),
        //        Color.FromArgb(255, 48, 96, 198),
        //    })
        //};

        //public static TerrainColorConfig LowSaturation1 = new TerrainColorConfig()
        //{
        //    TerrainColor = new ColorGenerator(new Color[]
        //    {
        //        Color.FromArgb(255, 41, 51, 44),    // hsv=140, 20, 20
        //        Color.FromArgb(255, 45, 59, 48),    // hsv=134, 23, 23
        //        Color.FromArgb(255, 49, 66, 51),    // hsv=128, 26, 26
        //        Color.FromArgb(255, 53, 74, 53),    // hsv=122, 29, 29
        //        Color.FromArgb(255, 57, 82, 55),    // hsv=116, 32, 32
        //        Color.FromArgb(255, 68, 89, 63),    // hsv=110, 29, 35
        //        Color.FromArgb(255, 78, 97, 72),    // hsv=104, 26, 38
        //        Color.FromArgb(255, 89, 105, 81),    // hsv=98, 23, 41
        //        Color.FromArgb(255, 100, 112, 90),    // hsv=92, 20, 44
        //        Color.FromArgb(255, 108, 120, 92),    // hsv=86, 23, 47

        //        Color.FromArgb(255, 102, 112, 83),    // hsv=80, 26, 44
        //        Color.FromArgb(255, 97, 105, 74),    // hsv=74, 29, 41
        //        Color.FromArgb(255, 94, 97, 72),    // hsv=68, 26,38
        //        Color.FromArgb(255, 89, 89, 69),    // hsv=62, 23, 35
        //        Color.FromArgb(255, 82, 81, 65),    // hsv=56, 20, 32
        //        Color.FromArgb(255, 74, 71, 57),    // hsv=50, 23, 29
        //        Color.FromArgb(255, 66, 62, 49),    // hsv=44, 26, 26
        //        Color.FromArgb(255, 59, 52, 42),    // hsv=38, 29, 23
        //        Color.FromArgb(255, 51, 43, 35),    // hsv=32, 32, 20
        //        Color.FromArgb(255, 59, 47, 38),    // hsv=26, 35, 23
        //    }),
        //    WaterColor = new ColorGenerator(new Color[]
        //    {
        //        Color.FromArgb(255, 202, 248, 255),
        //        Color.FromArgb(255, 99, 248, 255),
        //        Color.FromArgb(255, 73, 182, 255),
        //        Color.FromArgb(255, 26, 125, 198),
        //        Color.FromArgb(255, 48, 96, 198),
        //    })
        //};

        public static TerrainColorConfig LowSaturation2 = new TerrainColorConfig()
        {
            TerrainColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 59, 74, 59), 61),    // hsv: 120, 120, 30
                new ColorPoint(Color.FromArgb(255, 120, 120, 84), 27),  // 
                new ColorPoint(Color.FromArgb(255, 120, 93, 84), 0),
            }),
            WaterColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 107, 130, 153), 16),
                new ColorPoint(Color.FromArgb(255, 91, 116, 140), 14),
                new ColorPoint(Color.FromArgb(255, 77, 102, 128), 14),
                new ColorPoint(Color.FromArgb(255, 63, 89, 115), 13),
                new ColorPoint(Color.FromArgb(255, 51, 77, 102), 0),
            })
        };

        public static TerrainColorConfig LowSaturation3 = new TerrainColorConfig()
        {
            TerrainColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 54, 77, 54), 48),    // hsv: 120, 120, 30
                new ColorPoint(Color.FromArgb(255, 102, 102, 71), 15),  // hsv: 60, 30, 40
                new ColorPoint(Color.FromArgb(255, 102, 87, 71), 0),    // hsv: 30, 30, 40
            }),
            WaterColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 89, 108, 128), 14),
                new ColorPoint(Color.FromArgb(255, 75, 95, 115), 14),
                new ColorPoint(Color.FromArgb(255, 61, 82, 102), 13),
                new ColorPoint(Color.FromArgb(255, 49, 69, 89), 13),
                new ColorPoint(Color.FromArgb(255, 38, 57, 77), 0),
            })
        };

        public static TerrainColorConfig LowSaturation4 = new TerrainColorConfig()
        {
            TerrainColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 45, 64, 45), 44),
                new ColorPoint(Color.FromArgb(255, 89, 89, 54), 44),
                new ColorPoint(Color.FromArgb(255, 102, 87, 71), 26),
                new ColorPoint(Color.FromArgb(255, 128, 108, 89), 0),
            }),
            WaterColor = new ColorGenerator(new ColorPoint[]
            {
                new ColorPoint(Color.FromArgb(255, 89, 108, 128), 14),
                new ColorPoint(Color.FromArgb(255, 75, 95, 115), 14),
                new ColorPoint(Color.FromArgb(255, 61, 82, 102), 13),
                new ColorPoint(Color.FromArgb(255, 49, 69, 89), 13),
                new ColorPoint(Color.FromArgb(255, 38, 57, 77), 0),
            })
        };

        public ColorGenerator TerrainColor;
            
        public ColorGenerator WaterColor;
    }
}

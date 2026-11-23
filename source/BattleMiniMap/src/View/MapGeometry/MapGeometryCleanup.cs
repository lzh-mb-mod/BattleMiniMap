using System;

namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapGeometryCleanup
    {
        // Release every resource even when an earlier native wrapper or reporter throws.
        public static void Run(Action<Exception> report, params Action[] releases)
        {
            foreach (var release in releases)
            {
                try { release(); }
                catch (Exception ex) { try { report?.Invoke(ex); } catch { } }
            }
        }
    }
}

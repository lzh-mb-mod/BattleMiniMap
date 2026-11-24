using System;
using System.Collections.Generic;

namespace BattleMiniMap.View.MapGeometry
{
    // Application ticks continue in menus. This grace period is not a GPU fence;
    // there is intentionally no finalizer/unload flush into unknown native state.
    internal static class MapGeometryRetirementQueue
    {
        private static readonly object Gate = new object();
        private static readonly List<Tuple<long, Action>> Pending = new List<Tuple<long, Action>>();
        private static long _tick;
        public static void Enqueue(Action release)
        {
            if (release == null) return;
            lock (Gate) Pending.Add(Tuple.Create(_tick + 2, release));
        }
        public static void OnApplicationTick()
        {
            List<Action> due = null;
            lock (Gate)
            {
                ++_tick;
                for (var i = Pending.Count - 1; i >= 0; --i)
                    if (Pending[i].Item1 <= _tick)
                    {
                        if (due == null) due = new List<Action>();
                        due.Add(Pending[i].Item2); Pending.RemoveAt(i);
                    }
            }
            if (due == null) return;
            foreach (var release in due)
                MapGeometryCleanup.Run(error => {
                    try { TaleWorlds.Library.Debug.Print("BattleMiniMap runtime main application cleanup: " + error.Message); } catch { }
                }, release);
        }
    }
}

using System;

namespace BattleMiniMap.View.MapGeometry
{
    // Optional experimental settings affect profiling labels only, never rendering.
    internal static class MapGeometryOptionalFlag<T> where T : class
    {
        public static Func<T, bool> Resolve(string name)
        {
            try
            {
                var property = typeof(T).GetProperty(name);
                var getter = property?.PropertyType == typeof(bool) ? property.GetGetMethod() : null;
                if (getter != null && !getter.IsStatic)
                    return (Func<T, bool>)Delegate.CreateDelegate(typeof(Func<T, bool>), getter);
            }
            catch (Exception) { } // Optional metadata must not prevent map initialization.
            return _ => false;
        }
    }
}

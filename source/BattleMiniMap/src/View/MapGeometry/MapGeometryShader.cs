using System;
using TaleWorlds.Engine;

namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapGeometryShader
    {
        // Resource key; simple_shading is the shader program name, not the resource key.
        internal const string ResourceName = "simple_shader";

        internal static void Bind(Material material)
        {
            var shader = Shader.GetFromResource(ResourceName);
            var resolved = RequireName(shader, "lookup");
            material.SetShader(shader);
            var assigned = RequireName(material.GetShader(), "material readback");
            TaleWorlds.Library.Debug.Print("BattleMiniMap GPU shader identity: requested=" + ResourceName +
                " resolved=" + resolved + " assigned=" + assigned);
        }

        private static string RequireName(Shader shader, string stage)
        {
            var actual = shader != null && shader.IsValid ? shader.Name : "<invalid>";
            if (!string.Equals(actual, ResourceName, StringComparison.Ordinal))
                throw new InvalidOperationException("GPU shader " + stage + ": requested=" + ResourceName +
                    " actual=" + actual + "; refusing fallback resource");
            return actual;
        }
    }
}

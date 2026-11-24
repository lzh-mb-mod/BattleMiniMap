using System;
using TaleWorlds.Engine;

namespace BattleMiniMap.View.MapGeometry
{
    internal static class MapGeometryShader
    {
        // Resource key; simple_shading is the shader program name, not the resource key.
        internal const string ResourceName = "simple_shader";
        internal const string DepthVertexColorResourceName = "notexture";

        internal static void Bind(Material material) => BindResource(material, ResourceName);
        internal static void BindDepthProbe(Material material, bool vertexColor) =>
            BindResource(material, vertexColor ? DepthVertexColorResourceName : ResourceName);

        private static void BindResource(Material material, string resourceName)
        {
            if (resourceName != ResourceName && resourceName != DepthVertexColorResourceName)
                throw new InvalidOperationException("Unsupported map shader resource: " + resourceName);
            var shader = Shader.GetFromResource(resourceName);
            var resolved = RequireName(shader, resourceName, "lookup");
            material.SetShader(shader);
            var assigned = RequireName(material.GetShader(), resourceName, "material readback");
            TaleWorlds.Library.Debug.Print("BattleMiniMap GPU shader identity: requested=" + resourceName +
                " resolved=" + resolved + " assigned=" + assigned);
        }

        private static string RequireName(Shader shader, string resourceName, string stage)
        {
            var actual = shader != null && shader.IsValid ? shader.Name : "<invalid>";
            if (!string.Equals(actual, resourceName, StringComparison.Ordinal))
                throw new InvalidOperationException("GPU shader " + stage + ": requested=" + resourceName +
                    " actual=" + actual + "; refusing fallback resource");
            return actual;
        }
    }
}

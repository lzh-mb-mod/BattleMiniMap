using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace BattleMiniMap.View.MapGeometry
{
    internal readonly struct GeometryPoint
    {
        public readonly float X, Y, Z;
        public GeometryPoint(float x, float y, float z) { X = x; Y = y; Z = z; }
    }
    internal sealed class GeometryMesh
    {
        public GeometryPoint[] Vertices;
        public int[][] Faces;
    }
    internal sealed class GeometryLayer
    {
        // A source instance, never a floor number or runtime collision owner.
        public string Id, Resource, Origin, Rotation, Scale;
        public GeometryMesh Original, Proxy;
        public float MinimumZ => Original.Vertices.Min(p => p.Z);
        public float MaximumZ => Original.Vertices.Max(p => p.Z);
    }
    internal sealed class MapGeometryData
    {
        public const string SceneName = "empire_town_g";
        public const string SceneHash = "965ede77b48e0ef353578447160563af442234dfbf2f13063468bb99979a63ba";
        public readonly List<GeometryLayer> Layers = new List<GeometryLayer>();
        public bool HasCompleteProxy { get; private set; }
        public string ProxyFallbackReason { get; private set; }

        public static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static MapGeometryData Load(string path)
        {
            if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Geometry cache exceeds 1 MiB.");
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = 1024 * 1024 };
            XElement root;
            using (var reader = XmlReader.Create(path, settings)) root = XElement.Load(reader);
            if (root.Name != "mapGeometry" || (string)root.Attribute("schema") != "1" ||
                (string)root.Attribute("scene") != SceneName || (string)root.Attribute("sceneSha256") != SceneHash)
                throw new InvalidDataException("Unverified geometry scene/schema.");
            var result = new MapGeometryData();
            var ids = new HashSet<string>();
            foreach (var element in root.Elements("layer"))
            {
                var id = (string)element.Attribute("id");
                var resource = (string)element.Attribute("resource");
                if (string.IsNullOrEmpty(id) || !ids.Add(id) ||
                    (resource != "stone_bridge_wide" && resource != "stone_bridge_wide_extension"))
                    throw new InvalidDataException("Invalid source instance.");
                var layer = new GeometryLayer { Id = id, Resource = resource,
                    Origin = (string)element.Attribute("origin"), Rotation = (string)element.Attribute("euler"),
                    Scale = (string)element.Attribute("scale"), Original = ReadMesh(element.Element("original")) };
                try { layer.Proxy = ReadMesh(element.Element("proxy")); }
                catch (Exception ex) when (ex is InvalidDataException || ex is FormatException || ex is OverflowException)
                { result.ProxyFallbackReason = "Invalid proxy for " + id; }
                if (string.IsNullOrEmpty(layer.Origin) || string.IsNullOrEmpty(layer.Rotation) || string.IsNullOrEmpty(layer.Scale) ||
                    !id.StartsWith(resource + "/", StringComparison.Ordinal)) throw new InvalidDataException("Missing source transform metadata.");
                result.Layers.Add(layer);
                if (result.Layers.Count > 5) throw new InvalidDataException("Too many source instances.");
            }
            if (result.Layers.Count != 5 || result.Layers.Count(l => l.Resource == "stone_bridge_wide") != 1 ||
                result.Layers.Sum(l => l.Original.Faces.Length) != 3242)
                throw new InvalidDataException("Original geometry inventory mismatch.");
            result.HasCompleteProxy = result.Layers.All(l => l.Proxy != null) && result.Layers.Sum(l => l.Proxy.Faces.Length) == 218;
            if (!result.HasCompleteProxy && result.ProxyFallbackReason == null) result.ProxyFallbackReason = "Proxy inventory mismatch.";
            return result;
        }
        private static GeometryMesh ReadMesh(XElement element)
        {
            if (element == null) throw new InvalidDataException("Missing mesh.");
            var vertexLines = ((string)element.Element("vertices") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            var faceLines = ((string)element.Element("faces") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (vertexLines.Length == 0 || vertexLines.Length > 30000 || faceLines.Length == 0 || faceLines.Length > 10000)
                throw new InvalidDataException("Invalid mesh size.");
            var mesh = new GeometryMesh { Vertices = new GeometryPoint[vertexLines.Length], Faces = new int[faceLines.Length][] };
            for (var i = 0; i < vertexLines.Length; i++)
            {
                var values = vertexLines[i].Split(',');
                if (values.Length != 3) throw new InvalidDataException("Invalid vertex.");
                var xyz = values.Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                if (xyz.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 1000000)) throw new InvalidDataException("Nonfinite vertex.");
                mesh.Vertices[i] = new GeometryPoint(xyz[0], xyz[1], xyz[2]);
            }
            for (var i = 0; i < faceLines.Length; i++)
            {
                var values = faceLines[i].Split(',').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                if (values.Length != 3 || values.Distinct().Count() != 3 || values.Any(v => v < 0 || v >= mesh.Vertices.Length))
                    throw new InvalidDataException("Invalid triangle index.");
                mesh.Faces[i] = values;
            }
            return mesh;
        }
        public GeometryMesh Select(GeometryLayer layer, bool proxy) => proxy && HasCompleteProxy ? layer.Proxy : layer.Original;

        public static IEnumerable<GeometryPoint[]> Triangles(GeometryMesh mesh, float? ceiling)
        {
            foreach (var face in mesh.Faces)
            {
                var poly = face.Select(i => mesh.Vertices[i]).ToList();
                if (ceiling.HasValue)
                {
                    var cut = new List<GeometryPoint>();
                    for (var i = 0; i < poly.Count; i++)
                    {
                        var a = poly[i]; var b = poly[(i + 1) % poly.Count];
                        var insideA = a.Z <= ceiling.Value; var insideB = b.Z <= ceiling.Value;
                        if (insideA) cut.Add(a);
                        if (insideA != insideB)
                        {
                            var t = (ceiling.Value - a.Z) / (b.Z - a.Z);
                            cut.Add(new GeometryPoint(a.X + t * (b.X - a.X), a.Y + t * (b.Y - a.Y), ceiling.Value));
                        }
                    }
                    poly = cut;
                }
                for (var i = 1; i + 1 < poly.Count; i++) yield return new[] { poly[0], poly[i], poly[i + 1] };
            }
        }
        public static GeometryPoint MapPoint(GeometryPoint p, float minX, float minY) => new GeometryPoint(p.Y - minY, -(p.X - minX), p.Z);
    }
}

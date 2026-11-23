using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using BattleMiniMap.View.MapGeometry;

static class Program
{
    private static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static void Reject(Action action, string message) { bool rejected = false; try { action(); } catch { rejected = true; } Check(rejected, message); }
    static void Main(string[] args)
    {
        var path = Path.GetFullPath(args[0]); var data = MapGeometryData.Load(path);
        Check(MapGeometryData.HashFile(path) == MapGeometryManifest.CacheSha256, "Cache fingerprint");
        Check(data.Layers.Count == 5, "Five source instances");
        Check(data.Layers.Sum(l => data.Select(l, true).Faces.Length) == 218, "Proxy inventory");
        Check(data.Layers.Sum(l => data.Select(l, false).Faces.Length) == 3242, "Original fallback inventory");
        Check(data.Layers.All(l => l.MinimumZ < l.MaximumZ && l.Id.Contains("/")), "XYZ and instance metadata");
        var p = MapGeometryData.MapPoint(new GeometryPoint(102, 205, 7), 100, 200);
        Check(p.X == 5 && p.Y == -2 && p.Z == 7, "Map axis and height");
        var mesh = new GeometryMesh { Vertices = new[] { new GeometryPoint(0,0,0),new GeometryPoint(1,0,0),new GeometryPoint(0,1,3) }, Faces = new[] { new[] {0,1,2} } };
        var cut = MapGeometryData.Triangles(mesh, 1).ToArray();
        Check(cut.Length == 2 && cut.SelectMany(x => x).All(v => v.Z <= 1), "Cross-layer clipping interpolates a quad");
        Check(mesh.Vertices[2].Z == 3 && MapGeometryData.Triangles(mesh, null).Count() == 1, "Clipping retains original data");
        Check(!MapGeometryData.Triangles(mesh, -1).Any(), "Height below mesh");
        var raster = MapGeometryRasterizer.Render(data,true,null,168.779f,238.234f,1.034f,325,539,325,539);
        Check(raster.CoveredPixels>0 && raster.Triangles==218,"Real cache renders nontransparent pixels at reported map bounds");
        Check(raster.Pixels.Where((v,i)=>i%4==3).Count(v=>v==255)==raster.CoveredPixels,"Exact alpha coverage accounting");
        var originalRaster=MapGeometryRasterizer.Render(data,false,null,168.779f,238.234f,1.034f,325,539,325,539);
        Check(originalRaster.CoveredPixels>0 && originalRaster.Triangles==3242,"Original raster fallback visible");
        var emptyRaster=MapGeometryRasterizer.Render(data,true,-50,168.779f,238.234f,1.034f,325,539,325,539);
        Check(emptyRaster.CoveredPixels==0,"Height cutoff clears all geometry without stale pixels");
        var offMap=MapGeometryRasterizer.Render(data,true,null,10000,10000,1,325,539,325,539);
        Check(offMap.CoveredPixels==0,"Out-of-map geometry does not draw");
        Console.WriteLine("Raster evidence: proxy pixels="+raster.CoveredPixels+", original pixels="+originalRaster.CoveredPixels);
        var sampleData=new MapGeometryData();
        GeometryMesh flat(float z)=>new GeometryMesh{Vertices=new[]{new GeometryPoint(0,0,z),new GeometryPoint(10,0,z),new GeometryPoint(0,10,z)},Faces=new[]{new[]{0,1,2}}};
        sampleData.Layers.Add(new GeometryLayer{Resource="stone_bridge_wide",Original=flat(0)});
        sampleData.Layers.Add(new GeometryLayer{Resource="stone_bridge_wide_extension",Original=flat(3)});
        var top=MapGeometryRasterizer.Render(sampleData,false,null,0,0,1,10,10,10,10);
        var lower=MapGeometryRasterizer.Render(sampleData,false,1,0,0,1,10,10,10,10);
        Check(top.Pixels[0]==130 && lower.Pixels[0]==206,"Highest surface wins and height cut reveals lower source");
        sampleData.Layers.Reverse();
        Check(MapGeometryRasterizer.Render(sampleData,false,null,0,0,1,10,10,10,10).Pixels.SequenceEqual(top.Pixels),"Depth is independent of instance submission order");
        foreach(var layer in sampleData.Layers)Array.Reverse(layer.Original.Faces[0]);
        Check(MapGeometryRasterizer.Render(sampleData,false,null,0,0,1,10,10,10,10).Pixels.SequenceEqual(top.Pixels),"Raster is two sided");
        var temp = Path.Combine(Path.GetTempPath(), "BattleMiniMapGeometryTests-" + Guid.NewGuid().ToString("N"));Directory.CreateDirectory(temp);
        try
        {
            var test = Path.Combine(temp,"test.xml");var xml = XDocument.Load(path);
            xml.Root.Elements("layer").First().Element("proxy").Element("faces").Value = "999999,1,2";xml.Save(test);
            var fallback = MapGeometryData.Load(test);Check(!fallback.HasCompleteProxy && fallback.Layers.Sum(l => fallback.Select(l,true).Faces.Length)==3242,"Bad proxy atomically falls back to all originals");
            xml=XDocument.Load(path);xml.Root.Elements("layer").First().Element("original").Element("faces").Value="999999,1,2";xml.Save(test);
            Reject(()=>MapGeometryData.Load(test),"Bad original rejected");
            xml=XDocument.Load(path);xml.Root.SetAttributeValue("scene","wrong");xml.Save(test);Reject(()=>MapGeometryData.Load(test),"Wrong scene rejected");
            xml=XDocument.Load(path);xml.Root.SetAttributeValue("sceneSha256","wrong");xml.Save(test);Reject(()=>MapGeometryData.Load(test),"Wrong source fingerprint rejected");
            xml=XDocument.Load(path);xml.Root.Elements("layer").First().Element("original").Element("vertices").Value="NaN,0,0";xml.Save(test);Reject(()=>MapGeometryData.Load(test),"Nonfinite coordinate rejected");
            File.WriteAllText(test,"<!DOCTYPE x [<!ENTITY leak SYSTEM 'file:///not-read'>]><x>&leak;</x>");Reject(()=>MapGeometryData.Load(test),"DTD prohibited");
            File.WriteAllBytes(test,new byte[1024*1024+1]);Reject(()=>MapGeometryData.Load(test),"Oversize cache rejected");
            var culture = CultureInfo.CurrentCulture;try { CultureInfo.CurrentCulture=new CultureInfo("fr-FR");Check(MapGeometryData.Load(path).HasCompleteProxy,"Invariant coordinate parsing"); } finally { CultureInfo.CurrentCulture=culture; }
        }
        finally { foreach(var file in Directory.GetFiles(temp)) File.Delete(file); Directory.Delete(temp); }
        Console.WriteLine("PASS: " + checks + " managed cache, layer, clipping and fallback checks; no engine calls");
    }
}

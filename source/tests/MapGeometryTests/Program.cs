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
        Check(MapGeometryHeight.Ceiling(true,float.NaN)==null && MapGeometryHeight.Ceiling(true,float.PositiveInfinity)==null,"Nonfinite height disabled for both views");
        Check(MapGeometryHeight.Ceiling(false,5)==null && MapGeometryHeight.Ceiling(true,-100)==-50 && MapGeometryHeight.Ceiling(true,2000)==1000,"Shared ceiling enabled and bounded");
        var orbit = new MapGeometryPreviewCamera();
        float length(GeometryPoint v) => (float)Math.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z);
        var eye=orbit.Eye; var right=orbit.Right; var up=orbit.Up;
        Check(Math.Abs(length(eye)-4.5f)<1e-5f && Math.Abs(orbit.NearPlane-.045f)<1e-7f, "Preview default camera distance and adaptive near");
        Check(Math.Abs(length(right)-1)<1e-5f && Math.Abs(length(up)-1)<1e-5f && Math.Abs(right.X*up.X+right.Y*up.Y+right.Z*up.Z)<1e-5f, "Preview orthonormal screen basis");
        orbit.Orbit(100000,100000); Check(Math.Abs(orbit.Pitch-80*Math.PI/180)<1e-5, "Preview upper pitch bound");
        orbit.Orbit(-100000,-100000); Check(Math.Abs(orbit.Pitch-15*Math.PI/180)<1e-5, "Preview lower pitch bound");
        for(var k=0;k<100;k++)orbit.Zoom(10); Check(orbit.Distance==.02f && orbit.NearPlane>0 && orbit.NearPlane<orbit.Distance && Math.Abs(orbit.NearPlane-.0002f)<1e-8f,"Preview close zoom bound and positive near below distance");
        for(var k=0;k<100;k++)orbit.Zoom(-10); Check(orbit.Distance==16 && orbit.NearPlane==.05f,"Preview far zoom bound and capped near");
        orbit.Pan(float.MaxValue,float.MaxValue,1); Check(Math.Abs(orbit.Target.X)<=3 && Math.Abs(orbit.Target.Y)<=3 && Math.Abs(orbit.Target.Z)<=3,"Preview pan bounded");
        orbit.Reset(); orbit.Orbit(float.NaN,float.PositiveInfinity); orbit.Zoom(float.NaN); orbit.Pan(float.NaN,0,1);
        Check(orbit.Distance==4.5f && length(orbit.Target)==0 && !float.IsNaN(orbit.Eye.X),"Preview rejects nonfinite input");
        orbit.Pan(10,20,600); Check(length(orbit.Target)>0,"Preview camera-plane pan");
        orbit.Reset(); Check(length(orbit.Target)==0 && orbit.Distance==4.5f,"Preview reset");
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
        var normalized=new MapGpuDiagnosticGeometry(data);
        Check(data.Layers.SelectMany(l=>l.Original.Vertices).All(v=>{
            var q=normalized.Normalize(v);return Math.Abs(q.X)<=1.000001 && Math.Abs(q.Y)<=1.000001 && Math.Abs(q.Z)<=1.000001;
        }),"GPU diagnostic bridge fits centered volume without lost height");
        var first=data.Layers[0].Original.Vertices[0];var qfirst=normalized.Normalize(first);
        Check(Math.Abs((normalized.Center.X-qfirst.Y*normalized.Scale)-first.X)<.0001 && Math.Abs((normalized.Center.Y+qfirst.X*normalized.Scale)-first.Y)<.0001 && Math.Abs((normalized.Center.Z+qfirst.Z*normalized.Scale)-first.Z)<.0001,"Diagnostic transform reversible to original XYZ");
        var aUv=MapGpuDiagnosticGeometry.ExpectedViewport(MapGpuDiagnosticGeometry.Triangle[0]);var cUv=MapGpuDiagnosticGeometry.ExpectedViewport(MapGpuDiagnosticGeometry.Triangle[2]);
        Check(Math.Abs(aUv.X-.18)<.000001 && Math.Abs(aUv.Y-.22)<.000001 && Math.Abs(cUv.X-.5)<.000001 && Math.Abs(cUv.Y-.82)<.000001,"Synthetic triangle expected camera UV");
        Check(data.Layers.SelectMany(l=>l.Original.Vertices).All(v=>{var p=normalized.Normalize(v);return 400-p.Z>.1 && 400-p.Z<500;}),"Entire bridge remains inside diagnostic near/far depth");
        Check(Math.Abs(aUv.Z-.79996)<.00001,"Camera depth matches observed near/far normalization");
        var textureUv=MapGpuDiagnosticGeometry.ExpectedTextureUv(MapGpuDiagnosticGeometry.Triangle[0]);
        Check(Math.Abs(textureUv.Y-.78)<.000001 && Math.Abs(textureUv.X-.18)<.000001,"Texture UV flips camera viewport Y explicitly");
        var projection=new MapGeometryProjection(168.779f,238.234f,1.034f,325,539,-10,10);
        Check(data.Layers.SelectMany(l=>l.Original.Vertices).All(v=>{
            var uv=projection.TextureUv(v);var mapped=MapGeometryData.MapPoint(v,168.779f,238.234f);
            return Math.Abs(uv.X-mapped.X/(325*1.034f))<1e-6 && Math.Abs(uv.Y-(-mapped.Y)/(539*1.034f))<1e-6;
        }),"Main GPU texture axes match CPU raster on every cached vertex");
        var corner=projection.TextureUv(new GeometryPoint(168.779f,238.234f,0));
        Check(corner.X==0 && corner.Y==0,"World minimum maps to top-left texture corner");
        var lastPixel=projection.TextureUv(new GeometryPoint(168.779f+538*1.034f,238.234f+324*1.034f,0));
        Check(Math.Abs(lastPixel.X-324f/325)<1e-6 && Math.Abs(lastPixel.Y-538f/539)<1e-6,"Last map pixel preserves extra-pixel denominator");
        var large=new MapGeometryProjection(0,0,1,4096,8192,-10,10);
        var uvLarge=large.TextureUv(new GeometryPoint(4096,2048,0));
        Check(uvLarge.X*2048==1024 && uvLarge.Y*2048==1024,"Downsampled non-square map keeps world UV mapping");
        Check(projection.TextureUv(new GeometryPoint(200,300,10)).Z < projection.TextureUv(new GeometryPoint(200,300,-10)).Z,"Upper surface is nearer in GPU depth");
        Check(data.Layers.SelectMany(l=>l.Original.Vertices).All(v=>{var uv=projection.TextureUv(v);return uv.Z>0 && uv.Z<1;}),"Cached layers fit main-map camera near/far");
        try{new MapGeometryProjection(0,0,float.NaN,1,1,0,1);Check(false,"Invalid projection rejected");}catch(ArgumentOutOfRangeException){Check(true,"Invalid projection rejected");}
        var backend=new MapGeometryBackendState();
        Check(!backend.Update(0,false,null),"CPU retained before GPU callback");
        Check(!backend.Update(1,true,null),"CPU retained after first callback");
        Check(backend.Update(2,true,null),"Valid GPU target selected after callback warmup");
        Check(!backend.Update(3,false,null) && backend.Failure!=null,"GPU invalidation returns CPU");
        Check(!backend.Update(4,true,null),"GPU failure remains latched until reset");
        backend.Reset();Check(!backend.Update(2,true,null) && backend.Update(3,true,null),"Reset allows GPU retry with minimum visible warmup");
        Check(!backend.Update(4,true,"callback fault") && backend.Failure=="callback fault","Callback failure retains CPU");
        backend.Reset();for(var n=0;n<MapGeometryBackendState.StallUpdates;n++)backend.Update(0,true,null);
        Check(backend.Failure!=null,"No callback progress fails back to CPU");
        backend.Reset();backend.Update(1,true,null);
        // Hidden updates never call policy.Update; resuming counts only visible updates.
        Check(backend.Update(2,true,null) && backend.Failure==null,"Hidden interval does not count toward GPU stall");
        for(var n=0;n<MapGeometryBackendState.StallUpdates;n++)backend.Update(2,true,null);
        Check(backend.Failure!=null,"Previously active GPU also fails back when callbacks stall");
        backend.Reset();for(var n=0;n<MapGeometryBackendState.StallUpdates;n++)backend.Update(n+1,false,null);
        Check(backend.Failure!=null,"Never-valid GPU target retains CPU despite callbacks");
        var releases=0;var cleanupErrors=0;
        MapGeometryCleanup.Run(e=>cleanupErrors++,()=>{throw new InvalidOperationException("failed resource");},()=>releases++);
        Check(releases==1 && cleanupErrors==1,"One failed release does not skip remaining resources");
        MapGeometryCleanup.Run(e=>{throw new Exception("failed reporter");},()=>{throw new Exception("failed resource");},()=>releases++);
        Check(releases==2,"Cleanup reporter failure cannot prevent remaining releases");
        MapGeometryCleanup.Run(null,()=>{throw new Exception("no reporter");},()=>releases++);
        Check(releases==3,"Cleanup failure without reporter is contained");
        var perf=new MapGeometryPerformance();string perfRow=null;
        for(var n=0;n<=72;n++){var row=perf.Record("CPU",n*.1,2,false);if(row!=null)perfRow=row;}
        Check(perfRow!=null && perfRow.Contains("update_cpu_mean_ms=2.0000") && perfRow.Contains("mission_tick_p95_ms=100.0000"),"Performance window reports CPU and wall-clock intervals independently");
        perf.Reset();for(var n=0;n<40;n++)perf.Record("CPU",n*.1,1,false);
        Check(perf.Record("GPU",4,1,false)==null,"Backend switch drops partial performance window");
        Check(perf.Record("GPU",4.1,1,true)==null && perf.Record("GPU",4.2,1,false)==null,"Paused performance window is reset");
        Check(perf.Record("GPU",6,1,false)?.Contains("reason=long_gap")==true,"Long tick gap resets and reports discarded window");
        Check(perf.Record(null,6.1,1,false)==null,"Hidden or disabled profiling drops window");
        perf=new MapGeometryPerformance();
        for(var n=0;n<=40;n++)perf.Record("CPU",n*.1,99,false);
        perf.Record("GPU",4.1,1,false);perfRow=null;double firstWindowAt=0;
        for(var n=42;n<=120;n++){var row=perf.Record("GPU",n*.1,1,false);if(row!=null && row.Contains("event=window")){perfRow=row;firstWindowAt=n*.1;break;}}
        Check(perfRow!=null && firstWindowAt>=11.1 && perfRow.Contains("update_cpu_mean_ms=1.0000"),"Backend reset discards high-cost samples and restarts full warmup/window");
        perf=new MapGeometryPerformance();for(var n=0;n<=40;n++)perf.Record("CPU",n*.1,99,false);
        perf.Record("CPU",4.1,99,true);perfRow=null;
        for(var n=42;n<=120;n++){var row=perf.Record("CPU",n*.1,1,false);if(row!=null && row.Contains("event=window")){perfRow=row;firstWindowAt=n*.1;break;}}
        Check(perfRow!=null && firstWindowAt>=11.2 && perfRow.Contains("update_cpu_mean_ms=1.0000"),"Pause reset cannot leak previous samples into resumed window");
        perf=new MapGeometryPerformance();for(var n=0;n<=40;n++)perf.Record("CPU",n*.1,99,false);
        var dropped=perf.Record("CPU",6,1,false);Check(dropped.Contains("long_gap_resets=1") && !dropped.Contains("dropped_samples=0"),"Long stalls retain discarded-sample evidence");
        perfRow=null;for(var n=61;n<=140;n++){var row=perf.Record("CPU",n*.1,1,false);if(row!=null && row.Contains("event=window")){perfRow=row;firstWindowAt=n*.1;break;}}
        Check(perfRow!=null && firstWindowAt>=13 && perfRow.Contains("update_cpu_mean_ms=1.0000") && perfRow.Contains("long_gap_resets=1"),"Post-stall windows restart and retain cumulative reset counters");
        var contextA=new MapGeometryPerformanceContext{Gpu=true,Proxy=true,Tint=false,Follow=true,Dynamic=false,DiagnosticDepth=false,MarkersRequested=false,MarkerProfile=false,Generation=1,Width=790,Diagnostic=0,Cached=false,Zoom=.5f};var contextB=contextA;
        Check(contextA.Equals(contextB),"Stable typed performance context compares without formatting");contextB.DiagnosticDepth=true;
        Check(!contextA.Equals(contextB),"Diagnostic depth changes performance context");
        perf=new MapGeometryPerformance();perfRow=null;
        for(var n=0;n<=72;n++){var row=perf.Record("CPU",n*.1,n%5==0?9:1,false);if(row!=null && row.Contains("event=window")){perfRow=row;break;}}
        Check(perfRow!=null && perfRow.Contains("update_cpu_p95_ms=9.0000"),"Performance P95 reflects high-cost tail, not mean");
        backend.Reset();backend.Update(2,true,null,true);
        Check(backend.Update(2,true,null,true),"Submitted cache is selected after minimum visible warmup");
        var cachedStable=true;for(var n=0;n<1000;n++)cachedStable &= backend.Update(2,true,null,true);
        Check(cachedStable && backend.Failure==null,"Intentional cached idle never trips180-update watchdog");
        Check(!backend.Update(2,false,null,true) && backend.Failure!=null,"Cached texture invalidation still returns CPU");
        backend.Reset();backend.Update(2,true,null,true);backend.Update(2,true,null,true);
        Check(!backend.Update(2,true,"cache callback fault",true),"Late cached callback error still returns CPU");
        backend.Reset();Check(!backend.Update(0,true,null,false),"Dirty generation cannot inherit old cache selection");
        for(var n=0;n<=MapGeometryBackendState.StallUpdates;n++)backend.Update(1,true,null,false);
        Check(backend.Failure!=null,"New generation missing second successful paint still times out");
        var pendingContext=new MapGeometryPerformanceContext{Gpu=true,Cached=false};var cachedContext=pendingContext;cachedContext.Cached=true;
        Check(!pendingContext.Equals(cachedContext),"Performance context separates pending and cached scheduling");
        var flags=new OptionalFlagFixture();
        Check(MapGeometryOptionalFlag<OptionalFlagFixture>.Resolve("Enabled")(flags),"Optional label flag resolves typed bool getter once");
        Check(!MapGeometryOptionalFlag<OptionalFlagFixture>.Resolve("Missing")(flags),"Committed config without experimental flags still builds and labels false");
        Check(!MapGeometryOptionalFlag<OptionalFlagFixture>.Resolve("OtherType")(flags),"Non-bool optional property is ignored safely");
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

class OptionalFlagFixture { public bool Enabled => true; public string OtherType => "ignored"; }

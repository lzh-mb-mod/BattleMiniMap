using System;
using System.IO;
using System.Security.Cryptography;
using BattleMiniMap.View.MapGeometry;
static class PrimitiveFrameworkChecks
{
    static bool recordOnly;
    static void Compare(GeometryPoint a,GeometryPoint b,float radius,int expectedCount,string expectedHash,string name)
    {
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream))
        {
            var count=0;
            foreach(var face in MapLivePrimitiveGeometry.Capsule(a,b,radius))
            {++count;for(var i=0;i<3;++i){writer.Write(face[i].X);writer.Write(face[i].Y);writer.Write(face[i].Z);}}
            using(var sha=SHA256.Create())
            {
                var hash=BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();
                Console.WriteLine("FRAMEWORK_PRIMITIVE "+name+","+count+","+hash);
                if(count!=expectedCount||(!recordOnly&&hash!=expectedHash))throw new Exception("Framework primitive baseline mismatch: "+name);
            }
            Console.WriteLine((recordOnly?"RECORDED framework primitive: ":"PASS framework primitive: old ordered float bits and face count ")+name);
        }
    }
    public static void Run(bool record=false)
    {
        recordOnly=record;
        Compare(new GeometryPoint(0,0,0),new GeometryPoint(0,0,0),2f,168,"cbcf0d3457eaddf7e7b4e19039db79de958009655d6eb7163af25e28f76660d4","sphere");
        Compare(new GeometryPoint(0,0,0),new GeometryPoint(0,0,2),.5f,192,"f5aaad94196de71ea532500759f63b264af63157cfb6569989fde2ebf4aae301","vertical");
        Compare(new GeometryPoint(-3,5,2),new GeometryPoint(7,-11,9),4f,192,"09ae2ef24bfbaba2c0ea5fd91402d09100bc0613da0670da0bbc528361205237","diagonal");
        Compare(new GeometryPoint(2,1,5),new GeometryPoint(-8,1,5),1f,192,"8dc16fb7f594473de4598629436c398398a6cf685335e6fabb74fc6d82dc750a","horizontal");
        Compare(new GeometryPoint(0,0,0),new GeometryPoint(0,0,1e-7f),.1f,192,"d0f384b15442d84914f77fa97b23a4be87dad882f4e84676253f6e5b5780c7e2","near-axis");
        Compare(new GeometryPoint(0,0,0),new GeometryPoint(0,0,1),.00001f,24,"11c0979793d4290bce10512afb501c467cc88c3fa8d0f7b110ad0e5d5336a9ef","tiny");
        Compare(new GeometryPoint(-100,200,0),new GeometryPoint(200,-500,300),100000f,192,"483408061c74434c85b65ae47feb21455a671f1cb31033d29c2911fbc3c97580","large");
        Console.WriteLine(recordOnly?"RECORDED: 7 net472 primitive cases; hashes emitted without comparison":"PASS: 7 net472 primitive golden checks; baseline8a21bb9 Framework exact bits; no engine execution");
    }
}

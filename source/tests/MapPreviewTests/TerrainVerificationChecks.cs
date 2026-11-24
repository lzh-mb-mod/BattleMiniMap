using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using BattleMiniMap.View.MapGeometry;
using TaleWorlds.Engine;

static class TerrainVerificationChecks
{
    static int checks;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);++checks;Console.WriteLine("PASS terrain verification: "+message);}
    static void Drive(IEnumerator<int> steps)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(steps.MoveNext()){if(watch.Elapsed.TotalSeconds>5)throw new Exception("Verification timeout");Thread.Sleep(1);}
    }
    public static void Run()
    {
        var mainThread=Thread.CurrentThread.ManagedThreadId;var workerThread=mainThread;var published=false;
        var memory=new RawTerrainChecks.Memory(0x80000,false);var verification=new MapTerrainVerification();
        using(var iterator=verification.Run(token=>{workerThread=Thread.CurrentThread.ManagedThreadId;return memory;},m=>published=m==memory).GetEnumerator())Drive(iterator);
        Check(published&&workerThread!=mainThread&&memory.Reads==0&&verification.State=="hash-verified","Worker verifies without touching scene memory; publication returns to owner thread");
        Check(verification.WorkerMs>=0&&verification.WaitMs>=0,"Worker and waiting duration tracked separately");
        var started=new ManualResetEventSlim();var release=new ManualResetEventSlim();var cancelled=new ManualResetEventSlim();published=false;
        verification=new MapTerrainVerification();
        var steps=verification.Run(token=>
        {
            started.Set();try{release.Wait(token);token.ThrowIfCancellationRequested();return memory;}
            catch(OperationCanceledException){cancelled.Set();throw;}
        },m=>published=true).GetEnumerator();
        Check(steps.MoveNext()&&steps.Current==0&&steps.MoveNext()&&steps.Current==MapTerrainVerification.Waiting&&started.Wait(2000)&&!published,"Pending task yields a wait marker without publishing memory access");
        steps.Dispose();release.Set();Check(cancelled.Wait(2000)&&!published&&memory.Reads==0&&verification.State=="cancelled"&&verification.WaitMs>0,"Pending iterator disposal cancels worker and prevents late publication");
        started.Dispose();release.Dispose();cancelled.Dispose();
        var failure=false;
        using(var iterator=new MapTerrainVerification().Run(token=>{throw new InvalidDataException("bad build");},m=>published=true).GetEnumerator())
        {try{Drive(iterator);}catch(InvalidDataException){failure=true;}}
        Check(failure&&!published,"Failed verification is observed without publishing a result");
        var path=Path.Combine(Path.GetTempPath(),"BattleMiniMap-verification-"+Guid.NewGuid().ToString("N")+".bin");
        try
        {
            var bytes=new byte[150000];new Random(816).NextBytes(bytes);File.WriteAllBytes(path,bytes);
            var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
            MapSelfTerrainMemory.VerifyFile(path,hash,CancellationToken.None);
            Check(true,"Actual chunked file verification matches SHA256 over multiple chunks");
            var rejected=false;try{MapSelfTerrainMemory.VerifyFile(path,new string('0',64),CancellationToken.None);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"Wrong module digest is rejected");
            using(var token=new CancellationTokenSource())
            {token.Cancel();var stopped=false;try{MapSelfTerrainMemory.VerifyFile(path,hash,token.Token);}catch(OperationCanceledException){stopped=true;}Check(stopped,"Cancelled verification does not open/read the file");}
        }
        finally{File.Delete(path);}
        var identity=new MapSelfTerrainMemory.ModuleIdentity("A.dll",100,200);
        Check(identity.Matches(new MapSelfTerrainMemory.ModuleIdentity("a.DLL",100,200))&&!identity.Matches(new MapSelfTerrainMemory.ModuleIdentity("B.dll",100,200))&&!identity.Matches(new MapSelfTerrainMemory.ModuleIdentity("A.dll",101,200))&&!identity.Matches(new MapSelfTerrainMemory.ModuleIdentity("A.dll",100,201)),"Module path, loaded base and image size must all match after verification");
        var source=new Scene{Height=p=>p.x+10*p.y};memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
        started=new ManualResetEventSlim();release=new ManualResetEventSlim();cancelled=new ManualResetEventSlim();verification=new MapTerrainVerification();
        var live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,
            rawInitialization:complete=>verification.Run(token=>{started.Set();try{release.Wait(token);token.ThrowIfCancellationRequested();return memory;}catch(OperationCanceledException){cancelled.Set();throw;}},complete));
        live.Update(.1f);Check(started.Wait(2000)&&verification.Polls<=1&&memory.Reads==0,"Collector polls pending verification once per Update, never burns its 2048-step quota");
        var previousPolls=verification.Polls;live.Update(.1f);Check(verification.Polls==previousPolls+1&&memory.Reads==0&&source.Reads==0,"Pending verification performs no raw reads or scene enumeration");
        live.Dispose();release.Set();Check(cancelled.Wait(2000)&&memory.Reads==0,"Closing pending collector cancels initialization without touching scene memory");
        started.Dispose();release.Dispose();cancelled.Dispose();
        source=new Scene{Height=p=>p.x+10*p.y};memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
        live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,
            rawInitialization:complete=>new MapTerrainVerification().Run(token=>memory,complete));
        for(var i=0;i<1000&&!live.InitialFinished;++i){live.Update(.1f);Thread.Sleep(1);}
        Check(live.InitialFinished&&!live.Partial&&memory.Reads>0&&source.HeightReads==5,"Successful asynchronous verification resumes original raw extraction and coordinate checks");live.Dispose();
        source=new Scene();memory=new RawTerrainChecks.Memory(source.Pointer.ToUInt64(),false);
        live=new MapLiveSceneGeometry(source,new Scene(),new Material(),new MapLiveGeometryRules(0,0,2,2),false,false,true,
            rawInitialization:complete=>new MapTerrainVerification().Run(token=>{throw new InvalidDataException("unverified");},complete));
        for(var i=0;i<1000&&!live.InitialFinished;++i){live.Update(.1f);Thread.Sleep(1);}
        Check(live.InitialFinished&&live.Partial&&live.Failure==null&&memory.Reads==0,"Bad module remains a controlled raw coverage gap without scalar or disk fallback");live.Dispose();
        Console.WriteLine("PASS: "+checks+" asynchronous terrain verification checks; controlled worker/files, no game extraction");
    }
}

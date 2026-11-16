using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Porto.Core;

internal static class Rc8CoverageTests
{
    public static void Add(List<(string,Action)> tests)
    {
        tests.Add(("rc8 coverage reuses prior videos and reads every new video hint without reading photos",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var oldBytes=Enumerable.Range(1,3).Select(seed=>Fixtures.Video(seed)).ToArray();
            var oldItems=oldBytes.Select((bytes,index)=>new SourceItem("old-"+index,"dcim",index+".mov",MediaKind.Video,bytes.Length)).ToList();
            var initial=new ImportEngine(new FakeSource(oldItems,oldItems.Select((item,index)=>(item.Id,oldBytes[index])).ToDictionary(x=>x.Id,x=>x.Item2)),destination).Run(false,default);
            Fixtures.Check(initial.Count(Outcome.Imported)==3);
            var retained=Directory.GetFiles(destination.Videos).ToDictionary(path=>path,path=>VerifiedCopy.HashFile(path,default));
            var items=oldItems.Select(item=>item with{Id="current-"+item.Id}).ToList();
            var bytesById=items.Select((item,index)=>(item.Id,oldBytes[index])).ToDictionary(x=>x.Id,x=>x.Item2);
            void Add(string id,string name,byte[] bytes){items.Add(new(id,"dcim",name,MediaKind.Video,bytes.Length));bytesById.Add(id,bytes);}
            Add("paired","IMG_0010.MOV",Fixtures.Video(4));
            Add("edited","IMG_E0020.MOV",Fixtures.Video(5));
            Add("timed","metadata.mov",Rc8MediaTests.TimedVideo());
            Add("ordinary","ordinary.mov",Fixtures.Video(6,marker:"com.apple.quicktime.still-image-time"));
            items.Add(new("photo","dcim","IMG_0010.HEIC",MediaKind.Image,42));
            items.Add(new("sidecar","dcim","IMG_0010.AAE",MediaKind.Other,12));
            var source=new FakeSource(items,bytesById);
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.AlreadyPresent)==3&&report.CurrentCount(Outcome.Imported)==1
                &&report.CurrentCount(Outcome.Additional)==3&&report.Count(Outcome.Excluded)==2);
            Fixtures.Check(report.CandidateCount==7&&report.TransferCandidateCount==7&&source.Reads==7,
                "Every video must be read; image and adjustment sidecar resources must never be opened.");
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length==4&&Directory.GetFiles(destination.AdditionalVideos).Length==3);
            foreach(var saved in retained)Fixtures.Check(File.Exists(saved.Key)&&VerifiedCopy.HashFile(saved.Key,default)==saved.Value);
            foreach(var item in report.Items.Where(item=>item.Sha256!=null))
            {
                byte[] expected=bytesById[item.SourceId];
                Fixtures.Check(item.Bytes==expected.Length&&item.Sha256==Convert.ToHexString(SHA256.HashData(expected))
                    &&File.ReadAllBytes(destination.Resolve(item.File!)).SequenceEqual(expected));
            }
            Fixtures.Check(report.Items.Where(item=>item.Outcome==Outcome.Additional).All(item=>item.Detail.Contains("non certificabile",StringComparison.Ordinal)));
        }));
        tests.Add(("rc8 coverage current edited hint does not inflate the primary count on hash reuse",()=>
        {
            using var destination=new Destination(Fixtures.Root());byte[] bytes=Fixtures.Video();
            new ImportEngine(FakeSource.One(bytes,id:"old",name:"original.mov"),destination).Run(false,default);
            string original=Directory.GetFiles(destination.Videos).Single();var before=VerifiedCopy.HashFile(original,default);
            var source=new FakeSource([new("edited","p","IMG_E1234.MOV",MediaKind.Video,bytes.Length),
                new("ordinary","p","original.mov",MediaKind.Video,bytes.Length)],new(){{"edited",bytes},{"ordinary",bytes}});
            var report=new ImportEngine(source,destination).Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.Imported)+report.CurrentCount(Outcome.AlreadyPresent)==1
                &&report.CurrentCount(Outcome.Additional)+report.CurrentCount(Outcome.AdditionalAlreadyPresent)==1,
                "Reusing a prior primary path must not turn a current additional hint into a primary video.");
            Fixtures.Check(Directory.GetFiles(destination.Videos).Length+Directory.GetFiles(destination.AdditionalVideos).Length==2
                &&VerifiedCopy.HashFile(original,default)==before,"Two current byte-identical observations still require two physical logical copies.");
        }));
        foreach(bool additional in new[]{false,true})
            tests.Add(($"rc8 coverage old verified quarantine is published with original part intact; additional={additional}",()=>
            {
                using var destination=new Destination(Fixtures.Root());byte[] bytes=PromotionBytes(additional);
                var old=SeedQuarantine(destination,bytes,1).Single();
                var source=FakeSource.One(bytes,id:"current",name:"clip.mov");
                var report=new ImportEngine(source,destination).Run(true,default);
                Fixtures.Check(report.Count(additional?Outcome.Additional:Outcome.Imported)==1&&source.Reads==2);
                AssertPromoted(destination,bytes,[old],additional);
                var second=new ImportEngine(FakeSource.One(bytes,id:"next-session",name:"clip.mov"),destination).Run(false,default);
                Fixtures.Check(second.Count(additional?Outcome.AdditionalAlreadyPresent:Outcome.AlreadyPresent)==1);
                AssertPromoted(destination,bytes,[old],additional);
            }));
        foreach(string phase in new[]{"promotion-intent","promotion-flush","promotion-ready","promotion-rename","promotion-complete"})
            foreach(bool additional in new[]{false,true})
                tests.Add(($"rc8 coverage promotion survives process termination at {phase}; additional={additional}",()=>
                {
                    string root=Fixtures.Root();byte[] bytes=PromotionBytes(additional);CatalogEntry old;
                    using(var destination=new Destination(root))old=SeedQuarantine(destination,bytes,1).Single();
                    Crash(root,phase,additional);
                    using var recovered=new Destination(root);
                    var report=new ImportEngine(FakeSource.One(bytes,id:"current",name:"clip.mov"),recovered).Run(false,default);
                    bool ready=phase is "promotion-ready" or "promotion-rename" or "promotion-complete";
                    Outcome expected=additional?(ready?Outcome.AdditionalAlreadyPresent:Outcome.Additional):(ready?Outcome.AlreadyPresent:Outcome.Imported);
                    Fixtures.Check(report.Count(expected)==1);
                    AssertPromoted(recovered,bytes,[old],additional);
                }));
        tests.Add(("rc8 coverage additional promotion collision preserves foreign file and folder",()=>
        {
            using var destination=new Destination(Fixtures.Root());byte[] bytes=PromotionBytes(true);
            var old=SeedQuarantine(destination,bytes,1).Single();
            new ImportEngine(FakeSource.One(bytes,id:"current",name:"clip.mov"),destination)
                {FaultPoint=point=>{if(point=="promotion-ready")throw new OperationCanceledException();}}.Run(false,default);
            CatalogEntry ready;using(var catalog=new Catalog(destination))ready=catalog.Entries().Single(entry=>entry.State=="Ready");
            string collision=destination.Resolve(ready.Final);byte[] foreign=[9,8,7,6];
            using(var output=new FileStream(collision,FileMode.CreateNew,FileAccess.Write,FileShare.None))output.Write(foreign);
            var report=new ImportEngine(FakeSource.One(bytes,id:"current",name:"clip.mov"),destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.AdditionalAlreadyPresent)==1&&File.ReadAllBytes(collision).SequenceEqual(foreign)
                &&File.ReadAllBytes(destination.Resolve(old.Temp)).SequenceEqual(bytes));
            using var finalCatalog=new Catalog(destination);var published=finalCatalog.Entries().Single(entry=>entry.State=="Complete");
            Fixtures.Check(published.Final!=ready.Final&&Path.GetDirectoryName(published.Final)==Destination.AdditionalDirectory
                &&File.ReadAllBytes(destination.Resolve(published.Final)).SequenceEqual(bytes));
        }));
        tests.Add(("rc8 coverage promotion preserves identical-resource multiplicity",()=>
        {
            using var destination=new Destination(Fixtures.Root());byte[] bytes=PromotionBytes(true);
            var old=SeedQuarantine(destination,bytes,2);
            FakeSource Source(string generation)=>new(Enumerable.Range(0,2).Select(index=>
                new SourceItem(generation+index,"dcim","clip.mov",MediaKind.Video,bytes.Length)).ToList(),
                Enumerable.Range(0,2).ToDictionary(index=>generation+index,_=>bytes));
            var report=new ImportEngine(Source("current-"),destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Additional)==2&&report.Count(Outcome.Pending)==0);
            AssertPromoted(destination,bytes,old,true);
            var repeated=new ImportEngine(Source("later-"),destination).Run(false,default);
            Fixtures.Check(repeated.Count(Outcome.AdditionalAlreadyPresent)==2&&repeated.Count(Outcome.Pending)==0);
            AssertPromoted(destination,bytes,old,true);
        }));
        tests.Add(("rc8 coverage invalid quarantine stays explicit and cannot be promoted",()=>
        {
            using var destination=new Destination(Fixtures.Root());byte[] bytes=[1,2,3,4];
            var old=SeedQuarantine(destination,bytes,1).Single();
            var report=new ImportEngine(FakeSource.One(bytes,id:"current",name:"clip.mov"),destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Ambiguous)==1&&report.Count(Outcome.Imported)==0&&report.Count(Outcome.Additional)==0
                &&Directory.GetFiles(destination.Videos).Length==0&&Directory.GetFiles(destination.AdditionalVideos).Length==0);
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(old.Temp)).SequenceEqual(bytes));
            using var catalog=new Catalog(destination);Fixtures.Check(catalog.Entries().Single().State=="Quarantined");
        }));
        tests.Add(("rc8 coverage new final-inventory work stays current while unread old IDs remain historical",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            var inaccessible=FakeSource.One(id:"old-unread",name:"old.mov");
            inaccessible.Reader=(_,_)=>throw new IOException("Synthetic old unread resource.");
            new ImportEngine(inaccessible,destination).Run(false,default);
            var engine=new ImportEngine(new FinalInventoryAddsItem(),destination);
            var report=engine.Run(false,default);
            Fixtures.Check(report.Items.Single(item=>item.SourceId=="new-final").Outcome==Outcome.Pending
                &&!report.Items.Single(item=>item.SourceId=="new-final").Historical);
            Fixtures.Check(report.Items.Single(item=>item.SourceId=="old-unread").Historical
                &&report.CurrentCount(Outcome.Error)==0&&report.CurrentCount(Outcome.Pending)==1
                &&report.HistoricalCount==1&&!report.FinalInventoryChecked&&!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            string text=File.ReadAllText(engine.ReportPath!);
            Fixtures.Check(text.Contains("Errori: 0",StringComparison.Ordinal)&&text.Contains("Pendenti aggiuntivi: 1",StringComparison.Ordinal)
                &&text.Contains("Osservazioni storiche ancora da verificare: 1",StringComparison.Ordinal)
                &&text.Contains("[STORICO · Errori] old.mov",StringComparison.Ordinal));
            using var json=JsonDocument.Parse(File.ReadAllText(Path.ChangeExtension(engine.ReportPath!,".json")));
            var rows=json.RootElement.GetProperty("Items").EnumerateArray().ToArray();
            Fixtures.Check(rows.Single(row=>row.GetProperty("SourceId").GetString()=="old-unread").GetProperty("Historical").GetBoolean()
                &&!rows.Single(row=>row.GetProperty("SourceId").GetString()=="new-final").GetProperty("Historical").GetBoolean());
            var later=new ImportEngine(new FakeSource([],[]),destination).Run(false,default);
            Fixtures.Check(later.HistoricalCount==2&&later.Count(Outcome.Error)==1&&later.Count(Outcome.Pending)==1
                &&!later.Status.StartsWith("Completati",StringComparison.Ordinal),"A later empty inventory cannot erase unread historical debt.");
        }));
        tests.Add(("rc8 coverage a missing prior copy stays historical with an empty current inventory",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            new ImportEngine(FakeSource.One(),destination).Run(false,default);
            string original=Directory.GetFiles(destination.Videos).Single(),moved=original+".synthetic-user-move";
            byte[] before=File.ReadAllBytes(original);File.Move(original,moved);
            var report=new ImportEngine(new FakeSource([],[]),destination).Run(false,default);
            Fixtures.Check(report.CurrentCount(Outcome.Missing)==0&&report.Count(Outcome.Missing)==1&&report.HistoricalCount==1
                &&!report.Status.StartsWith("Completati",StringComparison.Ordinal)&&File.ReadAllBytes(moved).SequenceEqual(before));
        }));
        tests.Add(("rc8 coverage previously published video failing the new parser stays ambiguous and unchanged",()=>
        {
            using var destination=new Destination(Fixtures.Root());
            byte[] handler=new byte[12];Encoding.ASCII.GetBytes("vide").CopyTo(handler,8);
            // rc7 checked the video handler but did not descend into minf/stbl/stsd.
            byte[] bytes=Fixtures.Atom("moov",Fixtures.Atom("trak",Fixtures.Atom("mdia",Fixtures.Atom("hdlr",handler),
                Fixtures.Atom("minf",Fixtures.Atom("stbl",Fixtures.Atom("stsd",new byte[7]))))))
                .Concat(Fixtures.Atom("mdat",[1,2,3,4])).ToArray();
            string id=Guid.NewGuid().ToString("N");var source=new SourceItem("old","dcim","clip.mov",MediaKind.Video,bytes.Length);
            string relative=Path.Combine("Video Porto",Destination.SafeStem(source.Name)+"_"+id+".mov");
            string path=destination.Resolve(relative);File.WriteAllBytes(path,bytes);
            var fingerprint=VerifiedCopy.HashFile(path,default);
            using(var catalog=new Catalog(destination))catalog.Save(new(id,"Complete",Path.Combine(".porto",id+".part"),relative,fingerprint.Hash,bytes.Length,source));
            using(var ledger=new WorkLedger(destination))
            {
                ledger.Observe([source],"synthetic-rc7-inventory");ledger.ReadVerified(source,fingerprint);
                ledger.Record(source,new(source.Id,source.Name,Outcome.Imported,"Synthetic prior parser accepted this video.",relative,bytes.Length,fingerprint.Hash));
            }
            var report=new ImportEngine(FakeSource.One(bytes,id:"current",name:"clip.mov"),destination).Run(false,default);
            Fixtures.Check(report.Count(Outcome.Ambiguous)==1&&report.Count(Outcome.AlreadyPresent)==0&&report.Count(Outcome.AdditionalAlreadyPresent)==0
                &&!report.Status.StartsWith("Completati",StringComparison.Ordinal));
            Fixtures.Check(File.ReadAllBytes(path).SequenceEqual(bytes)&&Directory.GetFiles(destination.Videos).Length==1);
            var later=new ImportEngine(new FakeSource([],[]),destination).Run(false,default);
            Fixtures.Check(later.Count(Outcome.Ambiguous)==1&&later.HistoricalCount==1&&!later.Status.StartsWith("Completati",StringComparison.Ordinal));
        }));
    }

    public static int CrashChild(string root,string phase,bool additional)
    {
        using var destination=new Destination(root);
        new ImportEngine(FakeSource.One(PromotionBytes(additional),id:"current",name:"clip.mov"),destination)
            {FaultPoint=point=>{if(point==phase)Environment.Exit(77);}}.Run(false,default);
        return 2;
    }
    private static void Crash(string root,string phase,bool additional)
    {
        var info=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
        if(Path.GetFileNameWithoutExtension(Environment.ProcessPath!).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        foreach(string arg in new[]{"--rc8-promotion",root,phase,additional.ToString()})info.ArgumentList.Add(arg);
        using var child=Process.Start(info)!;
        if(!child.WaitForExit(15000)){child.Kill(true);throw new Exception("Synthetic promotion crash child timed out.");}
        Fixtures.Check(child.ExitCode==77,"Expected synthetic process termination at "+phase+", exit "+child.ExitCode);
    }
    private static byte[] PromotionBytes(bool additional)=>additional?Rc8MediaTests.TimedVideo()
        :Fixtures.Video(marker:"com.apple.quicktime.still-image-time");
    private static CatalogEntry[] SeedQuarantine(Destination destination,byte[] bytes,int count)
    {
        var items=Enumerable.Range(0,count).Select(index=>new SourceItem("old-"+index,"dcim","clip.mov",MediaKind.Video,bytes.Length)).ToArray();
        var fingerprint=new CopyResult(bytes.Length,Convert.ToHexString(SHA256.HashData(bytes)));
        using var ledger=new WorkLedger(destination);ledger.Observe(items,"synthetic-rc7-inventory");
        using var catalog=new Catalog(destination);
        return items.Select(item=>
        {
            string id=Guid.NewGuid().ToString("N");
            var entry=new CatalogEntry(id,"Quarantined",Path.Combine(".porto",id+".part"),"",fingerprint.Hash,bytes.Length,item);
            using(var output=new FileStream(destination.Resolve(entry.Temp),FileMode.CreateNew,FileAccess.Write,FileShare.None))output.Write(bytes);
            catalog.Save(entry);ledger.ReadVerified(item,fingerprint);
            ledger.Record(item,new(item.Id,item.Name,Outcome.Ambiguous,"Synthetic previous release quarantine.",entry.Temp,bytes.Length,fingerprint.Hash));
            return entry;
        }).ToArray();
    }
    private static void AssertPromoted(Destination destination,byte[] bytes,CatalogEntry[] previous,bool additional)
    {
        using var catalog=new Catalog(destination);var entries=catalog.Entries();
        Fixtures.Check(entries.Count(entry=>entry.State=="Complete")==previous.Length
            &&entries.Count(entry=>entry.State=="Replaced")==previous.Length);
        foreach(var old in previous)
        {
            Fixtures.Check(File.ReadAllBytes(destination.Resolve(old.Temp)).SequenceEqual(bytes));
            var published=entries.Single(entry=>entry.Replaces==old.Id&&entry.State=="Complete");
            Fixtures.Check(Path.GetDirectoryName(published.Final)==(additional?Destination.AdditionalDirectory:"Video Porto")
                &&File.ReadAllBytes(destination.Resolve(published.Final)).SequenceEqual(bytes));
        }
        Fixtures.Check(Directory.GetFiles(additional?destination.AdditionalVideos:destination.Videos).Length==previous.Length);
    }
    private sealed class FinalInventoryAddsItem:IVideoSource
    {
        private int inventories;
        private readonly byte[] bytes=Fixtures.Video(123);
        public Inventory Inventory(CancellationToken cancel)
        {
            List<SourceItem> items=[new("current","dcim","current.mov",MediaKind.Video,bytes.Length)];
            if(++inventories>1)items.Add(new("new-final","dcim","new.mov",MediaKind.Video,bytes.Length));
            return new(items,[],true);
        }
        public Stream OpenRead(SourceItem item,CancellationToken cancel)=>item.Id=="current"?new MemoryStream(bytes,false)
            :throw new Exception("A newly discovered final-inventory resource cannot be read before a fresh pass.");
        public void Dispose(){}
    }
}

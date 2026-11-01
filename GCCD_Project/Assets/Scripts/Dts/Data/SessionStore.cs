using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
namespace GCCD.Dts
{
    public static class CsvTable
    {
        public static string Escape(string value){value=value??"";return value.IndexOfAny(new[]{',','"','\r','\n'})>=0?"\""+value.Replace("\"","\"\"")+"\"":value;}
        public static string Line(params object[] values){return string.Join(",",values.Select(v=>Escape(v==null?"":v is IFormattable?((IFormattable)v).ToString(null,CultureInfo.InvariantCulture):v.ToString())));}
    }
    public sealed class SessionStore : IDisposable
    {
        readonly FileStream sessionLock;public string Folder{get;private set;}
        public HashSet<string> Completed{get;private set;}=new HashSet<string>(StringComparer.Ordinal);
        public string[] Pending{get{return Directory.GetFiles(Path.Combine(Folder,"journal"),"*.started.json").Select(Path.GetFileName).Select(n=>n.Replace(".started.json","")).Where(id=>!Completed.Contains(id)).ToArray();}}
        public static void ValidateId(string id){if(id==null||!Regex.IsMatch(id,@"\A[A-Za-z0-9_-]{1,32}\z"))throw new ArgumentException("Use anonymous IDs containing 1-32 letters, numbers, _ or -.");}
        public static string Hash(string value){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-","").ToLowerInvariant();}
        public SessionStore(string folder,string participant,string mode,string experiment,string metadata,bool resume=false)
        {
            ValidateId(participant);if(!new[]{"DEVELOPMENT","PILOT","MAIN","TRAINING"}.Contains(mode)||!new[]{"A","B"}.Contains(experiment))throw new ArgumentException("Explicit data provenance required.");
            Folder=Path.GetFullPath(folder);string metaPath=Path.Combine(Folder,"session_metadata.json");
            if(resume){if(!File.Exists(metaPath)||File.ReadAllText(metaPath)!=metadata)throw new ArgumentException("Resume metadata/config mismatch.");}
            else if(Directory.Exists(Folder))throw new ArgumentException("New sessions cannot overwrite an existing folder.");
            Directory.CreateDirectory(Path.Combine(Folder,"journal"));
            try
            {
                sessionLock=new FileStream(Path.Combine(Folder,".session.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
                if(!resume){AtomicWrite(metaPath,metadata);AtomicWrite(metaPath+".sha256",Hash(metadata));}
                else if(!File.Exists(metaPath+".sha256")||File.ReadAllText(metaPath+".sha256")!=Hash(metadata))throw new InvalidDataException("Session metadata checksum mismatch.");
                foreach(string f in Directory.GetFiles(Path.Combine(Folder,"journal"),"*.committed.json")){ReadCommitted(f);Completed.Add(Path.GetFileName(f).Replace(".committed.json",""));}
            }
            catch{if(sessionLock!=null)sessionLock.Dispose();throw;}
        }
        string Journal(string id,string kind){ValidateId(id);return Path.Combine(Folder,"journal",id+"."+kind+".json");}
        public void Begin(string id,string snapshot)
        {if(Completed.Contains(id)||File.Exists(Journal(id,"started")))throw new ArgumentException("Trial ID already used. Interrupted trials must be committed as technical failures before proceeding.");AtomicWrite(Journal(id,"started"),snapshot);}
        public void Commit(string id,string record)
        {if(Completed.Contains(id)||!File.Exists(Journal(id,"started")))throw new ArgumentException("Duplicate or unstarted trial.");AtomicWrite(Journal(id,"committed"),"{\"sha256\":\""+Hash(record)+"\",\"record\":"+record+"}");Completed.Add(id);}
        public static string ReadCommitted(string path)
        {
            string envelope=File.ReadAllText(path);const string prefix="{\"sha256\":\"";const string middle="\",\"record\":";
            int offset=prefix.Length+64;
            if(!envelope.StartsWith(prefix,StringComparison.Ordinal)||envelope.Length<=offset+middle.Length||envelope.Substring(offset,middle.Length)!=middle||!envelope.EndsWith("}",StringComparison.Ordinal))throw new InvalidDataException("Malformed journal envelope: "+Path.GetFileName(path));
            string payload=envelope.Substring(offset+middle.Length,envelope.Length-offset-middle.Length-1);
            if(Hash(payload)!=envelope.Substring(prefix.Length,64))throw new InvalidDataException("Journal checksum mismatch: "+Path.GetFileName(path));return payload;
        }
        public void AppendEvent(string id,string kind,string data)
        {
            ValidateId(id);string path=Path.Combine(Folder,"runtime_events.csv");bool exists=File.Exists(path);
            using(var file=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read))using(var w=new StreamWriter(file,new UTF8Encoding(false))){if(!exists)w.WriteLine("trial_id,utc,event_type,payload");w.WriteLine(CsvTable.Line(id,DateTime.UtcNow.ToString("O"),kind,data));w.Flush();file.Flush(true);}
        }
        public static void AtomicWrite(string path,string data)
        {
            string tmp=path+".tmp-"+Guid.NewGuid().ToString("N");
            try{using(var f=new FileStream(tmp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var w=new StreamWriter(f,new UTF8Encoding(false))){w.Write(data);w.Flush();f.Flush(true);}if(File.Exists(path))File.Replace(tmp,path,null);else File.Move(tmp,path);}
            finally{if(File.Exists(tmp))File.Delete(tmp);}
        }
        public void Dispose(){if(sessionLock!=null)sessionLock.Dispose();}
    }
}

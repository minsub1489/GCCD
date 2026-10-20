using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace GCCD.Research
{
    public sealed class ResearchJsonlWriter : IDisposable
    {
        private StreamWriter eventsWriter;
        public string Folder { get; private set; }
        public string LastError { get; private set; }
        public bool IsReady => eventsWriter != null && string.IsNullOrEmpty(LastError);
        public long Sequence { get; private set; }

        public bool Open(ResearchSnapshot snapshot)
        {
            try
            {
                string sessionFolder = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + "_" + snapshot.session_id;
                Folder = Path.Combine(Application.persistentDataPath, "GCCDResearch", sessionFolder);
                Directory.CreateDirectory(Folder);
                string snapshotJson = JsonUtility.ToJson(snapshot, true);
                snapshot.config_hash = ComputeSha256(snapshotJson);
                File.WriteAllText(Path.Combine(Folder, "session.json"), JsonUtility.ToJson(snapshot, true), new UTF8Encoding(false));
                eventsWriter = new StreamWriter(new FileStream(
                    Path.Combine(Folder, "events.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read),
                    new UTF8Encoding(false)) { AutoFlush = true };
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Unable to create session log: " + ex.Message;
                Dispose();
                return false;
            }
        }

        public bool Write(ResearchEvent entry)
        {
            if (!IsReady) return false;
            try
            {
                entry.sequence_number = ++Sequence;
                entry.timestamp_utc = DateTime.UtcNow.ToString("O");
                eventsWriter.WriteLine(JsonUtility.ToJson(entry));
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Logging stopped: " + ex.Message;
                Dispose();
                return false;
            }
        }

        public bool WriteSurvey(ResearchSurvey survey)
        {
            if (!IsReady) return false;
            try
            {
                File.AppendAllText(Path.Combine(Folder, "surveys.jsonl"),
                    JsonUtility.ToJson(survey) + Environment.NewLine, new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                LastError = "Unable to save survey: " + ex.Message;
                Dispose();
                return false;
            }
        }

        public static string ComputeSha256(string value)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        public void Dispose()
        {
            if (eventsWriter == null) return;
            try { eventsWriter.Dispose(); }
            catch (IOException) { }
            eventsWriter = null;
        }
    }
}

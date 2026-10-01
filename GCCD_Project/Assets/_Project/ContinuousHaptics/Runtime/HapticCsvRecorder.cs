using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace GCCD.ContinuousHaptics
{
    [RequireComponent(typeof(HapticThreatController))]
    public sealed class HapticCsvRecorder : MonoBehaviour
    {
        public bool Record;
        [SerializeField] string currentFile;
        StreamWriter writer;
        HapticThreatController controller;
        readonly StringBuilder row = new StringBuilder(1024);
        public string CurrentFile => currentFile;
        void OnEnable() { controller = GetComponent<HapticThreatController>(); controller.FrameComputed += Write; }
        void Update() { if (!Record) Close(); }
        void Write(HapticThreatController c, bool sent)
        {
            if (!Record) { Close(); return; }
            try
            {
                if (writer == null)
                {
                    string folder = Path.Combine(Application.persistentDataPath, "HapticResearch");
                    Directory.CreateDirectory(folder);
                    string name = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                    currentFile = Path.Combine(folder, name + ".csv");
                    File.WriteAllText(Path.Combine(folder, name + "_settings.json"), JsonUtility.ToJson(c, true));
                    writer = new StreamWriter(currentFile, false, Encoding.UTF8) { AutoFlush = true };
                    writer.Write("utc,time,distance,azimuth,elevation,smoothed_azimuth,smoothed_elevation,target_intensity,smoothed_intensity,vest_x,vest_y,sent,status,parameters_json");
                    for (int i = 0; i < 40; i++) writer.Write(",motor_" + i);
                    for (int i = 0; i < 40; i++) writer.Write(",weight_" + i);
                    writer.WriteLine();
                }
                row.Clear(); row.Append(DateTime.UtcNow.ToString("O"));
                Add(Time.unscaledTimeAsDouble); Add(c.CurrentDistance); Add(c.CurrentAzimuth); Add(c.CurrentElevation);
                Add(c.SmoothedAzimuth); Add(c.SmoothedElevation); Add(c.TargetIntensity); Add(c.SmoothedIntensity);
                Add(c.VestPosition.x); Add(c.VestPosition.y); row.Append(sent ? ",1" : ",0");
                Quote(c.Status);
                // Per-row snapshot preserves live Inspector parameter changes for reproducibility.
                Quote(JsonUtility.ToJson(c));
                for (int i = 0; i < 40; i++) Add(c.MotorIntensity(i));
                for (int i = 0; i < 40; i++) Add(c.MotorWeight(i));
                writer.WriteLine(row.ToString());
            }
            catch (Exception e) { Record = false; Close(); Debug.LogWarning("Haptic CSV recording stopped: " + e.GetType().Name, this); }
        }
        void Add(double v) { row.Append(',').Append(v.ToString("R", CultureInfo.InvariantCulture)); }
        void Quote(string s) { row.Append(",\"").Append(s.Replace("\"", "\"\"")).Append('"'); }
        void Close() { if (writer == null) return; try { writer.Dispose(); } catch (IOException) { } finally { writer = null; } }
        void OnDisable() { if (controller) controller.FrameComputed -= Write; Close(); }
        void OnApplicationQuit() => Close();
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using GCCD.ContinuousHaptics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GCCD.AcousticResearch.PlayModeTests
{
    public sealed class ResearchRuntimeTests
    {
        const string Scene="Assets/_Project/Scenes/OffscreenThreatResearch.unity";
        ResearchExperimentSession session;
        [UnitySetUp] public IEnumerator Setup()
        { yield return SceneManager.LoadSceneAsync(Scene);session=Object.FindFirstObjectByType<ResearchExperimentSession>();session.Record=false; }
        [UnityTearDown] public IEnumerator Cleanup()
        { session.StopSession();if(session.HasPendingLogs)session.DiscardLogs();yield return null; }
        [UnityTest] public IEnumerator BlankCenterDoesNotFallAndAudioMeasurementRequiresClips()
        {
            Assert.IsTrue(session.Player.CenterLocked);var home=session.Player.transform.position;
            session.Player.CaptureCursor(true);yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(home,session.Player.transform.position);
            Assert.AreEqual(1,Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length);
            session.Router.SetCondition(ExperimentCondition.AudioOnly);Assert.IsFalse(session.StartSession(false));StringAssert.Contains("audio clips",session.Message);
        }
        [UnityTest] public IEnumerator RaycastHitRemovesTargetAndWritesActualReaction()
        {
            session.Record=true;session.TrialCount=1;session.Router.SetCondition(ExperimentCondition.None);
            Assert.IsTrue(session.StartSession(true));
            float until=Time.unscaledTime+4;
            while(!session.ThreatVisual.activeSelf && Time.unscaledTime<until)yield return null;
            Assert.IsTrue(session.ThreatVisual.activeSelf);
            Assert.IsTrue(session.Player.LookEnabled);
            Assert.AreEqual(Vector3.zero,session.Player.transform.position);
            var camera=session.Player.View;
            camera.transform.rotation=Quaternion.LookRotation(session.ThreatVisual.transform.position-camera.transform.position);
            Physics.SyncTransforms();
            Assert.IsTrue(session.Shoot(Time.unscaledTimeAsDouble));Assert.AreEqual(1,session.Hits);Assert.IsFalse(session.ThreatVisual.activeSelf);
            string file=session.CurrentFile;session.StopSession();string csv=File.ReadAllText(file);
            StringAssert.Contains("\"cue_onset\"",csv);StringAssert.Contains("\"response\"",csv);StringAssert.Contains("\"hit\"",csv);
            Assert.IsNotNull(JsonUtility.FromJson<SettingsProbe>(File.ReadAllText(Path.ChangeExtension(file,"settings.json"))));
            Assert.IsTrue(session.HasPendingLogs);Assert.IsFalse(session.StartSession(true));
            Assert.IsTrue(session.SaveLogs());Assert.IsFalse(session.HasPendingLogs);Assert.IsFalse(File.Exists(file));Assert.IsTrue(File.Exists(session.CurrentFile));
            File.Delete(session.CurrentFile);File.Delete(Path.ChangeExtension(session.CurrentFile,"settings.json"));
        }
        [System.Serializable] public sealed class SettingsProbe {public string router;}
        [UnityTest] public IEnumerator DiscardRemovesOnlyPendingBlockAndDisarmedHapticsCannotMeasure()
        {
            session.Record=true;session.TrialCount=1;Assert.IsTrue(session.StartSession(true));session.StopSession("Failed run");
            string file=session.CurrentFile;Assert.IsTrue(session.HasPendingLogs);Assert.IsTrue(session.DiscardLogs());
            Assert.IsFalse(File.Exists(file));Assert.IsFalse(File.Exists(Path.ChangeExtension(file,"settings.json")));
            session.Router.SetCondition(ExperimentCondition.HapticsOnly);Assert.IsFalse(session.StartSession(false));StringAssert.Contains("connect",session.Message);yield return null;
        }
        [UnityTest] public IEnumerator ChannelSwitchImmediatelyStopsAudioWithoutLosingLogicalEvent()
        {
            var s=session.Sources[0];var clip=AudioClip.Create("Test silence",44100,1,44100,false);s.Profile.Clip=clip;
            session.Router.SetCondition(ExperimentCondition.AudioOnly);s.Trigger(Time.unscaledTimeAsDouble);yield return null;
            Assert.IsFalse(s.Source.mute);Assert.IsTrue(s.Source.isPlaying);
            session.Router.SetCondition(ExperimentCondition.HapticsOnly);Assert.IsTrue(s.Source.mute);Assert.IsFalse(s.Source.isPlaying);Assert.IsTrue(s.CueActive);
            session.Router.SetCondition(ExperimentCondition.None);Assert.IsFalse(s.AudioPlaying);s.StopCue();s.Profile.Clip=null;Object.Destroy(clip);
        }
        [UnityTest] public IEnumerator EarlyShotIsFalseAlarmAndConditionChangeAborts()
        {
            session.TrialCount=2;Assert.IsTrue(session.StartSession(true));Assert.IsFalse(session.Shoot(Time.unscaledTimeAsDouble));Assert.AreEqual(1,session.FalseAlarms);
            session.Router.SetCondition(ExperimentCondition.AudioOnly);yield return null;Assert.IsFalse(session.Running);Assert.AreEqual(0,session.Router.ActiveCount);
        }
        [UnityTest] public IEnumerator LogicalMutedEventsExpireAndStopOnDisable()
        {
            session.Router.SetCondition(ExperimentCondition.HapticsOnly);var s=session.Sources[0];Assert.IsTrue(s.Trigger(Time.unscaledTimeAsDouble,.1f));
            Assert.IsTrue(s.CueActive);Assert.IsFalse(s.AudioPlaying);yield return new WaitForSecondsRealtime(.15f);Assert.IsFalse(s.CueActive);
            s.transform.position=new Vector3(0,0,-8);s.Trigger(Time.unscaledTimeAsDouble);session.Router.Tick(Time.unscaledTimeAsDouble);Assert.Greater(session.Router.Motor(2)+session.Router.Motor(3)+Enumerable.Range(0,40).Sum(session.Router.Motor),0);
            session.Router.enabled=false;Assert.AreEqual(0,session.Router.ActiveCount);Assert.AreEqual(0,Enumerable.Range(0,40).Sum(session.Router.Motor));
        }
        [UnityTest] public IEnumerator HapticOnlyAndBothChooseIdenticalVirtualMaskedEvents()
        {
            session.Router.Selection.Mode=HapticSelectionMode.MaskingPriority;
            var threat=session.Sources[1];threat.transform.position=new Vector3(0,0,-8);threat.Trigger(Time.unscaledTimeAsDouble);
            foreach(var m in session.Maskers)m.Trigger(Time.unscaledTimeAsDouble);
            session.Router.SetCondition(ExperimentCondition.HapticsOnly);session.Router.Tick(Time.unscaledTimeAsDouble);
            var first=session.Router.Decisions.Select(d=>d.Emitter.name+":"+d.Selected+":"+d.Masked).ToArray();
            session.Router.SetCondition(ExperimentCondition.AudioAndHaptics);session.Router.Tick(Time.unscaledTimeAsDouble+.1);
            CollectionAssert.AreEqual(first,session.Router.Decisions.Select(d=>d.Emitter.name+":"+d.Selected+":"+d.Masked).ToArray());
            Assert.IsTrue(session.Router.Decisions.Single(d=>d.Emitter==threat).Selected);Assert.IsTrue(session.Router.Decisions.Where(d=>d.Emitter.TreatAsMasker).All(d=>!d.Selected));
            Assert.LessOrEqual(Enumerable.Range(0,40).Sum(session.Router.Motor),50);yield return null;
        }
    }
}

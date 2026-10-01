using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class AcousticResearchValidation
{
    const string Kind="GCCD.Research.Validation.Kind";
    static AcousticResearchValidation() { TestRunnerApi.RegisterTestCallback(new Callbacks()); }
    [MenuItem("GCCD/Acoustic Research/Run EditMode Validation")]
    public static void EditMode() => Run(TestMode.EditMode);
    [MenuItem("GCCD/Acoustic Research/Run PlayMode Validation")]
    public static void PlayMode() => Run(TestMode.PlayMode);
    static void Run(TestMode mode)
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play Mode before validation");
        SessionState.SetString(Kind,mode.ToString());
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter {testMode=mode,assemblyNames=mode==TestMode.EditMode?
            new[]{"GCCD.ContinuousHaptics.Tests","GCCD.AcousticResearch.Tests"}:
            new[]{"GCCD.ContinuousHaptics.PlayModeTests","GCCD.AcousticResearch.PlayModeTests"}}));
    }
    sealed class Callbacks : ICallbacks
    {
        public void RunStarted(ITestAdaptor test) {}
        public void TestStarted(ITestAdaptor test) {}
        public void TestFinished(ITestResultAdaptor result) {}
        public void RunFinished(ITestResultAdaptor result)
        {
            string kind=SessionState.GetString(Kind,"");if(kind=="")return;SessionState.EraseString(Kind);
            string path=Path.GetFullPath("Logs/Research"+kind+".xml");Directory.CreateDirectory(Path.GetDirectoryName(path));
            TestRunnerApi.SaveResultToFile(result,path);
            Debug.Log("Research validation complete: "+result.TestStatus+"; results: "+path);
        }
    }
}

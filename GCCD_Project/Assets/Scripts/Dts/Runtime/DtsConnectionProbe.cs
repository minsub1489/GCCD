#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEditor;
namespace GCCD.Dts
{
    public sealed class DtsConnectionProbe:MonoBehaviour
    {
        IEnumerator Start()
        {
            var platform=GetComponent<ExperimentPlatform>();
            for(int i=0;i<20;i++){platform.ConnectEasy();if(platform.ConnectionReady)break;yield return new WaitForSecondsRealtime(.5f);}
            string report="Connected: "+platform.ConnectionReady+"\nStatus: "+platform.ConnectionText+"\n";
            if(platform.ConnectionReady&&Environment.GetEnvironmentVariable("DTS_TEST_PHYSICAL")=="1")
            {var receipt=platform.TestConnectionPulse();report+="Pulse: "+receipt.status+"\nError: "+receipt.error+"\n";yield return new WaitForSecondsRealtime(1);}
            File.WriteAllText("connection-result.txt",report);Debug.Log("DTS_CONNECTION_PROBE_FINISHED");EditorApplication.Exit(0);
        }
    }
}
#endif

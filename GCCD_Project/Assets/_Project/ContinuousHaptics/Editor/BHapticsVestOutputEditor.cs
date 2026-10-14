using GCCD.ContinuousHaptics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BHapticsVestOutput))]
public sealed class BHapticsVestOutputEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var output = (BHapticsVestOutput)target;
        EditorGUILayout.HelpBox("Use Silent Study > X40 calibration in Play Mode to record all channel responses. SDK acceptance alone cannot verify physical output.", MessageType.Info);
        EditorGUILayout.LabelField("Connection", output.Status);
        if (GUILayout.Button("Stop owned pulse")) output.Stop();
    }
}

using GCCD.ContinuousHaptics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HapticThreatController))]
public sealed class HapticThreatControllerEditor : Editor
{
    public override bool RequiresConstantRepaint() => Application.isPlaying;
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "TestAzimuth", "TestElevation", "TestDistance");
        var c = (HapticThreatController)target;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Test coordinates", EditorStyles.boldLabel);
        EditorGUILayout.Slider(serializedObject.FindProperty("TestAzimuth"), 0, 360);
        EditorGUILayout.Slider(serializedObject.FindProperty("TestElevation"), -90, 90);
        EditorGUILayout.Slider(serializedObject.FindProperty("TestDistance"), 0, c.Distance == null ? 15 : Mathf.Max(0.01f, c.Distance.FarDistance));
        serializedObject.ApplyModifiedProperties();
        EditorGUILayout.HelpBox("Test Mode: sliders or arrows (azimuth/elevation), Page Up/Down (distance), Escape (haptics off). Output remains limited by Haptic Update Rate.", MessageType.Info);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Linear curve")) SetCurve(AnimationCurve.Linear(0, 0, 1, 1));
            if (GUILayout.Button("Quadratic")) SetCurve(new AnimationCurve(new Keyframe(0, 0, 0, 0), new Keyframe(1, 1, 2, 2)));
            if (GUILayout.Button("Ease-In/Out")) SetCurve(AnimationCurve.EaseInOut(0, 0, 1, 1));
        }
        if (GUILayout.Button("STOP / Disable haptics"))
        { Undo.RecordObject(c, "Stop haptics"); c.EnableHaptics = false; c.StopNow(); EditorUtility.SetDirty(c); }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.FloatField("Current Distance", c.CurrentDistance);
            EditorGUILayout.FloatField("Current Azimuth", c.CurrentAzimuth);
            EditorGUILayout.FloatField("Current Elevation", c.CurrentElevation);
            EditorGUILayout.FloatField("Target Intensity", c.TargetIntensity);
            EditorGUILayout.FloatField("Smoothed Intensity", c.SmoothedIntensity);
            EditorGUILayout.FloatField("Smoothed Azimuth", c.SmoothedAzimuth);
            EditorGUILayout.FloatField("Smoothed Elevation", c.SmoothedElevation);
            EditorGUILayout.Vector2Field("Vest X / Y (circumference / height)", c.VestPosition);
        }
        EditorGUILayout.HelpBox(c.Status, MessageType.None);
        if (!c.DebugMode) return;
        DrawPanel(c, true); DrawPanel(c, false);
    }
    void SetCurve(AnimationCurve curve)
    {
        var c = (HapticThreatController)target;
        Undo.RecordObject(c, "Intensity curve preset"); c.Distance.IntensityCurve = curve; EditorUtility.SetDirty(c);
    }
    static void DrawPanel(HapticThreatController c, bool front)
    {
        EditorGUILayout.LabelField(front ? "FRONT (external view: wearer's right at left)" : "BACK (external view: wearer's left at left)", EditorStyles.boldLabel);
        for (int row = 0; row < 5; row++)
        {
            using (new EditorGUILayout.HorizontalScope())
                for (int visualCol = 0; visualCol < 4; visualCol++)
                {
                    int col = front ? 3 - visualCol : visualCol;
                    int index = (front ? 0 : 20) + row * 4 + col;
                    Color old = GUI.backgroundColor;
                    GUI.backgroundColor = Color.Lerp(Color.gray, Color.green, c.MotorIntensity(index) / 30f);
                    GUILayout.Box($"{index:00}: {c.MotorIntensity(index):00}", GUILayout.ExpandWidth(true));
                    GUI.backgroundColor = old;
                }
        }
    }
    void OnSceneGUI()
    {
        var c = (HapticThreatController)target;
        if (!c.DebugMode || !c.Player) return;
        Handles.color = Color.yellow;
        Handles.DrawWireArc(c.Player.position, c.Player.up, c.Player.forward, c.CurrentAzimuth, 1.5f);
        Handles.Label(c.Player.position + c.Player.up * 2,
            $"Azimuth {c.CurrentAzimuth:F1}° / Elevation {c.CurrentElevation:F1}°\nDistance {c.CurrentDistance:F2} m / Intensity {c.SmoothedIntensity:P0}");
    }
}

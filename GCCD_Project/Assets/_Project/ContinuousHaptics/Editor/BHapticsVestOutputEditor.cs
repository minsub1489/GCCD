using GCCD.ContinuousHaptics;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BHapticsVestOutput))]
public sealed class BHapticsVestOutputEditor : Editor
{
    int motor;
    float strength = 0.1f;
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var output = (BHapticsVestOutput)target;
        EditorGUILayout.HelpBox("One-shot X40 index calibration. Disable the HapticThreatController component first. Each click sends one 100 ms pulse, at most 10% intensity.", MessageType.Info);
        motor = EditorGUILayout.IntSlider("Motor index", motor, 0, 39);
        strength = EditorGUILayout.Slider("Calibration intensity", strength, 0, .1f);
        var info = X40MotorLayout.Get(motor);
        EditorGUILayout.LabelField($"{(info.IsFront ? "Front" : "Back")} / row {info.Row + 1} from top / column {motor % 4 + 1} from wearer's left");
        var controller = output.GetComponent<HapticThreatController>();
        using (new EditorGUI.DisabledScope(!Application.isPlaying || (controller && controller.isActiveAndEnabled)))
            if (GUILayout.Button("Pulse selected motor"))
            {
                var values = new int[40]; values[motor] = Mathf.FloorToInt(strength * 100);
                output.TrySend(values, 100);
            }
        if (GUILayout.Button("Stop owned pulse")) output.Stop();
    }
}

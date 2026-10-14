using UnityEngine;

namespace GCCD.SilentResearch
{
    public sealed class SilentParticipantUI : MonoBehaviour
    {
        public SilentExperimentSession Session;
        void OnGUI()
        {
            if (!Session || !Session.Running) return;
            // Identical neutral instructions in all four conditions. No event countdown, marker, or correctness.
            GUI.Box(new Rect(12, 12, 620, 100), "Reach EXIT. WASD move / mouse look / Ctrl hide / Space evade\n" +
                "F threat / G safe · 1 footsteps / 2 door / 3 growl · arrows bearing\nEsc aborts and saves. Responses: first choice only.");
            GUI.Label(new Rect(22, 73, 600, 22), "Footsteps: hide. Growl: evade away. Door: either. Exit opens after pursuit.");
            GUI.Label(new Rect(22, 91, 600, 22), "No cue: report G (safe) after " + Session.SafeReportAfterSec.ToString("0.0") + " seconds from trial reset.");
            GUI.Label(new Rect(Screen.width / 2f - 8, Screen.height / 2f - 12, 20, 24), "+");
            if (Session.Purpose == "DifficultyPilot") GUI.Label(new Rect(16, 116, 400, 26), "DIFFICULTY PILOT — excluded from main study");
            if (Session.Practice) GUI.Label(new Rect(16, 116, 300, 26), "PRACTICE — simulation may be visible");
        }
    }
}

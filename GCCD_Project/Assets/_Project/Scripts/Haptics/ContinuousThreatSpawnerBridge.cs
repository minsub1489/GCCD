using GCCD.ContinuousHaptics;
using UnityEngine;

namespace GCCD.Minimal
{
    // Optional adapter: the mapper/controller know nothing about spawners or discrete directions.
    [DefaultExecutionOrder(-100)]
    public sealed class ContinuousThreatSpawnerBridge : MonoBehaviour
    {
        public ThreatSpawner Spawner;
        public HapticThreatController Controller;
        void Update()
        {
            if (Controller && !Controller.TestMode) Controller.Target = Spawner ? Spawner.CurrentThreat : null;
        }
        void OnDisable()
        {
            if (!Controller || Controller.TestMode) return;
            Controller.Target = null; Controller.StopNow("Threat source disabled");
        }
    }
}

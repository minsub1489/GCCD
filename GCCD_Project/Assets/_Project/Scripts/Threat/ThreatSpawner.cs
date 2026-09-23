using UnityEngine;
using UnityEngine.InputSystem;

namespace GCCD.Minimal
{
    public sealed class ThreatSpawner : MonoBehaviour
    {
        [SerializeField] Transform player;
        [SerializeField] GameObject threatPrefab;
        [SerializeField, Min(.5f)] float distance = 5f;
        [SerializeField] float heightOffset = 1f;
        [SerializeField] bool enableDebugKeys = true;
        [SerializeField] ThreatDirection testDirection = ThreatDirection.Back;
        public Transform CurrentThreat { get; private set; }
        public float Distance => distance;
        public void Spawn(ThreatDirection direction) => SpawnAtAngle((int)direction*90f);
        // One target at a time. Later experiments can call this with their own seeded angles.
        public void SpawnAtAngle(float degrees)
        {
            if(!player || !threatPrefab) { Debug.LogError("ThreatSpawner requires Player and Threat Prefab.",this); return; }
            Clear();
            Vector3 forward=Vector3.ProjectOnPlane(player.forward,Vector3.up).normalized;
            Vector3 position=player.position+Quaternion.AngleAxis(degrees,Vector3.up)*forward*distance+Vector3.up*heightOffset;
            CurrentThreat=Instantiate(threatPrefab,position,Quaternion.identity).transform;
            CurrentThreat.name="Threat";
        }
        [ContextMenu("Spawn configured direction (Play Mode)")]
        void SpawnConfigured() { if(Application.isPlaying) Spawn(testDirection); }
        public void Clear()
        {
            if(CurrentThreat) { CurrentThreat.gameObject.SetActive(false); Destroy(CurrentThreat.gameObject); }
            CurrentThreat=null;
        }
        void Update()
        {
            if(!enableDebugKeys || Keyboard.current==null) return;
            var k=Keyboard.current;
            if(k.digit1Key.wasPressedThisFrame) Spawn(ThreatDirection.Front);
            if(k.digit2Key.wasPressedThisFrame) Spawn(ThreatDirection.Right);
            if(k.digit3Key.wasPressedThisFrame) Spawn(ThreatDirection.Back);
            if(k.digit4Key.wasPressedThisFrame) Spawn(ThreatDirection.Left);
            if(k.deleteKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) Clear();
        }
        void OnDisable() { if(Application.isPlaying) Clear(); }
    }
}

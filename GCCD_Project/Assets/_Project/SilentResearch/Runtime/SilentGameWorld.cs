using UnityEngine;

namespace GCCD.SilentResearch
{
    // Lightweight deterministic patrol / pursuit, using CharacterController and shared study variables.
    // No NavMesh rebuild, audio AI, or stochastic difficulty changes between feedback conditions.
    public sealed class SilentGameWorld : MonoBehaviour
    {
        public ResearchPlayerMotion Player;
        public CharacterController Monster;
        public Renderer MonsterRenderer;
        public BoxCollider[] HidingVolumes;
        public Transform Exit;
        public SilentScenario Scenario;
        public bool Hidden { get; private set; }
        public bool Crouching { get; private set; }
        public bool LastEvadeCorrect { get; private set; }
        public bool Caught { get; private set; }
        public bool ThreatVisible { get; private set; }
        public bool AvoidanceEvaluated { get; private set; }
        public bool AvoidanceSuccess { get; private set; }
        public bool AtGoal => Exit && Vector2.Distance(new Vector2(Player.transform.position.x, Player.transform.position.z),
            new Vector2(Exit.position.x, Exit.position.z)) < .9f;
        Vector3[] route;
        Vector3 origin;
        int waypoint;
        bool threat, released;
        EventType eventType;
        double releaseAt, pursuitEnd;
        float patrolPhase;
        CharacterController body;
        public void Prepare(SilentScenario scenario, SilentTrial trial)
        {
            Scenario = scenario; body = Player.GetComponent<CharacterController>();
            Player.ResetPose(scenario.PlayerStart, Quaternion.identity); SetCrouch(false);
            Player.MoveSpeed = scenario.PlayerSpeed; Player.CenterLocked = false; Player.MeasurementMode = false;
            Player.CenterLocked = true; // Same foreperiod restraint in every feedback condition.
            Exit.position = scenario.Goal;
            threat = trial.Threat; released = Caught = ThreatVisible = AvoidanceEvaluated = AvoidanceSuccess = Hidden = LastEvadeCorrect = false;
            eventType = trial.Type;
            releaseAt = pursuitEnd = double.PositiveInfinity; patrolPhase = 0; waypoint = 0;
            origin = trial.Threat ? scenario.EventAnchors[trial.DirectionIndex] : new Vector3(0, 1, 4);
            origin.y = 1;
            Monster.enabled = false; Monster.transform.position = origin; Monster.enabled = true;
            MonsterRenderer.enabled = false;
            // Route round the opaque central partitions, same route for every condition.
            Vector3 outward = origin - scenario.PlayerStart; outward.y = 0; outward.Normalize();
            Vector3 side = Quaternion.Euler(0, 90, 0) * outward;
            route = new[] { origin + side * 2.8f, scenario.PlayerStart + outward * 1.7f + side * 2.8f };
        }
        public void Onset(double now) { Player.CenterLocked = false; releaseAt = now + Scenario.ResponseWindowSec; pursuitEnd = releaseAt + Scenario.PursuitWindowSec; }
        public bool GoalAvailable(double now) => now >= pursuitEnd;
        public void SetCrouch(bool value)
        {
            if (!body && Player) body = Player.GetComponent<CharacterController>();
            if (!value && Crouching && body && Physics.CheckCapsule(Player.transform.position + Vector3.down * .6f,
                Player.transform.position + Vector3.up * .6f, body.radius, ~(1 << 2), QueryTriggerInteraction.Ignore)) value = true;
            Crouching = value;
            if (body) { body.height = value ? 1f : 1.8f; body.center = new Vector3(0, value ? -.4f : 0, 0); }
            if (Player && Player.View) Player.View.transform.localPosition = new Vector3(0, value ? -.2f : .65f, 0);
            UpdateHidden();
        }
        void UpdateHidden()
        {
            Hidden = false;
            if (!Crouching || HidingVolumes == null || !Player) return;
            foreach (var volume in HidingVolumes)
                if (volume && volume.bounds.Contains(Player.transform.position)) { Hidden = true; break; }
        }
        public bool TryEvade(Vector3 source)
        {
            Vector3 away = Player.transform.position - source; away.y = 0;
            Vector3 forward = Player.transform.forward; forward.y = 0;
            Vector3 before = Player.transform.position;
            body.Move(forward.normalized * .8f);
            LastEvadeCorrect = away.sqrMagnitude >= .01f && Vector3.Dot(forward.normalized, away.normalized) >= .25f
                && Vector3.Distance(before, Player.transform.position) >= .3f;
            return LastEvadeCorrect;
        }
        public void Tick(double now, float dt)
        {
            UpdateHidden(); ThreatVisible = false;
            if (Caught || !Scenario) return;
            dt = Mathf.Clamp(dt, 0, .05f);
            if (now >= pursuitEnd && !AvoidanceEvaluated)
            { AvoidanceEvaluated = true; AvoidanceSuccess = threat && !Caught; MonsterRenderer.enabled = false; }
            if (!threat || AvoidanceEvaluated) return;
            if (now < releaseAt)
            {
                patrolPhase += dt * Scenario.MonsterPatrolSpeed;
                Vector3 destination = origin + (route[0] - origin).normalized * Mathf.Sin(patrolPhase) * .3f;
                Move(destination, Scenario.MonsterPatrolSpeed, dt); return;
            }
            if (!released) { released = true; MonsterRenderer.enabled = true; }
            if (waypoint < route.Length)
            {
                Move(route[waypoint], Scenario.MonsterChaseSpeed, dt);
                if (HorizontalDistance(Monster.transform.position, route[waypoint]) < .25f) waypoint++;
            }
            else if ((!Hidden || eventType == EventType.Growl) && HorizontalDistance(Monster.transform.position, Player.transform.position) < Scenario.MonsterDetectionRange)
                Move(Player.transform.position, Scenario.MonsterChaseSpeed, dt);
            else Move(origin, Scenario.MonsterPatrolSpeed, dt);
            float reach = Hidden && eventType == EventType.Growl ? Mathf.Max(1.3f, Scenario.CatchRadius) : Scenario.CatchRadius;
            Caught = (!Hidden || eventType == EventType.Growl) && HorizontalDistance(Monster.transform.position, Player.transform.position) < reach;
            if (Caught) { AvoidanceEvaluated = true; AvoidanceSuccess = false; }
            var view = Player.View;
            Vector3 target = Monster.transform.position + Vector3.up * .3f;
            Vector3 vp = view.WorldToViewportPoint(target);
            ThreatVisible = MonsterRenderer.enabled && vp.z > view.nearClipPlane && vp.x >= 0 && vp.x <= 1 && vp.y >= 0 && vp.y <= 1
                && (!Physics.Linecast(view.transform.position, target, out var hit, ~(1 << 2), QueryTriggerInteraction.Ignore)
                    || hit.collider == Monster);
        }
        void Move(Vector3 target, float speed, float dt)
        {
            Vector3 delta = target - Monster.transform.position; delta.y = 0;
            if (delta.sqrMagnitude > .001f) Monster.transform.rotation = Quaternion.LookRotation(delta);
            Monster.Move(Vector3.ClampMagnitude(delta, speed * dt) + Vector3.down * 2 * dt);
        }
        static float HorizontalDistance(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        public void Stop() { if (MonsterRenderer) MonsterRenderer.enabled = false; Player.enabled = false; Player.CaptureCursor(false); }
    }
}

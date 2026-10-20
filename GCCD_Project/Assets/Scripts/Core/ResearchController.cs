using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GCCD.Research
{
    /// <summary>
    /// Attach to the scene Camera. Controls a stationary first-person 2×4 experiment.
    /// The 3D arena/camera are constructed by the project's editor scene builder.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class ResearchController : MonoBehaviour
    {
        [Header("Researcher adjustable protocol (not an approved sample-size calculation)")]
        [Min(1)] public int trialsPerBlock = 4;
        [Min(0)] public int practiceTrials = 2;
        [Min(0.1f)] public float preparationMinimum = 1.0f;
        [Min(0.1f)] public float preparationMaximum = 1.8f;
        [Min(0.1f)] public float threatDeadlineSeconds = 4.0f;
        [Min(0.2f)] public float activeTrialSeconds = 8.0f;
        [Min(0.1f)] public float restSeconds = 12f;
        [Min(1f)] public float threatDistance = 7f;
        [Min(0.2f)] public float threatDiameter = 0.9f;
        [Range(0f, 12f)] public float sourceAngleJitterDegrees = 10f;
        [Min(1f)] public float mouseSensitivity = 2.1f;
        [Range(1f, 60f)] public float cameraLogHz = 20f;
        [Range(15f, 30f)] public float motorUpdateHz = 20f;
        [Range(10f, 60f)] public float motorSpreadDegrees = 22f;
        [Range(.001f, .3f)] public float motorAttackSeconds = .045f;
        [Range(.001f, .3f)] public float motorReleaseSeconds = .08f;
        [Range(.05f, 1f)] public float motorOutputCeiling = .8f;
        [Range(.1f, 5f)] public float visualWarningSeconds = 1.2f;
        [Range(2f, 30f)] public float nearMissInferenceDegrees = 12f;
        [Range(0f, 100f)] public float damagePerMiss = 10f;

        private static readonly ResearchCondition[] Conditions =
        {
            new ResearchCondition { code = "S0", threat_count = 1, intensity_level = "none" },
            new ResearchCondition { code = "SL", threat_count = 1, intensity_level = "low" },
            new ResearchCondition { code = "SM", threat_count = 1, intensity_level = "medium" },
            new ResearchCondition { code = "SH", threat_count = 1, intensity_level = "high" },
            new ResearchCondition { code = "M0", threat_count = 3, intensity_level = "none" },
            new ResearchCondition { code = "ML", threat_count = 3, intensity_level = "low" },
            new ResearchCondition { code = "MM", threat_count = 3, intensity_level = "medium" },
            new ResearchCondition { code = "MH", threat_count = 3, intensity_level = "high" }
        };

        private sealed class Threat
        {
            public string id;
            public TorsoZone zone;
            public GameObject gameObject;
            public Collider collider;
            public bool spawnValid;
            public string spawnError;
            public bool acquired;
            public bool attempted;
            public bool neutralized;
            public bool attacked;
            public double firstAcquisition = -1;
            public double firstAttempt = -1;
            public double neutralizedAt = -1;
            public double deadline;
            public float angle;
            public Vector3 position;
        }

        private readonly List<Threat> threats = new List<Threat>(3);
        private readonly Dictionary<Collider, Threat> threatByCollider = new Dictionary<Collider, Threat>();
        private readonly int[] surveyRatings = new int[9];
        private readonly bool[] surveyAnswered = new bool[9];

        private Camera researchCamera;
        private AudioClip warningClip;
        private IResearchHapticOutput output;
        private ResearchJsonlWriter logger;
        private ResearchCondition[] assignedOrder;

        private StudyMode mode = StudyMode.Demo;
        private StudyPhase phase = StudyPhase.Setup;
        private string participantCode = "";
        private string sequenceOverride = "";
        private string protocolError = "";
        private string sessionId;
        private string trialId = "";
        private int assignedSequence;
        private int scheduleSeed;
        private int blockIndex;
        private int trialIndex;
        private int practiceCompleted;
        private int shotIndex;
        private int hitCount;
        private int failedCount;
        private bool currentIsPractice;
        private bool currentTrialValid;
        private bool sessionClosed;
        private string firstShotTarget = "";
        private float hp;
        private Vector3 fixedPosition;
        private Quaternion initialRotation;
        private float pitch;
        private float lastYaw;
        private float yawPath;
        private float previousSign;
        private float accumulatedInputX;
        private float accumulatedInputY;
        private int turnReversals;
        private double sessionStart;
        private double warningAt;
        private double warningTriggeredAt;
        private double trialEndAt;
        private double nextCameraSample;
        private double reviewCompletedAt;
        private double restUntil;
        private float lastCameraSampleYaw;
        private bool cursorWasLocked;

        private static double Now => Time.realtimeSinceStartupAsDouble;
        private ResearchCondition CurrentCondition =>
            currentIsPractice ? Conditions[practiceCompleted % Conditions.Length] :
            assignedOrder != null && blockIndex < assignedOrder.Length ? assignedOrder[blockIndex] : null;

        private void Awake()
        {
            researchCamera = GetComponent<Camera>();
            fixedPosition = transform.position;
            initialRotation = transform.rotation;
            pitch = NormalizePitch(transform.localEulerAngles.x);
            output = new ResearchMockHaptics();
            InitializeSpatialMotors();
            warningClip = CreateWarningTone();
            SetCursor(false);
        }

        private void Update()
        {
            if (phase == StudyPhase.Preparation || phase == StudyPhase.Active)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    EndTrial("operator_abort_escape");
                    AbortSession("operator_aborted_session_escape");
                    return;
                }
                transform.position = fixedPosition;
                UpdateViewRotation();
                if (phase == StudyPhase.Preparation && Now >= warningAt) StartWarning();
                if (phase == StudyPhase.Active)
                {
                    if (Now >= trialEndAt)
                    {
                        UpdateDeadlines();
                        EndTrial("fixed_duration_elapsed");
                    }
                    else
                    {
                        UpdateDeadlines(); // Missed deadlines resolve BEFORE accepting a shot on that frame.
                        if (phase != StudyPhase.Active) return;
                        UpdateAcquisition();
                        if (phase != StudyPhase.Active) return;
                        UpdateShooting();
                        if (phase != StudyPhase.Active) return;
                        UpdateMotorTracking(false);
                        UpdateCameraLogging();
                    }
                }
            }
        }

        private void SetCursor(bool locked)
        {
            cursorWasLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private static float NormalizePitch(float angle) => angle > 180 ? angle - 360 : angle;

        private void UpdateViewRotation()
        {
            float dx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float dy = Input.GetAxis("Mouse Y") * mouseSensitivity;
            accumulatedInputX += dx;
            accumulatedInputY += dy;
            pitch = Mathf.Clamp(pitch - dy, -79f, 79f);
            float yaw = transform.eulerAngles.y + dx;
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);

            if (phase != StudyPhase.Active) return;
            float delta = Mathf.DeltaAngle(lastYaw, yaw);
            yawPath += Mathf.Abs(delta);
            if (Mathf.Abs(delta) >= 0.15f)
            {
                float sign = Mathf.Sign(delta);
                if (previousSign != 0 && previousSign != sign) turnReversals++;
                previousSign = sign;
            }
            lastYaw = yaw;
        }

        private static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (int i = 0; i < text.Length; ++i)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }
                return (int)(hash & 0x7fffffff);
            }
        }

        // Balanced Latin square / Williams counterbalancing for eight treatments.
        // The assigned order is saved, so an analyst never has to reconstruct it from an ID.
        private static ResearchCondition[] BuildOrder(int sequence)
        {
            int[] baseRow = { 0, 7, 1, 6, 2, 5, 3, 4 };
            var result = new ResearchCondition[8];
            for (int i = 0; i < result.Length; i++)
            {
                int shifted = (baseRow[i] + sequence) % 8;
                result[i] = Conditions[shifted];
            }
            return result;
        }

        private string BuildOrderText()
        {
            int index;
            if (!string.IsNullOrWhiteSpace(sequenceOverride) &&
                int.TryParse(sequenceOverride, out index) && index >= 0 && index < 8)
                return string.Join("  →  ", Array.ConvertAll(BuildOrder(index), c => c.code));
            if (string.IsNullOrWhiteSpace(participantCode)) return "ID 입력 후 8개 조건의 균형 순서를 표시합니다.";
            return string.Join("  →  ", Array.ConvertAll(BuildOrder(StableHash(participantCode.Trim()) % 8), c => c.code));
        }

        private bool ValidateSetup()
        {
            participantCode = participantCode.Trim();
            if (participantCode.Length < 2 || participantCode.Length > 32)
            {
                protocolError = "익명 참가자 코드는 2~32자로 입력하세요."; return false;
            }
            foreach (char c in participantCode)
                if (!(char.IsLetterOrDigit(c) && c <= 127) && c != '-' && c != '_')
                {
                    protocolError = "코드는 영문·숫자·-·_만 허용합니다. 이름이나 연락처를 사용하지 마세요.";
                    return false;
                }
            if (!string.IsNullOrWhiteSpace(sequenceOverride) &&
                (!int.TryParse(sequenceOverride, out int row) || row < 0 || row > 7))
            {
                protocolError = "순서 오버라이드는 비워두거나 0~7로 지정하세요."; return false;
            }
            if (trialsPerBlock < 1 || trialsPerBlock > 200 || practiceTrials < 0 || practiceTrials > 100 ||
                preparationMinimum < .1f || preparationMaximum < preparationMinimum ||
                threatDeadlineSeconds <= 0 || activeTrialSeconds <= threatDeadlineSeconds ||
                threatDistance < 1 || threatDiameter <= .1f || cameraLogHz < 1 ||
                sourceAngleJitterDegrees < 0 || sourceAngleJitterDegrees > 12f ||
                motorUpdateHz < 15f || motorUpdateHz > 30f ||
                motorSpreadDegrees < 10f || motorSpreadDegrees > 60f ||
                motorAttackSeconds < .001f || motorAttackSeconds > .3f ||
                motorReleaseSeconds < .001f || motorReleaseSeconds > .3f ||
                motorOutputCeiling < .05f || motorOutputCeiling > 1f ||
                visualWarningSeconds < .1f || visualWarningSeconds > 5f ||
                nearMissInferenceDegrees < 2f || nearMissInferenceDegrees > 30f)
            {
                protocolError = "프로토콜 값 오류: 준비 최대≥최소, 0<deadline<trial, 거리/직경/샘플주파수>0, 조건당 1~200시행.";
                return false;
            }
            if (!motorProfile.Validate(out protocolError, mode != StudyMode.Demo)) return false;
            if (mode != StudyMode.Demo && (!output.IsPhysicalHardware || !output.IsVerifiedReady))
            {
                protocolError = "Pilot/Main 시작 차단: 검증된 SDK·40모터 인덱스·방위각·강도 교정이 필요합니다.";
                return false;
            }
            protocolError = "";
            return true;
        }

        private void StartSession()
        {
            if (!ValidateSetup()) return;
            sessionId = Guid.NewGuid().ToString("N");
            sessionStart = Now;
            assignedSequence = string.IsNullOrWhiteSpace(sequenceOverride)
                ? StableHash(participantCode) % 8 : int.Parse(sequenceOverride, CultureInfo.InvariantCulture);
            scheduleSeed = StableHash(participantCode);
            assignedOrder = BuildOrder(assignedSequence);
            blockIndex = 0;
            trialIndex = 0;
            practiceCompleted = 0;
            sessionClosed = false;
            var snapshot = new ResearchSnapshot
            {
                session_id = sessionId,
                participant_id = participantCode,
                mode = mode.ToString(),
                started_at_utc = DateTime.UtcNow.ToString("O"),
                haptic_backend = output.Name,
                hardware_verified = output.IsPhysicalHardware && output.IsVerifiedReady,
                sequence_index = assignedSequence,
                schedule_seed = scheduleSeed,
                trials_per_block = trialsPerBlock,
                practice_trials = practiceTrials,
                preparation_min_seconds = preparationMinimum,
                preparation_max_seconds = preparationMaximum,
                threat_deadline_seconds = threatDeadlineSeconds,
                active_duration_seconds = activeTrialSeconds,
                target_distance = threatDistance,
                target_size = threatDiameter,
                source_angle_jitter_degrees = sourceAngleJitterDegrees,
                damage_per_failure = damagePerMiss,
                mouse_sensitivity = mouseSensitivity,
                camera_sample_hz = cameraLogHz,
                haptic_pulse_seconds = 0f, // Removed short pulse in continuous tracking v2.
                visual_warning_seconds = visualWarningSeconds,
                near_miss_inference_angle_degrees = nearMissInferenceDegrees,
                motor_update_hz = motorUpdateHz,
                motor_spread_degrees = motorSpreadDegrees,
                motor_attack_seconds = motorAttackSeconds,
                motor_release_seconds = motorReleaseSeconds,
                motor_output_ceiling = motorOutputCeiling,
                motor_layout_status = motorProfile.physicalLayoutVerified
                    ? "physical_layout_flag_set_sdk_not_verified" : "virtual_unverified_no_physical_output",
                motor_profile = motorProfile,
                rest_seconds = restSeconds,
                assigned_order = assignedOrder
            };
            logger = new ResearchJsonlWriter();
            if (!logger.Open(snapshot))
            {
                protocolError = logger.LastError;
                phase = StudyPhase.Error;
                return;
            }
            Log("session_start", null, p =>
            {
                p.event_source = "operator";
                p.reason = "research_session_started_continuous_360_motor_protocol_v2";
            });
            if (phase == StudyPhase.Error) return;
            Log("motor_profile", null, p =>
            {
                p.event_source = "40_channel_profile_session_snapshot";
                p.reason = motorProfile.provenance;
                p.output_status = motorProfile.physicalLayoutVerified
                    ? "operator_layout_flag_unverified_sdk" : "virtual_layout_not_physically_verified";
                p.active_sound_sources = 0;
            });
            if (phase == StudyPhase.Error) return;
            phase = practiceTrials > 0 ? StudyPhase.PracticeReady : StudyPhase.BlockReady;
            SetCursor(false);
        }

        private void StartBlock()
        {
            if (phase != StudyPhase.BlockReady || assignedOrder == null) return;
            if (trialIndex == 0)
                Log("block_start", null, p =>
                {
                    p.event_source = "operator"; p.reason = "counterbalanced_block";
                    p.block_index = blockIndex;
                });
            if (phase == StudyPhase.Error) return;
            StartTrial(false);
        }

        private void StartTrial(bool practice)
        {
            StopMotorTracking();
            ClearThreats();
            currentIsPractice = practice;
            trialId = sessionId + (practice ? "_P" + (practiceCompleted + 1).ToString("D3") :
                "_B" + (blockIndex + 1).ToString("D2") + "_T" + (trialIndex + 1).ToString("D3"));
            // Same per-participant, threat count and repetition, regardless of intensity.
            // Counterbalanced condition order does not change paired timing.
            int pairedSeed = StableHash(scheduleSeed + "|" + (practice ? "P" : "T") + "|" +
                                        (practice ? practiceCompleted : trialIndex) + "|" +
                                        CurrentCondition.threat_count);
            float jitter = Mathf.Lerp(preparationMinimum, preparationMaximum,
                (float)new System.Random(pairedSeed).NextDouble());
            warningAt = Now + jitter;
            warningTriggeredAt = 0;
            trialEndAt = warningAt + activeTrialSeconds;
            nextCameraSample = warningAt;
            currentTrialValid = true;
            hp = 100;
            shotIndex = 0;
            firstShotTarget = "";
            hitCount = 0;
            failedCount = 0;
            yawPath = 0;
            accumulatedInputX = 0;
            accumulatedInputY = 0;
            turnReversals = 0;
            previousSign = 0;
            transform.position = fixedPosition;
            transform.rotation = initialRotation;  // Equal initial gaze for every trial.
            pitch = NormalizePitch(transform.eulerAngles.x);
            lastYaw = transform.eulerAngles.y;
            phase = StudyPhase.Preparation;
            SetCursor(true);
            Log("trial_start", null, p =>
            {
                p.event_source = "scheduler";
                p.planned_threats = CurrentCondition.threat_count;
                p.deadline_time = threatDeadlineSeconds;
                p.valid = true;
                p.reason = "preparation_jitter_seconds=" + jitter.ToString("F5", CultureInfo.InvariantCulture);
            });
        }

        private static float AngleForZone(TorsoZone zone)
        {
            switch (zone)
            {
                case TorsoZone.FrontLeft: return -70f;
                case TorsoZone.FrontRight: return 70f;
                case TorsoZone.BackLeft: return -130f;
                default: return 130f;
            }
        }

        private List<TorsoZone> PlanZones()
        {
            // Repeat the exact same sector schedule across intensity conditions.
            // Four single sectors and complementary 3-of-4 combinations are balanced
            // when trialsPerBlock is a multiple of four; order does not alter geometry.
            int index = currentIsPractice ? practiceCompleted : trialIndex;
            int cycle = index % 4;
            int count = CurrentCondition.threat_count;
            var planned = new List<TorsoZone>(count);
            if (count == 1) planned.Add((TorsoZone)cycle);
            else
                for (int i = 0; i < 4; ++i)
                    if (i != cycle) planned.Add((TorsoZone)i);
            return planned;
        }

        private void StartWarning()
        {
            if (phase != StudyPhase.Preparation) return;
            warningTriggeredAt = Now;
            trialEndAt = warningTriggeredAt + activeTrialSeconds;
            phase = StudyPhase.Active;
            BeginMotorTracking();
            lastYaw = transform.eulerAngles.y;
            lastCameraSampleYaw = lastYaw;
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(researchCamera);
            Vector3 facing = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            if (facing.sqrMagnitude < .5f) facing = Vector3.forward;
            List<TorsoZone> zones = PlanZones();
            foreach (TorsoZone zone in zones)
            {
                // Continuous source bearing within each balanced spawn sector. Seed
                // ignores condition intensity and block index, so comparisons pair
                // identical directions for same participant/threat count/repetition.
                int repeatedIndex = currentIsPractice ? practiceCompleted : trialIndex;
                int angleSeed = StableHash(scheduleSeed + "|" +
                    (currentIsPractice ? "P" : "T") + "|" +
                    CurrentCondition.threat_count + "|" + repeatedIndex + "|" + zone);
                float angle = AngleForZone(zone) +
                    (((float)new System.Random(angleSeed).NextDouble() * 2f) - 1f) *
                    sourceAngleJitterDegrees;
                Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * facing;
                Vector3 worldPosition = fixedPosition + direction * threatDistance + Vector3.down * .35f;
                // Reject a spawn if any part of the complete target volume can be seen.
                bool outside = !GeometryUtility.TestPlanesAABB(frustumPlanes,
                    new Bounds(worldPosition,
                        new Vector3(threatDiameter, threatDiameter * 2.4f, threatDiameter)));
                float actualAngularOffset = Vector3.Angle(facing, direction);
                Vector3 towardTarget = worldPosition - fixedPosition;
                bool obstructed = Physics.Raycast(fixedPosition, towardTarget.normalized,
                    towardTarget.magnitude - threatDiameter * .5f, ~0, QueryTriggerInteraction.Ignore);
                bool overlapping = Physics.CheckSphere(worldPosition, threatDiameter * .5f, ~0,
                    QueryTriggerInteraction.Ignore);
                string invalidReason = !outside ? "visible_at_warning" :
                    obstructed ? "occluded_by_scene_geometry" :
                    overlapping ? "overlap_with_scene_geometry" : "";
                var threat = new Threat
                {
                    id = trialId + "_" + zone,
                    zone = zone,
                    angle = actualAngularOffset * Mathf.Sign(angle),
                    position = worldPosition,
                    spawnValid = string.IsNullOrEmpty(invalidReason),
                    spawnError = invalidReason,
                    deadline = warningTriggeredAt + threatDeadlineSeconds
                };
                threats.Add(threat);
                if (threat.spawnValid)
                {
                    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = "Threat_" + zone + "_" + trialId;
                    go.transform.position = worldPosition;
                    go.transform.localScale = Vector3.one * threatDiameter;
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null) renderer.enabled = false;
                    Vector3 towardPlayer = fixedPosition - worldPosition;
                    towardPlayer.y = 0f;
                    if (towardPlayer.sqrMagnitude > .001f)
                        go.transform.rotation = Quaternion.LookRotation(towardPlayer.normalized);
                    ConstructHostileSilhouette(go);
                    threat.gameObject = go;
                    threat.collider = go.GetComponent<Collider>();
                    if (threat.collider != null) threatByCollider[threat.collider] = threat;
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 1f;
                    source.rolloffMode = AudioRolloffMode.Logarithmic;
                    source.minDistance = .75f;
                    source.maxDistance = 24f;
                    source.volume = .8f;
                    source.loop = false;
                    source.clip = warningClip;
                    source.Play();  // Same brief audible cue in every condition; world sound origin stays registered.
                }
                else
                {
                    currentTrialValid = false;
                    Log("invalid_spawn", threat, p =>
                    {
                        p.event_source = "geometry_validator";
                        p.reason = invalidReason; p.valid = false;
                    });
                }
                Log("spawn", threat, p =>
                {
                    p.event_source = "scheduler";
                    p.valid = threat.spawnValid;
                    p.validity = threat.spawnValid ? "valid" : "invalid";
                    p.reason = invalidReason;
                    p.spawn_angle_deg = threat.angle;
                    p.world_x = worldPosition.x; p.world_y = worldPosition.y; p.world_z = worldPosition.z;
                    p.deadline_time = threatDeadlineSeconds;
                });
                if (phase == StudyPhase.Error) return;
            }
            // Emit a warning for every scheduled target, even if geometry invalidated a trial.
            for (int i = 0; i < threats.Count; ++i)
            {
                if (phase != StudyPhase.Active) break;
                Threat target = threats[i];
                Log("warning", target, p =>
                {
                    p.event_source = "visual_and_spatial_audio";
                    p.valid = target.spawnValid;
                    p.deadline_time = threatDeadlineSeconds;
                    p.warning_duration = visualWarningSeconds;
                });
                if (phase != StudyPhase.Active) break;
                BeginTargetHaptic(target);
            }
            if (phase == StudyPhase.Active) UpdateMotorTracking(true);
            UpdateCameraLogging();
        }

        private void UpdateAcquisition()
        {
            if (!Physics.Raycast(researchCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0)),
                out RaycastHit hit, 100f, ~0, QueryTriggerInteraction.Ignore)) return;
            if (!threatByCollider.TryGetValue(hit.collider, out Threat threat) ||
                !threat.spawnValid || threat.neutralized || threat.attacked || threat.acquired) return;
            threat.acquired = true;
            threat.firstAcquisition = Now - warningTriggeredAt;
            Log("first_acquired", threat, p =>
            {
                p.event_source = "camera_center_raycast";
                p.acquired = true;
                p.first_acquisition_time = threat.firstAcquisition;
                p.deadline_time = threatDeadlineSeconds;
            });
        }

        private void UpdateShooting()
        {
            if (!Input.GetMouseButtonDown(0) || Now >= trialEndAt) return;
            shotIndex++;
            Ray shotRay = researchCamera.ViewportPointToRay(new Vector3(.5f, .5f, 0));
            bool rayHit = Physics.Raycast(shotRay, out RaycastHit hit, 100f, ~0,
                QueryTriggerInteraction.Ignore);
            Threat directHit = null;
            if (rayHit) threatByCollider.TryGetValue(hit.collider, out directHit);
            Threat inferredTarget = null;
            float smallestAngle = float.MaxValue;
            foreach (Threat candidate in threats)
            {
                if (!candidate.spawnValid || candidate.neutralized || candidate.attacked ||
                    Now >= candidate.deadline) continue;
                float angle = Vector3.Angle(shotRay.direction, candidate.position - shotRay.origin);
                if (angle >= smallestAngle) continue;
                smallestAngle = angle;
                inferredTarget = candidate;
            }
            bool inferredNearMiss = directHit == null && inferredTarget != null &&
                                    smallestAngle <= nearMissInferenceDegrees;
            Threat intended = directHit ?? (inferredNearMiss ? inferredTarget : null);
            string attribution = directHit != null ? "direct_collider" :
                                 inferredNearMiss ? "angle_proximity_inference" : "unattributed";
            if (shotIndex == 1) firstShotTarget = intended != null ? intended.id : "";
            Log("shot", intended, p =>
            {
                p.event_source = "mouse_primary";
                p.shot_index = shotIndex;
                p.first_shot_target = firstShotTarget;
                p.hit_target_id = directHit != null ? directHit.id : "";
                p.inferred_target_id = intended != null ? intended.id : "";
                p.attribution_method = attribution;
                p.aim_error_angle_deg = smallestAngle == float.MaxValue ? -1 : smallestAngle;
                p.shot_ray_x = shotRay.direction.x;
                p.shot_ray_y = shotRay.direction.y;
                p.shot_ray_z = shotRay.direction.z;
                p.reason = directHit != null && directHit.attacked ? "after_deadline" :
                    directHit != null && directHit.neutralized ? "already_neutralized" :
                    directHit != null ? "active_target" :
                    inferredNearMiss ? "near_miss_intent_inferred" :
                    rayHit ? "non_target_collider" : "no_raycast_hit";
            });
            if (phase == StudyPhase.Error || intended == null || !intended.spawnValid ||
                intended.attacked || intended.neutralized || Now >= intended.deadline) return;
            if (!intended.attempted)
            {
                intended.attempted = true;
                intended.firstAttempt = Now - warningTriggeredAt;
                Log("response_attempt", intended, p =>
                {
                    p.event_source = attribution;
                    p.attempted = true;
                    p.shot_index = shotIndex;
                    p.first_shot_target = firstShotTarget;
                    p.attribution_method = attribution;
                    p.aim_error_angle_deg = smallestAngle == float.MaxValue ? -1 : smallestAngle;
                });
            }
            if (phase == StudyPhase.Error || directHit != intended) return;
            hitCount++;
            Log("hit", intended, p => { p.event_source = "camera_center_raycast"; p.hit_count = hitCount; });
            if (phase == StudyPhase.Error) return;
            intended.neutralized = true;  // Identical target HP = 1.
            intended.neutralizedAt = Now - warningTriggeredAt;
            if (intended.gameObject != null) intended.gameObject.SetActive(false);
            UpdateMotorTracking(true);
            if (phase == StudyPhase.Error) return;
            Log("neutralized", intended, p =>
            {
                p.event_source = "one_hit_to_neutralize";
                p.neutralized = true;
                p.attempted = true;
                p.neutralization_time = intended.neutralizedAt;
            });
        }

        private void UpdateDeadlines()
        {
            for (int i = 0; i < threats.Count; ++i)
            {
                Threat threat = threats[i];
                if (threat.neutralized || threat.attacked || Now < threat.deadline) continue;
                threat.attacked = true;
                if (!threat.spawnValid) continue; // Invalid trials are excluded, never silently counted as misses.
                if (threat.gameObject != null)
                {
                    AudioSource sound = threat.gameObject.GetComponent<AudioSource>();
                    if (sound != null) sound.Stop();
                }
                UpdateMotorTracking(true);
                if (phase == StudyPhase.Error) return;
                failedCount++;
                hp = Mathf.Max(0f, hp - damagePerMiss); // No death mechanic.
                Log("attack", threat, p =>
                {
                    p.event_source = "deadline_scheduler";
                    p.deadline_failed = true;
                    p.attempted = threat.attempted;
                    p.acquired = threat.acquired;
                    p.reason = threat.attempted ? "attempt_without_neutralization" :
                        threat.acquired ? "acquired_without_shot" : "no_targeted_response";
                    p.deadline_time = threatDeadlineSeconds;
                    p.damage = damagePerMiss;
                    p.hp_remaining = hp;
                    p.failed_count = failedCount;
                });
                if (phase == StudyPhase.Error) return;
                Log("damage", threat, p =>
                {
                    p.event_source = "nonlethal_hp";
                    p.damage = damagePerMiss; p.hp_remaining = hp;
                });
                if (phase == StudyPhase.Error) return;
            }
        }

        private void UpdateCameraLogging()
        {
            double now = Now;
            if (now < nextCameraSample || phase != StudyPhase.Active) return;
            nextCameraSample = now + 1.0 / cameraLogHz;
            float yaw = transform.eulerAngles.y;
            float yawDelta = Mathf.DeltaAngle(lastCameraSampleYaw, yaw);
            lastCameraSampleYaw = yaw;
            float sampledInputX = accumulatedInputX;
            float sampledInputY = accumulatedInputY;
            accumulatedInputX = 0;
            accumulatedInputY = 0;
            Log("camera_sample", null, p =>
            {
                p.event_source = "camera_transform";
                p.camera_yaw = yaw;
                p.camera_pitch = pitch;
                p.yaw_delta_deg = yawDelta;
                p.input_delta_x = sampledInputX;
                p.input_delta_y = sampledInputY;
                p.yaw_path_deg = yawPath;
                p.turn_reversals = turnReversals;
                p.world_x = transform.position.x;
                p.world_y = transform.position.y;
                p.world_z = transform.position.z;
            });
        }

        private void EndTrial(string reason)
        {
            if (phase != StudyPhase.Active && phase != StudyPhase.Preparation) return;
            StopMotorTracking();
            // Trial_end summarizes every scheduled target with planned and valid counts.
            int validTargets = 0;
            foreach (Threat threat in threats) if (threat.spawnValid) validTargets++;
            if (reason != "fixed_duration_elapsed") currentTrialValid = false;
            Log("trial_end", null, p =>
            {
                p.event_source = "trial_scheduler";
                p.planned_threats = CurrentCondition.threat_count;
                p.deadline_time = threatDeadlineSeconds;
                p.actual_threats = validTargets;
                p.failed_count = failedCount;
                p.hit_count = hitCount;
                p.response_attempts = shotIndex;
                p.first_shot_target = firstShotTarget;
                p.yaw_path_deg = yawPath;
                p.turn_reversals = turnReversals;
                p.hp_remaining = hp;
                p.valid = currentTrialValid;
                p.validity = currentTrialValid ? "valid" : "invalid";
                p.reason = reason;
            });
            if (phase == StudyPhase.Error) return;
            ClearThreats();
            SetCursor(false);
            reviewCompletedAt = Now;
            if (currentIsPractice) practiceCompleted++;
            else trialIndex++;
            phase = StudyPhase.TrialReview;
        }

        private void AdvanceTrialReview()
        {
            if (phase != StudyPhase.TrialReview) return;
            if (currentIsPractice)
            {
                phase = practiceCompleted < practiceTrials ? StudyPhase.PracticeReady : StudyPhase.BlockReady;
                if (phase == StudyPhase.BlockReady) currentIsPractice = false;
            }
            else
                phase = trialIndex < trialsPerBlock ? StudyPhase.BlockReady : StudyPhase.Survey;
            if (phase == StudyPhase.Survey)
            {
                Array.Clear(surveyAnswered, 0, surveyAnswered.Length);
                for (int i = 0; i < surveyRatings.Length; i++) surveyRatings[i] = 50;
            }
        }

        private void CompleteSurvey()
        {
            if (phase != StudyPhase.Survey) return;
            foreach (bool answered in surveyAnswered) if (!answered)
            {
                protocolError = "모든 설문 문항을 직접 확인·응답한 후 저장할 수 있습니다.";
                return;
            }
            var survey = new ResearchSurvey
            {
                session_id = sessionId,
                participant_id = participantCode,
                condition = assignedOrder[blockIndex].code,
                block_index = blockIndex,
                mental_demand = surveyRatings[0],
                physical_demand = surveyRatings[1],
                temporal_demand = surveyRatings[2],
                performance_poor = surveyRatings[3],
                effort = surveyRatings[4],
                frustration = surveyRatings[5],
                directional_difficulty = surveyRatings[6],
                selection_difficulty = surveyRatings[7],
                discomfort = surveyRatings[8],
                completed_at_utc = DateTime.UtcNow.ToString("O")
            };
            if (!logger.WriteSurvey(survey))
            {
                AbortSession(logger.LastError);
                return;
            }
            Log("survey", null, p =>
            {
                p.event_source = "participant_raw_unweighted";
                p.survey_json = JsonUtility.ToJson(survey);
                p.reason = "6_NASA_TLX_raw_plus_3_separate_questions";
            });
            if (phase == StudyPhase.Error) return;
            Log("block_end", null, p => { p.event_source = "scheduler"; p.reason = "survey_completed"; });
            if (phase == StudyPhase.Error) return;
            blockIndex++;
            trialIndex = 0;
            protocolError = "";
            if (blockIndex >= assignedOrder.Length)
            {
                FinishSession();
                return;
            }
            restUntil = Now + restSeconds;
            phase = StudyPhase.Rest;
        }

        private void FinishSession()
        {
            if (sessionClosed) return;
            Log("session_end", null, p => { p.event_source = "scheduler"; p.reason = "completed"; });
            if (phase == StudyPhase.Error) return;
            sessionClosed = true;
            logger?.Dispose();
            StopMotorTracking();
            phase = StudyPhase.Finished;
            SetCursor(false);
        }

        private void AbortSession(string message)
        {
            protocolError = message;
            currentTrialValid = false;
            if (!sessionClosed && logger != null && logger.IsReady)
            {
                Log("session_end", null, p =>
                {
                    p.event_source = "safety_or_io_guard";
                    p.valid = false;
                    p.reason = message;
                });
            }
            sessionClosed = true;
            logger?.Dispose();
            StopMotorTracking();
            ClearThreats();
            phase = StudyPhase.Error;
            SetCursor(false);
        }

        private void Log(string eventType, Threat target, Action<ResearchEventPayload> fill)
        {
            if (logger == null || !logger.IsReady) return;
            double now = Now;
            var payload = new ResearchEventPayload
            {
                mode = mode.ToString(),
                phase = phase.ToString(),
                output_mode = output.IsPhysicalHardware && output.IsVerifiedReady ? "real" : "mock",
                device_verified = output.IsPhysicalHardware && output.IsVerifiedReady,
                motor_profile_id = motorProfile != null ? motorProfile.profileId : "",
                practice = currentIsPractice,
                block_index = currentIsPractice ? -1 : blockIndex,
                trial_index = currentIsPractice ? practiceCompleted : trialIndex,
                schedule_seed = scheduleSeed,
                zone = target != null ? target.zone.ToString() : "",
                hp_remaining = hp,
                planned_threats = CurrentCondition != null ? CurrentCondition.threat_count : 0,
                valid = target == null || target.spawnValid
            };
            fill?.Invoke(payload);
            ResearchCondition condition = CurrentCondition;
            var record = new ResearchEvent
            {
                event_type = eventType,
                session_id = sessionId ?? "",
                trial_id = trialId ?? "",
                participant_id = participantCode ?? "",
                condition = condition != null ? condition.code : "",
                threat_count = condition != null ? condition.threat_count : 0,
                intensity_level = condition != null ? condition.intensity_level : "",
                trial_time = warningAt > 0 ? now - (warningTriggeredAt > 0 ? warningTriggeredAt : warningAt) : 0,
                session_time_monotonic = now - sessionStart,
                target_id = target != null ? target.id : "",
                payload = payload
            };
            if (!logger.Write(record))
                AbortSession(logger.LastError);
        }

        private static AudioClip CreateWarningTone()
        {
            const int frequency = 44100;
            const float seconds = .22f;
            int samples = Mathf.CeilToInt(frequency * seconds);
            var buffer = new float[samples];
            for (int i = 0; i < samples; ++i)
            {
                float t = (float)i / frequency;
                float envelope = Mathf.Sin(Mathf.PI * t / seconds);
                buffer[i] = envelope * envelope * (.14f * Mathf.Sin(2 * Mathf.PI * 740f * t) +
                                                    .07f * Mathf.Sin(2 * Mathf.PI * 940f * t));
            }
            AudioClip clip = AudioClip.Create("EqualSpatialWarningAllConditions", samples, 1, frequency, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        // Visual primitives intentionally share the original target's single collider.
        // Disabling child colliders immediately prevents inaccurate rays and inflated
        // collision/occlusion footprints. The root sphere remains the sole hit target.
        private static void ConstructHostileSilhouette(GameObject root)
        {
            Color armor = new Color(.17f, .22f, .25f);
            Color fabric = new Color(.08f, .11f, .13f);
            AddHostilePart(root, PrimitiveType.Capsule, "Armored torso",
                new Vector3(0f, -.02f, 0f), new Vector3(.72f, .67f, .38f), armor);
            AddHostilePart(root, PrimitiveType.Sphere, "Combat helmet",
                new Vector3(0f, .63f, 0f), new Vector3(.55f, .52f, .51f), armor);
            AddHostilePart(root, PrimitiveType.Cube, "Red threat visor",
                new Vector3(0f, .65f, .24f), new Vector3(.43f, .13f, .09f),
                new Color(.94f, .15f, .1f));
            AddHostilePart(root, PrimitiveType.Capsule, "Left arm",
                new Vector3(-.44f, -.10f, 0f), new Vector3(.28f, .63f, .28f), fabric);
            AddHostilePart(root, PrimitiveType.Capsule, "Right arm",
                new Vector3(.44f, -.10f, 0f), new Vector3(.28f, .63f, .28f), fabric);
            AddHostilePart(root, PrimitiveType.Capsule, "Left leg",
                new Vector3(-.22f, -.89f, 0f), new Vector3(.3f, .67f, .3f), fabric);
            AddHostilePart(root, PrimitiveType.Capsule, "Right leg",
                new Vector3(.22f, -.89f, 0f), new Vector3(.3f, .67f, .3f), fabric);
            AddHostilePart(root, PrimitiveType.Cube, "Tactical rig",
                new Vector3(0f, -.12f, .22f), new Vector3(.49f, .45f, .13f),
                new Color(.25f, .28f, .27f));
        }

        private static void AddHostilePart(GameObject parent, PrimitiveType shape, string name,
            Vector3 localPosition, Vector3 localScale, Color tint)
        {
            GameObject part = GameObject.CreatePrimitive(shape);
            part.name = name;
            part.transform.SetParent(parent.transform, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider hitCollider = part.GetComponent<Collider>();
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
                Destroy(hitCollider);
            }
            Renderer visual = part.GetComponent<Renderer>();
            if (visual != null)
            {
                // Avoid material instantiation for every trial and target.
                var props = new MaterialPropertyBlock();
                props.SetColor("_BaseColor", tint); // URP
                props.SetColor("_Color", tint);     // Built-in compatibility
                visual.SetPropertyBlock(props);
            }
        }

        private void ClearThreats()
        {
            foreach (Threat t in threats)
                if (t.gameObject != null) Destroy(t.gameObject);
            threats.Clear();
            threatByCollider.Clear();
        }

        private void OnApplicationQuit()
        {
            if (!sessionClosed && logger != null && logger.IsReady)
            {
                if (phase == StudyPhase.Preparation || phase == StudyPhase.Active) EndTrial("application_quit");
                Log("session_end", null, p => { p.event_source = "application"; p.valid = false; p.reason = "interrupted"; });
                sessionClosed = true;
            }
            StopMotorTracking();
            logger?.Dispose();
        }

        private void OnDestroy()
        {
            if (!sessionClosed && logger != null && logger.IsReady)
            {
                Log("session_end", null, p =>
                {
                    p.event_source = "component_lifecycle";
                    p.valid = false;
                    p.reason = "component_destroyed_unfinished_session";
                });
                sessionClosed = true;
            }
            StopMotorTracking();
            output?.Dispose();
            logger?.Dispose();
            if (warningClip != null) Destroy(warningClip);
            if (cursorWasLocked) SetCursor(false);
        }
    }
}

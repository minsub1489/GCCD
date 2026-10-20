using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GCCD.Research
{
    public sealed partial class ResearchController
    {
        [SerializeField] private SpatialMotorProfile motorProfile = SpatialMotorProfile.CreateVirtualPreview();
        private readonly SpatialMotorSmoother motorSmoother = new SpatialMotorSmoother();
        private readonly List<SpatialEmitter> activeEmitters = new List<SpatialEmitter>(3);
        private readonly float[] motorTarget = new float[40];
        private readonly float[] motorPreview = new float[40];
        private double nextMotorTick;
        private double nextMotorLog;
        private double lastMotorTick;
        private double nextPhysicalDispatch;
        private int activeMotorCount;
        private int motorFrameNumber;
        private bool motorTrackingStarted;
        private bool firstMotorFrameLogged;
        private int previewSelectedMotor;
        private float previewAzimuthDegrees = 45f;
        private int previewIntensity = 1;

        private void Reset()
        {
            if (motorProfile == null || motorProfile.motors == null)
                motorProfile = SpatialMotorProfile.CreateVirtualPreview();
        }

        private void OnValidate()
        {
            if (motorProfile == null || motorProfile.motors == null)
                motorProfile = SpatialMotorProfile.CreateVirtualPreview();
        }

        private void InitializeSpatialMotors()
        {
            if (motorProfile == null || motorProfile.motors == null ||
                motorProfile.motors.Length == 0)
                motorProfile = SpatialMotorProfile.CreateVirtualPreview();
            StopMotorTracking();
        }

        private void BeginMotorTracking()
        {
            StopMotorTracking();
            motorTrackingStarted = true;
            nextMotorTick = Now;
            nextMotorLog = Now;
            lastMotorTick = Now;
            nextPhysicalDispatch = Now;
            firstMotorFrameLogged = false;
            motorFrameNumber = 0;
        }

        // Immediate zero on every terminal path (death, missed deadline, trial end,
        // quit, abort, mode none). No async pulse or queued SDK haptics can leak.
        private void StopMotorTracking()
        {
            motorTrackingStarted = false;
            motorSmoother.Reset();
            Array.Clear(motorTarget, 0, motorTarget.Length);
            activeEmitters.Clear();
            activeMotorCount = 0;
            output?.StopAll();
        }

        private void BeginTargetHaptic(Threat target)
        {
            if (CurrentCondition.intensity_level == "none")
            {
                Log("haptic_result", target, p =>
                {
                    p.event_source = "zero_intensity_control";
                    p.output_status = "not_requested";
                    p.strength = 0f;
                    p.delivered = false;
                    p.motor_update_hz = motorUpdateHz;
                });
                return;
            }
            if (!target.spawnValid)
            {
                Log("haptic_result", target, p =>
                {
                    p.event_source = "geometry_safety";
                    p.output_status = "blocked_invalid_spawn";
                    p.valid = false;
                });
                return;
            }
            Log("haptic_request", target, p =>
            {
                p.event_source = "continuous_40_motor_mapping";
                p.output_status = "stream_scheduled";
                p.reason = "source_world_position_listener_yaw_azimuth_tracking_until_target_resolves";
                p.motor_update_hz = motorUpdateHz;
                p.reason = "per_motor_intensities_saved_in_session_motor_profile";
            });
        }

        private void UpdateMotorTracking(bool force)
        {
            if (!motorTrackingStarted || phase != StudyPhase.Active) return;
            double now = Now;
            if (!force && now < nextMotorTick) return;
            nextMotorTick = now + 1.0 / motorUpdateHz; // Skip overdue ticks; never burst queued frames.
            activeEmitters.Clear();
            foreach (Threat threat in threats)
            {
                if (!threat.spawnValid || threat.neutralized || threat.attacked ||
                    now >= threat.deadline || threat.gameObject == null) continue;
                AudioSource sound = threat.gameObject.GetComponent<AudioSource>();
                if (sound == null) continue;
                // Track its registered world sound origin after the brief cue has finished.
                // The logical emitter is active only while this threat is unresolved.
                Vector3 sourceWorld = sound.transform.position;
                activeEmitters.Add(new SpatialEmitter
                {
                    id = threat.id,
                    active = true,
                    worldX = sourceWorld.x,
                    worldZ = sourceWorld.z,
                    gain = 1f
                });
            }
            if (CurrentCondition.intensity_level == "none" || activeEmitters.Count == 0)
            {
                if (activeMotorCount > 0)
                {
                    StopMotorTracking();
                    if (phase == StudyPhase.Active)
                        Log("haptic_stop", null, p =>
                        {
                            p.event_source = "all_active_sources_resolved";
                            p.active_sound_sources = 0;
                            p.output_status = "immediate_all_channel_zero";
                        });
                    // Keep the tracking clock alive for an active trial, not the last signal.
                    motorTrackingStarted = phase == StudyPhase.Active;
                    nextMotorTick = now + 1.0 / motorUpdateHz;
                }
                return; // Zero intensity sends NO motor frames.
            }
            try
            {
                int count = SpatialMotorMath.Mix(
                    motorProfile, activeEmitters, CurrentCondition.intensity_level,
                    transform.position.x, transform.position.z, transform.eulerAngles.y,
                    motorSpreadDegrees, motorOutputCeiling, motorTarget);
                // A departing source must not leave smoothed energy behind it.
                if (count != activeMotorCount) motorSmoother.Reset();
                float dt = (float)Math.Max(0, now - lastMotorTick);
                if (dt <= 0) dt = 1f / motorUpdateHz;
                motorSmoother.Step(motorTarget, dt, motorAttackSeconds, motorReleaseSeconds);
                lastMotorTick = now;
                activeMotorCount = count;
                // SDK2 direct-motor docs recommend >=100ms playback; any later verified
                // physical provider must dispatch at <=10Hz while mapping still runs 20Hz.
                if (output.IsPhysicalHardware && !force && now < nextPhysicalDispatch) return;
                float outboundDuration = output.IsPhysicalHardware
                    ? .1f : 1f / motorUpdateHz;
                if (output.IsPhysicalHardware) nextPhysicalDispatch = now + .1;
                HapticDelivery result = output.SubmitFrame(
                    motorSmoother.Current, outboundDuration, trialId);
                if (!result.accepted ||
                    (mode != StudyMode.Demo && !result.physicallyDelivered))
                {
                    currentTrialValid = false;
                    AbortSession("Motor frame transport failed or physical delivery unverified: " + result.status);
                    return;
                }
                motorFrameNumber++;
                if (!firstMotorFrameLogged)
                {
                    firstMotorFrameLogged = true;
                    foreach (Threat threat in threats)
                    {
                        if (!threat.spawnValid || threat.neutralized || threat.attacked) continue;
                        Log("haptic_result", threat, p =>
                        {
                            p.event_source = result.source;
                            p.output_status = result.status;
                            p.delivered = result.physicallyDelivered;
                            p.motor_update_hz = motorUpdateHz;
                            p.reason = "per_motor_intensities_in_session_profile_and_haptic_update";
                        });
                        if (phase != StudyPhase.Active) return;
                    }
                }
                if (now >= nextMotorLog)
                {
                    nextMotorLog = now + 1.0; // 1Hz aggregate + source traces, independent of 20Hz delivery.
                    float peak = 0f;
                    float sum = 0f;
                    var csv = new StringBuilder(250);
                    float[] levels = motorSmoother.Current;
                    for (int i = 0; i < 40; ++i)
                    {
                        float level = levels[i];
                        peak = Math.Max(peak, level);
                        sum += level;
                        if (i > 0) csv.Append(',');
                        csv.Append(level.ToString("F3", CultureInfo.InvariantCulture));
                    }
                    Log("haptic_update", null, p =>
                    {
                        p.event_source = result.source;
                        p.output_status = result.status;
                        p.active_sound_sources = count;
                        p.motor_frame_number = motorFrameNumber;
                        p.motor_peak = peak;
                        p.motor_mean = sum / 40f;
                        p.motor_levels_csv = csv.ToString();
                        p.motor_update_hz = motorUpdateHz;
                        p.delivered = result.physicallyDelivered;
                    });
                    if (phase != StudyPhase.Active) return;
                    foreach (Threat threat in threats)
                    {
                        if (!threat.spawnValid || threat.attacked || threat.neutralized ||
                            threat.gameObject == null) continue;
                        AudioSource source = threat.gameObject.GetComponent<AudioSource>();
                        if (source == null) continue;
                        float relative = SpatialMotorMath.ListenerRelativeAzimuth(
                            transform.position.x, transform.position.z, transform.eulerAngles.y,
                            source.transform.position.x, source.transform.position.z);
                        Log("haptic_tracking", threat, p =>
                        {
                            p.event_source = "registered_sound_origin_and_listener_yaw";
                            p.tracked_azimuth_degrees = relative;
                            p.camera_yaw = transform.eulerAngles.y;
                            p.world_x = source.transform.position.x;
                            p.world_y = source.transform.position.y;
                            p.world_z = source.transform.position.z;
                            p.active_sound_sources = count;
                            p.motor_frame_number = motorFrameNumber;
                        });
                        if (phase != StudyPhase.Active) return;
                    }
                }
            }
            catch (Exception e)
            {
                currentTrialValid = false;
                AbortSession("360-degree motor mixing failed: " + e.Message);
            }
        }

        private float[] PreviewMotorMap()
        {
            var sample = new List<SpatialEmitter>(1)
            {
                new SpatialEmitter
                {
                    active = true,
                    id = "GUI_PREVIEW_NO_OUTPUT",
                    worldX = Mathf.Sin(previewAzimuthDegrees * Mathf.Deg2Rad),
                    worldZ = Mathf.Cos(previewAzimuthDegrees * Mathf.Deg2Rad),
                    gain = 1f
                }
            };
            SpatialMotorMath.Mix(motorProfile, sample,
                previewIntensity == 0 ? "low" : previewIntensity == 1 ? "medium" : "high",
                0, 0, 0, motorSpreadDegrees, motorOutputCeiling, motorPreview);
            return motorPreview;
        }
    }
}

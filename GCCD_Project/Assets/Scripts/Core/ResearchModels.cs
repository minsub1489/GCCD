using System;

namespace GCCD.Research
{
    public enum StudyMode { Demo, Pilot, Main }
    public enum StudyPhase { Setup, Calibration, PracticeReady, BlockReady, Preparation, Active, TrialReview, Survey, Rest, Finished, Error }
    public enum IntensityLevel { None, Low, Medium, High }
    public enum TorsoZone { FrontLeft, FrontRight, BackLeft, BackRight }

    [Serializable]
    public sealed class ResearchCondition
    {
        public string code;
        public int threat_count;
        public string intensity_level;
    }

    [Serializable]
    public sealed class ResearchCalibration
    {
        public string zone;
        public float low;
        public float medium;
        public float high;
        public bool distinguishable;
        public bool comfortable;
        public bool confirmed;
    }

    [Serializable]
    public sealed class ResearchSnapshot
    {
        public string schema_version = "2.0";
        public string study_title = "동시 위협 수와 진동 강도에 따른 몸통 방향성 햅틱 경고의 효과와 비용";
        public string session_id;
        public string participant_id;
        public string mode;
        public string started_at_utc;
        public string config_hash;
        public string haptic_backend;
        public bool hardware_verified;
        public int sequence_index;
        public int schedule_seed;
        public int trials_per_block;
        public int practice_trials;
        public float preparation_min_seconds;
        public float preparation_max_seconds;
        public float threat_deadline_seconds;
        public float active_duration_seconds;
        public float target_distance;
        public float target_size;
        public float source_angle_jitter_degrees;
        public float damage_per_failure;
        public float mouse_sensitivity;
        public float camera_sample_hz;
        public float rest_seconds;
        public float haptic_pulse_seconds;
        public float visual_warning_seconds;
        public float near_miss_inference_angle_degrees;
        public string haptic_tracking_protocol = "continuous_listener_relative_azimuth_v2";
        public string direction_protocol_change = "Replaces fixed warning-time 4-zone output with continuous source-world-position and listener-yaw tracking.";
        public string audio_source_tracking = "A 0.22-second non-looping audible sound is played at each registered source. The logical sound origin is tracked until the threat is neutralized or deadline expires.";
        public string motor_layout_status = "virtual_unverified_no_physical_output";
        public float motor_update_hz;
        public float motor_spread_degrees;
        public float motor_attack_seconds;
        public float motor_release_seconds;
        public float motor_output_ceiling;
        public int sdk_documented_min_motor_playback_ms = 100;
        public SpatialMotorProfile motor_profile;
        public ResearchCondition[] assigned_order;
        public ResearchCalibration[] calibration;
        public string notes = "Protocol changed from 4 fixed warning-time zones to continuous 360-degree listener-relative tracking. Synthetic angles are NOT verified X40 indices. Mock only. NASA-TLX raw unweighted.";
    }

    [Serializable]
    public sealed class ResearchEvent
    {
        public string event_type;
        public string session_id;
        public string trial_id;
        public string participant_id;
        public string condition;
        public int threat_count;
        public string intensity_level;
        public double trial_time;
        public double session_time_monotonic;
        public long sequence_number;
        public string target_id;
        public string timestamp_utc;
        public ResearchEventPayload payload;
    }

    // Explicit typed payload keeps JSONL machine-readable with Unity JsonUtility,
    // which cannot serialize anonymous/dictionary payloads.
    [Serializable]
    public sealed class ResearchEventPayload
    {
        public string event_source = "";
        public string mode = "";
        public string zone = "";
        public string phase = "";
        public string reason = "";
        public string validity = "";
        public string first_shot_target = "";
        public string hit_target_id = "";
        public string inferred_target_id = "";
        public string attribution_method = "";
        public string output_status = "";
        public string output_mode = "";
        public string motor_profile_id = "";
        public string motor_levels_csv = "";
        public string survey_json = "";
        public int block_index = -1;
        public int trial_index = -1;
        public int schedule_seed;
        public int planned_threats;
        public int actual_threats;
        public int shot_index;
        public int response_attempts;
        public int failed_count;
        public int hit_count;
        public int turn_reversals;
        public int active_sound_sources;
        public int motor_frame_number;
        public bool practice;
        public bool attempted;
        public bool acquired;
        public bool neutralized;
        public bool deadline_failed;
        public bool delivered;
        public bool device_verified;
        public bool valid = true;
        public float strength;
        public float damage;
        public float hp_remaining;
        public float yaw_path_deg;
        public float yaw_delta_deg;
        public float input_delta_x;
        public float input_delta_y;
        public float shot_ray_x;
        public float shot_ray_y;
        public float shot_ray_z;
        public float aim_error_angle_deg;
        public float motor_peak;
        public float motor_mean;
        public float tracked_azimuth_degrees;
        public float motor_update_hz;
        public float camera_yaw;
        public float camera_pitch;
        public float spawn_angle_deg;
        public float world_x;
        public float world_y;
        public float world_z;
        public double deadline_time;
        public double first_acquisition_time;
        public double neutralization_time;
        public double warning_duration;
    }

    [Serializable]
    public sealed class ResearchSurvey
    {
        public string session_id;
        public string participant_id;
        public string condition;
        public int block_index;
        public int mental_demand;
        public int physical_demand;
        public int temporal_demand;
        public int performance_poor;
        public int effort;
        public int frustration;
        public int directional_difficulty;
        public int selection_difficulty;
        public int discomfort;
        public string completed_at_utc;
        public string survey_version = "NASA-TLX raw 6 dimensions + 3 separate study questions (0-100, step 5)";
    }
}

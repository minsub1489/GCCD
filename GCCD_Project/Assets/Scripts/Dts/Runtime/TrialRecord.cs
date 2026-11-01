using System;
namespace GCCD.Dts
{
    [Serializable] public sealed class ThreatRecord
    {
        public string threat_id,suppression_parent_id,reason,comparisons;
        public bool is_active,is_offscreen,is_firing,top3_selected,retained;
        public double world_x,world_y,world_z,distance_m,bearing_deg;
        public int distance_rank;public int[] individual_motor_array;
    }
    [Serializable] public sealed class ResponseRecord {public int marker_id;public double response_bearing,response_time;public string response_distance_bin;}
    [Serializable] public sealed class ActionRecord {public double event_seconds,camera_heading;public string target_id;public bool hit,front_task,first_external;}
    [Serializable] public sealed class TrialRecord
    {
        public string participant_id,session_id,experiment_type,mode,trial_id,utc,event_utc,condition_order,output_status,error="",config_hash;
        public int condition_theta,block_index,scene_family,random_seed,output_duration,confusion_rating=-1;
        public double event_time,camera_heading,response_open,response_time,actual_pulse_wait_seconds;
        public bool timeout,technical_failure,interrupted,is_training;
        public int selected_count,retained_count;public int[] requested_motor_array;
        public ThreatRecord[] threats;public ResponseRecord[] responses=new ResponseRecord[0];public OutputReceipt receipt;
        public double joint_accuracy=-1,direction_accuracy=-1,distance_accuracy=-1,direction_error=-1,response_precision=-1,false_response_rate=-1,no_response_rate=-1;
        public bool ambiguous_matching;public string[] matched_threat_ids;public int[] matched_threat_indices,distance_confusion;
        public bool game_success,game_timeout;public string game_first_action_target="";
        public double game_first_response_seconds=-1,camera_rotation_path;public int front_shots,front_hits,additional_threats_hit;
        public ActionRecord[] game_actions=new ActionRecord[0];
    }
    [Serializable] public sealed class SessionMetadata
    {
        public string participant_id,session_id,experiment_type,mode,created_utc,unity_version,sdk_version,config_hash,mapping_version;
        public string software_version="dts-0.1",timing="Unity monotonic realtimeSinceStartupAsDouble; UTC timestamps stored separately";
        public string physical_verification="Software request acceptance is not skin-level confirmation.";
        public ExperimentConfig config;public int[] block_order;public bool training_passed;
    }
}

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
namespace GCCD.Dts
{
    public static class DataExporter
    {
        static void Write(string folder,string name,string header,IEnumerable<string> rows){SessionStore.AtomicWrite(Path.Combine(folder,name),header+"\n"+string.Join("\n",rows)+"\n");}
        [Serializable] sealed class LegacyThreat { public bool top3_selected; }
        [Serializable] sealed class LegacyTrial { public LegacyThreat[] threats; }

        public static TrialRecord[] Records(string folder)
        {
            return Directory.GetFiles(Path.Combine(folder, "journal"), "*.committed.json")
                .OrderBy(f => f, StringComparer.Ordinal).Select(f => ReadRecord(SessionStore.ReadCommitted(f))).ToArray();
        }

        static TrialRecord ReadRecord(string json)
        {
            var record = JsonUtility.FromJson<TrialRecord>(json);
            if (record.total_threat_count == 0 && record.threats != null)
            {
                var legacy = JsonUtility.FromJson<LegacyTrial>(json);
                for (int i = 0; i < record.threats.Length; i++)
                    record.threats[i].top3_selected = legacy.threats[i].top3_selected;
            }
            return record;
        }
        static object Nullable(double x){return x<0?null:(object)x;}
        public static string[] Validate(TrialRecord t)
        {
            var e=new List<string>();
            bool fullCircle = t.protocol_version == SessionMetadata.CurrentSoftwareVersion || t.protocol_version == "dts-full-circle-v3";
            bool fixedCount = t.total_threat_count > 0;
            int originalCount = t.threats?.Length ?? 0;
            int expected = fixedCount ? t.total_threat_count : t.is_training ? originalCount : t.experiment_type == "A" ? 3 : 5;
            int selectedLimit = fixedCount ? expected : Math.Min(3, expected);
            bool Selected(ThreatRecord threat) => fixedCount ? threat.selected : threat.top3_selected;
            if (originalCount != expected || originalCount == 0 ||
                (fixedCount && !t.is_training && expected < 2))
                e.Add("Original threat count.");
            if (t.threats != null)
            {
                if (!fixedCount && !t.is_training && t.experiment_type == "B" && t.threats.Count(x => x.is_active) != 5)
                    e.Add("B requires five active original threats.");
                if (t.threats.Count(Selected) != t.selected_count || t.threats.Count(x => x.retained) != t.retained_count)
                    e.Add("Recorded decisions/count mismatch.");
                if (t.threats.Count(x => Selected(x) && !x.retained) != t.selected_count - t.retained_count)
                    e.Add("Suppression count.");
            }
            if (fullCircle && (t.suppressed_count != expected - t.retained_count ||
                t.threats == null || t.threats.Count(x => x.suppressed) != t.suppressed_count ||
                t.threats.Any(x => x.retained == x.suppressed)))
                e.Add("Full-circle partition mismatch.");
            if(double.IsNaN(t.joint_accuracy)||double.IsInfinity(t.joint_accuracy))e.Add("Nonfinite score.");
            if(!t.technical_failure&&(t.requested_motor_array==null||t.requested_motor_array.Length!=40||t.requested_motor_array.Any(x=>x<0||x>100)))e.Add("Invalid submitted motor frame.");
            if(!t.technical_failure&&t.receipt!=null&&((t.receipt.simulated&&t.mode!="DEVELOPMENT"&&t.mode!="TRAINING_DEVELOPMENT")||t.output_status!=t.receipt.status))e.Add("Output status/provenance mismatch.");
            if(t.selected_count>selectedLimit||t.retained_count>t.selected_count||t.retained_count<0)e.Add("Selection/retention count.");
            if(!t.technical_failure&&(t.receipt==null||!t.receipt.submitted||t.selected_count!=selectedLimit||t.threats==null||(!fullCircle && t.threats.Any(x=>!x.is_offscreen))))e.Add("Normal trial without eligible output/snapshot.");
            if(t.responses.Length>selectedLimit||t.joint_accuracy>1||(!t.technical_failure&&t.experiment_type=="A"&&t.joint_accuracy<0))e.Add("Response/score.");
            return e.ToArray();
        }
        public static void Export(string folder)
        {
            var all=Records(folder);
            string eventLog = Path.Combine(folder,"runtime_events.log");
            if(File.Exists(eventLog)) SessionStore.AtomicWrite(Path.Combine(folder,"runtime_events.csv"),File.ReadAllText(eventLog));
            Write(folder,"trials.csv","participant_id,session_id,experiment_type,mode,trial_id,condition_theta,block_index,block_order,scene_family,random_seed,event_time,event_utc,camera_heading,total_threat_count,selected_count,retained_count,suppressed_count,retention_rate,suppression_rate,response_count,response_time,timeout,technical_failure,joint_accuracy,direction_accuracy,distance_accuracy,direction_error,response_precision,false_response_rate,no_response_rate,ambiguous_matching,retained_recognition_rate,confusion_rating,game_first_action_target,game_success,game_timeout,game_first_response_seconds,camera_rotation_path,front_shots,front_hits,additional_threats_hit,config_hash,error,minimum_threat_count,maximum_threat_count,direction_tolerance_deg,joint_correct_count,far_false_mark_count,adjusted_joint_correct_count,adjusted_joint_accuracy",
                all.Select(t=>CsvTable.Line(t.participant_id,t.session_id,t.experiment_type,t.mode,t.trial_id,t.condition_theta,t.block_index,t.condition_order,t.scene_family,t.random_seed,t.event_time,t.event_utc,t.camera_heading,t.threats?.Length??0,t.selected_count,t.retained_count,t.selected_count-t.retained_count,t.selected_count==0?(object)null:t.retained_count/(double)t.selected_count,t.selected_count==0?(object)null:(t.selected_count-t.retained_count)/(double)t.selected_count,t.responses.Length,t.response_time,t.timeout,t.technical_failure,Nullable(t.joint_accuracy),Nullable(t.direction_accuracy),Nullable(t.distance_accuracy),Nullable(t.direction_error),Nullable(t.response_precision),Nullable(t.false_response_rate),Nullable(t.no_response_rate),t.ambiguous_matching,t.protocol_version==SessionMetadata.CurrentSoftwareVersion?Nullable(t.retained_recognition_rate):null,t.confusion_rating<1?(object)null:t.confusion_rating,t.game_first_action_target,t.game_success,t.game_timeout,Nullable(t.game_first_response_seconds),t.camera_rotation_path,t.front_shots,t.front_hits,t.additional_threats_hit,t.config_hash,t.error,t.minimum_threat_count>0?(object)t.minimum_threat_count:null,t.maximum_threat_count>0?(object)t.maximum_threat_count:null,t.minimum_threat_count>0?(object)t.direction_tolerance_deg:null,t.adjusted_joint_accuracy>=0?(object)t.joint_correct_count:null,t.adjusted_joint_accuracy>=0?(object)t.far_false_mark_count:null,t.adjusted_joint_accuracy>=0?(object)t.adjusted_joint_correct_count:null,Nullable(t.adjusted_joint_accuracy))));
            Write(folder,"threats.csv","trial_id,threat_id,is_active,is_offscreen,is_firing,world_x,world_y,world_z,distance_m,bearing_deg,distance_rank,selected,retained,suppressed,joint_correct,suppression_parent_id,suppression_reason,comparisons,individual_motor_array",
                all.SelectMany(t=>(t.threats??new ThreatRecord[0]).Select(x=>CsvTable.Line(t.trial_id,x.threat_id,x.is_active,x.is_offscreen,x.is_firing,x.world_x,x.world_y,x.world_z,x.distance_m,x.bearing_deg,x.distance_rank,t.total_threat_count>0?x.selected:x.top3_selected,x.retained,(t.total_threat_count>0?x.selected:x.top3_selected)&&!x.retained,(t.protocol_version==SessionMetadata.CurrentSoftwareVersion||t.protocol_version=="dts-full-circle-v3")&&t.joint_accuracy>=0?(object)x.joint_correct:null,x.suppression_parent_id,x.reason,x.comparisons,x.individual_motor_array==null?"":string.Join(";",x.individual_motor_array)))));
            Write(folder,"haptic_outputs.csv","trial_id,requested_motor_array,output_duration,haptic_output_status,request_id,simulated,submit_started,submit_returned,actual_pulse_wait_seconds,error",
                all.Select(t=>CsvTable.Line(t.trial_id,t.requested_motor_array==null?"":string.Join(";",t.requested_motor_array),t.output_duration,t.output_status,t.receipt==null?-1:t.receipt.request_id,t.receipt==null?(object)null:t.receipt.simulated,t.receipt==null?(object)null:t.receipt.submit_started,t.receipt==null?(object)null:t.receipt.submit_returned,t.actual_pulse_wait_seconds,t.receipt==null?t.error:t.receipt.error)));
            Write(folder,"responses.csv","trial_id,marker_id,response_bearing,response_distance_bin,response_time,matched_threat_id,matched_distance_sorted_index,joint_correct,matched_angle_error_deg,nearest_angle_error_deg,far_false_mark",
                all.SelectMany(t=>t.responses.Select((r,i)=>CsvTable.Line(t.trial_id,r.marker_id,r.response_bearing,r.response_distance_bin,r.response_time,t.matched_threat_ids!=null&&i<t.matched_threat_ids.Length?t.matched_threat_ids[i]:"",t.matched_threat_indices!=null&&i<t.matched_threat_indices.Length?(object)t.matched_threat_indices[i]:null,(t.protocol_version==SessionMetadata.CurrentSoftwareVersion||t.protocol_version=="dts-full-circle-v3")&&t.joint_accuracy>=0?(object)r.joint_correct:null,t.adjusted_joint_accuracy>=0?(object)r.matched_angle_error_deg:null,t.adjusted_joint_accuracy>=0?(object)r.nearest_angle_error_deg:null,t.adjusted_joint_accuracy>=0?(object)r.far_false_mark:null))));
            Write(folder,"game_actions.csv","trial_id,event_seconds,camera_heading,target_id,hit,front_task,first_external",
                all.SelectMany(t=>t.game_actions.Select(a=>CsvTable.Line(t.trial_id,a.event_seconds,a.camera_heading,a.target_id,a.hit,a.front_task,a.first_external))));
            Write(folder,"distance_confusion.csv","trial_id,actual_distance_bin,response_distance_bin,count",
                all.Where(t=>t.distance_confusion!=null&&t.distance_confusion.Length==9).SelectMany(t=>Enumerable.Range(0,9).Select(i=>CsvTable.Line(t.trial_id,((DistanceBin)(i/3)).ToString(),((DistanceBin)(i%3)).ToString(),t.distance_confusion[i]))));
            Write(folder,"data_validation.csv","trial_id,error",all.SelectMany(t=>Validate(t).Select(err=>CsvTable.Line(t.trial_id,err))));
        }
    }
}

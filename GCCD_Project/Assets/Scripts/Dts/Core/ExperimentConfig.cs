using System;
using System.Collections.Generic;
using System.Linq;
namespace GCCD.Dts
{
    [Serializable] public sealed class ExperimentConfig
    {
        public const int MaximumThreatCount = 8;
        public const int DevelopmentThreatCount = 6;
        public string Version="dts-fixed-count-v2";
        public int TotalThreatCount;
        public string LobbyMode = "DEVELOPMENT";

        public int ThreatCountFor(bool development)
        {
            if (development && TotalThreatCount == 0) return DevelopmentThreatCount;
            if (TotalThreatCount < 2 || TotalThreatCount > MaximumThreatCount)
                throw new ArgumentException("시행당 전체 위협 수를 2~8개로 설정하세요. 0은 미확정입니다.");
            return TotalThreatCount;
        }
        public int Seed=1102,CounterbalanceRow=0,PulseMilliseconds=500;
        public double HorizontalFov=90,Low=.15,High=.75;
        public double DirectionTolerance=-1;
        public int FinalSampleSize=-1;
        public string SampleSizeRationale="",MinimumEffect="",CandidateTieRule="",ExclusionRule="",StatisticalModel="";
        public string TrainingCriterion="",TrainingExitRule="";public int TrainingMaximumRetries=-1,TrainingTripleTrials=3;
        public double TrainingMinimumJointAccuracy=-1;public int TrainingMinimumTrials=-1;
        public double PilotMinimumDirectionAccuracy=-1,PilotMinimumDistanceAccuracy=-1,PilotMaximumIdenticalPairRate=-1;
        public string PilotCriteriaRegisteredAt="",PilotEvidencePath="",PilotEvidenceSha256="";
        public bool PilotCriteriaPassed,DurationConfirmed,IntensityConfirmed,BodyCameraAlignmentConfirmed;
        public bool Raw40TransportConfirmed,PhysicalConcurrencyConfirmed,AllowRaw40CalibrationProbe;
        public string TransportEvidence="",VerifiedDeviceName="",SdkPackageVersion="unverified-local-cache";
        public string LockedHash="";
        public MotorProfile Mapping=X40Layout.Profile();
        public string[] Problems(bool main,bool physical)
        {
            var errors=new List<string>();
            try{ThreatCountFor(!main&&!physical);ConditionScheduler.Order(CounterbalanceRow);Angles.IsOffscreen(180,HorizontalFov);DistanceEncoding.Intensity(6,Low,High);Mapping.Validate(physical);}catch(ArgumentException e){errors.Add(e.Message);}
            if(PulseMilliseconds<100||PulseMilliseconds>5000)errors.Add("Pulse must be 100..5000 ms.");
            if(DirectionTolerance<0||DirectionTolerance>180||double.IsNaN(DirectionTolerance)||double.IsInfinity(DirectionTolerance))errors.Add("Set and freeze direction tolerance from pilot results.");
            if(physical&&(!Raw40TransportConfirmed||!PhysicalConcurrencyConfirmed||string.IsNullOrWhiteSpace(TransportEvidence)||string.IsNullOrWhiteSpace(VerifiedDeviceName)||!DurationConfirmed||!IntensityConfirmed||!BodyCameraAlignmentConfirmed))errors.Add("Physical X40 transport, mapping, concurrency, alignment and comfortable intensity/duration must be verified.");
            if(main)
            {
                if(FinalSampleSize<1)errors.Add("Set final sample size with rationale after pilot/power planning.");
                if(string.IsNullOrWhiteSpace(Version)||!PilotCriteriaPassed||string.IsNullOrWhiteSpace(PilotCriteriaRegisteredAt)||string.IsNullOrWhiteSpace(PilotEvidencePath)||PilotEvidenceSha256.Length!=64)errors.Add("Preregistered pilot criteria and a hashed evidence file required.");
                if(new[]{PilotMinimumDirectionAccuracy,PilotMinimumDistanceAccuracy,PilotMaximumIdenticalPairRate,TrainingMinimumJointAccuracy}.Any(x=>double.IsNaN(x)||double.IsInfinity(x)||x<0||x>1)||TrainingMinimumTrials<1||TrainingMinimumTrials>TrainingTripleTrials)errors.Add("Set pilot thresholds and quantitative training criterion.");
                if(new[]{SampleSizeRationale,MinimumEffect,CandidateTieRule,ExclusionRule,StatisticalModel,TrainingCriterion,TrainingExitRule}.Any(string.IsNullOrWhiteSpace)||TrainingMaximumRetries<0)errors.Add("Complete preregistration, training, analysis and exclusion settings.");
                if(string.IsNullOrWhiteSpace(LockedHash))errors.Add("Freeze configuration before MAIN.");
            }
            return errors.ToArray();
        }
    }
}

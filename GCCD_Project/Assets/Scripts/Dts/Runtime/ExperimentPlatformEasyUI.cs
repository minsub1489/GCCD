using System;
using System.IO;
using UnityEngine;
namespace GCCD.Dts
{
    public sealed partial class ExperimentPlatform
    {
        bool advanced,connectionReady,autoLoaded,connectionStarted;
        bool feltPulse,pulseSubmitted; string connectionText="Player에서 조끼를 찾고 있습니다…",testText="짧은 진동을 보내 연결을 확인하세요.";
        double nextConnectionPoll; Vector2 easyScroll;
        GUIStyle cardStyle,headingStyle,subtleStyle,actionStyle;
        Texture2D cardBackground,buttonBackground,hoverBackground;
        public bool ConnectionReady {get{return connectionReady;}}
        public string ConnectionText {get{return connectionText;}}
        void UpdateEasyConnection()
        {
            if(!Application.isPlaying||Clock!=null||Phase!=TrialPhase.Setup)return;
            if(!autoLoaded){autoLoaded=true;if(File.Exists(ConfigPath))try{LoadConfig();}catch(Exception){notice="저장된 설정을 읽지 못했습니다. 고급 설정에서 확인하세요.";}}
            if(Time.realtimeSinceStartupAsDouble<nextConnectionPoll)return;
            nextConnectionPoll=Time.realtimeSinceStartupAsDouble+2;
            ConnectEasy();
        }
        public void ConnectEasy()
        {
            string reason;connectionReady=haptics.AutoConnect(config,out reason);connectionStarted=true;
            connectionText=connectionReady?"조끼 연결됨 · "+(haptics.Transport=="local-player-v1"?"Player 자동 연결 · 로그인 불필요":config.VerifiedDeviceName):FriendlyConnection(reason);
        }
        string FriendlyConnection(string reason)
        {
            if(reason.Contains("Link/deploy"))return "앱 연결 정보가 없습니다. 이 PC의 Unity에서 연결하거나, 연결 정보가 포함된 실행 패키지를 사용하세요.";
            if(reason.Contains("initializer"))return "SDK 초기화가 필요합니다. Unity의 GCCD → bHaptics SDK2 메뉴에서 한 번 설정하세요.";
            if(reason.Contains("Player unavailable"))return "bHaptics Player를 열어 주세요. 열려 있다면 조끼 연결을 확인하세요.";
            if(haptics.ConnectedVestNames.Length>1)return "조끼가 여러 개 연결되어 있습니다. 아래에서 사용할 조끼를 선택하세요.";
            if(reason.Contains("does not match"))return "연결된 조끼를 기다리고 있습니다. Player에서 조끼가 연결되어 있는지 확인하세요.";
            return "연결 확인 필요 · "+reason;
        }
        public OutputReceipt TestConnectionPulse(){return TestDirectionPulse(0);}
        Texture2D Solid(Color color){var t=new Texture2D(1,1);t.SetPixel(0,0,color);t.Apply();return t;}
        void ApplyComfortableSkin()
        {
            if(cardStyle==null)
            {
                cardBackground=Solid(new Color(.09f,.12f,.17f));buttonBackground=Solid(new Color(.13f,.35f,.45f));hoverBackground=Solid(new Color(.18f,.46f,.56f));
                cardStyle=new GUIStyle(GUI.skin.box){padding=new RectOffset(24,24,20,20),margin=new RectOffset(0,0,8,12)};cardStyle.normal.background=cardBackground;
                headingStyle=new GUIStyle(titleStyle){fontSize=30};subtleStyle=new GUIStyle(bodyStyle){fontSize=17};subtleStyle.normal.textColor=new Color(.68f,.76f,.82f);
                actionStyle=new GUIStyle(GUI.skin.button){fontSize=19,padding=new RectOffset(18,18,14,14),margin=new RectOffset(0,12,6,6)};
                actionStyle.normal.background=buttonBackground;actionStyle.hover.background=hoverBackground;actionStyle.active.background=hoverBackground;actionStyle.normal.textColor=Color.white;
            }
            GUI.skin.font=appFont;GUI.skin.label.fontSize=18;GUI.skin.button.fontSize=18;GUI.skin.textField.fontSize=18;GUI.skin.textField.fixedHeight=34;GUI.skin.toggle.fontSize=17;
        }
        bool ActionButton(string label){return GUILayout.Button(label,actionStyle,GUILayout.MinHeight(48));}
        void DrawSetup()
        {
            if(advanced){DrawAdvancedSetup();GUILayout.BeginArea(new Rect(Screen.width-195,24,165,40));if(GUILayout.Button("← 간단 화면",GUILayout.Height(36)))advanced=false;GUILayout.EndArea();return;}
            float width=Math.Min(1020,Screen.width-48);GUILayout.BeginArea(new Rect((Screen.width-width)/2,28,width,Screen.height-56));
            easyScroll=GUILayout.BeginScrollView(easyScroll);
            GUILayout.Label("조끼를 연결하고, 바로 시작하세요",headingStyle);GUILayout.Label("GCCD  /  방향·거리 인지 실험",subtleStyle);GUILayout.Space(16);
            GUILayout.BeginVertical(cardStyle);GUILayout.Label("01   조끼 연결",titleStyle);
            Color previous=GUI.color;GUI.color=connectionReady?new Color(.5f,1,.78f):Color.white;GUILayout.Label(connectionText,bodyStyle);GUI.color=previous;
            if(!connectionStarted)GUILayout.Label("Player를 켜 두면 자동으로 연결합니다.",subtleStyle);
            if(haptics.ConnectedVestNames.Length>1)foreach(string name in haptics.ConnectedVestNames)if(GUILayout.Button(name,GUILayout.Height(38))){config.VerifiedDeviceName=name;ConnectEasy();}
            if(!connectionReady&&ActionButton("연결 다시 확인"))Guard(ConnectEasy);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(cardStyle);GUILayout.Label("02   진동 확인",titleStyle);GUILayout.Label(testText,bodyStyle);
            GUILayout.BeginHorizontal();GUI.enabled=connectionReady;if(ActionButton("짧게 진동 보내기"))Guard(()=>TestConnectionPulse());GUI.enabled=pulseSubmitted;
            if(ActionButton("진동이 느껴졌어요")){feltPulse=true;testText="연결 테스트 확인됨. 방향별 교정은 고급 설정에서 진행할 수 있습니다.";}
            GUI.enabled=true;GUILayout.EndHorizontal();GUILayout.Label("약한 강도로 모터 한 개에 0.2초간 요청합니다.",subtleStyle);GUILayout.EndVertical();
            GUILayout.BeginVertical(cardStyle);GUILayout.Label("03   FPS 미션 / 실험",titleStyle);
            if(ActionButton("FPS 미션 시작 · 이동 / 사격 / 보급품 수집"))StartMission();
            GUILayout.Label(connectionReady?"Player 연결됨: 미션 중 위협 발생 시 진동을 요청합니다. 교정 전 체험 모드입니다.":"조끼 없이도 플레이할 수 있습니다. 진동은 Player 연결 후 사용할 수 있습니다.",subtleStyle);
            GUILayout.BeginHorizontal();GUILayout.Label("참가자 ID",GUILayout.Width(130));Participant=GUILayout.TextField(Participant,32);GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();if(ActionButton("응답 방법 연습")){Mode="DEVELOPMENT";Experiment="A";StartSession(true);}if(ActionButton(Mode=="DEVELOPMENT"?"화면으로 실험 A 연습":"실험 A 시작")){Experiment="A";StartSession();}GUILayout.EndHorizontal();
            GUILayout.Label(Mode=="DEVELOPMENT"?"현재는 화면 연습 모드입니다. 실제 진동 실험은 고급 설정에서 검증된 프로필을 선택하세요.":"현재 자료 구분: "+Mode,subtleStyle);
            if(feltPulse)GUILayout.Label("진동 체감 확인은 연결 점검 기록이며, 실험 교정 완료를 뜻하지 않습니다.",subtleStyle);
            if(!string.IsNullOrEmpty(Error))GUILayout.Label("시작 전 확인: "+Error,bodyStyle);GUILayout.EndVertical();
            GUILayout.BeginHorizontal();if(GUILayout.Button("고급 설정 · 교정 / 실험 B / 데이터",GUILayout.Height(40)))advanced=true;
            if(GUILayout.Button("진동 중지",GUILayout.Width(140),GUILayout.Height(40))){notice=haptics.Stop();testText="진동 중지 요청을 보냈습니다.";}GUILayout.EndHorizontal();
            if(!string.IsNullOrEmpty(notice))GUILayout.Label(notice,subtleStyle);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
    }
}

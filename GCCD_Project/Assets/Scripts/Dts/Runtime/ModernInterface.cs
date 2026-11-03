using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    [DisallowMultipleComponent]
    public sealed partial class ModernInterface:MonoBehaviour
    {
        UIDocument document;PanelSettings panel;ExperimentPlatform platform;VisualElement root,lobby,hud,pause,background,reticle,marker,ammoProgress;
        Label device,connectionHelp,pulse,objectives,ammo,status,pauseTitle,hapticBadge,lobbyError,threatNotice;readonly Label[] supplyMarkers=new Label[3];string pulseError="";Texture2D scrimTexture;Button testButton,confirmButton,continueButton,experimentButton;Font font;
        string rangeError = "";
        public bool Showing {get;private set;}
        public bool HasLobby {get{return lobby!=null;}}
        System.Collections.IEnumerator Start()
        {
            bool preview=System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--mission-preview")>=0;
            if(preview||System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"--mission")>=0){yield return new WaitForSecondsRealtime(2);platform.ConnectEasy();platform.StartMission();if(preview)GetComponent<MissionDemo>().Physical=false;}
        }
        void Awake()
        {
            platform=GetComponent<ExperimentPlatform>();
            panel=ScriptableObject.CreateInstance<PanelSettings>();panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1600,900);panel.match=.5f;panel.themeStyleSheet=Resources.Load<ThemeStyleSheet>("Realism/UI/DefaultTheme");
            document=gameObject.AddComponent<UIDocument>();document.panelSettings=panel;document.sortingOrder=100;
            root=document.rootVisualElement;root.name="operator-interface";root.AddToClassList("screen");root.pickingMode=PickingMode.Ignore;
            var style=Resources.Load<StyleSheet>("Realism/UI/Operator");if(style)root.styleSheets.Add(style);
            string family=System.Array.Find(new[]{"Malgun Gothic","Apple SD Gothic Neo","Noto Sans CJK KR","Arial"},x=>System.Array.IndexOf(Font.GetOSInstalledFontNames(),x)>=0);
            font=family==null?Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"):Font.CreateDynamicFontFromOSFont(family,22);root.style.unityFontDefinition=FontDefinition.FromFont(font);
            BuildLobby();BuildHud();BuildMaps();BuildResults();BuildDirectionTest();
        }
        VisualElement Element(VisualElement parent,string classes){var e=new VisualElement();foreach(var c in classes.Split(' '))e.AddToClassList(c);parent.Add(e);return e;}
        Label Label(VisualElement parent,string value,string classes){var l=new Label(value);foreach(var c in classes.Split(' '))l.AddToClassList(c);parent.Add(l);return l;}
        Button Button(VisualElement parent,string value,System.Action action,string classes){var b=new Button(action){text=value};foreach(var c in classes.Split(' '))b.AddToClassList(c);parent.Add(b);return b;}
        void BuildLobby()
        {
            lobby=Element(root,"lobby");background=Element(lobby,"backdrop");var image=Resources.Load<Texture2D>("Realism/UI/lobby-background");if(image)background.style.backgroundImage=image;
            var scrim=Element(lobby,"scrim");scrimTexture=new Texture2D(256,1,TextureFormat.RGBA32,false);for(int x=0;x<256;x++){float f=x/255f;scrimTexture.SetPixel(x,0,new Color(.025f,.04f,.05f,Mathf.Lerp(.91f,.10f,Mathf.SmoothStep(0,1,f))));}scrimTexture.Apply();scrim.style.backgroundImage=scrimTexture;
var top=Element(lobby,"lobby-top");Label(top,"OUTPOST  /  07","brand");Label(top,"GCCD  ·  HAPTIC FIELD LAB","eyebrow");
            var content=Element(lobby,"lobby-content");Label(content,"RECOVERY OPERATION","eyebrow accent");Label(content,"공업지대\n수색 작전","hero");Label(content,"보급품을 회수하고 경계 드론을 무력화하세요.\n시야 밖 위협은 조끼의 진동으로 전달됩니다.","description");
            Button(content,"작전 시작    →",()=>platform.StartMission(),"button primary");
            var angles = new DropdownField("억제각", new System.Collections.Generic.List<string>{"전체 각도","0°","15°","30°","45°","60°","90°"}, 0);
            angles.name="studyAngle";
            angles.AddToClassList("small");
            content.Add(angles);
            var rangeRow = Element(content, "row");
            var minimumField = new TextField("최소 위협 수") { name = "studyThreatMinimum", value = "3" };
            var maximumField = new TextField("최대 위협 수") { name = "studyThreatMaximum", value = "8" };
            foreach (var input in new[] { minimumField, maximumField })
            {
                input.style.flexGrow = 1;
                input.AddToClassList("small");
                rangeRow.Add(input);
                input.RegisterValueChangedCallback(_ => rangeError = "");
            }
            var secondary = Element(content, "row");
            Button(secondary, "응답 연습", () => { rangeError = ""; platform.StartLobbyExperiment(true); }, "button secondary");
            experimentButton = Button(secondary, "실험 시작 · 화면 연습", () =>
            {
                if (!int.TryParse(minimumField.value, out int minimum) ||
                    !int.TryParse(maximumField.value, out int maximum) ||
                    minimum < 2 || maximum < minimum)
                {
                    rangeError = "2 이상의 정수를 입력하고, 최대 위협 수를 최소 이상으로 설정하세요.";
                    return;
                }
                rangeError = "";
                int theta = angles.index == 0 ? -1 : ThreatPolicy.Conditions[angles.index - 1];
                platform.StartLobbyExperiment(false, theta, minimum, maximum);
            }, "button secondary");
            experimentButton.name = "startExperiment";
            var field=new TextField("참가자 ID"){value=platform.Participant};field.AddToClassList("participant");field.RegisterValueChangedCallback(e=>platform.Participant=e.newValue);content.Add(field);lobbyError=Label(content,"","small accent");
            var bottom=Element(lobby,"lobby-bottom");Label(bottom,"WASD 이동  ·  우클릭 조준  ·  좌클릭 발사  ·  SPACE 점프  ·  C 앉기  ·  TAB 지도","control-hint");
            var card=Element(lobby,"device-panel");Label(card,"HAPTIC CONNECTION","eyebrow");device=Label(card,"조끼를 찾고 있습니다…","device-status");connectionHelp=Label(card,"Player를 켜 두면 자동으로 연결합니다.","small muted");
            testButton=Button(card,"진동 · 방향/거리 확인",()=>OpenDirectionTest(),"button outline");testButton.name="openDirectionCheck";
            pulse=Label(card,"","small muted");confirmButton=Button(card,"진동이 느껴졌어요",()=>platform.ConfirmPulse(),"text-button");
            Button(card,"출력 중지",()=>platform.StopMissionHaptics(),"text-button muted");
        }
        void BuildHud()
        {
            hud=Element(root,"hud");hud.pickingMode=PickingMode.Ignore;
            var objective=Element(hud,"objectives");Label(objective,"OUTPOST 07","eyebrow accent");Label(objective,"보급품 회수","objective-title");objectives=Label(objective,"","objective-detail");
            hapticBadge=Label(hud,"","haptic-badge");threatNotice=Label(hud,"","threat-notice");
            for(int i=0;i<3;i++)supplyMarkers[i]=Label(hud,"","supply-marker");
            reticle=Element(hud,"reticle");Element(reticle,"cross-top");Element(reticle,"cross-bottom");Element(reticle,"cross-left");Element(reticle,"cross-right");marker=Label(hud,"×","hit-marker");
            var weapon=Element(hud,"weapon-hud");Label(weapon,"AKM  /  7.62","eyebrow");ammo=Label(weapon,"30","ammo");ammoProgress=Element(weapon,"reload-bar");status=Label(weapon,"R  재장전","small muted");
            Label(hud,"WASD 이동  ·  SPACE 점프  ·  C 앉기  ·  TAB 지도  ·  E 회수  ·  ESC 메뉴","hud-controls");
            pause=Element(hud,"pause-overlay");var panel=Element(pause,"pause-content");Label(panel,"OPERATION CONTROL","eyebrow accent");pauseTitle=Label(panel,"일시정지","pause-title");continueButton=Button(panel,"작전 계속",()=>GetComponent<MissionDemo>().SetPaused(false),"button primary");Button(panel,"로비로 돌아가기",()=>GetComponent<MissionDemo>().Exit(),"button secondary");
            foreach(var e in hud.Query<VisualElement>().ToList())e.pickingMode=PickingMode.Ignore;
            pause.pickingMode=PickingMode.Position;foreach(var b in pause.Query<Button>().ToList())b.pickingMode=PickingMode.Position;
        }
        void Update()
        {
            var mission=GetComponent<MissionDemo>();bool game=mission&&mission.HasWorld;
            bool home=platform.enabled&&platform.Phase==TrialPhase.Setup&&!platform.AdvancedVisible;
            Showing=home||game;root.style.display=Showing?DisplayStyle.Flex:DisplayStyle.None;lobby.style.display=home?DisplayStyle.Flex:DisplayStyle.None;hud.style.display=game?DisplayStyle.Flex:DisplayStyle.None;
            if(home){experimentButton.text=platform.LobbySimulation?"실험 시작 · 화면 연습":"실험 시작";device.text=platform.ConnectionReady?"●  조끼 연결됨":"○  조끼 연결 대기";device.EnableInClassList("connected",platform.ConnectionReady);connectionHelp.text=platform.ConnectionReady?"Player 자동 연결  ·  로그인 불필요":"Player에서 조끼를 연결하세요. 조끼 없이도 플레이할 수 있습니다.";testButton.SetEnabled(true);pulse.text=string.IsNullOrEmpty(pulseError)?platform.PulseStatus:pulseError;pulse.style.display=pulse.text.StartsWith("짧은 진동을 보내")?DisplayStyle.None:DisplayStyle.Flex;confirmButton.style.display=DisplayStyle.None;lobbyError.text=string.IsNullOrEmpty(rangeError)?platform.Error:rangeError;}
            if(game){
                var points=mission.SupplyPositions;for(int i=0;i<3;i++){
                    bool visible=i<points.Length;Vector3 v=visible?GetComponent<Camera>().WorldToViewportPoint(points[i]):Vector3.zero;visible=visible&&v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1;
                    supplyMarkers[i].style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                    if(visible){supplyMarkers[i].style.left=Length.Percent(v.x*100);supplyMarkers[i].style.top=Length.Percent((1-v.y)*100);float distance=Vector3.Distance(transform.position,points[i]);supplyMarkers[i].text=distance<3?"[E] 회수":"◇  "+Mathf.RoundToInt(distance)+" m";}
                }
                threatNotice.text=mission.OutputText;threatNotice.style.display=string.IsNullOrEmpty(threatNotice.text)?DisplayStyle.None:DisplayStyle.Flex;
                objectives.text="보급품  "+mission.SuppliesCollected+" / 3\n드론 무력화  "+mission.ShotsHit+" / 5";ammo.text=mission.Ammo.ToString("D2")+"  /  30";status.text=mission.IsReloading?"재장전 중…":(mission.IsCrouched?"앉음  ·  ":"")+"R  재장전";ammoProgress.style.display=mission.IsReloading?DisplayStyle.Flex:DisplayStyle.None;reticle.style.opacity=mission.IsAiming?.15f:.8f;marker.style.display=mission.HitMarker?DisplayStyle.Flex:DisplayStyle.None;pause.style.display=mission.IsPaused?DisplayStyle.Flex:DisplayStyle.None;pauseTitle.text=mission.IsComplete?"작전 완료":"일시정지";continueButton.style.display=mission.IsComplete?DisplayStyle.None:DisplayStyle.Flex;hapticBadge.text=(mission.Physical?"HAPTICS  ●":"HAPTICS  OFF")+"   /   체험 모드";}
        }
        void LateUpdate(){var m=GetComponent<MissionDemo>();RefreshMaps(m,m&&m.HasWorld);RefreshResults(m,m&&m.HasWorld);RefreshDirectionTest();}
        void OnDestroy(){if(document)Destroy(document);if(panel)Destroy(panel);if(font)Destroy(font);if(scrimTexture)Destroy(scrimTexture);}
    }
}

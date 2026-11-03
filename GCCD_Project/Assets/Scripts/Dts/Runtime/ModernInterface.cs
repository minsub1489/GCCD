using UnityEngine;
using UnityEngine.UIElements;
namespace GCCD.Dts
{
    [DisallowMultipleComponent]
    public sealed partial class ModernInterface:MonoBehaviour
    {
        UIDocument document;PanelSettings panel;ExperimentPlatform platform;VisualElement root,lobby,hud,pause,background,reticle,marker,ammoProgress;
        Label device,connectionHelp,pulse,objectives,ammo,status,pauseTitle,hapticBadge,lobbyError,threatNotice;readonly Label[] supplyMarkers=new Label[3];string pulseError="";Texture2D scrimTexture;Button testButton,confirmButton,continueButton,experimentButton;Font font;
        string rangeError = "억제각을 선택하고 최소·최대 위협 수를 입력하세요.";
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
var top=Element(lobby,"lobby-top");Label(top,"Finding Threat","brand");Label(top,"GCCD  ·  HAPTIC FIELD LAB","eyebrow");
            var content = new ScrollView();
            content.AddToClassList("lobby-content");
            lobby.Add(content);
            Label(content,"HAPTIC THREAT DETECTION","eyebrow accent");Label(content,"Finding\nThreat","hero");Label(content,"시야 밖 위협의 방향과 거리를 진동으로 감지하세요.","description");

            var participantField = new TextField("플레이어 이름") { name = "participantName", value = platform.Participant, maxLength = 32 };
            participantField.AddToClassList("study-field");
            participantField.RegisterValueChangedCallback(evt => platform.Participant = evt.newValue);
            content.Add(participantField);

            var angles = new DropdownField("억제각", new System.Collections.Generic.List<string>{"억제각 선택","전체 각도","0°","15°","30°","45°","60°","90°"}, 0);
            angles.name="studyAngle";
            angles.AddToClassList("study-field");
            content.Add(angles);
            var rangeRow = Element(content, "row");
            var minimumField = new TextField("최소 위협 수") { name = "studyThreatMinimum" };
            var maximumField = new TextField("최대 위협 수") { name = "studyThreatMaximum" };
            foreach (var input in new[] { minimumField, maximumField })
            {
                input.style.flexGrow = 1;
                input.AddToClassList("study-field");
                rangeRow.Add(input);

            }
            var scoringRow = Element(content, "row");
            var penaltyAngle = new TextField("FPS 방향 허용각 (°)") { name = "fpsDirectionTolerance", value = "15" };
            var positionTolerance = new TextField("FPS 위치 허용거리 (m)") { name = "fpsPositionTolerance", value = "3" };
            foreach (var input in new[] { penaltyAngle, positionTolerance })
            {
                input.style.flexGrow = 1;
                input.AddToClassList("study-field");
                scoringRow.Add(input);
            }
            Label(content, "각도는 일치와 방향 감점에, 거리는 일치 판정에 적용됩니다.", "small muted");
            bool TryParseTolerance(string value, out double number)
            {
                const System.Globalization.NumberStyles style = System.Globalization.NumberStyles.Float;
                return double.TryParse(value, style, System.Globalization.CultureInfo.CurrentCulture, out number) ||
                    double.TryParse(value, style, System.Globalization.CultureInfo.InvariantCulture, out number);
            }

            string ValidateStudyInputs(out int minimum, out int maximum)
            {
                minimum = maximum = 0;
                bool missingAngle = angles.index <= 0;
                bool missingRange = string.IsNullOrWhiteSpace(minimumField.value) || string.IsNullOrWhiteSpace(maximumField.value);
                if (missingAngle && missingRange) return "억제각을 선택하고 최소·최대 위협 수를 입력하세요.";
                if (missingAngle) return "억제각을 선택하세요.";
                if (missingRange) return "최소·최대 위협 수를 모두 입력하세요.";
                if (!int.TryParse(minimumField.value, out minimum) || !int.TryParse(maximumField.value, out maximum) ||
                    minimum < 2 || maximum < minimum)
                    return "2 이상의 정수를 입력하고 최대 위협 수를 최소 이상으로 설정하세요.";
                return "";
            }

            angles.RegisterValueChangedCallback(evt => rangeError = ValidateStudyInputs(out _, out _));
            minimumField.RegisterValueChangedCallback(evt => rangeError = ValidateStudyInputs(out _, out _));
            maximumField.RegisterValueChangedCallback(evt => rangeError = ValidateStudyInputs(out _, out _));
            penaltyAngle.RegisterValueChangedCallback(evt => rangeError = ValidateStudyInputs(out _, out _));
            positionTolerance.RegisterValueChangedCallback(evt => rangeError = ValidateStudyInputs(out _, out _));
            lobbyError = Label(content, "", "small");
            lobbyError.name = "studyInputError";
            lobbyError.style.color = new Color(1f, .43f, .42f);
            Button(content, "작전 시작    →", () =>
            {
                rangeError = ValidateStudyInputs(out int minimum, out int maximum);
                if (!string.IsNullOrEmpty(rangeError)) return;
                if (maximum > MissionSpawnPlanner.MaximumThreatCount)
                {
                    rangeError = $"FPS 작전의 최대 위협 수는 {MissionSpawnPlanner.MaximumThreatCount}개까지 입력할 수 있습니다.";
                    return;
                }
                int theta = angles.index == 1 ? 45 : ThreatPolicy.Conditions[angles.index - 2];
                if (!TryParseTolerance(penaltyAngle.value, out double direction) || !(direction > 0 && direction <= 180))
                {
                    rangeError = "FPS 방향 허용각을 0° 초과 180° 이하로 입력하세요.";
                    return;
                }
                if (!TryParseTolerance(positionTolerance.value, out double distance) || !(distance > 0 && !double.IsInfinity(distance)))
                {
                    rangeError = "FPS 위치 허용거리를 0m보다 큰 숫자로 입력하세요.";
                    return;
                }
                platform.StartMission(theta, minimum, maximum, direction, distance);
            }, "button primary").name = "startMission";
            var secondary = Element(content, "row");
            Button(secondary, "응답 연습", () => platform.StartLobbyExperiment(true), "button secondary");
            experimentButton = Button(secondary, "실험 시작 · 화면 연습", () =>
            {
                rangeError = ValidateStudyInputs(out int minimum, out int maximum);
                if (!string.IsNullOrEmpty(rangeError)) return;
                int theta = angles.index == 1 ? -1 : ThreatPolicy.Conditions[angles.index - 2];
                platform.StartLobbyExperiment(false, theta, minimum, maximum);
            }, "button secondary");
            experimentButton.name = "startExperiment";

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
            if(home){experimentButton.text=platform.LobbySimulation?"실험 시작 · 화면 연습":"실험 시작";device.text=platform.ConnectionReady?"●  조끼 연결됨":"○  조끼 연결 대기";device.EnableInClassList("connected",platform.ConnectionReady);connectionHelp.text=platform.ConnectionReady?"Player 자동 연결  ·  로그인 불필요":"Player에서 조끼를 연결하세요. 조끼 없이도 플레이할 수 있습니다.";testButton.SetEnabled(true);pulse.text=string.IsNullOrEmpty(pulseError)?platform.PulseStatus:pulseError;pulse.style.display=pulse.text.StartsWith("짧은 진동을 보내")?DisplayStyle.None:DisplayStyle.Flex;confirmButton.style.display=DisplayStyle.None;lobbyError.text=string.IsNullOrEmpty(rangeError)?platform.Error:rangeError;lobbyError.style.display=string.IsNullOrEmpty(lobbyError.text)?DisplayStyle.None:DisplayStyle.Flex;}
            if(game){
                var points=mission.SupplyPositions;for(int i=0;i<3;i++){
                    bool visible=i<points.Length;Vector3 v=visible?GetComponent<Camera>().WorldToViewportPoint(points[i]):Vector3.zero;visible=visible&&v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1;
                    supplyMarkers[i].style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                    if(visible){supplyMarkers[i].style.left=Length.Percent(v.x*100);supplyMarkers[i].style.top=Length.Percent((1-v.y)*100);float distance=Vector3.Distance(transform.position,points[i]);supplyMarkers[i].text=distance<3?"[E] 회수":"◇  "+Mathf.RoundToInt(distance)+" m";}
                }
                threatNotice.text=mission.OutputText;threatNotice.style.display=string.IsNullOrEmpty(threatNotice.text)?DisplayStyle.None:DisplayStyle.Flex;
                objectives.text="보급품  "+mission.SuppliesCollected+" / 3\n드론 무력화  "+mission.ShotsHit+" / 5";ammo.text=mission.Ammo.ToString("D2")+"  /  30";status.text=mission.IsReloading?"재장전 중…":(mission.IsCrouched?"앉음  ·  ":"")+"R  재장전";ammoProgress.style.display=mission.IsReloading?DisplayStyle.Flex:DisplayStyle.None;reticle.style.opacity=mission.IsAiming?.15f:.8f;marker.style.display=mission.HitMarker?DisplayStyle.Flex:DisplayStyle.None;pause.style.display=mission.IsPaused&&!mission.MapVisible?DisplayStyle.Flex:DisplayStyle.None;pauseTitle.text=mission.IsComplete?"작전 완료":"일시정지";continueButton.style.display=mission.IsComplete?DisplayStyle.None:DisplayStyle.Flex;hapticBadge.text=(mission.Physical?"HAPTICS  ●":"HAPTICS  OFF")+"   /   체험 모드";}
        }
        void LateUpdate(){var m=GetComponent<MissionDemo>();RefreshMaps(m,m&&m.HasWorld);RefreshResults(m,m&&m.HasWorld);RefreshDirectionTest();}
        void OnDestroy(){if(document)Destroy(document);if(panel)Destroy(panel);if(font)Destroy(font);if(scrimTexture)Destroy(scrimTexture);}
    }
}

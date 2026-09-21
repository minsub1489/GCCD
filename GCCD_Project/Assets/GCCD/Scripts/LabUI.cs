using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace GCCD
{
    public class LabUI : MonoBehaviour
    {
        public ThreatLab lab;
        Transform root;
        GameObject menu,live,done,distance,feedback,motorPanel;
        TMP_Text heading,condition,status,timer,summary,mode,seedLabel,outputLabel;
        UnityEngine.UI.Image progress;
        readonly UnityEngine.UI.Image[] motors=new UnityEngine.UI.Image[4];
        readonly Color ink=new Color(.025f,.055f,.075f,.96f), panel=new Color(.055f,.105f,.13f,.94f);
        readonly Color mint=new Color(.38f,1,.77f), muted=new Color(.6f,.72f,.76f), white=new Color(.91f,.96f,.97f);
        void Start()
        {
            var go=new GameObject("Research HUD",typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            go.transform.SetParent(transform,false); root=go.transform;
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,900); scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if(FindFirstObjectByType<EventSystem>()==null) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)).transform.SetParent(transform);
            var top=Box(root,"Header",0,0,1600,114,ink);
            Text(top,"GCCD   /   RESEARCH PROTOTYPE 01",32,17,1100,23,16,mint);
            heading=Text(top,"",32,44,1170,42,32,white);
            mode=Text(top,"",1220,35,340,32,16,mint,TextAlignmentOptions.Right);
            condition=Text(root,"",34,135,1100,32,18,mint);
            var track=Box(root,"Progress track",0,111,1600,3,new Color(.15f,.25f,.27f));
            progress=Box(track,"Progress",0,0,1600,3,mint).GetComponent<UnityEngine.UI.Image>();
            // Width is updated directly; no texture-dependent Image fill mode.
            menu=Box(root,"Menu",32,198,580,620,ink).gameObject;
            Text(menu.transform,"LISTEN. LOCATE. CONFIRM.",28,28,530,72,36,white);
            Text(menu.transform,"Find an unseen charging drone using spatial sound and four-point directional cues.",28,117,520,85,23,muted);
            Text(menu.transform,"01   Wear headphones\n02   Hold right mouse or use arrows to look\n03   Aim at a drone, SPACE to select\n04   Report distance: near / mid / far",28,222,520,137,20,white);
            Button(menu.transform,"START PRACTICE  /  12 TRIALS",28,391,524,56,()=>lab.StartSession(true),mint);
            Button(menu.transform,"RESEARCH SESSION  /  144 TRIALS",28,460,524,48,()=>lab.StartSession(false),new Color(.14f,.25f,.29f));
            seedLabel=Text(menu.transform,"",28,531,360,34,17,muted);
            Button(menu.transform,"NEXT SEED",398,525,154,40,()=>lab.seed++,new Color(.14f,.25f,.29f));
            Text(menu.transform,"Prototype audio • no HRTF / Steam Audio yet",28,580,525,25,14,muted);
            var info=Box(menu.transform,"Simulation note",635,363,455,257,panel);
            Text(info,"FOUR-POINT HARNESS",25,23,400,28,20,mint);
            Text(info,"Front upper / lower\nBack upper / lower\n\nSame height = paired pulse\nDistance = three intensity levels",25,67,405,146,20,white);
            live=Box(root,"Live controls",32,685,950,144,ink).gameObject;
            Text(live.transform,"R  REPLAY       SPACE  SELECT       ESC  END SESSION",22,15,895,24,16,muted);
            Button(live.transform,"REPLAY SIGNAL",22,58,232,56,()=>lab.Replay(),new Color(.14f,.25f,.29f));
            Button(live.transform,"CONFIRM AIM",267,58,232,56,()=>lab.SelectAimedTarget(),mint);
            timer=Text(live.transform,"",545,61,372,48,28,white,TextAlignmentOptions.Right);
            distance=Box(root,"Distance response",390,325,820,238,ink).gameObject;
            Text(distance.transform,"ESTIMATE THE SIGNAL DISTANCE",25,23,770,35,27,white);
            Text(distance.transform,"Choose based on the cue you perceived.",25,72,770,30,19,muted);
            for(int i=0;i<3;i++) { int d=i; Button(distance.transform,ThreatLab.DistanceName(i),25+i*260,134,248,66,()=>lab.AnswerDistance(d),mint); }
            feedback=Box(root,"Between trials",390,375,820,159,ink).gameObject;
            Text(feedback.transform,"RESPONSE RECORDED",25,21,770,32,24,white);
            Button(feedback.transform,"CONTINUE",25,76,770,57,()=>lab.Next(),mint);
            done=Box(root,"Results",390,240,820,408,ink).gameObject;
            Text(done.transform,"SESSION SAVED",30,30,760,56,38,white);
            summary=Text(done.transform,"",30,110,760,85,24,muted);
            Button(done.transform,"OPEN RESULTS FOLDER",30,224,760,58,()=>lab.OpenResults(),mint);
            Button(done.transform,"RETURN TO MENU",30,304,760,58,()=>lab.Abort(),new Color(.14f,.25f,.29f));
            motorPanel=Box(root,"Practice motor monitor",1190,204,378,312,ink).gameObject;
            Text(motorPanel.transform,"HAPTIC PREVIEW",22,20,334,28,21,mint);
            Text(motorPanel.transform,"PRACTICE ONLY • VISUAL SIMULATION",22,56,334,25,13,muted);
            string[] labels={"FRONT / UPPER","FRONT / LOWER","BACK / UPPER","BACK / LOWER"};
            for(int i=0;i<4;i++) { motors[i]=Box(motorPanel.transform,labels[i],22,103+i*44,24,24,panel).GetComponent<UnityEngine.UI.Image>(); Text(motorPanel.transform,labels[i],61,100+i*44,290,30,18,white); }
            var crosshair=Text(root,"+",0,0,50,50,30,mint,TextAlignmentOptions.Center).rectTransform;
            crosshair.anchorMin=crosshair.anchorMax=crosshair.pivot=new Vector2(.5f,.5f); crosshair.anchoredPosition=Vector2.zero;
            var bottom=Box(root,"Footer",0,843,1600,57,ink);
            status=Text(bottom,"",25,6,1080,45,16,white);
            outputLabel=Text(bottom,"",1120,8,454,43,13,muted,TextAlignmentOptions.Right);
            Refresh();
        }
        public void Refresh()
        {
            if(heading==null || lab==null) return;
            string s=lab.State;
            heading.text=lab.Heading; condition.text=lab.ConditionLabel;
            mode.text=s=="MENU"?"READY TO CALIBRATE":lab.practice?"PRACTICE / SIMULATION":"RESEARCH / PROTOTYPE";
            menu.SetActive(s=="MENU"); live.SetActive(s=="ACTIVE" || s=="READY"); distance.SetActive(s=="DISTANCE"); feedback.SetActive(s=="FEEDBACK"); done.SetActive(s=="COMPLETE");
            motorPanel.SetActive(lab.practice && (s=="ACTIVE" || s=="READY"));
            for(int i=0;i<4;i++) motors[i].color=Color.Lerp(panel,mint,lab.haptics.levels[i]);
            progress.rectTransform.sizeDelta=new Vector2(1600*lab.Progress01,3);
            seedLabel.text="SESSION SEED  /  "+lab.seed;
            status.text=string.IsNullOrEmpty(lab.Notice)?"Prototype ready. Practice first to learn the cue mapping.":lab.Notice;
            outputLabel.text=lab.haptics.status;
            timer.text=s=="READY"?"PREPARE":lab.Remaining.ToString("0.0")+" s";
            summary.text=lab.Progress+"\nCSV includes condition, errors, response time and replay count.";
        }
        Transform Box(Transform parent,string name,float x,float y,float w,float h,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform; r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h);
            go.GetComponent<UnityEngine.UI.Image>().color=color; go.GetComponent<UnityEngine.UI.Image>().raycastTarget=false; return go.transform;
        }
        TMP_Text Text(Transform parent,string text,float x,float y,float w,float h,int size,Color color,TextAlignmentOptions alignment=TextAlignmentOptions.Left)
        {
            var go=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI)); go.transform.SetParent(parent,false);
            var r=(RectTransform)go.transform; r.anchorMin=r.anchorMax=new Vector2(0,1); r.pivot=new Vector2(0,1); r.anchoredPosition=new Vector2(x,-y); r.sizeDelta=new Vector2(w,h);
            var t=go.GetComponent<TextMeshProUGUI>(); t.text=text; t.fontSize=size; t.color=color; t.alignment=alignment; t.raycastTarget=false; t.textWrappingMode=TextWrappingModes.Normal;
            return t;
        }
        void Button(Transform parent,string title,float x,float y,float w,float h,UnityEngine.Events.UnityAction action,Color color)
        {
            var p=Box(parent,title,x,y,w,h,color); p.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var button=p.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic=p.GetComponent<UnityEngine.UI.Image>(); button.onClick.AddListener(action);
            var colors=button.colors; colors.highlightedColor=new Color(.8f,1,.94f); colors.pressedColor=new Color(.55f,.8f,.7f); button.colors=colors;
            Text(p,title,10,0,w-20,h,17,color==mint?ink:white,TextAlignmentOptions.Center);
        }
    }
}

using UnityEngine;
using UnityEngine.UIElements;

namespace GCCD.Dts
{
    // Presentation only: this layer never reads threat positions, haptic arrays, or scoring data.
    public sealed partial class ModernInterface
    {
        VisualElement lobbyInstrumentation, lobbySweep, lobbyRule, missionChrome, compassTape, presentationLobbyContent, presentationDevice;
        VisualElement ammoRail, missionToast, fireBracketLeft, fireBracketRight;
        VisualElement[] cartridgeSegments, reticleStrokes;
        Label[] compassTicks;
        Label compassHeading, missionClock, toastTitle, toastDetail, systemReadout;
        LobbyFieldDisplay fieldDisplay;
        Toggle reducedMotionToggle;
        MissionDemo presentationMission;
        VisualElement popupPanelRoot;
        StyleSheet popupStyleSheet;
        bool ownsPopupStyleSheet, ownsPopupPanelClass;
        const string PopupPanelClass = "outpost-popup-theme";
        bool presentationHome, presentationReloading, reduceMotion;
        int presentationShots, presentationSupplies, presentationHits;
        double presentationSeconds, revealStart, toastUntil, toastStart, reloadStarted, nextDialRepaint;
        float shotKick;
        const double RevealDuration = .7;
        static readonly Color SignalCyan = new Color(.39f, .89f, .88f);
        static readonly Color SignalAmber = new Color(1f, .76f, .36f);

        void BuildPresentation()
        {
            presentationLobbyContent = lobby.Q<VisualElement>(className: "lobby-content");
            presentationDevice = lobby.Q<VisualElement>(className: "device-panel");
            reduceMotion = PlayerPrefs.GetInt("GCCD.Visual.ReduceMotion", 0) != 0;
            root.EnableInClassList("reduced-motion", reduceMotion);

            lobbyInstrumentation = new VisualElement { name = "lobbyInstrumentation", pickingMode = PickingMode.Ignore };
            lobbyInstrumentation.AddToClassList("lobby-instrumentation");
            lobby.Insert(2, lobbyInstrumentation);
            var frame = Element(lobbyInstrumentation, "field-frame");
            Label(frame, "OUTPOST / 07", "field-code");
            Label(frame, "PERIMETER RECONNAISSANCE", "field-caption");
            fieldDisplay = new LobbyFieldDisplay();
            frame.Add(fieldDisplay);
            Label(frame, "360°", "field-angle");
            Label(frame, "TACTILE AWARENESS", "field-angle-caption");
            var readout = Element(frame, "field-readout");
            Label(readout, "01 / FIELD SYSTEM", "readout-label");
            systemReadout = Label(readout, "SYSTEM STANDBY", "readout-value");
            lobbyRule = Element(lobbyInstrumentation, "lobby-telemetry-rule");
            lobbySweep = Element(lobbyInstrumentation, "lobby-sweep");
            foreach (var e in lobbyInstrumentation.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            TidyLobbySetup();
            SetupPopupPresentation();

            reducedMotionToggle = new Toggle("화면 모션 줄이기") { value = reduceMotion, name = "reduceVisualMotion" };
            reducedMotionToggle.AddToClassList("motion-preference");
            reducedMotionToggle.RegisterValueChangedCallback(evt =>
            {
                reduceMotion = evt.newValue;
                root.EnableInClassList("reduced-motion", reduceMotion);
                PlayerPrefs.SetInt("GCCD.Visual.ReduceMotion", reduceMotion ? 1 : 0);
                PlayerPrefs.Save();
            });
            lobby.Q<VisualElement>(className: "device-panel").Add(reducedMotionToggle);

            missionChrome = Element(hud, "mission-chrome");
            missionChrome.name = "missionPresentation";
            var compass = Element(missionChrome, "compass");
            compassHeading = Label(compass, "N  /  000°", "compass-heading");
            compassTape = Element(compass, "compass-tape");
            compassTicks = new Label[13];
            for (int i = 0; i < compassTicks.Length; i++)
                compassTicks[i] = Label(compassTape, "", "compass-tick");
            Element(compass, "compass-needle");
            missionClock = Label(missionChrome, "00:00", "mission-clock");

            ammoRail = Element(hud.Q<VisualElement>(className: "weapon-hud"), "ammo-rail");
            cartridgeSegments = new VisualElement[30];
            for (int i = 0; i < cartridgeSegments.Length; i++) cartridgeSegments[i] = Element(ammoRail, "ammo-cartridge");
            missionToast = Element(missionChrome, "mission-toast");
            toastTitle = Label(missionToast, "", "toast-title");
            toastDetail = Label(missionToast, "", "toast-detail");
            fireBracketLeft = Element(missionChrome, "fire-bracket fire-bracket-left");
            fireBracketRight = Element(missionChrome, "fire-bracket fire-bracket-right");
            Element(missionChrome, "hud-edge hud-edge-tl");
            Element(missionChrome, "hud-edge hud-edge-tr");
            Element(missionChrome, "hud-edge hud-edge-bl");
            Element(missionChrome, "hud-edge hud-edge-br");
            reticleStrokes = new[] { reticle.Q<VisualElement>(className: "cross-top"), reticle.Q<VisualElement>(className: "cross-bottom"), reticle.Q<VisualElement>(className: "cross-left"), reticle.Q<VisualElement>(className: "cross-right") };
            foreach (var e in missionChrome.Query<VisualElement>().ToList()) e.pickingMode = PickingMode.Ignore;
            missionChrome.pickingMode = PickingMode.Ignore;
            ammoRail.pickingMode = PickingMode.Ignore;
            foreach (var e in cartridgeSegments) e.pickingMode = PickingMode.Ignore;
            missionToast.style.display = DisplayStyle.None;
            missionChrome.style.display = DisplayStyle.None;
        }

        void SetupPopupPresentation()
        {
            // Runtime GenericDropdownMenu is a sibling of the UIDocument root. Give only
            // that panel a popup-scoped sheet so its native hover/navigation states work.
            popupStyleSheet = Resources.Load<StyleSheet>("Realism/UI/OperatorPopup");
            root.RegisterCallback<AttachToPanelEvent>(OnPresentationPanelAttached);
            root.RegisterCallback<DetachFromPanelEvent>(OnPresentationPanelDetached);
            InstallPopupTheme(root.panel);
        }
        void OnPresentationPanelAttached(AttachToPanelEvent evt)
        {
            if (evt.target == root) InstallPopupTheme(evt.destinationPanel);
        }
        void OnPresentationPanelDetached(DetachFromPanelEvent evt)
        {
            if (evt.target == root) RemovePopupTheme();
        }
        void InstallPopupTheme(IPanel targetPanel)
        {
            var tree = targetPanel == null ? null : targetPanel.visualTree;
            if (tree == popupPanelRoot) return;
            RemovePopupTheme();
            if (tree == null || popupStyleSheet == null) return;
            popupPanelRoot = tree;
            ownsPopupStyleSheet = !tree.styleSheets.Contains(popupStyleSheet);
            if (ownsPopupStyleSheet) tree.styleSheets.Add(popupStyleSheet);
            ownsPopupPanelClass = !tree.ClassListContains(PopupPanelClass);
            if (ownsPopupPanelClass) tree.AddToClassList(PopupPanelClass);
        }
        void RemovePopupTheme()
        {
            if (popupPanelRoot == null) return;
            if (ownsPopupStyleSheet && popupStyleSheet != null)
                popupPanelRoot.styleSheets.Remove(popupStyleSheet);
            if (ownsPopupPanelClass) popupPanelRoot.RemoveFromClassList(PopupPanelClass);
            popupPanelRoot = null;
            ownsPopupStyleSheet = ownsPopupPanelClass = false;
        }

        void TidyLobbySetup()
        {
            // Move the existing controls without replacing their values, names, or callbacks.
            var player = lobby.Q<TextField>("participantName");
            var angle = lobby.Q<DropdownField>("studyAngle");
            var setup = player.parent;
            int identityIndex = setup.IndexOf(player);
            var identity = new VisualElement();
            identity.AddToClassList("row");
            identity.AddToClassList("setup-identity");
            setup.Insert(identityIndex, identity);
            identity.Add(player);
            identity.Add(angle);

            var tolerance = lobby.Q<TextField>("fpsDirectionTolerance");
            var scoringRow = tolerance.parent;
            int detailIndex = setup.IndexOf(scoringRow);
            var explanation = setup.ElementAt(detailIndex + 1);
            var details = new Foldout { text = "세부 판정 설정", value = false, name = "scoringPresentationDetails" };
            details.AddToClassList("setup-details");
            setup.Insert(detailIndex, details);
            details.Add(scoringRow);
            details.Add(explanation);
        }

        void RefreshPresentation(MissionDemo mission)
        {
            // Unfocused apps and recall/experiment views do no decorative animation work.
            if (!Application.isFocused)
            {
                missionChrome.style.display = DisplayStyle.None;
                return;
            }
            bool game = mission && mission.HasWorld;
            bool home = platform.enabled && platform.Phase == TrialPhase.Setup && !platform.AdvancedVisible;
            bool running = game && !mission.IsPaused && !mission.IsComplete && !mission.MapVisible && !mission.IsThreatResponse;
            if (!home && !running)
            {
                missionChrome.style.display = DisplayStyle.None;
                if (game) CachePresentationState(mission);
                presentationHome = false;
                return;
            }
            double dt = Mathf.Min(Time.unscaledDeltaTime, .05f);
            presentationSeconds += dt;
            if (home)
            {
                if (!presentationHome) revealStart = presentationSeconds;
                presentationHome = true;
                double age = presentationSeconds - revealStart;
                float reveal = reduceMotion ? 1f : EaseOut((float)(age / RevealDuration));
                presentationLobbyContent.style.opacity = reveal;
                presentationLobbyContent.style.translate = new Translate((1f - reveal) * -14f, 0);
                presentationDevice.style.opacity = reveal;
                presentationDevice.style.translate = new Translate((1f - reveal) * 14f, 0);
                lobbyInstrumentation.style.opacity = reveal;
                lobbyInstrumentation.style.translate = new Translate(0, (1f - reveal) * 18f);
                fieldDisplay.Phase = reduceMotion ? .15f : (float)presentationSeconds;
                // 20 Hz repaint is enough for the ambient dial; no layout allocations or timers.
                if (presentationSeconds >= nextDialRepaint)
                {
                    fieldDisplay.MarkDirtyRepaint();
                    nextDialRepaint = presentationSeconds + .05;
                }
                float scan = reduceMotion ? .5f : Mathf.Repeat((float)presentationSeconds * .055f, 1f);
                lobbySweep.style.top = Length.Percent(scan * 100f);
                lobbySweep.style.opacity = reduceMotion ? 0f : .14f;
                lobbyRule.style.opacity = .45f;
                systemReadout.text = platform.ConnectionReady ? "HAPTIC LINK ONLINE" : "SYSTEM STANDBY";
                systemReadout.style.color = platform.ConnectionReady ? SignalCyan : SignalAmber;
                missionChrome.style.display = DisplayStyle.None;
                presentationMission = null;
                return;
            }
            presentationHome = false;
            missionChrome.style.display = DisplayStyle.Flex;
            if (presentationMission != mission)
            {
                presentationMission = mission;
                CachePresentationState(mission);
                ShowPresentationToast("작전 개시", "보급품 3개 회수  /  드론 5대 무력화", 3.0);
            }
            if (mission.ShotsFired != presentationShots) shotKick = reduceMotion ? 0f : 1f;
            shotKick = Mathf.MoveTowards(shotKick, 0f, (float)dt * 7f);
            fireBracketLeft.style.opacity = shotKick * .3f;
            fireBracketRight.style.opacity = shotKick * .3f;
            if (!reduceMotion)
            {
                float spread = shotKick * 5f;
                reticleStrokes[0].style.top = -12f - spread;
                reticleStrokes[1].style.top = 7f + spread;
                reticleStrokes[2].style.left = -12f - spread;
                reticleStrokes[3].style.left = 7f + spread;
            }
            else
            {
                reticleStrokes[0].style.top = -12;
                reticleStrokes[1].style.top = 7;
                reticleStrokes[2].style.left = -12;
                reticleStrokes[3].style.left = 7;
            }
            if (mission.SuppliesCollected > presentationSupplies)
                ShowPresentationToast("보급품 확보", mission.SuppliesCollected + " / 3  RECOVERED", 2.2);
            else if (mission.ShotsHit > presentationHits)
                ShowPresentationToast("드론 무력화", mission.ShotsHit + " / 5  CONFIRMED", 1.6);
            if (mission.IsReloading)
                ammoProgress.style.width = Length.Percent(Mathf.Clamp01(presentationReloading ? (float)((mission.Elapsed - reloadStarted) / 1.7) : 0f) * 100f);
            for (int i = 0; i < cartridgeSegments.Length; i++)
            {
                cartridgeSegments[i].style.opacity = i < mission.Ammo ? 1f : .15f;
                cartridgeSegments[i].style.backgroundColor = mission.Ammo <= 5 ? SignalAmber : SignalCyan;
            }
            ammo.EnableInClassList("ammo-low", mission.Ammo <= 5);
            marker.style.color = SignalAmber;
            marker.style.opacity = mission.HitMarker ? 1f : 0f;
            UpdateCompass(transform.eulerAngles.y);
            int seconds = Mathf.Max(0, (int)mission.Elapsed);
            missionClock.text = "OPERATION  /  " + (seconds / 60).ToString("D2") + ":" + (seconds % 60).ToString("D2");
            bool toastVisible = presentationSeconds < toastUntil;
            missionToast.style.display = toastVisible ? DisplayStyle.Flex : DisplayStyle.None;
            if (toastVisible)
            {
                float appear = reduceMotion ? 1f : EaseOut((float)((presentationSeconds - toastStart) / .3));
                float fade = Mathf.Clamp01((float)((toastUntil - presentationSeconds) / .35));
                missionToast.style.opacity = appear * fade;
                missionToast.style.translate = new Translate(0, (1f - appear) * -10f);
            }
            CachePresentationState(mission);
        }

        static float EaseOut(float value)
        {
            float t = Mathf.Clamp01(value);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }
        void CachePresentationState(MissionDemo mission)
        {
            if (mission.IsReloading && !presentationReloading) reloadStarted = mission.Elapsed;
            presentationShots = mission.ShotsFired;
            presentationSupplies = mission.SuppliesCollected;
            presentationHits = mission.ShotsHit;
            presentationReloading = mission.IsReloading;
        }
        void ShowPresentationToast(string title, string detail, double seconds)
        {
            toastTitle.text = title;
            toastDetail.text = detail;
            toastStart = presentationSeconds;
            toastUntil = presentationSeconds + seconds;
        }
        static readonly string[] cardinal = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        void UpdateCompass(float heading)
        {
            compassHeading.text = cardinal[Mathf.RoundToInt(heading / 45f) % 8] + "  /  " + Mathf.RoundToInt(heading).ToString("D3") + "°";
            int first = Mathf.FloorToInt(heading / 15f) * 15 - 90;
            for (int i = 0; i < compassTicks.Length; i++)
            {
                int angle = first + i * 15;
                float offset = Mathf.DeltaAngle(heading, angle);
                compassTicks[i].style.left = Length.Percent(50f + offset / 1.5f);
                int bearing = ((angle % 360) + 360) % 360;
                compassTicks[i].text = bearing % 45 == 0 ? cardinal[bearing / 45] : bearing.ToString("D3");
                compassTicks[i].style.color = Mathf.Abs(offset) < 8 ? SignalCyan : new Color(.58f, .69f, .70f);
            }
        }

        sealed class LobbyFieldDisplay : VisualElement
        {
            public float Phase;
            public LobbyFieldDisplay()
            {
                AddToClassList("field-dial");
                pickingMode = PickingMode.Ignore;
                generateVisualContent += Draw;
            }
            void Draw(MeshGenerationContext context)
            {
                float width = contentRect.width, height = contentRect.height;
                if (width < 1 || height < 1) return;
                var painter = context.painter2D;
                Vector2 center = new Vector2(width * .5f, height * .5f);
                float radius = Mathf.Min(width, height) * .43f;
                painter.lineWidth = 1f;
                for (int ring = 1; ring <= 4; ring++)
                {
                    painter.strokeColor = new Color(.31f, .67f, .68f, ring == 4 ? .5f : .17f);
                    painter.BeginPath();
                    painter.Arc(center, radius * ring / 4f, 0, 360);
                    painter.Stroke();
                }
                for (int i = 0; i < 72; i++)
                {
                    float a = i * 5f * Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
                    float inner = i % 6 == 0 ? .91f : .96f;
                    painter.strokeColor = new Color(.40f, .80f, .80f, i % 6 == 0 ? .7f : .3f);
                    painter.BeginPath();painter.MoveTo(center + direction * radius * inner);painter.LineTo(center + direction * radius);painter.Stroke();
                }
                // Architectural plot, with no enemies or live-world data.
                painter.strokeColor = new Color(.41f, .71f, .71f, .30f);
                foreach (int side in new[] { -1, 1 })
                {
                    float x = center.x + side * radius * .22f;
                    float far = center.x + side * radius * .64f;
                    for (int row = 0; row < 4; row++)
                    {
                        float y = center.y - radius * .59f + row * radius * .30f;
                        painter.BeginPath();painter.MoveTo(new Vector2(x,y));painter.LineTo(new Vector2(far,y));painter.LineTo(new Vector2(far,y+radius*.23f));painter.LineTo(new Vector2(x,y+radius*.23f));painter.Stroke();
                    }
                }
                painter.strokeColor = new Color(.88f, .66f, .31f, .65f);
                painter.lineWidth = 2f;
                painter.BeginPath();painter.Arc(center, radius * 1.07f, Phase * 7f, Phase * 7f + 52f);painter.Stroke();
                painter.strokeColor = new Color(.28f, .9f, .86f, .7f);
                painter.BeginPath();painter.Arc(center, radius * .85f, 170f - Phase * 11f, 240f - Phase * 11f);painter.Stroke();
                float sweep = Phase * .22f;
                painter.strokeColor = new Color(.38f, .88f, .82f, .18f);
                painter.lineWidth = 1f;
                painter.BeginPath();painter.MoveTo(center);painter.LineTo(center + new Vector2(Mathf.Sin(sweep),-Mathf.Cos(sweep))*radius*.88f);painter.Stroke();
                painter.fillColor = new Color(.64f, .91f, .87f, .95f);
                painter.BeginPath();painter.MoveTo(center+new Vector2(0,-8));painter.LineTo(center+new Vector2(5,5));painter.LineTo(center);painter.LineTo(center+new Vector2(-5,5));painter.ClosePath();painter.Fill();
            }
        }
    }
}

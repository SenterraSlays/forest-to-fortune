using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// A short guided start: chop, collect, sell, upgrade, then buy the first Auto-Chopper.
    /// Shows the current objective at the top of the screen with an on-screen marker pointing
    /// at the place to go. Progress is saved; H hides the hints.
    /// </summary>
    public class Tutorial : MonoBehaviour
    {
        const int StepChop = 0, StepCollect = 1, StepSell = 2, StepUpgrade = 3, StepChopper = 4, StepDone = 5;

        RectTransform m_Panel, m_Marker;
        TextMeshProUGUI m_Step, m_Body, m_MarkerText;
        Image m_StepIcon;
        readonly Image[] m_Dots = new Image[5];
        CanvasGroup m_Group;
        float m_DoneTimer;
        bool m_Hidden;
        GameModel m_Model;
        float m_Pulse;
        int m_LastStep = -1;

        public void Create(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.Stretch();
            m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;

            var panel = UIFactory.Surface(rt, "Objective", UIStyle.Hud);
            m_Panel = panel.rectTransform;
            m_Panel.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(600, 0));
            UIFactory.HStack(panel, 16f, new RectOffset(16, 22, 14, 14), TextAnchor.MiddleLeft);
            UIFactory.Fit(panel, false, true);

            var badge = UIFactory.Panel(panel.transform, "Badge", UIStyle.Accent);
            badge.sprite = UIStyle.Circle;
            badge.Layout(52, 52);
            m_StepIcon = UIFactory.Icon(badge.transform, "axe", UIStyle.AccentDark, 30f);
            m_StepIcon.rectTransform.anchoredPosition = Vector2.zero;
            m_StepIcon.GetComponent<LayoutElement>().ignoreLayout = true;

            var col = UIFactory.Rect("Col", panel.transform);
            UIFactory.VStack(col, 5f);
            col.Layout(-1, -1, 1);
            var top = UIFactory.Rect("Top", col);
            UIFactory.HStack(top, 8f);
            m_Step = UIFactory.Caption(top, "", UIStyle.Accent, 14f);
            m_Step.Layout(-1, -1, 1);
            for (int i = 0; i < 5; i++)
            {
                var d = UIFactory.Panel(top, "Dot", UIStyle.Dim);
                d.sprite = UIStyle.Circle;
                d.Layout(9, 9);
                m_Dots[i] = d;
            }
            m_Body = UIFactory.Text(col, "", 22, UIStyle.Text);
            var hint = UIFactory.Rect("Hint", col);
            UIFactory.HStack(hint, 6f);
            UIFactory.KeyCap(hint, "H", 20f);
            UIFactory.Text(hint, "hide hints", 14, UIStyle.Dim).textWrappingMode = TextWrappingModes.NoWrap;

            var marker = UIFactory.Rect("Marker", rt);
            m_Marker = marker;
            marker.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40, 40));
            var ring = UIFactory.Panel(marker, "Ring", UIStyle.Accent, false);
            ring.sprite = UIStyle.Circle;
            ring.rectTransform.Stretch();
            var inner = UIFactory.Panel(marker, "Inner", UIStyle.AccentDark, false);
            inner.sprite = UIStyle.Circle;
            inner.rectTransform.Stretch(4, 4, 4, 4);
            var arrow = UIFactory.Icon(marker, "star", UIStyle.Accent, 22f);
            arrow.rectTransform.anchoredPosition = Vector2.zero;
            m_MarkerText = UIFactory.Text(marker, "", 17, UIStyle.Text, true, TextAlignmentOptions.Center);
            m_MarkerText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(200, 26));
            m_MarkerText.outlineWidth = 0.22f;
            m_MarkerText.outlineColor = new Color32(0, 0, 0, 200);

            m_Model = Game.Instance.Model;
            m_Model.TreeFelled += _ => Advance(StepChop);
            m_Model.Collected += (_, __) => Advance(StepCollect);
            m_Model.Sold += (_, __) => Advance(StepSell);
            m_Model.Purchased += _ => { Advance(StepUpgrade); if (m_Model.Production.ProducesAnything) Advance(StepChopper); };
        }

        int Step
        {
            get => m_Model.State.TutorialStep;
            set => m_Model.State.TutorialStep = value;
        }

        void Advance(int from)
        {
            if (m_Model.State.TutorialDone || Step != from) return;
            Step = from + 1;
            m_Pulse = 1f;
            AudioManager.Ui("objective", 0.6f);
            if (Step == StepCollect && m_Model.State.Carried(GameModel.Wood) >= 1) Step = StepSell;
            if (Step == StepUpgrade && AnyUpgradeOwned()) Step = StepChopper;
            if (Step == StepChopper && m_Model.Production.ProducesAnything) Step = StepDone;
            if (Step == StepDone) m_DoneTimer = 9f;
        }

        bool AnyUpgradeOwned()
        {
            foreach (var u in m_Model.Db.upgrades) if (u != null && m_Model.Upgrades.Level(u) > 0) return true;
            return false;
        }

        void Update()
        {
            if (GameInput.HintsTogglePressed && UIState.GameplayInput) m_Hidden = !m_Hidden;
            bool show = !m_Model.State.TutorialDone && GameSettings.Data.showTutorial && !m_Hidden
                        && Game.Instance.Phase == GamePhase.Playing && !UIState.AnyMenuOpen;
            m_Group.alpha = Mathf.MoveTowards(m_Group.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (m_Model.State.TutorialDone) return;

            if (Step >= StepDone)
            {
                if (Game.Instance.Phase == GamePhase.Playing) m_DoneTimer -= Time.deltaTime;
                if (m_DoneTimer <= 0f) m_Model.State.TutorialDone = true;
            }

            Transform target = null;
            string body, icon;
            var cost = m_Model.Db.CountUpgradeFor("chopper");
            switch (Step)
            {
                case StepChop:
                    body = GameInput.UsingTouch ? "Walk up to a tree and <b>hold the axe button</b> to chop it down."
                        : GameInput.UsingGamepad ? $"Walk up to a tree and <b>hold {GameInput.Key("chop")}</b> to chop it down." : "Walk up to a tree and <b>hold Left Mouse</b> to chop it down.";
                    icon = "axe";
                    break;
                case StepCollect:
                    body = $"Walk over the <b>logs</b> to collect the wood. Your backpack holds <b>{NumberUtil.Format(m_Model.BackpackCapacity)}</b>.";
                    icon = "log";
                    break;
                case StepSell:
                    body = GameInput.UsingTouch ? "Take your wood to the <b>Lumber Market</b> and tap <b>Use</b> to sell it."
                        : $"Take your wood to the <b>Lumber Market</b> and press <b>{GameInput.Key("use")}</b> to sell it.";
                    icon = "coin";
                    target = SellPoint.Instance != null ? SellPoint.Instance.transform : null;
                    break;
                case StepUpgrade:
                    body = GameInput.UsingTouch ? "Tap <b>Use</b> at the <b>Workbench</b> to open the skill tree, and unlock the <b>Steel Axe</b>."
                        : $"Open the <b>Workbench</b> skill tree ({GameInput.Key("use")}) and unlock the <b>Steel Axe</b>.";
                    icon = "leaf";
                    target = UpgradeStation.Instance != null ? UpgradeStation.Instance.transform : null;
                    break;
                case StepChopper:
                    double price = cost != null ? m_Model.Upgrades.NextCost(cost) : 200;
                    body = $"Save up for your first <b>Auto-Chopper</b>.  <color=#FFD466>{NumberUtil.Money(m_Model.State.Money)} / {NumberUtil.Money(price)}</color>";
                    icon = "saw";
                    if (m_Model.State.Money >= price && UpgradeStation.Instance != null) target = UpgradeStation.Instance.transform;
                    break;
                default:
                    body = "Machines now make wood for you, even while you're away. Keep chopping and grow the skill tree!";
                    icon = "check";
                    break;
            }
            int shown = Mathf.Min(Step, StepDone);
            m_Step.text = Step >= StepDone ? "AUTOMATION ONLINE" : $"STEP {Step + 1} OF 5";
            m_Body.text = body;
            if (shown != m_LastStep)
            {
                m_LastStep = shown;
                m_StepIcon.sprite = UIIcons.Get(icon);
            }
            for (int i = 0; i < 5; i++) m_Dots[i].color = i < Step ? UIStyle.Good : i == Step ? UIStyle.Accent : UIStyle.Dim;
            m_Pulse = Mathf.MoveTowards(m_Pulse, 0f, Time.unscaledDeltaTime * 1.5f);
            m_Panel.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(m_Pulse * Mathf.PI));
            PlaceMarker(target);
        }

        void PlaceMarker(Transform target)
        {
            var cam = Camera.main;
            if (target == null || cam == null)
            {
                m_Marker.gameObject.SetActive(false);
                return;
            }
            m_Marker.gameObject.SetActive(true);
            Vector3 world = target.position + Vector3.up * 2.6f;
            Vector3 sp = cam.WorldToScreenPoint(world);
            var parent = (RectTransform)transform;
            bool behind = sp.z < 0f;
            if (behind) sp = new Vector3(Screen.width - sp.x, Screen.height - sp.y, 0f);
            float margin = 70f;
            Vector2 clamped = new Vector2(Mathf.Clamp(sp.x, margin, Screen.width - margin), Mathf.Clamp(sp.y, margin, Screen.height - margin));
            if (behind) clamped.y = margin;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, clamped, null, out var local);
            m_Marker.anchoredPosition = local;
            float dist = Vector3.Distance(cam.transform.position, target.position);
            m_MarkerText.text = $"{dist:0} m";
            m_Marker.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }
}

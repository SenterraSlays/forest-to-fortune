using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

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
        TextMeshProUGUI m_Title, m_Body, m_MarkerText;
        CanvasGroup m_Group;
        float m_DoneTimer;
        bool m_Hidden;
        GameModel m_Model;
        float m_Pulse;

        public void Create(RectTransform parent)
        {
            var rt = (RectTransform)transform;
            rt.SetParent(parent, false);
            rt.Stretch();
            m_Group = gameObject.AddComponent<CanvasGroup>();
            m_Group.blocksRaycasts = false;
            m_Group.interactable = false;

            var panel = UIFactory.Panel(rt, "Objective", UIStyle.Hud);
            m_Panel = panel.rectTransform;
            m_Panel.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(560, 0));
            UIFactory.VStack(panel, 4f, new RectOffset(22, 22, 12, 14), TextAnchor.UpperCenter);
            UIFactory.Fit(panel, false, true);
            m_Title = UIFactory.Text(panel.transform, "", 19, UIStyle.Accent, true, TextAlignmentOptions.Center);
            m_Title.characterSpacing = 4f;
            m_Body = UIFactory.Text(panel.transform, "", 25, UIStyle.Text, false, TextAlignmentOptions.Center);
            var hint = UIFactory.Text(panel.transform, "H to hide hints", 15, new Color(1, 1, 1, 0.4f), false, TextAlignmentOptions.Center);

            var marker = UIFactory.Panel(rt, "Marker", UIStyle.Accent);
            marker.sprite = UIStyle.Circle;
            m_Marker = marker.rectTransform;
            m_Marker.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            m_MarkerText = UIFactory.Text(m_Marker, "", 18, UIStyle.Text, true, TextAlignmentOptions.Center);
            m_MarkerText.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0, -6), new Vector2(200, 30));
            m_MarkerText.outlineWidth = 0.2f;
            m_MarkerText.outlineColor = new Color32(0, 0, 0, 180);

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
            // Skip steps the player already did out of order.
            if (Step == StepCollect && m_Model.State.Get(GameModel.Wood) >= 1) Step = StepSell;
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
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame && UIState.GameplayInput) m_Hidden = !m_Hidden;
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
            string title, body;
            var cost = m_Model.Db.CountUpgradeFor("chopper");
            switch (Step)
            {
                case StepChop:
                    title = "STEP 1 OF 5";
                    body = "Walk up to a tree and <b>hold Left Mouse</b> to chop it down.";
                    break;
                case StepCollect:
                    title = "STEP 2 OF 5";
                    body = "Walk over the <b>logs</b> to collect the wood.";
                    break;
                case StepSell:
                    title = "STEP 3 OF 5";
                    body = "Take your wood to the <b>Lumber Market</b> and press <b>E</b> to sell it.";
                    target = SellPoint.Instance != null ? SellPoint.Instance.transform : null;
                    break;
                case StepUpgrade:
                    title = "STEP 4 OF 5";
                    body = "Use the <b>Workbench</b> (E) to buy an upgrade. The <b>Steel Axe</b> is a good start.";
                    target = UpgradeStation.Instance != null ? UpgradeStation.Instance.transform : null;
                    break;
                case StepChopper:
                    double price = cost != null ? m_Model.Upgrades.NextCost(cost) : 200;
                    title = "STEP 5 OF 5";
                    body = $"Save up and buy your first <b>Auto-Chopper</b> at the Workbench.\n<size=80%><color=#FBD661>{NumberUtil.Money(m_Model.State.Money)} / {NumberUtil.Money(price)}</color></size>";
                    if (m_Model.State.Money >= price && UpgradeStation.Instance != null) target = UpgradeStation.Instance.transform;
                    break;
                default:
                    title = "AUTOMATION ONLINE";
                    body = "Machines now make wood for you, even while you're away. Keep chopping, upgrading and expanding into new forests!";
                    break;
            }
            m_Title.text = title;
            m_Body.text = body;
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
            // Clamp to the screen edge so the marker always shows the direction.
            float margin = 60f;
            Vector2 clamped = new Vector2(Mathf.Clamp(sp.x, margin, Screen.width - margin), Mathf.Clamp(sp.y, margin, Screen.height - margin));
            if (behind) clamped.y = margin;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, clamped, null, out var local);
            m_Marker.anchoredPosition = local;
            float dist = Vector3.Distance(cam.transform.position, target.position);
            m_MarkerText.text = $"{dist:0} m";
            m_Marker.localScale = Vector3.one * (1f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f));
        }
    }
}

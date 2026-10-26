using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// In-game overlay: money and resources with production rates, crosshair and hit marker,
    /// the targeted tree's health, interaction prompts, auto-sell progress and the save indicator.
    /// Text is only reformatted when the shown value changes.
    /// </summary>
    public class Hud : UIScreen
    {
        class ResourceRow
        {
            public ResourceDef def;
            public GameObject go;
            public TextMeshProUGUI value, rate;
            public string lastValue, lastRate;
            public double lastAmount;
            public float punch;
        }

        protected override bool Modal => false;

        AxeController m_Axe;
        Interactor m_Interactor;

        TextMeshProUGUI m_Money, m_Prompt, m_PromptDetail, m_TreeName, m_TreeNote, m_Saved, m_Hint;
        RectTransform m_MoneyRect;
        double m_LastMoney = -1;
        float m_MoneyPunch;
        readonly List<ResourceRow> m_Rows = new List<ResourceRow>();
        Image m_Crosshair, m_HitMarker, m_TreeBar, m_AutoSellFill;
        GameObject m_TreePanel, m_PromptPanel, m_AutoSell;
        float m_HitFlash, m_SavedTimer;
        Color m_HitColor = Color.white;
        string m_LastAxe;

        public void Bind(AxeController axe, Interactor interactor)
        {
            m_Axe = axe;
            m_Interactor = interactor;
            if (m_Axe != null) m_Axe.Hit += OnHit;
        }

        protected override void Build()
        {
            var game = Game.Instance;

            // ---- resources (top-left)
            var res = UIFactory.Panel(Root, "Resources", UIStyle.Hud);
            res.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(380, 0));
            UIFactory.VStack(res, 4f, new RectOffset(18, 18, 14, 14));
            UIFactory.Fit(res, false, true);
            m_Money = UIFactory.Text(res.transform, "$0", 40, UIStyle.Money, true);
            m_Money.textWrappingMode = TextWrappingModes.NoWrap;
            m_MoneyRect = m_Money.rectTransform;
            foreach (var r in game.Db.resources)
            {
                if (r == null) continue;
                var row = UIFactory.Rect(r.id, res.transform);
                UIFactory.HStack(row, 10f);
                var dot = UIFactory.Panel(row, "Icon", r.color);
                dot.Layout(16, 16);
                var label = UIFactory.Text(row, r.displayName, 22, UIStyle.Muted);
                label.Layout(78);
                var value = UIFactory.Text(row, "0", 26, UIStyle.Text, true);
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.Layout(1, -1, 1);
                var rate = UIFactory.Text(row, "", 20, UIStyle.Good, false, TextAlignmentOptions.Right);
                rate.textWrappingMode = TextWrappingModes.NoWrap;
                rate.Layout(110);
                m_Rows.Add(new ResourceRow { def = r, go = row.gameObject, value = value, rate = rate });
            }
            m_AxeLabelHolder = UIFactory.Text(res.transform, "", 19, UIStyle.Muted);
            var autoRow = UIFactory.Rect("AutoSell", res.transform);
            UIFactory.HStack(autoRow, 8f);
            UIFactory.Text(autoRow, "Courier", 18, UIStyle.Muted).Layout(78);
            var track = UIFactory.Panel(autoRow, "Track", UIStyle.Button);
            track.Layout(-1, 8, 1);
            m_AutoSellFill = UIFactory.Panel(track.transform, "Fill", UIStyle.Money);
            m_AutoSellFill.rectTransform.Stretch();
            m_AutoSellFill.type = Image.Type.Filled;
            m_AutoSellFill.fillMethod = Image.FillMethod.Horizontal;
            m_AutoSellFill.sprite = null;
            m_AutoSell = autoRow.gameObject;

            // ---- crosshair
            m_Crosshair = UIFactory.Panel(Root, "Crosshair", Color.white);
            m_Crosshair.sprite = UIStyle.Circle;
            m_Crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 8));
            m_HitMarker = UIFactory.Panel(Root, "HitMarker", Color.white);
            m_HitMarker.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            m_HitMarker.sprite = UIStyle.Circle;
            m_HitMarker.type = Image.Type.Simple;
            m_HitMarker.color = new Color(1, 1, 1, 0);
            var outline = m_HitMarker.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.3f);

            // ---- tree info (above crosshair)
            var tree = UIFactory.Panel(Root, "Tree", UIStyle.Hud);
            tree.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, 46), new Vector2(300, 0));
            UIFactory.VStack(tree, 4f, new RectOffset(14, 14, 8, 10), TextAnchor.UpperCenter);
            UIFactory.Fit(tree, false, true);
            m_TreeName = UIFactory.Text(tree.transform, "", 22, UIStyle.Text, true, TextAlignmentOptions.Center);
            var barBg = UIFactory.Panel(tree.transform, "Bar", UIStyle.Button);
            barBg.Layout(-1, 10);
            m_TreeBar = UIFactory.Panel(barBg.transform, "Fill", UIStyle.Good);
            m_TreeBar.rectTransform.Stretch();
            m_TreeBar.sprite = null;
            m_TreeBar.type = Image.Type.Filled;
            m_TreeBar.fillMethod = Image.FillMethod.Horizontal;
            m_TreeNote = UIFactory.Text(tree.transform, "", 18, UIStyle.Bad, false, TextAlignmentOptions.Center);
            m_TreePanel = tree.gameObject;

            // ---- prompt (below crosshair)
            var prompt = UIFactory.Panel(Root, "Prompt", UIStyle.Hud);
            prompt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -60), new Vector2(0, 0));
            UIFactory.VStack(prompt, 2f, new RectOffset(18, 18, 8, 10), TextAnchor.UpperCenter);
            UIFactory.Fit(prompt, true, true);
            m_Prompt = UIFactory.Text(prompt.transform, "", 24, UIStyle.Text, true, TextAlignmentOptions.Center);
            m_PromptDetail = UIFactory.Text(prompt.transform, "", 19, UIStyle.Muted, false, TextAlignmentOptions.Center);
            m_PromptPanel = prompt.gameObject;

            // ---- saved indicator and control hint (bottom)
            m_Saved = UIFactory.Text(Root, "Progress saved", 18, UIStyle.Muted, false, TextAlignmentOptions.Right);
            m_Saved.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 18), new Vector2(300, 30));
            m_Saved.alpha = 0f;
            m_Hint = UIFactory.Text(Root, "<b>LMB</b> chop   <b>E</b> use   <b>Shift</b> sprint   <b>Space</b> jump   <b>Esc</b> menu", 18, new Color(1, 1, 1, 0.55f), false, TextAlignmentOptions.Left);
            m_Hint.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 16), new Vector2(800, 30));

            game.Saved += () => m_SavedTimer = 2f;
        }

        TextMeshProUGUI m_AxeLabelHolder;

        void OnHit(HitResult r, ChoppableTree tree)
        {
            m_HitFlash = 1f;
            m_HitColor = r == HitResult.TooTough ? UIStyle.Bad : r == HitResult.Felled ? UIStyle.Money : Color.white;
        }

        protected override void Update()
        {
            base.Update();
            var game = Game.Instance;
            if (game == null) return;
            var model = game.Model;
            float dt = Time.unscaledDeltaTime;

            // Money with a small punch when it rises.
            double money = model.State.Money;
            if (money != m_LastMoney)
            {
                if (m_LastMoney >= 0 && money > m_LastMoney) m_MoneyPunch = 1f;
                m_LastMoney = money;
                m_Money.text = NumberUtil.Money(money);
            }
            m_MoneyPunch = Mathf.MoveTowards(m_MoneyPunch, 0f, dt * 4f);
            m_MoneyRect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(m_MoneyPunch * Mathf.PI));

            foreach (var row in m_Rows)
            {
                double amount = model.State.Get(row.def.id);
                double net = model.Production.NetRate(row.def.id);
                double gross = model.Production.GrossRate(row.def.id);
                bool show = !row.def.hiddenUntilProduced || amount >= 1 || gross > 0;
                if (row.go.activeSelf != show) row.go.SetActive(show);
                if (!show) continue;
                string v = NumberUtil.Format(amount);
                if (v != row.lastValue)
                {
                    if (amount > row.lastAmount && row.lastValue != null) row.punch = 1f;
                    row.lastValue = v;
                    row.value.text = v;
                }
                row.lastAmount = amount;
                string rate = gross > 0 || net < 0 ? NumberUtil.Rate(net) : "";
                if (rate != row.lastRate)
                {
                    row.lastRate = rate;
                    row.rate.text = rate;
                    row.rate.color = net >= 0 ? UIStyle.Good : UIStyle.Bad;
                }
                row.punch = Mathf.MoveTowards(row.punch, 0f, dt * 5f);
                row.value.color = Color.Lerp(UIStyle.Text, UIStyle.Accent, row.punch);
            }

            string axe = $"{model.Upgrades.AxeName}  -  {NumberUtil.Format(model.Upgrades.AxeDamage, true)} dmg";
            if (axe != m_LastAxe) { m_LastAxe = axe; m_AxeLabelHolder.text = axe; }

            bool courier = model.Upgrades.AutoSellSeconds > 0;
            if (m_AutoSell.activeSelf != courier) m_AutoSell.SetActive(courier);
            if (courier) m_AutoSellFill.fillAmount = model.AutoSellProgress;

            // Crosshair and target info.
            var tree = m_Axe != null ? m_Axe.AimedTree : null;
            var inter = m_Interactor != null ? m_Interactor.Current : null;
            bool aiming = tree != null || inter != null;
            m_Crosshair.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(m_Crosshair.rectTransform.sizeDelta.x, aiming ? 12f : 7f, 1f - Mathf.Exp(-20f * dt));
            m_Crosshair.color = aiming ? UIStyle.Accent : new Color(1, 1, 1, 0.85f);
            m_HitFlash = Mathf.MoveTowards(m_HitFlash, 0f, dt * 5f);
            var hc = m_HitColor;
            hc.a = m_HitFlash * 0.6f;
            m_HitMarker.color = hc;
            m_HitMarker.rectTransform.localScale = Vector3.one * (1.3f - 0.5f * m_HitFlash);

            if (tree != null)
            {
                if (!m_TreePanel.activeSelf) m_TreePanel.SetActive(true);
                m_TreeName.text = $"{tree.Type.displayName}  <size=80%><color=#B5AE9C>{NumberUtil.Format(tree.Health, true)} / {NumberUtil.Format(tree.MaxHealth)}</color></size>";
                float f = (float)(tree.Health / tree.MaxHealth);
                m_TreeBar.fillAmount = f;
                m_TreeBar.color = Color.Lerp(UIStyle.Bad, UIStyle.Good, f);
                int need = tree.Type.requiredAxeTier;
                if (model.Upgrades.AxeTier < need)
                {
                    var tierUp = model.Upgrades.FirstWithEffect(UpgradeEffect.AxeTier);
                    m_TreeNote.text = "Too tough! Needs " + (tierUp != null ? tierUp.LevelName(need) : "a better axe");
                    m_TreeNote.gameObject.SetActive(true);
                }
                else if (m_TreeNote.gameObject.activeSelf) m_TreeNote.gameObject.SetActive(false);
            }
            else if (m_TreePanel.activeSelf) m_TreePanel.SetActive(false);

            if (inter != null && tree == null)
            {
                if (!m_PromptPanel.activeSelf) m_PromptPanel.SetActive(true);
                m_Prompt.text = $"<color=#FBBD52>[E]</color> {inter.Prompt}";
                string d = inter.Detail;
                m_PromptDetail.gameObject.SetActive(!string.IsNullOrEmpty(d));
                if (d != null) m_PromptDetail.text = d;
            }
            else if (m_PromptPanel.activeSelf) m_PromptPanel.SetActive(false);

            m_SavedTimer -= dt;
            m_Saved.alpha = Mathf.Clamp01(m_SavedTimer);
            m_Hint.alpha = model.State.TutorialDone ? 0.35f : 0.6f;
        }
    }
}

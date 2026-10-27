using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// In-game overlay: money, the backpack (what the player carries), the lumber yard stock with
    /// production rates, the equipped axe, crosshair and
    /// hit marker, the targeted tree's health, interaction prompts with key caps, the courier
    /// timer, the save indicator and the FPS counter (F3 cycles it).
    /// Text is only reformatted when the shown value changes.
    /// </summary>
    public class Hud : UIScreen
    {
        class ResourceRow
        {
            public ResourceDef def;
            public GameObject go;
            public TextMeshProUGUI value, rate;
            public Image ratePill;
            public string lastValue, lastRate;
            public double lastAmount;
            public float punch;
        }

        protected override bool Modal => false;

        AxeController m_Axe;
        Interactor m_Interactor;

        TextMeshProUGUI m_Money, m_AxeName, m_AxeDamage, m_Prompt, m_PromptDetail, m_TreeName, m_TreeHp, m_TreeNote;
        RectTransform m_MoneyRect, m_TreeBar, m_CourierBar;
        double m_LastMoney = -1;
        float m_MoneyPunch;
        readonly List<ResourceRow> m_Rows = new List<ResourceRow>();
        Image m_Crosshair, m_TreeIcon;
        readonly Image[] m_HitBars = new Image[4];
        GameObject m_TreePanel, m_PromptPanel, m_Courier;
        CanvasGroup m_Saved, m_Controls;
        float m_HitFlash, m_SavedTimer;
        Color m_HitColor = Color.white;
        string m_LastAxe;

        // backpack
        TextMeshProUGUI m_PackName, m_PackValue, m_PackFull;
        RectTransform m_PackBar, m_PackRect;
        Image m_PackBarImage, m_PackIcon;
        string m_LastPack;
        float m_PackPunch, m_PackShake;
        GameObject m_YardCaption;
        TextMeshProUGUI m_RebirthBadge;
        RectTransform m_UseCap;
        readonly List<(RectTransform cap, string action)> m_HintCaps = new List<(RectTransform, string)>();
        int m_LastRebirths;

        GameObject m_FpsRoot;
        TextMeshProUGUI m_Fps, m_FpsDetail;
        float m_FpsRefresh;

        public void Bind(AxeController axe, Interactor interactor)
        {
            m_Axe = axe;
            m_Interactor = interactor;
            if (m_Axe != null) m_Axe.Hit += OnHit;
        }

        protected override void Build()
        {
            var game = Game.Instance;
            BuildResources(game);
            BuildCrosshair();
            BuildTreeCard();
            BuildPrompt();
            BuildFps();
            BuildFooter();
            game.Saved += () => m_SavedTimer = 2.2f;
            game.Model.BackpackFilled += () =>
            {
                m_PackShake = 1f;
                AudioManager.Ui("error", 0.35f);
                Toasts.Show("Backpack full! Sell your wood at the Lumber Market.", Toasts.Kind.Warning);
            };
        }

        void BuildResources(Game game)
        {
            var col = UIFactory.Rect("Resources", Root);
            col.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -24), new Vector2(360, 0));
            UIFactory.VStack(col, 8f);
            UIFactory.Fit(col, false, true);

            var money = UIFactory.Surface(col, "Money", UIStyle.Hud);
            UIFactory.HStack(money, 12f, new RectOffset(14, 20, 10, 10));
            var coinBg = UIFactory.Panel(money.transform, "CoinBg", new Color(1f, 0.83f, 0.4f, 0.16f));
            coinBg.sprite = UIStyle.Circle;
            coinBg.Layout(46, 46);
            var coin = UIFactory.Icon(coinBg.transform, "coin", UIStyle.Money, 30f);
            coin.rectTransform.anchoredPosition = Vector2.zero;
            coin.GetComponent<LayoutElement>().ignoreLayout = true;
            m_Money = UIFactory.Text(money.transform, "$0", 40, UIStyle.Money, true);
            m_Money.textWrappingMode = TextWrappingModes.NoWrap;
            m_Money.Layout(-1, -1, 1);
            m_MoneyRect = m_Money.rectTransform;
            // Rebirth earnings multiplier, shown once the player has reborn.
            m_RebirthBadge = UIFactory.Pill(money.transform, "", new Color(1f, 0.83f, 0.4f, 0.16f), UIStyle.Money, 17f);
            m_RebirthBadge.transform.parent.gameObject.SetActive(false);

            BuildBackpack(col);

            var yardCaption = UIFactory.Caption(col, "Lumber yard", UIStyle.Muted, 13f);
            yardCaption.Layout(-1, 18);
            yardCaption.margin = new Vector4(6, 4, 0, 0);
            m_YardCaption = yardCaption.gameObject;

            foreach (var r in game.Db.resources)
            {
                if (r == null) continue;
                var chip = UIFactory.Surface(col, r.id, UIStyle.Hud);
                UIFactory.HStack(chip, 10f, new RectOffset(16, 12, 7, 7));
                UIFactory.Icon(chip.transform, r.id == "planks" ? "plank" : "log", r.color, 26f);
                var label = UIFactory.Text(chip.transform, r.displayName, 19, UIStyle.Muted);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.Layout(70);
                var value = UIFactory.Text(chip.transform, "0", 26, UIStyle.Text, true);
                value.textWrappingMode = TextWrappingModes.NoWrap;
                value.Layout(1, -1, 1);
                var rate = UIFactory.Pill(chip.transform, "", new Color(0.52f, 0.8f, 0.36f, 0.18f), UIStyle.Good, 16f);
                m_Rows.Add(new ResourceRow { def = r, go = chip.gameObject, value = value, rate = rate, ratePill = rate.transform.parent.GetComponent<Image>() });
            }

            var axe = UIFactory.Surface(col, "Axe", UIStyle.Hud);
            UIFactory.HStack(axe, 10f, new RectOffset(16, 12, 7, 7));
            UIFactory.Icon(axe.transform, "axe", UIStyle.CategoryColor(UpgradeCategory.Axe), 24f);
            m_AxeName = UIFactory.Text(axe.transform, "", 19, UIStyle.Text, true);
            m_AxeName.textWrappingMode = TextWrappingModes.NoWrap;
            m_AxeName.Layout(-1, -1, 1);
            m_AxeDamage = UIFactory.Pill(axe.transform, "", UIStyle.Inset, UIStyle.Muted, 15f);

            var courier = UIFactory.Surface(col, "Courier", UIStyle.Hud);
            UIFactory.HStack(courier, 10f, new RectOffset(16, 16, 9, 9));
            UIFactory.Icon(courier.transform, "cart", UIStyle.CategoryColor(UpgradeCategory.Economy), 22f);
            UIFactory.Text(courier.transform, "Courier", 17, UIStyle.Muted).Layout(70);
            m_CourierBar = UIFactory.Bar(courier.transform, 8, UIStyle.CategoryColor(UpgradeCategory.Economy));
            m_Courier = courier.gameObject;
        }

        void BuildBackpack(RectTransform col)
        {
            var pack = UIFactory.Surface(col, "Backpack", UIStyle.Hud);
            UIFactory.VStack(pack, 6f, new RectOffset(16, 16, 9, 11));
            m_PackRect = pack.rectTransform;
            var row = UIFactory.Rect("Row", pack.transform);
            UIFactory.HStack(row, 10f);
            m_PackIcon = UIFactory.Icon(row, "backpack", UIStyle.CategoryColor(UpgradeCategory.Axe), 26f);
            m_PackName = UIFactory.Text(row, "", 17, UIStyle.Muted);
            m_PackName.textWrappingMode = TextWrappingModes.NoWrap;
            m_PackName.overflowMode = TextOverflowModes.Ellipsis;
            m_PackName.Layout(-1, -1, 1);
            m_PackValue = UIFactory.Text(row, "", 24, UIStyle.Text, true, TextAlignmentOptions.Right);
            m_PackValue.textWrappingMode = TextWrappingModes.NoWrap;
            m_PackBar = UIFactory.Bar(pack.transform, 7, UIStyle.Accent);
            m_PackBarImage = m_PackBar.GetComponent<Image>();
            m_PackFull = UIFactory.Text(pack.transform, "Full - sell at the Lumber Market", 15, UIStyle.Bad, true);
            m_PackFull.textWrappingMode = TextWrappingModes.NoWrap;
            m_PackFull.gameObject.SetActive(false);
        }

        void UpdateBackpack(GameModel model, float dt)
        {
            double carried = model.State.CarriedTotal, cap = model.BackpackCapacity;
            string v = $"{NumberUtil.Format(Math.Floor(carried))}<size=70%><color=#B5AE9C> / {NumberUtil.Format(cap)}</color></size>";
            if (v != m_LastPack)
            {
                if (m_LastPack != null && carried > 0) m_PackPunch = 1f;
                m_LastPack = v;
                m_PackValue.text = v;
                m_PackName.text = model.Upgrades.BackpackName;
            }
            bool full = model.BackpackFull;
            float fill = model.BackpackFill;
            UIFactory.SetBar(m_PackBar, fill);
            Color bar = full ? UIStyle.Bad : fill > 0.8f ? Color.Lerp(UIStyle.Accent, UIStyle.Bad, 0.45f) : UIStyle.Accent;
            m_PackBarImage.color = bar;
            // A log was left behind because the pack is full: shake the chip and show the hint.
            if (full && Time.unscaledTime - WoodPickup.LastRefusedTime < 0.2f && m_PackShake < 0.3f) m_PackShake = 0.6f;
            if (m_PackFull.gameObject.activeSelf != full) m_PackFull.gameObject.SetActive(full);
            m_PackPunch = Mathf.MoveTowards(m_PackPunch, 0f, dt * 5f);
            m_PackShake = Mathf.MoveTowards(m_PackShake, 0f, dt * 2.5f);
            m_PackValue.color = full ? UIStyle.Bad : Color.Lerp(UIStyle.Text, UIStyle.Accent, m_PackPunch);
            m_PackIcon.color = full ? UIStyle.Bad : UIStyle.CategoryColor(UpgradeCategory.Axe);
            // Wobble (rotation, which the layout group leaves alone).
            m_PackRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 50f) * 2.5f * m_PackShake);
        }

        void BuildCrosshair()
        {
            m_Crosshair = UIFactory.Panel(Root, "Crosshair", Color.white, false);
            m_Crosshair.sprite = UIStyle.Circle;
            m_Crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));
            var outline = m_Crosshair.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.45f);
            outline.effectDistance = new Vector2(1, -1);
            // Hit marker: four short diagonal bars that spring out from the centre.
            for (int i = 0; i < 4; i++)
            {
                var bar = UIFactory.Panel(Root, "Hit" + i, Color.clear);
                bar.sprite = UIStyle.RoundedSmall;
                float a = 45f + i * 90f;
                bar.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 3.5f));
                bar.rectTransform.localRotation = Quaternion.Euler(0, 0, a);
                m_HitBars[i] = bar;
            }
        }

        void BuildTreeCard()
        {
            var tree = UIFactory.Surface(Root, "Tree", UIStyle.Hud);
            tree.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(0, 44), new Vector2(330, 0));
            UIFactory.VStack(tree, 6f, new RectOffset(16, 16, 10, 12), TextAnchor.UpperCenter);
            UIFactory.Fit(tree, false, true);
            var row = UIFactory.Rect("Row", tree.transform);
            UIFactory.HStack(row, 8f);
            m_TreeIcon = UIFactory.Icon(row, "pine", UIStyle.Good, 22f);
            m_TreeName = UIFactory.Text(row, "", 21, UIStyle.Text, true);
            m_TreeName.textWrappingMode = TextWrappingModes.NoWrap;
            m_TreeName.Layout(-1, -1, 1);
            m_TreeHp = UIFactory.Text(row, "", 17, UIStyle.Muted, false, TextAlignmentOptions.Right);
            m_TreeHp.textWrappingMode = TextWrappingModes.NoWrap;
            m_TreeBar = UIFactory.Bar(tree.transform, 8, UIStyle.Good);
            m_TreeNote = UIFactory.Text(tree.transform, "", 17, UIStyle.Bad, true, TextAlignmentOptions.Center);
            m_TreePanel = tree.gameObject;
        }

        void BuildPrompt()
        {
            var prompt = UIFactory.Surface(Root, "Prompt", UIStyle.Hud);
            prompt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0, -58), Vector2.zero);
            UIFactory.VStack(prompt, 4f, new RectOffset(14, 20, 9, 11), TextAnchor.UpperCenter, false);
            UIFactory.Fit(prompt, true, true);
            var row = UIFactory.Rect("Row", prompt.transform);
            UIFactory.HStack(row, 10f, null, TextAnchor.MiddleCenter);
            m_UseCap = UIFactory.KeyCap(row, GameInput.Key("use"), 30f);
            m_Prompt = UIFactory.Text(row, "", 23, UIStyle.Text, true);
            m_Prompt.textWrappingMode = TextWrappingModes.NoWrap;
            m_PromptDetail = UIFactory.Text(prompt.transform, "", 18, UIStyle.Muted, false, TextAlignmentOptions.Center);
            m_PromptDetail.textWrappingMode = TextWrappingModes.NoWrap;
            m_PromptPanel = prompt.gameObject;
        }

        void BuildFps()
        {
            var fps = UIFactory.Surface(Root, "Fps", UIStyle.Hud);
            fps.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -20), Vector2.zero);
            UIFactory.VStack(fps, 2f, new RectOffset(14, 14, 8, 9), TextAnchor.UpperRight, false);
            UIFactory.Fit(fps, true, true);
            m_Fps = UIFactory.Text(fps.transform, "", 24, UIStyle.Good, true, TextAlignmentOptions.Right);
            m_Fps.textWrappingMode = TextWrappingModes.NoWrap;
            m_FpsDetail = UIFactory.Text(fps.transform, "", 15, UIStyle.Muted, false, TextAlignmentOptions.Right);
            m_FpsDetail.textWrappingMode = TextWrappingModes.NoWrap;
            m_FpsRoot = fps.gameObject;
        }

        void BuildFooter()
        {
            var controls = UIFactory.Rect("Controls", Root);
            controls.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 20), new Vector2(0, 30));
            UIFactory.HStack(controls, 8f);
            UIFactory.Fit(controls, true, false);
            void Hint(string key, string what, bool last = false)
            {
                m_HintCaps.Add((UIFactory.KeyCap(controls, GameInput.Key(key), 26f), key));
                var t = UIFactory.Text(controls, what, 16, new Color(1, 1, 1, 0.8f));
                t.textWrappingMode = TextWrappingModes.NoWrap;
                if (!last) UIFactory.Rect("Gap", controls).Layout(10, 26);
            }
            Hint("chop", "Chop");
            Hint("use", "Use");
            Hint("sprint", "Sprint");
            Hint("jump", "Jump");
            Hint("menu", "Menu", true);
            GameInput.DeviceChanged += RelabelKeys;
            m_Controls = controls.gameObject.AddComponent<CanvasGroup>();

            var saved = UIFactory.Surface(Root, "Saved", UIStyle.Hud);
            saved.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 20), Vector2.zero);
            UIFactory.HStack(saved, 8f, new RectOffset(12, 14, 6, 7));
            UIFactory.Fit(saved, true, true);
            UIFactory.Icon(saved.transform, "save", UIStyle.Good, 18f);
            UIFactory.Text(saved.transform, "Progress saved", 16, UIStyle.Muted).textWrappingMode = TextWrappingModes.NoWrap;
            m_Saved = saved.gameObject.AddComponent<CanvasGroup>();
            m_Saved.alpha = 0f;
        }

        /// <summary>Key caps show keyboard keys or controller buttons, whichever was used last.</summary>
        void RelabelKeys()
        {
            if (m_UseCap != null) UIFactory.SetKeyCap(m_UseCap, GameInput.Key("use"), GameInput.Glyph("use"));
            foreach (var (cap, action) in m_HintCaps) if (cap != null) UIFactory.SetKeyCap(cap, GameInput.Key(action), GameInput.Glyph(action));
        }

        void OnDestroy() => GameInput.DeviceChanged -= RelabelKeys;

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

            if (GameInput.FpsTogglePressed)
            {
                var s = GameSettings.Data;
                s.fpsCounter = (FpsCounterMode)(((int)s.fpsCounter + 1) % 3);
                GameSettings.Save();
            }

            double money = model.State.Money;
            if (money != m_LastMoney)
            {
                if (m_LastMoney >= 0 && money > m_LastMoney) m_MoneyPunch = 1f;
                m_LastMoney = money;
                m_Money.text = NumberUtil.Money(money);
            }
            m_MoneyPunch = Mathf.MoveTowards(m_MoneyPunch, 0f, dt * 4f);
            m_MoneyRect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(m_MoneyPunch * Mathf.PI));

            UpdateBackpack(model, dt);
            if (model.State.Rebirths != m_LastRebirths)
            {
                m_LastRebirths = model.State.Rebirths;
                m_RebirthBadge.transform.parent.gameObject.SetActive(m_LastRebirths > 0);
                m_RebirthBadge.text = $"x{model.Market.RebirthMultiplier:0.0#}";
            }

            // Lumber yard: machine output, shown once there is any.
            bool anyYard = false;
            foreach (var row in m_Rows)
            {
                double amount = model.State.Get(row.def.id);
                double net = model.Production.NetRate(row.def.id);
                double gross = model.Production.GrossRate(row.def.id);
                bool show = amount >= 1 || gross > 0;
                if (row.go.activeSelf != show) row.go.SetActive(show);
                anyYard |= show;
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
                    row.ratePill.gameObject.SetActive(rate.Length > 0);
                    Color c = net >= 0 ? UIStyle.Good : UIStyle.Bad;
                    row.rate.color = c;
                    row.ratePill.color = new Color(c.r, c.g, c.b, 0.18f);
                }
                row.punch = Mathf.MoveTowards(row.punch, 0f, dt * 5f);
                row.value.color = Color.Lerp(UIStyle.Text, UIStyle.Accent, row.punch);
            }

            if (m_YardCaption.activeSelf != anyYard) m_YardCaption.SetActive(anyYard);

            string axe = model.Upgrades.AxeName;
            if (axe != m_LastAxe)
            {
                m_LastAxe = axe;
                m_AxeName.text = axe;
                m_AxeDamage.text = NumberUtil.Format(model.Upgrades.AxeDamage, true) + " dmg";
            }
            if (Time.frameCount % 15 == 0) m_AxeDamage.text = NumberUtil.Format(model.Upgrades.AxeDamage, true) + " dmg";

            bool courier = model.Upgrades.AutoSellSeconds > 0;
            if (m_Courier.activeSelf != courier) m_Courier.SetActive(courier);
            if (courier) UIFactory.SetBar(m_CourierBar, model.AutoSellProgress);

            UpdateTarget(model, dt);
            UpdateFps(dt);

            m_SavedTimer -= dt;
            m_Saved.alpha = Mathf.Clamp01(m_SavedTimer);
            m_Controls.alpha = model.State.TutorialDone ? 0.65f : 0.95f;
        }

        void UpdateTarget(GameModel model, float dt)
        {
            var tree = m_Axe != null ? m_Axe.AimedTree : null;
            var inter = m_Interactor != null ? m_Interactor.Current : null;
            bool aiming = tree != null || inter != null;
            float size = Mathf.Lerp(m_Crosshair.rectTransform.sizeDelta.x, aiming ? 11f : 7f, 1f - Mathf.Exp(-20f * dt));
            m_Crosshair.rectTransform.sizeDelta = Vector2.one * size;
            m_Crosshair.color = aiming ? UIStyle.Accent : new Color(1, 1, 1, 0.9f);
            m_HitFlash = Mathf.MoveTowards(m_HitFlash, 0f, dt * 4f);
            for (int i = 0; i < 4; i++)
            {
                var c = m_HitColor;
                c.a = m_HitFlash;
                m_HitBars[i].color = c;
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                m_HitBars[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (12f + 8f * (1f - m_HitFlash));
            }

            if (tree != null)
            {
                if (!m_TreePanel.activeSelf) m_TreePanel.SetActive(true);
                m_TreeName.text = tree.Type.displayName;
                m_TreeHp.text = $"{NumberUtil.Format(tree.Health, true)} / {NumberUtil.Format(tree.MaxHealth)}";
                float f = (float)(tree.Health / tree.MaxHealth);
                UIFactory.SetBar(m_TreeBar, f);
                m_TreeBar.GetComponent<Image>().color = Color.Lerp(UIStyle.Bad, UIStyle.Good, f);
                m_TreeIcon.color = tree.Type.leafColor;
                int need = tree.Type.requiredAxeTier;
                bool tough = model.Upgrades.AxeTier < need;
                if (tough)
                {
                    var tierUp = model.Upgrades.FirstWithEffect(UpgradeEffect.AxeTier);
                    m_TreeNote.text = "Too tough! Needs the " + (tierUp != null ? tierUp.LevelName(need) : "a better axe");
                }
                if (m_TreeNote.gameObject.activeSelf != tough) m_TreeNote.gameObject.SetActive(tough);
            }
            else if (m_TreePanel.activeSelf) m_TreePanel.SetActive(false);

            if (inter != null && tree == null)
            {
                if (!m_PromptPanel.activeSelf) m_PromptPanel.SetActive(true);
                m_Prompt.text = inter.Prompt;
                string d = inter.Detail;
                bool hasDetail = !string.IsNullOrEmpty(d);
                if (m_PromptDetail.gameObject.activeSelf != hasDetail) m_PromptDetail.gameObject.SetActive(hasDetail);
                if (hasDetail) m_PromptDetail.text = d;
            }
            else if (m_PromptPanel.activeSelf) m_PromptPanel.SetActive(false);
        }

        void UpdateFps(float dt)
        {
            var mode = GameSettings.Data.fpsCounter;
            bool show = mode != FpsCounterMode.Off && PerfStats.Instance != null;
            if (m_FpsRoot.activeSelf != show) m_FpsRoot.SetActive(show);
            if (!show) return;
            m_FpsRefresh -= dt;
            if (m_FpsRefresh > 0f) return;
            m_FpsRefresh = 0.25f;
            var p = PerfStats.Instance;
            m_Fps.text = $"{Mathf.RoundToInt(p.Fps)} <size=60%>FPS</size>";
            m_Fps.color = p.Fps >= 55f ? UIStyle.Good : p.Fps >= 30f ? UIStyle.Accent : UIStyle.Bad;
            bool detailed = mode == FpsCounterMode.Detailed;
            if (m_FpsDetail.gameObject.activeSelf != detailed) m_FpsDetail.gameObject.SetActive(detailed);
            if (detailed)
            {
                string cpu = p.HasCpuTime ? $"CPU {p.CpuMs:0.0}" : "CPU -";
                string gpu = p.HasGpuTime ? $"GPU {p.GpuMs:0.0}" : "GPU -";
                m_FpsDetail.text = $"{p.FrameMs:0.0} ms  (worst {p.WorstFrameMs:0})\n{cpu}  {gpu} ms\n{GraphicsService.Describe()}  {Screen.width}x{Screen.height}";
            }
        }
    }
}

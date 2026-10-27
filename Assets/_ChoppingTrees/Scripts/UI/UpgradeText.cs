using System.Collections.Generic;
using System.Text;

namespace ChoppingTrees
{
    /// <summary>Human-readable "current -> next" effect lines for upgrade cards.</summary>
    public static class UpgradeText
    {
        const string Arrow = " <color=#8F8A7C>-></color> ";

        public static string Effect(GameModel m, UpgradeDef u)
        {
            var up = m.Upgrades;
            int lv = up.Level(u);
            bool maxed = u.IsMaxed(lv);
            float e = u.effectPerLevel;
            switch (u.effect)
            {
                case UpgradeEffect.AxeTier:
                {
                    var cfg = m.Db.config;
                    int next = lv + 1;
                    if (maxed) return $"Damage {Dmg(m, cfg.axeTierDamage[Clamp(lv, cfg.axeTierDamage.Length)])}";
                    string trees = TreesForTier(m, next);
                    float now = cfg.axeTierDamage[Clamp(lv, cfg.axeTierDamage.Length)];
                    float then = cfg.axeTierDamage[Clamp(next, cfg.axeTierDamage.Length)];
                    return $"Base damage {Dmg(m, now)}{Arrow}<color=#FBBD52>{Dmg(m, then)}</color>" + (trees != null ? $"\nCan chop <b>{trees}</b>" : "");
                }
                case UpgradeEffect.AxeDamage:
                    return Pair("Damage", "x" + (1 + e * lv).ToString("0.00"), "x" + (1 + e * (lv + 1)).ToString("0.00"), maxed);
                case UpgradeEffect.AxeSpeed:
                    return Pair("Swing speed", "x" + (1 + e * lv).ToString("0.00"), "x" + (1 + e * (lv + 1)).ToString("0.00"), maxed);
                case UpgradeEffect.MachineCount:
                {
                    var def = m.Db.Machine(u.targetId);
                    string each = def != null ? $"\nEach makes {NumberUtil.Format(m.Production.RatePerMachine(def), true)} {Out(def)}/s" + (def.kind == MachineKind.Processor && def.input != null ? $" from {NumberUtil.Format(def.inputPerOutput)} {def.input.displayName.ToLower()}" : "") : "";
                    return Pair("Owned", lv.ToString(), (lv + 1).ToString(), maxed) + each;
                }
                case UpgradeEffect.MachineSpeed:
                    return Pair("Output", "+" + (e * lv * 100).ToString("0") + "%", "+" + (e * (lv + 1) * 100).ToString("0") + "%", maxed);
                case UpgradeEffect.MachineTier:
                {
                    string name = u.LevelName(lv + 1);
                    return maxed ? $"All machines upgraded (x{System.Math.Pow(e, lv):0} output)"
                        : $"Rebuilds every machine as <b>{name}</b>\nOutput x{System.Math.Pow(e, lv):0}{Arrow}<color=#FBBD52>x{System.Math.Pow(e, lv + 1):0}</color>";
                }
                case UpgradeEffect.ResourcePrice:
                {
                    var r = m.Db.Resource(u.targetId);
                    double baseP = r != null ? r.basePrice : 1;
                    double boost = up.SellMultiplier * m.Market.RebirthMultiplier;
                    double now = baseP * up.PriceMultiplier(u.targetId) * boost;
                    double next = now + baseP * e * boost;
                    return Pair((r != null ? r.displayName : "Price") + " price", NumberUtil.Money(now), NumberUtil.Money(next), maxed);
                }
                case UpgradeEffect.AutoSell:
                    return lv > 0 ? $"Selling the yard stock every {e:0}s" : $"Sells everything in the yard every {e:0}s, wherever you are\n<color=#8F8A7C>Your backpack is still sold by hand</color>";
                case UpgradeEffect.MagnetRange:
                {
                    float now = up.MagnetRadius;
                    return Pair("Pickup range", now.ToString("0.#") + " m", (now + e).ToString("0.#") + " m", maxed) + "\n<color=#8F8A7C>Logs fly to you from this far (when your backpack has room)</color>";
                }
                case UpgradeEffect.WoodYield:
                    return Pair("Wood per tree", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.CritChance:
                    return Pair("Double-damage chance", Pct(e * lv, false), Pct(e * (lv + 1), false), maxed);
                case UpgradeEffect.MoveSpeed:
                    return Pair("Move speed", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.ProcessorYield:
                {
                    var def = m.Db.Machine(u.targetId);
                    return Pair((def != null ? Out(def) : "Output") + " per cut", Pct(e * lv), Pct(e * (lv + 1)), maxed) + "\n<color=#8F8A7C>Same wood in, more out</color>";
                }
                case UpgradeEffect.MachineDiscount:
                    return Pair("Machine prices", "-" + Pct(e * lv, false), "-" + Pct(e * (lv + 1), false), maxed);
                case UpgradeEffect.SellBonus:
                    return Pair("All sale prices", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.LuckyFind:
                    return Pair("Lucky find chance", Pct(e * lv, false), Pct(e * (lv + 1), false), maxed) + $"\n<color=#8F8A7C>A find pays {m.Db.config.luckyFindValue:0}x the tree's wood value</color>";
                case UpgradeEffect.TreeRegrowth:
                    return Pair("Regrowth speed", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.TreeCashBonus:
                    return Pair("Goldbark cash", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.StartingAxe:
                {
                    var axe = up.FirstWithEffect(UpgradeEffect.AxeTier);
                    string now = lv > 0 && axe != null ? axe.LevelName(lv) : m.Db.config.baseAxeName;
                    string next = axe != null ? axe.LevelName(lv + 1) : "";
                    return maxed ? $"Every run starts with the <b>{now}</b>" : $"Every run starts with{Arrow}<color=#FBBD52>{next}</color>";
                }
                case UpgradeEffect.StartingMoney:
                    return Pair("Starting money", NumberUtil.Money(Upgrades.StartingMoneyFor(e, lv)), NumberUtil.Money(Upgrades.StartingMoneyFor(e, lv + 1)), maxed);
                case UpgradeEffect.StartingMachines:
                {
                    var def = m.Db.Machine(u.targetId);
                    return Pair("Free " + (def != null ? def.displayName + "s" : "machines") + " each run", lv.ToString(), (lv + 1).ToString(), maxed);
                }
                case UpgradeEffect.StartingAreas:
                {
                    var areas = new List<ForestAreaDef>();
                    foreach (var a in m.Db.areas) if (a != null && !a.unlockedByDefault) areas.Add(a);
                    areas.Sort((a, b) => a.sortOrder.CompareTo(b.sortOrder));
                    var names = areas.ConvertAll(a => a.displayName);
                    string Opened(int n) => n <= 0 ? "none" : string.Join(" + ", names.GetRange(0, System.Math.Min(n, names.Count)));
                    return maxed ? $"Every run starts with <b>{Opened(lv)}</b> open" : $"Open from the start: {Opened(lv)}{Arrow}<color=#FBBD52>{Opened(lv + 1)}</color>";
                }
                case UpgradeEffect.BackpackBonus:
                    return Pair("Backpack capacity", Pct(e * lv), Pct(e * (lv + 1)), maxed);
                case UpgradeEffect.BackpackCapacity:
                {
                    var cfg = m.Db.config;
                    double bonus = up.BackpackMultiplier;
                    string now = NumberUtil.Format(Upgrades.CapacityForTier(cfg, lv) * bonus);
                    if (maxed) return $"Carries {now} wood";
                    return Pair("Carries", now, NumberUtil.Format(Upgrades.CapacityForTier(cfg, lv + 1) * bonus), false) + " wood";
                }
                case UpgradeEffect.OfflineHours:
                {
                    float now = up.OfflineCapHours;
                    return Pair("Offline limit", now.ToString("0") + "h", (now + e).ToString("0") + "h", maxed);
                }
                case UpgradeEffect.ForestArea:
                {
                    var area = FindArea(m, u.targetId);
                    if (lv > 0) return "Unlocked";
                    return area != null ? $"New trees: <b>{TreeList(area)}</b>" : "";
                }
                case UpgradeEffect.ProductionBonus:
                    return Pair("All machines", "+" + (e * lv * 100).ToString("0") + "%", "+" + (e * (lv + 1) * 100).ToString("0") + "%", maxed);
            }
            return "";
        }

        /// <summary>0.15 -> "+15%" (or "15%" without the sign).</summary>
        static string Pct(float v, bool sign = true) => (sign ? "+" : "") + (v * 100f).ToString("0") + "%";

        static string Out(MachineDef d) => d.output != null ? d.output.displayName.ToLower() : "";
        static int Clamp(int i, int len) => i < 0 ? 0 : i >= len ? len - 1 : i;

        static string Dmg(GameModel m, float tierDamage)
        {
            return NumberUtil.Format(tierDamage, true);
        }

        static string Pair(string label, string now, string next, bool maxed) =>
            maxed ? $"{label} {now}" : $"{label} {now}{Arrow}<color=#FBBD52>{next}</color>";

        static string TreesForTier(GameModel m, int tier)
        {
            var names = new List<string>();
            foreach (var t in m.Db.trees) if (t != null && t.requiredAxeTier == tier) names.Add(t.displayName);
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        static ForestAreaDef FindArea(GameModel m, string id)
        {
            foreach (var a in m.Db.areas) if (a != null && a.id == id) return a;
            return null;
        }

        static string TreeList(ForestAreaDef a)
        {
            var sb = new StringBuilder();
            foreach (var t in a.trees)
            {
                if (t == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(t.displayName);
            }
            return sb.ToString();
        }
    }
}

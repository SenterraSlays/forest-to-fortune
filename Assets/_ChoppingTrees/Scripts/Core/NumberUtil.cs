using System;
using System.Globalization;

namespace ChoppingTrees
{
    /// <summary>Overflow-safe arithmetic and compact number formatting for incremental values.</summary>
    public static class NumberUtil
    {
        /// <summary>Largest value the economy allows; keeps totals finite and serialisable.</summary>
        public const double Max = 1e300;

        static readonly string[] s_Suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>Replaces NaN with 0 and clamps to [-Max, Max].</summary>
        public static double Safe(double v)
        {
            if (double.IsNaN(v)) return 0;
            if (v > Max) return Max;
            if (v < -Max) return -Max;
            return v;
        }

        /// <summary>Safe, never negative.</summary>
        public static double SafePositive(double v) => Math.Max(0, Safe(v));

        public static double Add(double a, double b) => Safe(a + b);

        /// <summary>1234 -> "1,234"; 1.5e6 -> "1.50M"; huge values use scientific notation.</summary>
        public static string Format(double v, bool decimalsBelowThousand = false)
        {
            v = Safe(v);
            if (v < 0) return "-" + Format(-v, decimalsBelowThousand);
            if (v < 1000)
            {
                if (decimalsBelowThousand && v < 100 && Math.Abs(v - Math.Round(v)) > 0.001)
                    return v.ToString(v < 10 ? "0.0#" : "0.0", s_Inv);
                return Math.Floor(v).ToString("0", s_Inv);
            }
            if (v < 100000) return Math.Floor(v).ToString("#,0", s_Inv);
            int group = (int)Math.Floor(Math.Log10(v) / 3);
            if (group < s_Suffixes.Length)
            {
                double scaled = v / Math.Pow(1000, group);
                // Truncate rather than round so "999.99K" never shows as "1000K".
                string num = scaled >= 100 ? (Math.Floor(scaled * 10) / 10).ToString("0.0", s_Inv)
                    : (Math.Floor(scaled * 100) / 100).ToString("0.00", s_Inv);
                return num + s_Suffixes[group];
            }
            return v.ToString("0.00e0", s_Inv);
        }

        /// <summary>"$12", "$2.50", "$1.50M": cents only for small amounts that have them.</summary>
        public static string Money(double v)
        {
            v = Safe(v);
            if (v >= 0 && v < 1000 && Math.Abs(v - Math.Round(v)) > 0.005) return "$" + v.ToString("0.00", s_Inv);
            return (v < 0 ? "-$" : "$") + Format(Math.Abs(v));
        }

        /// <summary>Rates like "+3.20/s"; tiny rates keep two decimals.</summary>
        public static string Rate(double perSecond)
        {
            perSecond = Safe(perSecond);
            string sign = perSecond < 0 ? "-" : "+";
            double a = Math.Abs(perSecond);
            string s = a < 1000 ? a.ToString(a < 10 ? "0.00" : "0.0", s_Inv) : Format(a);
            return sign + s + "/s";
        }

        public static string Duration(double seconds)
        {
            seconds = Math.Max(0, Safe(seconds));
            var t = TimeSpan.FromSeconds(Math.Min(seconds, TimeSpan.MaxValue.TotalSeconds - 1));
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes}m";
            if (t.TotalMinutes >= 1) return $"{t.Minutes}m {t.Seconds}s";
            return $"{t.Seconds}s";
        }
    }
}

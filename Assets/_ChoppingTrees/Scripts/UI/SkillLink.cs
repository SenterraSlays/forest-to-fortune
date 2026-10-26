using UnityEngine;
using UnityEngine.UI;

namespace ChoppingTrees
{
    /// <summary>
    /// A curved connection in the skill tree, drawn as one mesh: an S-curve that leaves the parent
    /// upwards and enters the child from below. Three looks: <see cref="Style.Owned"/> (thicker,
    /// with a soft glow underneath), <see cref="Style.Available"/> (thin and solid) and
    /// <see cref="Style.Locked"/> (thin and dashed), so the state doesn't depend on colour alone.
    /// <see cref="Flash"/> brightens the whole link (purchase feedback). Points are in the parent
    /// rect's space with the origin at its bottom-left corner.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class SkillLink : MaskableGraphic
    {
        public enum Style { Locked, Available, Owned }

        const int Segments = 32;
        const float Dash = 7f, Gap = 6f;

        readonly Vector2[] m_Points = new Vector2[Segments + 1];
        readonly float[] m_Dist = new float[Segments + 1];
        Style m_Style = Style.Locked;
        float m_Flash;

        public UpgradeDef From { get; set; }
        public UpgradeDef To { get; set; }
        public int RequiredLevel { get; set; }
        public float Length => m_Dist[Segments];
        public Style Current => m_Style;

        public static SkillLink Create(RectTransform parent)
        {
            var go = new GameObject("Link", typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = Vector2.zero;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var l = go.AddComponent<SkillLink>();
            l.raycastTarget = false;
            return l;
        }

        /// <summary>Sets the end points (parent node centre, child node centre).</summary>
        public void SetEnds(Vector2 a, Vector2 b)
        {
            float dy = Mathf.Max(30f, Mathf.Abs(b.y - a.y)) * 0.55f;
            Vector2 c1 = a + new Vector2(0f, dy), c2 = b - new Vector2(0f, dy);
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments, u = 1f - t;
                m_Points[i] = u * u * u * a + 3f * u * u * t * c1 + 3f * u * t * t * c2 + t * t * t * b;
                m_Dist[i] = i == 0 ? 0f : m_Dist[i - 1] + Vector2.Distance(m_Points[i - 1], m_Points[i]);
            }
            SetVerticesDirty();
        }

        /// <summary>Point at a fraction (0..1) of the link's length.</summary>
        public Vector2 PointAt(float f)
        {
            float d = Mathf.Clamp01(f) * Length;
            for (int i = 1; i <= Segments; i++)
            {
                if (m_Dist[i] < d) continue;
                float seg = m_Dist[i] - m_Dist[i - 1];
                return Vector2.Lerp(m_Points[i - 1], m_Points[i], seg > 1e-4f ? (d - m_Dist[i - 1]) / seg : 0f);
            }
            return m_Points[Segments];
        }

        public void SetStyle(Style s, Color c)
        {
            if (s != m_Style)
            {
                m_Style = s;
                SetVerticesDirty();
            }
            color = c;
        }

        /// <summary>0..1 brightness boost, faded out by the owner.</summary>
        public float Flash
        {
            get => m_Flash;
            set
            {
                value = Mathf.Clamp01(value);
                if (Mathf.Abs(value - m_Flash) < 0.004f) return;
                m_Flash = value;
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Length <= 0f) return;
            Color32 core = Color.Lerp(color, Color.white, m_Flash * 0.55f);
            switch (m_Style)
            {
                case Style.Owned:
                {
                    var glow = color;
                    glow.a *= 0.16f + 0.3f * m_Flash;
                    Strip(vh, 11f + 6f * m_Flash, glow, false);
                    Strip(vh, 3.6f + 1.2f * m_Flash, core, false);
                    // Bright centre line gives the path a polished, lit look.
                    var hi = Color.Lerp(color, Color.white, 0.5f);
                    hi.a = color.a * 0.55f;
                    Strip(vh, 1.1f, hi, false);
                    break;
                }
                case Style.Available:
                    if (m_Flash > 0f)
                    {
                        var g = color;
                        g.a *= 0.3f * m_Flash;
                        Strip(vh, 10f, g, false);
                    }
                    Strip(vh, 2.4f, core, false);
                    break;
                default:
                    Strip(vh, 2f, core, true);
                    break;
            }
        }

        /// <summary>Quad strip along the curve; dashed strips emit only the "on" parts of the pattern.</summary>
        void Strip(VertexHelper vh, float width, Color32 c, bool dashed)
        {
            float h = width * 0.5f;
            for (int i = 0; i < Segments; i++)
            {
                Vector2 p0 = m_Points[i], p1 = m_Points[i + 1];
                Vector2 dir = p1 - p0;
                float len = dir.magnitude;
                if (len < 1e-4f) continue;
                dir /= len;
                if (!dashed)
                {
                    // Normals averaged with the neighbours so consecutive quads meet without gaps.
                    Vector2 n0 = Normal(i), n1 = Normal(i + 1);
                    Quad(vh, p0 - n0 * h, p0 + n0 * h, p1 + n1 * h, p1 - n1 * h, c);
                    continue;
                }
                Vector2 n = new Vector2(-dir.y, dir.x) * h;
                float s0 = m_Dist[i], s1 = m_Dist[i + 1];
                float period = Dash + Gap;
                float start = Mathf.Floor(s0 / period) * period;
                for (float d = start; d < s1; d += period)
                {
                    float a = Mathf.Max(d, s0), b = Mathf.Min(d + Dash, s1);
                    if (b <= a) continue;
                    Vector2 pa = p0 + dir * (a - s0), pb = p0 + dir * (b - s0);
                    Quad(vh, pa - n, pa + n, pb + n, pb - n, c);
                }
            }
        }

        Vector2 Normal(int i)
        {
            Vector2 d = i == 0 ? m_Points[1] - m_Points[0]
                : i == Segments ? m_Points[Segments] - m_Points[Segments - 1]
                : m_Points[i + 1] - m_Points[i - 1];
            d.Normalize();
            return new Vector2(-d.y, d.x);
        }

        static void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color32 col)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, col, Vector2.zero);
            vh.AddVert(b, col, Vector2.zero);
            vh.AddVert(c, col, Vector2.zero);
            vh.AddVert(d, col, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i + 2, i + 3, i);
        }
    }
}

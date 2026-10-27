using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChoppingTrees
{
    /// <summary>
    /// Icons drawn in code with signed distance functions (white shapes, tinted by the Image),
    /// plus the soft shadow, outline ring and gradient sprites the interface uses. Everything is
    /// generated once and cached, so the UI needs no texture assets.
    /// </summary>
    public static class UIIcons
    {
        const int Size = 96;
        static readonly Dictionary<string, Sprite> s_Cache = new Dictionary<string, Sprite>();

        // ---------------------------------------------------------------- SDF primitives (unit square, y up, centre 0.5)

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float RingD(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w * 0.5f;

        static float Box(Vector2 p, Vector2 c, Vector2 half, float angleDeg = 0f, float round = 0f)
        {
            Vector2 d = p - c;
            if (angleDeg != 0f)
            {
                float a = -angleDeg * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                d = new Vector2(d.x * cs - d.y * sn, d.x * sn + d.y * cs);
            }
            Vector2 q = new Vector2(Mathf.Abs(d.x), Mathf.Abs(d.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - round;
        }

        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - r;
        }

        /// <summary>Convex polygon (counter-clockwise points).</summary>
        static float Poly(Vector2 p, params Vector2[] v)
        {
            float d = float.MinValue;
            for (int i = 0; i < v.Length; i++)
            {
                Vector2 a = v[i], b = v[(i + 1) % v.Length];
                Vector2 n = new Vector2(b.y - a.y, a.x - b.x).normalized; // outward for CCW
                d = Mathf.Max(d, Vector2.Dot(p - a, n));
            }
            return d;
        }

        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Cut(float a, float b) => Mathf.Max(a, -b);
        static float Inter(float a, float b) => Mathf.Max(a, b);
        static Vector2 V(float x, float y) => new Vector2(x, y);

        // ---------------------------------------------------------------- icon shapes

        static readonly Dictionary<string, Func<Vector2, float>> s_Shapes = new Dictionary<string, Func<Vector2, float>>
        {
            ["coin"] = p => Cut(Circle(p, V(.5f, .5f), .42f), Union(RingD(p, V(.5f, .5f), .3f, .05f), Box(p, V(.5f, .5f), V(.035f, .14f)))),
            ["log"] = p => Cut(Circle(p, V(.5f, .5f), .42f), Union(RingD(p, V(.5f, .5f), .28f, .045f), RingD(p, V(.5f, .5f), .14f, .045f))),
            ["plank"] = p => Cut(Union(Box(p, V(.5f, .62f), V(.4f, .09f), -12f, .03f), Box(p, V(.5f, .36f), V(.4f, .09f), -12f, .03f)),
                Box(p, V(.42f, .63f), V(.16f, .012f), -12f)),
            ["chair"] = p => Union(Union(Box(p, V(.3f, .55f), V(.06f, .37f), 0f, .02f), Box(p, V(.5f, .46f), V(.26f, .06f), 0f, .02f)),
                Box(p, V(.7f, .27f), V(.055f, .16f), 0f, .02f)),
            ["house"] = p => Cut(Union(Poly(p, V(.08f, .5f), V(.92f, .5f), V(.5f, .9f)), Box(p, V(.5f, .32f), V(.3f, .2f), 0f, .02f)),
                Box(p, V(.5f, .22f), V(.08f, .12f))),
            ["axe"] = p => Union(Capsule(p, V(.24f, .14f), V(.7f, .82f), .05f),
                Poly(p, V(.5f, .64f), V(.66f, .5f), V(.9f, .6f), V(.86f, .9f), V(.66f, .92f))),
            ["gear"] = p =>
            {
                float d = Circle(p, V(.5f, .5f), .3f);
                for (int i = 0; i < 8; i++) d = Union(d, Box(p, V(.5f, .5f) + new Vector2(Mathf.Cos(i * Mathf.PI / 4), Mathf.Sin(i * Mathf.PI / 4)) * .33f, V(.07f, .07f), i * 45f, .02f));
                return Cut(d, Circle(p, V(.5f, .5f), .12f));
            },
            ["saw"] = p =>
            {
                float d = Circle(p, V(.5f, .5f), .32f);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6;
                    Vector2 c = V(.5f, .5f) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .36f;
                    d = Union(d, Box(p, c, V(.06f, .04f), a * Mathf.Rad2Deg + 30f));
                }
                return Cut(d, Circle(p, V(.5f, .5f), .08f));
            },
            ["bolt"] = p => Union(Poly(p, V(.58f, .92f), V(.3f, .46f), V(.5f, .46f), V(.62f, .62f)), Poly(p, V(.5f, .54f), V(.38f, .38f), V(.42f, .08f), V(.7f, .54f))),
            ["gem"] = p => Poly(p, V(.5f, .1f), V(.86f, .58f), V(.7f, .8f), V(.3f, .8f), V(.14f, .58f)),
            ["pine"] = p => Union(Union(Poly(p, V(.5f, .92f), V(.26f, .58f), V(.74f, .58f)), Poly(p, V(.5f, .74f), V(.18f, .3f), V(.82f, .3f))),
                Box(p, V(.5f, .2f), V(.07f, .12f))),
            ["mountain"] = p => Union(Poly(p, V(.36f, .78f), V(.06f, .18f), V(.66f, .18f)), Poly(p, V(.68f, .62f), V(.42f, .18f), V(.94f, .18f))),
            ["crown"] = p => Union(Union(Box(p, V(.5f, .3f), V(.38f, .1f), 0f, .02f), Poly(p, V(.12f, .36f), V(.34f, .36f), V(.12f, .78f))),
                Union(Poly(p, V(.32f, .36f), V(.68f, .36f), V(.5f, .86f)), Poly(p, V(.66f, .36f), V(.88f, .36f), V(.88f, .78f)))),
            ["clock"] = p => Union(RingD(p, V(.5f, .5f), .36f, .08f), Union(Capsule(p, V(.5f, .5f), V(.5f, .74f), .045f), Capsule(p, V(.5f, .5f), V(.68f, .44f), .045f))),
            ["moon"] = p => Cut(Circle(p, V(.5f, .5f), .38f), Circle(p, V(.66f, .62f), .3f)),
            ["cart"] = p => Union(Union(Box(p, V(.48f, .58f), V(.32f, .16f), 0f, .03f), Box(p, V(.86f, .66f), V(.07f, .03f))),
                Union(Circle(p, V(.3f, .3f), .09f), Circle(p, V(.66f, .3f), .09f))),
            ["star"] = p =>
            {
                Vector2 c = V(.5f, .5f);
                float d = float.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    float a0 = Mathf.PI / 2 + i * Mathf.PI * 2 / 5, a1 = a0 + Mathf.PI / 5, a2 = a0 - Mathf.PI / 5;
                    d = Union(d, Poly(p, c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * .42f, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * .17f, c, c + new Vector2(Mathf.Cos(a2), Mathf.Sin(a2)) * .17f));
                }
                return d;
            },
            ["lock"] = p => Union(Box(p, V(.5f, .36f), V(.26f, .2f), 0f, .05f), Cut(RingD(p, V(.5f, .58f), .17f, .07f), Box(p, V(.5f, .42f), V(.3f, .16f)))),
            ["check"] = p => Union(Capsule(p, V(.2f, .52f), V(.42f, .3f), .07f), Capsule(p, V(.42f, .3f), V(.82f, .72f), .07f)),
            ["play"] = p => Poly(p, V(.3f, .16f), V(.84f, .5f), V(.3f, .84f)),
            ["power"] = p => Union(Cut(RingD(p, V(.5f, .46f), .3f, .08f), Box(p, V(.5f, .8f), V(.12f, .2f))), Capsule(p, V(.5f, .5f), V(.5f, .88f), .05f)),
            ["chevron_left"] = p => Union(Capsule(p, V(.6f, .2f), V(.32f, .5f), .07f), Capsule(p, V(.32f, .5f), V(.6f, .8f), .07f)),
            ["chevron_right"] = p => Union(Capsule(p, V(.4f, .2f), V(.68f, .5f), .07f), Capsule(p, V(.68f, .5f), V(.4f, .8f), .07f)),
            ["close"] = p => Union(Capsule(p, V(.24f, .24f), V(.76f, .76f), .07f), Capsule(p, V(.24f, .76f), V(.76f, .24f), .07f)),
            ["save"] = p => Cut(Box(p, V(.5f, .5f), V(.36f, .36f), 0f, .06f), Union(Box(p, V(.5f, .72f), V(.2f, .1f)), Circle(p, V(.5f, .38f), .1f))),
            ["refresh"] = p => Union(Cut(RingD(p, V(.5f, .5f), .3f, .08f), Poly(p, V(.5f, .5f), V(.95f, .5f), V(.95f, .95f), V(.5f, .95f))), Poly(p, V(.5f, .9f), V(.5f, .64f), V(.76f, .78f))),
            ["leaf"] = p => Union(Cut(Circle(p, V(.4f, .4f), .38f), Union(Circle(p, V(.04f, .82f), .4f), Circle(p, V(.82f, .04f), .4f))), Capsule(p, V(.14f, .14f), V(.4f, .4f), .03f)),
            ["chart"] = p => Union(Union(Box(p, V(.26f, .32f), V(.08f, .16f)), Box(p, V(.5f, .42f), V(.08f, .26f))), Box(p, V(.74f, .54f), V(.08f, .38f))),
            ["dot"] = p => Circle(p, V(.5f, .5f), .4f),
            ["backpack"] = p =>
            {
                float body = Cut(Box(p, V(.5f, .42f), V(.27f, .31f), 0f, .1f), Box(p, V(.5f, .55f), V(.3f, .018f)));
                float pocket = Cut(Box(p, V(.5f, .3f), V(.15f, .1f), 0f, .04f), Box(p, V(.5f, .3f), V(.105f, .058f), 0f, .02f));
                float handle = Cut(RingD(p, V(.5f, .76f), .11f, .06f), Box(p, V(.5f, .66f), V(.2f, .08f)));
                return Union(Cut(body, pocket), handle);
            },
            ["magnet"] = p =>
            {
                float u = Union(Cut(RingD(p, V(.5f, .46f), .24f, .16f), Box(p, V(.5f, .7f), V(.45f, .24f))),
                    Union(Box(p, V(.26f, .63f), V(.08f, .18f)), Box(p, V(.74f, .63f), V(.08f, .18f))));
                return Cut(u, Box(p, V(.5f, .7f), V(.5f, .022f)));
            },
            ["boot"] = p =>
            {
                float leg = Box(p, V(.42f, .56f), V(.15f, .3f), -6f, .04f);
                float foot = Union(Box(p, V(.5f, .22f), V(.23f, .1f), 0f, .05f), Circle(p, V(.72f, .25f), .13f));
                return Cut(Cut(Union(leg, foot), Box(p, V(.55f, .1f), V(.4f, .02f))), Box(p, V(.43f, .74f), V(.2f, .018f), -6f));
            },
            ["target"] = p => Union(RingD(p, V(.5f, .5f), .33f, .08f), Union(Circle(p, V(.5f, .5f), .1f),
                Union(Box(p, V(.5f, .9f), V(.035f, .08f)), Union(Box(p, V(.5f, .1f), V(.035f, .08f)), Union(Box(p, V(.1f, .5f), V(.08f, .035f)), Box(p, V(.9f, .5f), V(.08f, .035f))))))),
            ["wrench"] = p => Union(Capsule(p, V(.24f, .2f), V(.6f, .58f), .07f), Cut(RingD(p, V(.68f, .68f), .14f, .12f), Box(p, V(.82f, .82f), V(.07f, .12f), 45f))),
            ["nugget"] = p => Union(Poly(p, V(.16f, .34f), V(.38f, .2f), V(.7f, .24f), V(.82f, .44f), V(.64f, .64f), V(.32f, .62f)),
                Union(Box(p, V(.8f, .8f), V(.025f, .11f)), Box(p, V(.8f, .8f), V(.11f, .025f)))),
            ["tag"] = p => Cut(Poly(p, V(.12f, .5f), V(.38f, .2f), V(.86f, .2f), V(.86f, .8f), V(.38f, .8f)), Circle(p, V(.36f, .5f), .075f)),
            ["clover"] = p => Union(Union(Union(Circle(p, V(.37f, .63f), .14f), Circle(p, V(.63f, .63f), .14f)), Union(Circle(p, V(.37f, .37f), .14f), Circle(p, V(.63f, .37f), .14f))),
                Capsule(p, V(.5f, .4f), V(.66f, .08f), .035f)),
            ["sprout"] = p => Union(Capsule(p, V(.5f, .1f), V(.5f, .52f), .045f),
                Union(Inter(Circle(p, V(.46f, .48f), .22f), Circle(p, V(.24f, .72f), .22f)), Inter(Circle(p, V(.54f, .5f), .22f), Circle(p, V(.78f, .76f), .22f)))),
            ["rebirth"] = p => Union(Cut(RingD(p, V(.5f, .5f), .3f, .09f), Poly(p, V(.5f, .5f), V(.95f, .62f), V(.95f, .95f), V(.62f, .95f))),
                Union(Poly(p, V(.62f, .9f), V(.84f, .62f), V(.92f, .9f)), Poly(p, V(.4f, .56f), V(.5f, .4f), V(.6f, .56f), V(.5f, .7f)))),
            ["minus"] = p => Box(p, V(.5f, .5f), V(.32f, .075f), 0f, .04f),
            ["ring"] = p => RingD(p, V(.5f, .5f), .465f, .03f),
            ["panel"] = p => Union(Cut(Box(p, V(.45f, .45f), V(.32f, .27f), 0f, .04f), Union(Box(p, V(.45f, .52f), V(.24f, .022f)), Box(p, V(.45f, .38f), V(.24f, .022f)))),
                                   Cut(Box(p, V(.58f, .58f), V(.32f, .27f), 0f, .04f), Box(p, V(.45f, .45f), V(.37f, .32f), 0f, .06f))),
            ["sofa"] = p => Union(Union(Box(p, V(.5f, .38f), V(.34f, .09f), 0f, .03f), Box(p, V(.5f, .56f), V(.27f, .12f), 0f, .04f)),
                                  Union(Union(Box(p, V(.17f, .45f), V(.07f, .16f), 0f, .04f), Box(p, V(.83f, .45f), V(.07f, .16f), 0f, .04f)),
                                        Union(Box(p, V(.22f, .22f), V(.03f, .06f)), Box(p, V(.78f, .22f), V(.03f, .06f))))),
            ["lodge"] = p => Cut(Union(Union(Poly(p, V(.06f, .5f), V(.56f, .5f), V(.31f, .8f)), Poly(p, V(.38f, .56f), V(.94f, .56f), V(.66f, .9f))),
                                       Box(p, V(.5f, .32f), V(.4f, .2f), 0f, .02f)), Union(Box(p, V(.32f, .24f), V(.06f, .12f)), Box(p, V(.68f, .36f), V(.08f, .06f)))),
            ["trophy"] = p => Union(Union(Inter(Circle(p, V(.5f, .66f), .27f), Box(p, V(.5f, .68f), V(.3f, .2f))),
                                          Union(RingD(p, V(.24f, .72f), .1f, .05f), RingD(p, V(.76f, .72f), .1f, .05f))),
                                    Union(Box(p, V(.5f, .34f), V(.05f, .1f)), Box(p, V(.5f, .17f), V(.2f, .06f), 0f, .02f))),
            ["cloud"] = p => Union(Union(Circle(p, V(.36f, .45f), .17f), Circle(p, V(.56f, .55f), .22f)),
                Union(Circle(p, V(.74f, .44f), .15f), Box(p, V(.53f, .36f), V(.29f, .09f), 0f, .08f))),
            ["pause"] = p => Union(Box(p, V(.36f, .5f), V(.08f, .3f), 0f, .03f), Box(p, V(.64f, .5f), V(.08f, .3f), 0f, .03f)),
            ["hand"] = p => Union(Union(Box(p, V(.5f, .36f), V(.22f, .2f), 0f, .1f), Union(Capsule(p, V(.34f, .5f), V(.34f, .74f), .055f), Capsule(p, V(.46f, .52f), V(.46f, .86f), .055f))),
                Union(Union(Capsule(p, V(.58f, .52f), V(.58f, .82f), .055f), Capsule(p, V(.7f, .48f), V(.7f, .7f), .055f)), Capsule(p, V(.26f, .34f), V(.16f, .5f), .055f))),
            ["run"] = p => Union(Union(Circle(p, V(.6f, .82f), .09f), Capsule(p, V(.56f, .66f), V(.44f, .42f), .06f)),
                Union(Union(Capsule(p, V(.44f, .42f), V(.62f, .24f), .055f), Capsule(p, V(.62f, .24f), V(.6f, .08f), .05f)),
                    Union(Union(Capsule(p, V(.44f, .42f), V(.3f, .2f), .055f), Capsule(p, V(.54f, .6f), V(.74f, .52f), .05f)), Capsule(p, V(.54f, .6f), V(.34f, .58f), .05f)))),
            ["plus"] = p => Union(Box(p, V(.5f, .5f), V(.075f, .32f), 0f, .04f), Box(p, V(.5f, .5f), V(.32f, .075f), 0f, .04f)),
            ["arrow_up"] = p => Union(Capsule(p, V(.5f, .18f), V(.5f, .78f), .08f), Union(Capsule(p, V(.5f, .8f), V(.24f, .54f), .08f), Capsule(p, V(.5f, .8f), V(.76f, .54f), .08f))),
        };

        public static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (s_Cache.TryGetValue(name, out var s) && s != null) return s;
            if (!s_Shapes.TryGetValue(name, out var shape)) shape = s_Shapes["dot"];
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, true) { name = "Icon_" + name, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[Size * Size];
            float aa = 1.2f / Size;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2((x + 0.5f) / Size, (y + 0.5f) / Size);
                float d = shape(p);
                byte a = (byte)(Mathf.Clamp01(0.5f - d / aa) * 255f);
                px[y * Size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            s.name = name;
            s_Cache[name] = s;
            return s;
        }

        // ---------------------------------------------------------------- panel sprites

        static Sprite s_Shadow, s_Ring, s_GradientH, s_GradientV, s_Glow;
        static Sprite s_Bezel, s_Face, s_ThinRing, s_ArcRing, s_Vignette, s_TreeRings, s_ForestNear, s_ForestFar, s_Dash;
        static Sprite s_FocusRing, s_ThickRing;

        /// <summary>Bold rounded outline (about 4 px, 9-sliced) for the controller focus frame.</summary>
        public static Sprite FocusRing => s_FocusRing != null ? s_FocusRing : s_FocusRing = MakeSliced(64, 18, (x, y, size) =>
        {
            float d = RoundedDist(x, y, size, 1f, 16f);
            return Mathf.Clamp01(2.4f - Mathf.Abs(d + 2.4f));
        });

        /// <summary>Bold circle outline for the focus frame around round skill nodes.</summary>
        public static Sprite ThickRing => s_ThickRing != null ? s_ThickRing : s_ThickRing = MakeRadial(128, (r, nx, ny) =>
            new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) * 64f / 3.5f)));

        // ---------------------------------------------------------------- skill tree sprites

        /// <summary>Metal node bezel: a ring shaded light at the top and dark at the bottom (grey
        /// values, so the Image tint keeps the shading), with a thin bright inner lip.</summary>
        public static Sprite Bezel => s_Bezel != null ? s_Bezel : s_Bezel = MakeRadial(128, (r, nx, ny) =>
        {
            float a = Mathf.Clamp01((1f - r) * 64f) * Mathf.Clamp01((r - 0.80f) * 64f);
            float shade = Mathf.Lerp(0.58f, 1f, ny * 0.5f + 0.5f);
            shade += 0.22f * Mathf.Clamp01(1f - Mathf.Abs(r - 0.83f) * 30f);             // inner lip
            shade += 0.16f * Mathf.Pow(Mathf.Clamp01(-nx * 0.6f + ny * 0.8f), 6f);       // top-left glint
            shade = Mathf.Min(1f, shade);
            return new Color(shade, shade, shade, a);
        });

        /// <summary>Node face: a disc that is slightly lighter in the middle and at the top.</summary>
        public static Sprite Face => s_Face != null ? s_Face : s_Face = MakeRadial(128, (r, nx, ny) =>
        {
            float a = Mathf.Clamp01((1f - r) * 64f);
            float shade = Mathf.Lerp(1f, 0.72f, r * r) + 0.08f * Mathf.Clamp01(ny) * (1f - r);
            return new Color(shade, shade, shade, a);
        });

        /// <summary>Thin outline circle (selection ring, milestone ring, affordable ring).</summary>
        public static Sprite ThinRing => s_ThinRing != null ? s_ThinRing : s_ThinRing = MakeRadial(128, (r, nx, ny) =>
            new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Abs(r - 0.975f) * 64f / 1.6f)));

        /// <summary>Band inside the bezel used as a radial progress arc (Image.Type.Filled).</summary>
        public static Sprite ArcRing => s_ArcRing != null ? s_ArcRing : s_ArcRing = MakeRadial(128, (r, nx, ny) =>
            new Color(1, 1, 1, Mathf.Clamp01((0.955f - r) * 64f) * Mathf.Clamp01((r - 0.855f) * 64f)));

        /// <summary>Dark edges, clear centre (stretched over a panel).</summary>
        public static Sprite Vignette => s_Vignette != null ? s_Vignette : s_Vignette = MakeRadial(128, (r, nx, ny) =>
            new Color(1, 1, 1, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.42f, r))));

        /// <summary>Faint concentric growth rings, like the cut face of a log.</summary>
        public static Sprite TreeRings => s_TreeRings != null ? s_TreeRings : s_TreeRings = MakeRadial(512, (r, nx, ny) =>
        {
            if (r >= 1f) return Color.clear;
            float ang = Mathf.Atan2(ny, nx);
            float rr = r + 0.012f * Mathf.Sin(ang * 3f + r * 9f) + 0.006f * Mathf.Sin(ang * 7f - r * 15f);
            float ring = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(rr * Mathf.PI * 2f * 11f * (0.8f + 0.4f * rr)), 14f);
            float fade = Mathf.Clamp01((1f - r) * 4f) * Mathf.Clamp01(r * 6f);
            return new Color(1, 1, 1, ring * fade);
        });

        /// <summary>Rows of pine silhouettes (opaque trees, clear sky); two variations for depth.</summary>
        public static Sprite ForestNear => s_ForestNear != null ? s_ForestNear : s_ForestNear = MakeForest(9, 0.5f, 1f, 30);
        public static Sprite ForestFar => s_ForestFar != null ? s_ForestFar : s_ForestFar = MakeForest(21, 0.45f, 0.85f, 44);

        /// <summary>One dash of the pattern used for locked links (tiled along the link).</summary>
        public static Sprite Dash
        {
            get
            {
                if (s_Dash != null) return s_Dash;
                var tex = new Texture2D(16, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
                for (int x = 0; x < 16; x++)
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, new Color(1, 1, 1, x < 9 ? Mathf.Clamp01(Mathf.Min(x + 0.5f, 9f - x - 0.5f)) : 0f));
                tex.Apply();
                return s_Dash = Sprite.Create(tex, new Rect(0, 0, 16, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            }
        }

        /// <summary>Square sprite from f(r, nx, ny): r = distance from the centre (1 = edge),
        /// (nx, ny) = unit direction from the centre.</summary>
        static Sprite MakeRadial(int size, Func<float, float, float, Color> f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            float h = size / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f - h) / h, dy = (y + 0.5f - h) / h;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float inv = r > 1e-5f ? 1f / r : 0f;
                px[y * size + x] = f(r, dx * inv, dy * inv);
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Wide strip of pine silhouettes standing on the bottom edge. Deterministic (seeded).</summary>
        static Sprite MakeForest(int seed, float minH, float maxH, int trees)
        {
            const int w = 1024, h = 160;
            var rng = new System.Random(seed);
            var tops = new float[w];
            for (int i = 0; i < trees; i++)
            {
                float cx = (float)rng.NextDouble() * w;
                float height = Mathf.Lerp(minH, maxH, (float)rng.NextDouble()) * h;
                float half = height * Mathf.Lerp(0.22f, 0.3f, (float)rng.NextDouble());
                for (int x = Mathf.Max(0, (int)(cx - half)); x < Mathf.Min(w, (int)(cx + half)); x++)
                {
                    float t = 1f - Mathf.Abs(x - cx) / half;
                    // Stepped edges read as tiers of branches.
                    float tier = 0.85f + 0.15f * Mathf.Abs(Mathf.Sin((1f - t) * height * 0.18f));
                    tops[x] = Mathf.Max(tops[x], height * t * tier);
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(tops[x] - y + 0.5f) * 255));
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 100f, 0, SpriteMeshType.FullRect);
        }

        /// <summary>Soft blurred rounded rectangle for drop shadows (9-sliced).</summary>
        public static Sprite Shadow => s_Shadow != null ? s_Shadow : s_Shadow = MakeSliced(128, 40, (x, y, size) =>
        {
            float d = RoundedDist(x, y, size, 40f, 18f);
            return Mathf.Clamp01(1f - (d + 22f) / 30f) * 0.9f;
        });

        /// <summary>1.5 px rounded outline for panel borders (9-sliced).</summary>
        public static Sprite Ring => s_Ring != null ? s_Ring : s_Ring = MakeSliced(64, 18, (x, y, size) =>
        {
            float d = RoundedDist(x, y, size, 0f, 14f);
            return Mathf.Clamp01(1.6f - Mathf.Abs(d + 1f));
        });

        /// <summary>Radial glow (used behind affordable skill nodes).</summary>
        public static Sprite Glow => s_Glow != null ? s_Glow : s_Glow = MakeSliced(128, 0, (x, y, size) =>
        {
            float r = new Vector2(x - size / 2f, y - size / 2f).magnitude / (size / 2f);
            float a = Mathf.Clamp01(1f - r);
            return a * a;
        });

        /// <summary>Horizontal fade: opaque on the left, clear on the right.</summary>
        public static Sprite GradientH => s_GradientH != null ? s_GradientH : s_GradientH = MakeGradient(true);
        /// <summary>Vertical fade: clear at the top, opaque at the bottom.</summary>
        public static Sprite GradientV => s_GradientV != null ? s_GradientV : s_GradientV = MakeGradient(false);

        /// <summary>Distance (in pixels, negative inside) to a rounded rect inset by margin.</summary>
        static float RoundedDist(float x, float y, int size, float margin, float radius)
        {
            float half = size / 2f - margin;
            float qx = Mathf.Abs(x - size / 2f) - half + radius, qy = Mathf.Abs(y - size / 2f) - half + radius;
            return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
        }

        static Sprite MakeSliced(int size, int border, Func<float, float, int, float> alpha)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha(x + 0.5f, y + 0.5f, size)) * 255));
            tex.SetPixels32(px);
            tex.Apply();
            int b = border > 0 ? border + 8 : 0;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeGradient(bool horizontal)
        {
            const int n = 256;
            var tex = new Texture2D(horizontal ? n : 4, horizontal ? 4 : n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float a = horizontal ? Mathf.SmoothStep(1f, 0f, t) : Mathf.SmoothStep(1f, 0f, t);
                for (int j = 0; j < 4; j++)
                {
                    if (horizontal) tex.SetPixel(i, j, new Color(1, 1, 1, a));
                    else tex.SetPixel(j, i, new Color(1, 1, 1, a));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }
    }
}

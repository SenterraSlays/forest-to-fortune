using UnityEngine;

namespace ChoppingTrees.EditorTools
{
    /// <summary>
    /// The world plan shared by the scene builder: ground height, paths, the yard and the three
    /// forest areas laid out along +Z (clearing, Old Grove behind gate 1, Ironwood Ridge behind gate 2).
    /// </summary>
    public static class WorldLayout
    {
        public const float MinX = -90f, MaxX = 90f, MinZ = -80f, MaxZ = 225f;
        public const float WallX = 56f, WallSouth = -44f, WallNorth = 192f;
        public const float Gate1Z = 50f, Gate2Z = 120f;

        public static readonly Vector3 Spawn = new Vector3(0f, 0f, -4f);
        public static readonly Rect Yard = Rect.MinMaxRect(-30f, -44f, 30f, -7f);

        /// <summary>Dirt paths as polylines (x, z).</summary>
        public static readonly Vector2[][] Paths =
        {
            new[] { new Vector2(0f, -36f), new Vector2(0f, -4f), new Vector2(0.5f, 20f), new Vector2(-1f, 50f), new Vector2(1f, 85f), new Vector2(0f, 120f), new Vector2(-1f, 155f), new Vector2(0f, 186f) },
            new[] { new Vector2(-9f, -13f), new Vector2(-4f, -8f), new Vector2(0f, -6f), new Vector2(4f, -8f), new Vector2(9f, -13f) },
            new[] { new Vector2(-24f, -26f), new Vector2(-10f, -26f), new Vector2(0f, -26f), new Vector2(10f, -26f), new Vector2(24f, -26f) },
        };

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float DistToYard(float x, float z)
        {
            float dx = Mathf.Max(Yard.xMin - x, 0f, x - Yard.xMax);
            float dz = Mathf.Max(Yard.yMin - z, 0f, z - Yard.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        public static float DistToPath(float x, float z)
        {
            float best = float.MaxValue;
            var p = new Vector2(x, z);
            foreach (var line in Paths)
                for (int i = 0; i < line.Length - 1; i++)
                {
                    Vector2 a = line[i], b = line[i + 1], ab = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                    best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
                }
            return best;
        }

        /// <summary>Ground height. Gentle rolling ground inside, flat yard and paths, hills at the edges.</summary>
        public static float Height(float x, float z)
        {
            float n = (Mathf.PerlinNoise(x * 0.035f + 100f, z * 0.035f + 50f) - 0.5f) * 1.6f
                      + (Mathf.PerlinNoise(x * 0.11f + 7f, z * 0.11f + 3f) - 0.5f) * 0.35f;
            float flat = Mathf.Clamp01(DistToYard(x, z) / 10f) * Mathf.Clamp01((DistToPath(x, z) - 1f) / 6f + 0.35f);
            flat *= Mathf.Clamp01((Mathf.Abs(z - Gate1Z) - 3f) / 6f) * Mathf.Clamp01((Mathf.Abs(z - Gate2Z) - 3f) / 6f) * 0.8f + 0.2f;
            n *= flat;
            float edge = Smooth(48f, 76f, Mathf.Abs(x)) * 16f + Smooth(-38f, -64f, z) * 14f + Smooth(186f, 214f, z) * 18f;
            edge *= 0.7f + 0.6f * Mathf.PerlinNoise(x * 0.05f + 30f, z * 0.05f + 60f);
            return n + edge;
        }

        public static Vector3 OnGround(float x, float z, float sink = 0f) => new Vector3(x, Height(x, z) - sink, z);

        /// <summary>0 = clearing, 1 = grove, 2 = ridge, -1 = outside the play space.</summary>
        public static int AreaAt(float x, float z)
        {
            if (Mathf.Abs(x) > WallX || z < WallSouth || z > WallNorth) return -1;
            if (z < Gate1Z) return 0;
            if (z < Gate2Z) return 1;
            return 2;
        }
    }
}

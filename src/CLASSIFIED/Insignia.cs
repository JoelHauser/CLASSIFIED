using System.Collections.Generic;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The factions' insignia, made for this mod (not the game's own art), in four pieces each
    /// so they can fly together the way the logo assembles:
    ///   BEAR: a shield cut by three claw marks, a ridge of mountains rising from its foot.
    ///   USEC: a spearhead between two swept wings, over a chevron of bars.
    ///   Unknown: four wedges closing on an empty centre, before the terminal knows whose it is.
    /// Every part is a convex polygon (all <see cref="Shape.Polygon"/> draws), in units of the
    /// insignia's radius: it fits within about one radius of its centre.
    /// </summary>
    internal static class Insignia
    {
        internal const int Pieces = 4;

        private enum Ink
        {
            /// <summary>The faction's colour.</summary>
            Main,

            /// <summary>A highlight, as the logo's light triangle.</summary>
            Light,

            /// <summary>The background colour: a cut through the piece below.</summary>
            Cut
        }

        private struct Part
        {
            internal Vector2[] Points;
            internal Ink Ink;
        }

        private static readonly Vector2[] BearShield =
        {
            new Vector2(-0.66f, 0.88f), new Vector2(0.66f, 0.88f), new Vector2(0.76f, 0.78f), new Vector2(0.76f, -0.05f), new Vector2(0.66f, -0.40f),
            new Vector2(0.46f, -0.70f), new Vector2(0.24f, -0.88f), new Vector2(0f, -0.98f), new Vector2(-0.24f, -0.88f),
            new Vector2(-0.46f, -0.70f), new Vector2(-0.66f, -0.40f), new Vector2(-0.76f, -0.05f), new Vector2(-0.76f, 0.78f)
        };

        /// <summary>Draws piece <paramref name="piece"/> of the faction's insignia into <paramref name="shape"/>, radius <paramref name="r"/>.</summary>
        internal static void DrawPiece(Faction faction, int piece, Shape shape, float r, Color32 main, Color32 light, Color32 cut)
        {
            foreach (Part part in Parts(faction, piece))
            {
                var points = new Vector2[part.Points.Length];
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] = part.Points[i] * r;
                }
                shape.Polygon(points, part.Ink == Ink.Main ? main : part.Ink == Ink.Light ? light : cut);
            }
        }

        /// <summary>The whole insignia in one shape, for a watermark or a seal.</summary>
        internal static void DrawAll(Faction faction, Shape shape, float r, Color32 main, Color32 light, Color32 cut)
        {
            for (int piece = 0; piece < Pieces; piece++)
            {
                DrawPiece(faction, piece, shape, r, main, light, cut);
            }
        }

        /// <summary>Which way piece <paramref name="piece"/> sits from the centre: the way it flies in from.</summary>
        internal static Vector2 Direction(Faction faction, int piece)
        {
            switch (faction)
            {
                case Faction.Bear:
                    return piece == 0 ? Vector2.down : new Vector2(-1f, 1f + 0.4f * (piece - 2)).normalized;
                case Faction.Usec:
                    return piece == 0 ? Vector2.up : piece == 1 ? Vector2.left : piece == 2 ? Vector2.right : Vector2.down;
                default:
                    return piece == 0 ? Vector2.up : piece == 1 ? Vector2.right : piece == 2 ? Vector2.down : Vector2.left;
            }
        }

        private static List<Part> Parts(Faction faction, int piece)
        {
            var parts = new List<Part>();
            switch (faction)
            {
                case Faction.Bear:
                    Bear(piece, parts);
                    break;
                case Faction.Usec:
                    Usec(piece, parts);
                    break;
                default:
                    Unknown(piece, parts);
                    break;
            }
            return parts;
        }

        private static void Bear(int piece, List<Part> parts)
        {
            if (piece == 0)
            {
                // The shield, a band cut across it, and a ridge of mountains rising from its foot
                // into the band.
                parts.Add(new Part { Points = BearShield, Ink = Ink.Main });
                Clipped(parts, Ink.Cut, new Vector2(-0.62f, -0.37f), new Vector2(0.62f, -0.37f), new Vector2(0.62f, 0.06f), new Vector2(-0.62f, 0.06f));
                Clipped(parts, Ink.Main, new Vector2(-0.80f, -0.38f), new Vector2(-0.46f, -0.08f), new Vector2(-0.14f, -0.38f));
                Clipped(parts, Ink.Main, new Vector2(-0.30f, -0.38f), new Vector2(0.04f, 0.0f), new Vector2(0.38f, -0.38f));
                Clipped(parts, Ink.Main, new Vector2(0.22f, -0.38f), new Vector2(0.50f, -0.12f), new Vector2(0.80f, -0.38f));
                return;
            }

            // A claw mark: a long blade, sharper on one side, slanting down to the right, cut
            // through the shield.
            int i = piece - 1;
            float x = -0.42f + 0.33f * i;
            float drop = i == 1 ? 0.06f : 0f;
            Vector2 top = new Vector2(x - 0.12f, 0.76f + drop);
            Vector2 bottom = new Vector2(x + 0.22f, 0.16f - drop * 0.5f);
            Clipped(parts, Ink.Cut, Blade(top, bottom, 0.13f, 0.035f));
        }

        private static void Usec(int piece, List<Part> parts)
        {
            switch (piece)
            {
                case 0:
                    // The spearhead, its right face caught by the light.
                    parts.Add(new Part
                    {
                        Points = new[] { new Vector2(0f, 0.98f), new Vector2(0.22f, 0.34f), new Vector2(0f, -0.10f), new Vector2(-0.22f, 0.34f) },
                        Ink = Ink.Main
                    });
                    parts.Add(new Part
                    {
                        Points = new[] { new Vector2(0.02f, 0.80f), new Vector2(0.16f, 0.35f), new Vector2(0.02f, 0.04f) },
                        Ink = Ink.Light
                    });
                    return;
                case 1:
                case 2:
                {
                    // Two swept feathers each side.
                    float s = piece == 1 ? -1f : 1f;
                    parts.Add(Mirror(s, new Vector2(0.30f, 0.40f), new Vector2(0.98f, 0.70f), new Vector2(0.98f, 0.52f), new Vector2(0.30f, 0.20f)));
                    parts.Add(Mirror(s, new Vector2(0.30f, 0.12f), new Vector2(0.86f, 0.34f), new Vector2(0.86f, 0.18f), new Vector2(0.30f, -0.06f)));
                    return;
                }
                default:
                {
                    // The chevron, and bars standing in it that stop just short of its arms.
                    parts.Add(Mirror(-1f, new Vector2(0.80f, -0.20f), new Vector2(0.64f, -0.20f), new Vector2(0f, -0.74f), new Vector2(0f, -0.94f)));
                    parts.Add(Mirror(1f, new Vector2(0.80f, -0.20f), new Vector2(0.64f, -0.20f), new Vector2(0f, -0.74f), new Vector2(0f, -0.94f)));
                    const float Slope = 0.54f / 0.64f;
                    foreach (float bx in new[] { -0.30f, 0f, 0.30f })
                    {
                        float half = 0.065f;
                        float Floor(float px) => -0.20f - (0.64f - Mathf.Abs(px)) * Slope + 0.07f;
                        parts.Add(new Part
                        {
                            Points = new[]
                            {
                                new Vector2(bx - half, -0.24f), new Vector2(bx + half, -0.24f),
                                new Vector2(bx + half, Floor(bx + half)), new Vector2(bx - half, Floor(bx - half))
                            },
                            Ink = Ink.Main
                        });
                    }
                    return;
                }
            }
        }

        private static void Unknown(int piece, List<Part> parts)
        {
            // A wedge pointing at the centre, turned a quarter for each piece.
            Vector2 d = Direction(Faction.Unknown, piece);
            Vector2 n = new Vector2(-d.y, d.x);
            parts.Add(new Part
            {
                Points = new[] { d * 0.98f + n * 0.34f, d * 0.30f, d * 0.98f - n * 0.34f },
                Ink = Ink.Main
            });
        }

        private static Part Mirror(float side, params Vector2[] points)
        {
            var mirrored = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                // Mirroring reverses the winding; Polygon takes either.
                mirrored[i] = new Vector2(points[i].x * side, points[i].y);
            }
            return new Part { Points = mirrored, Ink = Ink.Main };
        }

        /// <summary>A part kept inside the shield, so a cut never shows against the background.</summary>
        private static void Clipped(List<Part> parts, Ink ink, params Vector2[] points)
        {
            Vector2[] clipped = Clip(points, BearShield);
            if (clipped.Length >= 3)
            {
                parts.Add(new Part { Points = clipped, Ink = ink });
            }
        }

        /// <summary>
        /// A blade from <paramref name="a"/> to <paramref name="b"/>: one edge bowed out by
        /// <paramref name="wide"/>, the other by <paramref name="narrow"/>, both away from the
        /// line between the tips, so the outline stays convex.
        /// </summary>
        private static Vector2[] Blade(Vector2 a, Vector2 b, float wide, float narrow)
        {
            const int Steps = 10;
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized;
            var points = new List<Vector2>();
            for (int k = 0; k <= Steps; k++)
            {
                float t = k / (float)Steps;
                points.Add(Vector2.Lerp(a, b, t) + n * (wide * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f))));
            }
            for (int k = Steps - 1; k > 0; k--)
            {
                float t = k / (float)Steps;
                points.Add(Vector2.Lerp(a, b, t) - n * (narrow * Mathf.Sin(Mathf.PI * t)));
            }
            return points.ToArray();
        }

        /// <summary>Sutherland-Hodgman: the part of convex <paramref name="subject"/> inside convex <paramref name="clip"/>.</summary>
        private static Vector2[] Clip(Vector2[] subject, Vector2[] clip)
        {
            float area = 0f;
            for (int i = 0; i < clip.Length; i++)
            {
                Vector2 p = clip[i];
                Vector2 q = clip[(i + 1) % clip.Length];
                area += p.x * q.y - q.x * p.y;
            }
            float inward = area > 0f ? 1f : -1f;

            var output = new List<Vector2>(subject);
            for (int e = 0; e < clip.Length && output.Count > 0; e++)
            {
                Vector2 c0 = clip[e];
                Vector2 c1 = clip[(e + 1) % clip.Length];
                var input = output;
                output = new List<Vector2>();
                for (int i = 0; i < input.Count; i++)
                {
                    Vector2 p = input[i];
                    Vector2 q = input[(i + 1) % input.Count];
                    float dp = Side(c0, c1, p) * inward;
                    float dq = Side(c0, c1, q) * inward;
                    if (dp >= 0f)
                    {
                        output.Add(p);
                    }
                    if ((dp >= 0f) != (dq >= 0f))
                    {
                        output.Add(Vector2.Lerp(p, q, dp / (dp - dq)));
                    }
                }
            }
            return output.ToArray();
        }

        private static float Side(Vector2 a, Vector2 b, Vector2 p) => (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }
}

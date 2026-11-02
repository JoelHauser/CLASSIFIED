using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// Each unit's ambient drawing: one large, faint, slow thing behind the terminal that belongs
    /// to that unit and, where it can, follows the load: the route on the map advances, the
    /// receiver locks on, the seal's ring closes. No widgets; faint, so the insignia and readout
    /// stay the subject.
    ///
    /// Nothing is drawn under text. Each layout names the areas its text leaves free (ArtZones):
    /// two zones, and a strip under the readout above the ticker that both zones reach down to,
    /// so a drawing can run from one zone along the strip into the other without crossing text.
    /// The drawings size themselves to those areas. Built on the full-screen layer, in 1080p
    /// units around the screen's centre.
    /// </summary>
    internal partial class Overlay
    {
        private const float ArtFloor = -405f;
        private const float ArtStripTop = -322f;
        private const float ArtStripBottom = -408f;
        /// <summary>Under the top corner lines.</summary>
        private const float ArtTop = 400f;
        /// <summary>Under the memo in the top-right corner.</summary>
        private const float ArtUnderMemo = 170f;

        /// <summary>
        /// The text-free areas of the current layout, in 1080p units, y up: a left and a right
        /// zone, both reaching down to the strip. False where the layout leaves no room (the dock,
        /// and screens too narrow for zones beside the text).
        /// </summary>
        private bool ArtZones(out Rect left, out Rect right, out Rect strip)
        {
            float w = _laidOutSize.x / _u;
            float edge = w * 0.5f - 72f;
            float stripHalf = w * 0.5f - 118f;
            left = right = strip = default;
            strip = Rect.MinMaxRect(-stripHalf, ArtStripBottom, stripHalf, ArtStripTop);
            switch (_layout)
            {
                case LayoutKind.Centered:
                    // Either side of the logo and readout column (within 320 of the centre line).
                    if (edge - 360f < 180f)
                    {
                        return false;
                    }
                    left = Rect.MinMaxRect(-edge, ArtFloor, -360f, ArtTop);
                    right = Rect.MinMaxRect(360f, ArtFloor, edge, ArtUnderMemo);
                    return true;
                case LayoutKind.Sidebar:
                {
                    // The open area right of the sign-in panel, in two halves.
                    float open = -w * 0.5f + 620f + 40f;
                    if (edge - open < 440f)
                    {
                        return false;
                    }
                    float middle = (open + edge) * 0.5f;
                    left = Rect.MinMaxRect(open, ArtFloor, middle - 20f, ArtTop);
                    right = Rect.MinMaxRect(middle + 20f, ArtFloor, edge, ArtUnderMemo);
                    strip = Rect.MinMaxRect(open, ArtStripBottom, Mathf.Min(edge, stripHalf), ArtStripTop);
                    return true;
                }
                case LayoutKind.Split:
                case LayoutKind.SplitMirror:
                {
                    // Under the logo and wordmark on one side, under the readout on the other.
                    bool brandLeft = _layout == LayoutKind.Split;
                    const float UnderBrand = -160f;
                    const float UnderReadout = -120f;
                    left = Rect.MinMaxRect(-edge, ArtFloor, -30f, brandLeft ? UnderBrand : UnderReadout);
                    right = Rect.MinMaxRect(30f, ArtFloor, edge, brandLeft ? UnderReadout : UnderBrand);
                    return true;
                }
                default:
                    return false;
            }
        }

        private bool AmbientRoom => ArtZones(out _, out _, out _);

        /// <summary>The unit's ambient drawing, or null where the backdrop already has one (the watermark).</summary>
        private static Signature CreateAmbient(ThemeChoice id)
        {
            // Every unit of a faction gets the debrief (Overlay.Debrief.cs): its weapon and its
            // operation. An unidentified terminal has neither.
            return Kit.Has(id) ? new Debrief() : null;
        }

        /// <summary>Shared strengths, shapes and the text-free areas for the ambient drawings.</summary>
        private abstract class Ambient : Signature
        {
            private bool _zoned;
            private Rect _left;
            private Rect _right;

            private void Zones()
            {
                if (!_zoned)
                {
                    O.ArtZones(out _left, out _right, out _);
                    _zoned = true;
                }
            }

            /// <summary>The left zone.</summary>
            protected Rect Lz { get { Zones(); return _left; } }
            /// <summary>The right zone.</summary>
            protected Rect Rz { get { Zones(); return _right; } }

            protected override bool Seeded => true;

            protected Color32 Line => A(T.Accent, 70);
            protected Color32 Soft => A(T.Accent, 30);
            protected Color32 Fill => A(T.Accent, 10);

            /// <summary>A masked area: what is drawn in it stops at its edges. Draw in it relative to its centre.</summary>
            protected RectTransform Clip(Rect area)
            {
                RectTransform rect = MakeRect("Clip", R);
                Place(rect, area.center.x * U, area.center.y * U, area.width * U, area.height * U);
                rect.gameObject.AddComponent<RectMask2D>();
                return rect;
            }

            /// <summary>A head-and-shoulders silhouette filling a photo h tall, centred at (x, y).</summary>
            protected void Person(Shape shape, float x, float y, float h, Color32 color)
            {
                shape.Disc(P(x, y + 0.16f * h), 0.2f * h * U, color);
                float r = 0.17f * h * U;
                shape.RoundedRect(P(x, y - 0.3f * h), 0.72f * h * U, 0.34f * h * U, r, r, 0f, 0f, color);
            }
        }
    }
}

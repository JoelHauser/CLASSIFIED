using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The layer behind the terminal that holds the unit's ambient drawing (see
    /// Overlay.Ambient.cs), and the base the drawings are built on: a few helpers for boxes,
    /// labels and shapes in 1080p units around the screen's centre. Rebuilt with the layout.
    /// </summary>
    internal partial class Overlay
    {
        private RectTransform _ambientRoot;
        private Signature _ambient;

        private void BuildSignatureRoot(Transform c)
        {
            _ambientRoot = MakeRect("Ambient", c);
            Isolate(_ambientRoot);
        }

        private void LayoutSignature()
        {
            for (int i = _ambientRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_ambientRoot.GetChild(i).gameObject);
            }
            // The drawings keep to the space the layout leaves free of text; where it leaves
            // none, there is no drawing.
            _ambient = AmbientRoom ? CreateAmbient(_variant.Theme.Id) : null;
            _ambient?.Init(this, _ambientRoot, _u);
        }

        private void AnimatePanels(float t)
        {
            _ambient?.Animate(t);
        }

        private abstract class Signature
        {
            protected Overlay O;
            protected RectTransform R;
            protected float U;
            protected Theme T;

            internal void Init(Overlay overlay, RectTransform root, float u)
            {
                O = overlay;
                R = root;
                U = u;
                T = overlay._variant.Theme;
                _rng = Seeded ? new System.Random(overlay._variant.ChartSeed) : null;
                Build();
            }

            protected abstract void Build();
            internal abstract void Animate(float t);

            protected float Progress => O._phase == Phase.Loading ? O._shownFraction : 1f;
            protected float Dt => O._frameDt;
            protected System.Random Rng => _rng ?? O._random;

            /// <summary>
            /// Draws from a generator seeded per launch, so a rebuild (the terminal lays itself out again when
            /// the game's font replaces the boot font) draws the same picture instead of a new one.
            /// </summary>
            protected virtual bool Seeded => false;

            private System.Random _rng;
            protected Color32 Dark => Color32.Lerp(T.Ink, new Color32(0, 0, 0, 255), 0.45f);

            protected Vector2 P(float x, float y) => new Vector2(x * U, y * U);

            protected static Color32 A(Color32 color, byte alpha) => Theme.WithAlpha(color, alpha);

            protected float Range(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

            protected Image Box(float x, float y, float w, float h, Color32 color, Transform parent = null)
            {
                Image image = MakeImage("Box", parent != null ? parent : R, color);
                Place(image.rectTransform, x * U, y * U, w * U, h * U);
                return image;
            }

            protected TextMeshProUGUI Label(string text, float x, float y, float w, float size, Color32 color,
                TextAlignmentOptions align = TextAlignmentOptions.Left, Transform parent = null, float spacing = 6f)
            {
                TextMeshProUGUI label = O.MakeText("Label", parent != null ? parent : R, align, color);
                label.text = text;
                SetText(label, x * U, y * U, w * U, (size + 8f) * U, size * U, spacing);
                return label;
            }

            protected Shape Draw(float x = 0f, float y = 0f, Transform parent = null)
            {
                Shape shape = Shape.Create("Shape", parent != null ? parent : R);
                shape.rectTransform.anchoredPosition = P(x, y);
                return shape;
            }

            protected void Outline(Shape shape, float x, float y, float w, float h, float width, Color32 color)
            {
                float l = x - w * 0.5f, r = x + w * 0.5f, b = y - h * 0.5f, t = y + h * 0.5f;
                float half = width * 0.5f;
                shape.Line(P(l - half, t), P(r + half, t), width * U, color);
                shape.Line(P(l - half, b), P(r + half, b), width * U, color);
                shape.Line(P(l, t), P(l, b), width * U, color);
                shape.Line(P(r, t), P(r, b), width * U, color);
            }
        }
    }
}

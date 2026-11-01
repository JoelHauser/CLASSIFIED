using System;
using TMPro;
using UnityEngine;

namespace CLASSIFIED
{
    /// <summary>
    /// The preview: from the main menu, the terminal plays over a pretend load (see
    /// <see cref="StartupTracker.BeginPreview"/>) so every unit and layout can be seen
    /// without relaunching. Keys while it shows: Left/Right unit, Up/Down layout, Space
    /// re-rolls the other parts, R replays, Esc closes. A caption names what is on screen.
    /// </summary>
    internal partial class Overlay
    {
        private static readonly LayoutKind[] Layouts =
        {
            LayoutKind.Centered, LayoutKind.Split, LayoutKind.SplitMirror, LayoutKind.Sidebar, LayoutKind.Dock
        };

        private bool _previewing;
        private Variant _previewVariant;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _captionHelp;

        /// <summary>
        /// Opens the preview with a fresh roll. Main menu only, so it can never cover a raid, and
        /// ignored while the game is really loading.
        /// </summary>
        internal static void StartPreview()
        {
            if (!CanPlayOverMenu())
            {
                return;
            }
            if (_instance == null)
            {
                FindBootFont(null, out TMP_FontAsset font, out Material material);
                Create(font, material);
            }
            _instance.PlayPreview(_instance.RollPreview(ClassifiedPlugin.Theme.Value, ClassifiedPlugin.Layout.Value));
        }

        /// <summary>The preview's keys. Called every frame by the plugin; does nothing unless a preview is showing.</summary>
        internal static void PreviewInput()
        {
            if (_instance == null || !_instance._previewing)
            {
                return;
            }
            Overlay o = _instance;
            Variant v = o._previewVariant;

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                StartupTracker.EndRun();
                o.Hide();
                return;
            }
            if (o._showreel)
            {
                // The reel plays as set; only Esc stops it.
                return;
            }

            int unit = Input.GetKeyDown(KeyCode.RightArrow) ? 1 : Input.GetKeyDown(KeyCode.LeftArrow) ? -1 : 0;
            int layout = Input.GetKeyDown(KeyCode.DownArrow) ? 1 : Input.GetKeyDown(KeyCode.UpArrow) ? -1 : 0;
            if (unit != 0)
            {
                int i = Wrap(Array.IndexOf(Theme.All, v.Theme) + unit, Theme.All.Length);
                o.PlayPreview(o.RollPreview(Theme.All[i].Id, v.Layout));
            }
            else if (layout != 0)
            {
                int i = Wrap(Array.IndexOf(Layouts, v.Layout) + layout, Layouts.Length);
                o.PlayPreview(o.RollPreview(v.Theme.Id, Layouts[i]));
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                o.PlayPreview(o.RollPreview(v.Theme.Id, v.Layout));
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                o.PlayPreview(v);
            }
        }

        private static int Wrap(int i, int count) => ((i % count) + count) % count;

        private Variant RollPreview(ThemeChoice theme, LayoutKind layout)
        {
            if (_random == null)
            {
                _random = new System.Random(Environment.TickCount);
            }
            Pins pins = Pins.FromConfig();
            pins.Layout = layout;
            Faction faction = theme != ThemeChoice.Random ? Theme.Get(theme).Faction : PlayerFaction();
            if (faction == Faction.Unknown && theme == ThemeChoice.Random)
            {
                // No profile seen yet: the preview shows a faction anyway.
                faction = Faction.Bear;
            }
            return Variant.Roll(_random, theme, faction, ClassifiedPlugin.Glitches.Value, StartupTracker.Launches, pins, preview: true);
        }

        /// <summary>Plays <paramref name="variant"/> from the intro, cutting off whatever was showing.</summary>
        private void PlayPreview(Variant variant)
        {
            if (_phase != Phase.Hidden)
            {
                StartupTracker.EndRun();
                Hide();
            }
            if (!StartupTracker.BeginPreview())
            {
                return;
            }
            _previewVariant = variant;
            _previewing = true;
            StartLoading(null);

            _caption.text = $"PREVIEW  //  {variant.Theme.Division}  /  {Name(variant.Layout.ToString())} LAYOUT  /  "
                + $"{Name(variant.Backdrop.ToString())}  /  {Name(variant.Frame.ToString())} FRAME  /  "
                + $"{Name(variant.Intro.ToString())} INTRO  /  {Name(variant.Bar.ToString())} BAR  /  {Name(variant.Outro.ToString())} ENDING";
            _caption.color = Accent;
            _captionHelp.color = Dim;
            _caption.gameObject.SetActive(true);
            _captionHelp.gameObject.SetActive(true);
        }

        /// <summary>SplitMirror becomes SPLIT MIRROR.</summary>
        private static string Name(string identifier)
        {
            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                if (i > 0 && char.IsUpper(identifier[i]))
                {
                    builder.Append(' ');
                }
                builder.Append(char.ToUpperInvariant(identifier[i]));
            }
            return builder.ToString();
        }

        private void BuildCaption()
        {
            _caption = MakeText("PreviewCaption", transform, TextAlignmentOptions.Center, Accent);
            _captionHelp = MakeText("PreviewHelp", transform, TextAlignmentOptions.Center, Dim);
            _captionHelp.text = "LEFT / RIGHT  UNIT      UP / DOWN  LAYOUT      SPACE  RE-ROLL      R  REPLAY      ESC  CLOSE";
            _caption.gameObject.SetActive(false);
            _captionHelp.gameObject.SetActive(false);
        }

        /// <summary>Along the very bottom, under the corner brackets, where nothing else goes.</summary>
        private void LayoutCaption(Vector2 size)
        {
            SetText(_caption, 0f, -size.y * 0.5f + 34f * _u, size.x * 0.9f, 18f * _u, 12f * _u, 6f);
            SetText(_captionHelp, 0f, -size.y * 0.5f + 16f * _u, size.x * 0.9f, 16f * _u, 10f * _u, 6f);
        }

        private void EndPreview()
        {
            _previewing = false;
            _caption.gameObject.SetActive(false);
            _captionHelp.gameObject.SetActive(false);
        }
    }
}

using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// Changing terminals mid-load. The terminal starts in the faction of the last profile it
    /// saw (or unidentified on a first launch); when the game picks this launch's profile and the
    /// faction is wrong, it crossfades to one of the right faction's units. The showreel uses the
    /// same crossfade between its units.
    ///
    /// A crossfade is a capture of the screen as it was, laid over the new terminal and faded out
    /// while it grows slightly. The overlay is one set of parts, so two terminals cannot be drawn
    /// at once; the capture stands in for the one leaving.
    ///
    /// Also here: the faction to show, the Cyrillic fallback, and standing in for ACCESS GRANTED.
    /// </summary>
    internal partial class Overlay
    {
        private const float CrossfadeTime = 0.5f;
        private const float CrossfadeGrow = 0.03f;

        private Variant _pendingSwitch;
        private bool _capturing;
        private bool _captured;
        private RawImage _snapshot;
        private RenderTexture _snapshotTexture;

        /// <summary>When the showing capture started fading; below zero, none is showing.</summary>
        private float _snapshotAt = -1f;
        private bool _snapshotReady;

        /// <summary>The faction to show: the setting, else this launch's profile, else the last one seen.</summary>
        internal static Faction PlayerFaction()
        {
            switch (ClassifiedPlugin.FactionSetting.Value)
            {
                case FactionChoice.Bear: return Faction.Bear;
                case FactionChoice.Usec: return Faction.Usec;
                default: return Theme.ParseSide(StartupTracker.Side ?? StartupTracker.LastSide);
            }
        }

        /// <summary>The game picked its profile. If the terminal is wearing the wrong faction, crossfade to the right one.</summary>
        internal static void ProfileKnown()
        {
            Overlay o = _instance;
            if (o == null || o._phase != Phase.Loading || o._previewing || ClassifiedPlugin.Theme.Value != ThemeChoice.Random)
            {
                return;
            }
            Faction faction = PlayerFaction();
            if (faction == Faction.Unknown || faction == o._variant.Faction)
            {
                o.AddQuestIntel(o._variant);
                return;
            }
            ClassifiedPlugin.Log.LogInfo($"Terminal: operator is {Theme.Name(faction)}, changing terminals.");
            Variant next = Variant.Roll(o._random, ThemeChoice.Random, faction, ClassifiedPlugin.Glitches.Value,
                StartupTracker.Launches, Pins.FromConfig());
            // Its notices are spaced over the whole load; the ones already behind it come at once,
            // so only keep those still ahead.
            next.Notices = System.Array.FindAll(next.Notices, n => n.At > StartupTracker.Fraction());
            o.AddQuestIntel(next);
            o.CrossfadeTo(next);
        }

        /// <summary>
        /// The profile's tasks are known now: the next notice still to come becomes intel about
        /// one of them, or, if none is left and the load has a way to go, one is added.
        /// </summary>
        private void AddQuestIntel(Variant variant)
        {
            if (StartupTracker.Quests.Count == 0 || variant.Notices == null)
            {
                return;
            }
            float done = StartupTracker.Fraction();
            foreach (Notice existing in variant.Notices)
            {
                if (existing.Kind == NoticeKind.Intel && existing.From != null && (existing.From.StartsWith("S2") || existing.From.Contains("HQ") || existing.From.Contains("РАЗВЕД")))
                {
                    return;
                }
            }
            int index = System.Array.FindIndex(variant.Notices, n => n.At > done);
            if (index >= 0)
            {
                Notice quest = QuestIntel.Make(_random, variant.Faction, variant.Notices[index].At);
                if (quest != null)
                {
                    variant.Notices[index] = quest;
                }
            }
            else if (done < 0.7f)
            {
                Notice quest = QuestIntel.Make(_random, variant.Faction, Mathf.Min(0.85f, done + 0.12f));
                if (quest != null)
                {
                    var list = new System.Collections.Generic.List<Notice>(variant.Notices) { quest };
                    variant.Notices = list.ToArray();
                }
            }
        }

        /// <summary>Starts a crossfade to <paramref name="variant"/>: the screen is captured at the end of this frame, the switch made on the next.</summary>
        private void CrossfadeTo(Variant variant)
        {
            _pendingSwitch = variant;
            if (!_capturing)
            {
                _capturing = true;
                StartCoroutine(CaptureScreen());
            }
        }

        private IEnumerator CaptureScreen()
        {
            yield return new WaitForEndOfFrame();
            if (_phase == Phase.Hidden)
            {
                yield break;
            }
            bool ok = false;
            try
            {
                int width = Screen.width;
                int height = Screen.height;
                if (_snapshotTexture == null || _snapshotTexture.width != width || _snapshotTexture.height != height)
                {
                    ReleaseSnapshot();
                    _snapshotTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
                    _snapshotTexture.Create();
                    _snapshot.texture = _snapshotTexture;
                    // Direct3D's render textures start at the top: shown as they are, the capture is upside down.
                    _snapshot.uvRect = SystemInfo.graphicsUVStartsAtTop ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
                }
                ScreenCapture.CaptureScreenshotIntoRenderTexture(_snapshotTexture);
                ok = true;
            }
            catch (System.Exception e)
            {
                ClassifiedPlugin.Log.LogWarning("Could not capture the screen, changing terminals without a crossfade: " + e.Message);
            }
            _snapshotReady = ok;
            _captured = true;
        }

        /// <summary>Makes a switch whose capture has been taken. Called each frame of the load.</summary>
        private void CompleteCrossfade()
        {
            if (!_captured)
            {
                return;
            }
            _captured = _capturing = false;
            Variant next = _pendingSwitch;
            _pendingSwitch = null;
            if (next != null)
            {
                SwitchShot(next);
            }
        }

        /// <summary>The next terminal takes over the running load, under the capture of the last.</summary>
        private void SwitchShot(Variant variant)
        {
            _previewVariant = _previewing ? variant : _previewVariant;
            _variant = variant;
            _debriefAt = _animClock;
            _chatterSource = new Chatter(_random, variant.Theme);
            ResetFx();
            ApplyVariant();
            ResetStyle();
            _laidOutSize = Vector2.zero;
            _shownStep = -2;
            _shownDetail = null;
            _shownStepTenths = -1;
            _shownElapsedTenths = -1;
            _shownPercent = -1;
            _ringProgressDrawn = -1f;
            _slow = false;
            _emblem.localScale = Vector3.one;
            _currentLabel.text = string.Empty;
            _logLabels.text = _logTimes.text = string.Empty;
            _chatter.text = string.Empty;
            _chatterNextAt = 0f;
            // Past the intro's covers, but the insignia still assembles, under the crossfade.
            _introClock = IntroLength + 0.1f;

            if (_snapshotReady)
            {
                _snapshotReady = false;
                _snapshotAt = _animClock;
                _snapshot.rectTransform.localScale = Vector3.one;
                _snapshot.canvasRenderer.SetAlpha(1f);
                _snapshot.gameObject.SetActive(true);
            }
        }

        private void AnimateSnapshot()
        {
            if (_snapshotAt < 0f)
            {
                return;
            }
            float p = Mathf.Clamp01((_animClock - _snapshotAt) / CrossfadeTime);
            float ease = p * p * (3f - 2f * p);
            _snapshot.canvasRenderer.SetAlpha(1f - ease);
            _snapshot.rectTransform.localScale = Vector3.one * (1f + CrossfadeGrow * ease);
            if (p >= 1f)
            {
                _snapshotAt = -1f;
                _snapshot.gameObject.SetActive(false);
            }
        }

        private void BuildSnapshot()
        {
            _snapshot = MakeRaw("Snapshot", transform, null, Color.white);
            Stretch(_snapshot.rectTransform);
            Isolate(_snapshot);
            _snapshot.gameObject.SetActive(false);
        }

        private void EndCrossfade()
        {
            _capturing = _captured = _snapshotReady = false;
            _pendingSwitch = null;
            _snapshotAt = -1f;
            _snapshot.gameObject.SetActive(false);
            ReleaseSnapshot();
        }

        private void ReleaseSnapshot()
        {
            if (_snapshotTexture != null)
            {
                _snapshot.texture = null;
                _snapshotTexture.Release();
                Destroy(_snapshotTexture);
                _snapshotTexture = null;
            }
        }

        // ---- words --------------------------------------------------------------------------

        /// <summary>The unit, in Russian too for BEAR where the font allows, and the stage of the launch.</summary>
        private string Subtitle(bool boot)
        {
            Theme theme = _variant.Theme;
            // BEAR in Russian gives its unit's own name; the stage follows the terminal's language.
            string unit = theme.Faction == Faction.Bear && BearRussian && !string.IsNullOrEmpty(theme.Native) ? Cyr(theme.Native) : theme.Division;
            return unit + "  //  " + Stage(boot);
        }

        /// <summary>Whether the terminal's font has Russian letters; checked when the font is set.</summary>
        private bool _cyrillic = true;

        private void CheckCyrillic()
        {
            try
            {
                _cyrillic = _font != null && _font.HasCharacters("АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ", out _, true, true);
            }
            catch (System.Exception)
            {
                _cyrillic = false;
            }
            if (!_cyrillic)
            {
                ClassifiedPlugin.Log.LogInfo("Terminal font has no Russian letters: BEAR's Russian is shown in Latin letters.");
            }
        }

        private static readonly string[] Latin =
        {
            "A", "B", "V", "G", "D", "E", "ZH", "Z", "I", "Y", "K", "L", "M", "N", "O", "P", "R", "S", "T", "U", "F", "KH", "TS", "CH",
            "SH", "SHCH", "", "Y", "", "E", "YU", "YA"
        };

        /// <summary>Russian text as it is, or spelled in Latin letters if the font cannot show it.</summary>
        private string Cyr(string text)
        {
            if (_cyrillic || string.IsNullOrEmpty(text))
            {
                return text;
            }
            StringBuilder builder = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = char.ToUpperInvariant(text[i]);
                string latin = c >= 'А' && c <= 'Я' ? Latin[c - 'А'] : c == 'Ё' ? "YO" : null;
                if (latin == null)
                {
                    builder?.Append(text[i]);
                    continue;
                }
                if (builder == null)
                {
                    builder = new StringBuilder(text.Substring(0, i));
                }
                builder.Append(latin);
            }
            return builder == null ? text : builder.ToString();
        }

        // ---- ACCESS GRANTED -----------------------------------------------------------------

        private Canvas _otherTerminal;
        private bool _otherHidden;
        private float _lookForOtherAt;

        /// <summary>
        /// On the launches CLASSIFIED shows, ACCESS GRANTED's terminal (if installed) still runs,
        /// under this one; its canvas is switched off so its own ending cannot show through ours.
        /// Switched back on when this terminal is done, so its preview still works in the menu.
        /// </summary>
        private void StandInFor(Phase phase)
        {
            if (!ClassifiedPlugin.AccessGrantedInstalled)
            {
                return;
            }
            bool hide = phase != Phase.Hidden && !_previewing;
            if (hide && _otherTerminal == null && Time.unscaledTime >= _lookForOtherAt)
            {
                // A name lookup, a few times a second at most, until it turns up.
                _lookForOtherAt = Time.unscaledTime + 0.25f;
                GameObject other = GameObject.Find("TerraGroupLoading");
                _otherTerminal = other != null ? other.GetComponent<Canvas>() : null;
            }
            if (_otherTerminal == null)
            {
                return;
            }
            if (hide && _otherTerminal.enabled)
            {
                _otherTerminal.enabled = false;
                _otherHidden = true;
            }
            else if (!hide && _otherHidden)
            {
                _otherTerminal.enabled = true;
                _otherHidden = false;
            }
        }
    }
}

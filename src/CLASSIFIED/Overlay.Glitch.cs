using System;
using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The terminal's glitches, and the closing ACCESS GRANTED sequence they dress up.
    ///
    /// Every glitch runs on the animation clock and also ends after a few drawn frames, whichever
    /// comes first, and any long frame ends it at once: a glitch that happened to be on screen when
    /// the game stalled would otherwise sit there for the whole stall and look like a fault. For
    /// the same reason a random glitch only starts on a smooth frame, never during the intro, and
    /// never once access is granted; the closing sequence waits for the menu to settle first.
    /// </summary>
    internal partial class Overlay
    {
        [Flags]
        private enum Fx
        {
            None = 0,
            Tear = 1,
            Split = 2,
            Corrupt = 4,
            Drop = 8
        }

        /// <summary>A frame this long ends whatever glitch is showing.</summary>
        private const float StallFrame = 0.1f;

        /// <summary>No random glitches until the intro has played out.</summary>
        private float GlitchesFrom => IntroLength + 1.5f;

        /// <summary>No angle brackets: TextMeshPro would read them as tags.</summary>
        private const string CorruptGlyphs = "#%&@$*+=/\\|?!01";

        private const int TearBands = 2;
        private const float RingKickTime = 0.4f;
        private const float DeniedFor = 0.4f;
        private const float RetryFor = 0.35f;
        private const float BurstFor = 0.22f;

        private Fx _fx;
        private float _fxUntil;
        private int _fxFrames;
        private float _nextGlitchAt;
        private string _labelClean;
        private string _labelCorrupt;

        private readonly float[] _bandY = new float[TearBands];
        private readonly float[] _bandHalf = new float[TearBands];
        private readonly float[] _bandShift = new float[TearBands];
        private int _bandCount;
        private readonly Image[] _tearFills = new Image[TearBands];
        private readonly Image[] _tearEdges = new Image[TearBands * 2];
        private RectTransform[] _tearables;
        private Vector2[] _tearBase;
        private float[] _tearHalf;

        private float _ringKick;
        private float _ringKickAt;
        private int _ringKickWhich;

        private float _revealAt = -1f;
        private int _revealStage;
        private float _finaleAt;
        private float _finaleLength;

        private void ResetFx()
        {
            EndFx();
            _ringKick = 0f;
            _nextGlitchAt = 0f;
            _revealAt = -1f;
            _revealStage = 0;
        }

        /// <summary>Remembers where everything a tear can shove sits. Called after each layout.</summary>
        private void CaptureTearBase()
        {
            for (int i = 0; i < _tearables.Length; i++)
            {
                _tearBase[i] = _tearables[i].anchoredPosition;
                // The emblem's rect is empty; its rings reach about this far.
                _tearHalf[i] = _tearables[i] == _emblem ? 130f * _u * _brandScale : _tearables[i].sizeDelta.y * 0.5f;
            }
        }

        private void UpdateFx(float t)
        {
            if (_fx != Fx.None)
            {
                _fxFrames--;
                if (t >= _fxUntil || _fxFrames <= 0 || Time.unscaledDeltaTime > StallFrame)
                {
                    EndFx();
                }
                else if ((_fx & Fx.Drop) != 0)
                {
                    _dropout.canvasRenderer.SetAlpha(0.15f + 0.4f * (float)_random.NextDouble());
                }
            }

            // The last steps are where the game freezes longest (seconds at a time); a glitch
            // caught on screen by one of those looks like a fault, so none start there.
            if (_phase != Phase.Loading || _variant.Glitches == GlitchLevel.Off || _introClock < GlitchesFrom
                || StartupTracker.Current >= (int)StepId.WeaponBuilds)
            {
                return;
            }
            if (_nextGlitchAt <= 0f)
            {
                ScheduleGlitch(t);
                return;
            }
            if (t < _nextGlitchAt || _fx != Fx.None || Time.unscaledDeltaTime >= SmoothFrame)
            {
                return;
            }
            RandomGlitch(t);
            ScheduleGlitch(t);
        }

        private void ScheduleGlitch(float t)
        {
            // Glitches come closer together as the load nears the end, and faster still in alert.
            float gap = _variant.Glitches == GlitchLevel.Heavy ? Range(1.2f, 3.5f) : Range(3.5f, 8f);
            _nextGlitchAt = t + gap * Mathf.Lerp(1f, 0.6f, _shownFraction) / (1f + _alert);
        }

        private void RandomGlitch(float t)
        {
            bool heavy = _variant.Glitches == GlitchLevel.Heavy;
            int roll = _random.Next(100);
            if (roll < 30)
            {
                StartTear(t, heavy);
            }
            else if (roll < 55)
            {
                StartFx(Fx.Split, t, Range(0.08f, 0.16f), 9, false);
            }
            else if (roll < 75)
            {
                if (!StartCorrupt(t, heavy ? 4 : 2))
                {
                    StartFx(Fx.Split, t, Range(0.08f, 0.16f), 9, false);
                }
            }
            else if (roll < 85)
            {
                StartFx(Fx.Drop, t, Range(0.1f, 0.22f), 12, false);
            }
            else
            {
                KickRing(t);
            }

            // Heavy sometimes stacks a second one on top.
            if (heavy && _random.Next(100) < 35)
            {
                if (roll < 30)
                {
                    StartFx(Fx.Split, t, Range(0.08f, 0.14f), 8, false);
                }
                else
                {
                    StartTear(t, false);
                }
            }
        }

        private void StartFx(Fx fx, float t, float duration, int frames, bool onLabel)
        {
            Fx added = fx & ~_fx;
            _fxUntil = _fx == Fx.None ? t + duration : Mathf.Max(_fxUntil, t + duration);
            _fxFrames = _fx == Fx.None ? frames : Mathf.Max(_fxFrames, frames);
            _fx |= fx;

            if ((added & Fx.Tear) != 0)
            {
                ApplyTear();
            }
            if ((added & Fx.Split) != 0)
            {
                ShowSplit(onLabel);
            }
            if ((added & Fx.Drop) != 0)
            {
                _dropout.gameObject.SetActive(true);
                _dropout.canvasRenderer.SetAlpha(0.4f);
            }
        }

        private void EndFx()
        {
            if (_fx == Fx.None)
            {
                return;
            }
            if ((_fx & Fx.Tear) != 0)
            {
                for (int i = 0; i < _tearables.Length; i++)
                {
                    _tearables[i].anchoredPosition = _tearBase[i];
                }
                foreach (Image fill in _tearFills)
                {
                    fill.gameObject.SetActive(false);
                }
                foreach (Image edge in _tearEdges)
                {
                    edge.gameObject.SetActive(false);
                }
            }
            if ((_fx & Fx.Split) != 0)
            {
                _wordmarkGhost.enabled = _wordmarkSplit.enabled = false;
                _labelGhost.enabled = _labelSplit.enabled = false;
            }
            if ((_fx & Fx.Corrupt) != 0 && _currentLabel.text == _labelCorrupt)
            {
                // Only if the line is still the corrupted one: a new step may have replaced it.
                _currentLabel.text = _labelClean;
            }
            if ((_fx & Fx.Drop) != 0)
            {
                _dropout.gameObject.SetActive(false);
            }
            _fx = Fx.None;
        }

        /// <summary>One or two horizontal bands of the screen jump sideways.</summary>
        private void StartTear(float t, bool twoBands)
        {
            if ((_fx & Fx.Tear) != 0)
            {
                return;
            }
            float h = _laidOutSize.y;
            _bandCount = twoBands && _random.Next(2) == 0 ? 2 : 1;
            for (int b = 0; b < _bandCount; b++)
            {
                _bandY[b] = Mathf.Round(Range(-0.35f, 0.4f) * h);
                _bandHalf[b] = Mathf.Round(Range(10f, 45f) * _u);
                _bandShift[b] = Mathf.Round(Range(10f, 40f) * _u) * (_random.Next(2) == 0 ? -1f : 1f);
            }
            StartFx(Fx.Tear, t, Range(0.06f, 0.12f), 7, false);
        }

        private void ApplyTear()
        {
            for (int i = 0; i < _tearables.Length; i++)
            {
                float shift = 0f;
                for (int b = 0; b < _bandCount; b++)
                {
                    if (Mathf.Abs(_tearBase[i].y - _bandY[b]) < _bandHalf[b] + _tearHalf[i])
                    {
                        shift = _bandShift[b];
                    }
                }
                _tearables[i].anchoredPosition = _tearBase[i] + new Vector2(shift, 0f);
            }

            float edge = Mathf.Max(1f, Mathf.Round(_u * 1.5f));
            for (int b = 0; b < TearBands; b++)
            {
                bool on = b < _bandCount;
                _tearFills[b].gameObject.SetActive(on);
                _tearEdges[b * 2].gameObject.SetActive(on);
                _tearEdges[b * 2 + 1].gameObject.SetActive(on);
                if (!on)
                {
                    continue;
                }
                float w = _laidOutSize.x + 4f;
                Place(_tearFills[b].rectTransform, 0f, _bandY[b], w, _bandHalf[b] * 2f);
                Place(_tearEdges[b * 2].rectTransform, 0f, _bandY[b] + _bandHalf[b], w, edge);
                Place(_tearEdges[b * 2 + 1].rectTransform, 0f, _bandY[b] - _bandHalf[b], w, edge);
            }
        }

        /// <summary>The wordmark, and with <paramref name="onLabel"/> the current line, splits into two colours.</summary>
        private void ShowSplit(bool onLabel)
        {
            float shift = Mathf.Round(Range(4f, 8f) * _u);
            Vector2 wordmark = _wordmark.rectTransform.anchoredPosition;
            _wordmarkGhost.rectTransform.anchoredPosition = wordmark + new Vector2(shift, 0f);
            _wordmarkSplit.rectTransform.anchoredPosition = wordmark - new Vector2(Mathf.Round(shift * 0.8f), Mathf.Round(_u));
            _wordmarkGhost.enabled = _wordmarkSplit.enabled = true;
            if (!onLabel)
            {
                return;
            }
            Vector2 label = _currentLabel.rectTransform.anchoredPosition;
            _labelGhost.text = _labelSplit.text = _currentLabel.text;
            _labelGhost.maxVisibleCharacters = _labelSplit.maxVisibleCharacters = _currentLabel.maxVisibleCharacters;
            _labelGhost.rectTransform.anchoredPosition = label + new Vector2(shift, 0f);
            _labelSplit.rectTransform.anchoredPosition = label - new Vector2(Mathf.Round(shift * 0.8f), 0f);
            _labelGhost.enabled = _labelSplit.enabled = true;
        }

        /// <summary>A few characters of the current line turn to noise for a moment.</summary>
        private bool StartCorrupt(float t, int maxCharacters)
        {
            string clean = _currentLabel.text;
            if ((_fx & Fx.Corrupt) != 0 || clean.Length < 5 || _currentLabel.maxVisibleCharacters < clean.Length)
            {
                return false;
            }
            char[] chars = clean.ToCharArray();
            int count = _random.Next(1, maxCharacters + 1);
            for (int k = 0; k < count; k++)
            {
                int i = _random.Next(2, chars.Length);
                if (chars[i] != ' ')
                {
                    chars[i] = CorruptGlyphs[_random.Next(CorruptGlyphs.Length)];
                }
            }
            _labelClean = clean;
            _labelCorrupt = new string(chars);
            _currentLabel.text = _labelCorrupt;
            StartFx(Fx.Corrupt, t, Range(0.05f, 0.1f), 6, false);
            return true;
        }

        /// <summary>One ring snaps back against its spin, then catches up.</summary>
        private void KickRing(float t)
        {
            _ringKickWhich = _random.Next(2);
            float spin = _ringKickWhich < _partCount ? _partSpin[_ringKickWhich] : 1f;
            _ringKick = -15f * Mathf.Sign(spin);
            _ringKickAt = t;
        }

        private float RingKick(float t, int which)
        {
            float age = t - _ringKickAt;
            if (which != _ringKickWhich || _ringKick == 0f || age >= RingKickTime)
            {
                return 0f;
            }
            float k = 1f - age / RingKickTime;
            return _ringKick * k * k;
        }

        /// <summary>
        /// The closing sequence, once the menu's frames are smooth: now and then ACCESS DENIED
        /// and a retry first, then the grant line, then the unit's finale (a glitch burst,
        /// the stamp, a shine). True when it is over and the fade can start.
        /// </summary>
        private bool UpdateReveal(float t)
        {
            float age = t - _revealAt;
            if (!_variant.FakeDenial)
            {
                if (_revealStage == 0)
                {
                    _revealStage = 3;
                    _finaleAt = t;
                }
            }
            else
            {
                switch (_revealStage)
                {
                    case 0:
                        _revealStage = 1;
                        ShowLabel(Words("> ДОСТУП ЗАПРЕЩЁН", "> AUTHENTICATION FAILED"), Warn);
                        SetSegments(Warn);
                        StartTear(t, true);
                        KickRing(t);
                        break;
                    case 1 when age >= DeniedFor:
                        _revealStage = 2;
                        EndFx();
                        ShowLabel(Words("> ПОВТОРНАЯ ПОПЫТКА...", "> RETRYING..."), Dim);
                        SetSegments(Faint);
                        break;
                    case 2 when age >= DeniedFor + RetryFor:
                        _revealStage = 3;
                        EndFx();
                        ShowLabel("> " + _grantText, Accent);
                        SetSegments(Accent);
                        // The finale once the line has typed out.
                        _finaleAt = t + _currentLabel.text.Length / TypeSpeed + 0.05f;
                        break;
                }
            }

            if (_revealStage == 3 && t >= _finaleAt)
            {
                _revealStage = 4;
                StartFinale(t);
            }
            float hold = _variant.FakeDenial ? GrantedHold : 0f;
            return _revealStage == 4 && t - _finaleAt >= _finaleLength + hold;
        }

        private void StartFinale(float t)
        {
            _finaleLength = 0f;
            if (_variant.Glitches != GlitchLevel.Off)
            {
                Burst(t);
                _finaleLength = BurstFor;
            }
            switch (_variant.Outro)
            {
                case OutroKind.Flash:
                    _sheenAt = t;
                    _finaleLength = Mathf.Max(_finaleLength, 0.35f);
                    break;
            }
        }

        /// <summary>The finale's one flourish: a quick colour split over the wordmark and the grant line.</summary>
        private void Burst(float t)
        {
            StartFx(Fx.Split, t, 0.16f, 10, true);
        }

        /// <summary>The menu never settled: skip the sequence, but end on the grant line.</summary>
        private void FinishReveal()
        {
            EndFx();
            string grant = "> " + _grantText;
            if (_currentLabel.text != grant)
            {
                ShowLabel(grant, Accent);
            }
            _currentLabel.maxVisibleCharacters = grant.Length;
            SetSegments(Accent);
        }

        private void ShowLabel(string text, Color32 color)
        {
            _currentLabel.text = text;
            _currentLabel.color = color;
            _currentLabel.maxVisibleCharacters = 0;
            _stepShownAt = _animClock;
        }

        private void SetSegments(Color32 color)
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                _segments[i].color = color;
            }
            _solidFill.color = color;
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
    }
}

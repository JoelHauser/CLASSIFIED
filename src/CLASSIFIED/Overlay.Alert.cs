using UnityEngine;
using UnityEngine.UI;

namespace CLASSIFIED
{
    /// <summary>
    /// The countdown alert (F12 > Raid Loading > Countdown Alert, off by default): over a raid
    /// load's final countdown, so nobody misses the raid starting, the step's row pulses in the
    /// warning colour over a line that drains as the time runs out, and a frame round the screen
    /// pulses brighter towards zero, quicker over the last seconds.
    /// </summary>
    internal partial class Overlay
    {
        private Image _alertLine;
        private readonly Image[] _alertEdges = new Image[4];
        private bool _alertShown;
        private float _alertLineX;
        private float _alertLineWidth;

        /// <summary>The last seconds of the countdown, when the pulse quickens.</summary>
        private const float AlertHurryAt = 3f;

        private void BuildCountdownAlert(Transform readout, Transform content)
        {
            _alertLine = MakeImage("AlertLine", readout, Warn);
            for (int i = 0; i < _alertEdges.Length; i++)
            {
                _alertEdges[i] = MakeImage("AlertEdge" + i, content, Warn);
                RectTransform edge = _alertEdges[i].rectTransform;
                // Top, bottom, left, right: each stretched along its own side.
                bool horizontal = i < 2;
                float side = i == 0 || i == 3 ? 1f : 0f;
                edge.anchorMin = horizontal ? new Vector2(0f, side) : new Vector2(side, 0f);
                edge.anchorMax = horizontal ? new Vector2(1f, side) : new Vector2(side, 1f);
                edge.pivot = horizontal ? new Vector2(0.5f, side) : new Vector2(side, 0.5f);
                edge.anchoredPosition = Vector2.zero;
            }
            ShowCountdownAlert(false);
        }

        /// <summary>Under the running step's row, the bar's full width; the edges a few pixels thick.</summary>
        private void LayoutCountdownAlert(float rx, float barWidth, float rowY, float u)
        {
            _alertLineX = rx - barWidth * 0.5f;
            _alertLineWidth = barWidth;
            float thick = Mathf.Max(2f, Mathf.Round(2f * u));
            Place(_alertLine.rectTransform, rx, Mathf.Round(rowY - 16f * u), barWidth, thick);
            float edge = Mathf.Max(3f, Mathf.Round(5f * u));
            for (int i = 0; i < _alertEdges.Length; i++)
            {
                _alertEdges[i].rectTransform.sizeDelta = i < 2 ? new Vector2(0f, edge) : new Vector2(edge, 0f);
            }
        }

        /// <summary>Every frame: on through the countdown, when switched on; off otherwise.</summary>
        private void UpdateCountdownAlert(float t)
        {
            int current = StartupTracker.Current;
            bool on = ClassifiedPlugin.CountdownAlert.Value && _phase == Phase.Loading && Deploying
                && current == (int)StepId.DeployCountdown;
            if (!on)
            {
                if (_alertShown)
                {
                    ShowCountdownAlert(false);
                    if (_phase == Phase.Loading)
                    {
                        _currentLabel.color = Text;
                        _currentTime.color = Text;
                    }
                }
                return;
            }
            if (!_alertShown)
            {
                ShowCountdownAlert(true);
            }

            float total = Mathf.Max(0.5f, StartupTracker.ExpectedSeconds(current));
            float left = Mathf.Max(0f, total - StartupTracker.StepElapsed);
            float rate = left > AlertHurryAt ? 1.5f : 4f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * rate * Mathf.PI * 2f);
            float near = 1f - Mathf.Clamp01(left / Mathf.Min(total, 10f));

            // The line drains from the right, like a fuse.
            float width = Mathf.Round(_alertLineWidth * Mathf.Clamp01(left / total));
            RectTransform line = _alertLine.rectTransform;
            line.sizeDelta = new Vector2(width, line.sizeDelta.y);
            line.anchoredPosition = new Vector2(Mathf.Round(_alertLineX + width * 0.5f), line.anchoredPosition.y);
            _alertLine.canvasRenderer.SetAlpha(0.7f + 0.3f * pulse);

            _currentLabel.color = Color32.Lerp(Text, Warn, 0.45f + 0.55f * pulse);
            _currentTime.color = Warn;

            float edgeAlpha = (0.15f + 0.55f * near) * (0.45f + 0.55f * pulse);
            for (int i = 0; i < _alertEdges.Length; i++)
            {
                _alertEdges[i].canvasRenderer.SetAlpha(edgeAlpha);
            }
        }

        private void ShowCountdownAlert(bool show)
        {
            _alertShown = show;
            _alertLine.gameObject.SetActive(show);
            for (int i = 0; i < _alertEdges.Length; i++)
            {
                _alertEdges[i].gameObject.SetActive(show);
            }
        }

        /// <summary>The alert in the unit's warning colour, after a roll or a crossfade.</summary>
        private void ColourCountdownAlert()
        {
            _alertLine.color = Warn;
            for (int i = 0; i < _alertEdges.Length; i++)
            {
                _alertEdges[i].color = Warn;
            }
        }
    }
}

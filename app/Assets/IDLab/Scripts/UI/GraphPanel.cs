using System.Collections.Generic;
using IDLab.Core;
using IDLab.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>
    /// Right-hand "Live Data" panel: one card per data channel (every probe publishes one), each with the
    /// current value and a scrolling chart of value vs. simulated time.
    /// </summary>
    public sealed class GraphPanel : MonoBehaviour
    {
        private sealed class Card
        {
            public DataChannel Channel;
            public RectTransform Root;
            public Text Value;
            public LineGraph Graph;
            public Text YMax, YMin, XMin, XMax;
            public Color Color;
        }

        private readonly List<Card> _cards = new List<Card>();
        private LabWorld _world;
        private RectTransform _content;
        private Text _empty;
        private Text _windowButtonLabel;
        private int _paletteIndex;

        /// <summary>Visible time window in seconds; 0 = show everything since t = 0.</summary>
        public double WindowSeconds { get; private set; } = 120;

        public void Initialize(LabWorld world, RectTransform panel)
        {
            _world = world;

            var header = UIFactory.Rect("Header", panel);
            UIFactory.Place(header, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(0, 44));
            header.anchorMax = new Vector2(1, 1);
            header.offsetMin = new Vector2(14, -44);
            header.offsetMax = new Vector2(-10, -6);
            UIFactory.HorizontalLayout(header, 8, new RectOffset(0, 0, 0, 0));
            var title = UIFactory.Label(header, "LIVE DATA", UITheme.FontTitle, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Layout(title, -1, 1);
            var windowButton = UIFactory.Button(header, "2 min", ToggleWindow, null, UITheme.FontSmall);
            UIFactory.Layout(windowButton, -1, -1, 64);
            _windowButtonLabel = windowButton.GetComponentInChildren<Text>();
            var clear = UIFactory.Button(header, "Clear", () => _world.Logger.Clear(), null, UITheme.FontSmall);
            UIFactory.Layout(clear, -1, -1, 64);

            _content = UIFactory.Rect("Cards", panel);
            _content.anchorMin = Vector2.zero;
            _content.anchorMax = Vector2.one;
            _content.offsetMin = new Vector2(10, 10);
            _content.offsetMax = new Vector2(-10, -50);
            UIFactory.VerticalLayout(_content, 10, new RectOffset(0, 0, 0, 0));

            _empty = UIFactory.Label(_content, "No sensors on the bench.\nDrag a thermometer or pH probe into a container to start logging.",
                UITheme.FontBody, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.Layout(_empty, 60);

            foreach (var channel in _world.Logger.Channels)
            {
                AddCard(channel);
            }

            _world.Logger.ChannelAdded += AddCard;
            _world.Logger.ChannelRemoved += RemoveCard;
        }

        private void OnDestroy()
        {
            if (_world != null)
            {
                _world.Logger.ChannelAdded -= AddCard;
                _world.Logger.ChannelRemoved -= RemoveCard;
            }
        }

        private void ToggleWindow()
        {
            WindowSeconds = WindowSeconds > 0 ? 0 : 120;
            _windowButtonLabel.text = WindowSeconds > 0 ? "2 min" : "All";
        }

        private void AddCard(DataChannel channel)
        {
            var card = new Card { Channel = channel, Color = UITheme.SeriesPalette[_paletteIndex++ % UITheme.SeriesPalette.Length] };
            card.Root = UIFactory.Panel("Card " + channel.Id, _content, UITheme.PanelRaised);
            UIFactory.Layout(card.Root, 230);

            var label = UIFactory.Label(card.Root, channel.Label, UITheme.FontSmall, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.Place(label.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -8), new Vector2(240, 20));
            card.Value = UIFactory.Label(card.Root, "--", UITheme.FontValue, card.Color, TextAnchor.UpperRight, FontStyle.Bold);
            UIFactory.Place(card.Value.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -4), new Vector2(160, 34));

            var graphRect = UIFactory.Rect("Graph", card.Root);
            graphRect.anchorMin = Vector2.zero;
            graphRect.anchorMax = Vector2.one;
            graphRect.offsetMin = new Vector2(46, 26);
            graphRect.offsetMax = new Vector2(-12, -44);
            card.Graph = graphRect.gameObject.AddComponent<LineGraph>();
            card.Graph.Series = channel.Series;
            card.Graph.LineColor = card.Color;
            card.Graph.raycastTarget = false;

            card.YMax = AxisLabel(card.Root, new Vector2(0, 1), new Vector2(1, 1), new Vector2(42, -44), 40, TextAnchor.UpperRight);
            card.YMin = AxisLabel(card.Root, new Vector2(0, 0), new Vector2(1, 0), new Vector2(42, 26), 40, TextAnchor.LowerRight);
            card.XMin = AxisLabel(card.Root, new Vector2(0, 0), new Vector2(0, 0), new Vector2(46, 6), 80, TextAnchor.LowerLeft);
            card.XMax = AxisLabel(card.Root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 6), 80, TextAnchor.LowerRight);
            var unit = AxisLabel(card.Root, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(17, 6), 200, TextAnchor.LowerCenter);
            unit.text = $"time (s)  ·  {channel.Unit}";

            _cards.Add(card);
            _empty.gameObject.SetActive(false);
        }

        private static Text AxisLabel(Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position, float width, TextAnchor alignment)
        {
            var t = UIFactory.Label(parent, "", 11, UITheme.TextMuted, alignment);
            UIFactory.Place(t.rectTransform, anchor, pivot, position, new Vector2(width, 16));
            return t;
        }

        private void RemoveCard(DataChannel channel)
        {
            int i = _cards.FindIndex(c => c.Channel == channel);
            if (i < 0)
            {
                return;
            }

            Destroy(_cards[i].Root.gameObject);
            _cards.RemoveAt(i);
            _empty.gameObject.SetActive(_cards.Count == 0);
        }

        private void LateUpdate()
        {
            double now = _world.Time;
            double xMax = WindowSeconds > 0 ? System.Math.Max(WindowSeconds, now) : System.Math.Max(10, now);
            double xMin = WindowSeconds > 0 ? System.Math.Max(0, xMax - WindowSeconds) : 0;

            foreach (var card in _cards)
            {
                var s = card.Channel.Series;
                double yMin = card.Channel.SuggestedMin, yMax = card.Channel.SuggestedMax;
                if (s.TryGetRange(xMin, out double lo, out double hi))
                {
                    yMin = System.Math.Min(yMin, System.Math.Floor(lo / 10) * 10);
                    yMax = System.Math.Max(yMax, System.Math.Ceiling(hi / 10) * 10);
                }

                card.Graph.SetWindow(xMin, xMax, yMin, yMax);
                card.Value.text = s.Count > 0 ? $"{s.LastValue:0.0} <size=14>{card.Channel.Unit}</size>" : "--";
                card.YMax.text = $"{yMax:0}";
                card.YMin.text = $"{yMin:0}";
                card.XMin.text = $"{xMin:0}";
                card.XMax.text = $"{xMax:0}";
            }
        }
    }
}

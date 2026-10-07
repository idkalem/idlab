using System.Collections.Generic;
using System.Linq;
using IDLab.Core.Model;
using UnityEngine;
using UnityEngine.UI;

namespace IDLab.App.UI
{
    /// <summary>
    /// Builds the whole heads-up display in code:
    ///   top bar (experiment selector, simulation speed, clock, panel toggles) ·
    ///   left inventory · right live-data graphs · bottom-left micro view inset ·
    ///   bottom-centre inspector for the selected item · toasts for hazards.
    /// </summary>
    public sealed class LabHud : MonoBehaviour
    {
        private const float TopBarHeight = 56f;
        private const float InventoryWidth = 270f;
        private const float DataWidth = 400f;

        private LabApp _app;
        private RectTransform _canvasRect;
        private RectTransform _inventoryPanel;
        private RectTransform _inventoryList;
        private ScrollRect _inventoryScroll;
        private RectTransform _dataPanel;
        private RectTransform _microPanel;
        private Text _experimentTitle;
        private Text _clock;
        private Text _objectives;
        private readonly List<(Button button, float scale)> _speedButtons = new List<(Button, float)>();
        private List<ExperimentDefinition> _playable;

        public InspectorPanel Inspector { get; private set; }
        public ToastPresenter Toasts { get; private set; }

        public void Initialize(LabApp app)
        {
            _app = app;
            _playable = app.Experiments.All.Where(e => e.Status == ExperimentStatus.Playable).ToList();
            UIFactory.EnsureEventSystem();

            var canvasGo = new GameObject("HUD", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvasRect = (RectTransform)canvasGo.transform;

            BuildTopBar();
            BuildInventory();
            BuildDataPanel();
            BuildMicroPanel();

            Inspector = UIFactory.Rect("Inspector", _canvasRect).gameObject.AddComponent<InspectorPanel>();
            Inspector.Initialize(app);
            Toasts = UIFactory.Rect("Toasts", _canvasRect).gameObject.AddComponent<ToastPresenter>();
            Toasts.Initialize(TopBarHeight + 12);

            app.ExperimentLoaded += OnExperimentLoaded;
            OnExperimentLoaded(app.CurrentExperiment);
            SetSpeed(1f);
        }

        public bool IsOverInventory(Vector2 screenPosition) =>
            _inventoryPanel.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(_inventoryPanel, screenPosition, null);

        // ------------------------------------------------------------------ top bar

        private void BuildTopBar()
        {
            var bar = UIFactory.Panel("TopBar", _canvasRect, UITheme.TopBar, rounded: false);
            bar.anchorMin = new Vector2(0, 1);
            bar.anchorMax = new Vector2(1, 1);
            bar.pivot = new Vector2(0.5f, 1);
            bar.sizeDelta = new Vector2(0, TopBarHeight);
            UIFactory.HorizontalLayout(bar, 8, new RectOffset(16, 16, 9, 9));

            var logo = UIFactory.Label(bar, "<color=#35C4FF>ID</color> LAB", 24, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Layout(logo, -1, -1, 110);

            UIFactory.Layout(UIFactory.Button(bar, "<", () => CycleExperiment(-1)), -1, -1, 38);
            _experimentTitle = UIFactory.Label(bar, "", UITheme.FontTitle, UITheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIFactory.Layout(_experimentTitle, -1, -1, 330);
            UIFactory.Layout(UIFactory.Button(bar, ">", () => CycleExperiment(1)), -1, -1, 38);

            var spacer = UIFactory.Rect("Spacer", bar);
            UIFactory.Layout(spacer, -1, 1);

            foreach (var (label, scale) in new[] { ("Pause", 0f), ("1x", 1f), ("5x", 5f), ("20x", 20f) })
            {
                float s = scale;
                var b = UIFactory.Button(bar, label, () => SetSpeed(s));
                UIFactory.Layout(b, -1, -1, label == "Pause" ? 70 : 52);
                _speedButtons.Add((b, s));
            }

            _clock = UIFactory.Label(bar, "t = 0.0 s", UITheme.FontBody, UITheme.TextMuted, TextAnchor.MiddleCenter);
            UIFactory.Layout(_clock, -1, -1, 120);

            UIFactory.Layout(UIFactory.Button(bar, "Reset", () => _app.LoadExperiment(_app.CurrentExperiment.Id), UITheme.AccentDim), -1, -1, 74);
            UIFactory.Layout(UIFactory.Button(bar, "Equipment", () => Toggle(_inventoryPanel)), -1, -1, 104);
            UIFactory.Layout(UIFactory.Button(bar, "Data", () => Toggle(_dataPanel)), -1, -1, 64);
            UIFactory.Layout(UIFactory.Button(bar, "Micro", () => Toggle(_microPanel)), -1, -1, 70);
        }

        private void CycleExperiment(int direction)
        {
            int i = _playable.IndexOf(_app.CurrentExperiment);
            i = (i + direction + _playable.Count) % _playable.Count;
            _app.LoadExperiment(_playable[i].Id);
        }

        private void SetSpeed(float scale)
        {
            _app.World.Clock.Paused = scale <= 0f;
            if (scale > 0f)
            {
                _app.World.Clock.TimeScale = scale;
            }

            foreach (var (button, s) in _speedButtons)
            {
                bool active = scale <= 0f ? s <= 0f : Mathf.Approximately(s, scale);
                button.GetComponent<Image>().color = active ? UITheme.Accent * 0.75f : UITheme.Button;
            }
        }

        private static void Toggle(RectTransform panel) => panel.gameObject.SetActive(!panel.gameObject.activeSelf);

        // ------------------------------------------------------------------ inventory

        private void BuildInventory()
        {
            _inventoryPanel = UIFactory.Panel("Inventory", _canvasRect, UITheme.Panel);
            _inventoryPanel.anchorMin = new Vector2(0, 0);
            _inventoryPanel.anchorMax = new Vector2(0, 1);
            _inventoryPanel.pivot = new Vector2(0, 0.5f);
            _inventoryPanel.offsetMin = new Vector2(10, 10);
            _inventoryPanel.offsetMax = new Vector2(10 + InventoryWidth, -TopBarHeight - 10);

            var title = UIFactory.Label(_inventoryPanel, "EQUIPMENT", UITheme.FontTitle, UITheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIFactory.Place(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -8), new Vector2(240, 28));
            var hint = UIFactory.Label(_inventoryPanel, "Drag onto the bench · tap to place · drop back here to remove",
                UITheme.FontSmall - 1, UITheme.TextMuted, TextAnchor.UpperLeft);
            UIFactory.Place(hint.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -36), new Vector2(InventoryWidth - 24, 32));

            var viewport = UIFactory.Rect("Viewport", _inventoryPanel);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(8, 170);
            viewport.offsetMax = new Vector2(-8, -72);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0); // raycast target for scrolling

            _inventoryList = UIFactory.Rect("List", viewport);
            _inventoryList.anchorMin = new Vector2(0, 1);
            _inventoryList.anchorMax = new Vector2(1, 1);
            _inventoryList.pivot = new Vector2(0.5f, 1);
            _inventoryList.sizeDelta = Vector2.zero;
            UIFactory.VerticalLayout(_inventoryList, 6, new RectOffset(2, 2, 2, 2));
            var fitter = _inventoryList.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _inventoryScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _inventoryScroll.content = _inventoryList;
            _inventoryScroll.viewport = viewport;
            _inventoryScroll.horizontal = false;
            _inventoryScroll.movementType = ScrollRect.MovementType.Clamped;
            _inventoryScroll.scrollSensitivity = 30f;

            var objectivesBox = UIFactory.Panel("Objectives", _inventoryPanel, UITheme.PanelRaised);
            objectivesBox.anchorMin = new Vector2(0, 0);
            objectivesBox.anchorMax = new Vector2(1, 0);
            objectivesBox.pivot = new Vector2(0.5f, 0);
            objectivesBox.offsetMin = new Vector2(8, 8);
            objectivesBox.offsetMax = new Vector2(-8, 162);
            _objectives = UIFactory.Label(objectivesBox, "", UITheme.FontSmall, UITheme.Text, TextAnchor.UpperLeft);
            UIFactory.Stretch(_objectives.rectTransform, 10);
        }

        private void RebuildInventory()
        {
            for (int i = _inventoryList.childCount - 1; i >= 0; i--)
            {
                Destroy(_inventoryList.GetChild(i).gameObject);
            }

            foreach (var group in _app.InventoryForCurrentExperiment().GroupBy(d => d.Category).OrderBy(g => g.Key))
            {
                var header = UIFactory.Label(_inventoryList, CategoryName(group.Key).ToUpperInvariant(), 11, UITheme.Accent, TextAnchor.LowerLeft, FontStyle.Bold);
                UIFactory.Layout(header, 22);
                foreach (var definition in group)
                {
                    BuildInventoryRow(definition);
                }
            }
        }

        private void BuildInventoryRow(EquipmentDefinition definition)
        {
            var row = UIFactory.Panel(definition.Id, _inventoryList, UITheme.PanelRaised);
            UIFactory.Layout(row, 54);
            row.gameObject.GetComponent<Image>().raycastTarget = true;

            var swatch = UIFactory.Panel("Swatch", row, definition.AccentColor.ToUnity());
            UIFactory.Place(swatch, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(6, 38));

            var name = UIFactory.Label(row, definition.DisplayName, UITheme.FontBody, UITheme.Text, TextAnchor.UpperLeft, FontStyle.Bold);
            UIFactory.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -7), new Vector2(InventoryWidth - 50, 20));
            var detail = UIFactory.Label(row, definition.Description, 11, UITheme.TextMuted, TextAnchor.UpperLeft);
            detail.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIFactory.Place(detail.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -28), new Vector2(InventoryWidth - 50, 18));
            row.gameObject.AddComponent<RectMask2D>();

            row.gameObject.AddComponent<InventoryItem>().Initialize(_app, definition, _inventoryScroll);
        }

        private static string CategoryName(EquipmentCategory c)
        {
            switch (c)
            {
                case EquipmentCategory.HeatSource: return "Heat sources";
                case EquipmentCategory.Sensor: return "Sensors";
                case EquipmentCategory.Reagent: return "Reagents";
                default: return c.ToString();
            }
        }

        // ------------------------------------------------------------------ data + micro panels

        private void BuildDataPanel()
        {
            _dataPanel = UIFactory.Panel("LiveData", _canvasRect, UITheme.Panel);
            _dataPanel.anchorMin = new Vector2(1, 0);
            _dataPanel.anchorMax = new Vector2(1, 1);
            _dataPanel.pivot = new Vector2(1, 0.5f);
            _dataPanel.offsetMin = new Vector2(-10 - DataWidth, 10);
            _dataPanel.offsetMax = new Vector2(-10, -TopBarHeight - 10);
            _dataPanel.gameObject.AddComponent<GraphPanel>().Initialize(_app.World, _dataPanel);
        }

        private void BuildMicroPanel()
        {
            _microPanel = UIFactory.Panel("MicroView", _canvasRect, UITheme.Panel);
            UIFactory.Place(_microPanel, new Vector2(0, 0), new Vector2(0, 0), new Vector2(InventoryWidth + 22, 10), new Vector2(340, 330));
            _app.Micro.BuildPanel(_microPanel);
        }

        // ------------------------------------------------------------------ per-frame

        private void OnExperimentLoaded(ExperimentDefinition experiment)
        {
            if (experiment == null)
            {
                return;
            }

            _experimentTitle.text = experiment.Title;
            var text = $"<b>{experiment.Title}</b>\n<color=#8A9BB4>{experiment.Summary}</color>";
            if (experiment.Objectives.Count > 0)
            {
                text += "\n" + string.Join("\n", experiment.Objectives.Select((o, i) => $"{i + 1}. {o}"));
            }

            _objectives.text = text;
            RebuildInventory();
        }

        private void Update()
        {
            var clock = _app.World.Clock;
            _clock.text = clock.Paused ? $"t = {_app.World.Time:0.0} s  ||" : $"t = {_app.World.Time:0.0} s";
        }

        private void OnDestroy()
        {
            if (_app != null)
            {
                _app.ExperimentLoaded -= OnExperimentLoaded;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sea.Client
{
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class SeaHudController : MonoBehaviour
    {
        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private SeaGameController game;
        private ISeaInputPort input;

        private VisualElement root;
        private VisualElement hudRoot;
        private VisualElement connectionBeacon;
        private VisualElement targetFrame;
        private VisualElement fireControl;
        private VisualElement coordinateNavigator;
        private VisualElement chartMenu;
        private VisualElement miniMapFrame;
        private VisualElement wreckPrompt;
        private ScrollView rebindList;
        private TextField coordinateInput;
        private Label coordinateError;
        private Camera chartCamera;
        private SeaChartCameraController chartCameraController;
        private bool recenterOffered;
        // One slot per square of the map on each edge, so a chart pulled out reads 1 to 40.
        private readonly Label[] topCoordinateLabels = new Label[SeaChartCoordinates.ColumnCount];
        private readonly Label[] leftCoordinateLabels = new Label[SeaChartCoordinates.RowCount];

        // The racks never hold more than the hull's magazine plus the rigging bonus.
        private readonly VisualElement[] magazineDots = new VisualElement[6];
        private Label windArrow;

        // Apply() touches ~26 named elements per rebuild; memoising the query results
        // keeps the HUD off the visual-tree walk once the document is built.
        private readonly Dictionary<string, Label> labelCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ProgressBar> progressCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttonCache = new(StringComparer.Ordinal);

        public void Configure(StyleSheet hudStyleSheet)
        {
            styleSheet = hudStyleSheet;
        }

        private void OnEnable()
        {
            InitializeDocument();
            BindHudEvents(connection);
        }

        private void OnDisable() => BindHudEvents(null);

        private void Start() => InitializeDocument();

        public void ConfigureDependencies(
            SeaConnectionController connectionController,
            SeaGameController gameController,
            ISeaInputPort inputPort)
        {
            connection = connectionController;
            game = gameController;
            input = inputPort;
            BindHudEvents(connectionController);
            hudDirty.Mark();
        }

        private void Update()
        {
            if (root == null)
            {
                return;
            }

            if (hudDirty.TryConsume())
            {
                using (HudMarker.Auto())
                {
                    Apply(SeaHudViewModel.From(CaptureSnapshot()));
                }
            }

            if (CameraRulersChanged())
            {
                using (MinimapMarker.Auto())
                {
                    UpdateCoordinateRulers();
                }
            }

            OfferRecenter();
        }

        // The recenter tool lights up only while the camera has been pushed off the ship.
        private void OfferRecenter()
        {
            var offer = chartCameraController != null && !chartCameraController.IsFollowingPlayer;
            if (offer != recenterOffered)
            {
                recenterOffered = offer;
                SelectButton("recenter-button", offer);
            }
        }

        // The minimap camera renders inside the HUD frame, whatever the panel scale is.
        private void FitMiniMapCamera()
        {
            var miniMapCamera = GetComponent<SeaChartCameraController>()?.MiniMapCamera;
            if (miniMapCamera == null || root?.panel == null)
            {
                return;
            }

            var bound = miniMapFrame.worldBound;
            var style = miniMapFrame.resolvedStyle;
            var inner = new Rect(
                bound.x + style.borderLeftWidth,
                bound.y + style.borderTopWidth,
                bound.width - style.borderLeftWidth - style.borderRightWidth,
                bound.height - style.borderTopWidth - style.borderBottomWidth);
            miniMapCamera.pixelRect = SeaMiniMapRules.ScreenPixelRect(
                inner,
                root.panel.visualTree.worldBound.height,
                Screen.height);
        }

        public bool IsPointerOverInterface(Vector2 screenPosition)
        {
            if (root?.panel == null)
            {
                return false;
            }

            var panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, screenPosition);
            var picked = root.panel.Pick(panelPosition);
            return picked != null && picked != root && picked != hudRoot;
        }

        public void OpenCoordinateNavigator()
        {
            if (coordinateNavigator == null || input?.IsMenuOpen == true)
            {
                return;
            }

            coordinateError.text = string.Empty;
            coordinateNavigator.RemoveFromClassList("hidden");
            coordinateInput.Focus();
            coordinateInput.SelectAll();
        }

        public void SetMenuVisible(bool visible)
        {
            if (chartMenu == null)
            {
                return;
            }

            chartMenu.EnableInClassList("hidden", !visible);
            if (visible)
            {
                coordinateNavigator?.AddToClassList("hidden");
                PopulateRebindList();
            }
        }

        private void InitializeDocument()
        {
            var document = GetComponent<UIDocument>();
            if (document == null || document.rootVisualElement == null)
            {
                return;
            }

            root = document.rootVisualElement;
            if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            {
                root.styleSheets.Add(styleSheet);
            }

            hudRoot = root.Q("sea-hud");
            if (hudRoot == null || hudRoot.userData != null)
            {
                return;
            }

            hudRoot.userData = this;
            labelCache.Clear();
            progressCache.Clear();
            buttonCache.Clear();
            ammoLabelsApplied = false;
            root.pickingMode = PickingMode.Ignore;
            hudRoot.pickingMode = PickingMode.Ignore;
            connectionBeacon = root.Q("connection-beacon");
            targetFrame = root.Q("target-frame");
            wreckPrompt = root.Q("wreck-prompt");
            fireControl = root.Q("fire-control");
            coordinateNavigator = root.Q("coordinate-navigator");
            chartMenu = root.Q("chart-menu");
            miniMapFrame = root.Q("mini-map-frame");
            miniMapFrame.RegisterCallback<GeometryChangedEvent>(_ => FitMiniMapCamera());
            rebindList = root.Q<ScrollView>("rebind-list");
            coordinateInput = root.Q<TextField>("coordinate-input");
            coordinateError = root.Q<Label>("coordinate-error");
            chartCamera = Camera.main;
            chartCameraController = GetComponent<SeaChartCameraController>();
            for (var index = 0; index < topCoordinateLabels.Length; index++)
            {
                topCoordinateLabels[index] = root.Q<Label>($"top-coordinate-{index}");
            }

            for (var index = 0; index < leftCoordinateLabels.Length; index++)
            {
                leftCoordinateLabels[index] = root.Q<Label>($"left-coordinate-{index}");
            }

            for (var index = 0; index < magazineDots.Length; index++)
            {
                magazineDots[index] = root.Q($"magazine-dot-{index}");
            }

            windArrow = root.Q<Label>("wind-arrow");

            HookButton("recenter-button", () => chartCameraController?.Recenter());
            HookButton("navigator-button", OpenCoordinateNavigator);
            HookButton("menu-button", () => input?.SetMenuOpen(true));
            HookButton("menu-close", () => input?.SetMenuOpen(false));
            HookButton("resume-button", () => input?.SetMenuOpen(false));
            HookButton("coordinate-submit", SubmitCoordinate);
            HookButton("coordinate-cancel", CloseCoordinateNavigator);
            HookButton("ammo-round", () => game?.SetSelectedAmmo("round"));
            HookButton("ammo-chain", () => game?.SetSelectedAmmo("chain"));
            HookButton("ammo-grapeshot", () => game?.SetSelectedAmmo("grapeshot"));
            HookButton("ammo-incendiary", () => game?.SetSelectedAmmo("incendiary"));
            HookButton("fire-control", () => game?.Fire());
            HookButton("repair", () => game?.ToggleRepair());
            HookButton("repair-kit", () => game?.UseRepairKit());
            HookButton("respawn-button", () => game?.ChooseHomePortRespawn());
            coordinateInput.RegisterCallback<KeyDownEvent>(HandleCoordinateKey);
            hudDirty.Mark();
        }

        private void Apply(SeaHudViewModel model)
        {
            SetText("connection-status", model.ConnectionStatus.ToUpperInvariant());
            SetText("navigation-readout", model.NavigationText);
            SetText("map-rank-label", model.MapRankText);
            SetText("gold-label", model.GoldText);
            SetText("ship-loadout", model.ShipText);
            SetText("volley-text", model.VolleyText);
            SetText("combat-power-label", model.CombatPowerText);
            SetText("hull-text", model.HullText);
            SetText("last-action", model.LastAction);
            SetProgress("player-hull", model.HullProgress);
            connectionBeacon?.EnableInClassList("ready", model.IsReady);

            targetFrame?.EnableInClassList("hidden", !model.HasTarget);
            if (model.HasTarget)
            {
                SetText("target-name", model.TargetName);
                SetText("target-range", model.TargetRangeText);
                SetText("target-hull-text", model.TargetHullText);
                SetText("target-armor-text", model.TargetArmorText);
                SetProgress("target-hull", model.TargetHullProgress);
            }

            SetProgress("reload-gauge", model.ReloadProgress);
            SetText("reload-text", model.ReloadText);
            SetText("magazine-text", model.MagazineText);
            ApplyMagazineDots(model);
            ApplyWind(model);
            fireControl?.EnableInClassList("ready", model.IsLoaded);
            SetText("ammo-count", $"{model.SelectedAmmoLabel} • {model.AmmoQuantity}");
            SelectButton("ammo-round", model.SelectedAmmo == "ROUND");
            SelectButton("ammo-chain", model.SelectedAmmo == "CHAIN");
            SelectButton("ammo-grapeshot", model.SelectedAmmo == "GRAPESHOT");
            SelectButton("ammo-incendiary", model.SelectedAmmo == "INCENDIARY");
            SetText("status-text", model.StatusText);
            SetText("channel-label", string.IsNullOrWhiteSpace(model.ProgressText) ? "NO ACTIVE ORDER" : model.ProgressText);
            SetProgress("channel-progress", model.Progress);
            SetAbilityCooldown("repair", "R", model.RepairCooldownSeconds);
            SetAbilityCooldown("repair-kit", "K", model.RepairKitCooldownSeconds);

            // A wreck has one order left to give, so the chart is covered until it gives it.
            wreckPrompt?.EnableInClassList("hidden", !model.IsSunk);
            SetText("wreck-text", model.WreckText);
            ButtonFor("respawn-button")?.EnableInClassList("hidden", !model.CanChooseBerth);
        }

        /// <summary>
        /// A dot per volley the racks hold, filled while that volley can leave. Counting dots is
        /// faster in a fight than reading "2 / 3", so the pair stays only as the exact figure.
        /// </summary>
        private void ApplyMagazineDots(SeaHudViewModel model)
        {
            for (var index = 0; index < magazineDots.Length; index++)
            {
                var dot = magazineDots[index];
                if (dot == null)
                {
                    continue;
                }

                dot.EnableInClassList("hidden", index >= model.MagazineSize);
                dot.EnableInClassList("loaded", index < model.ReadyVolleys);
            }
        }

        private void ApplyWind(SeaHudViewModel model)
        {
            SetText("wind-text", model.WindText);
            if (windArrow != null)
            {
                windArrow.style.rotate = new StyleRotate(
                    new Rotate(new Angle(model.WindRotationDegrees, AngleUnit.Degree)));
            }
        }

        private void SetAbilityCooldown(string name, string binding, float seconds)
        {
            var button = ButtonFor(name);
            if (button == null)
            {
                return;
            }

            button.text = seconds <= 0f ? binding : $"{seconds:0.0}";
            button.SetEnabled(seconds <= 0f);
        }

        private void PopulateRebindList()
        {
            if (rebindList == null || input == null)
            {
                return;
            }

            rebindList.Clear();
            foreach (var binding in input.GetRebindableBindings())
            {
                var row = new VisualElement();
                row.AddToClassList("rebind-row");
                var label = new Label(binding.Label);
                label.AddToClassList("rebind-action");
                var button = new Button { text = binding.DisplayPath };
                button.AddToClassList("chart-button");
                button.AddToClassList("rebind-button");
                var captured = binding;
                button.clicked += () =>
                {
                    button.text = "PRESS A KEY…";
                    input.StartInteractiveRebind(
                        captured.ActionName,
                        captured.BindingIndex,
                        displayPath => button.text = displayPath);
                };
                row.Add(label);
                row.Add(button);
                rebindList.Add(row);
            }

            var reset = new Button { text = "RESET ALL BINDINGS" };
            reset.AddToClassList("chart-button");
            reset.clicked += () =>
            {
                input.ResetBindingOverrides();
                PopulateRebindList();
            };
            rebindList.Add(reset);
        }

        private void SubmitCoordinate()
        {
            var coordinate = coordinateInput.value.Trim().ToUpperInvariant();
            var error = "Chart controls are not ready.";
            if (game != null && game.TryNavigateToCoordinate(coordinate, out error))
            {
                CloseCoordinateNavigator();
            }
            else
            {
                coordinateError.text = error;
            }
        }

        private void CloseCoordinateNavigator()
        {
            coordinateNavigator?.AddToClassList("hidden");
        }

        private void HandleCoordinateKey(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter)
            {
                SubmitCoordinate();
                keyEvent.StopPropagation();
            }
            else if (keyEvent.keyCode == KeyCode.Escape)
            {
                CloseCoordinateNavigator();
                keyEvent.StopPropagation();
            }
        }

        private void HookButton(string name, Action callback)
        {
            var button = ButtonFor(name);
            if (button != null)
            {
                button.clicked += callback;
            }
        }

        private void SetText(string name, string value)
        {
            var label = LabelFor(name);
            if (label != null && !string.Equals(label.text, value, StringComparison.Ordinal))
            {
                label.text = value;
            }
        }

        private void SetProgress(string name, float value)
        {
            var progress = ProgressFor(name);
            if (progress != null && !Mathf.Approximately(progress.value, value))
            {
                progress.value = value;
            }
        }

        private void SelectButton(string name, bool selected)
        {
            ButtonFor(name)?.EnableInClassList("selected", selected);
        }

        private Label LabelFor(string name) => Cached(labelCache, name);

        private ProgressBar ProgressFor(string name) => Cached(progressCache, name);

        private Button ButtonFor(string name) => Cached(buttonCache, name);

        private TElement Cached<TElement>(Dictionary<string, TElement> cache, string name)
            where TElement : VisualElement
        {
            if (cache.TryGetValue(name, out var element))
            {
                return element;
            }

            element = root?.Q<TElement>(name);
            cache[name] = element;
            return element;
        }
    }
}

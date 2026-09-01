using System;
using System.Collections.Generic;
using System.Linq;
using SpacetimeDB.Types;
using UnityEngine;
using UnityEngine.UIElements;

namespace Sea.Client
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SeaHudController : MonoBehaviour
    {
        private const float RefreshIntervalSeconds = 0.1f;

        [SerializeField] private StyleSheet styleSheet;
        [SerializeField] private SeaConnectionController connection;
        [SerializeField] private SeaGameController game;
        [SerializeField] private SeaInputController input;

        private VisualElement root;
        private VisualElement hudRoot;
        private VisualElement connectionBeacon;
        private VisualElement targetFrame;
        private VisualElement portBroadside;
        private VisualElement starboardBroadside;
        private VisualElement coordinateNavigator;
        private VisualElement chartMenu;
        private VisualElement miniMapFrame;
        private ScrollView rebindList;
        private TextField coordinateInput;
        private Label coordinateError;
        private Camera chartCamera;
        private readonly Label[] topCoordinateLabels = new Label[9];
        private readonly Label[] leftCoordinateLabels = new Label[7];
        private float nextRefreshTime;

        public void Configure(StyleSheet hudStyleSheet)
        {
            styleSheet = hudStyleSheet;
        }

        private void Awake()
        {
            RefreshReferences();
        }

        private void OnEnable()
        {
            InitializeDocument();
        }

        private void Start()
        {
            RefreshReferences();
            InitializeDocument();
        }

        private void Update()
        {
            if (root == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
            Apply(SeaHudViewModel.From(CaptureSnapshot()));
            UpdateCoordinateRulers();
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
            root.pickingMode = PickingMode.Ignore;
            hudRoot.pickingMode = PickingMode.Ignore;
            connectionBeacon = root.Q("connection-beacon");
            targetFrame = root.Q("target-frame");
            portBroadside = root.Q("port-broadside");
            starboardBroadside = root.Q("starboard-broadside");
            coordinateNavigator = root.Q("coordinate-navigator");
            chartMenu = root.Q("chart-menu");
            miniMapFrame = root.Q("mini-map-frame");
            rebindList = root.Q<ScrollView>("rebind-list");
            coordinateInput = root.Q<TextField>("coordinate-input");
            coordinateError = root.Q<Label>("coordinate-error");
            chartCamera = Camera.main;
            for (var index = 0; index < topCoordinateLabels.Length; index++)
            {
                topCoordinateLabels[index] = root.Q<Label>($"top-coordinate-{index}");
            }

            for (var index = 0; index < leftCoordinateLabels.Length; index++)
            {
                leftCoordinateLabels[index] = root.Q<Label>($"left-coordinate-{index}");
            }

            HookButton("navigator-button", OpenCoordinateNavigator);
            HookButton("menu-button", () => input?.SetMenuOpen(true));
            HookButton("menu-close", () => input?.SetMenuOpen(false));
            HookButton("resume-button", () => input?.SetMenuOpen(false));
            HookButton("coordinate-submit", SubmitCoordinate);
            HookButton("coordinate-cancel", CloseCoordinateNavigator);
            HookButton("aim-hull", () => game?.SetSelectedWeakPoint("hull"));
            HookButton("aim-sails", () => game?.SetSelectedWeakPoint("sails"));
            HookButton("aim-cannons", () => game?.SetSelectedWeakPoint("cannons"));
            HookButton("ammo-round", () => game?.SetSelectedAmmo("round"));
            HookButton("ammo-chain", () => game?.SetSelectedAmmo("chain"));
            HookButton("ammo-grapeshot", () => game?.SetSelectedAmmo("grapeshot"));
            HookButton("ammo-incendiary", () => game?.SetSelectedAmmo("incendiary"));
            HookButton("port-broadside", () => game?.FireBroadside("port"));
            HookButton("starboard-broadside", () => game?.FireBroadside("starboard"));
            HookButton("ability-full-sail", () => game?.ActivateAbility("full_sail"));
            HookButton("ability-brace", () => game?.ActivateAbility("brace"));
            HookButton("ability-pump", () => game?.ActivateAbility("emergency_pump"));
            HookButton("ability-smoke", () => game?.ActivateAbility("smoke_screen"));
            HookButton("repair", () => game?.ToggleRepair());
            HookButton("board", () => game?.ToggleBoarding());
            coordinateInput.RegisterCallback<KeyDownEvent>(HandleCoordinateKey);
            Apply(SeaHudViewModel.From(CaptureSnapshot()));
        }

        private SeaHudSnapshot CaptureSnapshot()
        {
            var snapshot = new SeaHudSnapshot
            {
                IsReady = connection?.IsSubscribed == true,
                ConnectionStatus = connection?.Status ?? "CONTROLLER MISSING",
                LastAction = game?.LastAction ?? "Waiting for chart link.",
            };

            if (game == null || !game.TryGetLocalShip(out var ship))
            {
                return snapshot;
            }

            snapshot.Coordinate = SeaChartCoordinates.LabelAt(ship.PositionX, ship.PositionY);
            snapshot.HeadingDegrees = ship.HeadingDegrees;
            snapshot.Speed = ship.Speed;
            snapshot.Hull = ship.Hull;
            snapshot.MaxHull = ship.MaxHull;
            snapshot.SelectedAmmo = game.SelectedAmmoId;
            snapshot.SelectedWeakPoint = game.SelectedWeakPoint;

            var progression = connection.Connection.Db.PlayerProgression.Owner.Find(connection.LocalIdentity);
            if (progression != null)
            {
                snapshot.Level = progression.Level;
                snapshot.Experience = progression.Experience;
                snapshot.Gold = progression.Gold;
                foreach (var definition in connection.Connection.Db.LevelDefinition.Iter())
                {
                    if (definition.Level == progression.Level)
                    {
                        snapshot.CurrentLevelExperience = definition.RequiredExperience;
                    }
                    else if (definition.Level == progression.Level + 1)
                    {
                        snapshot.NextLevelExperience = definition.RequiredExperience;
                    }
                }

                if (snapshot.NextLevelExperience == 0)
                {
                    snapshot.NextLevelExperience = Math.Max(snapshot.Experience, snapshot.CurrentLevelExperience);
                }
            }

            snapshot.AmmoQuantity = connection.Connection.Db.Inventory.ByShip
                .Filter(ship.EntityId)
                .FirstOrDefault(item => item.ItemId == game.SelectedAmmoId)?.Quantity ?? 0;

            var world = connection.Connection.Db.WorldState.Id.Find(1);
            if (world != null)
            {
                var tickRate = Math.Max(1u, world.TickRateHz);
                snapshot.ReloadDurationSeconds = (float)ship.CannonCooldownTicks / tickRate;
                snapshot.PortReloadRemainingSeconds = RemainingSeconds(ship.NextPortFireTick, world.Tick, tickRate);
                snapshot.StarboardReloadRemainingSeconds = RemainingSeconds(ship.NextStarboardFireTick, world.Tick, tickRate);
            }

            var targetId = ship.TargetEntityId != 0 ? ship.TargetEntityId : game.SelectedTargetId;
            var target = targetId == 0 ? null : connection.Connection.Db.Ship.EntityId.Find(targetId);
            if (target != null && target.IsAlive)
            {
                snapshot.TargetName = $"{target.ArchetypeId.ToUpperInvariant()}  {target.EntityId}";
                snapshot.TargetHull = target.Hull;
                snapshot.TargetMaxHull = target.MaxHull;
                snapshot.TargetSails = target.Sails;
                snapshot.TargetMaxSails = target.MaxSails;
                snapshot.TargetCannons = target.Cannons;
                snapshot.TargetMaxCannons = target.MaxCannons;
                var range = Vector2.Distance(
                    new Vector2(ship.PositionX, ship.PositionY),
                    new Vector2(target.PositionX, target.PositionY));
                snapshot.TargetRange = range;
            }

            var statuses = new List<string>();
            foreach (var status in connection.Connection.Db.ShipStatus.ByShip.Filter(ship.EntityId))
            {
                if (status.IsActive)
                {
                    statuses.Add(status.Stacks > 1
                        ? $"{status.StatusType.ToUpperInvariant()} ×{status.Stacks}"
                        : status.StatusType.ToUpperInvariant());
                }
            }

            snapshot.StatusText = statuses.Count == 0 ? "CLEAR" : string.Join("  •  ", statuses);
            if (world != null)
            {
                var channel = connection.Connection.Db.ShipChannel.ShipEntityId.Find(ship.EntityId);
                if (channel != null && channel.IsActive)
                {
                    snapshot.ProgressText = channel.ChannelType == "repair"
                        ? "REPAIRING"
                        : $"BOARDING  •  TARGET {channel.TargetEntityId}";
                    snapshot.Progress = SeaTacticalPresentationRules.ChannelProgress(
                        channel.StartedAtTick,
                        channel.CompletesAtTick,
                        world.Tick);
                }

                foreach (var cooldown in connection.Connection.Db.Cooldown.ByShip.Filter(ship.EntityId))
                {
                    var seconds = RemainingSeconds(
                        cooldown.ReadyAtTick,
                        world.Tick,
                        Math.Max(1u, world.TickRateHz));
                    switch (cooldown.CooldownType)
                    {
                        case "full_sail":
                            snapshot.FullSailCooldownSeconds = seconds;
                            break;
                        case "brace":
                            snapshot.BraceCooldownSeconds = seconds;
                            break;
                        case "emergency_pump":
                            snapshot.PumpCooldownSeconds = seconds;
                            break;
                        case "smoke_screen":
                            snapshot.SmokeCooldownSeconds = seconds;
                            break;
                    }
                }
            }

            return snapshot;
        }

        private void Apply(SeaHudViewModel model)
        {
            SetText("connection-status", model.ConnectionStatus.ToUpperInvariant());
            SetText("navigation-readout", model.NavigationText);
            SetText("level-label", model.LevelText);
            SetText("gold-label", model.GoldText + " ¤");
            SetText("hull-text", model.HullText);
            SetText("experience-text", model.ExperienceText);
            SetText("last-action", model.LastAction);
            SetProgress("player-hull", model.HullProgress);
            SetProgress("player-experience", model.ExperienceProgress);
            connectionBeacon?.EnableInClassList("ready", model.IsReady);

            targetFrame?.EnableInClassList("hidden", !model.HasTarget);
            if (model.HasTarget)
            {
                SetText("target-name", model.TargetName);
                SetText("target-range", model.TargetRangeText);
                SetText("target-hull-text", model.TargetHullText);
                SetText("target-sails-text", model.TargetSailsText);
                SetText("target-cannons-text", model.TargetCannonsText);
                SetProgress("target-hull", model.TargetHullProgress);
                SetProgress("target-sails", model.TargetSailsProgress);
                SetProgress("target-cannons", model.TargetCannonsProgress);
            }

            SetProgress("port-reload", model.PortReloadProgress);
            SetProgress("starboard-reload", model.StarboardReloadProgress);
            SetText("port-reload-text", model.PortReloadText);
            SetText("starboard-reload-text", model.StarboardReloadText);
            portBroadside?.EnableInClassList("ready", model.PortReady);
            starboardBroadside?.EnableInClassList("ready", model.StarboardReady);
            SetText("ammo-count", $"{model.SelectedAmmo} • {model.AmmoQuantity}");
            SelectButton("aim-hull", model.SelectedWeakPoint == "HULL");
            SelectButton("aim-sails", model.SelectedWeakPoint == "SAILS");
            SelectButton("aim-cannons", model.SelectedWeakPoint == "CANNONS");
            SelectButton("ammo-round", model.SelectedAmmo == "ROUND");
            SelectButton("ammo-chain", model.SelectedAmmo == "CHAIN");
            SelectButton("ammo-grapeshot", model.SelectedAmmo == "GRAPESHOT");
            SelectButton("ammo-incendiary", model.SelectedAmmo == "INCENDIARY");
            SetText("status-text", model.StatusText);
            SetText("channel-label", string.IsNullOrWhiteSpace(model.ProgressText) ? "NO ACTIVE ORDER" : model.ProgressText);
            SetProgress("channel-progress", model.Progress);
            SetAbilityCooldown("ability-full-sail", "Z", model.FullSailCooldownSeconds);
            SetAbilityCooldown("ability-brace", "X", model.BraceCooldownSeconds);
            SetAbilityCooldown("ability-pump", "C", model.PumpCooldownSeconds);
            SetAbilityCooldown("ability-smoke", "V", model.SmokeCooldownSeconds);
        }

        private void SetAbilityCooldown(string name, string binding, float seconds)
        {
            var button = root?.Q<Button>(name);
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
            var button = root.Q<Button>(name);
            if (button != null)
            {
                button.clicked += callback;
            }
        }

        private void SetText(string name, string value)
        {
            var label = root.Q<Label>(name);
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }

        private void SetProgress(string name, float value)
        {
            var progress = root.Q<ProgressBar>(name);
            if (progress != null && !Mathf.Approximately(progress.value, value))
            {
                progress.value = value;
            }
        }

        private void SelectButton(string name, bool selected)
        {
            root.Q<Button>(name)?.EnableInClassList("selected", selected);
        }

        private void RefreshReferences()
        {
            connection ??= FindFirstObjectByType<SeaConnectionController>();
            game ??= FindFirstObjectByType<SeaGameController>();
            input ??= FindFirstObjectByType<SeaInputController>();
        }

        private void UpdateCoordinateRulers()
        {
            if (chartCamera == null)
            {
                return;
            }

            for (var index = 0; index < topCoordinateLabels.Length; index++)
            {
                var viewportX = 0.04f + 0.74f * index / (topCoordinateLabels.Length - 1);
                if (TryChartPoint(new Vector2(viewportX, 0.96f), out var point))
                {
                    topCoordinateLabels[index].text = SeaChartCoordinates.LabelAt(point.x, point.z)
                        .Split(' ')[1];
                }
            }

            for (var index = 0; index < leftCoordinateLabels.Length; index++)
            {
                var viewportY = 0.16f + 0.76f * index / (leftCoordinateLabels.Length - 1);
                if (TryChartPoint(new Vector2(0.03f, viewportY), out var point))
                {
                    leftCoordinateLabels[leftCoordinateLabels.Length - 1 - index].text =
                        SeaChartCoordinates.LabelAt(point.x, point.z).Split(' ')[0];
                }
            }
        }

        private bool TryChartPoint(Vector2 viewportPosition, out Vector3 point)
        {
            var ray = chartCamera.ViewportPointToRay(viewportPosition);
            var plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out var distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private static float RemainingSeconds(ulong readyTick, ulong currentTick, uint tickRate) =>
            readyTick <= currentTick ? 0f : (float)(readyTick - currentTick) / tickRate;
    }
}

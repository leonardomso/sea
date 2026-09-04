using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sea.Client
{
    public sealed class SeaInputController : MonoBehaviour, ISeaInputPort
    {
        private const string RebindPreferenceKey = "sea.controls.binding-overrides";

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private SeaGameController game;
        [SerializeField] private SeaChartCameraController chartCamera;
        [SerializeField] private SeaHudController hud;

        private readonly List<(InputAction Action, Action<InputAction.CallbackContext> Callback, bool Canceled)> callbacks = new();
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private Vector2 pointerPosition;

        // The fire key is held far more often than it is tapped, so the repeat is driven from
        // Update rather than from an input interaction: the racks decide, not the keyboard.
        private bool fireHeld;
        private float secondsSinceFireRequest = SeaFireRepeatRules.MinimumIntervalSeconds;

        public bool IsMenuOpen { get; private set; }
        public InputActionAsset Actions => actions;

        private void Awake()
        {
            if (actions != null)
            {
                InitializeActions();
            }
        }

        private void OnEnable()
        {
            if (actions != null && callbacks.Count == 0)
            {
                InitializeActions();
            }
        }

        public void Configure(InputActionAsset inputActions)
        {
            UnbindActions();
            actions = inputActions;
            InitializeActions();
        }

        public void ConfigureDependencies(
            SeaGameController gameController,
            SeaChartCameraController cameraController,
            SeaHudController hudController)
        {
            game = gameController;
            chartCamera = cameraController;
            hud = hudController;
        }

        public void SetMenuOpen(bool isOpen)
        {
            IsMenuOpen = isOpen;
            fireHeld = false;
            if (actions != null)
            {
                actions.Disable();
                actions.FindActionMap(isOpen ? "Menu" : "Gameplay", throwIfNotFound: true).Enable();
            }

            hud?.SetMenuVisible(isOpen);
        }

        public IReadOnlyList<SeaBindingEntry> GetRebindableBindings()
        {
            var entries = new List<SeaBindingEntry>();
            if (actions == null)
            {
                return entries;
            }

            foreach (var action in actions.FindActionMap("Gameplay", throwIfNotFound: true).actions)
            {
                for (var index = 0; index < action.bindings.Count; index++)
                {
                    var binding = action.bindings[index];
                    if (binding.isComposite || binding.path == "<Mouse>/position")
                    {
                        continue;
                    }

                    var label = string.IsNullOrWhiteSpace(binding.name)
                        ? Humanize(action.name)
                        : $"{Humanize(action.name)} • {binding.name.ToUpperInvariant()}";
                    entries.Add(new SeaBindingEntry(
                        action.name,
                        index,
                        label,
                        action.GetBindingDisplayString(index)));
                }
            }

            return entries;
        }

        public void StartInteractiveRebind(
            string actionName,
            int bindingIndex,
            Action<string> completed)
        {
            if (actions == null || rebindOperation != null)
            {
                return;
            }

            var action = actions.FindActionMap("Gameplay", throwIfNotFound: true)
                .FindAction(actionName, throwIfNotFound: true);
            rebindOperation = action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnComplete(operation => CompleteRebind(action, bindingIndex, operation, completed))
                .OnCancel(operation => CompleteRebind(action, bindingIndex, operation, completed));
            rebindOperation.Start();
        }

        public void SaveBindingOverrides()
        {
            if (actions != null)
            {
                PlayerPrefs.SetString(RebindPreferenceKey, actions.SaveBindingOverridesAsJson());
                PlayerPrefs.Save();
            }
        }

        public void ResetBindingOverrides()
        {
            actions?.RemoveAllBindingOverrides();
            PlayerPrefs.DeleteKey(RebindPreferenceKey);
        }

        private void InitializeActions()
        {
            if (actions == null)
            {
                return;
            }

            var savedOverrides = PlayerPrefs.GetString(RebindPreferenceKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(savedOverrides))
            {
                actions.LoadBindingOverridesFromJson(savedOverrides);
            }

            BindActions();
            SetMenuOpen(false);
        }

        private void BindActions()
        {
            if (callbacks.Count != 0)
            {
                return;
            }

            Bind("Point", context =>
            {
                pointerPosition = context.ReadValue<Vector2>();
                chartCamera?.DragTo(pointerPosition);
            });
            Bind("SetCourse", _ =>
            {
                if (chartCamera?.TryShowMiniMapPosition(pointerPosition) == true)
                {
                }
                else if (hud == null || !hud.IsPointerOverInterface(pointerPosition))
                {
                    game?.HandlePrimaryClick(pointerPosition);
                }
            });
            Bind("StopCourse", _ => game?.StopCourse());
            Bind("PanChart", context => chartCamera?.SetPanInput(context.ReadValue<Vector2>()));
            Bind("PanChart", _ => chartCamera?.SetPanInput(Vector2.zero), canceled: true);
            Bind("DragChart", _ => chartCamera?.BeginDrag(pointerPosition));
            Bind("DragChart", _ => chartCamera?.EndDrag(), canceled: true);
            Bind("ZoomChart", context => chartCamera?.Zoom(context.ReadValue<Vector2>().y));
            Bind("RecenterChart", _ => chartCamera?.Recenter());
            Bind("OpenNavigator", _ => hud?.OpenCoordinateNavigator());
            Bind("CycleTargetNext", _ =>
            {
                if (Keyboard.current == null || !Keyboard.current.shiftKey.isPressed)
                {
                    game?.SelectNextEnemy();
                }
            });
            Bind("CycleTargetPrevious", _ => game?.SelectNextEnemy(-1));
            // Escape drops the aim first and only opens the menu once there is no aim to drop,
            // so a captain in a fight never has the menu thrown over the chart.
            Bind("ClearTarget", _ => ClearTargetOrOpenMenu());
            BindMenu("CloseMenu", _ => SetMenuOpen(false));

            // Guns bear in every direction: one key, one magazine, no aim point.
            Bind("Fire", _ => RequestFire());
            Bind("Fire", _ => fireHeld = false, canceled: true);
            Bind("AmmoRound", _ => game?.SetSelectedAmmo("round"));
            Bind("AmmoChain", _ => game?.SetSelectedAmmo("chain"));
            Bind("AmmoGrapeshot", _ => game?.SetSelectedAmmo("grapeshot"));
            Bind("AmmoIncendiary", _ => game?.SetSelectedAmmo("incendiary"));
            Bind("Repair", _ => game?.ToggleRepair());
            Bind("RepairKit", _ => game?.UseRepairKit());

            // Section 1.1 keeps these keys bound so they appear in the rebinder and answer when
            // pressed; the answer, for now, is that the milestone has not reached them.
            BindUnavailable("Board", "Boarding");
            BindUnavailable("Ram", "Ramming");
            BindUnavailable("Ability1", "Abilities");
            BindUnavailable("Ability2", "Abilities");
            BindUnavailable("Ability3", "Abilities");
            BindUnavailable("Ability4", "Abilities");
            BindUnavailable("PvpFlag", "The PvP flag");
        }

        private void ClearTargetOrOpenMenu()
        {
            if (game != null && game.SelectedTargetId != 0)
            {
                game.ClearTarget();
                return;
            }

            SetMenuOpen(true);
        }

        private void RequestFire()
        {
            fireHeld = true;
            secondsSinceFireRequest = 0f;
            game?.Fire();
        }

        /// <summary>
        /// A held key is a request to keep firing, not a stream of commands: the repeat waits for
        /// the racks to reload and for the module's own minimum interval to pass.
        /// </summary>
        private void Update()
        {
            secondsSinceFireRequest += Time.unscaledDeltaTime;
            if (SeaFireRepeatRules.ShouldRepeat(
                    fireHeld && !IsMenuOpen,
                    game != null && game.CanFireNow,
                    secondsSinceFireRequest))
            {
                secondsSinceFireRequest = 0f;
                game.Fire();
            }
        }

        private void BindUnavailable(string actionName, string feature) =>
            Bind(actionName, _ => game?.ReportUnavailable(feature));

        private void Bind(string actionName, Action<InputAction.CallbackContext> callback, bool canceled = false)
        {
            var action = actions.FindActionMap("Gameplay", throwIfNotFound: true)
                .FindAction(actionName, throwIfNotFound: true);
            if (canceled)
            {
                action.canceled += callback;
            }
            else
            {
                action.performed += callback;
            }

            callbacks.Add((action, callback, canceled));
        }

        private void BindMenu(string actionName, Action<InputAction.CallbackContext> callback)
        {
            var action = actions.FindActionMap("Menu", throwIfNotFound: true)
                .FindAction(actionName, throwIfNotFound: true);
            action.performed += callback;
            callbacks.Add((action, callback, false));
        }

        private void UnbindActions()
        {
            foreach (var binding in callbacks)
            {
                if (binding.Canceled)
                {
                    binding.Action.canceled -= binding.Callback;
                }
                else
                {
                    binding.Action.performed -= binding.Callback;
                }
            }

            callbacks.Clear();
            actions?.Disable();
        }

        private void CompleteRebind(
            InputAction action,
            int bindingIndex,
            InputActionRebindingExtensions.RebindingOperation operation,
            Action<string> completed)
        {
            operation.Dispose();
            rebindOperation = null;
            SaveBindingOverrides();
            SetMenuOpen(true);
            completed?.Invoke(action.GetBindingDisplayString(bindingIndex));
        }

        private static string Humanize(string value)
        {
            var result = string.Empty;
            for (var index = 0; index < value.Length; index++)
            {
                if (index > 0 && char.IsUpper(value[index]) && !char.IsUpper(value[index - 1]))
                {
                    result += " ";
                }

                result += char.ToUpperInvariant(value[index]);
            }

            return result;
        }

        private void OnDisable()
        {
            rebindOperation?.Cancel();
            UnbindActions();
        }
    }
}

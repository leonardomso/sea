using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sea.Client
{
    public readonly struct SeaBindingEntry
    {
        public SeaBindingEntry(string actionName, int bindingIndex, string label, string displayPath)
        {
            ActionName = actionName;
            BindingIndex = bindingIndex;
            Label = label;
            DisplayPath = displayPath;
        }

        public string ActionName { get; }
        public int BindingIndex { get; }
        public string Label { get; }
        public string DisplayPath { get; }
    }

    public sealed class SeaInputController : MonoBehaviour
    {
        private const string RebindPreferenceKey = "sea.controls.binding-overrides";

        [SerializeField] private InputActionAsset actions;
        [SerializeField] private SeaGameController game;
        [SerializeField] private SeaChartCameraController chartCamera;
        [SerializeField] private SeaHudController hud;

        private readonly List<(InputAction Action, Action<InputAction.CallbackContext> Callback, bool Canceled)> callbacks = new();
        private InputActionRebindingExtensions.RebindingOperation rebindOperation;
        private Vector2 pointerPosition;

        public bool IsMenuOpen { get; private set; }
        public InputActionAsset Actions => actions;

        private void Awake()
        {
            RefreshReferences();
            if (actions != null)
            {
                InitializeActions();
            }
        }

        private void Start()
        {
            RefreshReferences();
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

        public void SetMenuOpen(bool isOpen)
        {
            IsMenuOpen = isOpen;
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

            Bind("Point", context => pointerPosition = context.ReadValue<Vector2>());
            Bind("SetCourse", _ =>
            {
                if (hud == null || !hud.IsPointerOverInterface(pointerPosition))
                {
                    game?.HandlePrimaryClick(pointerPosition);
                }
            });
            Bind("StopCourse", _ => game?.StopCourse());
            Bind("PanChart", context => chartCamera?.SetPanInput(context.ReadValue<Vector2>()));
            Bind("PanChart", _ => chartCamera?.SetPanInput(Vector2.zero), canceled: true);
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
            Bind("ClearTarget", _ => game?.ClearTarget());
            Bind("Pause", _ => SetMenuOpen(true));
            BindMenu("CloseMenu", _ => SetMenuOpen(false));

            Bind("FirePort", _ => game?.RequestCombatIntent("Port broadside ordered."));
            Bind("FireStarboard", _ => game?.RequestCombatIntent("Starboard broadside ordered."));
            Bind("AimHull", _ => game?.SetSelectedWeakPoint("hull"));
            Bind("AimSails", _ => game?.SetSelectedWeakPoint("sails"));
            Bind("AimCannons", _ => game?.SetSelectedWeakPoint("cannons"));
            Bind("AmmoRound", _ => game?.SetSelectedAmmo("round"));
            Bind("AmmoChain", _ => game?.SetSelectedAmmo("chain"));
            Bind("AmmoGrapeshot", _ => game?.SetSelectedAmmo("grapeshot"));
            Bind("AmmoIncendiary", _ => game?.SetSelectedAmmo("incendiary"));
            Bind("FullSail", _ => game?.RequestCombatIntent("Full Sail ordered."));
            Bind("Brace", _ => game?.RequestCombatIntent("Brace ordered."));
            Bind("EmergencyPump", _ => game?.RequestCombatIntent("Emergency Pump ordered."));
            Bind("SmokeScreen", _ => game?.RequestCombatIntent("Smoke Screen ordered."));
            Bind("Repair", _ => game?.RequestCombatIntent("Repair order toggled."));
            Bind("Board", _ => game?.RequestCombatIntent("Boarding order toggled."));
        }

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

        private void RefreshReferences()
        {
            game ??= FindFirstObjectByType<SeaGameController>();
            chartCamera ??= FindFirstObjectByType<SeaChartCameraController>();
            hud ??= FindFirstObjectByType<SeaHudController>();
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

using System;
using System.Collections.Generic;

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

    public interface ISeaInputPort
    {
        bool IsMenuOpen { get; }
        void SetMenuOpen(bool isOpen);
        IReadOnlyList<SeaBindingEntry> GetRebindableBindings();
        void StartInteractiveRebind(string actionName, int bindingIndex, Action<string> completed);
        void ResetBindingOverrides();
    }
}

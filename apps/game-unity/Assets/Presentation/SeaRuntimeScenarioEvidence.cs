using System;

namespace Sea.Client
{
    [Serializable]
    public sealed class SeaRuntimeScenarioEvidence
    {
        public int schemaVersion = 1;
        public string recordedAtUtc;
        public bool movementRequired;
        public bool combatRequired;
        public bool progressionRequired;
        public bool tacticalRequired;
        public bool movementObserved;
        public bool combatObserved;
        public bool progressionObserved;
        public bool tacticalObserved;
        public int runtimeErrors;

        public bool IsComplete()
        {
            return schemaVersion == 1 &&
                (!movementRequired || movementObserved) &&
                (!combatRequired || combatObserved) &&
                (!progressionRequired || progressionObserved) &&
                (!tacticalRequired || tacticalObserved) &&
                runtimeErrors == 0;
        }
    }
}

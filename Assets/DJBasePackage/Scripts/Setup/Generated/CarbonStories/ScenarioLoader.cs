using System;
using System.Collections.Generic;

namespace CarbonStories
{
    /// <summary>
    /// Backward-compatible component for scenes that already reference ScenarioLoader.
    /// New code should use PersonaLoader and PersonaData.
    /// </summary>
    [Obsolete("Use PersonaLoader instead.")]
    public class ScenarioLoader : PersonaLoader
    {
        public void LoadPersona(
            Persona persona,
            Action<IReadOnlyList<ScenarioData>> onLoaded,
            Action<string> onError = null)
        {
            LoadPersonaData(persona, data => onLoaded?.Invoke(data.scenarios), onError);
        }

        public bool TryGetLoadedScenarios(Persona persona, out IReadOnlyList<ScenarioData> scenarios)
        {
            if (TryGetPersonaData(persona, out PersonaData data))
            {
                scenarios = data.scenarios;
                return true;
            }

            scenarios = null;
            return false;
        }
    }
}

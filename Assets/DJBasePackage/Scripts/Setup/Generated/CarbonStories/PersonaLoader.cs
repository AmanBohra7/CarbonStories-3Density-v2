using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace CarbonStories
{
    [Serializable]
    public class IntroductionPageData
    {
        public string heading;
        public string subheading;
        public string imagePath;
        public Sprite image;
    }

    [Serializable]
    public class IntroductionData
    {
        public IntroductionPageData page1 = new IntroductionPageData();
        public IntroductionPageData page2 = new IntroductionPageData();
        public IntroductionPageData page3 = new IntroductionPageData();
    }

    [Serializable]
    public class TutorialData
    {
        [TextArea] public string infoText;
    }

    [Serializable]
    public class PersonaData
    {
        public string name;
        public string designation;
        public IntroductionData introduction = new IntroductionData();
        public TutorialData tutorial = new TutorialData();
        public List<ScenarioData> scenarios = new List<ScenarioData>();
    }

    [Serializable]
    public class LoadedPersonaData
    {
        public Persona persona;
        public PersonaData data;
    }

    /// <summary>
    /// Preloads all persona data, option icons, and question images from StreamingAssets at startup.
    /// </summary>
    public class PersonaLoader : MonoBehaviour
    {
        [SerializeField] private string personasFolder = "Personas";
        [SerializeField, Min(1f)] private float spritePixelsPerUnit = 100f;
        [SerializeField] private bool loadOnStart = true;

        [Header("Runtime Loaded Data")]
        [Tooltip("Populated while the game is running so all loaded persona data can be inspected.")]
        [SerializeField] private List<LoadedPersonaData> inspectorPersonas =
            new List<LoadedPersonaData>();

        private readonly Dictionary<Persona, PersonaData> loadedPersonas =
            new Dictionary<Persona, PersonaData>();

        private readonly Dictionary<string, Sprite> loadedSprites =
            new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private bool isLoading;

        public IReadOnlyDictionary<Persona, PersonaData> Personas => loadedPersonas;
        public IReadOnlyList<LoadedPersonaData> InspectorPersonas => inspectorPersonas;
        public bool IsLoading => isLoading;
        public bool IsLoaded => loadedPersonas.Count == Enum.GetValues(typeof(Persona)).Length;

        public event Action AllPersonasLoaded;
        public event Action<Persona, string> PersonaLoadFailed;

        protected virtual void Awake()
        {
            loadedPersonas.Clear();
            inspectorPersonas.Clear();
        }

        protected virtual void Start()
        {
            if (loadOnStart)
                LoadAllPersonas();
        }

        public void LoadAllPersonas(
            Action<IReadOnlyDictionary<Persona, PersonaData>> onLoaded = null,
            Action<string> onError = null)
        {
            if (IsLoaded)
            {
                onLoaded?.Invoke(loadedPersonas);
                return;
            }

            if (isLoading)
            {
                onError?.Invoke("Persona data is already loading.");
                return;
            }

            StartCoroutine(LoadAllPersonasRoutine(onLoaded, onError));
        }

        public void LoadPersonaData(
            Persona persona,
            Action<PersonaData> onLoaded,
            Action<string> onError = null)
        {
            if (loadedPersonas.TryGetValue(persona, out PersonaData cached))
            {
                onLoaded?.Invoke(cached);
                return;
            }

            StartCoroutine(LoadPersonaRoutine(persona, onLoaded, onError));
        }

        public bool TryGetPersonaData(Persona persona, out PersonaData data)
        {
            return loadedPersonas.TryGetValue(persona, out data);
        }

        public PersonaData GetPersonaData(Persona persona)
        {
            if (loadedPersonas.TryGetValue(persona, out PersonaData data))
                return data;

            throw new InvalidOperationException(
                $"{persona} has not loaded yet. Wait for AllPersonasLoaded or check IsLoaded.");
        }

        public void ClearCache()
        {
            StopAllCoroutines();
            isLoading = false;
            loadedPersonas.Clear();
            inspectorPersonas.Clear();

            foreach (Sprite sprite in loadedSprites.Values)
            {
                if (sprite == null)
                    continue;

                Texture2D texture = sprite.texture;
                Destroy(sprite);
                Destroy(texture);
            }

            loadedSprites.Clear();
        }

        private IEnumerator LoadAllPersonasRoutine(
            Action<IReadOnlyDictionary<Persona, PersonaData>> onLoaded,
            Action<string> onError)
        {
            isLoading = true;

            foreach (Persona persona in Enum.GetValues(typeof(Persona)))
            {
                if (loadedPersonas.ContainsKey(persona))
                    continue;

                string loadError = null;
                yield return LoadPersonaRoutine(persona, null, error => loadError = error);

                if (!string.IsNullOrEmpty(loadError))
                {
                    isLoading = false;
                    PersonaLoadFailed?.Invoke(persona, loadError);
                    onError?.Invoke(loadError);
                    yield break;
                }
            }

            isLoading = false;
            AllPersonasLoaded?.Invoke();
            onLoaded?.Invoke(loadedPersonas);
        }

        private IEnumerator LoadPersonaRoutine(
            Persona persona,
            Action<PersonaData> onLoaded,
            Action<string> onError)
        {
            string fileName = persona.ToString().ToLowerInvariant() + ".json";
            string jsonPath = CombineStreamingAssetsPath(personasFolder, fileName);

            string json = null;
            string requestError = null;
            yield return LoadText(jsonPath, value => json = value, error => requestError = error);

            if (!string.IsNullOrEmpty(requestError))
            {
                Fail(requestError, onError);
                yield break;
            }

            PersonaData personaData;
            List<string[]> iconPaths;

            try
            {
                ParsePersona(json, out personaData, out iconPaths);
            }
            catch (Exception exception)
            {
                Fail($"Could not parse persona file '{jsonPath}': {exception.Message}", onError);
                yield break;
            }

            IntroductionPageData[] introductionPages = GetIntroductionPages(personaData.introduction);
            foreach (IntroductionPageData introductionPage in introductionPages)
            {
                if (introductionPage == null || string.IsNullOrWhiteSpace(introductionPage.imagePath))
                    continue;

                Sprite sprite = null;
                requestError = null;
                yield return LoadSprite(
                    CombineStreamingAssetsPath(introductionPage.imagePath),
                    value => sprite = value,
                    error => requestError = error);

                if (!string.IsNullOrEmpty(requestError))
                    Debug.LogWarning(requestError);
                else
                    introductionPage.image = sprite;
            }

            for (int scenarioIndex = 0; scenarioIndex < personaData.scenarios.Count; scenarioIndex++)
            {
                ScenarioData scenario = personaData.scenarios[scenarioIndex];
                Option[] options = GetOptions(scenario);

                if (!string.IsNullOrWhiteSpace(scenario.questionImagePath))
                {
                    Sprite questionImage = null;
                    requestError = null;
                    yield return LoadSprite(
                        CombineStreamingAssetsPath(scenario.questionImagePath),
                        value => questionImage = value,
                        error => requestError = error);

                    if (!string.IsNullOrEmpty(requestError))
                        Debug.LogWarning(requestError);
                    else
                        scenario.questionImage = questionImage;
                }

                for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
                {
                    string iconPath = iconPaths[scenarioIndex][optionIndex];
                    if (options[optionIndex] == null || string.IsNullOrWhiteSpace(iconPath))
                        continue;

                    Sprite sprite = null;
                    requestError = null;
                    yield return LoadSprite(
                        CombineStreamingAssetsPath(iconPath),
                        value => sprite = value,
                        error => requestError = error);

                    if (!string.IsNullOrEmpty(requestError))
                    {
                        Debug.LogWarning(requestError);
                        continue;
                    }

                    options[optionIndex].icon = sprite;
                }
            }

            StorePersona(persona, personaData);
            onLoaded?.Invoke(personaData);
        }

        private void StorePersona(Persona persona, PersonaData personaData)
        {
            loadedPersonas[persona] = personaData;

            LoadedPersonaData inspectorEntry = inspectorPersonas.Find(entry => entry.persona == persona);
            if (inspectorEntry == null)
            {
                inspectorEntry = new LoadedPersonaData { persona = persona };
                inspectorPersonas.Add(inspectorEntry);
                inspectorPersonas.Sort((left, right) => left.persona.CompareTo(right.persona));
            }

            inspectorEntry.data = personaData;
        }

        private static void ParsePersona(
            string json,
            out PersonaData personaData,
            out List<string[]> iconPaths)
        {
            JToken root = JToken.Parse(json);
            JObject personaJson;

            // Keep loading the previous array-only files during migration.
            if (root.Type == JTokenType.Array)
            {
                personaJson = new JObject
                {
                    ["name"] = string.Empty,
                    ["designation"] = string.Empty,
                    ["scenarios"] = root
                };
            }
            else
            {
                personaJson = root as JObject
                    ?? throw new FormatException("Persona JSON must be an object.");
                personaJson = (JObject)personaJson.DeepClone();
            }

            JArray scenarioTokens = personaJson["scenarios"] as JArray
                ?? throw new FormatException("Persona JSON must contain a 'scenarios' array.");

            iconPaths = new List<string[]>(scenarioTokens.Count);

            foreach (JToken token in scenarioTokens)
            {
                JObject scenarioJson = token as JObject
                    ?? throw new FormatException("Every scenario must be a JSON object.");

                string[] paths = new string[3];

                for (int index = 0; index < paths.Length; index++)
                {
                    JObject optionJson = scenarioJson[$"option{index + 1}"] as JObject;
                    if (optionJson == null)
                        continue;

                    paths[index] = optionJson.Value<string>("icon");
                    optionJson.Remove("icon");
                    ConvertColorToUnityRange(optionJson["scoreColor"] as JObject);
                }

                iconPaths.Add(paths);
            }

            // Question video paths deserialize with each scenario. Videos are streamed
            // by ScenarioScreen on demand; question images are preloaded above.
            personaData = personaJson.ToObject<PersonaData>()
                ?? throw new FormatException("Persona data could not be deserialized.");

            if (personaData.scenarios == null)
                personaData.scenarios = new List<ScenarioData>();

            if (personaData.introduction == null)
                personaData.introduction = new IntroductionData();

            if (personaData.tutorial == null)
                personaData.tutorial = new TutorialData();
        }

        private static void ConvertColorToUnityRange(JObject colorJson)
        {
            if (colorJson == null)
                return;

            ConvertColorChannel(colorJson, "r");
            ConvertColorChannel(colorJson, "g");
            ConvertColorChannel(colorJson, "b");
            ConvertColorChannel(colorJson, "a");
        }

        private static void ConvertColorChannel(JObject colorJson, string channel)
        {
            JToken value = colorJson[channel];
            if (value == null)
                return;

            float byteValue = Mathf.Clamp(value.Value<float>(), 0f, 255f);
            colorJson[channel] = byteValue / 255f;
        }

        private IEnumerator LoadText(string uri, Action<string> onLoaded, Action<string> onError)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(uri))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Could not load '{uri}': {request.error}");
                    yield break;
                }

                onLoaded?.Invoke(request.downloadHandler.text);
            }
        }

        private IEnumerator LoadSprite(string uri, Action<Sprite> onLoaded, Action<string> onError)
        {
            if (loadedSprites.TryGetValue(uri, out Sprite cached))
            {
                onLoaded?.Invoke(cached);
                yield break;
            }

            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(uri))
            {
                yield return request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"Could not load icon '{uri}': {request.error}");
                    yield break;
                }

                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    spritePixelsPerUnit);

                sprite.name = System.IO.Path.GetFileNameWithoutExtension(uri);
                loadedSprites[uri] = sprite;
                onLoaded?.Invoke(sprite);
            }
        }

        private string CombineStreamingAssetsPath(params string[] parts)
        {
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\');

            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;

                path += "/" + part.Trim('/', '\\').Replace('\\', '/');
            }

            if (path.Contains("://") || path.StartsWith("jar:", StringComparison.OrdinalIgnoreCase))
                return path;

            return new Uri(path).AbsoluteUri;
        }

        private static Option[] GetOptions(ScenarioData scenario)
        {
            return new[] { scenario.option1, scenario.option2, scenario.option3 };
        }

        private static IntroductionPageData[] GetIntroductionPages(IntroductionData introduction)
        {
            return new[] { introduction.page1, introduction.page2, introduction.page3 };
        }

        private static void Fail(string message, Action<string> onError)
        {
            Debug.LogError(message);
            onError?.Invoke(message);
        }

        protected virtual void OnDestroy()
        {
            ClearCache();
        }
    }
}

using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

namespace CarbonStories
{
    public enum Persona
    {
        Persona_1,
        Persona_2,
        Persona_3,
        Persona_4,
        Persona_5,
        Persona_6
    }
    public class App : MonoBehaviour
    {
        public static App Instance { get; private set; }
        public GameConfiguration Configuration { get; private set; } = new GameConfiguration();
        public ResultDescriptions ResultDescriptions { get; private set; } = new ResultDescriptions();
        private bool configurationLoaded;
        private bool configurationLoading;
        private AudioSource backgroundAudio;
        private AudioSource timerAudio;
        private bool timerRequested;

        public System.Collections.IEnumerator LoadConfiguration()
        {
            if (configurationLoading)
            {
                while (!configurationLoaded)
                    yield return null;
                yield break;
            }
            if (configurationLoaded)
                yield break;

            configurationLoading = true;
            yield return ConfigurationReader.Load<GameConfiguration>("config.json", data => Configuration = data);
            if (float.IsNaN(Configuration.questionWaitTime) || float.IsInfinity(Configuration.questionWaitTime) || Configuration.questionWaitTime <= 0f)
                Configuration.questionWaitTime = 60f;
            yield return ConfigurationReader.Load<ResultDescriptions>("Results/descriptions.json", data => ResultDescriptions = data);
            yield return LoadAudio(Configuration.backgroundMusic, backgroundAudio, true);
            yield return LoadAudio(Configuration.timerSound, timerAudio, false);
            configurationLoaded = true;
            configurationLoading = false;
        }

        private void Awake()
        {
            Instance = this;
            backgroundAudio = gameObject.AddComponent<AudioSource>();
            timerAudio = gameObject.AddComponent<AudioSource>();
            backgroundAudio.loop = true;
            timerAudio.loop = true;
            backgroundAudio.playOnAwake = false;
            timerAudio.playOnAwake = false;
            backgroundAudio.spatialBlend = 0f;
            timerAudio.spatialBlend = 0f;
            ApplyAudioSettings();
            StartCoroutine(LoadConfiguration());
        }

        public void ApplyAudioSettings()
        {
            if (backgroundAudio != null) backgroundAudio.mute = !SettingController.MusicEnabled;
            if (timerAudio != null) timerAudio.mute = !SettingController.SfxEnabled;
        }

        private System.Collections.IEnumerator LoadAudio(string relativePath, AudioSource source, bool background)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) yield break;
            string extension = System.IO.Path.GetExtension(relativePath).ToLowerInvariant();
            AudioType audioType = extension == ".wav" ? AudioType.WAV : extension == ".ogg" ? AudioType.OGGVORBIS : AudioType.MPEG;
            string path = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/" + relativePath.Trim().TrimStart('/', '\\').Replace('\\', '/');
            if (!path.Contains("://") && !path.StartsWith("jar:")) path = new Uri(path).AbsoluteUri;
            using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(path, audioType))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"Could not load audio {relativePath}: {request.error}");
                    yield break;
                }
                source.clip = DownloadHandlerAudioClip.GetContent(request);
                if (background || timerRequested) source.Play();
            }
        }

        public void SetScenarioTimerAudio(bool playing)
        {
            timerRequested = playing;
            if (timerAudio == null || timerAudio.clip == null) return;
            if (playing && !timerAudio.isPlaying) timerAudio.Play();
            else if (!playing && timerAudio.isPlaying) timerAudio.Stop();
        }

        public DJScreen HomeScreen;
        public DJScreen LoginScreen;
        public DJScreen EntryQuestionScreen;
        public DJScreen ExitQuestionScreen;
        public DJScreen SelectionScreen;
        public DJScreen InfoScreen;
        public DJScreen TutorialScreen;
        public DJScreen ScenarioScreen;
        public DJScreen ResultScreen;
        public string SubmittedLoginCode { get; private set; }
        private bool loginCodeSubmitted;

        [Space(10)]
        [ReadOnly] [SerializeField] Persona currentPersona;
        public Persona ActivePersona => currentPersona;
        public UserResult CurrentUserResult { get; set; }

        [Space(10)]
        public bool toTest;
        public UserResult TestResult;

        public void OnSelectedPersonaIndex(int index)
        {
            currentPersona = (Persona)index;
        }

        public void GoToHomeScreen()
        {
            DJScreenHandler.Instance.LoadScreen(HomeScreen.GetInfo());
        }

        public void GoToLoginScreen()
        {
            loginCodeSubmitted = false;
            SubmittedLoginCode = null;
            LoadScreen(LoginScreen);
        }

        public void SubmitLoginCode(string code)
        {
            if (loginCodeSubmitted || string.IsNullOrEmpty(code) || code.Length != 5)
                return;

            foreach (char digit in code)
                if (digit < '0' || digit > '9')
                    return;

            SubmittedLoginCode = code;
            loginCodeSubmitted = true;
            GoToEntryQuestionScreen();
        }

        public void GoToEntryQuestionScreen()
        {
            LoadScreen(EntryQuestionScreen);
        }

        public void GoToExitQuestionScreen()
        {
            LoadScreen(ExitQuestionScreen);
        }

        public void GoToSelectionScreen()
        {
            if (toTest)
            {
                Testing();
            }
            else
            {
                DJScreenHandler.Instance.LoadScreen(SelectionScreen.GetInfo());
            }
        }


        void Testing()
        {
            currentPersona = Persona.Persona_1;
            CurrentUserResult = TestResult;
            DJScreenHandler.Instance.LoadScreen(ScenarioScreen.GetInfo());
        }

        public void LoadScreen(DJScreen screen)
        {
            DJScreenHandler.Instance.LoadScreen(screen.GetInfo());
        }

        public void RestartApplication()
        {
            SceneManager.LoadScene(0);
        }


        void Update()
        {
            if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
            {
                RestartApplication();
            }   
        }
    }
}

using NaughtyAttributes;
using System;
using System.Collections;
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

        private IEnumerator LoadAudio(string relativePath, AudioSource source, bool background)
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
        private bool apiBusy;
        private StampIqClient stampIq;
        private string stampIqSessionId;
        private string stampIqNextAction;
        private float gameStartedAt;
        private StampIqResultPayload pendingResult;

        [Serializable]
        private sealed class StampIqResultPayload
        {
            public int score;
            public int completion_time;
            public int attempts = 1;
            public string game_status = "completed";
        }

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
            if (!string.IsNullOrEmpty(stampIqSessionId))
            {
                StartCoroutine(AbandonAndGoHome());
                return;
            }
            DJScreenHandler.Instance.LoadScreen(HomeScreen.GetInfo());
        }

        private IEnumerator AbandonAndGoHome()
        {
            if (apiBusy) yield break;
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Abandon(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success)
            {
                Debug.LogWarning("StampIQ session could not be abandoned: " + (reply?.error_code ?? "NETWORK_ERROR"));
                yield break;
            }
            stampIqSessionId = null;
            stampIqNextAction = null;
            DJScreenHandler.Instance.LoadScreen(HomeScreen.GetInfo());
        }

        public void GoToLoginScreen()
        {
            if (!string.IsNullOrEmpty(stampIqSessionId))
            {
                StartCoroutine(AbandonAndGoToLogin());
                return;
            }
            loginCodeSubmitted = false;
            SubmittedLoginCode = null;
            LoadScreen(LoginScreen);
        }

        private IEnumerator AbandonAndGoToLogin()
        {
            yield return AbandonAndGoHome();
            if (string.IsNullOrEmpty(stampIqSessionId)) GoToLoginScreen();
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
            StartCoroutine(ValidateAndOpenSession(code));
        }

        private IEnumerator ValidateAndOpenSession(string code)
        {
            LoginScreen login = LoginScreen as LoginScreen;
            login?.SetSubmissionState(true, "Checking code...");
            yield return LoadConfiguration();
            if (string.IsNullOrWhiteSpace(Configuration.stampIqGameCode))
            {
                LoginFailed(login, "StampIQ game code is missing from config.json.");
                yield break;
            }
            stampIq = new StampIqClient(Configuration.stampIqGameCode.Trim());
            string keyError = null;
            yield return stampIq.LoadKey(error => keyError = error);
            if (keyError != null)
            {
                LoginFailed(login, keyError);
                yield break;
            }
            StampIqReply<StampIqGameConfig> config = null;
            yield return stampIq.Config(reply => config = reply);
            if (config == null || !config.success)
            {
                LoginFailed(login, ErrorMessage(config));
                yield break;
            }
            if (config.data == null || config.data.pre_questionnaire_enabled || config.data.post_questionnaire_enabled)
            {
                LoginFailed(login, "Disable StampIQ pre/post questionnaires for this game. This kiosk uses local CMS questions.");
                yield break;
            }
            StampIqReply<StampIqValidation> validation = null;
            yield return stampIq.Validate(code, reply => validation = reply);
            if (validation == null || !validation.success || string.IsNullOrEmpty(validation.data?.scan_token))
            {
                LoginFailed(login, ErrorMessage(validation));
                yield break;
            }
            StampIqReply<StampIqSession> session = null;
            yield return stampIq.CreateSession(validation.data.scan_token, reply => session = reply);
            if (session == null || !session.success || string.IsNullOrEmpty(session.data?.session_id))
            {
                LoginFailed(login, ErrorMessage(session));
                yield break;
            }
            stampIqSessionId = session.data.session_id;
            stampIqNextAction = session.data.next_action;
            if (stampIqNextAction != "start_game")
            {
                string unexpectedAction = stampIqNextAction;
                yield return stampIq.Abandon(stampIqSessionId, _ => { });
                stampIqSessionId = null;
                stampIqNextAction = null;
                LoginFailed(login, "StampIQ expects " + unexpectedAction + ". Disable its questionnaire for this game.");
                yield break;
            }
            login?.SetSubmissionState(false, null);
            GoToEntryQuestionScreen();
        }

        private void LoginFailed(LoginScreen login, string message)
        {
            loginCodeSubmitted = false;
            SubmittedLoginCode = null;
            login?.SetSubmissionState(false, message);
            Debug.LogWarning("StampIQ login: " + message);
        }

        private static string ErrorMessage<T>(StampIqReply<T> reply) =>
            !string.IsNullOrWhiteSpace(reply?.message) ? reply.message : "StampIQ is unavailable. Please try again.";

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
            if (screen == ScenarioScreen && !toTest)
            {
                if (!apiBusy) StartCoroutine(StartGameAndLoadScenario());
                return;
            }
            DJScreenHandler.Instance.LoadScreen(screen.GetInfo());
        }

        private IEnumerator StartGameAndLoadScenario()
        {
            if (string.IsNullOrEmpty(stampIqSessionId))
            {
                Debug.LogError("StampIQ session is missing. Return to login before playing.");
                (TutorialScreen as TutorialScreen)?.ShowStartError("Session is missing. Please return to login.");
                yield break;
            }
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Start(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success || reply.data?.next_action != "submit_result")
            {
                Debug.LogError("StampIQ could not start the game: " + ErrorMessage(reply));
                (TutorialScreen as TutorialScreen)?.ShowStartError(ErrorMessage(reply) + " Tap Skip to retry.");
                yield break;
            }
            stampIqNextAction = reply.data.next_action;
            gameStartedAt = Time.realtimeSinceStartup;
            DJScreenHandler.Instance.LoadScreen(ScenarioScreen.GetInfo());
        }

        public void SubmitResultAndComplete(Action<bool, string> done)
        {
            if (toTest) { done(true, null); return; }
            if (apiBusy) { done(false, "StampIQ is busy. Please try again."); return; }
            StartCoroutine(SubmitResultAndCompleteRoutine(done));
        }

        private IEnumerator SubmitResultAndCompleteRoutine(Action<bool, string> done)
        {
            if (string.IsNullOrEmpty(stampIqSessionId) || stampIq == null)
            {
                done(false, "StampIQ session is missing.");
                yield break;
            }
            apiBusy = true;
            if (pendingResult == null)
                pendingResult = new StampIqResultPayload
                {
                    score = CurrentUserResult != null ? CurrentUserResult.TotalScore : 0,
                    completion_time = Mathf.Clamp(Mathf.RoundToInt(Time.realtimeSinceStartup - gameStartedAt), 0, 86400)
                };
            if (stampIqNextAction == "submit_result")
            {
                StampIqReply<StampIqSession> result = null;
                yield return stampIq.SubmitResult(stampIqSessionId, pendingResult, reply => result = reply);
                if (result == null || !result.success)
                {
                    if (result?.error_code == "RESULT_ALREADY_SUBMITTED" || result?.error_code == "INVALID_SESSION_STATE")
                    {
                        StampIqReply<StampIqSession> state = null;
                        yield return stampIq.GetSession(stampIqSessionId, reply => state = reply);
                        if (state != null && state.success) stampIqNextAction = state.data?.next_action;
                    }
                    if (stampIqNextAction == "submit_result")
                    {
                        apiBusy = false;
                        done(false, ErrorMessage(result));
                        yield break;
                    }
                }
                else stampIqNextAction = result.data?.next_action;
            }
            if (stampIqNextAction != "complete")
            {
                apiBusy = false;
                done(false, "StampIQ expects " + stampIqNextAction + ". Disable its post questionnaire for this game.");
                yield break;
            }
            StampIqReply<StampIqSession> completion = null;
            yield return stampIq.Complete(stampIqSessionId, reply => completion = reply);
            apiBusy = false;
            if (completion == null || !completion.success)
            {
                done(false, ErrorMessage(completion));
                yield break;
            }
            stampIqSessionId = null;
            stampIqNextAction = null;
            pendingResult = null;
            done(true, null);
        }

        public void RestartApplication()
        {
            if (!string.IsNullOrEmpty(stampIqSessionId))
                StartCoroutine(AbandonAndRestart());
            else
                SceneManager.LoadScene(0);
        }

        private IEnumerator AbandonAndRestart()
        {
            if (apiBusy) yield break;
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Abandon(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success)
            {
                Debug.LogWarning("StampIQ abandon failed: " + ErrorMessage(reply));
                yield break;
            }
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

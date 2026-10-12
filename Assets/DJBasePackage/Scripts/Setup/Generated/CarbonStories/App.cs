using NaughtyAttributes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
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
        private bool preQuestionnaireEnabled;
        private bool postQuestionnaireEnabled;
        private float gameStartedAt;
        private StampIqResultPayload pendingResult;

        [Serializable]
        private sealed class StampIqResultPayload
        {
            public int score;
            public int completion_time;
            public int attempts = 1;
            public string game_status = "completed";
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string score_image;
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
            if (SubmitActiveResultBeforeLeaving(GoToHomeScreen)) return;
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
            bool leavingHandledResult = IsHandledResultActive();
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Abandon(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success)
            {
                Debug.LogWarning("StampIQ session could not be abandoned: " + (reply?.error_code ?? "NETWORK_ERROR"));
                if (!leavingHandledResult) yield break;
                LogResultFailure("abandon", reply);
            }
            stampIqSessionId = null;
            stampIqNextAction = null;
            pendingResult = null;
            DJScreenHandler.Instance.LoadScreen(HomeScreen.GetInfo());
        }

        public void GoToLoginScreen()
        {
            if (SubmitActiveResultBeforeLeaving(GoToLoginScreen)) return;
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
            if (config.data == null)
            {
                LoginFailed(login, "StampIQ game configuration is missing.");
                yield break;
            }
            preQuestionnaireEnabled = config.data.pre_questionnaire_enabled;
            postQuestionnaireEnabled = config.data.post_questionnaire_enabled;
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
            pendingResult = null;
            stampIqNextAction = session.data.next_action;
            if (stampIqNextAction != "start_game" && stampIqNextAction != "submit_pre_questionnaire")
            {
                LogResultFailure("create_session", "UNEXPECTED_STATE", "StampIQ expects " + stampIqNextAction + ".");
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
            gameStartedAt = Time.realtimeSinceStartup;
            if (string.IsNullOrEmpty(stampIqSessionId))
            {
                LogResultFailure("start_game", "MISSING_SESSION", "StampIQ session is missing.");
                DJScreenHandler.Instance.LoadScreen(ScenarioScreen.GetInfo());
                yield break;
            }
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Start(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success || reply.data?.next_action != "submit_result")
            {
                if (reply == null || !reply.success)
                {
                    LogResultFailure("start_game", reply);
                    StampIqReply<StampIqSession> state = null;
                    yield return stampIq.GetSession(stampIqSessionId, value => state = value);
                    if (state != null && state.success) stampIqNextAction = state.data?.next_action;
                    else LogResultFailure("start_game_state", state);
                }
                else LogResultFailure("start_game", "UNEXPECTED_STATE", "StampIQ expects " + reply.data?.next_action + ".");
            }
            else stampIqNextAction = reply.data.next_action;
            DJScreenHandler.Instance.LoadScreen(ScenarioScreen.GetInfo());
        }

        public void SubmitQuestionnaire(bool post, List<QuestionData> localQuestions,
            IReadOnlyList<int> ratings, Action done)
        {
            if (toTest) { done?.Invoke(); return; }
            if (apiBusy)
            {
                LogResultFailure(post ? "post_questionnaire" : "pre_questionnaire", "API_BUSY", "StampIQ was busy when answers were ready.");
                done?.Invoke();
                return;
            }
            StartCoroutine(SubmitQuestionnaireRoutine(post, localQuestions, ratings, done));
        }

        private IEnumerator SubmitQuestionnaireRoutine(bool post, List<QuestionData> localQuestions,
            IReadOnlyList<int> ratings, Action done)
        {
            if (post && !postQuestionnaireEnabled)
            {
                done?.Invoke();
                yield break;
            }
            string stage = post ? "post_questionnaire" : "pre_questionnaire";
            string expectedAction = post ? "submit_post_questionnaire" : "submit_pre_questionnaire";
            bool enabled = post ? postQuestionnaireEnabled : preQuestionnaireEnabled;
            if (string.IsNullOrEmpty(stampIqSessionId) || stampIq == null)
            {
                LogResultFailure(stage, "MISSING_SESSION", "StampIQ session is missing.");
                done?.Invoke();
                yield break;
            }

            apiBusy = true;
            if (enabled && stampIqNextAction == expectedAction)
            {
                StampIqReply<StampIqQuestionnaire> questionnaire = null;
                yield return stampIq.GetQuestionnaire(stampIqSessionId, post, reply => questionnaire = reply);
                if (questionnaire == null || !questionnaire.success)
                {
                    LogResultFailure(stage + "_get", questionnaire);
                }
                else if (TryBuildAnswers(stage, questionnaire.data, localQuestions, ratings,
                    out StampIqAnswerPayload payload))
                {
                    StampIqReply<StampIqSession> submitted = null;
                    yield return stampIq.SubmitQuestionnaire(stampIqSessionId, post, payload, reply => submitted = reply);
                    if (submitted != null && submitted.success)
                    {
                        stampIqNextAction = submitted.data?.next_action;
                    }
                    else
                    {
                        LogResultFailure(stage + "_post", submitted);
                        StampIqReply<StampIqSession> state = null;
                        yield return stampIq.GetSession(stampIqSessionId, reply => state = reply);
                        if (state != null && state.success) stampIqNextAction = state.data?.next_action;
                        else LogResultFailure(stage + "_state", state);
                    }
                }
            }
            else if (enabled && stampIqNextAction != (post ? "complete" : "start_game"))
            {
                LogResultFailure(stage, "UNEXPECTED_STATE", "StampIQ expects " + stampIqNextAction + ".");
            }

            if (post)
            {
                if (stampIqNextAction == "complete")
                {
                    yield return CompleteSessionRoutine();
                }
                else LogResultFailure("complete", "UNEXPECTED_STATE", "StampIQ expects " + stampIqNextAction + ".");
            }

            apiBusy = false;
            done?.Invoke();
        }

        private bool TryBuildAnswers(string stage, StampIqQuestionnaire questionnaire,
            List<QuestionData> localQuestions, IReadOnlyList<int> ratings, out StampIqAnswerPayload payload)
        {
            payload = null;
            List<StampIqRatingQuestion> serverQuestions = questionnaire?.questions;
            int count = localQuestions?.Count ?? 0;
            if (serverQuestions == null || serverQuestions.Count != count || ratings == null || ratings.Count != count)
            {
                LogResultFailure(stage, "QUESTION_COUNT_MISMATCH", "Local and StampIQ question counts do not match.");
                return false;
            }

            var mapped = new List<StampIqRatingAnswer>(count);
            for (int i = 0; i < count; i++)
            {
                StampIqRatingQuestion question = serverQuestions[i];
                int rating = ratings[i];
                if (question == null || question.id <= 0 || rating < question.min || rating > question.max)
                {
                    LogResultFailure(stage, "INVALID_ANSWER", "Question " + (i + 1) + " has no valid server ID or its rating is outside the server range.");
                    return false;
                }
                mapped.Add(new StampIqRatingAnswer { question_id = question.id, answer = rating });
            }
            payload = new StampIqAnswerPayload { answers = mapped };
            return true;
        }

        public void SubmitGameResult(string scoreImage, Action<bool, string> done)
        {
            if (toTest) { done(true, null); return; }
            if (apiBusy)
            {
                LogResultFailure("submit_result", "API_BUSY", "StampIQ was busy when the result was ready.");
                done(false, "StampIQ is busy.");
                return;
            }
            StartCoroutine(SubmitGameResultRoutine(scoreImage, done));
        }

        public void LogResultFailure(string stage, string code, string message, long httpStatus = 0)
        {
            string line = DateTime.UtcNow.ToString("O") + " StampIQ API failure" +
                " stage=" + LogValue(stage) + " session=" + LogValue(stampIqSessionId) +
                " http=" + httpStatus + " code=" + LogValue(code) +
                " message=" + LogValue(message) + Environment.NewLine;
            string path = Path.Combine(Application.streamingAssetsPath, "logs.txt");
            try
            {
                File.AppendAllText(path, line, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                // StreamingAssets is read-only on some platforms; keep the diagnostic if possible.
                try
                {
                    string fallback = Path.Combine(Application.persistentDataPath, "logs.txt");
                    File.AppendAllText(fallback, line, Encoding.UTF8);
                    Debug.LogWarning("Could not write StreamingAssets/logs.txt; saved result failure to " + fallback + ": " + exception.Message);
                }
                catch (Exception fallbackException)
                {
                    Debug.LogError("Could not write result failure log: " + exception.Message + "; " + fallbackException.Message);
                }
            }
        }

        private static string LogValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "-";
            string clean = value.Replace('\r', ' ').Replace('\n', ' ');
            return clean.Length > 500 ? clean.Substring(0, 500) : clean;
        }

        private void LogResultFailure<T>(string stage, StampIqReply<T> reply) =>
            LogResultFailure(stage, reply?.error_code, ErrorMessage(reply), reply?.httpStatus ?? 0);

        private IEnumerator SubmitGameResultRoutine(string scoreImage, Action<bool, string> done)
        {
            if (string.IsNullOrEmpty(stampIqSessionId) || stampIq == null)
            {
                LogResultFailure("submit_result", "MISSING_SESSION", "StampIQ session is missing.");
                done(false, "StampIQ session is missing.");
                yield break;
            }
            apiBusy = true;
            if (pendingResult == null)
                pendingResult = new StampIqResultPayload
                {
                    score = CurrentUserResult != null ? CurrentUserResult.TotalScore : 0,
                    completion_time = Mathf.Clamp(Mathf.RoundToInt(Time.realtimeSinceStartup - gameStartedAt), 0, 86400),
                    score_image = scoreImage
                };
            if (stampIqNextAction == "submit_result")
            {
                StampIqReply<StampIqSession> result = null;
                yield return stampIq.SubmitResult(stampIqSessionId, pendingResult, reply => result = reply);
                if (result == null || !result.success)
                {
                    LogResultFailure("submit_result", result);
                    StampIqReply<StampIqSession> state = null;
                    yield return stampIq.GetSession(stampIqSessionId, reply => state = reply);
                    if (state == null || !state.success) LogResultFailure("get_session", state);
                    if (state != null && state.success) stampIqNextAction = state.data?.next_action;
                    if (stampIqNextAction == "submit_result")
                    {
                        apiBusy = false;
                        done(false, ErrorMessage(result));
                        yield break;
                    }
                }
                else stampIqNextAction = result.data?.next_action;
            }
            else if (stampIqNextAction != "submit_post_questionnaire" && stampIqNextAction != "complete")
            {
                LogResultFailure("submit_result", "UNEXPECTED_STATE", "StampIQ expects " + stampIqNextAction + ".");
                apiBusy = false;
                done(false, "StampIQ is not ready for the result.");
                yield break;
            }
            if (stampIqNextAction == "complete" && !postQuestionnaireEnabled)
                yield return CompleteSessionRoutine();
            apiBusy = false;
            done(true, null);
        }

        private IEnumerator CompleteSessionRoutine()
        {
            StampIqReply<StampIqSession> completion = null;
            yield return stampIq.Complete(stampIqSessionId, reply => completion = reply);
            if (completion != null && completion.success)
            {
                stampIqSessionId = null;
                stampIqNextAction = null;
                pendingResult = null;
            }
            else LogResultFailure("complete", completion);
        }

        public void RestartApplication()
        {
            if (SubmitActiveResultBeforeLeaving(RestartApplication)) return;
            if (!string.IsNullOrEmpty(stampIqSessionId))
                StartCoroutine(AbandonAndRestart());
            else
                SceneManager.LoadScene(0);
        }

        private bool SubmitActiveResultBeforeLeaving(Action onSubmitted)
        {
            if (!IsResultScreenActive(out CarbonStories.ResultScreen resultScreen) || resultScreen.ResultHandled) return false;

            resultScreen.SubmitBeforeLeaving(onSubmitted);
            return true;
        }

        private bool IsHandledResultActive() =>
            IsResultScreenActive(out CarbonStories.ResultScreen resultScreen) && resultScreen.ResultHandled;

        private bool IsResultScreenActive(out CarbonStories.ResultScreen resultScreen)
        {
            resultScreen = ResultScreen as CarbonStories.ResultScreen;
            return resultScreen != null && DJScreenHandler.Instance != null &&
                   DJScreenHandler.Instance.CurrentScreen == ResultScreen.GetInfo();
        }

        private IEnumerator AbandonAndRestart()
        {
            if (apiBusy) yield break;
            bool leavingHandledResult = IsHandledResultActive();
            apiBusy = true;
            StampIqReply<StampIqSession> reply = null;
            yield return stampIq.Abandon(stampIqSessionId, value => reply = value);
            apiBusy = false;
            if (reply == null || !reply.success)
            {
                Debug.LogWarning("StampIQ abandon failed: " + ErrorMessage(reply));
                if (!leavingHandledResult) yield break;
                LogResultFailure("abandon", reply);
            }
            stampIqSessionId = null;
            stampIqNextAction = null;
            pendingResult = null;
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

using UnityEngine;
using Lean.Transition;
using System;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace CarbonStories
{
    [System.Serializable]
    public class Option
    {
        public Sprite icon;
        public string videoPath;
        public string optionVideoPath;
        public string name;
        public string description;
        public int score;
        public string scoreText;
        public Color scoreColor;
        public string aiResponse;
    }


    [System.Serializable]
    public class ScenarioData
    {
        public int number;
        public string question;
        public string hint;
        public string questionVideoPath;
        public Option option1;
        public Option option2;  
        public Option option3;  
    }

    [System.Serializable]
    public class OptionRef
    {
        public Image icon;
        public TextMeshProUGUI title;
        public Image highlightImage;
        public Button optionBtn;

        public void SetContent(Option option)
        {
            icon.sprite = option.icon;
            title.text = option.name;
            highlightImage.enabled = false;
        }
    }

    public class ScenarioScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        [SerializeField] PersonaLoader personaLoader;   

        [Space(10)]
        [SerializeField] TextMeshProUGUI questionNumber;
        [SerializeField] TextMeshProUGUI question;
        [SerializeField] TextMeshProUGUI hintText;
        [SerializeField] OptionRef option1;
        [SerializeField] OptionRef option2;
        [SerializeField] OptionRef option3;

        [Space(10)]
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI scenarioNumberText;
        [SerializeField] TextMeshProUGUI scoreText;


        [Space(10)]
        [SerializeField] ConfirmationPopup confirmationPopup;
        [SerializeField] ScorePopup scorePopup; 
        

        [Space(10)]
        [SerializeField] CustomMediaPlayer mediaPlayer;
        public CustomMediaPlayer questionMediaPlayer;
        [SerializeField] CustomMediaPlayer optionMediaPlayer;
        [SerializeField] CanvasGroup optionVideoCanvasGroup;
        [SerializeField, Min(0f)] float optionVideoFadeDuration = 0.35f;

        [Space(10)]
        [SerializeField] Color buttonHighlightColor;


        [Space(10)]
        [SerializeField] GameObject animatedPanel;
        CanvasGroup animatedPanelCG;

        private PersonaData _personaData;
        private Option _selectedOption;
        private OptionRef _selectedOptionRef;
        private int _currentQuestionIndex;
        private bool _optionVideoPlaying;
        private int _optionCanvasTweenId = -1;
        private float _remainingSeconds;
        private bool _timerRunning;
        private bool _answerConfirmed;
        private bool _advancing;
        private UserResult _userResult;

        private void Update()
        {
            if (App.Instance != null) App.Instance.SetScenarioTimerAudio(_timerRunning);
            if (!_timerRunning)
                return;

            _remainingSeconds = Mathf.Max(0f, _remainingSeconds - Time.deltaTime);
            UpdateTimerText();
            if (_remainingSeconds > 0f)
                return;

            _timerRunning = false;
            App.Instance.SetScenarioTimerAudio(false);
            if (!_answerConfirmed)
            {
                confirmationPopup.Hide();
                scorePopup.Hide();
                SetOptionsVisible(false);
                LoadNext();
            }
        }

        private void UpdateTimerText()
        {
            int seconds = Mathf.CeilToInt(_remainingSeconds);
            if (timerText != null)
                timerText.text = $"TIMER: {seconds / 60:00}:{seconds % 60:00}";
        }

        private void UpdateScoreText()
        {
            if (scoreText != null)
                scoreText.text = $"SCORE: {_userResult.TotalScore} /  40";
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            StartCoroutine(InitializeScreen(info));
        }

        private IEnumerator InitializeScreen(ScreenLoadingInfo info)
        {
            yield return App.Instance.LoadConfiguration();
            StopOptionVideo();
            loadIn.BeginAllTransitions();

            animatedPanelCG = animatedPanel.GetComponent<CanvasGroup>();

            option1.optionBtn.interactable = false;
            option2.optionBtn.interactable = false;
            option3.optionBtn.interactable = false;

            _currentQuestionIndex = 0;
            _personaData = personaLoader.Personas[App.Instance.ActivePersona];
            _userResult = new UserResult { persona = App.Instance.ActivePersona };
            for (int i = 0; i < _personaData.scenarios.Count; i++)
                _userResult.answers.Add(new ScenarioAnswerResult { scenarioNumber = i + 1 });
            App.Instance.CurrentUserResult = _userResult;
            UpdateScoreText();
            if (_personaData.scenarios.Count == 0)
            {
                App.Instance.LoadScreen(App.Instance.ResultScreen);
                yield break;
            }
            LoadScenario();
        }


        void LoadScenario()
        {
            _timerRunning = false;
            _remainingSeconds = App.Instance.Configuration.questionWaitTime;
            _answerConfirmed = false;
            _advancing = false;
            UpdateTimerText();
            if (scenarioNumberText != null)
                scenarioNumberText.text = $"SCENARIO {_currentQuestionIndex + 1}";
            mediaPlayer.OnVideoEnded -= OnVideoEnded;
            mediaPlayer.OnVideoFailed -= OnVideoEnded;
            mediaPlayer.OnVideoStarted -= OnAnswerVideoStarted;
            mediaPlayer.Stop();
            if (questionMediaPlayer != null)
                questionMediaPlayer.Stop();
            StopOptionVideo();
            _selectedOption = null;
            _selectedOptionRef = null;
            ScenarioData scenario = _personaData.scenarios[_currentQuestionIndex];
            hintText.text = scenario.hint;
            questionNumber.text = $"Scenario {scenario.number}";
            question.text = scenario.question;
            SetOptionsVisible(false);

            animatedPanelCG.alpha = 1;
            animatedPanel.SetActive(true);

            if (questionMediaPlayer != null && !string.IsNullOrWhiteSpace(scenario.questionVideoPath))
                questionMediaPlayer.PlayVideo(scenario.questionVideoPath, true);
            ShowOptions();
        }

        private void ShowOptions()
        {
            StopOptionVideo();
            ScenarioData scenario = _personaData.scenarios[_currentQuestionIndex];
            option1.SetContent(scenario.option1);
            option2.SetContent(scenario.option2);
            option3.SetContent(scenario.option3);
            SetOptionsVisible(true);
            _remainingSeconds = App.Instance.Configuration.questionWaitTime;
            UpdateTimerText();
            _timerRunning = true;
        }

        private void SetOptionsVisible(bool visible)
        {
            foreach (OptionRef option in new[] { option1, option2, option3 })
            {
                option.optionBtn.interactable = visible;
                option.optionBtn.gameObject.SetActive(visible);
            }
        }


        public void OnOptionSelected(int index)
        {
            if (!_timerRunning || _answerConfirmed || _advancing)
                return;
            ScenarioData scenario = personaLoader.Personas[App.Instance.ActivePersona]
                .scenarios[_currentQuestionIndex];
            _selectedOption = index == 1 ? scenario.option1 :
                index == 2 ? scenario.option2 :
                index == 3 ? scenario.option3 : null;

            _selectedOptionRef = index == 1 ? option1:
                index == 2 ? option2 :
                index == 3 ? option3 : null;    

            if (_selectedOption == null || string.IsNullOrWhiteSpace(_selectedOption.videoPath))
            {
                Debug.LogWarning($"No video is configured for option {index}.", this);
                return;
            }

            option1.optionBtn.interactable = false;
            option2.optionBtn.interactable = false;
            option3.optionBtn.interactable = false;


            if (_selectedOptionRef != null)
            {
                _selectedOptionRef.highlightImage.enabled = true;
                _selectedOptionRef.highlightImage.color = buttonHighlightColor;
            }

            confirmationPopup.Show(_selectedOption);
            // mediaPlayer.PlayVideo(_selectedOption.videoPath);
        }

        void LoadNext()
        {
            _timerRunning = false;
            ++_currentQuestionIndex;
            if(_currentQuestionIndex == _personaData.scenarios.Count)
            {
                Debug.Log("REACHED END");
                App.Instance.LoadScreen(App.Instance.ResultScreen);
            }
            else
            {
                LoadScenario();
            }
        }

        public void CloseConfirmationPanel()
        {
            if (_answerConfirmed || !_timerRunning || _selectedOptionRef == null)
                return;
            _selectedOptionRef.highlightImage.enabled = false;
            confirmationPopup.Hide();

            option1.optionBtn.interactable = true;
            option2.optionBtn.interactable = true;
            option3.optionBtn.interactable = true;
        }

        public void OnOptionConfirmed()
        {
            if (_answerConfirmed || !_timerRunning || _selectedOption == null || _selectedOptionRef == null)
                return;

            _answerConfirmed = true;
            ScenarioAnswerResult answer = _userResult.answers[_currentQuestionIndex];
            answer.icon = _selectedOption.icon;
            answer.name = _selectedOption.name ?? string.Empty;
            answer.score = _selectedOption.score;
            answer.answered = true;
            UpdateScoreText();
            confirmationPopup.Hide();
            _selectedOptionRef.highlightImage.color = _selectedOption.scoreColor;

            ShowConfirmedScore();
        }

        private void PlayConfirmedOptionVideo()
        {
            string path = _selectedOption.optionVideoPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                int optionIndex = _selectedOptionRef == option1 ? 1 : _selectedOptionRef == option2 ? 2 : 3;
                int scenarioNumber = _personaData.scenarios[_currentQuestionIndex].number;
                path = $"Personas/{App.Instance.ActivePersona.ToString().ToLowerInvariant()}/scenarios/{scenarioNumber}/optionVideos/{optionIndex}.mp4";
            }

            path = path.Trim().TrimStart('/', '\\').Replace('\\', '/');
            if (optionMediaPlayer != null && optionMediaPlayer != mediaPlayer &&
                System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, path)))
            {
                _optionVideoPlaying = true;
                optionMediaPlayer.OnVideoStarted += OnOptionVideoStarted;
                optionMediaPlayer.OnVideoEnded += HideOptionVideo;
                optionMediaPlayer.OnVideoFailed += HideOptionVideo;
                optionMediaPlayer.PlayVideo(path);
            }
        }

        private void OnOptionVideoStarted()
        {
            optionMediaPlayer.OnVideoStarted -= OnOptionVideoStarted;
            FadeOptionCanvas(1f);
        }

        private void HideOptionVideo()
        {
            if (!_optionVideoPlaying)
                return;
            _optionVideoPlaying = false;
            RemoveOptionVideoCallbacks();
            optionMediaPlayer.Hide();
            FadeOptionCanvas(0f);
        }

        private void RemoveOptionVideoCallbacks()
        {
            if (optionMediaPlayer == null || optionMediaPlayer == mediaPlayer)
                return;
            optionMediaPlayer.OnVideoStarted -= OnOptionVideoStarted;
            optionMediaPlayer.OnVideoEnded -= HideOptionVideo;
            optionMediaPlayer.OnVideoFailed -= HideOptionVideo;
        }

        private void FadeOptionCanvas(float alpha)
        {
            CancelOptionCanvasFade();
            if (optionVideoCanvasGroup == null)
                return;
            optionVideoCanvasGroup.interactable = false;
            optionVideoCanvasGroup.blocksRaycasts = false;
            _optionCanvasTweenId = LeanTween.alphaCanvas(
                optionVideoCanvasGroup, alpha, optionVideoFadeDuration).setIgnoreTimeScale(true).id;
        }

        private void CancelOptionCanvasFade()
        {
            if (_optionCanvasTweenId < 0)
                return;
            LeanTween.cancel(_optionCanvasTweenId);
            _optionCanvasTweenId = -1;
        }

        private void StopOptionVideo()
        {
            _optionVideoPlaying = false;
            RemoveOptionVideoCallbacks();
            if (optionMediaPlayer != null && optionMediaPlayer != mediaPlayer)
                optionMediaPlayer.Stop();
            CancelOptionCanvasFade();
            if (optionVideoCanvasGroup != null)
            {
                optionVideoCanvasGroup.alpha = 0f;
                optionVideoCanvasGroup.interactable = false;
                optionVideoCanvasGroup.blocksRaycasts = false;
            }
        }

        private void ShowConfirmedScore()
        {
            StartCoroutine(WaitAndExecute(0.5f,() => scorePopup.Show(_selectedOption)));

            StartCoroutine(WaitAndExecute(3.5f, () => {
                scorePopup.Hide();
                //LoadNext();
                mediaPlayer.OnVideoEnded += OnVideoEnded;
                mediaPlayer.OnVideoFailed += OnVideoEnded;
                mediaPlayer.OnVideoStarted += OnAnswerVideoStarted;
                if (questionMediaPlayer != null)
                    questionMediaPlayer.Stop();
                PlayConfirmedOptionVideo();
                mediaPlayer.PlayVideo(_selectedOption.videoPath);
            }));
        }

        private void OnAnswerVideoStarted()
        {
            _timerRunning = false;
            mediaPlayer.OnVideoStarted -= OnAnswerVideoStarted;
        }

        void OnVideoEnded()
        {
            if (_advancing)
                return;
            _advancing = true;
            _timerRunning = false;
            HideOptionVideo();
            mediaPlayer.OnVideoEnded -= OnVideoEnded;
            mediaPlayer.OnVideoFailed -= OnVideoEnded;
            mediaPlayer.OnVideoStarted -= OnAnswerVideoStarted;
            StartCoroutine(WaitAndExecute(0.8f, () => {
                LeanTween.alphaCanvas(animatedPanelCG, 0, 0.35f);
                StartCoroutine(WaitAndExecute(0.35f,()=>animatedPanel.SetActive(false)));
                StartCoroutine(WaitAndExecute(0.5f,()=> LoadNext()));
            }));
        }

        protected override void OnLoadingCompleted()
        {
        }

        protected override void OnReset()
        {
            _timerRunning = false;
            App.Instance.SetScenarioTimerAudio(false);
            StopOptionVideo();
            if (questionMediaPlayer != null)
                questionMediaPlayer.Stop();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            _timerRunning = false;
            App.Instance.SetScenarioTimerAudio(false);
            if (questionMediaPlayer != null)
                questionMediaPlayer.Stop();
            mediaPlayer.OnVideoEnded -= OnVideoEnded;
            mediaPlayer.OnVideoFailed -= OnVideoEnded;
            mediaPlayer.OnVideoStarted -= OnAnswerVideoStarted;
            mediaPlayer.Stop();
            StopOptionVideo();
            StopAllCoroutines();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }

        IEnumerator WaitAndExecute(float delay,Action callback)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }
    }
}

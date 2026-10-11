using System.Collections.Generic;
using Lean.Transition;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CarbonStories
{
    public abstract class PersonaQuestionScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        [SerializeField] private GameObject animatedPanel;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private UnityEngine.UI.Button[] ratingButtons = new UnityEngine.UI.Button[10];
        [SerializeField, InspectorName("Continue Button")] private UnityEngine.UI.Button nextButton;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(0.96f, 0.7f, 0.14f, 1f);
        [SerializeField] private DJScreen screenAfterQuestions;
        [SerializeField] private UnityEvent onQuestionsCompleted;

        private readonly List<int> answers = new List<int>();
        private List<QuestionData> questions;
        private int questionIndex;
        private int selectedRating;
        private bool completed;
        private bool acceptingQuestions;

        public IReadOnlyList<int> Answers => answers;

        protected abstract List<QuestionData> GetQuestions(SharedQuestionData data);

        protected override void Awake()
        {
            base.Awake();
            if (progressText != null)
                progressText.gameObject.SetActive(false);
            for (int i = 0; i < 10 && i < ratingButtons.Length; i++)
            {
                int rating = i + 1;
                if (ratingButtons[i] != null)
                {
                    ratingButtons[i].transition = UnityEngine.UI.Selectable.Transition.None;
                    ratingButtons[i].onClick.AddListener(() => SelectRating(rating));
                }
            }
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            ResetQuestions();
            acceptingQuestions = true;
            loadIn.BeginAllTransitions();
            StartCoroutine(ConfigurationReader.Load<SharedQuestionData>("questions.json", ShowQuestions));
        }

        private void ShowQuestions(SharedQuestionData data)
        {
            if (!acceptingQuestions) return;
            questions = GetQuestions(data);
            if (questions == null || questions.Count == 0)
            {
                Debug.LogWarning($"{GetType().Name} has no questions in questions.json.", this);
                return;
            }
            ShowCurrentQuestion();
        }

        private void ShowCurrentQuestion()
        {
            if (animatedPanel != null) animatedPanel.SetActive(false);
            QuestionData current = questions[questionIndex];
            if (questionText != null)
                questionText.text = $"Question {questionIndex + 1} :\n{current?.question ?? string.Empty}";
            selectedRating = 0;
            if (nextButton != null) nextButton.interactable = false;
            for (int i = 0; i < ratingButtons.Length; i++)
                if (ratingButtons[i] != null) ratingButtons[i].interactable = i < 10;
            UpdateRatingColors();
            if (animatedPanel != null) animatedPanel.SetActive(true);
        }

        public void SelectRating(int rating)
        {
            if (!acceptingQuestions || completed || questions == null || rating < 1 || rating > 10 ||
                ratingButtons == null || rating > ratingButtons.Length || ratingButtons[rating - 1] == null) return;
            selectedRating = rating;
            if (nextButton != null) nextButton.interactable = true;
            UpdateRatingColors();
        }

        private void UpdateRatingColors()
        {
            for (int i = 0; i < ratingButtons.Length; i++)
                if (ratingButtons[i] != null && ratingButtons[i].targetGraphic != null)
                    ratingButtons[i].targetGraphic.color = i + 1 == selectedRating ? highlightColor : defaultColor;
        }

        public void SubmitAnswer()
        {
            if (!acceptingQuestions || completed || questions == null || selectedRating == 0) return;
            answers.Add(selectedRating);
            questionIndex++;
            if (questionIndex < questions.Count) ShowCurrentQuestion();
            else CompleteQuestions();
        }

        private void CompleteQuestions()
        {
            if (completed) return;
            completed = true;
            onQuestionsCompleted?.Invoke();
            if (screenAfterQuestions != null)
                App.Instance.LoadScreen(screenAfterQuestions);
        }

        private void ResetQuestions()
        {
            acceptingQuestions = false;
            if (animatedPanel != null) animatedPanel.SetActive(false);
            answers.Clear();
            questions = null;
            questionIndex = 0;
            selectedRating = 0;
            completed = false;
            if (questionText != null) questionText.text = string.Empty;
            if (progressText != null) progressText.text = string.Empty;
            if (nextButton != null) nextButton.interactable = false;
            for (int i = 0; i < ratingButtons.Length; i++)
                if (ratingButtons[i] != null) ratingButtons[i].interactable = false;
            UpdateRatingColors();
        }

        protected override void OnLoadingCompleted() { }
        protected override void OnReset() => ResetQuestions();
        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            acceptingQuestions = false;
            if (animatedPanel != null) animatedPanel.SetActive(false);
            loadOut.BeginAllTransitions();
        }
        protected override void OnUnloadingCompleted() { }

    }
}

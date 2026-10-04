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

        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI[] optionTexts = new TextMeshProUGUI[4];
        [SerializeField] private UnityEngine.UI.Button[] optionButtons = new UnityEngine.UI.Button[4];
        [SerializeField, InspectorName("Continue Button")] private UnityEngine.UI.Button nextButton;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private Color highlightColor = new Color(0.96f, 0.7f, 0.14f, 1f);
        [SerializeField] private DJScreen screenAfterQuestions;
        [SerializeField] private UnityEvent onQuestionsCompleted;

        private readonly List<int> answers = new List<int>();
        private List<QuestionData> questions;
        private int questionIndex;
        private int selectedOption = -1;
        private bool completed;
        private bool acceptingQuestions;

        public IReadOnlyList<int> Answers => answers;

        protected abstract List<QuestionData> GetQuestions(SharedQuestionData data);

        protected override void Awake()
        {
            base.Awake();
            if (progressText != null)
                progressText.gameObject.SetActive(false);
            for (int i = 0; i < 4 && i < optionButtons.Length; i++)
            {
                int optionIndex = i;
                if (optionButtons[i] != null)
                {
                    optionButtons[i].transition = UnityEngine.UI.Selectable.Transition.None;
                    optionButtons[i].onClick.AddListener(() => SelectOption(optionIndex));
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
            QuestionData current = questions[questionIndex];
            if (questionText != null)
                questionText.text = $"Question {questionIndex + 1} :\n{current?.question ?? string.Empty}";
            selectedOption = -1;
            if (nextButton != null) nextButton.interactable = false;
            for (int i = 0; i < 4; i++)
            {
                string option = current?.options != null && i < current.options.Length
                    ? current.options[i] : string.Empty;
                if (i < optionTexts.Length && optionTexts[i] != null) optionTexts[i].text = option;
                if (i < optionButtons.Length && optionButtons[i] != null)
                    optionButtons[i].interactable = !string.IsNullOrWhiteSpace(option);
            }
            UpdateOptionColors();
        }

        public void SelectOption(int index)
        {
            if (!acceptingQuestions || completed || questions == null || index < 0 || index >= 4) return;
            QuestionData current = questions[questionIndex];
            if (current?.options == null || index >= current.options.Length ||
                string.IsNullOrWhiteSpace(current.options[index])) return;
            selectedOption = index;
            if (nextButton != null) nextButton.interactable = true;
            UpdateOptionColors();
        }

        private void UpdateOptionColors()
        {
            for (int i = 0; i < optionButtons.Length; i++)
                if (optionButtons[i] != null && optionButtons[i].targetGraphic != null)
                    optionButtons[i].targetGraphic.color = i == selectedOption ? highlightColor : defaultColor;
        }

        public void SubmitAnswer()
        {
            if (!acceptingQuestions || completed || questions == null || selectedOption < 0) return;
            answers.Add(selectedOption);
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
            answers.Clear();
            questions = null;
            questionIndex = 0;
            selectedOption = -1;
            completed = false;
            if (questionText != null) questionText.text = string.Empty;
            if (progressText != null) progressText.text = string.Empty;
            if (nextButton != null) nextButton.interactable = false;
            for (int i = 0; i < optionButtons.Length; i++)
                if (optionButtons[i] != null) optionButtons[i].interactable = false;
            UpdateOptionColors();
        }

        protected override void OnLoadingCompleted() { }
        protected override void OnReset() => ResetQuestions();
        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            acceptingQuestions = false;
            loadOut.BeginAllTransitions();
        }
        protected override void OnUnloadingCompleted() { }

    }
}

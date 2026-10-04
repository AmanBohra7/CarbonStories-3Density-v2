using System.Collections;
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

        [SerializeField] private PersonaLoader personaLoader;
        [SerializeField] private TextMeshProUGUI questionText;
        [SerializeField] private TextMeshProUGUI progressText;
        [SerializeField] private TextMeshProUGUI[] optionTexts = new TextMeshProUGUI[4];
        [SerializeField] private UnityEngine.UI.Button[] optionButtons = new UnityEngine.UI.Button[4];
        [SerializeField] private UnityEngine.UI.Button nextButton;
        [SerializeField] private DJScreen screenAfterQuestions;
        [SerializeField] private UnityEvent onQuestionsCompleted;

        private readonly List<int> answers = new List<int>();
        private List<PersonaQuestionData> questions;
        private int questionIndex;
        private int selectedOption = -1;
        private bool completed;
        private bool acceptingQuestions;
        private Coroutine pendingAdvance;

        public IReadOnlyList<int> Answers => answers;

        protected abstract List<PersonaQuestionData> GetQuestions(PersonaData persona);
        protected virtual float AutoAdvanceDelaySeconds => -1f;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < 4 && i < optionButtons.Length; i++)
            {
                int optionIndex = i;
                if (optionButtons[i] != null)
                    optionButtons[i].onClick.AddListener(() => SelectOption(optionIndex));
            }
            if (nextButton != null)
                nextButton.onClick.AddListener(NextQuestion);
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            ResetQuestions();
            acceptingQuestions = true;
            loadIn.BeginAllTransitions();
            if (personaLoader == null || App.Instance == null)
            {
                Debug.LogError($"{GetType().Name} requires a PersonaLoader and App.", this);
                return;
            }
            personaLoader.LoadPersonaData(App.Instance.ActivePersona, ShowQuestions,
                error => Debug.LogError(error, this));
        }

        private void ShowQuestions(PersonaData persona)
        {
            if (!acceptingQuestions) return;
            questions = GetQuestions(persona);
            if (questions == null || questions.Count == 0)
            {
                Debug.LogWarning($"{GetType().Name} has no questions for {App.Instance.ActivePersona}.", this);
                return;
            }
            ShowCurrentQuestion();
        }

        private void ShowCurrentQuestion()
        {
            PersonaQuestionData current = questions[questionIndex];
            if (questionText != null) questionText.text = current?.question ?? string.Empty;
            if (progressText != null) progressText.text = $"{questionIndex + 1} / {questions.Count}";
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
        }

        public void SelectOption(int index)
        {
            if (!acceptingQuestions || completed || pendingAdvance != null || questions == null || index < 0 || index >= 4) return;
            PersonaQuestionData current = questions[questionIndex];
            if (current?.options == null || index >= current.options.Length ||
                string.IsNullOrWhiteSpace(current.options[index])) return;
            selectedOption = index;
            if (nextButton != null) nextButton.interactable = true;
            if (AutoAdvanceDelaySeconds >= 0f)
            {
                for (int i = 0; i < optionButtons.Length; i++)
                    if (optionButtons[i] != null) optionButtons[i].interactable = false;
                pendingAdvance = StartCoroutine(AdvanceAfterDelay());
            }
        }

        private IEnumerator AdvanceAfterDelay()
        {
            yield return new WaitForSeconds(AutoAdvanceDelaySeconds);
            pendingAdvance = null;
            NextQuestion();
        }

        public void NextQuestion()
        {
            if (!acceptingQuestions || completed || questions == null || selectedOption < 0) return;
            CancelPendingAdvance();
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
            CancelPendingAdvance();
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
        }

        protected override void OnLoadingCompleted() { }
        protected override void OnReset() => ResetQuestions();
        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            CancelPendingAdvance();
            acceptingQuestions = false;
            loadOut.BeginAllTransitions();
        }
        protected override void OnUnloadingCompleted() { }

        private void CancelPendingAdvance()
        {
            if (pendingAdvance == null) return;
            StopCoroutine(pendingAdvance);
            pendingAdvance = null;
        }
    }
}

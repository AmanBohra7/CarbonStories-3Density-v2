using Lean.Transition;
using TMPro;
using UnityEngine;

namespace CarbonStories
{
    public class ResultScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        [Header("Results")]
        [SerializeField] private TextMeshProUGUI totalScoreText;
        [SerializeField] private TextMeshProUGUI totalScoreText2;
        [SerializeField] private TextMeshProUGUI[] optionNameTexts = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI[] optionScoreTexts = new TextMeshProUGUI[4];
        [SerializeField] private TextMeshProUGUI gradeText;
        [SerializeField] private TextMeshProUGUI descriptionText;
        [SerializeField] private UnityEngine.UI.Button continueButton;
        private bool resultSynchronized;
        private bool resultBusy;
        private string resultDescription;

        [Header("Media")]
        [SerializeField] private CustomMediaPlayer customMediaPlayer;

        [Header("Grade Thresholds (out of 40)")]
        [SerializeField, Range(0, 40)] private int aPlusMinimumScore = 32;
        [SerializeField, Range(0, 40)] private int bPlusMinimumScore = 24;

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            StartCoroutine(InitializeScreen(info));
        }

        private System.Collections.IEnumerator InitializeScreen(ScreenLoadingInfo info)
        {
            yield return App.Instance.LoadConfiguration();
            customMediaPlayer?.Stop();
            UpdateResults();
            loadIn.BeginAllTransitions();
            resultSynchronized = false;
            SubmitResult();
        }

        private void SubmitResult()
        {
            if (resultBusy) return;
            resultBusy = true;
            if (continueButton != null) continueButton.interactable = false;
            if (descriptionText != null) descriptionText.text = resultDescription + "\nSaving your result...";
            App.Instance.SubmitResultAndComplete((success, error) =>
            {
                resultBusy = false;
                resultSynchronized = success;
                if (continueButton != null) continueButton.interactable = true;
                if (descriptionText != null)
                    descriptionText.text = success ? resultDescription :
                        resultDescription + "\n" + error + " Tap the button to retry.";
            });
        }

        public void OnContinuePressed()
        {
            if (resultBusy) return;
            if (resultSynchronized)
            {
                if (continueButton != null) continueButton.interactable = false;
                App.Instance.GoToExitQuestionScreen();
            }
            else SubmitResult();
        }

        private void UpdateResults()
        {
            UserResult result = App.Instance.CurrentUserResult;
            int totalScore = result != null ? result.TotalScore : 0;
            int gradeIndex = totalScore >= aPlusMinimumScore ? 0 :
                totalScore >= bPlusMinimumScore ? 1 : 2;
            string grade = gradeIndex == 0 ? "A+" : gradeIndex == 1 ? "B+" : "C+";
            if (descriptionText != null)
                descriptionText.text = App.Instance.ResultDescriptions.GetDescription(grade);
            resultDescription = descriptionText != null ? descriptionText.text : string.Empty;

            if (totalScoreText != null)
                totalScoreText.text = $"{totalScore}";

            if (totalScoreText2 != null)
                totalScoreText2.text = $"{totalScore}";

            if (gradeText != null)
                gradeText.text = grade;

            if (optionNameTexts != null)
            {
                for (int i = 0; i < optionNameTexts.Length; i++)
                {
                    ScenarioAnswerResult answer = GetAnswer(result, i);
                    if (optionNameTexts[i] != null)
                        optionNameTexts[i].text = answer != null ? answer.name ?? string.Empty : string.Empty;
                }
            }

            if (optionScoreTexts != null)
            {
                for (int i = 0; i < optionScoreTexts.Length; i++)
                {
                    ScenarioAnswerResult answer = GetAnswer(result, i);
                    if (optionScoreTexts[i] != null)
                        optionScoreTexts[i].text = (answer != null ? answer.score : 0).ToString();
                }
            }

            if (customMediaPlayer != null)
            {
                string videoPath = App.Instance.Configuration.ResultVideo(gradeIndex);
                if (string.IsNullOrWhiteSpace(videoPath))
                {
                    customMediaPlayer.Stop();
                }
                else{
                    Debug.Log("playing video - " + videoPath);
                    customMediaPlayer.PlayVideo(videoPath);
                }
            }
            else
                Debug.LogWarning("ResultScreen has no CustomMediaPlayer assigned.", this);
        }

        private static ScenarioAnswerResult GetAnswer(UserResult result, int index)
        {
            return result != null && result.answers != null && index < result.answers.Count
                ? result.answers[index]
                : null;
        }

        protected override void OnLoadingCompleted()
        {
        }

        protected override void OnReset()
        {
            customMediaPlayer?.Stop();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            StopAllCoroutines();
            customMediaPlayer?.Stop();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }
    }
}

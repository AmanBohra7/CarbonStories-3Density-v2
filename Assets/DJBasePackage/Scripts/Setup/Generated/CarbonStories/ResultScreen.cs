using System;
using System.Collections;
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
        private bool resultHandled;
        private bool resultBusy;
        private string resultDescription;
        private string scoreImage;
        private bool resultVideoReady;
        private bool resultInitialized;
        private Action leaveAfterSubmission;

        public bool ResultHandled => resultHandled;

        [Header("Media")]
        [SerializeField] private CustomMediaPlayer customMediaPlayer;

        [Header("Grade Thresholds (out of 40)")]
        [SerializeField, Range(0, 40)] private int aPlusMinimumScore = 32;
        [SerializeField, Range(0, 40)] private int bPlusMinimumScore = 24;

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            resultHandled = false;
            resultBusy = false;
            resultInitialized = false;
            scoreImage = null;
            leaveAfterSubmission = null;
            if (continueButton != null) continueButton.interactable = true;
            StartCoroutine(InitializeScreen(info));
        }

        private IEnumerator InitializeScreen(ScreenLoadingInfo info)
        {
            yield return App.Instance.LoadConfiguration();
            customMediaPlayer?.Stop();
            if (customMediaPlayer != null)
            {
                customMediaPlayer.OnVideoStarted -= OnResultVideoReady;
                customMediaPlayer.OnVideoFailed -= OnResultVideoReady;
                customMediaPlayer.OnVideoStarted += OnResultVideoReady;
                customMediaPlayer.OnVideoFailed += OnResultVideoReady;
            }
            UpdateResults();
            loadIn.BeginAllTransitions();
            resultInitialized = true;
        }

        public void SubmitBeforeLeaving(Action onSubmitted)
        {
            if (resultHandled)
            {
                onSubmitted?.Invoke();
                return;
            }

            if (leaveAfterSubmission == null) leaveAfterSubmission = onSubmitted;
            SubmitResult(true);
        }

        private IEnumerator SubmitAfterThreeMinutes()
        {
            float videoDeadline = Time.realtimeSinceStartup + 10f;
            yield return new WaitUntil(() => resultInitialized &&
                (resultVideoReady || Time.realtimeSinceStartup >= videoDeadline));
            yield return new WaitForEndOfFrame();
            yield return new WaitForSecondsRealtime(180f);
            if (!resultHandled) SubmitResult();
        }

        private void SubmitResult(bool leavingNow = false)
        {
            if (resultBusy || resultHandled) return;
            resultBusy = true;
            StartCoroutine(CaptureAndSubmitResult(leavingNow));
        }

        private IEnumerator CaptureAndSubmitResult(bool leavingNow)
        {
            if (scoreImage == null)
            {
                // The score screen fade and the result video's first frame must be visible.
                float videoDeadline = Time.realtimeSinceStartup + 10f;
                yield return new WaitUntil(() => resultInitialized && currentState == DJScreenState.LOADED &&
                    (leavingNow || resultVideoReady || Time.realtimeSinceStartup >= videoDeadline));
                if (descriptionText != null) descriptionText.text = resultDescription;
                yield return new WaitForEndOfFrame();
                try
                {
                    scoreImage = CaptureScoreImage();
                }
                catch (Exception exception)
                {
                    Debug.LogError("Could not capture the result screen: " + exception, this);
                    App.Instance.LogResultFailure("capture", "CAPTURE_FAILED", exception.Message);
                    // The image is optional; still submit the score if capture failed.
                }
            }

            if (continueButton != null) continueButton.interactable = false;
            if (descriptionText != null) descriptionText.text = resultDescription + "\nSaving your result...";
            App.Instance.SubmitGameResult(scoreImage, (_, __) =>
            {
                resultBusy = false;
                resultHandled = true;
                if (continueButton != null) continueButton.interactable = true;
                if (descriptionText != null) descriptionText.text = resultDescription;
                Action onSubmitted = leaveAfterSubmission;
                leaveAfterSubmission = null;
                onSubmitted?.Invoke();
            });
        }

        private void OnResultVideoReady() => resultVideoReady = true;

        private static string CaptureScoreImage()
        {
            const int maxWidth = 1280;
            const int maxBytes = 5 * 1024 * 1024;
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (screenshot == null) throw new InvalidOperationException("Screen capture returned no image.");

            Texture2D resized = null;
            RenderTexture target = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Texture2D image = screenshot;
                if (screenshot.width > maxWidth)
                {
                    int height = Mathf.Max(1, Mathf.RoundToInt((float)screenshot.height * maxWidth / screenshot.width));
                    target = RenderTexture.GetTemporary(maxWidth, height, 0);
                    Graphics.Blit(screenshot, target);
                    RenderTexture.active = target;
                    resized = new Texture2D(maxWidth, height, TextureFormat.RGB24, false);
                    resized.ReadPixels(new Rect(0, 0, maxWidth, height), 0, 0);
                    resized.Apply();
                    image = resized;
                }

                byte[] jpg = image.EncodeToJPG(80);
                if (jpg.Length > maxBytes) jpg = image.EncodeToJPG(60);
                if (jpg.Length > maxBytes) throw new InvalidOperationException("Result screen image exceeds the 5 MB API limit.");
                return "data:image/jpeg;base64," + Convert.ToBase64String(jpg);
            }
            finally
            {
                RenderTexture.active = previous;
                if (target != null) RenderTexture.ReleaseTemporary(target);
                if (resized != null) UnityEngine.Object.Destroy(resized);
                UnityEngine.Object.Destroy(screenshot);
            }
        }

        public void OnContinuePressed()
        {
            if (continueButton != null) continueButton.interactable = false;
            App.Instance.GoToExitQuestionScreen();
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
                resultVideoReady = string.IsNullOrWhiteSpace(videoPath);
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
            {
                resultVideoReady = true;
                Debug.LogWarning("ResultScreen has no CustomMediaPlayer assigned.", this);
            }
        }

        private static ScenarioAnswerResult GetAnswer(UserResult result, int index)
        {
            return result != null && result.answers != null && index < result.answers.Count
                ? result.answers[index]
                : null;
        }

        protected override void OnLoadingCompleted()
        {
            StartCoroutine(SubmitAfterThreeMinutes());
        }

        protected override void OnReset()
        {
            if (customMediaPlayer != null)
            {
                customMediaPlayer.OnVideoStarted -= OnResultVideoReady;
                customMediaPlayer.OnVideoFailed -= OnResultVideoReady;
            }
            customMediaPlayer?.Stop();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            StopAllCoroutines();
            if (customMediaPlayer != null)
            {
                customMediaPlayer.OnVideoStarted -= OnResultVideoReady;
                customMediaPlayer.OnVideoFailed -= OnResultVideoReady;
            }
            customMediaPlayer?.Stop();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }
    }
}

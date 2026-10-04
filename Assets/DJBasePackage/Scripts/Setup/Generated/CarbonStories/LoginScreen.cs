using Lean.Transition;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CarbonStories
{
    public class LoginScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;
        [SerializeField] private FiveDigitCodeInput codeInput;
        [SerializeField] private UnityEngine.UI.Button submitButton;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private UnityEvent<string> onCodeCompleted = new UnityEvent<string>();

        public FiveDigitCodeInput CodeInput => codeInput;
        public UnityEvent<string> OnCodeCompleted => onCodeCompleted;
        private string defaultStatus;
        private bool submitting;

        protected override void Awake()
        {
            base.Awake();
            if (codeInput == null)
            {
                Debug.LogError("LoginScreen needs a Five Digit Code Input reference assigned in the scene.", this);
                return;
            }

            codeInput.OnCodeChanged.AddListener(HandleCodeChanged);
            codeInput.OnCodeCompleted.AddListener(HandleCodeCompleted);
            if (statusText != null) defaultStatus = statusText.text;
            HandleCodeChanged(codeInput.Code);
            if (submitButton == null)
                Debug.LogError("LoginScreen needs a Submit Button reference assigned in the scene.", this);
        }

        private void OnDestroy()
        {
            if (codeInput != null)
            {
                codeInput.OnCodeChanged.RemoveListener(HandleCodeChanged);
                codeInput.OnCodeCompleted.RemoveListener(HandleCodeCompleted);
            }
        }

        private void HandleCodeChanged(string code)
        {
            if (submitButton != null)
                submitButton.interactable = !submitting && codeInput != null && codeInput.IsComplete;
            if (!submitting && statusText != null) statusText.text = defaultStatus;
        }

        private void HandleCodeCompleted(string code)
        {
            onCodeCompleted.Invoke(code);
        }

        public void SubmitCode()
        {
            if (submitting || codeInput == null || !codeInput.IsComplete)
                return;

            if (App.Instance == null)
            {
                Debug.LogError("Cannot submit the login code without App.", this);
                return;
            }

            codeInput.HideKeyboard();
            App.Instance.SubmitLoginCode(codeInput.Code);
        }

        public void SetSubmissionState(bool busy, string message)
        {
            submitting = busy;
            if (statusText != null) statusText.text = string.IsNullOrEmpty(message) ? defaultStatus : message;
            if (submitButton != null) submitButton.interactable = !busy && codeInput != null && codeInput.IsComplete;
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            SetSubmissionState(false, null);
            if (codeInput != null)
                codeInput.Clear();
            loadIn.BeginAllTransitions();
        }

        protected override void OnLoadingCompleted()
        {
            StartCoroutine(FocusInputNextFrame());
        }

        private IEnumerator FocusInputNextFrame()
        {
            yield return null;
            if (codeInput != null && isActiveAndEnabled)
                codeInput.Focus();
        }

        protected override void OnReset()
        {
            if (codeInput != null)
                codeInput.Clear();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            if (codeInput != null)
                codeInput.HideKeyboard();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }
    }
}

using Lean.Transition;
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
        [SerializeField] private UnityEvent<string> onCodeCompleted = new UnityEvent<string>();

        public FiveDigitCodeInput CodeInput => codeInput;
        public UnityEvent<string> OnCodeCompleted => onCodeCompleted;

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
                submitButton.interactable = codeInput != null && codeInput.IsComplete;
        }

        private void HandleCodeCompleted(string code)
        {
            onCodeCompleted.Invoke(code);
        }

        public void SubmitCode()
        {
            if (codeInput == null || !codeInput.IsComplete)
                return;

            if (App.Instance == null)
            {
                Debug.LogError("Cannot submit the login code without App.", this);
                return;
            }

            App.Instance.SubmitLoginCode(codeInput.Code);
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            if (codeInput != null)
                codeInput.Clear();
            loadIn.BeginAllTransitions();
        }

        protected override void OnLoadingCompleted()
        {
        }

        protected override void OnReset()
        {
            if (codeInput != null)
                codeInput.Clear();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }
    }
}

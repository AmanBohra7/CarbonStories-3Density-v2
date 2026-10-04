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
        [SerializeField] private UnityEvent<string> onCodeCompleted = new UnityEvent<string>();

        public FiveDigitCodeInput CodeInput => codeInput;
        public UnityEvent<string> OnCodeCompleted => onCodeCompleted;

        protected override void Awake()
        {
            base.Awake();
            if (codeInput == null)
            {
                Transform panel = transform.Find("Panel");
                if (panel == null)
                {
                    Debug.LogError("LoginScreen needs a Panel for the code input.", this);
                    return;
                }

                var inputObject = new GameObject("Five Digit Code Input", typeof(RectTransform));
                inputObject.transform.SetParent(panel, false);
                codeInput = inputObject.AddComponent<FiveDigitCodeInput>();
            }

            codeInput.OnCodeCompleted.AddListener(HandleCodeCompleted);
        }

        private void OnDestroy()
        {
            if (codeInput != null)
                codeInput.OnCodeCompleted.RemoveListener(HandleCodeCompleted);
        }

        private void HandleCodeCompleted(string code)
        {
            onCodeCompleted.Invoke(code);
            SubmitCode();
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

using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CarbonStories
{
    /// <summary>One keyboard input with five visual digit cells.</summary>
    public class FiveDigitCodeInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TextMeshProUGUI[] digits = new TextMeshProUGUI[5];
        [SerializeField] private UnityEvent<string> onCodeChanged = new UnityEvent<string>();
        [SerializeField] private UnityEvent<string> onCodeCompleted = new UnityEvent<string>();

        public string Code => inputField != null ? inputField.text : string.Empty;
        public bool IsComplete => Code.Length == 5;
        public UnityEvent<string> OnCodeChanged => onCodeChanged;
        public UnityEvent<string> OnCodeCompleted => onCodeCompleted;

        private void Awake()
        {
            if (inputField == null)
            {
                Debug.LogError("FiveDigitCodeInput needs its Keyboard Input reference assigned in the scene.", this);
                return;
            }

            inputField.characterLimit = 5;
            inputField.contentType = TMP_InputField.ContentType.IntegerNumber;
            inputField.keyboardType = TouchScreenKeyboardType.NumberPad;
            inputField.onValidateInput = ValidateDigit;
            inputField.onValueChanged.AddListener(HandleChanged);
            HandleChanged(inputField.text);
        }

        private void OnDestroy()
        {
            if (inputField != null)
                inputField.onValueChanged.RemoveListener(HandleChanged);
        }

        public void Focus()
        {
            if (inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }

        public void HideKeyboard()
        {
            if (inputField != null)
                inputField.DeactivateInputField();
        }

        public void Clear()
        {
            if (inputField == null) return;
            inputField.text = string.Empty;
            inputField.caretPosition = 0;
        }

        private static char ValidateDigit(string text, int position, char character)
        {
            return character >= '0' && character <= '9' ? character : '\0';
        }

        private void HandleChanged(string value)
        {
            var cleaned = new StringBuilder(5);
            foreach (char character in value)
            {
                if (character >= '0' && character <= '9')
                    cleaned.Append(character);
                if (cleaned.Length == 5)
                    break;
            }

            string code = cleaned.ToString();
            if (value != code)
            {
                inputField.SetTextWithoutNotify(code);
                inputField.caretPosition = code.Length;
            }

            for (int i = 0; i < digits.Length; i++)
                digits[i].text = i < code.Length ? code[i].ToString() : string.Empty;

            onCodeChanged.Invoke(code);
            if (code.Length == 5)
                onCodeCompleted.Invoke(code);
        }

    }
}

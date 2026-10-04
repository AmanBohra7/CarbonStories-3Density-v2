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
                BuildView();

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

        public void Focus() => inputField.Select();

        public void Clear()
        {
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

        private void BuildView()
        {
            var root = (RectTransform)transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;

            var cells = new GameObject("Digit Cells", typeof(RectTransform), typeof(UnityEngine.UI.HorizontalLayoutGroup));
            cells.transform.SetParent(transform, false);
            var cellsRect = (RectTransform)cells.transform;
            cellsRect.anchorMin = cellsRect.anchorMax = new Vector2(0.5f, 0.5f);
            cellsRect.sizeDelta = new Vector2(1900, 300);
            cellsRect.anchoredPosition = Vector2.zero;
            var layout = cells.GetComponent<UnityEngine.UI.HorizontalLayoutGroup>();
            layout.spacing = 100;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < 5; i++)
            {
                var cell = new GameObject("Digit " + (i + 1), typeof(RectTransform), typeof(UnityEngine.UI.Image));
                cell.transform.SetParent(cells.transform, false);
                ((RectTransform)cell.transform).sizeDelta = new Vector2(300, 300);
                var background = cell.GetComponent<UnityEngine.UI.Image>();
                background.color = new Color(1, 1, 1, 0.9f);
                background.raycastTarget = false;

                var label = new GameObject("Digit Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                label.transform.SetParent(cell.transform, false);
                var labelRect = (RectTransform)label.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var digit = label.GetComponent<TextMeshProUGUI>();
                digit.alignment = TextAlignmentOptions.Center;
                digit.fontSize = 160;
                digit.color = new Color(0.12f, 0.12f, 0.12f);
                digit.raycastTarget = false;
                digits[i] = digit;
            }

            var inputObject = new GameObject("Keyboard Input", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(TMP_InputField));
            inputObject.transform.SetParent(transform, false);
            var inputRect = (RectTransform)inputObject.transform;
            inputRect.anchorMin = inputRect.anchorMax = new Vector2(0.5f, 0.5f);
            inputRect.sizeDelta = new Vector2(1900, 300);
            inputRect.anchoredPosition = Vector2.zero;
            var image = inputObject.GetComponent<UnityEngine.UI.Image>();
            image.color = new Color(1, 1, 1, 0);
            image.raycastTarget = true;

            var textObject = new GameObject("Input Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(inputObject.transform, false);
            var textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var hiddenText = textObject.GetComponent<TextMeshProUGUI>();
            hiddenText.color = Color.clear;
            hiddenText.raycastTarget = false;

            inputField = inputObject.GetComponent<TMP_InputField>();
            inputField.textViewport = inputRect;
            inputField.textComponent = hiddenText;
            inputField.customCaretColor = true;
            inputField.caretColor = Color.clear;
            inputField.selectionColor = Color.clear;
        }
    }
}

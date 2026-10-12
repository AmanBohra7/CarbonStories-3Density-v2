using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace CarbonStories
{
/// <summary>One serialized keyboard input displayed in five digit cells.</summary>
public class FiveDigitCodeInput : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI[] digits = new TextMeshProUGUI[5];
    [SerializeField] private KeyboardScript keyboardPrefab;
    [SerializeField, Range(0.5f, 4f)] private float keyboardScale = 1.05f;
    [SerializeField] private float keyboardBottomPadding = 24f;
    [SerializeField] private bool showFullKeyboard;
    [SerializeField] private UnityEvent<string> onCodeChanged = new UnityEvent<string>();
    [SerializeField] private UnityEvent<string> onCodeCompleted = new UnityEvent<string>();
    private KeyboardScript keyboard;
    private EventTrigger clickTrigger;
    private EventTrigger.Entry clickEntry;
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
        inputField.onSelect.AddListener(ShowKeyboard);
        clickTrigger = inputField.GetComponent<EventTrigger>();
        if (clickTrigger == null) clickTrigger = inputField.gameObject.AddComponent<EventTrigger>();
        clickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
        clickEntry.callback.AddListener(_ => ShowKeyboard(string.Empty));
        clickTrigger.triggers.Add(clickEntry);
        HandleChanged(inputField.text);
    }

    private void OnDestroy()
    {
        if (inputField != null) inputField.onValueChanged.RemoveListener(HandleChanged);
        if (inputField != null) inputField.onSelect.RemoveListener(ShowKeyboard);
        if (clickTrigger != null && clickEntry != null) clickTrigger.triggers.Remove(clickEntry);
    }

    private void OnDisable()
    {
        if (keyboard != null) keyboard.gameObject.SetActive(false);
    }

    public void Focus()
    {
        if (inputField == null || !inputField.IsActive() || !inputField.IsInteractable()) return;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(inputField.gameObject);
        inputField.Select();
        inputField.ActivateInputField();
        ShowKeyboard(string.Empty);
    }

    public void HideKeyboard()
    {
        if (inputField != null) inputField.DeactivateInputField();
        if (keyboard != null) keyboard.gameObject.SetActive(false);
    }

    public void Clear()
    {
        if (inputField == null) return;
        inputField.text = string.Empty;
        inputField.caretPosition = 0;
    }

    private void ShowKeyboard(string _)
    {
        if (keyboardPrefab == null)
        {
            Debug.LogError("FiveDigitCodeInput needs an on-screen keyboard prefab.", this);
            return;
        }

        if (keyboard == null)
        {
            Canvas parentCanvas = GetComponentInParent<Canvas>();
            if (parentCanvas == null)
            {
                Debug.LogError("FiveDigitCodeInput needs a parent Canvas for the on-screen keyboard.", this);
                return;
            }
            keyboard = Instantiate(keyboardPrefab, parentCanvas.transform);
            RectTransform rect = keyboard.transform as RectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 0);
            rect.anchoredPosition = new Vector2(0f, keyboardBottomPadding);

            Canvas overlay = keyboard.GetComponent<Canvas>();
            if (overlay == null) overlay = keyboard.gameObject.AddComponent<Canvas>();
            overlay.overrideSorting = true;
            overlay.sortingOrder = 100;
            if (keyboard.GetComponent<GraphicRaycaster>() == null)
                keyboard.gameObject.AddComponent<GraphicRaycaster>();
        }

        keyboard.transform.localScale = Vector3.one * keyboardScale;
        keyboard.SetNewInputField(inputField);
        keyboard.EngLayoutBig.SetActive(showFullKeyboard);
        keyboard.EngLayoutSml.SetActive(false);
        keyboard.SymbLayout.SetActive(false);
        keyboard.NumberLayout.SetActive(!showFullKeyboard);
        keyboard.gameObject.SetActive(true);
        keyboard.transform.SetAsLastSibling();
    }

    private static char ValidateDigit(string text, int position, char character) =>
        character >= '0' && character <= '9' ? character : '\0';

    private void HandleChanged(string value)
    {
        var cleaned = new StringBuilder(5);
        foreach (char character in value)
        {
            if (character >= '0' && character <= '9') cleaned.Append(character);
            if (cleaned.Length == 5) break;
        }

        string code = cleaned.ToString();
        if (value != code) inputField.SetTextWithoutNotify(code);
        for (int i = 0; i < digits.Length; i++)
            digits[i].text = i < code.Length ? code[i].ToString() : string.Empty;
        if (code.Length == 5 && keyboard != null) keyboard.gameObject.SetActive(false);
        onCodeChanged.Invoke(code);
        if (code.Length == 5) onCodeCompleted.Invoke(code);
    }
}
}

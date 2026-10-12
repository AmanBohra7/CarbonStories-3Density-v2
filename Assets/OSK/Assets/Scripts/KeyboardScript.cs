using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
public class KeyboardScript : MonoBehaviour
{
    public TMP_InputField TextField;
    public GameObject EngLayoutBig,EngLayoutSml, NumberLayout, SymbLayout;
    public GameObject closeBtn;
    [Space]
    public bool darkMode;
    public Sprite[] dark;
    public Sprite[] light;
    public Sprite[] closeDL;

    public bool desktop;
    private void OnEnable()
    {
        SwitchMode();
    }
    public void SwitchMode()
    {
        if (desktop)
        {
            return;
        }
        if (darkMode)
        {
            UnityImageUpdate(EngLayoutBig, dark[0]);
            UnityImageUpdate(EngLayoutSml, dark[1]);
            UnityImageUpdate(NumberLayout, dark[2]);
            UnityImageUpdate(SymbLayout, dark[3]);
            UnityImageUpdate(closeBtn, closeDL[0]);
        }
        else
        {
            UnityImageUpdate(EngLayoutBig, light[0]);
            UnityImageUpdate(EngLayoutSml, light[1]);
            UnityImageUpdate(NumberLayout, light[2]);
            UnityImageUpdate(SymbLayout, light[3]);
            UnityImageUpdate(closeBtn, closeDL[1]);
        }
        
    }
    void UnityImageUpdate(GameObject obj,Sprite img)
    {
        obj.GetComponent<UnityEngine.UI.Image>().sprite = img;
    }
    public void AlphabetFunction(string alphabet)
    {
        if (TextField.characterLimit > 0 && TextField.text.Length >= TextField.characterLimit)
        {
            return; // Don't add more characters
        }
        // Insert character at cursor position
        int cursorPos = TextField.caretPosition;
        string currentText = TextField.text;

        TextField.text = currentText.Insert(cursorPos, alphabet);

        // Move cursor to after the inserted character
        TextField.caretPosition = cursorPos + alphabet.Length;
        TextField.ForceLabelUpdate();
    }

    public void BackSpace()
    {
        if (TextField.text.Length > 0 && TextField.caretPosition > 0)
        {
            int cursorPos = TextField.caretPosition;
            string currentText = TextField.text;

            // Remove character before cursor
            TextField.text = currentText.Remove(cursorPos - 1, 1);

            // Set cursor position after deletion
            TextField.caretPosition = cursorPos - 1;
            TextField.ForceLabelUpdate();
        }
    }
    public void NewLine()
    {
        // Insert new line character at cursor position
        int cursorPos = TextField.caretPosition;
        string currentText = TextField.text;

        TextField.text = currentText.Insert(cursorPos, "\n");

        // Move cursor to after the new line
        TextField.caretPosition = cursorPos + 1;
        TextField.ForceLabelUpdate();
    }
    public void Delete()
    {
        if (TextField.text.Length > 0 && TextField.caretPosition < TextField.text.Length)
        {
            int cursorPos = TextField.caretPosition;
            string currentText = TextField.text;

            // Remove character at cursor position
            TextField.text = currentText.Remove(cursorPos, 1);

            // Keep cursor at same position
            TextField.caretPosition = cursorPos;
            TextField.ForceLabelUpdate();
        }
    }

    public void CloseAllLayouts()
    {
       // EngLayoutSml.SetActive(false);
       // EngLayoutBig.SetActive(false);
       // SymbLayout.SetActive(false);
       // NumberLayout.SetActive(false);
       // closeBtn.SetActive(false);
        gameObject.SetActive(false);
    }

    public void ShowLayout(GameObject SetLayout)
    {
        CloseAllLayouts();
        SetLayout.SetActive(true);
        closeBtn.SetActive(true);
    }

    public void Clear()
    {
        TextField.text = "";

    }


    public void SetNewInputField(TMP_InputField newInputField)
    {
        TextField = newInputField;
    }
}

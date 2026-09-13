using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace CarbonStories
{
    public class ConfirmationPopup : MonoBehaviour
    {
        CanvasGroup cg;

        [SerializeField] Image icon;
        [SerializeField] TextMeshProUGUI iconName;
        [SerializeField] TextMeshProUGUI description;

        void Awake()
        {
            cg = GetComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0;
        }

        public void Show(Option option)
        {
            icon.sprite = option.icon;
            iconName.text = option.name;
            description.text = option.description;

            LeanTween.alphaCanvas(cg, 1, 0.3f).setOnComplete(() =>
            {
                cg.interactable = true;
                cg.blocksRaycasts = true;
            });
        }

        public void Hide()
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            LeanTween.alphaCanvas(cg, 0, 0.45f);
        }
       
    }
    
}
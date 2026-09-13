using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CarbonStories
{
    public class ScorePopup : MonoBehaviour
    {

        CanvasGroup cg;
        [SerializeField] TextMeshProUGUI scoreText;

        void Awake()
        {
            cg = GetComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;
            cg.alpha = 0;
        }


        public void Show(Option option)
        {
            scoreText.text = "+" + option.score.ToString();
            scoreText.color = option.scoreColor;
            LeanTween.alphaCanvas(cg, 1, 0.35f);
        }

        public void Hide()
        {
            cg.interactable = false;
            cg.blocksRaycasts = false;
            LeanTween.alphaCanvas(cg, 0, 0.25f);
        }
    }
}
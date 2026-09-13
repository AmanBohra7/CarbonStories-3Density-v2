using System.Collections.Generic;
using UnityEngine;
using Lean.Transition;

namespace CarbonStories
{
    public class TutorialScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;
        public List<GameObject> tutorialPages = new List<GameObject>();
        public int currentIndex;

        [Header("Media")]
        [SerializeField] private CustomMediaPlayer customMediaPlayer;

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            StartCoroutine(InitializeScreen(info));
        }

        private System.Collections.IEnumerator InitializeScreen(ScreenLoadingInfo info)
        {
            yield return App.Instance.LoadConfiguration();
            customMediaPlayer?.Stop();
            ShowPage(0);
            loadIn.BeginAllTransitions();
        }

        protected override void OnLoadingCompleted()
        {
        }

        public void MoveNext()
        {
            ShowPage(currentIndex + 1);
        }

        public void MovePrev()
        {
            ShowPage(currentIndex - 1);
        }

        private void ShowPage(int index)
        {
            if (tutorialPages == null || tutorialPages.Count == 0)
            {
                currentIndex = 0;
                customMediaPlayer?.Stop();
                return;
            }

            currentIndex = ((index % tutorialPages.Count) + tutorialPages.Count) % tutorialPages.Count;

            for (int i = 0; i < tutorialPages.Count; i++)
            {
                if (tutorialPages[i] != null)
                {
                    tutorialPages[i].SetActive(i == currentIndex);
                }
            }

            if (customMediaPlayer == null)
            {
                Debug.LogWarning("TutorialScreen has no CustomMediaPlayer assigned.", this);
                return;
            }

            string videoPath = App.Instance.Configuration.TutorialVideo(currentIndex);
            if (string.IsNullOrWhiteSpace(videoPath))
                customMediaPlayer.Stop();
            else
                customMediaPlayer.PlayVideo(videoPath);
        }

        public void SkipTutorial()
        {
            CallForScenarioSceen();
        }

        public void TutorialCompleted()
        {
            CallForScenarioSceen();
        }

        void CallForScenarioSceen()
        {
            App.Instance.LoadScreen(App.Instance.ScenarioScreen);
        }

        public void GoBackFromTutorial()
        {
            App.Instance.LoadScreen(App.Instance.InfoScreen);
        }

        protected override void OnReset()
        {
            customMediaPlayer?.Stop();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            StopAllCoroutines();
            customMediaPlayer?.Stop();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }
    }
}

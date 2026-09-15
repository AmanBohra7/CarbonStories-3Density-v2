using System;
using System.Collections.Generic;
using Lean.Transition;
using TMPro;
using UnityEngine;

namespace CarbonStories
{
    [Serializable]
    public class Page
    {
        public CanvasGroup canvasGroup;
        [Tooltip("Video path relative to the StreamingAssets folder.")]
        public string videoPath;
        public TextMeshProUGUI heading;
        public TextMeshProUGUI description;
        [Tooltip("Optional. Assign this on introduction pages that display a persona image.")]
        public UnityEngine.UI.Image image;
    }

    public class IntroductionScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        [Header("Data")]
        [SerializeField] private PersonaLoader personaLoader;

        [Header("Pages")]
        [SerializeField] private List<Page> pages = new List<Page>();

        [Header("Media")]
        [SerializeField] private CustomMediaPlayer customMediaPlayer;

        [Space(10)]
        [SerializeField] TextMeshProUGUI nameHeading;
        [SerializeField] TextMeshProUGUI designationHeading;

        private int currentPageIndex;

        protected override void Awake()
        {
            base.Awake();
            SetInitialPageState();
        }

        public void GoToSelectionScreen()
        {
            App.Instance.LoadScreen(App.Instance.SelectionScreen);
        }

        public void CallCompleteIntroduction()
        {
            App.Instance.LoadScreen(App.Instance.TutorialScreen);
        }

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            StopMediaPlayer();
            SetInitialPageState();
            LoadPersonaContent();
            loadIn.BeginAllTransitions();
        }

        protected override void OnLoadingCompleted()
        {
            ShowPage(0);
        }

        protected override void OnReset()
        {
            StopMediaPlayer();
            SetInitialPageState();
        }

        protected override void OnUnloadingStarted(ScreenLoadingInfo info)
        {
            StopMediaPlayer();
            loadOut.BeginAllTransitions();
        }

        protected override void OnUnloadingCompleted()
        {
        }

        public void MoveNext()
        {
            if (currentPageIndex < pages.Count - 1)
            {
                ShowPage(currentPageIndex + 1);
            }
        }

        public void MovePrev()
        {
            if (currentPageIndex > 0)
            {
                ShowPage(currentPageIndex - 1);
            }
        }

        private void ShowPage(int pageIndex)
        {
            if (!IsValidPage(pageIndex))
            {
                return;
            }

            currentPageIndex = pageIndex;
            SetOnlyPageVisible(pageIndex);
            PlayPageMedia(pageIndex);
        }

        private void SetInitialPageState()
        {
            currentPageIndex = 0;

            if (pages.Count > 0)
            {
                SetOnlyPageVisible(currentPageIndex);
            }
        }

        private void SetOnlyPageVisible(int pageIndex)
        {
            for (int i = 0; i < pages.Count; i++)
            {
                CanvasGroup canvasGroup = GetCanvasGroup(i);
                if (canvasGroup == null)
                {
                    continue;
                }

                bool isCurrentPage = i == pageIndex;
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                canvasGroup.gameObject.SetActive(isCurrentPage);
            }
        }

        private void PlayPageMedia(int pageIndex)
        {
            if (customMediaPlayer == null)
            {
                Debug.LogWarning("IntroductionScreen has no CustomMediaPlayer assigned.", this);
                return;
            }

            string videoPath = pages[pageIndex].videoPath;
            if (string.IsNullOrWhiteSpace(videoPath))
            {
                customMediaPlayer.Stop();
                return;
            }

            customMediaPlayer.PlayVideo(videoPath);
        }

        private void StopMediaPlayer()
        {
            customMediaPlayer?.Stop();
        }

        private void LoadPersonaContent()
        {
            if (personaLoader == null)
            {
                Debug.LogError("IntroductionScreen has no PersonaLoader assigned.", this);
                return;
            }

            if (!personaLoader.TryGetPersonaData(App.Instance.ActivePersona, out PersonaData personaData))
            {
                Debug.LogError($"Persona data for {App.Instance.ActivePersona} has not loaded.", this);
                return;
            }

            //nameHeading.text = ""
            designationHeading.text = personaData.name + " - " + personaData.designation;

            IntroductionData introduction = personaData.introduction;
            if (introduction == null)
            {
                Debug.LogWarning($"No introduction data exists for {App.Instance.ActivePersona}.", this);
                return;
            }

            IntroductionPageData[] pageData =
            {
                introduction.page1,
                introduction.page2,
                introduction.page3
            };

            int pageCount = Mathf.Min(pages.Count, pageData.Length);
            for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
            {
                SetPageContent(pages[pageIndex], pageData[pageIndex]);
            }
        }

        private static void SetPageContent(Page page, IntroductionPageData data)
        {
            if (page == null || data == null)
                return;

            if (page.heading != null)
                page.heading.text = data.heading ?? string.Empty;

            if (page.description != null)
                page.description.text = data.subheading ?? string.Empty;

            page.videoPath = data.videoPath ?? string.Empty;

            if (page.image != null)
            {
                page.image.sprite = data.image;
                page.image.enabled = data.image != null;
            }
        }

        private bool IsValidPage(int pageIndex)
        {
            return pageIndex >= 0 &&
                   pageIndex < pages.Count &&
                   pages[pageIndex] != null &&
                   pages[pageIndex].canvasGroup != null;
        }

        private CanvasGroup GetCanvasGroup(int pageIndex)
        {
            return pageIndex >= 0 && pageIndex < pages.Count && pages[pageIndex] != null
                ? pages[pageIndex].canvasGroup
                : null;
        }
    }
}

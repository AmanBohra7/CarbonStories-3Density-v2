using Lean.Transition;
using NaughtyAttributes;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CarbonStories
{
    public class SelectionScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        [SerializeField] Button selectBtn;
        private bool _personaSelected = false;

        [ReadOnly] private int _selectedIndex = -1;

        [SerializeField] List<GameObject> highlights;

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            loadIn.BeginAllTransitions();
            LoadPersonas();

            _personaSelected = false;
            selectBtn.interactable = false;
            foreach (var item in highlights)
            {
                item.SetActive(false);
            }
        }

        private void LoadPersonas()
        {
            // loading from cms and showing on screen with names and disgnations 
        }

        public void OnPersonaSelected(int index)
        {
            _selectedIndex = index;
            HiglightBtn(_selectedIndex);
            _personaSelected = true;
            selectBtn.interactable = true;
        }

        void HiglightBtn(int index)
        {
            foreach (var item in highlights)
            {
                item.SetActive(false);
            }
            highlights[index].SetActive(true);
        }

        public void OnFinalized()
        {
            //if (_selectedIndex != 0) return; // temp

            App.Instance.OnSelectedPersonaIndex(_selectedIndex);
            App.Instance.LoadScreen(App.Instance.InfoScreen);
        }


        protected override void OnLoadingCompleted()
        {
        }

        protected override void OnReset()
        {
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

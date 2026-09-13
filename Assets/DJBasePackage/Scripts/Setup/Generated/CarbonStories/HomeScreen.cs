using UnityEngine;
using Lean.Transition;

namespace CarbonStories
{
    public class HomeScreen : DJScreen
    {
        public LeanMethod loadIn;
        public LeanMethod loadOut;

        protected override void OnLoadingStarted(ScreenLoadingInfo info)
        {
            loadIn.BeginAllTransitions();
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

using System;
using UnityEngine;

namespace CarbonStories
{
    public abstract class DJScreenTransitionAnimation : MonoBehaviour
    {
        public abstract void Begin(Action onMidPointReached = null);
        public abstract void End(Action onTransitionEnd = null);
        [SerializeField] protected ScreenTransitionAnimationInfo TransitionInfo;
    }
}

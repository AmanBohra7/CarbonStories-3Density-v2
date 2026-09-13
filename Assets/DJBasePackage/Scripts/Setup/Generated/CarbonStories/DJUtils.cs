using System;
using System.Collections;
using UnityEngine;

namespace CarbonStories
{
    public static class DJUtils
    {
        public static IEnumerator WaitAndExecute(float delay, Action callback)
        {
            yield return new WaitForSeconds(delay);
            callback?.Invoke();
        }
    }

    public class DJScreenData
    {
        public string Name;
        public DJScreen Screen;
    }

    public enum DJScreenState
    {
        LOADED = 0,
        UNLOADED = 1,
        LOADING = 2
    }

    public class ScreenLoadingInfo
    {
        public DJScreen prevScreen;
        public DJScreen nextScreen;
    }

    [System.Serializable]
    public class ScreenTransitionAnimationInfo
    {
        public float MidTime;
    }
}

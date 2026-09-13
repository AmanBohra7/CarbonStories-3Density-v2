using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CarbonStories
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class DJScreen : MonoBehaviour
    {
        [SerializeField] protected DJScreenState currentState = DJScreenState.UNLOADED;
        Action callbackRef;

        CanvasGroup _canvasGroup;

        [SerializeField] public DJScreenInfoScriptableObject screenInfo;

        public bool IsDefaultLoaded = false;

        [SerializeField] protected bool DisableLoadUnloadAnimation = false;

        protected virtual void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = IsDefaultLoaded;
            if (IsDefaultLoaded) currentState = DJScreenState.LOADED;
        }

        public void Load(ScreenLoadingInfo info, Action onComplete)
        {
            callbackRef = onComplete;
            currentState = DJScreenState.LOADING;

            OnLoadingStarted(info);
            onLoadingStarted.Invoke();

            LeanTween.value(0, 1, !DisableLoadUnloadAnimation ? screenInfo.LoadingTime : 0).setOnComplete(LoadingCompleted);
        }

        public void Unload(ScreenLoadingInfo info, Action onComplete)
        {
            callbackRef = onComplete;
            currentState = DJScreenState.LOADING;

            OnUnloadingStarted(info);
            onUnloadingStarted.Invoke();

            LeanTween.value(0, 1, !DisableLoadUnloadAnimation ? screenInfo.UnloadingTime : 0).setOnComplete(UnLoadingCompleted);
        }

        private void LoadingCompleted()
        {
            currentState = DJScreenState.LOADED;
            _canvasGroup.blocksRaycasts = true;
            callbackRef?.Invoke();
            OnLoadingCompleted();
            onLoadingCompleted.Invoke();
        }

        private void UnLoadingCompleted()
        {
            currentState = DJScreenState.UNLOADED;
            _canvasGroup.blocksRaycasts = false;
            callbackRef?.Invoke();
            OnUnloadingCompleted();
            onUnloadingCompleted.Invoke();
        }

        protected abstract void OnLoadingStarted(ScreenLoadingInfo info);
        protected abstract void OnUnloadingStarted(ScreenLoadingInfo info);
        protected abstract void OnLoadingCompleted();
        protected abstract void OnUnloadingCompleted();
        protected abstract void OnReset();

        [Space(10)]
        [SerializeField] private UnityEvent onLoadingStarted;
        [SerializeField] private UnityEvent onUnloadingStarted;
        [SerializeField] private UnityEvent onLoadingCompleted;
        [SerializeField] private UnityEvent onUnloadingCompleted;

        public DJScreenInfoScriptableObject GetInfo() => screenInfo;
    }
}

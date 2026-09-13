using AYellowpaper.SerializedCollections;
using System;
using System.Collections;
using UnityEngine;

namespace CarbonStories
{
    public class DJScreenHandler : MonoBehaviour
    {
        public static DJScreenHandler Instance;

        private void Awake()
        {
            Instance = this;
        }

        private DJScreenInfoScriptableObject _lastScreen = null;
        private DJScreenInfoScriptableObject _currentScreen = null;
        public DJScreenInfoScriptableObject CurrentScreen => _currentScreen;

        [SerializeField] bool PreloadInitialScreen = false;
        [SerializeField] DJScreenInfoScriptableObject InitialLoadedScreen = null;

        [Space(10)]
        [SerializeField] bool UseCustomTransition = false;
        [SerializeField] Transform TransitionParent = null;
        [SerializeField] GameObject TransitionAnimationPrefab = null;

        [Space(10)]
        [SerializedDictionary("Name", "Reference")]
        [SerializeField] public SerializedDictionary<DJScreenInfoScriptableObject, DJScreen> Screens;

        private bool _inTransition = false;

        private void Start()
        {
            if (InitialLoadedScreen != null)
            {
                _currentScreen = InitialLoadedScreen;
            }
        }

        public void LoadScreen(DJScreenInfoScriptableObject screenName, Action<DJScreenInfoScriptableObject, DJScreenInfoScriptableObject> onComplete = null)
        {
            UpdateCurrentScreen(screenName, onComplete);
        }

        public void UpdateCurrentScreen(DJScreenInfoScriptableObject newScreen, Action<DJScreenInfoScriptableObject, DJScreenInfoScriptableObject> onComplete = null)
        {
            if (_inTransition)
            {
                Debug.LogError("In transition cannot change to: " + newScreen);
                return;
            }

            _inTransition = true;
            _lastScreen = _currentScreen;
            StartCoroutine(ProcessUpdateScreen(_lastScreen, newScreen, (oldScreen, updatedNewScreen) =>
            {
                _inTransition = false;
                _currentScreen = updatedNewScreen;
                onComplete?.Invoke(oldScreen, updatedNewScreen);
            }));
        }

        IEnumerator ProcessUpdateScreen(DJScreenInfoScriptableObject oldScreen, DJScreenInfoScriptableObject newScreen, Action<DJScreenInfoScriptableObject, DJScreenInfoScriptableObject> onComplete)
        {
            yield return new WaitForSeconds(0);
            Debug.Log($"ProcessUpdateScreen {oldScreen} to {newScreen}");

            DJScreen oldScreenRef = null;
            if (oldScreen != null && Screens.ContainsKey(oldScreen)) oldScreenRef = Screens[oldScreen];

            DJScreen newScreenRef = null;
            if (Screens.ContainsKey(newScreen)) newScreenRef = Screens[newScreen];

            if (newScreenRef == null)
            {
                Debug.LogError("No screen found to load!");
                yield break;
            }

            ScreenLoadingInfo info = new ScreenLoadingInfo();
            info.prevScreen = oldScreenRef;
            info.nextScreen = newScreenRef;

            DJScreenTransitionAnimation screenTransition = null;
            if (UseCustomTransition && TransitionAnimationPrefab != null)
            {
                GameObject transitionObject = Instantiate(TransitionAnimationPrefab, TransitionParent);
                screenTransition = transitionObject.GetComponent<DJScreenTransitionAnimation>();
            }

            if (UseCustomTransition && screenTransition != null)
            {
                screenTransition.Begin(() =>
                {
                    if (oldScreenRef != null)
                    {
                        oldScreenRef.Unload(info, () =>
                        {
                            newScreenRef.Load(info, () =>
                            {
                                onComplete(oldScreen, newScreen);
                                screenTransition.End(() => Destroy(screenTransition.gameObject));
                            });
                        });
                    }
                    else
                    {
                        newScreenRef.Load(info, () =>
                        {
                            onComplete(oldScreen, newScreen);
                            screenTransition.End(() => Destroy(screenTransition.gameObject));
                        });
                    }
                });
            }
            else
            {
                if (oldScreenRef != null)
                {
                    oldScreenRef.Unload(info, () =>
                    {
                        newScreenRef.Load(info, () => onComplete(oldScreen, newScreen));
                    });
                }
                else
                {
                    newScreenRef.Load(info, () => onComplete(oldScreen, newScreen));
                }
            }
        }
    }
}

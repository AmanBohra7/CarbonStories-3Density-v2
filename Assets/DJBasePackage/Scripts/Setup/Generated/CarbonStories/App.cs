using NaughtyAttributes;
using System;
using UnityEngine;
using UnityEngine.PlayerLoop;
using UnityEngine.SceneManagement;

namespace CarbonStories
{
    public enum Persona
    {
        Persona_1,
        Persona_2,
        Persona_3,
        Persona_4,
        Persona_5,
        Persona_6
    }
    public class App : MonoBehaviour
    {
        public static App Instance { get; private set; }
        public GameConfiguration Configuration { get; private set; } = new GameConfiguration();
        public ResultDescriptions ResultDescriptions { get; private set; } = new ResultDescriptions();
        private bool configurationLoaded;
        private bool configurationLoading;

        public System.Collections.IEnumerator LoadConfiguration()
        {
            if (configurationLoading)
            {
                while (!configurationLoaded)
                    yield return null;
                yield break;
            }
            if (configurationLoaded)
                yield break;

            configurationLoading = true;
            yield return ConfigurationReader.Load<GameConfiguration>("config.json", data => Configuration = data);
            if (float.IsNaN(Configuration.questionWaitTime) || float.IsInfinity(Configuration.questionWaitTime) || Configuration.questionWaitTime <= 0f)
                Configuration.questionWaitTime = 60f;
            yield return ConfigurationReader.Load<ResultDescriptions>("Results/descriptions.json", data => ResultDescriptions = data);
            configurationLoaded = true;
            configurationLoading = false;
        }

        private void Awake()
        {
            Instance = this;
            StartCoroutine(LoadConfiguration());
        }

        public DJScreen HomeScreen;
        public DJScreen SelectionScreen;
        public DJScreen InfoScreen;
        public DJScreen TutorialScreen;
        public DJScreen ScenarioScreen;
        public DJScreen ResultScreen;

        [Space(10)]
        [ReadOnly] [SerializeField] Persona currentPersona;
        public Persona ActivePersona => currentPersona;
        public UserResult CurrentUserResult { get; set; }

        [Space(10)]
        public bool toTest;
        public UserResult TestResult;

        public void OnSelectedPersonaIndex(int index)
        {
            currentPersona = (Persona)index;
        }

        public void GoToHomeScreen()
        {
            DJScreenHandler.Instance.LoadScreen(HomeScreen.GetInfo());
        }

        public void GoToSelectionScreen()
        {
            if (toTest)
            {
                Testing();
            }
            else
            {
                DJScreenHandler.Instance.LoadScreen(SelectionScreen.GetInfo());
            }
        }


        void Testing()
        {
            currentPersona = Persona.Persona_1;
            CurrentUserResult = TestResult;
            DJScreenHandler.Instance.LoadScreen(ScenarioScreen.GetInfo());
        }

        public void LoadScreen(DJScreen screen)
        {
            DJScreenHandler.Instance.LoadScreen(screen.GetInfo());
        }

        public void RestartApplication()
        {
            SceneManager.LoadScene(0);
        }


        void Update()
        {
            if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
            {
                RestartApplication();
            }   
        }
    }
}

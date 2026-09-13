using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarbonStories
{
    [Serializable]
    public class ScenarioAnswerResult
    {
        public int scenarioNumber;
        public Sprite icon;
        public string name = string.Empty;
        public int score;
        public bool answered;
    }

    [Serializable]
    public class UserResult
    {
        public Persona persona;
        public List<ScenarioAnswerResult> answers = new List<ScenarioAnswerResult>();

        public int TotalScore
        {
            get
            {
                int total = 0;
                foreach (ScenarioAnswerResult answer in answers)
                    total += answer.score;
                return total;
            }
        }
    }
}

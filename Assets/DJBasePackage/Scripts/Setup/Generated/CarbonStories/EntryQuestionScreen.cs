using System.Collections.Generic;
using UnityEngine;

namespace CarbonStories
{
    public class EntryQuestionScreen : PersonaQuestionScreen
    {
        [SerializeField, Min(0f)] private float secondsBeforeNextQuestion = 2f;

        protected override float AutoAdvanceDelaySeconds => secondsBeforeNextQuestion;
        protected override List<QuestionData> GetQuestions(SharedQuestionData data) => data.entryQuestions;
    }
}

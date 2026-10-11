using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarbonStories
{
    [Serializable]
    public class QuestionData
    {
        [TextArea] public string question;
    }

    [Serializable]
    public class SharedQuestionData
    {
        public List<QuestionData> entryQuestions = new List<QuestionData>();
        public List<QuestionData> exitQuestions = new List<QuestionData>();
    }
}

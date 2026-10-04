using System;
using System.Collections.Generic;
using UnityEngine;

namespace CarbonStories
{
    [Serializable]
    public class QuestionData
    {
        [TextArea] public string question;
        public string[] options = new string[4];
    }

    [Serializable]
    public class SharedQuestionData
    {
        public List<QuestionData> entryQuestions = new List<QuestionData>();
        public List<QuestionData> exitQuestions = new List<QuestionData>();
    }
}

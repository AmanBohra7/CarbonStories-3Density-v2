using System.Collections.Generic;

namespace CarbonStories
{
    public class EntryQuestionScreen : PersonaQuestionScreen
    {
        protected override List<QuestionData> GetQuestions(SharedQuestionData data) => data.entryQuestions;
    }
}

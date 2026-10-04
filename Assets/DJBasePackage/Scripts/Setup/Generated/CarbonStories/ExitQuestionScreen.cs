using System.Collections.Generic;

namespace CarbonStories
{
    public class ExitQuestionScreen : PersonaQuestionScreen
    {
        protected override List<QuestionData> GetQuestions(SharedQuestionData data) => data.exitQuestions;
    }
}

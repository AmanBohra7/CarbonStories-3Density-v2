using System.Collections.Generic;

namespace CarbonStories
{
    public class ExitQuestionScreen : PersonaQuestionScreen
    {
        protected override List<PersonaQuestionData> GetQuestions(PersonaData persona) => persona.exitQuestions;
    }
}

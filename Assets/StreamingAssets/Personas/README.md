# Scenario data

Open `../persona-editor.html` in Chrome or Edge, select this `Personas` folder,
and use the editor to update these files. Browsers require you to select the folder
before the page is allowed to overwrite JSON files.

Create one JSON file per persona:

- `persona_1.json`
- `persona_2.json`
- `persona_3.json`
- `persona_4.json`
- `persona_5.json`
- `persona_6.json`

Each file contains persona details and a `scenarios` array. Question image, option
icon, and option video paths are relative to `StreamingAssets`. Color channels use
the 0–255 range.

At runtime, `PersonaLoader` loads all six files during `Start`. Access a loaded
persona with `TryGetPersonaData(Persona.Persona_1, out PersonaData data)`, or
wait for `AllPersonasLoaded` before reading the `Personas` dictionary.

```json
{
  "name": "Sustainability Leader",
  "designation": "Plant Manager",
  "scenarios": [
    {
    "number": 1,
    "question": "How will you travel today?",
    "hint": "Consider the carbon impact of each choice.",
    "questionImagePath": "Personas/persona_1/scenarios/1/images/question_image.png",
    "option1": {
      "icon": "Personas/persona_1/scenarios/1/icons/1.png",
      "videoPath": "Personas/persona_1/scenarios/1/videos/Scenario 1 A.mp4",
      "name": "Walk",
      "description": "Travel on foot.",
      "score": 10,
      "scoreText": "+10",
      "scoreColor": { "r": 51, "g": 204, "b": 77, "a": 255 },
      "aiResponse": "Walking is the lowest-carbon choice."
    },
    "option2": {
      "icon": "Personas/persona_1/scenarios/1/icons/2.png",
      "videoPath": "Personas/persona_1/scenarios/1/videos/Scenario 1 B.mp4",
      "name": "Bus",
      "description": "Use public transport.",
      "score": 5,
      "scoreText": "+5",
      "scoreColor": { "r": 230, "g": 179, "b": 51, "a": 255 },
      "aiResponse": "Public transport reduces emissions per passenger."
    },
    "option3": {
      "icon": "Personas/persona_1/scenarios/1/icons/3.png",
      "videoPath": "Personas/persona_1/scenarios/1/videos/Scenario 1 C.mp4",
      "name": "Car",
      "description": "Drive alone.",
      "score": -5,
      "scoreText": "-5",
      "scoreColor": { "r": 230, "g": 51, "b": 51, "a": 255 },
      "aiResponse": "Driving alone usually has a higher carbon impact."
    }
    }
  ]
}
```

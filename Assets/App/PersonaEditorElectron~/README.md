# Persona Loader desktop app

The app remembers the last selected StreamingAssets folder containing Personas. On the first launch, or if that folder is unavailable, it asks you to select a folder. Canceling that startup selection closes the app. Use Change folder in the header to choose another content folder; unsaved changes require confirmation before switching. The executable can live anywhere. All persona files, config, grade descriptions and media are read and saved relative to the selected folder.

Entry and exit questions are shared across all personas. Edit them in the **Questions** tab; the CMS saves them to `StreamingAssets/questions.json`. Persona JSON files contain only persona-specific content.

Unity ignores this development folder because its name ends in `~`. Keep that suffix so `node_modules` are excluded from Unity import and builds. The packaged app is built directly into `Assets/StreamingAssets/dist`, which Unity includes in game builds.

Development (run commands from `Assets/App/PersonaEditorElectron~`):

```powershell
npm install
npm start
```

Fast-launch Windows build (recommended):

```powershell
npm run build
```

The build writes directly to `Assets/StreamingAssets/dist/win-unpacked`. Launch `Persona Loader.exe` inside it. End users do not need Node.js or an installer. Keep the directory together; unlike the old portable executable, it does not unpack itself on every launch.

The legacy single-file build is still available with `npm run build:portable`, but it starts more slowly because Electron must extract itself to the temporary directory each time.

## Confirmation overlay videos

Each decision option has a **Confirmation overlay video path (optional)** field, separate from its **Answer video path**. The default is `Personas/persona_1/scenarios/1/optionVideos/1.mp4`, using the current persona, scenario number, and option number (1, 2, or 3). Use the media preview and Replace control to add or replace a video at that path, then save the persona.

The original confirmation and score timing stays the same. The game starts an existing overlay video alongside the answer video, plays it once, and fades it out on completion. If the answer finishes first, the overlay also fades out before the next options appear. Missing files are skipped independently: `1.mp4` and `3.mp4` can exist without `2.mp4`. Assign a separate Option Media Player and Option Video Canvas Group on ScenarioScreen in Unity. ScenarioScreen controls the additional CanvasGroup fade using Option Video Fade Duration; CustomMediaPlayer controls the video fade.

The CMS page source is `persona-editor.html` in this Electron project; both development and Windows packaging use this file. Run `npm run build` after CMS edits to refresh the packaged app in `StreamingAssets/dist/win-unpacked` before making a Unity build.

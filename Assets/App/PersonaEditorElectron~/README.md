# Persona Loader desktop app

The app remembers the last selected StreamingAssets folder containing Personas. On the first launch, or if that folder is unavailable, it asks you to select a folder. Canceling that startup selection closes the app. Use Change folder in the header to choose another content folder; unsaved changes require confirmation before switching. The executable can live anywhere. All persona files, config, grade descriptions and media are read and saved relative to the selected folder.

Unity ignores this development folder because its name ends in `~`. Keep that suffix so `node_modules` and `dist` are excluded from Unity import and builds. `.gitignore` alone does not exclude files from Unity.

Development (run commands from `Assets/App/PersonaEditorElectron~`):

```powershell
npm install
npm start
```

Fast-launch Windows build (recommended):

```powershell
npm run build
```

Copy the complete `dist/win-unpacked` directory to `StreamingAssets/Persona-Loader`. Launch `Persona Loader.exe` inside it. End users do not need Node.js or an installer. Keep the directory together; unlike the old portable executable, it does not unpack itself on every launch.

The legacy single-file build is still available with `npm run build:portable`, but it starts more slowly because Electron must extract itself to the temporary directory each time.

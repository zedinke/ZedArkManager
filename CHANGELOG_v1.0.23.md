# Release v1.0.23

## Changes

- Added Text Editor button in Config window for direct text editing of config files
- Created new ConfigTextEditorWindow for raw text editing of Game.ini and GameUserSettings.ini
- Text editor allows adding, removing, and editing lines directly in the config files
- Text editor features:
  - Dropdown to select between Game.ini and GameUserSettings.ini
  - Load button to reload the file from server
  - Large text box with Consolas font for easy editing
  - Save button to save changes back to server
  - Automatic file loading when window opens
  - Automatic file reloading when switching between config files

## Technical Details

- Created `ConfigTextEditorWindow.xaml` and `ConfigTextEditorWindow.xaml.cs`
- Added `OpenTextEditorCommand` to `ConfigViewModel`
- Added "Szöveges Szerkesztő" button to `ConfigWindow.xaml`
- Added localization strings: "text_editor" and "config_saved_successfully"
- Text editor uses `ConfigService.ReadConfigFileAsync` and `ConfigService.SaveConfigFileAsync` for file operations

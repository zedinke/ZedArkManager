# Release v1.0.20

## Changes

- Fixed config files load button not working properly
- Added comprehensive logging to config loading process for debugging
- Improved property change notifications for config files (GameIni, GameUserSettingsIni, CurrentIniFile)
- Enhanced error handling in LoadConfigAsync method
- Added null checks and validation before loading config files
- Improved RelayCommandSyncTask error handling to catch and display exceptions
- Config files now properly refresh UI when loaded manually via button click
- Fixed automatic loading to only run if config files are not already loaded

## Technical Details

- Enhanced `LoadConfigAsync` method with:
  - Comprehensive logging at each step
  - Null checks for ServerInstance and ConfigService
  - Proper dispatcher invocation for UI updates
  - Better error messages with stack traces
- Added `OnPropertyChanged` calls for `GameIni` and `GameUserSettingsIni` properties
- Improved `RelayCommandSyncTask.Execute` to catch and display async exceptions
- Added logging to `LoadCommand` CanExecute and Execute methods
- Modified automatic loading in ConfigWindow to check if files are already loaded

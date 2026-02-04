# Release v1.0.22

## Changes

- Fixed config file loading issue - config files now load correctly
- ConfigService now dynamically finds Instance_* directory instead of assuming server name equals instance name
- ConfigService uses server DirectoryPath instead of server Name to locate config files
- Added FindInstanceDirectoryAsync method to locate the correct Instance_* directory
- Config files (Game.ini and GameUserSettings.ini) can now be loaded and edited properly

## Technical Details

- Modified `ConfigService.GetConfigPath` to accept `serverDirectoryPath` instead of `serverName`
- Added `FindInstanceDirectoryAsync` method that searches for Instance_* directories using `find` command
- Updated `ReadConfigFileAsync` and `SaveConfigFileAsync` to use `serverDirectoryPath` parameter
- Updated `ConfigViewModel.LoadConfigAsync` to pass `DirectoryPath` instead of `Name`
- Updated `ConfigViewModel.SaveConfigAsync` to pass `DirectoryPath` instead of `Name`
- Added fallback logic to extract instance name from directory path if Instance_* directory is not found

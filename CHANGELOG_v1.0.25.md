# Changelog v1.0.25

## New Features
- Added search functionality to Server Configuration Settings window
  - Search box filters configuration entries in real-time
  - Searches across section names, keys, values, descriptions, and content
  - Works with both Game.ini and GameUserSettings.ini files
- Added search functionality to Text Editor window
  - Search box to find text in configuration files
  - "Find Next" button to navigate through search results
  - Automatically highlights and scrolls to found text
  - Case-insensitive search

## Technical Details
- Added `SearchText` property to `ConfigViewModel` with real-time filtering
- Implemented `FilterIniFile` method to filter sections and lines based on search text
- Added `FilteredCurrentIniFile` property that returns filtered or unfiltered data based on search
- Added search TextBox and Find Next button to `ConfigTextEditorWindow`
- Implemented text search and highlighting in `ConfigTextEditorWindow.xaml.cs`
- Added localization strings: "search" and "find_next"

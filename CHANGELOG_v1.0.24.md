# Changelog v1.0.24

## UI/UX Improvements
- Renamed "Beállítások" button to "Manager Settings"
- Renamed "Változásnapló" button to "Patch note from Update"
- Renamed "Kapcsolódás" button to "Connect to SSH"
- Renamed "Kapcsolat Bontása" button to "Disconnect"
- Renamed "Szerver Hozzáadása" button (next to SSH connection selector) to "Add New SSH Connection"
- Renamed "Szerver Törlése" button to "Delete SSH Connection and data"

## Localization
- Updated English localization strings for all renamed buttons
- All button texts now use localization system instead of hardcoded Hungarian text

## Technical Changes
- All buttons in MainWindow now use Loaded event handlers for proper localization
- Improved code maintainability by removing hardcoded button texts

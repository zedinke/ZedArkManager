# Version 1.0.37 Release Notes

## 🐛 Bug Fixes

### Server Connection and Discovery
- **Fixed**: Server discovery now works correctly when switching between multiple SSH connections
- **Fixed**: Removed duplicate `SwitchConnectionAsync` method that was causing build errors
- **Improved**: Added better error handling and logging for server discovery process
- **Fixed**: Base path is now properly set when switching between different servers
- **Improved**: Added debug logging to help diagnose server discovery issues

### Server Loading
- **Fixed**: Servers now automatically load when switching between different SSH connections
- **Improved**: Base path validation and automatic fallback to default path (`/home/{username}/asa_server`)
- **Fixed**: Server list is now properly cleared and reloaded when switching connections

## 🔧 Technical Changes

- Enhanced `DiscoverServersAsync()` method with base path validation
- Improved `SwitchConnectionAsync()` method to properly handle connection switching
- Added debug logging throughout the server discovery process
- Fixed duplicate method definition issue in `MainViewModel.cs`

## 📝 Notes

This update fixes the issue where servers were not loading when switching between multiple SSH connections. The application now properly handles connection switching and automatically discovers servers for each connection.

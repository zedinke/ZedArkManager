# Version 1.0.35 Release Notes

## 🐛 Bug Fixes

### Docker Permission Handling
- **Fixed**: Docker permission denied errors when starting servers
- **Added**: Automatic Docker group membership check and user addition
- **Improved**: Server start command now works with sudoers NOPASSWD configuration
- **Fixed**: Server start command compatibility with sudoers setup

### Server Start Improvements
- **Added**: Container existence verification after server start
- **Improved**: Better error messages when server fails to start
- **Fixed**: Server start command now properly handles Docker permissions

### Live Logs
- **Fixed**: "No such container" error when viewing live logs
- **Added**: Container existence check before attempting to fetch logs
- **Improved**: Better error handling for non-running containers

## 🔧 Technical Changes

- Added `DockerPermissionService` for automatic Docker permission management
- Improved `ServerCardViewModel` to handle Docker permissions before server operations
- Enhanced `LiveLogsWindow` with container existence validation
- Updated version to 1.0.35

## 📝 Installation

1. Download `ZedASAManager-1.0.35.zip` from the releases page
2. Extract to your desired location
3. Run `ZedASAManager.exe`

## 🔗 Related Issues

- Fixed Docker permission denied errors
- Fixed server start command compatibility with sudoers
- Fixed live logs error for non-running containers

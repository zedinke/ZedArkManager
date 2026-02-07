# Version 1.0.36 Release Notes

## 🐛 Bug Fixes

### Update Installation Mechanism
- **Fixed**: Update installation now properly waits for application to close before copying files
- **Improved**: Added process ID-based waiting mechanism to ensure application fully closes
- **Fixed**: File copying now includes retry mechanism for better reliability
- **Improved**: Better timing for file handle release (3 seconds after process closes)
- **Fixed**: Temporary files and folders are now properly cleaned up after update
- **Improved**: Update script now verifies executable exists before starting application

## 🔧 Technical Changes

- Enhanced `UpdateService.ApplyUpdateAsync` method with improved update script
- Update script now uses process ID checking instead of fixed timeout
- Added retry mechanism for file copying operations
- Improved cleanup of temporary update files

## 📝 Notes

This update fixes the issue where the update installation would fail because files were still in use when the update script tried to copy new files. The update mechanism now properly waits for the application to fully close before attempting to copy files, ensuring a successful update installation.

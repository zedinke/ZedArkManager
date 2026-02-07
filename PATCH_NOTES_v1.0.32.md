# Patch Notes v1.0.32

## Release Date
2025-01-XX

## Summary
This release focuses on improving SSH connection stability and user experience by implementing mandatory SSH key authentication and enhancing the top navigation bar layout.

## Major Changes

### 🔐 SSH Key Management
- **Mandatory SSH Key Authentication**: All SSH operations now require SSH key authentication. Password-based SSH connections are no longer supported for improved security and stability.
- **SSH Key Generation Button**: Added a new "SSH Key" button in the top navigation bar that allows users to generate and install SSH keys directly from the application.
- **SSH Key Validation**: All manager buttons (Start, Stop, Restart, Update, Backup, Config, Live Logs, Docker Setup) now check for SSH key existence before execution. If no SSH key is found, an error message is displayed prompting the user to generate one.
- **Manager Admin & Server Admin Access**: The SSH Key button is now visible to Manager Admin and Server Admin users even when connected, allowing them to regenerate or update SSH keys as needed.

### 🔧 Connection Stability Improvements
- **Removed Connection Lost Dialogs**: Fixed an issue where "Connection Lost" dialogs were constantly appearing on the servers page. In stateless mode, temporary connection failures no longer trigger connection lost events, as each command uses its own connection.
- **Stateless SSH Operations**: All SSH operations (queries, commands, file uploads/downloads, server status checks) now use stateless connections, eliminating persistent connection issues.

### 🎨 UI/UX Improvements
- **Dynamic Top Bar Layout**: Improved the top navigation bar layout with dynamic width management:
  - Added ScrollViewer containers for better space management
  - Buttons now dynamically adjust their width based on text content
  - Fixed cutoff issues with buttons and selectors
  - Improved spacing and alignment of UI elements
- **Better Responsive Design**: The top bar now properly handles different screen sizes and content lengths, with automatic scrolling when elements don't fit.

## Technical Details

### Modified Files
- `ZedASAManager/Services/SshService.cs`: Removed `OnConnectionLost()` calls in stateless mode, enforced SSH key-only authentication
- `ZedASAManager/ViewModels/MainViewModel.cs`: Added SSH key button visibility logic for Manager/Server Admins, added SSH key validation properties
- `ZedASAManager/ViewModels/ServerCardViewModel.cs`: Added SSH key validation to all command CanExecute logic
- `ZedASAManager/MainWindow.xaml`: Improved top bar layout with ScrollViewers and dynamic width management

### Breaking Changes
- **SSH Key Required**: Users must generate an SSH key using the new "SSH Key" button before using any SSH-dependent features. Password-based SSH connections are no longer supported.

## Bug Fixes
- Fixed "Connection Lost" dialogs appearing constantly on the servers page
- Fixed top navigation bar elements being cut off or not visible
- Fixed button widths not adjusting to text content dynamically

## Migration Notes
1. **First Time Setup**: After updating, users must:
   - Select a server from the dropdown
   - Click the "SSH Key" button in the top bar
   - Enter the server password when prompted
   - The application will automatically generate and install the SSH key

2. **Existing Users**: If you already have SSH keys configured, they will continue to work. However, you may want to regenerate them using the new button for consistency.

## Known Issues
None at this time.

## Next Steps
- Continue monitoring SSH connection stability
- Consider adding SSH key rotation features
- Further UI/UX improvements based on user feedback

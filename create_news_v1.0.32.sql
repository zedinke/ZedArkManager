-- Create news item for version 1.0.32
-- Note: Replace 'MANAGER_ADMIN_USER_ID' with the actual Manager Admin user ID from the users table

INSERT INTO news (id, title, content, image_path, category, is_published, created_by, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'Version 1.0.32 Released - Docker Setup & SSH Stability Improvements',
    '<FlowDocument xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" PagePadding="0">
        <Paragraph>
            <Run FontWeight="Bold" FontSize="18">Version 1.0.32 is now available!</Run>
        </Paragraph>
        <Paragraph>
            <Run>We are excited to announce the release of version 1.0.32 with a Docker Setup & SSH Stability Improvements and enhanced notification features.</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold" FontSize="16">🎫 Docker Setup & SSH Stability Improvements</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">Three-Section Ticket Organization:</Run>
        </Paragraph>
        <Paragraph>
            <Run>• New Tickets: Tickets that haven''t been viewed by any admin yet</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Open Tickets: Tickets currently being worked on (InProgress, Open, Reopened status)</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Closed Tickets: Resolved or closed tickets</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">"Start Working" Functionality:</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Admins can click "Start Working" button on new tickets</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Automatically marks ticket as viewed and sets status to InProgress</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Sends notification to ticket creator when admin starts working on their ticket</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">Comprehensive Notification System:</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Ticket creators receive notifications when:</Run>
        </Paragraph>
        <Paragraph>
            <Run>  - Admin starts working on their ticket</Run>
        </Paragraph>
        <Paragraph>
            <Run>  - New comment is added to their ticket</Run>
        </Paragraph>
        <Paragraph>
            <Run>  - Ticket is closed</Run>
        </Paragraph>
        <Paragraph>
            <Run>  - Ticket is resolved</Run>
        </Paragraph>
        <Paragraph>
            <Run>  - Ticket is reopened</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Clicking on ticket notifications navigates directly to the ticket</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Notifications are stored in database and displayed in the notification window</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">15 Predefined Ticket Categories:</Run>
        </Paragraph>
        <Paragraph>
            <Run>General Question, Technical Problem, Feature Request, Database Issue, SSH/Connection Error, UI/UX Suggestion, Localization Error, Security Vulnerability, Performance Issue, Documentation Error, Integration Problem, Licensing/Payment, User Account, Other, Server Management</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">Ticket Priority Levels:</Run>
        </Paragraph>
        <Paragraph>
            <Run>Critical, High, Medium, Low, Info</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold" FontSize="16">🎨 Modern UI Design Updates</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Redesigned Create Ticket View with modern dark theme</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Redesigned Notification Window with improved visual feedback</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Enhanced Ticket List View with section-based organization</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold" FontSize="16">🔔 Enhanced Notification System</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Database-backed notifications for ticket-related events</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Combined display of in-app and ticket notifications</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Click-to-navigate functionality for ticket notifications</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold" FontSize="16">🐛 Bug Fixes</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Fixed image display issues in chat</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Fixed like button functionality</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Fixed ticket creation errors</Run>
        </Paragraph>
        <Paragraph>
            <Run>• Improved error handling for missing database tables</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold" FontSize="16">📋 Important: Database Migration Required</Run>
        </Paragraph>
        <Paragraph>
            <Run>After updating to version 1.0.32, you must run the database migration script:</Run>
        </Paragraph>
        <Paragraph>
            <Run FontWeight="Bold">add_ticket_notifications.sql</Run>
        </Paragraph>
        <Paragraph>
            <Run>This migration will add the viewed_by column to tickets table and create the ticket_notifications table.</Run>
        </Paragraph>
        <Paragraph>
            <Run>Download the latest version from the releases page and enjoy the improved functionality!</Run>
        </Paragraph>
    </FlowDocument>',
    NULL,
    'PatchNote',
    true,
    (SELECT id FROM users WHERE user_type = 'ManagerAdmin' LIMIT 1),
    NOW(),
    NOW()
);

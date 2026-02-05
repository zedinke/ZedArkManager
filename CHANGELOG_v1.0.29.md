# Changelog v1.0.29

## [1.0.29] - 2025-01-XX

### Added
- Automatikus jogkiosztás ServerAdmin felhasználóknak minden szerver művelethez
- ServerAdmin hozzáférés az Admin Kezeléshez

### Fixed
- ServerAdmin felhasználók most automatikusan megkapják az összes jogot a szervereik kezeléséhez
- Javított jogosultság ellenőrzések a szerver műveleteknél (Start, Stop, Restart, Update, Shutdown, Backup, Config, Live Logs, Docker Setup)
- ServerAdmin felhasználók most hozzáférhetnek az Admin Kezeléshez anélkül, hogy manuális jogkiosztásra lenne szükség

### Changed
- `ServerCardViewModel`: Minden parancs végrehajtási metódus most ellenőrzi a `UserType`-ot, és automatikusan engedélyezi a ServerAdmin és ManagerAdmin felhasználókat
- `MainViewModel`: Az `OpenAdminManagement` metódus most a `_currentUser.UserType` alapján ellenőrzi a jogosultságokat
- `LoadPermissionsAsync`: ServerAdmin felhasználók automatikusan megkapják az összes jogot

### Technical Details
- Frissített `ServerCardViewModel.ExecuteActionAsync`, `ExecuteStopAsync`, `ExecuteShutdownAsync`, `ExecuteUpdateAsync`, `ExecuteBackupAsync`, `OpenConfigWindow`, `OpenLiveLogsWindow`, `OpenDockerSetupWindowAsync` metódusok
- Frissített `MainViewModel.OpenAdminManagement` metódus
- Javított jogosultság ellenőrzési logika az adatbázis-alapú felhasználói szerepkörök támogatásához

# Release Notes v1.0.29

## 🎉 Új verzió: 1.0.29

### ✨ Főbb változások

#### 🔐 Automatikus jogkiosztás ServerAdmin felhasználóknak

A regisztrált felhasználók (akik automatikusan `ServerAdmin` szerepkört kapnak) most **automatikusan megkapják az összes jogot** a szervereik kezeléséhez. Nincs szükség manuális jogkiosztásra!

**Mit jelent ez a gyakorlatban:**
- ✅ **Minden szerver gomb használható**: Start, Stop, Restart, Update, Shutdown, Backup, Config, Live Logs, Docker Setup
- ✅ **Admin Kezelés hozzáférés**: ServerAdmin felhasználók most hozzáférhetnek az Admin Kezeléshez
- ✅ **Nincs manuális beállítás**: A jogkiosztás automatikus, amint bejelentkeznek

### 🐛 Javítások

- **Jogosultság ellenőrzések javítva**: A szerver műveletek most helyesen ellenőrzik a felhasználói szerepköröket
- **Admin Kezelés hozzáférés**: ServerAdmin felhasználók most hozzáférhetnek az Admin Kezeléshez
- **Automatikus jogkiosztás**: A regisztrált felhasználók automatikusan megkapják az összes szükséges jogot

### 📝 Technikai részletek

- Frissített jogosultság ellenőrzési logika az adatbázis-alapú felhasználói szerepkörök támogatásához
- Javított `ServerCardViewModel` parancs végrehajtási metódusok
- Frissített `MainViewModel` Admin Kezelés hozzáférési logika

### 🚀 Telepítés

1. Töltse le a legújabb verziót a GitHub Releases oldalról
2. Csomagolja ki a zip fájlt
3. Futtassa a `ZedASAManager.exe` fájlt

### 📋 Követelmények

- Windows 10 vagy újabb
- .NET 8.0 Runtime
- PostgreSQL adatbázis kapcsolat (távoli szerver)

### 🙏 Köszönet

Köszönjük, hogy használja a ZedASAManager alkalmazást! Ha bármilyen problémát tapasztal, kérjük, jelezze a GitHub Issues oldalon.

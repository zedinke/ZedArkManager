# ZedASAManager

ARK Survival Ascended Server Cluster Manager - A WPF alkalmazás ARK ASA szerverek kezeléséhez.

## Funkciók

- **Cluster Kezelés**: Hozz létre és kezelj cluster-eket
- **Szerver Kezelés**: Adj hozzá, törölj és konfigurálj szervereket
- **SSH Kapcsolat**: Biztonságos SSH kapcsolat szerverekhez
- **Élő Monitoring**: Valós idejű szerver állapot és logok megtekintése
- **Automatikus Frissítés**: GitHub-alapú verziókezelés és automatikus frissítés
- **Többnyelvű Támogatás**: Magyar és angol nyelv támogatás

## Követelmények

- .NET 8.0 SDK
- Windows operációs rendszer
- SSH hozzáférés a szerverekhez

## Telepítés

1. Klónozd a repository-t:
```bash
git clone https://github.com/YOUR_USERNAME/ZedASAManager.git
cd ZedASAManager
```

2. Build-elés:
```bash
dotnet build -c Release
```

3. Futtatás:
```bash
dotnet run --project ZedASAManager/ZedASAManager.csproj
```

Vagy közvetlenül a Release mappából:
```bash
ZedASAManager/bin/Release/net8.0-windows/ZedASAManager.exe
```

## Konfiguráció

### GitHub Repository Beállítása

Az automatikus frissítéshez be kell állítani a GitHub repository információkat az `UpdateService.cs` fájlban:

```csharp
private const string GitHubOwner = "YOUR_GITHUB_USERNAME";
private const string GitHubRepo = "ZedASAManager";
private const string AssetFileNamePattern = "ZedASAManager-{version}.zip";
```

### Release Létrehozása

1. Hozz létre egy új release-t a GitHub-on
2. A tag neve legyen `v1.0.0` formátumban (vagy `1.0.0`)
3. Csatolj egy ZIP fájlt az asset-ként, amely tartalmazza az alkalmazás fájljait
4. A ZIP fájl neve legyen: `ZedASAManager-{version}.zip` (pl. `ZedASAManager-1.0.0.zip`)

## Használat

1. Indítsd el az alkalmazást
2. Jelentkezz be vagy regisztrálj új felhasználót
3. Állítsd be az SSH kapcsolatot a szerverhez
4. Hozz létre cluster-eket és szervereket
5. Kezeld a szervereket az alkalmazás felületén keresztül

## Verziókezelés

Az alkalmazás Semantic Versioning formátumot használ (pl. 1.0.0, 1.1.0, 2.0.0).

- Minden indításkor automatikusan ellenőrzi az új verziót
- Kötelező frissítések esetén nem lehet továbbmenni a frissítés nélkül
- Manuális frissítés ellenőrzés az "Update" gombbal

## Fejlesztés

### Projekt Struktúra

```
ZedASAManager/
├── Models/          # Adatmodell osztályok
├── Services/        # Üzleti logika szolgáltatások
├── ViewModels/       # MVVM ViewModel osztályok
├── Views/            # XAML UI definíciók
├── Utilities/        # Segédeszközök
└── Localization/     # Többnyelvű stringek
```

### Build és Tesztelés

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Futtatás
dotnet run --project ZedASAManager/ZedASAManager.csproj
```

## Licenc

[Megadandó licenc]

## Közreműködés

Pull request-eket szívesen fogadunk! Nagyobb változtatásokhoz először nyiss egy issue-t, hogy megbeszéljük, mit szeretnél változtatni.

## Kapcsolat

[Megadandó kapcsolati információk]

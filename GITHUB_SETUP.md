# GitHub Repository Beállítási Útmutató

Ez az útmutató segít beállítani a GitHub repository-t és a release-eket az automatikus frissítéshez.

## 1. GitHub Repository Létrehozása

1. Lépj be a GitHub-ra: https://github.com
2. Kattints a jobb felső sarokban a "+" ikonra, majd válaszd a "New repository" opciót
3. Töltsd ki az adatokat:
   - **Repository name**: `ZedASAManager` (vagy más név, ha szeretnéd)
   - **Description**: "ARK Survival Ascended Server Cluster Manager"
   - **Visibility**: Válaszd ki, hogy Public vagy Private
   - **NE** inicializáld a README-t, .gitignore-t vagy licencet (már van)
4. Kattints a "Create repository" gombra

## 2. Helyi Repository Csatlakoztatása a GitHub-hoz

A terminálban futtasd le a következő parancsokat (cseréld ki `YOUR_USERNAME`-t a saját GitHub felhasználónevedre):

```bash
git remote add origin https://github.com/YOUR_USERNAME/ZedASAManager.git
git branch -M main
git push -u origin main
```

## 3. UpdateService Konfigurálása

Nyisd meg a `ZedASAManager/Services/UpdateService.cs` fájlt és módosítsd a következő értékeket:

```csharp
private const string GitHubOwner = "YOUR_GITHUB_USERNAME";  // Cseréld ki a GitHub felhasználónevedre
private const string GitHubRepo = "ZedASAManager";           // Cseréld ki, ha más nevet adtál a repository-nak
private const string AssetFileNamePattern = "ZedASAManager-{version}.zip";  // Ha más formátumot szeretnél, módosítsd
```

## 4. Release Létrehozása

### Előfeltételek

1. Build-eld az alkalmazást Release módban:
```bash
cd ZedASAManager
dotnet build -c Release
```

2. Készíts egy ZIP fájlt a Release mappából:
   - Navigálj a `ZedASAManager/bin/Release/net8.0-windows/` mappába
   - Válaszd ki az összes fájlt (ZedASAManager.exe, DLL-ek, Localization mappa, stb.)
   - Csomagold ZIP-be
   - Nevezd át: `ZedASAManager-1.0.0.zip` (a verziószámot a `.csproj` fájlban lévő `<Version>` érték alapján)

### Release Létrehozása GitHub-on

1. Lépj be a GitHub repository-ba
2. Kattints a "Releases" linkre (jobb oldalon)
3. Kattints a "Create a new release" gombra
4. Töltsd ki az adatokat:
   - **Choose a tag**: Hozz létre egy új tag-et (pl. `v1.0.0` vagy `1.0.0`)
   - **Release title**: Pl. "Version 1.0.0" vagy "Initial Release"
   - **Description**: Írj leírást a változtatásokról (changelog)
5. Húzd be a ZIP fájlt az "Attach binaries" részbe
6. Kattints a "Publish release" gombra

### Fontos Megjegyzések

- A tag neve lehet `v1.0.0` vagy `1.0.0` formátumban (a `v` prefix opcionális)
- A ZIP fájl neve tartalmaznia kell a verziószámot (pl. `ZedASAManager-1.0.0.zip`)
- A ZIP fájlnak tartalmaznia kell az összes szükséges fájlt az alkalmazás futtatásához

## 5. Verzió Frissítése

Amikor új verziót szeretnél kiadni:

1. Frissítsd a verziót a `ZedASAManager.csproj` fájlban:
```xml
<Version>1.1.0</Version>
```

2. Commit-old és push-old a változtatásokat:
```bash
git add ZedASAManager/ZedASAManager.csproj
git commit -m "Bump version to 1.1.0"
git push
```

3. Build-eld és csomagold az új verziót (lásd 4. lépés)
4. Hozz létre egy új release-t a GitHub-on az új verzióval

## 6. Tesztelés

1. Indítsd el az alkalmazást
2. Az alkalmazás automatikusan ellenőrzi a verziót indításkor
3. Ha van új verzió, megjelenik az UpdateWindow
4. Kattints az "Update" gombra a frissítéshez

## Hibaelhárítás

### Az alkalmazás nem találja a release-t

- Ellenőrizd, hogy a `GitHubOwner` és `GitHubRepo` értékek helyesek-e
- Ellenőrizd, hogy van-e release a GitHub-on
- Ellenőrizd, hogy a release asset fájlnév megfelel-e a `AssetFileNamePattern`-nek

### A frissítés nem működik

- Ellenőrizd, hogy a ZIP fájl tartalmazza-e az összes szükséges fájlt
- Ellenőrizd, hogy az alkalmazásnak van-e írási jogosultsága a saját mappájába
- Nézd meg a Windows Event Log-ot vagy a debug kimenetet

## További Információk

- GitHub Releases API dokumentáció: https://docs.github.com/en/rest/releases
- Semantic Versioning: https://semver.org/

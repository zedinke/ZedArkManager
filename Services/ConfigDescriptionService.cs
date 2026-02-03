using System.Collections.Generic;

namespace ZedASAManager.Services;

public class ConfigDescriptionService
{
    private readonly Dictionary<string, string> _descriptions = new();

    public ConfigDescriptionService()
    {
        InitializeDescriptions();
    }

    public string GetDescription(string key, string section = "")
    {
        // Try section-specific key first
        string fullKey = !string.IsNullOrEmpty(section) ? $"{section}.{key}" : key;
        if (_descriptions.TryGetValue(fullKey, out string? description))
        {
            return description;
        }

        // Try just the key
        if (_descriptions.TryGetValue(key, out description))
        {
            return description;
        }

        return "Nincs leírás elérhető ehhez a beállításhoz.";
    }

    private void InitializeDescriptions()
    {
        // Game.ini beállítások
        _descriptions["ServerAdminPassword"] = "A szerver adminisztrátor jelszava. Ezzel a jelszóval lehet admin jogosultságot kapni a szerveren.";
        _descriptions["ServerPassword"] = "A szerver jelszava. Ha be van állítva, csak ezzel a jelszóval lehet csatlakozni a szerverhez.";
        _descriptions["MaxPlayers"] = "A szerveren egyidejűleg játszható játékosok maximális száma.";
        _descriptions["ServerName"] = "A szerver neve, amely a szerver listában jelenik meg.";
        _descriptions["ServerDescription"] = "A szerver leírása, amely a szerver listában látható.";
        _descriptions["ServerMap"] = "A szerveren játszandó térkép neve (pl. TheIsland, ScorchedEarth, Aberration).";
        _descriptions["ServerIP"] = "A szerver IP címe. Hagyja üresen, ha automatikus IP-t szeretne használni.";
        _descriptions["QueryPort"] = "A szerver query portja, amely a szerver információk lekérdezésére szolgál.";
        _descriptions["Port"] = "A szerver fő portja, amelyen a játékosok csatlakoznak.";
        _descriptions["RCONPort"] = "A RCON (Remote Console) portja, amelyen keresztül távolról lehet kezelni a szervert.";
        _descriptions["RCONEnabled"] = "Engedélyezi vagy letiltja a RCON (Remote Console) funkciót.";
        _descriptions["RCONServerPassword"] = "A RCON jelszava, amelyet a távoli konzol eléréséhez kell használni.";
        _descriptions["ServerCrosshair"] = "Engedélyezi vagy letiltja a keresztjelet a szerveren.";
        _descriptions["ServerForceNoHud"] = "Kikapcsolja a HUD-ot (Heads-Up Display) a szerveren.";
        _descriptions["ServerThirdPerson"] = "Engedélyezi vagy letiltja a harmadik személy nézetet a szerveren.";
        _descriptions["MaxStructuresInRange"] = "Egy adott területen belül lévő struktúrák maximális száma.";
        _descriptions["StructurePickupTimeAfterPlacement"] = "Az idő másodpercben, ameddig egy struktúra felvehető az elhelyezés után.";
        _descriptions["StructurePickupHoldDuration"] = "Az idő másodpercben, ameddig tartsa a gombot a struktúra felvételéhez.";
        _descriptions["AllowThirdPersonPlayer"] = "Engedélyezi vagy letiltja a harmadik személy nézetet a játékosok számára.";
        _descriptions["AlwaysNotifyPlayerLeft"] = "Mindig értesíti a játékosokat, amikor valaki elhagyja a szervert.";
        _descriptions["AlwaysNotifyPlayerJoined"] = "Mindig értesíti a játékosokat, amikor valaki csatlakozik a szerverhez.";
        _descriptions["DontAlwaysNotifyPlayerJoined"] = "Nem mindig értesíti a játékosokat, amikor valaki csatlakozik.";
        _descriptions["ServerHardcore"] = "Engedélyezi vagy letiltja a hardcore módot (halál esetén minden tárgy elveszik).";
        _descriptions["ServerPVE"] = "Engedélyezi vagy letiltja a PvE (Player vs Environment) módot.";
        _descriptions["NoTributeDownloads"] = "Letiltja a karakterek és dinoszauruszok letöltését más szerverekről.";
        _descriptions["AllowFlyerCarryPvE"] = "Engedélyezi a repülő dinoszauruszok általi hordozást PvE módban.";
        _descriptions["PreventDownloadSurvivors"] = "Megakadályozza a túlélők letöltését más szerverekről.";
        _descriptions["PreventDownloadItems"] = "Megakadályozza a tárgyak letöltését más szerverekről.";
        _descriptions["PreventDownloadDinos"] = "Megakadályozza a dinoszauruszok letöltését más szerverekről.";
        _descriptions["PreventUploadSurvivors"] = "Megakadályozza a túlélők feltöltését más szerverekre.";
        _descriptions["PreventUploadItems"] = "Megakadályozza a tárgyak feltöltését más szerverekre.";
        _descriptions["PreventUploadDinos"] = "Megakadályozza a dinoszauruszok feltöltését más szerverekre.";
        _descriptions["ForceAllowCaveFlyers"] = "Kényszeríti a repülő dinoszauruszok használatát barlangokban.";
        _descriptions["DisableDinoDecayPvE"] = "Letiltja a dinoszauruszok elpusztulását PvE módban.";
        _descriptions["AllowCaveBuildingPvE"] = "Engedélyezi az építést barlangokban PvE módban.";
        _descriptions["EnablePvPGamma"] = "Engedélyezi a gamma beállítást PvP módban.";
        _descriptions["DisablePvEGamma"] = "Letiltja a gamma beállítást PvE módban.";
        _descriptions["PvEStructureDecayPeriodMultiplier"] = "A PvE struktúrák elpusztulási idejének szorzója.";
        _descriptions["PvEStructureDecayDestructionPeriod"] = "Az idő másodpercben, ameddig egy PvE struktúra elpusztul.";
        _descriptions["PvPDinoDecay"] = "Engedélyezi vagy letiltja a dinoszauruszok elpusztulását PvP módban.";
        _descriptions["PvPStructureDecay"] = "Engedélyezi vagy letiltja a struktúrák elpusztulását PvP módban.";
        _descriptions["AutoSavePeriodMinutes"] = "Az automatikus mentés időköze percekben.";
        _descriptions["DayCycleSpeedScale"] = "A nappal/éjszaka ciklus sebességének szorzója.";
        _descriptions["DayTimeSpeedScale"] = "A nappali idő sebességének szorzója.";
        _descriptions["NightTimeSpeedScale"] = "Az éjszakai idő sebességének szorzója.";
        _descriptions["DinoDamageMultiplier"] = "A dinoszauruszok által okozott sebzés szorzója.";
        _descriptions["PlayerDamageMultiplier"] = "A játékosok által okozott sebzés szorzója.";
        _descriptions["StructureDamageMultiplier"] = "A struktúrák által okozott sebzés szorzója.";
        _descriptions["PlayerResistanceMultiplier"] = "A játékosok ellenállásának szorzója.";
        _descriptions["DinoResistanceMultiplier"] = "A dinoszauruszok ellenállásának szorzója.";
        _descriptions["StructureResistanceMultiplier"] = "A struktúrák ellenállásának szorzója.";
        _descriptions["XPMultiplier"] = "A tapasztalati pontok (XP) szorzója.";
        _descriptions["TamingSpeedMultiplier"] = "A szelídítés sebességének szorzója.";
        _descriptions["HarvestAmountMultiplier"] = "A begyűjtött nyersanyagok mennyiségének szorzója.";
        _descriptions["HarvestHealthMultiplier"] = "A nyersanyag források életerejének szorzója.";
        _descriptions["PlayerCharacterWaterDrainMultiplier"] = "A játékos víz fogyasztásának szorzója.";
        _descriptions["PlayerCharacterFoodDrainMultiplier"] = "A játékos élelem fogyasztásának szorzója.";
        _descriptions["DinoCharacterFoodDrainMultiplier"] = "A dinoszauruszok élelem fogyasztásának szorzója.";
        _descriptions["PlayerCharacterStaminaDrainMultiplier"] = "A játékos stamina fogyasztásának szorzója.";
        _descriptions["DinoCharacterStaminaDrainMultiplier"] = "A dinoszauruszok stamina fogyasztásának szorzója.";
        _descriptions["PlayerCharacterHealthRecoveryMultiplier"] = "A játékos életerejének regenerálódásának szorzója.";
        _descriptions["DinoCharacterHealthRecoveryMultiplier"] = "A dinoszauruszok életerejének regenerálódásának szorzója.";
        _descriptions["DinoCountMultiplier"] = "A dinoszauruszok számának szorzója a világban.";
        _descriptions["DinoSpawnWeightMultiplier"] = "A dinoszauruszok megjelenési súlyának szorzója.";
        _descriptions["ResourceNoReplenishRadiusPlayers"] = "A játékosok körüli terület, ahol a nyersanyagok nem regenerálódnak.";
        _descriptions["ResourceNoReplenishRadiusStructures"] = "A struktúrák körüli terület, ahol a nyersanyagok nem regenerálódnak.";
        _descriptions["GlobalVoiceChat"] = "Engedélyezi vagy letiltja a globális hangos beszédet.";
        _descriptions["ProximityChat"] = "Engedélyezi vagy letiltja a közelségi beszédet.";
        _descriptions["NoTributeDownloads"] = "Letiltja a karakterek és dinoszauruszok letöltését más szerverekről.";
        _descriptions["AllowThirdPersonPlayer"] = "Engedélyezi vagy letiltja a harmadik személy nézetet.";
        _descriptions["ShowFloatingDamageText"] = "Megjeleníti vagy elrejti a lebegő sebzés szöveget.";
        _descriptions["EnablePlayerNames"] = "Engedélyezi vagy letiltja a játékos nevek megjelenítését.";
        _descriptions["DifficultyOffset"] = "A nehézségi szint beállítása (0.0-1.0).";
        _descriptions["OverrideOfficialDifficulty"] = "Felülírja a hivatalos nehézségi szintet.";
        _descriptions["MaxDifficulty"] = "A maximális nehézségi szint.";
        _descriptions["UseSingleplayerSettings"] = "Egyjátékos beállítások használata.";
        _descriptions["EnableExtraStructurePreventionVolumes"] = "Engedélyezi a további struktúra megelőző területeket.";
        _descriptions["PreventDiseases"] = "Megakadályozza a betegségeket a szerveren.";
        _descriptions["PreventMateBoost"] = "Megakadályozza a párzási erősítést.";
        _descriptions["PreventTribeAlliances"] = "Megakadályozza a törzsek szövetségeit.";
        _descriptions["PreventOfflinePvP"] = "Megakadályozza az offline PvP-t.";
        _descriptions["PreventOfflinePvPInterval"] = "Az offline PvP megelőzési időköz másodpercben.";
        _descriptions["PreventSpawnDinos"] = "Megakadályozza a dinoszauruszok automatikus megjelenését.";
        _descriptions["PreventResourceRadius"] = "A nyersanyagok megelőzési sugara.";
        _descriptions["PreventResourceRadiusPlayers"] = "A játékosok körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventResourceRadiusStructures"] = "A struktúrák körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventSpawnDinos"] = "Megakadályozza a dinoszauruszok automatikus megjelenését.";
        _descriptions["PreventDiseases"] = "Megakadályozza a betegségeket a szerveren.";
        _descriptions["PreventMateBoost"] = "Megakadályozza a párzási erősítést.";
        _descriptions["PreventTribeAlliances"] = "Megakadályozza a törzsek szövetségeit.";
        _descriptions["PreventOfflinePvP"] = "Megakadályozza az offline PvP-t.";
        _descriptions["PreventOfflinePvPInterval"] = "Az offline PvP megelőzési időköz másodpercben.";
        _descriptions["PreventResourceRadius"] = "A nyersanyagok megelőzési sugara.";
        _descriptions["PreventResourceRadiusPlayers"] = "A játékosok körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventResourceRadiusStructures"] = "A struktúrák körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventSpawnDinos"] = "Megakadályozza a dinoszauruszok automatikus megjelenését.";
        _descriptions["PreventDiseases"] = "Megakadályozza a betegségeket a szerveren.";
        _descriptions["PreventMateBoost"] = "Megakadályozza a párzási erősítést.";
        _descriptions["PreventTribeAlliances"] = "Megakadályozza a törzsek szövetségeit.";
        _descriptions["PreventOfflinePvP"] = "Megakadályozza az offline PvP-t.";
        _descriptions["PreventOfflinePvPInterval"] = "Az offline PvP megelőzési időköz másodpercben.";
        _descriptions["PreventResourceRadius"] = "A nyersanyagok megelőzési sugara.";
        _descriptions["PreventResourceRadiusPlayers"] = "A játékosok körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventResourceRadiusStructures"] = "A struktúrák körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventSpawnDinos"] = "Megakadályozza a dinoszauruszok automatikus megjelenését.";
        _descriptions["PreventDiseases"] = "Megakadályozza a betegségeket a szerveren.";
        _descriptions["PreventMateBoost"] = "Megakadályozza a párzási erősítést.";
        _descriptions["PreventTribeAlliances"] = "Megakadályozza a törzsek szövetségeit.";
        _descriptions["PreventOfflinePvP"] = "Megakadályozza az offline PvP-t.";
        _descriptions["PreventOfflinePvPInterval"] = "Az offline PvP megelőzési időköz másodpercben.";
        _descriptions["PreventResourceRadius"] = "A nyersanyagok megelőzési sugara.";
        _descriptions["PreventResourceRadiusPlayers"] = "A játékosok körüli nyersanyag megelőzési sugara.";
        _descriptions["PreventResourceRadiusStructures"] = "A struktúrák körüli nyersanyag megelőzési sugara.";

        // GameUserSettings.ini beállítások
        _descriptions["MasterVolume"] = "A fő hangerő szintje (0.0-1.0).";
        _descriptions["MusicVolume"] = "A zene hangerő szintje (0.0-1.0).";
        _descriptions["SFXVolume"] = "A hanghatások hangerő szintje (0.0-1.0).";
        _descriptions["VoiceVolume"] = "A hangos beszéd hangerő szintje (0.0-1.0).";
        _descriptions["CameraShakeSpeed"] = "A kamera rázás sebessége.";
        _descriptions["CameraShakeSpeedScale"] = "A kamera rázás sebességének szorzója.";
        _descriptions["FOVMultiplier"] = "A látómező (Field of View) szorzója.";
        _descriptions["GroundClutterDensity"] = "A talaj növényzet sűrűsége.";
        _descriptions["GroundClutterDistance"] = "A talaj növényzet távolsága.";
        _descriptions["MeshLODDistanceScale"] = "A mesh LOD távolságának szorzója.";
        _descriptions["TextureStreaming"] = "Engedélyezi vagy letiltja a textúra streaminget.";
        _descriptions["UseVSync"] = "Engedélyezi vagy letiltja a VSync-et.";
        _descriptions["ResolutionSizeX"] = "A képernyő felbontásának szélessége pixelben.";
        _descriptions["ResolutionSizeY"] = "A képernyő felbontásának magassága pixelben.";
        _descriptions["LastUserConfirmedResolutionSizeX"] = "Az utolsó megerősített felbontás szélessége.";
        _descriptions["LastUserConfirmedResolutionSizeY"] = "Az utolsó megerősített felbontás magassága.";
        _descriptions["WindowPosX"] = "Az ablak X pozíciója.";
        _descriptions["WindowPosY"] = "Az ablak Y pozíciója.";
        _descriptions["FullscreenMode"] = "A teljes képernyős mód típusa (0=ablak, 1=teljes képernyő, 2=ablak nélküli).";
        _descriptions["LastConfirmedFullscreenMode"] = "Az utolsó megerősített teljes képernyős mód.";
        _descriptions["PreferredFullscreenMode"] = "Az előnyben részesített teljes képernyős mód.";
        _descriptions["AudioQualityLevel"] = "A hang minőségi szintje.";
        _descriptions["FrameRateLimit"] = "A képkocka sebesség korlátja (0=nincs korlát).";
        _descriptions["DesiredScreenWidth"] = "A kívánt képernyő szélessége.";
        _descriptions["DesiredScreenHeight"] = "A kívánt képernyő magassága.";
        _descriptions["LastUserConfirmedDesiredScreenWidth"] = "Az utolsó megerősített kívánt képernyő szélessége.";
        _descriptions["LastUserConfirmedDesiredScreenHeight"] = "Az utolsó megerősített kívánt képernyő magassága.";
        _descriptions["LastRecommendedScreenWidth"] = "Az utolsó ajánlott képernyő szélessége.";
        _descriptions["LastRecommendedScreenHeight"] = "Az utolsó ajánlott képernyő magassága.";
        _descriptions["LastCPUBenchmarkResult"] = "Az utolsó CPU benchmark eredménye.";
        _descriptions["LastGPUBenchmarkResult"] = "Az utolsó GPU benchmark eredménye.";
        _descriptions["LastGPUBenchmarkMultiplier"] = "Az utolsó GPU benchmark szorzója.";
        _descriptions["bUseDesktopResolution"] = "Használja-e az asztali felbontást.";
        _descriptions["ResX"] = "A felbontás X értéke.";
        _descriptions["ResY"] = "A felbontás Y értéke.";
        _descriptions["Windowed"] = "Ablakos mód használata.";
        _descriptions["Borderless"] = "Keret nélküli ablak használata.";
        _descriptions["bUseVSync"] = "VSync használata.";
        _descriptions["bUseDynamicResolution"] = "Dinamikus felbontás használata.";
        _descriptions["ResolutionSizeX"] = "A felbontás szélessége.";
        _descriptions["ResolutionSizeY"] = "A felbontás magassága.";
        _descriptions["LastUserConfirmedResolutionSizeX"] = "Az utolsó megerősített felbontás szélessége.";
        _descriptions["LastUserConfirmedResolutionSizeY"] = "Az utolsó megerősített felbontás magassága.";
        _descriptions["WindowPosX"] = "Az ablak X pozíciója.";
        _descriptions["WindowPosY"] = "Az ablak Y pozíciója.";
        _descriptions["FullscreenMode"] = "A teljes képernyős mód típusa.";
        _descriptions["LastConfirmedFullscreenMode"] = "Az utolsó megerősített teljes képernyős mód.";
        _descriptions["PreferredFullscreenMode"] = "Az előnyben részesített teljes képernyős mód.";
        _descriptions["AudioQualityLevel"] = "A hang minőségi szintje.";
        _descriptions["FrameRateLimit"] = "A képkocka sebesség korlátja.";
        _descriptions["DesiredScreenWidth"] = "A kívánt képernyő szélessége.";
        _descriptions["DesiredScreenHeight"] = "A kívánt képernyő magassága.";
        _descriptions["LastUserConfirmedDesiredScreenWidth"] = "Az utolsó megerősített kívánt képernyő szélessége.";
        _descriptions["LastUserConfirmedDesiredScreenHeight"] = "Az utolsó megerősített kívánt képernyő magassága.";
        _descriptions["LastRecommendedScreenWidth"] = "Az utolsó ajánlott képernyő szélessége.";
        _descriptions["LastRecommendedScreenHeight"] = "Az utolsó ajánlott képernyő magassága.";
        _descriptions["LastCPUBenchmarkResult"] = "Az utolsó CPU benchmark eredménye.";
        _descriptions["LastGPUBenchmarkResult"] = "Az utolsó GPU benchmark eredménye.";
        _descriptions["LastGPUBenchmarkMultiplier"] = "Az utolsó GPU benchmark szorzója.";
        _descriptions["bUseDesktopResolution"] = "Használja-e az asztali felbontást.";
        _descriptions["ResX"] = "A felbontás X értéke.";
        _descriptions["ResY"] = "A felbontás Y értéke.";
        _descriptions["Windowed"] = "Ablakos mód használata.";
        _descriptions["Borderless"] = "Keret nélküli ablak használata.";
        _descriptions["bUseVSync"] = "VSync használata.";
        _descriptions["bUseDynamicResolution"] = "Dinamikus felbontás használata.";
    }
}

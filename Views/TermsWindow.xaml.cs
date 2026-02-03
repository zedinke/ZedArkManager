using System.Windows;

namespace ZedASAManager.Views;

public partial class TermsWindow : Window
{
    public TermsWindow()
    {
        InitializeComponent();
        LoadTerms();
    }

    private void LoadTerms()
    {
        TermsTextBlock.Text = @"
ÁLTALÁNOS SZERZŐDÉSI FELTÉTELEK (ÁSZF)

ARK Survival Ascended Cluster Manager Szoftver
Verzió: 1.0
Utolsó frissítés: 2024. január

1. ÁLTALÁNOS RENDELKEZÉSEK

1.1. Jelen Általános Szerződési Feltételek (a továbbiakban: ÁSZF) az ARK Survival Ascended Cluster Manager szoftver (a továbbiakban: Szoftver) használatára vonatkozó feltételeket határozza meg.

1.2. A Szoftver használatával a Felhasználó elfogadja és kötelezettséget vállal arra, hogy jelen ÁSZF-ben foglalt rendelkezéseket betartja.

1.3. A Szoftver tulajdonosa és üzemeltetője (a továbbiakban: Szolgáltató) fenntartja a jogot az ÁSZF módosítására. A módosításokról a Felhasználókat a Szoftveren belül értesítjük.

2. SZOLGÁLTATÁS LEÍRÁSA

2.1. A Szoftver célja, hogy segítse a Felhasználókat az ARK Survival Ascended szerverek távoli kezelésében SSH kapcsolaton keresztül.

2.2. A Szoftver funkciói:
- Távoli szerverek kezelése SSH protokollon keresztül
- Szerver állapotok monitorozása
- Szerver műveletek végrehajtása (indítás, leállítás, frissítés, újraindítás)
- Statisztikák megjelenítése
- Többszerveres (cluster) kezelés

2.3. A Szolgáltató fenntartja a jogot a Szoftver funkcióinak módosítására, bővítésére vagy korlátozására.

3. REGISZTRÁCIÓ ÉS FELHASZNÁLÓI FŐKÖNYV

3.1. A Szoftver használatához regisztráció szükséges.

3.2. A regisztráció során a Felhasználó köteles:
- Valós és pontos adatokat megadni
- Biztonságos jelszót választani (minimum 6 karakter)
- Elfogadni jelen ÁSZF-et

3.3. A Felhasználó felelős a felhasználói fiókja biztonságáért. A jelszavak titkosítva tárolódnak, de a Felhasználó köteles megfelelően védeni hozzáférési adatait.

3.4. A Felhasználó nem adhatja át felhasználói fiókját harmadik személyeknek.

4. HASZNÁLATI FELTÉTELEK

4.1. A Felhasználó köteles:
- A Szoftvert csak törvényes célokra használni
- Nem használni a Szoftvert bűncselekmények elkövetésére vagy előkészítésére
- Nem próbálkozni a Szoftver biztonsági mechanizmusainak megkerülésével
- Nem végezni reverse engineering műveleteket a Szoftveren
- Tiszteletben tartani más felhasználók jogait

4.2. TILTOTT TEVÉKENYSÉGEK:
- A Szoftver vagy annak részeinek másolása, terjesztése engedély nélkül
- A Szoftver módosítása, hackelése
- Automatizált rendszerek (botok) használata
- A Szoftver használata más szerverek jogosulatlan elérésére
- Bármilyen kártékony vagy rosszindulatú tevékenység

5. FELELŐSSÉG KORLÁTOZÁSA

5.1. A Szolgáltató nem vállal felelősséget:
- A Felhasználó által kezelt szerverek működéséért
- A Felhasználó által végrehajtott műveletek következményeiért
- Adatvesztésért, amely a Felhasználó hibájából vagy a kezelt szerverek problémájából ered
- Harmadik fél által okozott károkért

5.2. A Szoftver ""JELEN ÁLLAPOTÁBAN"" kerül nyújtásra, garanciális kötelezettség nélkül.

5.3. A Szolgáltató nem garantálja, hogy a Szoftver minden esetben hibamentesen működik, vagy hogy minden funkció elérhető lesz.

6. ADATKEZELÉS

6.1. A Szolgáltató kezeli a Felhasználó által megadott adatokat a GDPR rendelkezéseinek megfelelően.

6.2. Tárolt adatok:
- Felhasználónév
- Titkosított jelszó
- Teljes név
- E-mail cím
- Telefonszám (opcionális)
- Cégnév (opcionális)
- Szerver kapcsolati adatok (titkosítva)

6.3. A jelszavak és szerver kapcsolati adatok Windows DataProtectionScope segítségével titkosítva tárolódnak.

6.4. Az adatok lokálisan tárolódnak a Felhasználó számítógépén (AppData\Local\ZedASAManager).

7. SZELLEMI TULAJDONJOGOK

7.1. A Szoftver minden jogosultsága a Szolgáltató tulajdonában van.

7.2. A Felhasználó nem jogosult a Szoftver másolására, módosítására, terjesztésére engedély nélkül.

8. SZOLGÁLTATÁS MEGSZÜNTETÉSE

8.1. A Szolgáltató fenntartja a jogot a Felhasználó hozzáférésének megszüntetésére, ha:
- A Felhasználó megsérti jelen ÁSZF-et
- A Felhasználó jogosulatlan tevékenységet végez
- A Felhasználó adatai hamisak vagy megtévesztőek

8.2. A Felhasználó bármikor törölheti fiókját, ezzel azonban elveszíti az összes tárolt adatot.

9. VÁLTOZÁSOK ÉS ÉRTESÍTÉSEK

9.1. A Szolgáltató fenntartja a jogot az ÁSZF módosítására.

9.2. A módosításokról a Felhasználókat a Szoftveren belül értesítjük.

9.3. A módosítások elfogadása a Szoftver további használatával történik.

10. VÉGLEGES RENDELKEZÉSEK

10.1. Jelen ÁSZF a magyar jog hatálya alá tartozik.

10.2. A jelen ÁSZF-ben nem szabályozott kérdésekben a magyar jogszabályok az irányadók.

10.3. A feleket érintő viták esetén a felek kötelezettséget vállalnak a békés megoldásra törekedni. Ha ez nem lehetséges, a viták rendezésére a magyar bíróságok illetékesek.

10.4. Kapcsolatfelvétel: A Szoftveren belül elérhető támogatási lehetőségeken keresztül.

10.5. Jelen ÁSZF hatálybalépése: 2024. január 1.

Köszönjük, hogy használod az ARK Survival Ascended Cluster Manager szoftvert!

© 2024 ZedASAManager. Minden jog fenntartva.
";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}

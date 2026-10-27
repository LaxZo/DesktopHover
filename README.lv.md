<p align="center">
  <img src="assets/icon-preview.png" width="88" alt="Desktop Hover ikona">
</p>

<h1 align="center">Desktop Hover</h1>

<p align="center"><strong>Apskati tuvāk. Vienkārši uzved peli.</strong></p>
<p align="center">Apskati attēlus, dokumentus un dizaina projektus, atskaņo medijus un lasi kodu, neatverot citu programmu.</p>

<p align="center">
  <strong>Windows x64</strong> &nbsp;·&nbsp; Darbvirsma un File Explorer &nbsp;·&nbsp; Tumšā un gaišā tēma &nbsp;·&nbsp; 12 valodas
</p>

<p align="center">
  <a href="#darba-sākšana">Darba sākšana</a> &nbsp;·&nbsp;
  <a href="#ekrānattēli">Ekrānattēli</a> &nbsp;·&nbsp;
  <a href="#atbalstītie-faili">Atbalstītie faili</a> &nbsp;·&nbsp;
  <a href="README.md">English</a>
</p>

---

Desktop Hover ir Windows lietotne, kas darbojas sistēmas teknē un rāda lokāla faila priekšskatījumu, kad uz tā uzved peli darbvirsmā vai File Explorer. Apskati attēlu, noklausies audio, noskaties video vai ieskaties koda failā. Aizvirzot peli, priekšskatījums aizveras un atskaņošana apstājas.

<p align="center">
  <img src="docs/assets/image-preview.png" width="720" alt="Attēla priekšskatījums ar faila nosaukumu, izmēriem un faila lielumu">
</p>

## Iespējas

| Iespēja | Kā tā darbojas |
| --- | --- |
| **Ātri apskatīt attēlus** | Kompakts priekšskatījums ar faila nosaukumu, izmēriem un lielumu. Attēla proporcijas saglabājas. |
| **Klausīties un skatīties** | Audio sāk skanēt pēc peles uzvešanas. Video atskaņojas priekšskatījuma logā ar skaņu. Abiem ir kopīgs skaļuma iestatījums. |
| **Lasīt tekstu un kodu** | Rindu numuri, vienāda platuma fonts un sintakses krāsas izplatītām programmēšanas valodām, skriptiem un konfigurācijas failiem. |
| **Ieskatīties dokumentos un projektos** | PDF pirmās lapas, Word fragmenti, Excel lapas, PowerPoint slaidi, Photoshop apvienotie attēli un ar PDF saderīgs Illustrator saturs. Citiem Adobe projektiem izmanto iegultos priekšskatījumus, atbilstošus attēla/video priekšskatījuma failus vai Windows sīktēlus. |
| **Pielāgot sev** | Tumšā vai gaišā tēma, 12 saskarnes valodas, maināms priekšskatījuma lielums, parādīšanās aizture un palaišana kopā ar Windows. |

## Ekrānattēli

### Dokumenti un dizaina projekti

Īsti priekšskatījumi ar demonstrācijas failiem. Ja nav derīga saglabāta priekšskatījuma, Word un Excel saturu rāda vienkāršoti. PowerPoint izmanto oriģināli atveidotus vai saglabātus slaidu attēlus, lai saglabātu noformējumu. Priekšskatījuma veids ir norādīts apakšējā joslā.

<table>
  <tr><th>Photoshop</th><th>Word</th></tr>
  <tr>
    <td><img src="docs/assets/photoshop-preview.png" width="360" alt="PSD apvienotā attēla priekšskatījums"></td>
    <td><img src="docs/assets/word-preview.png" width="360" alt="DOCX fragments ar virsrakstiem un tabulu"></td>
  </tr>
  <tr><th>Excel</th><th>PowerPoint</th></tr>
  <tr>
    <td><img src="docs/assets/excel-preview.png" width="360" alt="XLSX pirmā lapa ar saglabātām vērtībām"></td>
    <td><img src="docs/assets/powerpoint-preview.png" width="360" alt="PowerPoint slaids Desktop Hover tumšajā noformējumā ar piparmētru krāsas akcentiem"></td>
  </tr>
</table>

### Kods, neatverot redaktoru

Atslēgvārdi, virknes, komentāri, skaitļi un citi elementi ir iekrāsoti. Koda krāsas pielāgojas izvēlētajai tēmai.

![C++ koda priekšskatījums tumšajā tēmā](docs/assets/code-dark.png)

<details>
<summary>Tas pats kods gaišajā tēmā</summary>

![C++ koda priekšskatījums gaišajā tēmā](docs/assets/code-light.png)

</details>

### Viens iestatījumu logs. Divas tēmas.

Izskatu pārslēdz ar saules/mēness pogu blakus valodas izvēlei. Iestatījumi saglabājas automātiski un stājas spēkā uzreiz.

<table>
  <tr><th>Tumšā tēma</th><th>Gaišā tēma</th></tr>
  <tr>
    <td><img src="docs/assets/settings-dark.png" width="360" alt="Tumšās tēmas iestatījumi: priekšskatījuma lielums, aizture, skaļums un pārslēgi"></td>
    <td><img src="docs/assets/settings-light.png" width="360" alt="Tas pats iestatījumu logs gaišajā tēmā"></td>
  </tr>
</table>

<details>
<summary><strong>Audio priekšskatījums un ātrā izvēlne</strong></summary>

| Audio | Ātrie iestatījumi |
| :---: | :---: |
| ![Audio priekšskatījums ar atskaņošanas informāciju](docs/assets/audio-preview.png) | ![Sistēmas teknes ikonas labā klikšķa izvēlne](docs/assets/tray-menu.png) |

</details>

*Ekrānattēli iegūti no pašas lietotnes ar demonstrācijas failiem. Ainava ir dokumentācijai veidota ilustrācija. Attēlos saskarne ir angliski; lietotnē var izvēlēties latviešu valodu.*

## Darba sākšana

### Palaišana bez uzstādīšanas

1. Atver repozitorija failu [DesktopHover.exe](DesktopHover.exe) un izvēlies **Download raw file**, vai lejupielādē un atpako visu repozitoriju ZIP formātā.
2. Saglabā `DesktopHover.exe`, [LICENSE](LICENSE) un [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md) mapē, kuru plāno paturēt, un palaid izpildfailu.
3. Atrodi lietotnes ikonu Windows sistēmas teknē. Tā var būt paslēpto ikonu izvēlnē.
4. Uzved peli uz atbalstīta lokāla faila darbvirsmā vai File Explorer.

**Nepieciešams:** 64 bitu Windows, darbvirsmas .NET Framework 4.x vide ar Windows Forms/WPF un attiecīgo mediju Windows kodeki. Būvēšanas skripts izmanto sistēmas Framework64 instalāciju. macOS, Linux un citi failu pārvaldnieki netiek atbalstīti.

### Uzstādīšana pašreizējam lietotājam

Lejupielādē vai klonē visu repozitoriju, tā mapē atver PowerShell un izpildi:

```powershell
powershell -ExecutionPolicy Bypass -File .\Install.ps1 -OpenSettings
```

Skripts iekopē lietotni un dokumentāciju mapē `%LOCALAPPDATA%\DesktopHover` un atver iestatījumus. **Pirmajā uzstādīšanas reizē tiek ieslēgta palaišana kopā ar Windows.** To var izslēgt iestatījumos. Atkārtota skripta palaišana atjaunina uzstādīto kopiju, saglabājot izvēlētos iestatījumus.

## Ikdienas vadība

Kamēr priekšskatījumi ir ieslēgti, DesktopHover uz laiku deaktivizē Windows failu informācijas lodziņus pašreizējā Explorer vai darbvirsmas failu skatā, lai tie atkārtoti neparādītos virs priekšskatījuma. Tas attiecas arī uz neatbalstītiem failiem šajā skatā. Aizejot no skata, iepauzējot priekšskatījumus vai aizverot lietotni, vadīklas tiek atjaunotas; neliels fona palīgprocess tās atjauno arī pēc galvenā procesa piespiedu apturēšanas. Windows iestatījumi un Explorer priekšskatījuma sānu panelis netiek mainīti. Pirmais lodziņš var īslaicīgi parādīties, pirms tā vadīkla ir noteikta.

| Darbība | Rezultāts |
| --- | --- |
| Uzved peli uz atbalstīta faila | Pēc izvēlētās aiztures parādās priekšskatījums. |
| Aizvirzi peli no faila | Priekšskatījums aizveras un atskaņošana apstājas. |
| Kreisais klikšķis uz teknes ikonas | Iestatījumi atveras peles kursora monitora centrā. |
| Labais klikšķis uz teknes ikonas | Atveras ātrie iestatījumi: priekšskatījumi, lielums, aizture, skaļums, tēma un palaišana. |
| Poga **−** | Samazina iestatījumus uz Windows uzdevumjoslu. Nospied loga pogu uzdevumjoslā, lai to atjaunotu. |
| Poga **×** | Aizver iestatījumu logu; Desktop Hover turpina darboties sistēmas teknē. |
| **Aizvērt lietotni** teknes izvēlnē | Pilnībā aizver Desktop Hover. |
| Izslēgts priekšskatījumu pārslēgs | Priekšskatījumi tiek apturēti, neaizverot lietotni. |

### Iestatījumi

| Iestatījums | Izvēles |
| --- | --- |
| Priekšskatījuma lielums | 480 × 360, 720 × 540, 1080 × 810 vai 1440 × 1080; slīdnis ļauj pielāgot maksimālo lielumu. Saturs saglabā proporcijas. |
| Parādīšanās aizture | 0, 0,25, 0,45, 0,75 vai 1,00 sekunde. Pie 0 nav papildu gaidīšanas; faila noteikšana un ielāde joprojām prasa laiku. |
| Slaidu maiņas intervāls | 1–10 sekundes PowerPoint prezentācijām. |
| Atskaņošanas skaļums | 0–100% audio un video failiem. 0% izslēdz skaņu. |
| Izskats | Tumšā vai gaišā tēma, arī izvēlnēs un koda priekšskatījumos. |
| Mazu attēlu palielināšana | Ļauj attēlu, kas mazāks par priekšskatījuma robežām, parādīt palielinātu. |
| Palaišana kopā ar Windows | Automātiska palaišana pēc pierakstīšanās. |

**Saskarnes valodas:** English · Latviešu · Русский · 简体中文 · Deutsch · Español · Français · Português (Brasil) · Italiano · 日本語 · 한국어 · Українська.

Iestatījumi glabājas `%LOCALAPPDATA%\DesktopHover\settings.ini`. Automātiskās palaišanas izvēle glabājas atsevišķi pašreizējā lietotāja Windows reģistra `Run` atslēgā.

## Atbalstītie faili

| Veids | Atpazītie formāti un piemēri |
| --- | --- |
| **Attēli** | JPG/JPEG/JFIF, PNG, BMP, GIF (arī animēti), TIFF, ICO, WebP, HEIC/HEIF, AVIF un Adobe DNG (vajadzīgs saderīgs Windows RAW dekodētājs). |
| **Audio** | MP3, WAV, WMA, M4A, AAC, FLAC, AIF/AIFF. |
| **Video** | MP4/M4V, MOV, AVI, WMV, MPG/MPEG, MKV, WebM, FLV/F4V; atskaņošanai vajadzīgs saderīgs Windows kodeks. |
| **Teksts un dati** | TXT, LOG, CSV/TSV, Markdown, JSON, YAML, INI, TOML un citi konfigurācijas faili. |
| **Pirmkods** | C/C++, C#, JavaScript/TypeScript, Python, Java, Go, Rust, Kotlin, Swift, PHP, Ruby, SQL, HTML/XML/CSS un saistītie formāti. |
| **Skripti un projekta faili** | PowerShell, čaulas un batch skripti; Dockerfile, Makefile, CMakeLists.txt, README, LICENSE un izplatīti konfigurācijas faili ar punktu nosaukuma sākumā. |
| **PDF** | Pirmā lapa, atveidota ar Windows 10/11 iebūvēto PDF atbalstu. Nav vajadzīga Adobe lietotne vai Windows sīktēlu nodrošinātājs. |
| **Photoshop** | PSD/PSB/PSDT apvienotais 8 bitu RGB/pelēktoņu attēls bez saspiešanas vai ar RLE, pielāgots priekšskatījuma izmēram. Saglabātos sīktēlus izmanto kā rezerves variantu. |
| **Word, Excel un PowerPoint** | DOCX, XLSX, PPTX un to makrosu/veidņu varianti; PPSX/PPSM prezentācijas. Word/Excel izmanto saglabāto priekšskatījumu vai vienkāršotu fragmentu/pirmo redzamo lapu. PowerPoint saglabā stilus, izmantojot pietiekami lielu saglabāto attēlu, paša PowerPoint eksportu vai labāko pieejamo saglabāto/Windows sīktēlu. |
| **Illustrator** | AI/AIT ar PDF saderīgu saturu rāda pirmo lapu izvēlētajā izmērā. EPS/EPSF/EPSI var izmantot saglabātu TIFF vai XMP priekšskatījumu, atbilstošu priekšskatījuma failu vai Windows sīktēlu. PostScript netiek izpildīts. |
| **Adobe maketu un animācijas projekti** | Tiek atpazīti INDD/INDT/INDB/IDML/INX, ICML/ICMT, XD, AEP/AEPX/AET/AETX, PRPROJ/PRTL, MOGRT/AEGRAPHIC, FLA/XFL/SWF un CHPROJ/PUPPET. **Vajadzīgs atbalstīts iegultais priekšskatījums, atbilstošs attēla/video priekšskatījuma fails vai Windows sīktēls.** SWF saturs netiek izpildīts. |
| **Citi Adobe projekti** | Captivate CP/CPT/CPTX/CPTL, Audition SESX, Dimension DN un Substance SBS/SBSAR/SPP/SSA/ALCH/SSG/SMOD izmanto atbalstītus saglabātos priekšskatījumus, atbilstošus attēla/video priekšskatījuma failus vai Windows sīktēlus. Slēgto projektu formātu ainas un montāža netiek atveidota. |
| **Citi dokumenti** | Vecajiem DOC/XLS/PPT un to veidnēm, kā arī XLSB nepieciešams Windows sīktēlu atbalsts. ODT/ODS/ODP var izmantot arī failā saglabāto PNG sīktēlu. |

Paplašinājums nosaka priekšskatījuma veidu, bet **faktiskā attēlu atvēršana un audio/video atskaņošana ir atkarīga no datorā uzstādītajiem Windows kodekiem**. Pilnie paplašinājumu saraksti ir [MediaFiles.cs](src/Preview/MediaFiles.cs), [TextPreviewContent.cs](src/Preview/TextPreviewContent.cs), [AdobeProjectFiles.cs](src/Preview/Documents/AdobeProjectFiles.cs) un [DocumentPreviewContent.cs](src/Preview/Documents/DocumentPreviewContent.cs).

Dokumenti ielādējas fonā; aizvirzot peli, ielāde tiek atcelta. Iebūvētajiem lasītājiem nav vajadzīga Office vai Adobe lietotne; lielas izšķirtspējas eksportam ar pašu PowerPoint vajadzīga instalēta PowerPoint darbvirsmas lietotne. Windows sīktēlu pieejamība ir atkarīga no datorā uzstādītā atbalsta; ja derīga priekšskatījuma nav, lietotne parāda paskaidrojumu.

Atbalstītie Photoshop apvienotie attēli un ar pašu PowerPoint eksportētie slaidi izmanto iestatīto priekšskatījuma izmēru, saglabājot proporcijas, arī bez **mazu attēlu palielināšanas**. PowerPoint tēmas, fontus, fonus un formas saglabā īsts slaida attēls; slaidus nepārzīmē no daļēja XML satura. Ja iespējams nolasīt tikai mazu saglabāto sīktēlu, tā palielināšana neatjauno trūkstošās detaļas.

Eksports ar pašu PowerPoint darbojas PPTX/POTX/PPSX failiem, ja PowerPoint ir instalēts un pašlaik nav atvērts. Tas atver pagaidu kopiju tikai lasīšanai bez redzama loga. No šīs kopijas izņem slēptos OLE metadatus, arī think-cell datus; redzamajiem OLE objektiem izmanto saglabātos attēlus. Oriģinālā prezentācija netiek mainīta. Makrosi, ActiveX un saistītie ārējie resursi joprojām netiek atbalstīti. Ja PowerPoint jau ir atvērts, izmanto saglabāto/Windows priekšskatījumu, nepieslēdzoties esošajai sesijai. Prezentācijas izmanto izvēlēto priekšskatījuma lielumu arī tad, ja pieejams tikai sīktēls. Pirmā sagatavošana var prasīt vairākas sekundes; pēdējo veiksmīgo slaidu kopumu glabā kešatmiņā līdz faila vai pieprasītā izmēra maiņai. Aizvirzot peli, logs aizveras uzreiz, bet iesāktā sagatavošana var pabeigties, lai rezultātu izmantotu nākamajā priekšskatījumā.

PDF atveidošana notiek atsevišķā palīgprocesā ar laika ierobežojumu. Aizvirzot peli no faila, priekšskatījuma ielāde tiek atcelta un palīgprocess apturēts, lai neatverams PDF nebloķētu lietotni.

### Adobe attēlu un video priekšskatījumi

Illustrator darbu var atveidot tieši, ja tas saglabāts ar **Create PDF Compatible File**. Citiem Adobe failiem lasītāji meklē skaidri nosauktus PNG/JPEG priekšskatījumus un XMP sīktēlus, arī ZIP metadatos un ar gzip saspiestos Premiere projekta metadatos ar ierobežotu apjomu. Tiek izvēlēts lielākais izmantojamais saglabātais attēls. EPS faili ar DOS bināro galveni var izmantot iegulto TIFF priekšskatījumu.

Ja projektā nav derīga iegultā priekšskatījuma, eksportē attēlu vai video tajā pašā mapē un nosaukumā pievieno `.preview`. Piemēram, `Reklama.aep` var izmantot `Reklama.preview.mp4` vai `Reklama.preview.png`. Der arī `Reklama.aep.preview.mp4`; šim pilnajam nosaukumam ir priekšroka. Šādi nosauktiem priekšskatījuma failiem ir priekšroka pār iegulto attēlu; projekta avota video netiek atskaņots gatavās kompozīcijas vietā.

Priekšskatījuma faili atbalsta MP4/MOV/WebM/M4V/AVI/WMV video un PNG/JPG/JPEG/WebP/BMP attēlus. Video ievēro izvēlēto skaļumu un apstājas, aizvirzot peli no projekta. Apakšējā joslā saglabājas projekta nosaukums un norāde **Priekšskatījuma fails**. Pēc projekta izmaiņām priekšskatījums jāeksportē no jauna. Attēlu robeža ir 64 MiB un 40 miljoni pikseļu, video — 512 MiB. Lielāks projekts joprojām var izmantot šādu priekšskatījumu, jo projekta saturs netiek parsēts.

### Pašreizējās robežas

- Priekšskatījumi darbojas ar **lokāli pieejamiem failiem**. Tīkla mapes, piesaistīti tīkla diski un tikai mākonī pieejami faili tiek izlaisti.
- Tekstam nolasa līdz **256 KiB**, **300 rindām** un **500 rakstzīmēm rindā**. Redzama tikai daļa, kas ietilpst logā; par saīsinājumu informē apakšējā josla.
- Sintakses krāsas nodrošina pamata leksiska analīze. Kods netiek izpildīts vai rediģēts.
- Markdown, HTML un SVG tiek rādīti kā avota teksts.
- Dokumentu/projektu satura nolasīšanai ir **512 MiB** faila robeža; Adobe priekšskatījuma failiem ir iepriekš norādītie atsevišķie ierobežojumi. ZIP pakotnēm ierobežots arī ierakstu, XML un iegulto attēlu apjoms; šifrēts, bojāts vai pārāk liels saturs var nebūt pieejams.
- PDF rāda **tikai pirmo lapu**, pielāgotu izvēlētajam priekšskatījuma lielumam. Lapu pāršķiršana, rediģēšana un paroles ievade nav paredzēta. Failiem, kuri prasa paroli, bojātiem PDF un neatbalstītam saturam priekšskatījums var nebūt pieejams.
- Word un Excel priekšskatījumi ir fragmenti, ne precīzi drukas maketi. Excel rāda saglabātās vērtības vai formulas tekstu; formulas un makrosi netiek izpildīti, ārējās saites netiek ielādētas. Tiek ņemts tikai pirmās redzamās lapas sākums (līdz 8 kolonnām/30 rindām). Atbalstītās PowerPoint prezentācijās slaidi mainās iestatītajā 1–10 sekunžu intervālā; animācijas un pārejas netiek atskaņotas. Ja pilns slaidu eksports nav pieejams, tiek lietots saglabātais/Windows priekšskatījums. Eksportam ar pašu PowerPoint ir 60 sekunžu, 200 slaidu un 64 MiB atveidoto PNG attēlu robeža.
- Photoshop apvienotā attēla nolasīšana ierobežota līdz **40 miljoniem pikseļu**, 8 bitu RGB/pelēktoņiem un nesaspiestam/RLE saturam. Citiem krāsu režīmiem, bitu dziļumiem un ZIP saspiešanai vajadzīgs saglabāts vai Windows sīktēls. Slāņi netiek rekonstruēti, krāsu profili netiek piemēroti. Adobe projekta atpazīšana negarantē iegulta attēla esamību. After Effects/Premiere montāžai, Animate animācijām, 3D ainām un citiem slēgto formātu projektiem vajadzīgs saglabāts priekšskatījums vai eksportēts priekšskatījuma fails; pilna projektu atveidošana nav paredzēta.
- Saglabātais priekšskatījums vai Windows sīktēls var atspoguļot agrāku saglabāšanas brīdi. Tā izšķirtspēja ir atkarīga no avota; var palīdzēt projekta saglabāšana ar ieslēgtu priekšskatījumu.
- Animēti GIF atskaņojas automātiski, kamēr pele ir uz faila, ievērojot kadru aiztures un atkārtošanās iestatījumus. Peles kustība vienas faila ikonas robežās animāciju nepārstartē; aizvirzot peli prom, atskaņošana apstājas. Citu animētu vai vairāku kadru attēlu formātiem tiek rādīts nekustīgs kadrs.
- Attēli virs **100 MiB** vai **100 miljoniem pikseļu** tiek izlaisti. GIF papildus ir **2000 kadru** un **100 miljonu pikseļu summas visu kadru taisnstūros** robeža. Ja GIF kadra aizture nav norādīta vai ir ļoti īsa (0–10 ms), izmanto 100 ms.
- Priekšskatījums ir pasīvs: tas nepārņem tastatūras fokusu un nepiedāvā redaktoru vai interaktīvas atskaņošanas pogas.

## Būvēšana no pirmkoda

Lietotne izmanto C#, Windows Forms saskarnei, WPF mediju atskaņošanai un attēlu atvēršanai, kā arī Windows Shell/UI Automation faila noteikšanai zem peles kursora. Nav NuGet pakotņu atjaunošanas soļa vai `.sln`/`.csproj` būvēšanas projekta.

Būvēšanai izmanto Windows 10/11 ar .NET Framework 4.8. PDF atbalsts atsaucas uz operētājsistēmas `System32\WinMetadata` failiem un ietvara `System.Runtime.dll` / `System.Runtime.WindowsRuntime.dll`; Windows SDK uzstādīšana nav vajadzīga.

```powershell
git clone https://github.com/LaxZo/DesktopHover.git
cd DesktopHover
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\DesktopHover.exe --settings
```

`build.ps1` palaiž [scripts/build.ps1](scripts/build.ps1), kas izmanto 64 bitu .NET Framework C# kompilatoru:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Pirms izpildfaila aizstāšanas aizver palaisto kopiju vai norādi citu izvades vietu ar `-OutputPath`.

### Repozitorija struktūra

```text
DesktopHover/
├── src/
│   ├── App/                 # Palaišana, iestatījumi un tulkojumi
│   ├── UI/                  # Iestatījumu logs, teknes izvēlne un stils
│   ├── Preview/             # Priekšskata logs, attēli, mediji un teksts
│   │   └── Documents/       # PDF/Office/Adobe lasītāji un arhīvu pārbaude
│   └── Windows/             # Explorer integrācija un dokumentu palīgprocesi
├── assets/                  # Lietotnes ikona un README logotips
├── scripts/                 # Būvēšanas un uzstādīšanas skripti
├── docs/assets/             # Ekrānattēli un to izcelsmes apraksts
├── DesktopHover.exe         # Portatīvā lietotne
├── build.ps1                # Palaiž scripts/build.ps1
├── Install.ps1              # Palaiž scripts/Install.ps1
├── README.md
└── README.lv.md
```

Programmas dzīves ciklam sāc ar `src/App/Program.cs`, priekšskatījumiem — ar `src/Preview/PreviewWindow.cs`. Būve iekļauj C# failus no `src` un iegulst `assets/DesktopHover.ico`. Saknes skripti saglabā iepriekš aprakstītās komandas; pagaidu pārbaudes faili un lokālie Graphify dati Git repozitorijā netiek iekļauti.

<details>
<summary><strong>Koda uzbūve un palaišanas parametri</strong></summary>

| Fails | Atbildība |
| --- | --- |
| [scripts/build.ps1](scripts/build.ps1), [scripts/Install.ps1](scripts/Install.ps1) | Būvēšana un uzstādīšana; saknes skripti nodod tiem parametrus. |
| [Program.cs](src/App/Program.cs) | Programmas dzīves cikls, teknes ikona, aizture un automātiskā palaišana. |
| [ShellResolver.cs](src/Windows/ShellResolver.cs) | Faila noteikšana zem peles darbvirsmā vai Explorer. |
| [ShellInfoTipSuppressor.cs](src/Windows/ShellInfoTipSuppressor.cs), [InfoTipRestoreGuard.cs](src/Windows/InfoTipRestoreGuard.cs) | Explorer failu paskaidrojumu pagaidu deaktivizēšana un atjaunošana pauzē, izejot vai pēc negaidītas apturēšanas. |
| [PreviewWindow.cs](src/Preview/PreviewWindow.cs) | Priekšskatījuma izkārtojums, zīmēšana un satura pārslēgšana. |
| [AnimatedGif.cs](src/Preview/AnimatedGif.cs) | GIF kadru aiztures, atkārtošana, ierobežota ielāde un kadru salikšana ar Windows atbalstu. |
| [MediaPlayback.cs](src/Preview/MediaPlayback.cs) | Audio un video atskaņošana. |
| [TextPreviewContent.cs](src/Preview/TextPreviewContent.cs) | Ierobežota teksta nolasīšana, kodējumi un sintakses marķieri. |
| [DocumentPreviewContent.cs](src/Preview/Documents/DocumentPreviewContent.cs), [DocumentArchiveGuard.cs](src/Preview/Documents/DocumentArchiveGuard.cs) | Dokumentu veidi, robežas un ZIP struktūras pārbaude. |
| [OfficePreviewReader.cs](src/Preview/Documents/OfficePreviewReader.cs), [AdobePreviewReader.cs](src/Preview/Documents/AdobePreviewReader.cs) | Dokumentu satura un projektu iegulto attēlu nolasīšana. |
| [PdfPreviewReader.cs](src/Preview/Documents/PdfPreviewReader.cs) | PDF pirmās lapas atveidošana ar Windows.Data.Pdf. |
| [DocumentPreviewWorker.cs](src/Windows/DocumentPreviewWorker.cs) | Kopīgs PDF/PowerPoint/sīktēlu palīgprocess ar atcelšanu, laika un izvades robežām. |
| [PowerPointPreviewReader.cs](src/Preview/Documents/PowerPointPreviewReader.cs) | Slaidu eksports ar pašu PowerPoint, stilu saglabāšana un kešatmiņa. |
| [ShellThumbnailProvider.cs](src/Windows/ShellThumbnailProvider.cs) | Windows sīktēlu iegūšana kopīgajā dokumentu palīgprocesā. |
| [SettingsWindow.cs](src/UI/SettingsWindow.cs), [TrayMenu.cs](src/UI/TrayMenu.cs) | Iestatījumi un ātrā izvēlne. |
| [Brand.cs](src/UI/Brand.cs), [Localization.cs](src/App/Localization.cs) | Tēmu krāsas, ikonas un tulkojumi. |
| [AppSettings.cs](src/App/AppSettings.cs) | Iestatījumu validācija un saglabāšana. |

```powershell
.\DesktopHover.exe --settings
.\DesktopHover.exe --enable-startup
.\DesktopHover.exe --disable-startup
```

Automātiskās palaišanas parametri reģistrē vai noņem izpildītās kopijas ceļu. Ja parasti izmanto uzstādīto kopiju, izmanto to arī šīm komandām. Atkārtota programmas palaišana atver jau strādājošās lietotnes iestatījumus.

</details>

## Ja kaut kas nedarbojas

| Problēma | Ko pārbaudīt |
| --- | --- |
| Uzvedot peli, nekas nenotiek | Pārbaudi, vai lietotne darbojas, priekšskatījumi ir ieslēgti un fails ir lokāls un atbalstīts. Izmanto Windows darbvirsmu vai File Explorer. |
| Mākoņa fails netiek rādīts | Vispirms padari failu pieejamu bezsaistē. Peles uzvešana nesāk tā lejupielādi. |
| Audio vai video neatskaņojas | Pārbaudi skaļumu un Windows kodeku atbalstu. Atpazīts paplašinājums negarantē atskaņošanu. |
| Teksts ir nogriezts | Lieli faili un garas rindas tiek apzināti saīsināti. Pilnam saturam atver failu redaktorā. |
| PDF nav priekšskatījuma | Pārbaudi, vai fails ir lokāls, nepārsniedz 512 MiB un ir atverams bez paroles. Bojāti vai neatbalstīti PDF var nebūt pieejami; Adobe uzstādīšana nav vajadzīga. |
| Adobe projektam nav priekšskatījuma | Saglabā Illustrator failu ar PDF saderību, ieslēdz priekšskatījumu saglabāšanu InDesign vai blakus projektam eksportē atbilstošu `.preview.png`/`.preview.mp4`. Skati sadaļu par Adobe attēlu un video priekšskatījumiem. |
| Vecam Office failam nav priekšskatījuma | Saglabā failu ar iegultu priekšskatījumu vai izmanto saderīgu Windows sīktēlu nodrošinātāju. Atpazīts paplašinājums negarantē satura nolasīšanu. |
| Dokuments izskatās citādi nekā redaktorā | Apakšējā joslā norādīts vienkāršots saturs. Pilnai formatēšanai un visām lapām vai slaidiem atver sākotnējo lietotni. |
| Iestatījumi ir samazināti | Atjauno logu no Windows uzdevumjoslas vai ar kreiso klikšķi uz teknes ikonas. |
| Pēc **×** nospiešanas lietotne joprojām darbojas | Šī poga aizver iestatījumus un atstāj priekšskatījumus ieslēgtus. Lai izietu pilnībā, ar labo klikšķi uz teknes ikonas izvēlies **Aizvērt lietotni**. |

Lai noņemtu uzstādīto kopiju, izslēdz **Palaist kopā ar Windows**, aizver lietotni un izdzēs `%LOCALAPPDATA%\DesktopHover`. Šīs mapes dzēšana noņem arī saglabātos iestatījumus.

## Atsauksmes un ieguldījums

[Ziņo par kļūdu vai iesaki iespēju](https://github.com/LaxZo/DesktopHover/issues). Norādi Windows versiju, faila veidu, iestatījumus un darbības, kas izraisa problēmu. Noder arī neliels parauga fails bez privātas informācijas.

Piedaloties izstrādē, apraksti mainīto uzvedību, pārbaudi būvi ar `build.ps1` un saskarnes izmaiņām pievieno ekrānattēlus.

## Licence

Desktop Hover ir pieejams ar [MIT licenci](LICENSE). Autortiesības © 2026 LaxZo.

Drīkst lietot, pārveidot un izplatīt, arī komerciāli un slēgta koda projektos, saglabājot autora un licences paziņojumus. Programmatūra tiek nodrošināta bez garantijas. Izplatot pirmkodu vai izpildfailu, pievieno `LICENSE` un [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md); instalētājs tos kopē līdzās lietotnei. GitHub pogā izmantots GitHub Octicons logo ar MIT licenci.

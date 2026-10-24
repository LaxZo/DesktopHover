<p align="center">
  <img src="icon-preview.png" width="88" alt="Desktop Hover ikona">
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
| **Ieskatīties dokumentos un projektos** | Word fragmenti, Excel lapas, PowerPoint slaidi un Photoshop attēli. Citiem Adobe projektiem izmanto saglabātos priekšskatījumus vai pieejamos Windows sīktēlus. |
| **Pielāgot sev** | Tumšā vai gaišā tēma, 12 saskarnes valodas, maināms priekšskatījuma lielums, parādīšanās aizture un palaišana kopā ar Windows. |

## Ekrānattēli

### Dokumenti un dizaina projekti

Īsti priekšskatījumi ar demonstrācijas failiem. Ja nav derīga saglabāta priekšskatījuma, Word, Excel un PowerPoint saturu rāda vienkāršoti; priekšskatījuma veids ir norādīts apakšējā joslā.

<table>
  <tr><th>Photoshop</th><th>Word</th></tr>
  <tr>
    <td><img src="docs/assets/photoshop-preview.png" width="360" alt="PSD apvienotā attēla priekšskatījums"></td>
    <td><img src="docs/assets/word-preview.png" width="360" alt="DOCX fragments ar virsrakstiem un tabulu"></td>
  </tr>
  <tr><th>Excel</th><th>PowerPoint</th></tr>
  <tr>
    <td><img src="docs/assets/excel-preview.png" width="360" alt="XLSX pirmā lapa ar saglabātām vērtībām"></td>
    <td><img src="docs/assets/powerpoint-preview.png" width="360" alt="PPTX pirmais slaids ar tekstu un attēlu"></td>
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
2. Saglabā `DesktopHover.exe` mapē, kuru plāno paturēt, un palaid to.
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
| Atskaņošanas skaļums | 0–100% audio un video failiem. 0% izslēdz skaņu. |
| Izskats | Tumšā vai gaišā tēma, arī izvēlnēs un koda priekšskatījumos. |
| Mazu attēlu palielināšana | Ļauj attēlu, kas mazāks par priekšskatījuma robežām, parādīt palielinātu. |
| Palaišana kopā ar Windows | Automātiska palaišana pēc pierakstīšanās. |

**Saskarnes valodas:** English · Latviešu · Русский · 简体中文 · Deutsch · Español · Français · Português (Brasil) · Italiano · 日本語 · 한국어 · Українська.

Iestatījumi glabājas `%LOCALAPPDATA%\DesktopHover\settings.ini`. Automātiskās palaišanas izvēle glabājas atsevišķi pašreizējā lietotāja Windows reģistra `Run` atslēgā.

## Atbalstītie faili

| Veids | Atpazītie formāti un piemēri |
| --- | --- |
| **Attēli** | JPG/JPEG/JFIF, PNG, BMP, GIF, TIFF, ICO, WebP, HEIC/HEIF, AVIF. |
| **Audio** | MP3, WAV, WMA, M4A, AAC, FLAC, AIF/AIFF. |
| **Video** | MP4/M4V, MOV, AVI, WMV, MPG/MPEG, MKV, WebM. |
| **Teksts un dati** | TXT, LOG, CSV/TSV, Markdown, JSON, YAML, INI, TOML un citi konfigurācijas faili. |
| **Pirmkods** | C/C++, C#, JavaScript/TypeScript, Python, Java, Go, Rust, Kotlin, Swift, PHP, Ruby, SQL, HTML/XML/CSS un saistītie formāti. |
| **Skripti un projekta faili** | PowerShell, čaulas un batch skripti; Dockerfile, Makefile, CMakeLists.txt, README, LICENSE un izplatīti konfigurācijas faili ar punktu nosaukuma sākumā. |
| **Photoshop** | PSD/PSB saglabātie sīktēli vai apvienotais 8 bitu RGB/pelēktoņu attēls bez saspiešanas vai ar RLE. |
| **Word, Excel un PowerPoint** | DOCX, XLSX, PPTX un to makrosu/veidņu varianti; PPSX/PPSM prezentācijas. Saglabātais priekšskatījums vai vienkāršots fragments, pirmā redzamā lapa vai pirmais slaids. |
| **Citi Adobe projekti** | AI, EPS, INDD/INDT, IDML, XD, AEP/AEPX, PRPROJ un FLA, **ja pieejams atbalstīts iegultais priekšskatījums vai Windows sīktēls**. |
| **Citi dokumenti** | Vecajiem DOC/XLS/PPT un to veidnēm, XLSB un PDF nepieciešams Windows sīktēlu atbalsts. ODT/ODS/ODP var izmantot arī failā saglabāto PNG sīktēlu. |

Paplašinājums nosaka priekšskatījuma veidu, bet **faktiskā attēlu atvēršana un audio/video atskaņošana ir atkarīga no datorā uzstādītajiem Windows kodekiem**. Pilnie paplašinājumu saraksti ir [MediaFiles.cs](MediaFiles.cs), [TextPreviewContent.cs](TextPreviewContent.cs) un [DocumentPreviewContent.cs](DocumentPreviewContent.cs).

Dokumenti ielādējas fonā; aizvirzot peli, ielāde tiek atcelta. Iebūvētajiem lasītājiem nav vajadzīga Office vai Adobe lietotne. Windows sīktēlu pieejamība ir atkarīga no datorā uzstādītā atbalsta; ja derīga priekšskatījuma nav, lietotne parāda paskaidrojumu.

### Pašreizējās robežas

- Priekšskatījumi darbojas ar **lokāli pieejamiem failiem**. Tīkla mapes, piesaistīti tīkla diski un tikai mākonī pieejami faili tiek izlaisti.
- Tekstam nolasa līdz **256 KiB**, **300 rindām** un **500 rakstzīmēm rindā**. Redzama tikai daļa, kas ietilpst logā; par saīsinājumu informē apakšējā josla.
- Sintakses krāsas nodrošina pamata leksiska analīze. Kods netiek izpildīts vai rediģēts.
- Markdown, HTML un SVG tiek rādīti kā avota teksts.
- Dokumentu/projektu priekšskatījumiem ir **512 MiB** faila robeža. Office ZIP pakotnēm ierobežots arī ierakstu, XML un iegulto attēlu apjoms; šifrēts, bojāts vai pārāk liels saturs var nebūt pieejams.
- Office priekšskatījums ir fragments, ne precīzs drukas makets. Sarežģīta formatēšana, diagrammas, slaidu šabloni, animācijas un iegulti objekti netiek pilnībā atveidoti. Excel rāda saglabātās vērtības vai formulas tekstu; formulas un makrosi netiek izpildīti, ārējās saites netiek ielādētas. Tiek ņemts tikai pirmās redzamās lapas sākums (līdz 8 kolonnām/30 rindām) un pirmais slaids.
- Photoshop apvienotā attēla nolasīšana ierobežota līdz **40 miljoniem pikseļu**, 8 bitu RGB/pelēktoņiem un nesaspiestam/RLE saturam. Citiem krāsu režīmiem, bitu dziļumiem un ZIP saspiešanai vajadzīgs saglabāts vai Windows sīktēls. Slāņi netiek rekonstruēti, krāsu profili netiek piemēroti. Citiem Adobe failiem izmanto atbalstītu XMP/ZIP priekšskatījumu vai Windows sīktēlu; pilna projektu atveidošana nav paredzēta.
- Saglabātais priekšskatījums vai Windows sīktēls var atspoguļot agrāku saglabāšanas brīdi. Tā izšķirtspēja ir atkarīga no avota; var palīdzēt projekta saglabāšana ar ieslēgtu priekšskatījumu.
- Animētiem un vairāku kadru attēliem tiek rādīts nekustīgs kadrs. Attēli virs **100 MiB** vai **100 miljoniem pikseļu** tiek izlaisti.
- Priekšskatījums ir pasīvs: tas nepārņem tastatūras fokusu un nepiedāvā redaktoru vai interaktīvas atskaņošanas pogas.

## Būvēšana no pirmkoda

Lietotne izmanto C#, Windows Forms saskarnei, WPF mediju atskaņošanai un attēlu atvēršanai, kā arī Windows Shell/UI Automation faila noteikšanai zem peles kursora. Nav NuGet pakotņu atjaunošanas soļa vai `.sln`/`.csproj` būvēšanas projekta.

```powershell
git clone https://github.com/LaxZo/DesktopHover.git
cd DesktopHover
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\DesktopHover.exe --settings
```

`build.ps1` izmanto 64 bitu .NET Framework C# kompilatoru:

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

Pirms izpildfaila aizstāšanas aizver palaisto kopiju vai norādi citu izvades vietu ar `-OutputPath`.

<details>
<summary><strong>Koda uzbūve un palaišanas parametri</strong></summary>

| Fails | Atbildība |
| --- | --- |
| [Program.cs](Program.cs) | Programmas dzīves cikls, teknes ikona, aizture un automātiskā palaišana. |
| [ShellResolver.cs](ShellResolver.cs) | Faila noteikšana zem peles darbvirsmā vai Explorer. |
| [PreviewWindow.cs](PreviewWindow.cs) | Priekšskatījuma izkārtojums, zīmēšana un satura pārslēgšana. |
| [MediaPlayback.cs](MediaPlayback.cs) | Audio un video atskaņošana. |
| [TextPreviewContent.cs](TextPreviewContent.cs) | Ierobežota teksta nolasīšana, kodējumi un sintakses marķieri. |
| [DocumentPreviewContent.cs](DocumentPreviewContent.cs), [DocumentArchiveGuard.cs](DocumentArchiveGuard.cs) | Dokumentu veidi, robežas un ZIP struktūras pārbaude. |
| [OfficePreviewReader.cs](OfficePreviewReader.cs), [AdobePreviewReader.cs](AdobePreviewReader.cs) | Dokumentu satura un projektu iegulto attēlu nolasīšana. |
| [ShellThumbnailProvider.cs](ShellThumbnailProvider.cs) | Windows sīktēli atsevišķā palīgprocesā ar laika ierobežojumu. |
| [SettingsWindow.cs](SettingsWindow.cs), [TrayMenu.cs](TrayMenu.cs) | Iestatījumi un ātrā izvēlne. |
| [Brand.cs](Brand.cs), [Localization.cs](Localization.cs) | Tēmu krāsas, ikonas un tulkojumi. |
| [AppSettings.cs](AppSettings.cs) | Iestatījumu validācija un saglabāšana. |

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
| Adobe vai vecam Office failam nav priekšskatījuma | Saglabā failu ar iegultu priekšskatījumu vai izmanto saderīgu Windows sīktēlu nodrošinātāju. Atpazīts paplašinājums negarantē satura nolasīšanu. |
| Dokuments izskatās citādi nekā redaktorā | Apakšējā joslā norādīts vienkāršots saturs. Pilnai formatēšanai un visām lapām vai slaidiem atver sākotnējo lietotni. |
| Iestatījumi ir samazināti | Atjauno logu no Windows uzdevumjoslas vai ar kreiso klikšķi uz teknes ikonas. |
| Pēc **×** nospiešanas lietotne joprojām darbojas | Šī poga aizver iestatījumus un atstāj priekšskatījumus ieslēgtus. Lai izietu pilnībā, ar labo klikšķi uz teknes ikonas izvēlies **Aizvērt lietotni**. |

Lai noņemtu uzstādīto kopiju, izslēdz **Palaist kopā ar Windows**, aizver lietotni un izdzēs `%LOCALAPPDATA%\DesktopHover`. Šīs mapes dzēšana noņem arī saglabātos iestatījumus.

## Atsauksmes un ieguldījums

[Ziņo par kļūdu vai iesaki iespēju](https://github.com/LaxZo/DesktopHover/issues). Norādi Windows versiju, faila veidu, iestatījumus un darbības, kas izraisa problēmu. Noder arī neliels parauga fails bez privātas informācijas.

Piedaloties izstrādē, apraksti mainīto uzvedību, pārbaudi būvi ar `build.ps1` un saskarnes izmaiņām pievieno ekrānattēlus.

**Licence:** šajā repozitorijā licence vēl nav norādīta.

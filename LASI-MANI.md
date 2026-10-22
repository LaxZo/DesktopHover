# Desktop Hover

Neliela lokāla Windows programma, kas rāda attēlu un video priekšskatījumus
un atskaņo audio, uzvedot peli uz faila darbvirsmā vai File Explorer.

## Lietošana

Uzved peli uz attēla, audio vai video faila un nekustini apmēram pussekundi
(vai iestatīto aiztures laiku). Attēls parādīsies pie kursora kopā ar faila
nosaukumu, pikseļu izmēriem un faila lielumu. Audio fails sāks skanēt, bet video
sāks spēlēt priekšskatījuma logā kopā ar skaņu. Video un audio skaļumu var regulēt
lietotnes iestatījumos no 0 līdz 100%; to ietekmē arī Windows skaļuma iestatījumi.

Aizvirzot peli no faila, klikšķinot, nospiežot Esc vai pauzējot priekšskatījumus,
priekšskatījums aizveras un atskaņošana tiek apturēta. Uzvedot peli atkārtoti,
atskaņošana sākas no sākuma. Faila beigās atskaņošana netiek atkārtota.

Tas ir atsevišķs lodziņš blakus kursora pozīcijai. Windows parastais teksta
infotip netiek pārveidots vai aizstāts.

Pie pulksteņa atrodi jauno ikonu: tirkīza attēla simbols ar baltu peles kursoru.
Tā var būt zem slēpto ikonu bultiņas. **Kreisais klikšķis** atver pilno iestatījumu
logu. **Labais klikšķis** atver mazu ātro iestatījumu izvēlni tajā pašā tumšajā stilā.
Tajā var ieslēgt vai pauzēt priekšskatījumu, izvēlēties priekšskatījuma lielumu, aizturi un skaļumu,
pārslēgt mazo attēlu palielināšanu un palaišanu kopā ar Windows, atvērt pilnos
iestatījumus vai aizvērt lietotni. Izmaiņas uzreiz atjaunojas arī atvērtajā
iestatījumu logā.

Pilnajā iestatījumu logā:

- Loga apakšējā labajā stūrī nospied valodas pogu ar saīsinājumu, piemēram, **ENG** vai **LAT**.
  Saraksts atveras uz augšu un rāda pilnos valodu nosaukumus. Pieejamas **English**, **Русский**,
  **Latviešu**, **简体中文** (vienkāršotā ķīniešu rakstība), **Deutsch**, **Español**,
  **Français**, **Português (Brasil)**, **Italiano**, **日本語**, **한국어** un **Українська**.
  Noklusējuma valoda ir angļu, arī atjauninot
  iepriekšējo versiju. Izvēlētā valoda stājas spēkā uzreiz un saglabājas pēc
  pārstartēšanas. Tālāk minēti izvēlnes nosaukumi latviešu valodā;
- **Priekšskatījuma lielums** — Mazs (480 × 360), Vidējs (720 × 540), Liels (1080 × 810)
  vai Ļoti liels (1440 × 1080 px). Slīdnis ļauj izvēlēties platumu no 320 līdz
  1600 px. Tie ir maksimālie izmēri: attēla vai video proporcijas saglabājas, un priekšskatījums
  tiek ietilpināts ekrānā;
- **Palielināt mazus attēlus** — ļauj rādīt attēlu lielāku par tā oriģinālo izmēru;
- **Parādīšanās aizture** — 0, 250, 450, 750 vai 1000 ms. Izvēloties 0, nav papildu
  gaidīšanas pirms faila meklēšanas un ielādes;
- **Atskaņošanas skaļums** — slīdnis no 0 līdz 100% video un audio failiem. Pie 0% skaņa
  ir izslēgta. Ātrajā izvēlnē pieejami 0%, 25%, 50%, 75% un 100%;
- **Palaist kopā ar Windows** — automātiskās palaišanas ieslēgšana/izslēgšana;
- **Priekšskatījums ieslēgts** — ieslēgšana vai īslaicīga pauze;
- **×** augšējā stūrī — programmas apturēšana.

Valoda, izmērs, mazu attēlu palielināšana, aizture un skaļums saglabājas uzreiz un paliek spēkā
pēc pārstartēšanas. Pauze ir spēkā līdz programmas aizvēršanai.
Atvērtu valodu sarakstu var aizvērt ar Esc. Samazināšanas poga **−** augšējā stūrī
vai Esc aizver tikai iestatījumu logu; priekšskatījumi turpina darboties.
Iestatījumus var atkal atvērt ar ikonu pie pulksteņa.
Krustiņš **×** pilnībā aizver programmu.
Atkārtoti palaižot programmu, atveras jau palaistās kopijas iestatījumi.

## Atbalsts un ierobežojumi

- PNG, JPG/JPEG, JFIF, BMP, GIF, TIFF un ICO.
- WebP, HEIC, HEIF un AVIF darbojas, ja datorā ir atbilstošais Windows attēlu kodeks.
- GIF un citi vairāku kadru faili tiek rādīti kā nekustīgs pirmais kadrs.
- JPEG orientācijas metadati tiek ņemti vērā.
- Attēli tiek rādīti līdz 100 MB un 100 megapikseļiem. Šie attēlu ierobežojumi
  neattiecas uz audio un video failiem.
- Audio failu paplašinājumi: MP3, WAV, WMA, M4A, AAC, FLAC, AIF un AIFF.
- Video failu paplašinājumi: MP4, M4V, MOV, AVI, WMV, MPG, MPEG, MKV un WebM.
- Audio un video atskaņošanai tiek izmantots Windows WPF `MediaPlayer` un datorā
  pieejamie kodeki. Paplašinājums vēl negarantē atskaņošanu: jāatbalsta arī faila
  konteiners un tajā izmantotie audio un video kodeki. Ja failu nevar atskaņot,
  priekšskatījumā parādās paziņojums. Programma nelejupielādē kodekus.
- Mākoņa faili, kas vēl jālejupielādē, un tīkla diski tiek izlaisti.
- Ja Windows slēpj paplašinājumus un vienā mapē vairākiem failiem ir vienāds
  redzamais nosaukums, programma tos var izlaist, lai neatvērtu nepareizu failu.
- Ļoti lielas mapes ar vairāk nekā 20 000 elementiem tiek izlaistas.
- PDF, SVG un mapju priekšskatījums nav iekļauts.

## Uzstādīšana un noņemšana

Šajā datorā programma tiek uzstādīta lietotāja mapē
`%LOCALAPPDATA%\DesktopHover`, un automātiskā palaišana norāda uz šo kopiju.
Izvades mapē esošā kopija un pirmkods ir saglabāti kā rezerves eksemplārs.
Atkārtotai uzstādīšanai paredzēts `Install.ps1`. Atjauninājums saglabā esošos
iestatījumus un automātiskās palaišanas izvēli.

Lai izslēgtu uz visiem laikiem, noņem ķeksi **Palaist kopā ar Windows** un
nospied **×**. Pēc tam `%LOCALAPPDATA%\DesktopHover` mapi var dzēst.

Programma nesūta failus internetā, neveic tīkla pieprasījumus un neievieto kodu
Explorer procesā. Tā izmanto Windows pieejamības un Shell saskarnes.
Parastā darbībā diagnostikas faili netiek rakstīti.

## Pirmkods

Pirmkods ir blakus programmai: `Program.cs`, `ShellResolver.cs`,
`PreviewWindow.cs`, `MediaFiles.cs`, `MediaPlayback.cs`, `AppSettings.cs`,
`SettingsWindow.cs`, `TrayMenu.cs`, `Localization.cs` un `Brand.cs`.
Kompilēšanai palaid `build.ps1`; tas izmanto Windows
iebūvēto .NET Framework 4 kompilatoru. Papildu lejupielādes nav nepieciešamas.

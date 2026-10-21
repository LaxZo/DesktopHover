# Desktop Hover

Neliela lokāla Windows programma, kas parāda lielu attēla priekšskatījumu,
uzvedot peli uz attēla faila darbvirsmā vai File Explorer.

## Lietošana

Uzved peli uz attēla un nekustini apmēram pussekundi. Attēls parādīsies pie
kursora kopā ar faila nosaukumu, pikseļu izmēriem un faila lielumu.
Aizvirzot peli vai klikšķinot, priekšskatījums pazūd.

Tas ir atsevišķs lodziņš blakus kursora pozīcijai. Windows parastais teksta
infotip netiek pārveidots vai aizstāts.

Pie pulksteņa atrodi jauno ikonu: tirkīza attēla simbols ar baltu peles kursoru.
Tā var būt zem slēpto ikonu bultiņas. Kreisais vai labais klikšķis atver tumšo
iestatījumu izvēlni:

- Loga augšā vari pārslēgt **English**, **Русский**, **Latviešu** vai **中文**
  (vienkāršotā ķīniešu rakstība). Noklusējuma valoda ir angļu, arī atjauninot
  iepriekšējo versiju. Izvēlētā valoda stājas spēkā uzreiz un saglabājas pēc
  pārstartēšanas. Tālāk minēti izvēlnes nosaukumi latviešu valodā;
- **Attēla lielums** — Mazs (480 × 360), Vidējs (720 × 540), Liels (1080 × 810)
  vai Ļoti liels (1440 × 1080 px). Slīdnis ļauj izvēlēties platumu no 320 līdz
  1600 px. Tie ir maksimālie izmēri: attēla proporcijas saglabājas, un priekšskatījums
  tiek ietilpināts ekrānā;
- **Palielināt mazus attēlus** — ļauj rādīt attēlu lielāku par tā oriģinālo izmēru;
- **Parādīšanās aizture** — 250, 450 vai 750 ms;
- **Palaist kopā ar Windows** — automātiskās palaišanas ieslēgšana/izslēgšana;
- **Priekšskatījums ieslēgts** — ieslēgšana vai īslaicīga pauze;
- **Aizvērt lietotni** — programmas apturēšana.

Valoda, izmērs, mazu attēlu palielināšana un aizture saglabājas uzreiz un paliek spēkā
pēc pārstartēšanas. Pauze ir spēkā līdz programmas aizvēršanai.
Krustiņš augšējā stūrī vai Esc aizver tikai iestatījumu logu; priekšskatījumi turpina
darboties. Atkārtoti palaižot programmu, atveras jau palaistās kopijas iestatījumi.

## Atbalsts un ierobežojumi

- PNG, JPG/JPEG, JFIF, BMP, GIF, TIFF un ICO.
- WebP, HEIC, HEIF un AVIF darbojas, ja datorā ir atbilstošais Windows attēlu kodeks.
- GIF un citi vairāku kadru faili tiek rādīti kā nekustīgs pirmais kadrs.
- JPEG orientācijas metadati tiek ņemti vērā.
- Tiek rādīti vietējie faili līdz 100 MB un 100 megapikseļiem.
- Mākoņa faili, kas vēl jālejupielādē, un tīkla diski tiek izlaisti.
- Ja Windows slēpj paplašinājumus un vienā mapē vairākiem failiem ir vienāds
  redzamais nosaukums, programma tos var izlaist, lai nerādītu nepareizu attēlu.
- Ļoti lielas mapes ar vairāk nekā 20 000 elementiem tiek izlaistas.
- Video, PDF, SVG un mapju priekšskatījums nav iekļauts.

## Uzstādīšana un noņemšana

Šajā datorā programma tiek uzstādīta lietotāja mapē
`%LOCALAPPDATA%\DesktopHover`, un automātiskā palaišana norāda uz šo kopiju.
Izvades mapē esošā kopija un pirmkods ir saglabāti kā rezerves eksemplārs.
Atkārtotai uzstādīšanai paredzēts `Install.ps1`. Atjauninājums saglabā esošos
iestatījumus un automātiskās palaišanas izvēli.

Lai izslēgtu uz visiem laikiem, noņem ķeksi **Palaist kopā ar Windows** un
izvēlies **Aizvērt lietotni**. Pēc tam `%LOCALAPPDATA%\DesktopHover` mapi var dzēst.

Programma nesūta failus internetā, neveic tīkla pieprasījumus un neievieto kodu
Explorer procesā. Tā izmanto Windows pieejamības un Shell saskarnes.
Parastā darbībā diagnostikas faili netiek rakstīti.

## Pirmkods

Pirmkods ir blakus programmai: `Program.cs`, `ShellResolver.cs`,
`PreviewWindow.cs`, `AppSettings.cs`, `SettingsWindow.cs`, `Localization.cs` un `Brand.cs`.
Kompilēšanai palaid `build.ps1`; tas izmanto Windows
iebūvēto .NET Framework 4 kompilatoru. Papildu lejupielādes nav nepieciešamas.

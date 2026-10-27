using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopHover
{
    internal static class L10n
    {
        internal sealed class LanguageOption
        {
            public string Code { get; private set; }
            public string NativeName { get; private set; }
            public string ShortName { get; private set; }
            internal string CultureName { get; private set; }

            internal LanguageOption(string code, string nativeName, string shortName, string cultureName)
            {
                Code = code;
                NativeName = nativeName;
                ShortName = shortName;
                CultureName = cultureName;
            }
        }

        // Translation columns follow this list. Keep existing codes stable for saved settings.
        internal static readonly LanguageOption[] Languages =
        {
            new LanguageOption("en", "English", "ENG", "en-US"),
            new LanguageOption("ru", "Русский", "RUS", "ru-RU"),
            new LanguageOption("lv", "Latviešu", "LAT", "lv-LV"),
            new LanguageOption("zh", "简体中文", "ZHO", "zh-CN"),
            new LanguageOption("de", "Deutsch", "DEU", "de-DE"),
            new LanguageOption("es", "Español", "ESP", "es-ES"),
            new LanguageOption("fr", "Français", "FRA", "fr-FR"),
            new LanguageOption("pt", "Português (Brasil)", "POR", "pt-BR"),
            new LanguageOption("it", "Italiano", "ITA", "it-IT"),
            new LanguageOption("ja", "日本語", "JPN", "ja-JP"),
            new LanguageOption("ko", "한국어", "KOR", "ko-KR"),
            new LanguageOption("uk", "Українська", "UKR", "uk-UA")
        };

        private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
        {
            { "document_preview", new[] { "Document preview", "Просмотр документа", "Dokumenta priekšskatījums", "文档预览", "Dokumentvorschau", "Vista previa del documento", "Aperçu du document", "Prévia do documento", "Anteprima documento", "ドキュメントのプレビュー", "문서 미리 보기", "Перегляд документа" } },
            { "document_loading", new[] { "Loading preview…", "Загрузка предпросмотра…", "Ielādē priekšskatījumu…", "正在加载预览…", "Vorschau wird geladen…", "Cargando vista previa…", "Chargement de l’aperçu…", "Carregando prévia…", "Caricamento anteprima…", "プレビューを読み込み中…", "미리 보기 로드 중…", "Завантаження перегляду…" } },
            { "document_pdf", new[] { "First page", "Первая страница", "Pirmā lapa", "第一页", "Erste Seite", "Primera página", "Première page", "Primeira página", "Prima pagina", "最初のページ", "첫 페이지", "Перша сторінка" } },
            { "document_pdf_unavailable", new[] { "PDF preview is unavailable. The file may be damaged, password-protected or unsupported.", "Предпросмотр PDF недоступен. Возможно, файл повреждён, защищён паролем или не поддерживается.", "PDF priekšskatījums nav pieejams. Fails var būt bojāts, aizsargāts ar paroli vai neatbalstīts.", "无法预览 PDF。文件可能已损坏、受密码保护或不受支持。", "Die PDF-Vorschau ist nicht verfügbar. Die Datei ist möglicherweise beschädigt, passwortgeschützt oder wird nicht unterstützt.", "La vista previa del PDF no está disponible. El archivo puede estar dañado, protegido con contraseña o no ser compatible.", "L’aperçu du PDF est indisponible. Le fichier est peut-être endommagé, protégé par un mot de passe ou non pris en charge.", "A prévia do PDF não está disponível. O arquivo pode estar danificado, protegido por senha ou não ser compatível.", "L’anteprima PDF non è disponibile. Il file potrebbe essere danneggiato, protetto da password o non supportato.", "PDF をプレビューできません。ファイルが破損しているか、パスワードで保護されているか、未対応の形式である可能性があります。", "PDF 미리 보기를 사용할 수 없습니다. 파일이 손상되었거나 암호로 보호되어 있거나 지원되지 않을 수 있습니다.", "Перегляд PDF недоступний. Можливо, файл пошкоджений, захищений паролем або не підтримується." } },
            { "document_embedded", new[] { "Saved preview", "Сохранённый предпросмотр", "Saglabātais priekšskatījums", "已保存的预览", "Gespeicherte Vorschau", "Vista previa guardada", "Aperçu enregistré", "Prévia salva", "Anteprima salvata", "保存されたプレビュー", "저장된 미리 보기", "Збережений перегляд" } },
            { "document_sidecar", new[] { "Preview file", "Файл предпросмотра", "Priekšskatījuma fails", "预览文件", "Vorschaudatei", "Archivo de vista previa", "Fichier d’aperçu", "Arquivo de prévia", "File di anteprima", "プレビューファイル", "미리 보기 파일", "Файл перегляду" } },
            { "document_adobe_unavailable", new[] { "No saved preview. Add a matching .preview.png or .preview.mp4 file in the same folder.", "Нет сохранённого предпросмотра. Добавьте в ту же папку файл с тем же именем и окончанием .preview.png или .preview.mp4.", "Nav saglabāta priekšskatījuma. Tajā pašā mapē pievieno failu ar atbilstošu nosaukumu un .preview.png vai .preview.mp4 beigās.", "没有保存的预览。请在同一文件夹中添加同名的 .preview.png 或 .preview.mp4 文件。", "Keine gespeicherte Vorschau. Fügen Sie im selben Ordner eine gleichnamige .preview.png- oder .preview.mp4-Datei hinzu.", "No hay vista previa guardada. Añade un archivo del mismo nombre terminado en .preview.png o .preview.mp4 en la misma carpeta.", "Aucun aperçu enregistré. Ajoutez dans le même dossier un fichier du même nom se terminant par .preview.png ou .preview.mp4.", "Sem prévia salva. Adicione na mesma pasta um arquivo com o mesmo nome terminado em .preview.png ou .preview.mp4.", "Nessuna anteprima salvata. Aggiungi nella stessa cartella un file con lo stesso nome e suffisso .preview.png o .preview.mp4.", "保存されたプレビューがありません。同じフォルダーに、同名で末尾が .preview.png または .preview.mp4 のファイルを追加してください。", "저장된 미리 보기가 없습니다. 같은 폴더에 이름이 같고 .preview.png 또는 .preview.mp4로 끝나는 파일을 추가하세요.", "Немає збереженого перегляду. Додайте до тієї самої папки файл із відповідною назвою та закінченням .preview.png або .preview.mp4." } },
            { "document_composite", new[] { "Merged image", "Сведённое изображение", "Apvienotais attēls", "合并图像", "Zusammengefügtes Bild", "Imagen combinada", "Image fusionnée", "Imagem mesclada", "Immagine unita", "統合画像", "병합된 이미지", "Зведене зображення" } },
            { "document_word", new[] { "Excerpt · simplified", "Отрывок · упрощённо", "Fragments · vienkāršots", "摘录 · 简化", "Auszug · vereinfacht", "Extracto · simplificado", "Extrait · simplifié", "Trecho · simplificado", "Estratto · semplificato", "抜粋 · 簡易表示", "발췌 · 간략 보기", "Уривок · спрощено" } },
            { "document_sheet", new[] { "First sheet · simplified", "Первый лист · упрощённо", "Pirmā lapa · vienkāršota", "首个工作表 · 简化", "Erstes Blatt · vereinfacht", "Primera hoja · simplificada", "Première feuille · simplifiée", "Primeira planilha · simplificada", "Primo foglio · semplificato", "最初のシート · 簡易表示", "첫 시트 · 간략 보기", "Перший аркуш · спрощено" } },
            { "document_powerpoint", new[] { "First slide · PowerPoint", "Первый слайд · PowerPoint", "Pirmais slaids · PowerPoint", "首张幻灯片 · PowerPoint", "Erste Folie · PowerPoint", "Primera diapositiva · PowerPoint", "Première diapositive · PowerPoint", "Primeiro slide · PowerPoint", "Prima diapositiva · PowerPoint", "最初のスライド · PowerPoint", "첫 슬라이드 · PowerPoint", "Перший слайд · PowerPoint" } },
            { "document_shell", new[] { "Windows thumbnail", "Эскиз Windows", "Windows sīktēls", "Windows 缩略图", "Windows-Miniaturansicht", "Miniatura de Windows", "Miniature Windows", "Miniatura do Windows", "Miniatura Windows", "Windows サムネイル", "Windows 썸네일", "Ескіз Windows" } },
            { "document_unavailable", new[] { "No preview available. This file needs a saved preview or a compatible Windows thumbnail provider.", "Предпросмотр недоступен. Нужен сохранённый эскиз или совместимый обработчик эскизов Windows.", "Priekšskatījums nav pieejams. Failam vajadzīgs saglabāts priekšskatījums vai atbilstošs Windows sīktēlu atbalsts.", "无法预览。此文件需要保存的预览或兼容的 Windows 缩略图提供程序。", "Keine Vorschau verfügbar. Die Datei benötigt eine gespeicherte Vorschau oder einen passenden Windows-Miniaturansichtanbieter.", "Vista previa no disponible. Se necesita una vista previa guardada o un proveedor de miniaturas de Windows compatible.", "Aperçu indisponible. Le fichier nécessite un aperçu enregistré ou un fournisseur de miniatures Windows compatible.", "Prévia indisponível. É necessária uma prévia salva ou um provedor de miniaturas compatível com o Windows.", "Anteprima non disponibile. Serve un’anteprima salvata o un provider di miniature Windows compatibile.", "プレビューを表示できません。保存済みプレビューまたは対応する Windows サムネイル機能が必要です。", "미리 보기를 사용할 수 없습니다. 저장된 미리 보기 또는 호환되는 Windows 썸네일 공급자가 필요합니다.", "Перегляд недоступний. Потрібен збережений ескіз або сумісний обробник ескізів Windows." } },
            { "document_limit", new[] { "This file exceeds the 512 MiB document preview limit.", "Файл превышает лимит предпросмотра документов: 512 МиБ.", "Fails pārsniedz dokumentu priekšskatījuma 512 MiB ierobežojumu.", "此文件超过文档预览的 512 MiB 限制。", "Diese Datei überschreitet die Vorschaugrenze von 512 MiB.", "El archivo supera el límite de vista previa de 512 MiB.", "Ce fichier dépasse la limite d’aperçu de 512 Mio.", "O arquivo excede o limite de prévia de 512 MiB.", "Il file supera il limite di anteprima di 512 MiB.", "ファイルがプレビューの上限 512 MiB を超えています。", "파일이 문서 미리 보기 제한인 512 MiB를 초과합니다.", "Файл перевищує ліміт перегляду документів: 512 МіБ." } },
            { "settings", new[] { "Settings", "Настройки", "Iestatījumi", "设置", "Einstellungen", "Ajustes", "Paramètres", "Configurações", "Impostazioni", "設定", "설정", "Налаштування" } },
            { "minimize_settings", new[] { "Minimize to taskbar", "Свернуть на панель задач", "Samazināt uz uzdevumjoslu", "最小化到任务栏", "In die Taskleiste minimieren", "Minimizar a la barra de tareas", "Réduire dans la barre des tâches", "Minimizar para a barra de tarefas", "Riduci nella barra delle applicazioni", "タスクバーに最小化", "작업 표시줄로 최소화", "Згорнути на панель завдань" } },
            { "close_settings", new[] { "Close to tray", "Скрыть в трей", "Paslēpt sistēmas teknē", "关闭到系统托盘", "In den Infobereich schließen", "Cerrar a la bandeja", "Fermer dans la zone de notification", "Fechar para a bandeja", "Chiudi nell’area di notifica", "トレイに格納", "시스템 트레이로 닫기", "Приховати в трей" } },
            { "language", new[] { "Language", "Язык", "Valoda", "语言", "Sprache", "Idioma", "Langue", "Idioma", "Lingua", "言語", "언어", "Мова" } },
            { "theme", new[] { "Theme", "Тема", "Tēma", "主题", "Design", "Tema", "Thème", "Tema", "Tema", "テーマ", "테마", "Тема" } },
            { "project_link", new[] { "Open project on GitHub", "Открыть проект на GitHub", "Atvērt projektu GitHub", "在 GitHub 上打开项目", "Projekt auf GitHub öffnen", "Abrir proyecto en GitHub", "Ouvrir le projet sur GitHub", "Abrir projeto no GitHub", "Apri il progetto su GitHub", "GitHub でプロジェクトを開く", "GitHub에서 프로젝트 열기", "Відкрити проєкт на GitHub" } },
            { "project_link_error", new[] { "Could not open the browser. Open this address manually:", "Не удалось открыть браузер. Откройте этот адрес вручную:", "Neizdevās atvērt pārlūku. Atver šo adresi manuāli:", "无法打开浏览器。请手动打开此地址：", "Der Browser konnte nicht geöffnet werden. Öffne diese Adresse manuell:", "No se pudo abrir el navegador. Abre esta dirección manualmente:", "Impossible d’ouvrir le navigateur. Ouvrez cette adresse manuellement :", "Não foi possível abrir o navegador. Abra este endereço manualmente:", "Impossibile aprire il browser. Apri questo indirizzo manualmente:", "ブラウザーを開けませんでした。このアドレスを手動で開いてください：", "브라우저를 열 수 없습니다. 이 주소를 직접 열어 주세요:", "Не вдалося відкрити браузер. Відкрийте цю адресу вручну:" } },
            { "theme_dark", new[] { "Dark", "Тёмная", "Tumšā", "深色", "Dunkel", "Oscuro", "Sombre", "Escuro", "Scuro", "ダーク", "어둡게", "Темна" } },
            { "theme_light", new[] { "Light", "Светлая", "Gaišā", "浅色", "Hell", "Claro", "Clair", "Claro", "Chiaro", "ライト", "밝게", "Світла" } },
            { "theme_switch", new[] { "Switch theme to: {0}", "Переключить тему: {0}", "Pārslēgt tēmu: {0}", "切换主题：{0}", "Design wechseln: {0}", "Cambiar tema a: {0}", "Changer de thème : {0}", "Mudar tema para: {0}", "Cambia tema: {0}", "テーマを切り替え：{0}", "테마 전환: {0}", "Змінити тему: {0}" } },
            { "small", new[] { "Small", "Маленький", "Mazs", "小", "Klein", "Pequeño", "Petite", "Pequeno", "Piccola", "小", "작게", "Малий" } },
            { "medium", new[] { "Medium", "Средний", "Vidējs", "中", "Mittel", "Mediano", "Moyenne", "Médio", "Media", "中", "보통", "Середній" } },
            { "large", new[] { "Large", "Большой", "Liels", "大", "Groß", "Grande", "Grande", "Grande", "Grande", "大", "크게", "Великий" } },
            { "extra_large", new[] { "Extra large", "Очень большой", "Ļoti liels", "超大", "Sehr groß", "Muy grande", "Très grande", "Muito grande", "Molto grande", "特大", "매우 크게", "Дуже великий" } },
            { "size_accessible", new[] { "{0}, {1} by {2} pixels", "{0}, {1} на {2} пикселей", "{0}, {1} reiz {2} pikseļi", "{0}，{1} × {2} 像素", "{0}, {1} mal {2} Pixel", "{0}, {1} por {2} píxeles", "{0}, {1} sur {2} pixels", "{0}, {1} por {2} pixels", "{0}, {1} per {2} pixel", "{0}、{1} × {2} ピクセル", "{0}, {1} × {2}픽셀", "{0}, {1} на {2} пікселів" } },
            { "width_accessible", new[] { "Maximum preview width in pixels", "Максимальная ширина просмотра в пикселях", "Priekšskatījuma maksimālais platums pikseļos", "预览最大宽度（像素）", "Maximale Vorschaubreite in Pixeln", "Ancho máximo de vista previa en píxeles", "Largeur maximale de l’aperçu en pixels", "Largura máxima da prévia em pixels", "Larghezza massima anteprima in pixel", "プレビューの最大幅（ピクセル）", "미리 보기 최대 너비(픽셀)", "Максимальна ширина перегляду в пікселях" } },
            { "instant", new[] { "Instant · 0 s", "Сразу · 0 с", "Uzreiz · 0 s", "立即 · 0 秒", "Sofort · 0 s", "Al instante · 0 s", "Immédiat · 0 s", "Imediato · 0 s", "Subito · 0 s", "即時 · 0 秒", "즉시 · 0초", "Одразу · 0 с" } },
            { "fast", new[] { "Fast · 0.25 s", "Быстро · 0,25 с", "Ātri · 0,25 s", "快速 · 0.25 秒", "Schnell · 0,25 s", "Rápido · 0,25 s", "Rapide · 0,25 s", "Rápido · 0,25 s", "Rapida · 0,25 s", "速い · 0.25 秒", "빠르게 · 0.25초", "Швидко · 0,25 с" } },
            { "natural", new[] { "Normal · 0.45 s", "Обычно · 0,45 с", "Dabiski · 0,45 s", "标准 · 0.45 秒", "Normal · 0,45 s", "Normal · 0,45 s", "Normal · 0,45 s", "Normal · 0,45 s", "Normale · 0,45 s", "標準 · 0.45 秒", "보통 · 0.45초", "Звичайно · 0,45 с" } },
            { "slow", new[] { "Slow · 0.75 s", "Долго · 0,75 с", "Lēni · 0,75 s", "缓慢 · 0.75 秒", "Langsam · 0,75 s", "Lento · 0,75 s", "Lent · 0,75 s", "Lento · 0,75 s", "Lenta · 0,75 s", "遅い · 0.75 秒", "느리게 · 0.75초", "Повільно · 0,75 с" } },
            { "one_second", new[] { "1.00 s", "1,00 с", "1,00 s", "1.00 秒", "1,00 s", "1,00 s", "1,00 s", "1,00 s", "1,00 s", "1.00 秒", "1.00초", "1,00 с" } },
            { "delay_seconds", new[] { "{0} s", "{0} с", "{0} s", "{0} 秒", "{0} s", "{0} s", "{0} s", "{0} s", "{0} s", "{0} 秒", "{0}초", "{0} с" } },
            { "delay_accessible", new[] { "Delay: {0} milliseconds", "Задержка: {0} миллисекунд", "Aizture: {0} milisekundes", "延迟：{0} 毫秒", "Verzögerung: {0} Millisekunden", "Espera: {0} milisegundos", "Délai : {0} millisecondes", "Atraso: {0} milissegundos", "Ritardo: {0} millisecondi", "待ち時間：{0} ミリ秒", "지연 시간: {0}밀리초", "Затримка: {0} мілісекунд" } },
            { "volume", new[] { "Playback volume", "Громкость воспроизведения", "Atskaņošanas skaļums", "播放音量", "Wiedergabelautstärke", "Volumen de reproducción", "Volume de lecture", "Volume de reprodução", "Volume di riproduzione", "再生音量", "재생 볼륨", "Гучність відтворення" } },
            { "volume_detail", new[] { "For video and audio files", "Для видео- и аудиофайлов", "Video un audio failiem", "视频和音频文件", "Für Video- und Audiodateien", "Para archivos de vídeo y audio", "Pour les fichiers vidéo et audio", "Para arquivos de vídeo e áudio", "Per file video e audio", "動画・音声ファイル用", "비디오 및 오디오 파일용", "Для відео- й аудіофайлів" } },
            { "volume_accessible", new[] { "Playback volume: {0}%", "Громкость воспроизведения: {0}%", "Atskaņošanas skaļums: {0}%", "播放音量：{0}%", "Wiedergabelautstärke: {0}%", "Volumen de reproducción: {0}%", "Volume de lecture : {0}%", "Volume de reprodução: {0}%", "Volume di riproduzione: {0}%", "再生音量：{0}%", "재생 볼륨: {0}%", "Гучність відтворення: {0}%" } },
            { "upscale", new[] { "Enlarge small images", "Увеличивать маленькие изображения", "Palielināt mazus attēlus", "放大小图片", "Kleine Bilder vergrößern", "Ampliar imágenes pequeñas", "Agrandir les petites images", "Ampliar imagens pequenas", "Ingrandisci immagini piccole", "小さい画像を拡大", "작은 이미지 확대", "Збільшувати малі зображення" } },
            { "upscale_detail", new[] { "Allow previews larger than the original", "Разрешить размер больше оригинала", "Atļaut lielāku izmēru par oriģinālu", "允许预览尺寸大于原图", "Vorschau größer als das Original erlauben", "Permitir un tamaño mayor que el original", "Autoriser un aperçu plus grand que l’original", "Permitir prévias maiores que o original", "Consenti anteprime più grandi dell’originale", "元の画像より大きく表示する", "원본보다 큰 미리 보기 허용", "Дозволити розмір більший за оригінал" } },
            { "startup", new[] { "Start with Windows", "Запускать вместе с Windows", "Palaist kopā ar Windows", "随 Windows 启动", "Mit Windows starten", "Iniciar con Windows", "Lancer avec Windows", "Iniciar com o Windows", "Avvia con Windows", "Windows と同時に起動", "Windows 시작 시 실행", "Запускати разом із Windows" } },
            { "startup_detail", new[] { "Ready as soon as you sign in", "Готово сразу после входа в систему", "Gatavs uzreiz pēc pierakstīšanās", "登录后即可使用", "Direkt nach der Anmeldung bereit", "Listo al iniciar sesión", "Prêt dès l’ouverture de session", "Pronto assim que você entrar", "Pronta appena accedi", "サインイン後すぐに使用可能", "로그인하면 바로 사용 가능", "Готово одразу після входу в систему" } },
            { "preview_on", new[] { "Previews enabled", "Просмотр включён", "Priekšskatījums ieslēgts", "预览已启用", "Vorschau aktiviert", "Vista previa activada", "Aperçus activés", "Prévias ativadas", "Anteprime attive", "プレビュー有効", "미리 보기 켜짐", "Перегляд увімкнено" } },
            { "preview_paused", new[] { "Previews paused", "Просмотр приостановлен", "Priekšskatījums pauzēts", "预览已暂停", "Vorschau pausiert", "Vista previa en pausa", "Aperçus en pause", "Prévias pausadas", "Anteprime in pausa", "プレビュー一時停止中", "미리 보기 일시 중지됨", "Перегляд призупинено" } },
            { "preview_detail", new[] { "Hover over a supported file", "Наведите курсор на поддерживаемый файл", "Uzved peli uz atbalstīta faila", "悬停预览支持的文件", "Maus über eine unterstützte Datei bewegen", "Pasa el cursor sobre un archivo compatible", "Survolez un fichier pris en charge", "Passe o cursor sobre um arquivo compatível", "Passa su un file supportato", "対応ファイルにマウスを合わせる", "지원되는 파일에 마우스를 올려 미리 보기", "Наведіть курсор на підтримуваний файл" } },
            { "exit", new[] { "Quit app", "Выйти", "Aizvērt lietotni", "退出应用", "Beenden", "Salir", "Quitter", "Sair", "Esci", "終了", "종료", "Вийти" } },
            { "exit_accessible", new[] { "Quit Desktop Hover", "Выйти из Desktop Hover", "Pilnībā aizvērt Desktop Hover", "退出 Desktop Hover", "Desktop Hover beenden", "Salir de Desktop Hover", "Quitter Desktop Hover", "Sair do Desktop Hover", "Esci da Desktop Hover", "Desktop Hover を終了", "Desktop Hover 종료", "Вийти з Desktop Hover" } },
            { "save_error", new[] { "Could not save changes.", "Не удалось сохранить изменения.", "Neizdevās saglabāt izmaiņas.", "无法保存更改。", "Änderungen konnten nicht gespeichert werden.", "No se pudieron guardar los cambios.", "Impossible d’enregistrer les modifications.", "Não foi possível salvar as alterações.", "Impossibile salvare le modifiche.", "変更を保存できませんでした。", "변경 사항을 저장할 수 없습니다.", "Не вдалося зберегти зміни." } },
            { "toggle", new[] { "Toggle", "Переключить", "Pārslēgt", "切换", "Umschalten", "Alternar", "Basculer", "Alternar", "Attiva/disattiva", "切り替え", "전환", "Перемкнути" } },
            { "active", new[] { "Active", "Активен", "Aktīvs", "运行中", "Aktiv", "Activo", "Actif", "Ativo", "Attiva", "有効", "활성", "Активний" } },
            { "paused", new[] { "Paused", "Пауза", "Pauzēts", "已暂停", "Pausiert", "En pausa", "En pause", "Em pausa", "In pausa", "一時停止中", "일시 중지", "Призупинено" } },
            { "surfaces", new[] { "Desktop and File Explorer", "Рабочий стол и Проводник", "Darbvirsma un File Explorer", "桌面和文件资源管理器", "Desktop und Datei-Explorer", "Escritorio y Explorador", "Bureau et Explorateur", "Área de trabalho e Explorador", "Desktop ed Esplora file", "デスクトップとエクスプローラー", "바탕 화면 및 파일 탐색기", "Робочий стіл і Провідник" } },
            { "image_size", new[] { "Preview size", "Размер предпросмотра", "Priekšskatījuma lielums", "预览大小", "Vorschaugröße", "Tamaño de vista previa", "Taille de l’aperçu", "Tamanho da prévia", "Dimensione anteprima", "プレビューサイズ", "미리 보기 크기", "Розмір перегляду" } },
            { "maximum_size", new[] { "Maximum size", "Максимальный размер", "Maksimālais izmērs", "最大尺寸", "Maximale Größe", "Tamaño máximo", "Taille maximale", "Tamanho máximo", "Dimensione massima", "最大サイズ", "최대 크기", "Максимальний розмір" } },
            { "aspect_ratio", new[] { "Original proportions are preserved.", "Исходные пропорции сохраняются.", "Oriģinālās proporcijas saglabājas.", "保持原始比例。", "Die Originalproportionen bleiben erhalten.", "Se conservan las proporciones originales.", "Les proportions d’origine sont conservées.", "As proporções originais são mantidas.", "Le proporzioni originali vengono mantenute.", "元の縦横比を維持します。", "원본 비율이 유지됩니다.", "Пропорції оригіналу зберігаються." } },
            { "delay", new[] { "Hover delay", "Задержка показа", "Parādīšanās aizture", "悬停延迟", "Anzeigeverzögerung", "Espera al pasar el cursor", "Délai au survol", "Atraso ao passar o cursor", "Ritardo al passaggio", "表示までの待ち時間", "마우스 오버 지연", "Затримка показу" } },
            { "autosaved", new[] { "Changes save automatically", "Изменения сохраняются сразу", "Izmaiņas saglabājas uzreiz", "更改会自动保存", "Wird automatisch gespeichert", "Se guarda automáticamente", "Enregistrement automatique", "Alterações salvas automaticamente", "Salvataggio automatico", "変更は自動保存されます", "변경 사항 자동 저장", "Зміни зберігаються автоматично" } },
            { "tray_active", new[] { "File previews", "Просмотр файлов", "Failu priekšskatījums", "文件预览", "Dateivorschau", "Vista previa de archivos", "Aperçus de fichiers", "Prévias de arquivos", "Anteprime dei file", "ファイルプレビュー", "파일 미리 보기", "Перегляд файлів" } },
            { "tray_paused", new[] { "Paused", "Приостановлен", "Pauzēts", "已暂停", "Pausiert", "En pausa", "En pause", "Em pausa", "In pausa", "一時停止中", "일시 중지", "Призупинено" } },
            { "preview_title", new[] { "File preview", "Просмотр файла", "Faila priekšskatījums", "文件预览", "Dateivorschau", "Vista previa de archivo", "Aperçu de fichier", "Prévia de arquivo", "Anteprima del file", "ファイルプレビュー", "파일 미리 보기", "Перегляд файлу" } },
            { "audio_preview", new[] { "Audio preview", "Прослушивание аудио", "Audio priekšskatījums", "音频预览", "Audiovorschau", "Vista previa de audio", "Aperçu audio", "Prévia de áudio", "Anteprima audio", "音声プレビュー", "오디오 미리 보기", "Прослуховування аудіо" } },
            { "video_preview", new[] { "Video preview", "Просмотр видео", "Video priekšskatījums", "视频预览", "Videovorschau", "Vista previa de vídeo", "Aperçu vidéo", "Prévia de vídeo", "Anteprima video", "動画プレビュー", "비디오 미리 보기", "Перегляд відео" } },
            { "text_preview", new[] { "Text preview", "Просмотр текста", "Teksta priekšskatījums", "文本预览", "Textvorschau", "Vista previa de texto", "Aperçu du texte", "Prévia de texto", "Anteprima del testo", "テキストプレビュー", "텍스트 미리 보기", "Перегляд тексту" } },
            { "code_preview", new[] { "Code preview", "Просмотр кода", "Koda priekšskatījums", "代码预览", "Codevorschau", "Vista previa de código", "Aperçu du code", "Prévia de código", "Anteprima del codice", "コードプレビュー", "코드 미리 보기", "Перегляд коду" } },
            { "text_empty", new[] { "Empty file", "Пустой файл", "Tukšs fails", "空文件", "Leere Datei", "Archivo vacío", "Fichier vide", "Arquivo vazio", "File vuoto", "空のファイル", "빈 파일", "Порожній файл" } },
            { "text_truncated", new[] { "Preview shortened", "Просмотр сокращён", "Priekšskatījums saīsināts", "预览已截短", "Vorschau gekürzt", "Vista previa abreviada", "Aperçu abrégé", "Prévia abreviada", "Anteprima abbreviata", "プレビューを省略しています", "미리 보기가 축약됨", "Перегляд скорочено" } },
            { "media_loading", new[] { "Loading…", "Загрузка…", "Ielādē…", "正在加载…", "Wird geladen…", "Cargando…", "Chargement…", "Carregando…", "Caricamento…", "読み込み中…", "불러오는 중…", "Завантаження…" } },
            { "media_playing", new[] { "Playing", "Воспроизведение", "Atskaņo", "正在播放", "Wiedergabe", "Reproduciendo", "Lecture en cours", "Reproduzindo", "Riproduzione", "再生中", "재생 중", "Відтворення" } },
            { "media_ended", new[] { "Playback finished", "Воспроизведение завершено", "Atskaņošana pabeigta", "播放结束", "Wiedergabe beendet", "Reproducción finalizada", "Lecture terminée", "Reprodução concluída", "Riproduzione terminata", "再生が終了しました", "재생 완료", "Відтворення завершено" } },
            { "media_unavailable", new[] { "Cannot play this file. It may need a Windows codec.", "Не удалось воспроизвести файл. Возможно, нужен кодек Windows.", "Failu nevar atskaņot. Iespējams, vajadzīgs Windows kodeks.", "无法播放此文件。可能需要 Windows 编解码器。", "Datei nicht abspielbar. Eventuell fehlt ein Windows-Codec.", "No se puede reproducir. Puede faltar un códec de Windows.", "Lecture impossible. Un codec Windows peut être nécessaire.", "Não foi possível reproduzir. Pode ser necessário um codec do Windows.", "Impossibile riprodurre il file. Potrebbe servire un codec Windows.", "このファイルを再生できません。Windows コーデックが必要な場合があります。", "파일을 재생할 수 없습니다. Windows 코덱이 필요할 수 있습니다.", "Не вдалося відтворити файл. Можливо, потрібен кодек Windows." } }
        };

        internal static string Normalize(string language)
        {
            return Languages[LanguageIndex(language)].Code;
        }

        internal static int LanguageIndex(string language)
        {
            string code = (language ?? "").Trim();
            for (int i = 0; i < Languages.Length; i++)
                if (String.Equals(Languages[i].Code, code, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        internal static CultureInfo Culture(string language)
        {
            return CultureInfo.GetCultureInfo(Languages[LanguageIndex(language)].CultureName);
        }

        internal static string Text(string language, string key)
        {
            string[] translations;
            if (!Strings.TryGetValue(key, out translations)) return key;
            int index = LanguageIndex(language);
            return index < translations.Length && !String.IsNullOrEmpty(translations[index])
                ? translations[index] : translations[0];
        }
    }
}

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
            { "settings", new[] { "Settings", "Настройки", "Iestatījumi", "设置", "Einstellungen", "Ajustes", "Paramètres", "Configurações", "Impostazioni", "設定", "설정", "Налаштування" } },
            { "minimize_settings", new[] { "Minimize to tray", "Свернуть в трей", "Samazināt uz sistēmas tekni", "最小化到系统托盘", "In den Infobereich minimieren", "Minimizar a la bandeja", "Réduire dans la zone de notification", "Minimizar para a bandeja", "Riduci nell’area di notifica", "トレイに最小化", "시스템 트레이로 최소화", "Згорнути в трей" } },
            { "language", new[] { "Language", "Язык", "Valoda", "语言", "Sprache", "Idioma", "Langue", "Idioma", "Lingua", "言語", "언어", "Мова" } },
            { "small", new[] { "Small", "Маленький", "Mazs", "小", "Klein", "Pequeño", "Petite", "Pequeno", "Piccola", "小", "작게", "Малий" } },
            { "medium", new[] { "Medium", "Средний", "Vidējs", "中", "Mittel", "Mediano", "Moyenne", "Médio", "Media", "中", "보통", "Середній" } },
            { "large", new[] { "Large", "Большой", "Liels", "大", "Groß", "Grande", "Grande", "Grande", "Grande", "大", "크게", "Великий" } },
            { "extra_large", new[] { "Extra large", "Очень большой", "Ļoti liels", "超大", "Sehr groß", "Muy grande", "Très grande", "Muito grande", "Molto grande", "特大", "매우 크게", "Дуже великий" } },
            { "size_accessible", new[] { "{0}, {1} by {2} pixels", "{0}, {1} на {2} пикселей", "{0}, {1} reiz {2} pikseļi", "{0}，{1} × {2} 像素", "{0}, {1} mal {2} Pixel", "{0}, {1} por {2} píxeles", "{0}, {1} sur {2} pixels", "{0}, {1} por {2} pixels", "{0}, {1} per {2} pixel", "{0}、{1} × {2} ピクセル", "{0}, {1} × {2}픽셀", "{0}, {1} на {2} пікселів" } },
            { "width_accessible", new[] { "Maximum preview width in pixels", "Максимальная ширина просмотра в пикселях", "Priekšskatījuma maksimālais platums pikseļos", "预览最大宽度（像素）", "Maximale Vorschaubreite in Pixeln", "Ancho máximo de vista previa en píxeles", "Largeur maximale de l’aperçu en pixels", "Largura máxima da prévia em pixels", "Larghezza massima anteprima in pixel", "プレビューの最大幅（ピクセル）", "미리 보기 최대 너비(픽셀)", "Максимальна ширина перегляду в пікселях" } },
            { "fast", new[] { "Fast · 0.25 s", "Быстро · 0,25 с", "Ātri · 0,25 s", "快速 · 0.25 秒", "Schnell · 0,25 s", "Rápido · 0,25 s", "Rapide · 0,25 s", "Rápido · 0,25 s", "Rapida · 0,25 s", "速い · 0.25 秒", "빠르게 · 0.25초", "Швидко · 0,25 с" } },
            { "natural", new[] { "Normal · 0.45 s", "Обычно · 0,45 с", "Dabiski · 0,45 s", "标准 · 0.45 秒", "Normal · 0,45 s", "Normal · 0,45 s", "Normal · 0,45 s", "Normal · 0,45 s", "Normale · 0,45 s", "標準 · 0.45 秒", "보통 · 0.45초", "Звичайно · 0,45 с" } },
            { "slow", new[] { "Slow · 0.75 s", "Долго · 0,75 с", "Lēni · 0,75 s", "缓慢 · 0.75 秒", "Langsam · 0,75 s", "Lento · 0,75 s", "Lent · 0,75 s", "Lento · 0,75 s", "Lenta · 0,75 s", "遅い · 0.75 秒", "느리게 · 0.75초", "Повільно · 0,75 с" } },
            { "delay_accessible", new[] { "Delay: {0} milliseconds", "Задержка: {0} миллисекунд", "Aizture: {0} milisekundes", "延迟：{0} 毫秒", "Verzögerung: {0} Millisekunden", "Espera: {0} milisegundos", "Délai : {0} millisecondes", "Atraso: {0} milissegundos", "Ritardo: {0} millisecondi", "待ち時間：{0} ミリ秒", "지연 시간: {0}밀리초", "Затримка: {0} мілісекунд" } },
            { "upscale", new[] { "Enlarge small images", "Увеличивать маленькие изображения", "Palielināt mazus attēlus", "放大小图片", "Kleine Bilder vergrößern", "Ampliar imágenes pequeñas", "Agrandir les petites images", "Ampliar imagens pequenas", "Ingrandisci immagini piccole", "小さい画像を拡大", "작은 이미지 확대", "Збільшувати малі зображення" } },
            { "upscale_detail", new[] { "Allow previews larger than the original", "Разрешить размер больше оригинала", "Atļaut lielāku izmēru par oriģinālu", "允许预览尺寸大于原图", "Vorschau größer als das Original erlauben", "Permitir un tamaño mayor que el original", "Autoriser un aperçu plus grand que l’original", "Permitir prévias maiores que o original", "Consenti anteprime più grandi dell’originale", "元の画像より大きく表示する", "원본보다 큰 미리 보기 허용", "Дозволити розмір більший за оригінал" } },
            { "startup", new[] { "Start with Windows", "Запускать вместе с Windows", "Palaist kopā ar Windows", "随 Windows 启动", "Mit Windows starten", "Iniciar con Windows", "Lancer avec Windows", "Iniciar com o Windows", "Avvia con Windows", "Windows と同時に起動", "Windows 시작 시 실행", "Запускати разом із Windows" } },
            { "startup_detail", new[] { "Ready as soon as you sign in", "Готово сразу после входа в систему", "Gatavs uzreiz pēc pierakstīšanās", "登录后即可使用", "Direkt nach der Anmeldung bereit", "Listo al iniciar sesión", "Prêt dès l’ouverture de session", "Pronto assim que você entrar", "Pronta appena accedi", "サインイン後すぐに使用可能", "로그인하면 바로 사용 가능", "Готово одразу після входу в систему" } },
            { "preview_on", new[] { "Image previews enabled", "Просмотр включён", "Priekšskatījums ieslēgts", "图片预览已启用", "Bildvorschau aktiviert", "Vista previa activada", "Aperçu des images activé", "Prévias de imagens ativadas", "Anteprima immagini attiva", "画像プレビュー有効", "이미지 미리 보기 켜짐", "Перегляд зображень увімкнено" } },
            { "preview_paused", new[] { "Image previews paused", "Просмотр приостановлен", "Priekšskatījums pauzēts", "图片预览已暂停", "Bildvorschau pausiert", "Vista previa en pausa", "Aperçu des images en pause", "Prévias de imagens pausadas", "Anteprima immagini in pausa", "画像プレビュー一時停止中", "이미지 미리 보기 일시 중지됨", "Перегляд зображень призупинено" } },
            { "preview_detail", new[] { "Hover over an image to preview it", "Наведите курсор на изображение", "Attēls parādās, uzvedot peles kursoru", "将鼠标悬停在图片上即可预览", "Maus über ein Bild bewegen", "Pasa el cursor sobre una imagen", "Survolez une image pour l’afficher", "Passe o cursor sobre uma imagem", "Passa il puntatore su un’immagine", "画像にマウスを合わせると表示", "이미지 위에 마우스를 올려 미리 보기", "Наведіть курсор на зображення" } },
            { "exit", new[] { "Quit app", "Выйти", "Aizvērt lietotni", "退出应用", "Beenden", "Salir", "Quitter", "Sair", "Esci", "終了", "종료", "Вийти" } },
            { "exit_accessible", new[] { "Quit Desktop Hover", "Выйти из Desktop Hover", "Pilnībā aizvērt Desktop Hover", "退出 Desktop Hover", "Desktop Hover beenden", "Salir de Desktop Hover", "Quitter Desktop Hover", "Sair do Desktop Hover", "Esci da Desktop Hover", "Desktop Hover を終了", "Desktop Hover 종료", "Вийти з Desktop Hover" } },
            { "save_error", new[] { "Could not save changes.", "Не удалось сохранить изменения.", "Neizdevās saglabāt izmaiņas.", "无法保存更改。", "Änderungen konnten nicht gespeichert werden.", "No se pudieron guardar los cambios.", "Impossible d’enregistrer les modifications.", "Não foi possível salvar as alterações.", "Impossibile salvare le modifiche.", "変更を保存できませんでした。", "변경 사항을 저장할 수 없습니다.", "Не вдалося зберегти зміни." } },
            { "toggle", new[] { "Toggle", "Переключить", "Pārslēgt", "切换", "Umschalten", "Alternar", "Basculer", "Alternar", "Attiva/disattiva", "切り替え", "전환", "Перемкнути" } },
            { "active", new[] { "Active", "Активен", "Aktīvs", "运行中", "Aktiv", "Activo", "Actif", "Ativo", "Attiva", "有効", "활성", "Активний" } },
            { "paused", new[] { "Paused", "Пауза", "Pauzēts", "已暂停", "Pausiert", "En pausa", "En pause", "Em pausa", "In pausa", "一時停止中", "일시 중지", "Призупинено" } },
            { "surfaces", new[] { "Desktop and File Explorer", "Рабочий стол и Проводник", "Darbvirsma un File Explorer", "桌面和文件资源管理器", "Desktop und Datei-Explorer", "Escritorio y Explorador", "Bureau et Explorateur", "Área de trabalho e Explorador", "Desktop ed Esplora file", "デスクトップとエクスプローラー", "바탕 화면 및 파일 탐색기", "Робочий стіл і Провідник" } },
            { "image_size", new[] { "Image size", "Размер изображения", "Attēla lielums", "图片大小", "Bildgröße", "Tamaño de imagen", "Taille de l’image", "Tamanho da imagem", "Dimensione immagine", "画像サイズ", "이미지 크기", "Розмір зображення" } },
            { "maximum_size", new[] { "Maximum size", "Максимальный размер", "Maksimālais izmērs", "最大尺寸", "Maximale Größe", "Tamaño máximo", "Taille maximale", "Tamanho máximo", "Dimensione massima", "最大サイズ", "최대 크기", "Максимальний розмір" } },
            { "aspect_ratio", new[] { "Original proportions are preserved.", "Пропорции изображения сохраняются.", "Attēla proporcijas saglabājas.", "保持图片原始比例。", "Die Originalproportionen bleiben erhalten.", "Se conservan las proporciones originales.", "Les proportions d’origine sont conservées.", "As proporções originais são mantidas.", "Le proporzioni originali vengono mantenute.", "元の画像の縦横比を維持します。", "원본 비율이 유지됩니다.", "Пропорції оригіналу зберігаються." } },
            { "delay", new[] { "Hover delay", "Задержка показа", "Parādīšanās aizture", "悬停延迟", "Anzeigeverzögerung", "Espera al pasar el cursor", "Délai au survol", "Atraso ao passar o cursor", "Ritardo al passaggio", "表示までの待ち時間", "마우스 오버 지연", "Затримка показу" } },
            { "autosaved", new[] { "Changes save automatically", "Изменения сохраняются сразу", "Izmaiņas saglabājas uzreiz", "更改会自动保存", "Wird automatisch gespeichert", "Se guarda automáticamente", "Enregistrement automatique", "Alterações salvas automaticamente", "Salvataggio automatico", "変更は自動保存されます", "변경 사항 자동 저장", "Зміни зберігаються автоматично" } },
            { "tray_active", new[] { "Image previews", "Просмотр изображений", "Attēlu priekšskatījums", "图片预览", "Bildvorschau", "Vista previa de imágenes", "Aperçu des images", "Prévias de imagens", "Anteprima immagini", "画像プレビュー", "이미지 미리 보기", "Перегляд зображень" } },
            { "tray_paused", new[] { "Paused", "Приостановлен", "Pauzēts", "已暂停", "Pausiert", "En pausa", "En pause", "Em pausa", "In pausa", "一時停止中", "일시 중지", "Призупинено" } },
            { "preview_title", new[] { "Image preview", "Просмотр изображения", "Attēla priekšskatījums", "图片预览", "Bildvorschau", "Vista previa de imagen", "Aperçu de l’image", "Prévia de imagem", "Anteprima immagine", "画像プレビュー", "이미지 미리 보기", "Перегляд зображення" } }
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

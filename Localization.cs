using System;
using System.Collections.Generic;
using System.Globalization;

namespace DesktopHover
{
    internal static class L10n
    {
        // Each row contains English, Russian, Latvian and Simplified Chinese.
        private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
        {
            { "settings", new[] { "Settings", "Настройки", "Iestatījumi", "设置" } },
            { "close_settings", new[] { "Close settings", "Закрыть настройки", "Aizvērt iestatījumus", "关闭设置" } },
            { "language", new[] { "Language", "Язык", "Valoda", "语言" } },
            { "small", new[] { "Small", "Маленький", "Mazs", "小" } },
            { "medium", new[] { "Medium", "Средний", "Vidējs", "中" } },
            { "large", new[] { "Large", "Большой", "Liels", "大" } },
            { "extra_large", new[] { "Extra large", "Очень большой", "Ļoti liels", "超大" } },
            { "size_accessible", new[] { "{0}, {1} by {2} pixels", "{0}, {1} на {2} пикселей", "{0}, {1} reiz {2} pikseļi", "{0}，{1} × {2} 像素" } },
            { "width_accessible", new[] { "Maximum preview width in pixels", "Максимальная ширина просмотра в пикселях", "Priekšskatījuma maksimālais platums pikseļos", "预览最大宽度（像素）" } },
            { "fast", new[] { "Fast · 0.25 s", "Быстро · 0,25 с", "Ātri · 0,25 s", "快速 · 0.25 秒" } },
            { "natural", new[] { "Normal · 0.45 s", "Обычно · 0,45 с", "Dabiski · 0,45 s", "标准 · 0.45 秒" } },
            { "slow", new[] { "Slow · 0.75 s", "Долго · 0,75 с", "Lēni · 0,75 s", "缓慢 · 0.75 秒" } },
            { "delay_accessible", new[] { "Delay: {0} milliseconds", "Задержка: {0} миллисекунд", "Aizture: {0} milisekundes", "延迟：{0} 毫秒" } },
            { "upscale", new[] { "Enlarge small images", "Увеличивать маленькие изображения", "Palielināt mazus attēlus", "放大小图片" } },
            { "upscale_detail", new[] { "Allow previews larger than the original", "Разрешить размер больше оригинала", "Atļaut lielāku izmēru par oriģinālu", "允许预览尺寸大于原图" } },
            { "startup", new[] { "Start with Windows", "Запускать вместе с Windows", "Palaist kopā ar Windows", "随 Windows 启动" } },
            { "startup_detail", new[] { "Ready as soon as you sign in", "Готово сразу после входа в систему", "Gatavs uzreiz pēc pierakstīšanās", "登录后即可使用" } },
            { "preview_on", new[] { "Image previews enabled", "Просмотр включён", "Priekšskatījums ieslēgts", "图片预览已启用" } },
            { "preview_paused", new[] { "Image previews paused", "Просмотр приостановлен", "Priekšskatījums pauzēts", "图片预览已暂停" } },
            { "preview_detail", new[] { "Hover over an image to preview it", "Наведите курсор на изображение", "Attēls parādās, uzvedot peles kursoru", "将鼠标悬停在图片上即可预览" } },
            { "exit", new[] { "Quit app", "Выйти", "Aizvērt lietotni", "退出应用" } },
            { "exit_accessible", new[] { "Quit Desktop Hover", "Выйти из Desktop Hover", "Pilnībā aizvērt Desktop Hover", "退出 Desktop Hover" } },
            { "save_error", new[] { "Could not save changes.", "Не удалось сохранить изменения.", "Neizdevās saglabāt izmaiņas.", "无法保存更改。" } },
            { "toggle", new[] { "Toggle", "Переключить", "Pārslēgt", "切换" } },
            { "active", new[] { "Active", "Активен", "Aktīvs", "运行中" } },
            { "paused", new[] { "Paused", "Пауза", "Pauzēts", "已暂停" } },
            { "surfaces", new[] { "Desktop and File Explorer", "Рабочий стол и Проводник", "Darbvirsma un File Explorer", "桌面和文件资源管理器" } },
            { "image_size", new[] { "Image size", "Размер изображения", "Attēla lielums", "图片大小" } },
            { "maximum_size", new[] { "Maximum size", "Максимальный размер", "Maksimālais izmērs", "最大尺寸" } },
            { "aspect_ratio", new[] { "Original proportions are preserved.", "Пропорции изображения сохраняются.", "Attēla proporcijas saglabājas.", "保持图片原始比例。" } },
            { "delay", new[] { "Hover delay", "Задержка показа", "Parādīšanās aizture", "悬停延迟" } },
            { "autosaved", new[] { "Changes save automatically", "Изменения сохраняются сразу", "Izmaiņas saglabājas uzreiz", "更改会自动保存" } },
            { "tray_active", new[] { "Image previews", "Просмотр изображений", "Attēlu priekšskatījums", "图片预览" } },
            { "tray_paused", new[] { "Paused", "Приостановлен", "Pauzēts", "已暂停" } },
            { "preview_title", new[] { "Image preview", "Просмотр изображения", "Attēla priekšskatījums", "图片预览" } }
        };

        internal static string Normalize(string language)
        {
            string code = (language ?? "").Trim().ToLowerInvariant();
            return code == "ru" || code == "lv" || code == "zh" ? code : "en";
        }

        internal static CultureInfo Culture(string language)
        {
            switch (Normalize(language))
            {
                case "ru": return CultureInfo.GetCultureInfo("ru-RU");
                case "lv": return CultureInfo.GetCultureInfo("lv-LV");
                case "zh": return CultureInfo.GetCultureInfo("zh-CN");
                default: return CultureInfo.GetCultureInfo("en-US");
            }
        }

        internal static string Text(string language, string key)
        {
            string[] translations;
            if (!Strings.TryGetValue(key, out translations)) return key;
            int index;
            switch (Normalize(language))
            {
                case "ru": index = 1; break;
                case "lv": index = 2; break;
                case "zh": index = 3; break;
                default: index = 0; break;
            }
            return translations[index];
        }
    }
}

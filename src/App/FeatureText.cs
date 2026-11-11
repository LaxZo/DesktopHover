using System;
using System.Collections.Generic;

namespace DesktopHover
{
    internal static class FeatureText
    {
        private static readonly Dictionary<string, string[]> strings = new Dictionary<string, string[]>
        {
            { "pinned_preview", new[] { "Pinned preview", "Piespraustais priekšskatījums" } },
            { "maximize_preview", new[] { "Maximize preview", "Maksimizēt priekšskatījumu" } },
            { "close_preview", new[] { "Close preview", "Aizvērt priekšskatījumu" } },
            { "zoom_level", new[] { "Image scale · double-click the image to fit", "Attēla mērogs · dubultklikšķis ievieto attēlu logā" } },
            { "pin", new[] { "Pin last preview", "Piespraust pēdējo priekšskatījumu" } },
            { "previous", new[] { "Previous page / slide", "Iepriekšējā lapa / slaids" } },
            { "next", new[] { "Next page / slide", "Nākamā lapa / slaids" } },
            { "play_pause", new[] { "Play / pause (Space)", "Atskaņot / pauze (atstarpe)" } },
            { "play", new[] { "Play", "Atskaņot" } },
            { "pause", new[] { "Pause", "Pauze" } },
            { "zoom_in", new[] { "Zoom in (+ or Ctrl+wheel)", "Palielināt (+ vai Ctrl+ritenis)" } },
            { "zoom_out", new[] { "Zoom out (− or Ctrl+wheel)", "Samazināt (− vai Ctrl+ritenis)" } },
            { "fit", new[] { "Fit", "Ietilpt" } },
            { "media_paused", new[] { "Paused · pin the preview to play", "Pauze · piespraud priekšskatījumu, lai atskaņotu" } },
            { "autoplay", new[] { "Autoplay audio and video", "Automātiski atskaņot medijus" } },
            { "loop_video", new[] { "Loop videos", "Atkārtot video" } },
            { "maximize_settings", new[] { "Maximize settings", "Maksimizēt iestatījumus" } },
            { "restore_settings", new[] { "Restore window size", "Atjaunot loga izmēru" } },
            { "loop_video_detail", new[] { "Restart videos automatically after playback", "Pēc beigām sākt video no jauna" } },
            { "autoplay_detail", new[] { "Start playback when hovering", "Sākt audio un video, uzvedot peli" } },
            { "file_types", new[] { "Enabled file types", "Ieslēgtie failu tipi" } },
            { "type_images", new[] { "Images and GIFs", "Attēli un GIF" } },
            { "type_documents", new[] { "Documents and design projects", "Dokumenti un dizaina projekti" } },
            { "type_text", new[] { "Text and code", "Teksts un kods" } },
            { "type_audio", new[] { "Audio", "Audio" } },
            { "type_video", new[] { "Video", "Video" } },
            { "shortcut_toggle", new[] { "{0} — enable / pause previews", "{0} — ieslēgt / apturēt priekšskatījumus" } },
            { "shortcut_pin", new[] { "{0} — pin the last preview", "{0} — piespraust pēdējo priekšskatījumu" } },
            { "hotkey_unavailable", new[] { "Shortcut is in use by another app; use this menu item.", "Saīsni izmanto cita programma; lieto šo izvēlnes pogu." } },
            { "saving", new[] { "Saving…", "Saglabā…" } },
            { "ppt_busy", new[] { "Slide export is unavailable while PowerPoint is open. Showing the saved preview.", "Kamēr PowerPoint ir atvērts, slaidus nevar eksportēt. Rāda saglabāto priekšskatījumu." } },
            { "ppt_missing", new[] { "Install PowerPoint to preview every slide. Showing the saved preview.", "Visu slaidu priekšskatīšanai vajadzīgs PowerPoint. Rāda saglabāto priekšskatījumu." } },
            { "ppt_unsupported", new[] { "This presentation contains content unsupported by slide export.", "Prezentācijā ir slaidu eksportam neatbalstīts saturs." } },
            { "ppt_slide_limit", new[] { "Slide export supports up to 200 slides.", "Slaidu eksports atbalsta līdz 200 slaidiem." } },
            { "ppt_timeout", new[] { "Slide preparation exceeded 60 seconds. Showing the saved preview.", "Slaidu sagatavošana pārsniedza 60 sekundes. Rāda saglabāto priekšskatījumu." } },
            { "ppt_export_failed", new[] { "PowerPoint could not prepare all slides. Showing the available preview.", "PowerPoint nevarēja sagatavot visus slaidus. Rāda pieejamo priekšskatījumu." } },
            { "preview_loading", new[] { "Loading preview…", "Ielādē priekšskatījumu…" } },
            { "image_unavailable", new[] { "Cannot decode this image. The file may be damaged or need a Windows codec.", "Attēlu nevar nolasīt. Fails var būt bojāts vai tam vajadzīgs Windows kodeks." } },
            { "image_limit", new[] { "Image exceeds the 100 MiB preview limit.", "Attēls pārsniedz priekšskatījuma 100 MiB ierobežojumu." } }
        };
        // Other languages follow ru, zh-CN, de, es, fr, pt-BR, it, ja, ko, uk.
        private static readonly Dictionary<string, string[]> translated = new Dictionary<string, string[]>
        {
            { "pinned_preview", new[] { "Закреплённый просмотр", "固定预览", "Angeheftete Vorschau", "Vista previa fijada", "Aperçu épinglé", "Prévia fixada", "Anteprima fissata", "固定プレビュー", "고정 미리보기", "Закріплений перегляд" } },
            { "maximize_preview", new[] { "Развернуть просмотр", "最大化预览", "Vorschau maximieren", "Maximizar vista previa", "Agrandir l’aperçu", "Maximizar prévia", "Ingrandisci anteprima", "プレビューを最大化", "미리보기 최대화", "Розгорнути перегляд" } },
            { "close_preview", new[] { "Закрыть просмотр", "关闭预览", "Vorschau schließen", "Cerrar vista previa", "Fermer l’aperçu", "Fechar prévia", "Chiudi anteprima", "プレビューを閉じる", "미리보기 닫기", "Закрити перегляд" } },
            { "zoom_level", new[] { "Масштаб · двойной щелчок вписывает изображение", "图像缩放 · 双击以适应窗口", "Bildmaßstab · Doppelklick passt das Bild ein", "Escala · doble clic para ajustar", "Échelle · double-clic pour ajuster", "Escala · clique duplo para ajustar", "Scala · doppio clic per adattare", "画像倍率 · ダブルクリックで全体表示", "이미지 배율 · 두 번 클릭하여 창에 맞춤", "Масштаб · подвійний клік вписує зображення" } },
            { "maximize_settings", new[] { "Развернуть настройки", "最大化设置", "Einstellungen maximieren", "Maximizar ajustes", "Agrandir les paramètres", "Maximizar configurações", "Ingrandisci impostazioni", "設定を最大化", "설정 최대화", "Розгорнути налаштування" } },
            { "restore_settings", new[] { "Восстановить размер окна", "还原窗口大小", "Fenstergröße wiederherstellen", "Restaurar tamaño de ventana", "Restaurer la taille de la fenêtre", "Restaurar tamanho da janela", "Ripristina dimensioni finestra", "ウィンドウサイズを元に戻す", "창 크기 복원", "Відновити розмір вікна" } },
            { "loop_video", new[] { "Повторять видео", "循环播放视频", "Videos wiederholen", "Repetir vídeos", "Lire les vidéos en boucle", "Repetir vídeos", "Ripeti video", "動画をループ再生", "동영상 반복", "Повторювати відео" } },
            { "loop_video_detail", new[] { "Начинать видео заново после окончания", "播放结束后自动重新开始", "Videos nach dem Ende automatisch neu starten", "Reiniciar los vídeos al terminar", "Relancer automatiquement les vidéos à la fin", "Reiniciar vídeos automaticamente ao terminar", "Riavvia automaticamente i video al termine", "再生終了後に自動で最初から再生", "재생이 끝나면 자동으로 다시 시작", "Починати відео знову після завершення" } },
            { "pin", new[] { "Закрепить последний просмотр", "固定上次预览", "Letzte Vorschau anheften", "Fijar última vista previa", "Épingler le dernier aperçu", "Fixar última prévia", "Fissa ultima anteprima", "最後のプレビューを固定", "최근 미리 보기 고정", "Закріпити останній перегляд" } },
            { "previous", new[] { "Предыдущая страница / слайд", "上一页 / 幻灯片", "Vorherige Seite / Folie", "Página / diapositiva anterior", "Page / diapositive précédente", "Página / slide anterior", "Pagina / diapositiva precedente", "前のページ / スライド", "이전 페이지 / 슬라이드", "Попередня сторінка / слайд" } },
            { "next", new[] { "Следующая страница / слайд", "下一页 / 幻灯片", "Nächste Seite / Folie", "Página / diapositiva siguiente", "Page / diapositive suivante", "Próxima página / slide", "Pagina / diapositiva successiva", "次のページ / スライド", "다음 페이지 / 슬라이드", "Наступна сторінка / слайд" } },
            { "play_pause", new[] { "Воспроизведение / пауза (Пробел)", "播放 / 暂停（空格）", "Wiedergabe / Pause (Leertaste)", "Reproducir / pausar (Espacio)", "Lecture / pause (Espace)", "Reproduzir / pausar (Espaço)", "Riproduci / pausa (Spazio)", "再生 / 一時停止（スペース）", "재생 / 일시 정지 (스페이스)", "Відтворення / пауза (Пробіл)" } },
            { "play", new[] { "Воспроизвести", "播放", "Wiedergeben", "Reproducir", "Lire", "Reproduzir", "Riproduci", "再生", "재생", "Відтворити" } },
            { "pause", new[] { "Пауза", "暂停", "Pause", "Pausar", "Pause", "Pausar", "Pausa", "一時停止", "일시 정지", "Пауза" } },
            { "zoom_in", new[] { "Увеличить (+ или Ctrl+колесо)", "放大（+ 或 Ctrl+滚轮）", "Vergrößern (+ oder Strg+Mausrad)", "Ampliar (+ o Ctrl+rueda)", "Agrandir (+ ou Ctrl+molette)", "Ampliar (+ ou Ctrl+roda)", "Ingrandisci (+ o Ctrl+rotella)", "拡大（+ または Ctrl+ホイール）", "확대 (+ 또는 Ctrl+휠)", "Збільшити (+ або Ctrl+колесо)" } },
            { "zoom_out", new[] { "Уменьшить (− или Ctrl+колесо)", "缩小（− 或 Ctrl+滚轮）", "Verkleinern (− oder Strg+Mausrad)", "Reducir (− o Ctrl+rueda)", "Réduire (− ou Ctrl+molette)", "Reduzir (− ou Ctrl+roda)", "Riduci (− o Ctrl+rotella)", "縮小（− または Ctrl+ホイール）", "축소 (− 또는 Ctrl+휠)", "Зменшити (− або Ctrl+колесо)" } },
            { "fit", new[] { "Вписать", "适应", "Einpassen", "Ajustar", "Ajuster", "Ajustar", "Adatta", "全体表示", "맞춤", "Вмістити" } },
            { "media_paused", new[] { "Пауза · закрепите просмотр для воспроизведения", "已暂停 · 固定预览后播放", "Pausiert · Vorschau zum Abspielen anheften", "En pausa · fija la vista previa para reproducir", "En pause · épinglez l’aperçu pour lire", "Pausado · fixe a prévia para reproduzir", "In pausa · fissa l’anteprima per riprodurre", "一時停止中 · 固定して再生", "일시 정지 · 미리 보기를 고정하여 재생", "Пауза · закріпіть перегляд для відтворення" } },
            { "autoplay", new[] { "Автовоспроизведение медиа", "自动播放音视频", "Medien automatisch abspielen", "Reproducción automática", "Lecture automatique", "Reprodução automática", "Riproduzione automatica", "メディアの自動再生", "미디어 자동 재생", "Автовідтворення медіа" } },
            { "autoplay_detail", new[] { "Запускать аудио и видео при наведении", "悬停时开始播放音视频", "Audio und Video beim Zeigen starten", "Iniciar audio y vídeo al pasar el cursor", "Lire l’audio et la vidéo au survol", "Iniciar áudio e vídeo ao passar o cursor", "Avvia audio e video al passaggio del mouse", "ポイント時に音声と動画を再生", "마우스를 올리면 오디오와 비디오 재생", "Запускати аудіо та відео при наведенні" } },
            { "file_types", new[] { "Разрешённые типы файлов", "启用的文件类型", "Aktivierte Dateitypen", "Tipos de archivo habilitados", "Types de fichiers activés", "Tipos de arquivo ativados", "Tipi di file abilitati", "有効なファイルの種類", "사용할 파일 유형", "Увімкнені типи файлів" } },
            { "type_images", new[] { "Изображения и GIF", "图像和 GIF", "Bilder und GIFs", "Imágenes y GIF", "Images et GIF", "Imagens e GIFs", "Immagini e GIF", "画像と GIF", "이미지 및 GIF", "Зображення та GIF" } },
            { "type_documents", new[] { "Документы и дизайн-проекты", "文档和设计项目", "Dokumente und Designprojekte", "Documentos y proyectos de diseño", "Documents et projets de création", "Documentos e projetos de design", "Documenti e progetti grafici", "文書とデザインプロジェクト", "문서 및 디자인 프로젝트", "Документи та дизайн-проєкти" } },
            { "type_text", new[] { "Текст и код", "文本和代码", "Text und Code", "Texto y código", "Texte et code", "Texto e código", "Testo e codice", "テキストとコード", "텍스트 및 코드", "Текст і код" } },
            { "type_audio", new[] { "Аудио", "音频", "Audio", "Audio", "Audio", "Áudio", "Audio", "音声", "오디오", "Аудіо" } },
            { "type_video", new[] { "Видео", "视频", "Video", "Vídeo", "Vidéo", "Vídeo", "Video", "動画", "비디오", "Відео" } },
            { "shortcut_toggle", new[] { "{0} — включить / приостановить просмотр", "{0} — 启用 / 暂停预览", "{0} — Vorschau ein / aus", "{0} — activar / pausar vistas previas", "{0} — activer / suspendre les aperçus", "{0} — ativar / pausar prévias", "{0} — attiva / sospendi anteprime", "{0} — プレビューの有効化 / 停止", "{0} — 미리 보기 켜기 / 일시 정지", "{0} — увімкнути / призупинити перегляд" } },
            { "shortcut_pin", new[] { "{0} — закрепить последний просмотр", "{0} — 固定上次预览", "{0} — letzte Vorschau anheften", "{0} — fijar última vista previa", "{0} — épingler le dernier aperçu", "{0} — fixar última prévia", "{0} — fissa ultima anteprima", "{0} — 最後のプレビューを固定", "{0} — 최근 미리 보기 고정", "{0} — закріпити останній перегляд" } },
            { "hotkey_unavailable", new[] { "Сочетание занято другой программой; используйте меню.", "快捷键已被其他应用占用，请使用菜单。", "Tastenkürzel belegt; bitte diesen Menüpunkt verwenden.", "Otra aplicación usa el atajo; utiliza este menú.", "Raccourci utilisé ailleurs ; utilisez ce menu.", "Atalho usado por outro app; use este menu.", "Scorciatoia occupata; usa questa voce di menu.", "他のアプリが使用中です。メニューを使ってください。", "다른 앱이 단축키를 사용 중입니다. 메뉴를 이용하세요.", "Сполучення зайняте іншою програмою; використайте меню." } },
            { "saving", new[] { "Сохранение…", "正在保存…", "Wird gespeichert…", "Guardando…", "Enregistrement…", "Salvando…", "Salvataggio…", "保存中…", "저장 중…", "Збереження…" } },
            { "ppt_busy", new[] { "PowerPoint открыт. Показан сохранённый просмотр.", "PowerPoint 正在运行，显示已保存的预览。", "PowerPoint ist geöffnet. Gespeicherte Vorschau wird angezeigt.", "PowerPoint está abierto. Se muestra la vista guardada.", "PowerPoint est ouvert. Affichage de l’aperçu enregistré.", "PowerPoint está aberto. Exibindo a prévia salva.", "PowerPoint è aperto. È mostrata l’anteprima salvata.", "PowerPoint が開いているため保存済みプレビューを表示します。", "PowerPoint가 열려 있어 저장된 미리 보기를 표시합니다.", "PowerPoint відкрито. Показано збережений перегляд." } },
            { "ppt_missing", new[] { "Для всех слайдов нужен PowerPoint. Показан сохранённый просмотр.", "需要安装 PowerPoint 才能预览所有幻灯片，现显示已保存的预览。", "Für alle Folien ist PowerPoint erforderlich. Gespeicherte Vorschau wird angezeigt.", "Instala PowerPoint para ver todas las diapositivas. Se muestra la vista guardada.", "PowerPoint est requis pour toutes les diapositives. Affichage de l’aperçu enregistré.", "Instale PowerPoint para ver todos os slides. Exibindo a prévia salva.", "Serve PowerPoint per tutte le diapositive. È mostrata l’anteprima salvata.", "全スライドの表示には PowerPoint が必要です。保存済みプレビューを表示します。", "전체 슬라이드를 보려면 PowerPoint가 필요합니다. 저장된 미리 보기를 표시합니다.", "Для всіх слайдів потрібен PowerPoint. Показано збережений перегляд." } },
            { "ppt_unsupported", new[] { "В презентации есть неподдерживаемое для экспорта содержимое.", "演示文稿含有不支持导出的内容。", "Die Präsentation enthält nicht unterstützte Exportinhalte.", "La presentación contiene elementos no compatibles con la exportación.", "La présentation contient des éléments non pris en charge pour l’export.", "A apresentação contém conteúdo incompatível com a exportação.", "La presentazione contiene contenuti non supportati dall’esportazione.", "書き出しに対応していない内容が含まれています。", "프레젠테이션에 내보내기가 지원되지 않는 콘텐츠가 있습니다.", "Презентація містить непідтримуваний для експорту вміст." } },
            { "ppt_slide_limit", new[] { "Экспорт поддерживает до 200 слайдов.", "最多支持导出 200 张幻灯片。", "Der Export unterstützt bis zu 200 Folien.", "La exportación admite hasta 200 diapositivas.", "L’export prend en charge jusqu’à 200 diapositives.", "A exportação aceita até 200 slides.", "L’esportazione supporta fino a 200 diapositive.", "最大 200 スライドを書き出せます。", "최대 200개 슬라이드를 내보낼 수 있습니다.", "Експорт підтримує до 200 слайдів." } },
            { "ppt_timeout", new[] { "Подготовка превысила 60 секунд. Показан сохранённый просмотр.", "准备时间超过 60 秒，显示已保存的预览。", "Vorbereitung nach 60 Sekunden abgebrochen. Gespeicherte Vorschau wird angezeigt.", "La preparación superó 60 segundos. Se muestra la vista guardada.", "La préparation a dépassé 60 secondes. Affichage de l’aperçu enregistré.", "A preparação excedeu 60 segundos. Exibindo a prévia salva.", "Preparazione oltre 60 secondi. È mostrata l’anteprima salvata.", "準備に 60 秒以上かかったため保存済みプレビューを表示します。", "준비 시간이 60초를 초과했습니다. 저장된 미리 보기를 표시합니다.", "Підготовка перевищила 60 секунд. Показано збережений перегляд." } },
            { "ppt_export_failed", new[] { "Не удалось подготовить все слайды. Показан доступный просмотр.", "无法准备所有幻灯片，显示可用的预览。", "Nicht alle Folien konnten vorbereitet werden. Verfügbare Vorschau wird angezeigt.", "No se pudieron preparar todas las diapositivas. Se muestra la vista disponible.", "Toutes les diapositives n’ont pas pu être préparées. Affichage de l’aperçu disponible.", "Não foi possível preparar todos os slides. Exibindo a prévia disponível.", "Impossibile preparare tutte le diapositive. È mostrata l’anteprima disponibile.", "全スライドを準備できませんでした。利用可能なプレビューを表示します。", "모든 슬라이드를 준비하지 못했습니다. 사용 가능한 미리 보기를 표시합니다.", "Не вдалося підготувати всі слайди. Показано доступний перегляд." } },
            { "preview_loading", new[] { "Загрузка просмотра…", "正在加载预览…", "Vorschau wird geladen…", "Cargando vista previa…", "Chargement de l’aperçu…", "Carregando prévia…", "Caricamento anteprima…", "プレビューを読み込み中…", "미리 보기 로드 중…", "Завантаження перегляду…" } },
            { "image_unavailable", new[] { "Не удалось прочитать изображение. Возможно, файл повреждён или нужен кодек Windows.", "无法解码图像，文件可能损坏或需要 Windows 编解码器。", "Bild nicht lesbar. Datei möglicherweise beschädigt oder Windows-Codec fehlt.", "No se pudo leer la imagen. Puede estar dañada o necesitar un códec de Windows.", "Image illisible. Fichier endommagé ou codec Windows manquant.", "Não foi possível ler a imagem. Arquivo danificado ou codec do Windows ausente.", "Immagine illeggibile. File danneggiato o codec Windows mancante.", "画像を読み込めません。破損または Windows コーデック不足の可能性があります。", "이미지를 읽을 수 없습니다. 파일이 손상되었거나 Windows 코덱이 필요할 수 있습니다.", "Не вдалося прочитати зображення. Файл може бути пошкоджений або потрібен кодек Windows." } },
            { "image_limit", new[] { "Изображение превышает предел просмотра 100 МиБ.", "图像超过 100 MiB 预览限制。", "Bild überschreitet die Vorschaugrenze von 100 MiB.", "La imagen supera el límite de 100 MiB.", "L’image dépasse la limite de 100 Mio.", "A imagem excede o limite de 100 MiB.", "L’immagine supera il limite di 100 MiB.", "画像がプレビュー上限の 100 MiB を超えています。", "이미지가 미리 보기 제한인 100 MiB를 초과합니다.", "Зображення перевищує ліміт перегляду 100 МіБ." } },
        };
        internal static string Text(string language, string key)
        {
            string[] values;
            if (!strings.TryGetValue(key, out values)) return key;
            int index = L10n.LanguageIndex(language);
            string[] local;
            if (index != 0 && index != 2 && translated.TryGetValue(key, out local))
                return local[index == 1 ? 0 : index - 2];
            return values[index == 2 ? 1 : 0];
        }
    }
}

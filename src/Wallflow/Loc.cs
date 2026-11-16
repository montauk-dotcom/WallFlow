using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace Wallflow;

/// <summary>
/// Interface language. Russian is the source text; English comes from the table below.
/// Windows call <see cref="Apply"/> once after InitializeComponent; code uses <see cref="T"/> / <see cref="F"/>.
/// </summary>
public static class Loc
{
    public static bool En { get; set; }

    public static string T(string ru) => En && Map.TryGetValue(ru, out var en) ? en : ru;
    public static string F(string ru, params object[] args) => string.Format(T(ru), args);

    /// <summary>Translates every text in a window/element tree (XAML-defined texts).</summary>
    public static void Apply(DependencyObject root)
    {
        if (!En) return;
        Walk(root);
    }

    static void Walk(object o)
    {
        switch (o)
        {
            case Window w: w.Title = T(w.Title); break;
        }
        if (o is TextBlock tb)
        {
            if (tb.Inlines.Count > 1 || (tb.Inlines.Count == 1 && tb.Inlines.FirstInline is not Run))
            { foreach (var inl in tb.Inlines.ToList()) if (inl is Run r) r.Text = T(r.Text); }
            else if (!string.IsNullOrEmpty(tb.Text)) tb.Text = T(tb.Text);
        }
        if (o is HeaderedItemsControl hi && hi.Header is string hs) hi.Header = T(hs);
        if (o is HeaderedContentControl hc && hc.Header is string hcs) hc.Header = T(hcs);
        if (o is ContentControl cc && cc.Content is string cs) cc.Content = T(cs);
        if (o is FrameworkElement fe && fe.ToolTip is string ts) fe.ToolTip = T(ts);
        if (o is DependencyObject d)
            foreach (var child in LogicalTreeHelper.GetChildren(d).Cast<object>().ToList())
                Walk(child);
    }

    static readonly Dictionary<string, string> Map = new()
    {
        // ---- tray / app ----
        ["Wallflow запущен"] = "Wallflow is running",
        ["Виджеты уже на рабочем столе. Значок в трее — обои, настройки, перестановка виджетов."] = "Widgets are on your desktop. Tray icon: wallpapers, settings, moving widgets.",
        ["Не удалось сменить обои"] = "Couldn't change the wallpaper",
        ["🖼  Обои…"] = "🖼  Wallpapers…",
        ["⟳  Следующие обои"] = "⟳  Next wallpaper",
        ["Слайдшоу обоев"] = "Wallpaper slideshow",
        ["✎  Переставить виджеты"] = "✎  Move widgets",
        ["Виджеты"] = "Widgets",
        ["Часы и день недели"] = "Clock and weekday",
        ["Погода"] = "Weather",
        ["Календарь"] = "Calendar",
        ["Папки с ярлыками"] = "Shortcut folders",
        ["Плеер"] = "Player",
        ["Список дел"] = "To-do list",
        ["＋ Новая заметка"] = "＋ New note",
        ["Вернуть на места по умолчанию"] = "Reset positions",
        ["Скрыть обычные значки рабочего стола"] = "Hide regular desktop icons",
        ["Видео-обои"] = "Video wallpaper",
        ["Разложить ярлыки по папкам заново"] = "Sort shortcuts into folders again",
        ["Папки соберутся заново по категориям (игры, браузеры…). Твои ручные перестановки сбросятся. Продолжить?"] = "Folders will be rebuilt by category (games, browsers…). Your manual changes will be lost. Continue?",
        ["⚙  Настройки…"] = "⚙  Settings…",
        ["Запускать вместе с Windows"] = "Start with Windows",
        ["Выход"] = "Exit",
        // ---- color picker / prompt ----
        ["Выбери цвет"] = "Pick a color",
        ["Готово"] = "Done",
        ["Отмена"] = "Cancel",
        // ---- gallery ----
        ["Wallflow — обои"] = "Wallflow — wallpapers",
        ["Обзор"] = "Browse",
        ["Bing дня"] = "Bing daily",
        ["Избранное "] = "Favorites ",
        ["Избранное"] = "Favorites",
        ["Слова, #тег, like:id (похожие)"] = "Words, #tag, like:id (similar)",
        ["Найти"] = "Search",
        ["Настройки"] = "Settings",
        ["РАЗРЕШЕНИЕ"] = "RESOLUTION",
        ["Не меньше"] = "At least",
        ["Точно"] = "Exactly",
        ["⌖ Под мой экран"] = "⌖ Match my screen",
        ["СООТНОШЕНИЕ СТОРОН"] = "ASPECT RATIO",
        ["ЦВЕТ"] = "COLOR",
        ["ТЕМЫ"] = "TOPICS",
        ["КАТЕГОРИИ"] = "CATEGORIES",
        ["Общие"] = "General",
        ["Аниме"] = "Anime",
        ["Люди"] = "People",
        ["СОРТИРОВКА"] = "SORTING",
        ["Топ"] = "Top",
        ["Новые"] = "Newest",
        ["Популярные сейчас"] = "Hot",
        ["Просмотры"] = "Most viewed",
        ["Больше всего в избранном"] = "Most favorited",
        ["Релевантность"] = "Relevance",
        ["Случайно"] = "Random",
        ["за день"] = "last day",
        ["за 3 дня"] = "last 3 days",
        ["за неделю"] = "last week",
        ["за месяц"] = "last month",
        ["за 3 месяца"] = "last 3 months",
        ["за полгода"] = "last 6 months",
        ["за год"] = "last year",
        ["Применить"] = "Apply",
        ["Сбросить"] = "Reset",
        ["Обои не хранятся в приложении — они загружаются из Wallhaven и Bing по API."] = "Wallpapers aren't stored in the app — they're loaded from Wallhaven and Bing via API.",
        ["🖥  Установить на рабочий стол"] = "🖥  Set as wallpaper",
        ["Установить на рабочий стол"] = "Set as wallpaper",
        ["⬇  Скачать"] = "⬇  Download",
        ["≈  Похожие"] = "≈  Similar",
        ["Открыть источник ↗"] = "Open source page ↗",
        ["Альбомные"] = "Landscape",
        ["Портретные"] = "Portrait",
        ["Все"] = "All",
        ["Любое"] = "Any",
        ["Загрузка…"] = "Loading…",
        ["Ничего не найдено — попробуй ослабить фильтры."] = "Nothing found — try looser filters.",
        ["Ошибка: "] = "Error: ",
        ["Скачиваю и ставлю обои…"] = "Downloading and applying…",
        ["Обои установлены ✓"] = "Wallpaper set ✓",
        ["Категория: "] = "Category: ",
        ["Источник: "] = "Source: ",
        ["★  В избранном"] = "★  In favorites",
        ["☆  В избранное"] = "☆  Add to favorites",
        ["Искать в этом цвете"] = "Search this color",
        ["Не удалось загрузить"] = "Couldn't load",
        ["Сохранено"] = "Saved",
        ["Мой экран  {0}"] = "My screen  {0}",
        ["Ищу не меньше {0}×{1} — как твой экран"] = "Searching at least {0}×{1} — your screen",
        ["В избранном: {0}"] = "Favorites: {0}",
        ["Пока пусто — жми ☆ на обоях."] = "Empty so far — press ☆ on a wallpaper.",
        ["Bing — обои дня за последние {0} дней, 4K"] = "Bing — daily wallpapers of the last {0} days, 4K",
        ["Найдено: {0:N0} · страница {1} из {2}"] = "Found: {0:N0} · page {1} of {2}",
        ["Разрешение: {0}"] = "Resolution: {0}",
        ["Размер: {0:0.0} МБ"] = "Size: {0:0.0} MB",
        ["Просмотры: {0:N0} · ★ {1:N0}"] = "Views: {0:N0} · ★ {1:N0}",
        // topics
        ["Природа"] = "Nature", ["Пейзажи"] = "Landscapes", ["Горы"] = "Mountains", ["Море"] = "Sea", ["Лес"] = "Forest",
        ["Закат"] = "Sunset", ["Космос"] = "Space", ["Планеты"] = "Planets", ["Города"] = "Cities", ["Ночной город"] = "Night city",
        ["Архитектура"] = "Architecture", ["Машины"] = "Cars", ["Мото"] = "Bikes", ["Авиация"] = "Aviation", ["Игры"] = "Games",
        ["Киберпанк"] = "Cyberpunk", ["Неон"] = "Neon", ["Синтвейв"] = "Synthwave", ["Минимализм"] = "Minimalism",
        ["Абстракция"] = "Abstract", ["Тёмные"] = "Dark", ["Диджитал-арт"] = "Digital art", ["Пиксель-арт"] = "Pixel art",
        ["Фэнтези"] = "Fantasy", ["Животные"] = "Animals", ["Кошки"] = "Cats", ["Цветы"] = "Flowers", ["Дождь"] = "Rain",
        ["Зима"] = "Winter", ["Япония"] = "Japan", ["Технологии"] = "Technology", ["Музыка"] = "Music", ["Спорт"] = "Sport",
        ["Еда"] = "Food", ["Винтаж"] = "Vintage",
        // folder categories
        ["Браузеры"] = "Browsers", ["Общение"] = "Chat", ["Медиа"] = "Media", ["Творчество"] = "Creative",
        ["Разработка"] = "Development", ["Офис"] = "Office", ["Утилиты"] = "Utilities", ["Другое"] = "Other",
        // ---- settings ----
        ["Wallflow — настройки"] = "Wallflow — settings",
        ["🕒  Виджеты"] = "🕒  Widgets",
        ["📁  Рабочий стол и папки"] = "📁  Desktop & folders",
        ["🖼  Обои"] = "🖼  Wallpapers",
        ["⚙  Общее"] = "⚙  General",
        ["Открыть галерею обоев"] = "Open wallpaper gallery",
        ["✎ Переставить виджеты"] = "✎ Move widgets",
        ["✓ Готово (закрепить виджеты)"] = "✓ Done (lock widgets)",
        ["Плеер (что сейчас играет)"] = "Player (now playing)",
        ["＋ Добавить заметку"] = "＋ Add note",
        ["Заметки появляются на рабочем столе; удалить — крестик на самой заметке."] = "Notes appear on the desktop; delete with the ✕ on the note.",
        ["Заметок на рабочем столе: {0}"] = "Notes on the desktop: {0}",
        ["СТИЛЬ ЧАСОВ (клик — применить)"] = "CLOCK STYLE (click to apply)",
        ["Язык дней и месяцев:"] = "Day & month names:",
        ["Русский (Воскресенье)"] = "Russian (Воскресенье)",
        ["24-часовой формат"] = "24-hour time",
        ["Показывать секунды"] = "Show seconds",
        ["Тень под текстом"] = "Text shadow",
        ["Цвет часов:"] = "Clock color:",
        ["Цвет часов"] = "Clock color",
        ["Свой цвет…"] = "Custom color…",
        ["Градиент:"] = "Gradient:",
        ["Включить"] = "Enable",
        ["Первый цвет"] = "First color",
        ["Второй цвет"] = "Second color",
        ["Первый цвет градиента"] = "First gradient color",
        ["Второй цвет градиента"] = "Second gradient color",
        ["угол"] = "angle",
        ["Готовые градиенты:"] = "Preset gradients:",
        ["Закат "] = "Sunset ", ["Океан"] = "Ocean", ["Аврора"] = "Aurora", ["Золото"] = "Gold", ["Лаванда"] = "Lavender",
        ["Огонь"] = "Fire", ["Мята"] = "Mint",
        ["ПОГОДА"] = "WEATHER",
        ["Сохранить город"] = "Save city",
        ["Например: Tashkent, Ташкент, Moscow"] = "e.g. Tashkent, London, New York",
        ["Данные: Open-Meteo (бесплатно, без ключа). Обновление раз в 30 минут."] = "Data: Open-Meteo (free, no key). Updated every 30 minutes.",
        ["Ищу…"] = "Searching…",
        ["Сейчас: {0}"] = "Now: {0}",
        ["Сейчас: {0} ({1:0.00}, {2:0.00})"] = "Now: {0} ({1:0.00}, {2:0.00})",
        ["СТЕКЛО ВИДЖЕТОВ И ПАПОК"] = "GLASS OF WIDGETS AND FOLDERS",
        ["Матовое стекло (размытые обои, как на iPhone)"] = "Frosted glass (blurred wallpaper, like iPhone)",
        ["Тонировка"] = "Tinted",
        ["Только контур"] = "Outline only",
        ["Тёмный оттенок"] = "Dark tint",
        ["Светлый оттенок"] = "Light tint",
        ["Размытие"] = "Blur",
        ["Затемнение / осветление"] = "Tint strength",
        ["Скругление углов"] = "Corner radius",
        ["Размер виджетов"] = "Widget size",
        ["Вернуть виджеты на места по умолчанию"] = "Put widgets back to default places",
        ["Рабочий стол и папки"] = "Desktop & folders",
        ["Скрыть обычные значки рабочего стола (останутся только виджеты и папки)"] = "Hide regular desktop icons (only widgets and folders stay)",
        ["Новые ярлыки на рабочем столе сами попадают в подходящую папку"] = "New desktop shortcuts go into the right folder automatically",
        ["Открывать папки:"] = "Open folders:",
        ["кликом"] = "on click",
        ["при наведении"] = "on hover",
        ["Разложить всё по папкам заново"] = "Sort everything again",
        ["Новая папка"] = "New folder",
        ["Новая папка…"] = "New folder…",
        ["Файлы ярлыков никуда не перемещаются и не удаляются — папки только показывают их красиво. При выходе из Wallflow обычные значки возвращаются. Ещё можно перетащить любой файл прямо на папку-виджет."] = "Shortcut files are never moved or deleted — folders only show them nicely. Regular icons come back when Wallflow exits. You can also drop any file onto a folder widget.",
        ["Папки соберутся заново по категориям. Ручные перестановки сбросятся. Продолжить?"] = "Folders will be rebuilt by category. Manual changes will be lost. Continue?",
        ["Выше"] = "Up",
        ["Ниже"] = "Down",
        ["Переименовать"] = "Rename",
        ["Удалить папку (ярлыки уйдут в «Другое»)"] = "Delete folder (shortcuts go to “Other”)",
        ["Переместить выбранные в:"] = "Move selected to:",
        ["Убрать"] = "Remove",
        ["Убрать из папок (файл останется)"] = "Remove from folders (the file stays)",
        ["Название новой папки"] = "New folder name",
        ["Название папки"] = "Folder name",
        ["Обои"] = "Wallpapers",
        ["Как размещать картинку"] = "Picture position",
        ["Заполнение"] = "Fill",
        ["По размеру"] = "Fit",
        ["Растянуть"] = "Stretch",
        ["По центру"] = "Center",
        ["Замостить"] = "Tile",
        ["Расширение (на несколько мониторов)"] = "Span (multiple monitors)",
        ["Папка для скачанных обоев"] = "Folder for downloaded wallpapers",
        ["Выбрать…"] = "Choose…",
        ["Открыть"] = "Open",
        ["Куда сохранять обои"] = "Where to save wallpapers",
        ["Ключ API Wallhaven (необязательно)"] = "Wallhaven API key (optional)",
        ["ВИДЕО-ОБОИ ИЗ СВОЕГО ФАЙЛА"] = "VIDEO WALLPAPER FROM YOUR FILE",
        ["Выбрать видео…"] = "Choose video…",
        ["Выключить"] = "Turn off",
        ["Без звука"] = "Muted",
        ["Пауза, когда окно на весь экран"] = "Pause when a window covers the screen",
        ["Надёжный режим (прячет значки)"] = "Reliable mode (hides icons)",
        ["MP4/MOV/WMV. Видео играет на рабочем столе и встаёт на паузу, когда его не видно. Если на Windows 11 24H2 видео не появилось — включи надёжный режим."] = "MP4/MOV/WMV. The video plays on the desktop and pauses when it can't be seen. If it doesn't show on Windows 11 24H2, turn on reliable mode.",
        ["▶ Сейчас: "] = "▶ Now: ",
        ["Выключены"] = "Off",
        ["Видео|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv|Все файлы|*.*"] = "Video|*.mp4;*.m4v;*.mov;*.wmv;*.avi;*.mkv|All files|*.*",
        ["Видео для обоев"] = "Video for wallpaper",
        ["СЛАЙДШОУ — АВТОСМЕНА ОБОЕВ"] = "SLIDESHOW — CHANGE WALLPAPER AUTOMATICALLY",
        ["Каждые"] = "Every",
        ["минут, источник:"] = "minutes, source:",
        ["Wallhaven с последними фильтрами"] = "Wallhaven with the last filters",
        ["Bing — обои дня"] = "Bing — daily wallpaper",
        ["Сменить сейчас"] = "Change now",
        ["Общее"] = "General",
        ["Запускать вместе с Windows (сразу в трей)"] = "Start with Windows (straight to the tray)",
        ["Автозапуск"] = "Autostart",
        ["ЯЗЫК ИНТЕРФЕЙСА"] = "INTERFACE LANGUAGE",
        ["ПОТРЕБЛЕНИЕ"] = "RESOURCE USAGE",
        ["Освободить память сейчас"] = "Free memory now",
        ["Сейчас Wallflow занимает {0:0} МБ RAM (с открытым окном настроек; в трее — меньше). Приватная память: {1:0} МБ."] = "Wallflow uses {0:0} MB of RAM right now (with this window open; less in the tray). Private memory: {1:0} MB.",
        ["Wallflow — один нативный процесс без браузерного движка. Галерея и это окно создаются только когда открыты и полностью выгружаются после закрытия. Часы просыпаются раз в минуту, погода — раз в 30 минут, календарь — в полночь. В фоне процессор ≈ 0%."] = "Wallflow is a single native process without a browser engine. The gallery and this window exist only while open and are fully unloaded after closing. The clock wakes up once a minute, weather every 30 minutes, the calendar at midnight. CPU in the background ≈ 0%.",
        ["Шрифты: Bebas Neue, Great Vibes, Orbitron, Unbounded, Monoton, Major Mono Display, Comfortaa, Poiret One — SIL Open Font License. Обои: Wallhaven, Bing. Погода: Open-Meteo."] = "Fonts: Bebas Neue, Great Vibes, Orbitron, Unbounded, Monoton, Major Mono Display, Comfortaa, Poiret One — SIL Open Font License. Wallpapers: Wallhaven, Bing. Weather: Open-Meteo.",
        // clock faces / palette
        ["Постер"] = "Poster", ["Контур"] = "Outline", ["Каллиграфия"] = "Script", ["Градиент"] = "Gradient", ["Стопка"] = "Stack",
        ["Как на iPhone"] = "iPhone", ["Стрелочные"] = "Analog", ["Словами"] = "In words", ["Жирный"] = "Bold", ["Ретро"] = "Retro",
        ["Моно"] = "Mono", ["Мягкий"] = "Soft", ["Тонкий"] = "Thin", ["Минимум"] = "Minimal",
        ["Белый"] = "White", ["Розовый"] = "Pink", ["Голубой"] = "Cyan", ["Фиолетовый"] = "Violet", ["Золотой"] = "Gold",
        ["Мятный"] = "Mint", ["Красный"] = "Red",
        // ---- folders ----
        ["Переименовать…"] = "Rename…",
        ["Переставить виджеты"] = "Move widgets",
        ["Настройки папок…"] = "Folder settings…",
        ["Не удалось запустить"] = "Couldn't start",
        ["Открыть расположение"] = "Open file location",
        ["Переместить в"] = "Move to",
        ["Убрать из папки (ярлык останется на диске)"] = "Remove from folder (the shortcut stays on disk)",
        // ---- notes ----
        ["Мелкий"] = "Small", ["Обычный"] = "Normal", ["Крупный"] = "Large", ["Очень крупный"] = "Extra large", ["Огромный"] = "Huge",
        ["Узкая"] = "Narrow", ["Обычная"] = "Normal", ["Широкая"] = "Wide", ["Очень широкая"] = "Extra wide",
        ["Текст меньше"] = "Smaller text",
        ["Текст больше"] = "Bigger text",
        ["Удалить заметку"] = "Delete note",
        ["Сменить цвет"] = "Change color",
        ["Заголовок"] = "Title",
        ["Текст заметки…"] = "Note text…",
        ["Размер текста"] = "Text size",
        ["Ширина"] = "Width",
        ["Удалить эту заметку?"] = "Delete this note?",
        // ---- player ----
        ["Ничего не играет"] = "Nothing playing",
        ["Включи музыку или видео"] = "Play some music or a video",
        ["Медиа-управление Windows недоступно"] = "Windows media controls unavailable",
        ["Без названия"] = "Untitled",
        ["звук"] = "app",
        ["система"] = "system",
        ["МЕДИАПЛЕЕР"] = "MEDIA PLAYER",
        // ---- to-do ----
        ["Настройки списка"] = "List settings",
        ["Новая задача… (Enter)"] = "New task… (Enter)",
        ["Расстояние между пунктами"] = "Spacing",
        ["Плотно"] = "Tight", ["Обычно"] = "Normal", ["Свободно"] = "Relaxed", ["Просторно"] = "Spacious",
        ["Выполненные опускать вниз"] = "Move done tasks to the bottom",
        ["Удалить выполненные"] = "Delete done tasks",
        ["Переименовать список…"] = "Rename list…",
        ["Название списка"] = "List name",
    };
}

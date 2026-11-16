using System.Globalization;

namespace Wallflow.Core;

/// <summary>
/// Interface language. The app is written in Russian; for English every UI string is looked up here.
/// Unknown strings (user data: note text, file names…) are left as they are.
/// </summary>
public static class L
{
    /// <summary>"ru" or "en"</summary>
    public static string Lang { get; set; } = "ru";
    public static bool En => Lang == "en";

    /// <summary>Default for a fresh install: Russian for ru/uk/be/kk/uz/ky systems, English otherwise.</summary>
    public static string SystemDefault()
    {
        var c = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return c is "ru" or "uk" or "be" or "kk" or "uz" or "ky" or "tg" ? "ru" : "en";
    }

    public static string T(string ru) => En && ru != null && Map.TryGetValue(ru, out var en) ? en : ru!;

    public static string F(string ruFormat, params object?[] args) => string.Format(T(ruFormat), args);

    /// <summary>"1 объект / 5 объектов" — "1 item / 5 items".</summary>
    public static string Items(int n)
    {
        if (En) return n == 1 ? "1 item" : $"{n} items";
        int m10 = n % 10, m100 = n % 100;
        var w = m10 == 1 && m100 != 11 ? "объект" : m10 is >= 2 and <= 4 && (m100 < 12 || m100 > 14) ? "объекта" : "объектов";
        return $"{n} {w}";
    }

    public static bool Has(string ru) => Map.ContainsKey(ru);

    static readonly Dictionary<string, string> Map = new()
    {
        // ---- tray / app
        ["Wallflow запущен"] = "Wallflow is running",
        ["Виджеты уже на рабочем столе. Значок в трее — обои, настройки, перестановка виджетов."] = "Widgets are on your desktop. The tray icon has wallpapers, settings and widget arranging.",
        ["Не удалось сменить обои"] = "Couldn't change the wallpaper",
        ["🖼  Обои…"] = "🖼  Wallpapers…",
        ["⟳  Следующие обои"] = "⟳  Next wallpaper",
        ["Слайдшоу обоев"] = "Wallpaper slideshow",
        ["✎  Переставить виджеты"] = "✎  Arrange widgets",
        ["Виджеты"] = "Widgets",
        ["Часы и день недели"] = "Clock & day name",
        ["Погода"] = "Weather",
        ["Календарь"] = "Calendar",
        ["Папки с ярлыками"] = "Folders",
        ["Плеер"] = "Player",
        ["Список дел"] = "To-do list",
        ["＋ Новая заметка"] = "＋ New note",
        ["Вернуть на места по умолчанию"] = "Reset positions",
        ["Скрыть обычные значки рабочего стола"] = "Hide regular desktop icons",
        ["Видео-обои"] = "Video wallpaper",
        ["Разложить ярлыки по папкам заново"] = "Re-sort desktop into folders",
        ["Папки соберутся заново по категориям (игры, браузеры…). Твои ручные перестановки сбросятся. Продолжить?"] = "Folders will be rebuilt by category (games, browsers…). Your manual changes will be lost. Continue?",
        ["⚙  Настройки…"] = "⚙  Settings…",
        ["Запускать вместе с Windows"] = "Start with Windows",
        ["Выход"] = "Exit",

        // ---- common
        ["Выбери цвет"] = "Pick a color",
        ["Готово"] = "Done",
        ["Отмена"] = "Cancel",
        ["Ошибка: "] = "Error: ",
        ["Сохранено"] = "Saved",
        ["Открыть"] = "Open",
        ["Выбрать…"] = "Browse…",
        ["Включить"] = "Enable",
        ["Выключить"] = "Turn off",
        ["Выключены"] = "Off",
        ["Переименовать"] = "Rename",
        ["Переименовать…"] = "Rename…",
        ["Убрать"] = "Remove",
        ["Выше"] = "Up",
        ["Ниже"] = "Down",

        // ---- gallery
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
        ["СОРТИРОВКА"] = "SORT",
        ["Топ"] = "Top",
        ["Новые"] = "Latest",
        ["Популярные сейчас"] = "Hot",
        ["Просмотры"] = "Views",
        ["Больше всего в избранном"] = "Most favorited",
        ["Релевантность"] = "Relevance",
        ["Случайно"] = "Random",
        ["за день"] = "past day",
        ["за 3 дня"] = "past 3 days",
        ["за неделю"] = "past week",
        ["за месяц"] = "past month",
        ["за 3 месяца"] = "past 3 months",
        ["за полгода"] = "past 6 months",
        ["за год"] = "past year",
        ["Применить"] = "Apply",
        ["Сбросить"] = "Reset",
        ["Обои не хранятся в приложении — они загружаются из Wallhaven и Bing по API."] = "Wallpapers aren't stored in the app — they're loaded from Wallhaven and Bing via their APIs.",
        ["Загрузка…"] = "Loading…",
        ["🖥  Установить на рабочий стол"] = "🖥  Set as wallpaper",
        ["Установить на рабочий стол"] = "Set as wallpaper",
        ["☆  В избранное"] = "☆  Add to favorites",
        ["★  В избранном"] = "★  In favorites",
        ["⬇  Скачать"] = "⬇  Download",
        ["≈  Похожие"] = "≈  Similar",
        ["Открыть источник ↗"] = "Open source page ↗",
        ["Альбомные"] = "Landscape", ["Портретные"] = "Portrait",
        ["Все"] = "All", ["Любое"] = "Any",
        ["Природа"] = "Nature", ["Пейзажи"] = "Landscapes", ["Горы"] = "Mountains", ["Море"] = "Sea", ["Лес"] = "Forest",
        ["Закат"] = "Sunset", ["Космос"] = "Space", ["Планеты"] = "Planets", ["Города"] = "Cities", ["Ночной город"] = "Night city",
        ["Архитектура"] = "Architecture", ["Машины"] = "Cars", ["Мото"] = "Motorbikes", ["Авиация"] = "Aviation", ["Игры"] = "Games",
        ["Киберпанк"] = "Cyberpunk", ["Неон"] = "Neon", ["Синтвейв"] = "Synthwave", ["Минимализм"] = "Minimal", ["Абстракция"] = "Abstract",
        ["Тёмные"] = "Dark", ["Диджитал-арт"] = "Digital art", ["Пиксель-арт"] = "Pixel art", ["Фэнтези"] = "Fantasy", ["Животные"] = "Animals",
        ["Кошки"] = "Cats", ["Цветы"] = "Flowers", ["Дождь"] = "Rain", ["Зима"] = "Winter", ["Япония"] = "Japan", ["Технологии"] = "Tech",
        ["Музыка"] = "Music", ["Спорт"] = "Sport", ["Еда"] = "Food", ["Винтаж"] = "Vintage",
        ["Мой экран  {0}"] = "My screen  {0}",
        ["Ищу не меньше {0}×{1} — как твой экран"] = "Looking for at least {0}×{1} — your screen",
        ["В избранном: {0}"] = "Favorites: {0}",
        ["Пока пусто — жми ☆ на обоях."] = "Nothing yet — press ☆ on a wallpaper.",
        ["Bing — обои дня за последние {0} дней, 4K"] = "Bing — wallpaper of the day, last {0} days, 4K",
        ["Найдено: {0:N0} · страница {1} из {2}"] = "Found: {0:N0} · page {1} of {2}",
        ["Ничего не найдено — попробуй ослабить фильтры."] = "Nothing found — try looser filters.",
        ["Скачиваю и ставлю обои…"] = "Downloading and setting the wallpaper…",
        ["Обои установлены ✓"] = "Wallpaper set ✓",
        ["Разрешение: {0}"] = "Resolution: {0}",
        ["Категория: "] = "Category: ",
        ["Размер: {0:0.0} МБ"] = "Size: {0:0.0} MB",
        ["Просмотры: {0:N0} · ★ {1:N0}"] = "Views: {0:N0} · ★ {1:N0}",
        ["Источник: "] = "Source: ",
        ["Искать в этом цвете"] = "Search this color",
        ["Не удалось загрузить"] = "Couldn't load",

        // ---- settings
        ["Wallflow — настройки"] = "Wallflow — settings",
        ["🕒  Виджеты"] = "🕒  Widgets",
        ["📁  Рабочий стол и папки"] = "📁  Desktop & folders",
        ["🖼  Обои"] = "🖼  Wallpapers",
        ["⚙  Общее"] = "⚙  General",
        ["Открыть галерею обоев"] = "Open wallpaper gallery",
        ["✎ Переставить виджеты"] = "✎ Arrange widgets",
        ["✓ Готово (закрепить виджеты)"] = "✓ Done (lock widgets)",
        ["Плеер (что сейчас играет)"] = "Player (now playing)",
        ["＋ Добавить заметку"] = "＋ Add a note",
        ["СТИЛЬ ЧАСОВ (клик — применить)"] = "CLOCK STYLE (click to apply)",
        ["Язык дней и месяцев:"] = "Day & month names:",
        ["Русский (Воскресенье)"] = "Russian (Воскресенье)",
        ["24-часовой формат"] = "24-hour time",
        ["Показывать секунды"] = "Show seconds",
        ["Тень под текстом"] = "Text shadow",
        ["Цвет часов:"] = "Clock color:",
        ["Градиент:"] = "Gradient:",
        ["Первый цвет"] = "First color",
        ["Второй цвет"] = "Second color",
        ["угол"] = "angle",
        ["Готовые градиенты:"] = "Preset gradients:",
        ["ПОГОДА"] = "WEATHER",
        ["Сохранить город"] = "Save city",
        ["Например: Tashkent, Ташкент, Moscow"] = "For example: Tashkent, London, New York",
        ["Данные: Open-Meteo (бесплатно, без ключа). Обновление раз в 30 минут."] = "Data: Open-Meteo (free, no key). Updates every 30 minutes.",
        ["СТЕКЛО ВИДЖЕТОВ И ПАПОК"] = "GLASS OF WIDGETS & FOLDERS",
        ["Матовое стекло (размытые обои, как на iPhone)"] = "Frosted glass (blurred wallpaper, like iPhone)",
        ["Тонировка"] = "Tinted",
        ["Только контур"] = "Outline only",
        ["Тёмный оттенок"] = "Dark tint",
        ["Светлый оттенок"] = "Light tint",
        ["Размытие"] = "Blur",
        ["Затемнение / осветление"] = "Tint strength",
        ["Скругление углов"] = "Corner radius",
        ["Размер виджетов"] = "Widget size",
        ["Вернуть виджеты на места по умолчанию"] = "Move widgets back to default places",
        ["Рабочий стол и папки"] = "Desktop & folders",
        ["Скрыть обычные значки рабочего стола (останутся только виджеты и папки)"] = "Hide regular desktop icons (only widgets and folders stay)",
        ["Новые ярлыки на рабочем столе сами попадают в подходящую папку"] = "New items on the desktop go into the right folder automatically",
        ["Открывать папки:"] = "Open folders:",
        ["кликом"] = "on click",
        ["при наведении"] = "on hover",
        ["Разложить всё по папкам заново"] = "Re-sort everything into folders",
        ["Новая папка"] = "New folder",
        ["Файлы ярлыков никуда не перемещаются и не удаляются — папки только показывают их красиво. При выходе из Wallflow обычные значки возвращаются. Ещё можно перетащить любой файл прямо на папку-виджет."] =
            "Files are never moved or deleted — folders only show them nicely. When Wallflow exits, regular icons come back. You can also drop any file right onto a folder widget.",
        ["Удалить папку (ярлыки уйдут в «Другое»)"] = "Delete folder (items move to “Other”)",
        ["Переместить выбранные в:"] = "Move selected to:",
        ["Убрать из папок (файл останется)"] = "Remove from folders (the file stays)",
        ["Обои"] = "Wallpapers",
        ["Как размещать картинку"] = "Picture position",
        ["Заполнение"] = "Fill", ["По размеру"] = "Fit", ["Растянуть"] = "Stretch", ["По центру"] = "Center", ["Замостить"] = "Tile",
        ["Расширение (на несколько мониторов)"] = "Span (multiple monitors)",
        ["Папка для скачанных обоев"] = "Folder for downloaded wallpapers",
        ["Ключ API Wallhaven (необязательно)"] = "Wallhaven API key (optional)",
        ["ВИДЕО-ОБОИ ИЗ СВОЕГО ФАЙЛА"] = "VIDEO WALLPAPER FROM YOUR FILE",
        ["Выбрать видео…"] = "Choose a video…",
        ["Без звука"] = "Muted",
        ["Пауза, когда окно на весь экран"] = "Pause when a window is maximized",
        ["Надёжный режим (прячет значки)"] = "Reliable mode (hides icons)",
        ["MP4/MOV/WMV. Видео играет на рабочем столе и встаёт на паузу, когда его не видно. Если на Windows 11 24H2 видео не появилось — включи надёжный режим."] =
            "MP4/MOV/WMV. The video plays on the desktop and pauses when it can't be seen. If it doesn't show up on Windows 11 24H2, turn on reliable mode.",
        ["СЛАЙДШОУ — АВТОСМЕНА ОБОЕВ"] = "SLIDESHOW — CHANGE WALLPAPER AUTOMATICALLY",
        ["Каждые"] = "Every",
        ["минут, источник:"] = "minutes, from:",
        ["Wallhaven с последними фильтрами"] = "Wallhaven with your last filters",
        ["Bing — обои дня"] = "Bing — wallpaper of the day",
        ["Сменить сейчас"] = "Change now",
        ["Общее"] = "General",
        ["Запускать вместе с Windows (сразу в трей)"] = "Start with Windows (straight to the tray)",
        ["ПОТРЕБЛЕНИЕ"] = "RESOURCE USAGE",
        ["Освободить память сейчас"] = "Free memory now",
        ["Wallflow — один нативный процесс без браузерного движка. Галерея и это окно создаются только когда открыты и полностью выгружаются после закрытия. Часы просыпаются раз в минуту, погода — раз в 30 минут, календарь — в полночь. В фоне процессор ≈ 0%."] =
            "Wallflow is one native process with no browser engine. The gallery and this window exist only while open and are fully unloaded when closed. The clock wakes once a minute, weather every 30 minutes, the calendar at midnight. Idle CPU ≈ 0%.",
        ["Шрифты: Bebas Neue, Great Vibes, Orbitron, Unbounded, Monoton, Major Mono Display, Comfortaa, Poiret One — SIL Open Font License. Обои: Wallhaven, Bing. Погода: Open-Meteo."] =
            "Fonts: Bebas Neue, Great Vibes, Orbitron, Unbounded, Monoton, Major Mono Display, Comfortaa, Poiret One — SIL Open Font License. Wallpapers: Wallhaven, Bing. Weather: Open-Meteo.",
        ["ЯЗЫК ИНТЕРФЕЙСА"] = "INTERFACE LANGUAGE",
        ["Сейчас: {0}"] = "Now: {0}",
        ["▶ Сейчас: "] = "▶ Now: ",
        ["Видео для обоев"] = "Video for the wallpaper",
        ["Свой цвет…"] = "Custom color…",
        ["Цвет часов"] = "Clock color",
        ["Первый цвет градиента"] = "First gradient color",
        ["Второй цвет градиента"] = "Second gradient color",
        ["Океан"] = "Ocean", ["Аврора"] = "Aurora", ["Золото"] = "Gold", ["Лаванда"] = "Lavender", ["Огонь"] = "Fire", ["Мята"] = "Mint",
        ["Заметки появляются на рабочем столе; удалить — крестик на самой заметке."] = "Notes appear on the desktop; delete one with the ✕ on the note itself.",
        ["Заметок на рабочем столе: {0}"] = "Notes on the desktop: {0}",
        ["Ищу…"] = "Searching…",
        ["Сейчас: {0} ({1:0.00}, {2:0.00})"] = "Now: {0} ({1:0.00}, {2:0.00})",
        ["Папки соберутся заново по категориям. Ручные перестановки сбросятся. Продолжить?"] = "Folders will be rebuilt by category. Manual changes will be lost. Continue?",
        ["Название новой папки"] = "New folder name",
        ["Название папки"] = "Folder name",
        ["Куда сохранять обои"] = "Where to save wallpapers",
        ["Автозапуск"] = "Autostart",
        ["Сейчас Wallflow занимает {0:0} МБ RAM (с открытым окном настроек; в трее — меньше). Приватная память: {1:0} МБ."] =
            "Wallflow is using {0:0} MB of RAM right now (with this window open; less in the tray). Private memory: {1:0} MB.",

        // ---- clock faces & colors
        ["Постер"] = "Poster", ["Контур"] = "Outline", ["Каллиграфия"] = "Script", ["Градиент"] = "Gradient", ["Стопка"] = "Stack",
        ["Как на iPhone"] = "iPhone style", ["Стрелочные"] = "Analog", ["Словами"] = "In words", ["Жирный"] = "Bold", ["Ретро"] = "Retro",
        ["Моно"] = "Mono", ["Мягкий"] = "Soft", ["Тонкий"] = "Thin", ["Минимум"] = "Minimal",
        ["Белый"] = "White", ["Розовый"] = "Pink", ["Голубой"] = "Cyan", ["Фиолетовый"] = "Violet", ["Золотой"] = "Gold", ["Мятный"] = "Mint", ["Красный"] = "Red",

        // ---- folders
        ["Браузеры"] = "Browsers", ["Общение"] = "Chat", ["Медиа"] = "Media", ["Творчество"] = "Creative", ["Разработка"] = "Dev",
        ["Офис"] = "Office", ["Утилиты"] = "Utilities", ["Другое"] = "Other", ["Документы"] = "Documents", ["Картинки"] = "Pictures",
        ["Скриншоты"] = "Screenshots", ["Видео"] = "Videos", ["Архивы"] = "Archives", ["Установщики"] = "Installers", ["Код"] = "Code",
        ["Торренты"] = "Torrents", ["Папки"] = "Folders", ["Файлы"] = "Files", ["Папка"] = "Folder",
        ["Переставить виджеты"] = "Arrange widgets",
        ["Настройки папок…"] = "Folder settings…",
        ["Не удалось запустить"] = "Couldn't open",
        ["Открыть расположение"] = "Show in Explorer",
        ["Переместить в"] = "Move to",
        ["Новая папка…"] = "New folder…",
        ["Убрать из папки (ярлык останется на диске)"] = "Remove from folder (the file stays on disk)",

        // ---- notes / to-do / player / weather
        ["Мелкий"] = "Small", ["Обычный"] = "Normal", ["Крупный"] = "Large", ["Очень крупный"] = "Extra large", ["Огромный"] = "Huge",
        ["Узкая"] = "Narrow", ["Обычная"] = "Normal", ["Широкая"] = "Wide", ["Очень широкая"] = "Extra wide",
        ["Текст меньше"] = "Smaller text", ["Текст больше"] = "Bigger text",
        ["Удалить заметку"] = "Delete note", ["Сменить цвет"] = "Change color", ["Заголовок"] = "Title", ["Текст заметки…"] = "Note text…",
        ["Размер текста"] = "Text size", ["Ширина"] = "Width", ["Удалить эту заметку?"] = "Delete this note?",
        ["Ничего не играет"] = "Nothing playing", ["Включи музыку или видео"] = "Play some music or a video",
        ["Медиа-управление Windows недоступно"] = "Windows media controls are unavailable", ["Без названия"] = "Untitled", ["МЕДИАПЛЕЕР"] = "MEDIA PLAYER",
        ["Настройки списка"] = "List options", ["Расстояние между пунктами"] = "Spacing",
        ["Плотно"] = "Compact", ["Обычно"] = "Normal", ["Свободно"] = "Relaxed", ["Просторно"] = "Spacious",
        ["Выполненные опускать вниз"] = "Move done items to the bottom", ["Удалить выполненные"] = "Delete done items",
        ["Переименовать список…"] = "Rename list…", ["Название списка"] = "List name",
        ["Без звука (клик)"] = "Mute (click)", ["звук"] = "app", ["система"] = "system",
        ["Погода недоступна\n"] = "Weather unavailable\n",
        ["Windows не принял обои (код {0})"] = "Windows rejected the wallpaper (code {0})",
    };
}

# Content of the Wallflow user guide. Each section: heading, body blocks, optional screenshot.
# Body block: ("p", text) paragraph, ("ul", [items]) bullet list, ("tip", text) highlighted note.

RU = {
    "title": "Wallflow",
    "subtitle": "Инструкция пользователя",
    "lead": "Виджеты на стекле, папки с ярлыками как на iPhone и обои в 2K и 4K — в одном лёгком приложении для Windows 10 и 11.",
    "toc": "Содержание",
    "fig": "Рис.",
    "footer": "Wallflow — инструкция",
    "sections": [
        ("Установка и первый запуск", [
            ("ul", [
                "Скачай архив <b>Wallflow-windows-x64</b>: страница репозитория на GitHub → вкладка <b>Actions</b> → последний зелёный запуск → раздел <b>Artifacts</b>.",
                "Внутри три файла. Возьми <b>Wallflow.exe</b> — в нём всё нужное и он быстрее всех запускается. <b>Wallflow-compact.exe</b> — то же, но меньше по размеру и стартует на секунду дольше. <b>Wallflow-lite.exe</b> требует установленный .NET 8 Desktop Runtime.",
                "Положи exe в постоянную папку (например, «Документы\\Wallflow») и запусти. Автозапуск запомнит именно это место.",
                "При первом запуске откроются настройки, а на рабочем столе появятся виджеты и папки с ярлыками. Обычные значки рабочего стола прячутся — их заменяют папки.",
            ]),
            ("tip", "Wallflow сам запускается при входе в Windows (через Планировщик заданий — сразу, без задержки). Отключить можно в настройках → «Общее»."),
        ], None),
        ("Рабочий стол", [
            ("p", "Слева — часы с днём недели, под ними стеклянные папки с ярлыками. Справа — погода, календарь, плеер, список дел и заметки. Размер виджетов подбирается под экран: на ноутбуке они меньше, на большом мониторе — крупнее."),
            ("ul", [
                "Стекло виджетов — это размытые обои под ними, как на iPhone.",
                "Виджеты живут на рабочем столе: не мешают окнам и остаются на месте после «Свернуть все» (Win+D).",
                "Для каждого экрана (ноутбук, монитор) запоминается своя расстановка.",
            ]),
        ], "01-desktop"),
        ("Как переставить виджеты", [
            ("p", "Значок Wallflow в трее → <b>«Переставить виджеты»</b>. Вокруг виджетов появится пунктирная рамка — перетаскивай их мышкой. Когда закончишь, сними галочку в том же меню."),
            ("tip", "Зажми <b>Shift</b> во время перетаскивания — виджет прилипнет к краям и центрам соседних виджетов и к краям экрана. Так легко выровнять всё ровно."),
            ("p", "Вернуть всё на места по умолчанию: настройки → «Виджеты» → «Вернуть виджеты на места по умолчанию»."),
        ], "02-edit"),
        ("Папки с ярлыками", [
            ("p", "Ярлыки программ с рабочего стола сами раскладываются по папкам: Игры, Браузеры, Общение, Медиа, Творчество, Разработка, Офис, Утилиты, Другое. Новые ярлыки тоже сами попадают в нужную папку."),
            ("ul", [
                "Клик по папке открывает её. В настройках можно включить открытие при наведении мыши.",
                "Правый клик по программе в открытой папке: открыть расположение файла, переместить в другую папку, убрать из папки.",
                "Правый клик по самой папке: переименовать.",
                "Любой файл можно перетащить прямо на папку-виджет.",
            ]),
            ("tip", "Файлы ярлыков никуда не перемещаются и не удаляются — папки только красиво их показывают."),
        ], "03-folder"),
        ("Заметки и список дел", [
            ("p", "Заметка: клик — и пиши. У каждой есть заголовок и цветная полоска (клик по ней меняет цвет). При наведении появляются «A−» и «A+» — размер текста. Правый клик: размер текста, ширина, удаление."),
            ("p", "Список дел: впиши задачу и нажми Enter, клик по пункту — отметить выполненным. Кнопка «⋯» открывает настройки: размер текста, расстояние между пунктами, опускать ли выполненные вниз, переименовать список, удалить выполненные."),
            ("p", "Новая заметка: трей → «Виджеты» → «Новая заметка» или настройки → «Добавить заметку»."),
        ], "04-todo-menu"),
        ("Значок в трее", [
            ("p", "Всё самое нужное — в меню значка Wallflow рядом с часами Windows:"),
            ("ul", [
                "<b>Обои…</b> — галерея обоев; <b>Следующие обои</b> и <b>Слайдшоу</b>.",
                "<b>Переставить виджеты</b> и выбор, какие виджеты показывать.",
                "<b>Скрыть обычные значки рабочего стола</b> — сними галочку, чтобы вернуть значки.",
                "<b>Разложить ярлыки по папкам заново</b>, <b>Видео-обои</b>, <b>Настройки</b>, автозапуск и выход.",
            ]),
        ], "05-tray"),
        ("Настройки: виджеты и стили часов", [
            ("p", "Галочками включаются виджеты: часы, погода, календарь, папки, плеер, список дел. Ниже — 15 стилей часов: клик по карточке сразу применяет стиль."),
        ], "06-settings-widgets"),
        ("Цвет часов, градиент и погода", [
            ("ul", [
                "Язык дней и месяцев (Sunday / Воскресенье), 24-часовой формат, секунды, тень под текстом.",
                "Цвет часов: готовые цвета или любой свой через палитру (радужный кружок).",
                "Градиент: два цвета и угол, плюс готовые наборы — закат, неон, океан и другие.",
                "Погода: впиши город и нажми «Сохранить город». Данные Open-Meteo, бесплатно и без ключа.",
            ]),
        ], "07-settings-colors"),
        ("Стекло и размер", [
            ("ul", [
                "<b>Матовое стекло</b> (размытые обои), <b>Тонировка</b> или <b>Только контур</b>; тёмный или светлый оттенок.",
                "Ползунки: размытие, затемнение, скругление углов, размер виджетов.",
                "<b>«Подбирать размер под экран»</b> — размер сам подстраивается под ноутбук или монитор. Если подвинешь ползунок размера, автоподбор выключится.",
            ]),
        ], "08-settings-glass"),
        ("Палитра", [
            ("p", "Квадрат — насыщенность и яркость, полоска справа — оттенок. Можно вписать HEX-код или выбрать готовый цвет из ряда снизу."),
        ], "09-color-picker"),
        ("Настройки: рабочий стол и папки", [
            ("ul", [
                "Скрыть обычные значки рабочего стола (по умолчанию включено). При выходе из Wallflow значки возвращаются.",
                "Новые ярлыки сами попадают в подходящую папку.",
                "Открывать папки кликом или при наведении.",
                "Редактор папок: переименовать, поменять порядок, удалить, перенести выбранные ярлыки в другую папку.",
            ]),
        ], "10-settings-desktop"),
        ("Настройки: обои, слайдшоу и видео", [
            ("ul", [
                "Как размещать картинку: заполнение, по размеру, растянуть, по центру, замостить, на несколько мониторов.",
                "Папка для скачанных обоев и ключ API Wallhaven (необязательно).",
                "Видео-обои из своего файла (MP4, MOV, WMV): пауза, когда окно на весь экран, без звука, надёжный режим для Windows 11 24H2.",
                "Слайдшоу: смена обоев каждые N минут из избранного, Wallhaven или Bing.",
            ]),
        ], "11-settings-wallpapers"),
        ("Настройки: общее", [
            ("ul", [
                "Автозапуск вместе с Windows.",
                "<b>Язык интерфейса</b>: русский или английский — меняется сразу, без перезапуска.",
                "Сколько памяти занимает Wallflow и кнопка «Освободить память».",
            ]),
        ], "12-settings-general"),
        ("Галерея обоев", [
            ("p", "Трей → «Обои…». Обои не хранятся в приложении — они подгружаются из Wallhaven и Bing."),
            ("ul", [
                "<b>Разрешение</b>: «Не меньше» (2K, 4K, 5K, 8K…) или «Точно»; кнопка «Под мой экран».",
                "<b>Соотношение сторон</b>, <b>цвет</b>, <b>темы</b> (природа, космос, города, машины, игры, киберпанк, минимализм и ещё три десятка), категории и сортировка.",
                "Поиск по словам, по тегу (#mountains) и похожие обои (like:id).",
                "Вкладки: Обзор, Bing дня (4K), Избранное.",
            ]),
        ], "13-gallery"),
        ("Просмотр и установка", [
            ("p", "Клик по обоям открывает просмотр: разрешение, размер, цвета (клик по цвету — искать в нём), теги. Кнопки: <b>Установить на рабочий стол</b>, В избранное, Скачать, Похожие, Открыть источник."),
        ], "14-preview"),
        ("Плеер", [
            ("p", "Показывает то, что сейчас играет в любом приложении: Spotify, Chrome и другие браузеры, Яндекс Музыка, Telegram. Есть обложка, прогресс, кнопки «назад / пауза / вперёд» и громкость."),
            ("ul", [
                "Громкость меняет звук именно играющего приложения (как в микшере Windows), а если его не найти — общую.",
                "Колёсико мыши над плеером — громкость, клик по динамику — без звука.",
            ]),
        ], None),
        ("Вопросы и советы", [
            ("ul", [
                "<b>Пропали значки рабочего стола.</b> Они скрыты нарочно — трей → снять «Скрыть обычные значки рабочего стола». Файлы на месте, их видно в Проводнике → «Рабочий стол».",
                "<b>Виджеты налезают друг на друга.</b> Настройки → «Вернуть виджеты на места по умолчанию» или включи «Подбирать размер под экран».",
                "<b>Стекло тёмное.</b> Сдвинь «Затемнение / осветление» левее или выбери светлый оттенок.",
                "<b>Видео-обои не видно на Windows 11 24H2.</b> Включи «Надёжный режим».",
                "<b>Где настройки.</b> Папка %AppData%\\Wallflow: settings.json, журнал ошибок error.log и время запуска startup.log.",
                "<b>Как удалить.</b> Выключи автозапуск в настройках, выйди из Wallflow через трей, удали exe и папку %AppData%\\Wallflow.",
            ]),
        ], None),
    ],
}

EN = {
    "title": "Wallflow",
    "subtitle": "User guide",
    "lead": "Glass widgets, iPhone-style shortcut folders and 2K/4K wallpapers — in one lightweight app for Windows 10 and 11.",
    "toc": "Contents",
    "fig": "Fig.",
    "footer": "Wallflow — user guide",
    "sections": [
        ("Installation and first start", [
            ("ul", [
                "Download the <b>Wallflow-windows-x64</b> archive: the GitHub repository → <b>Actions</b> tab → latest green run → <b>Artifacts</b>.",
                "There are three files. Take <b>Wallflow.exe</b> — it contains everything and starts the fastest. <b>Wallflow-compact.exe</b> is the same but smaller and starts a second slower. <b>Wallflow-lite.exe</b> needs the .NET 8 Desktop Runtime installed.",
                "Put the exe into a permanent folder (for example “Documents\\Wallflow”) and run it. Autostart remembers exactly this location.",
                "On the first start the settings open, and widgets and shortcut folders appear on the desktop. Regular desktop icons are hidden — the folders replace them.",
            ]),
            ("tip", "Wallflow starts when you sign in to Windows (via Task Scheduler — immediately, without delay). You can turn this off in Settings → General."),
        ], None),
        ("The desktop", [
            ("p", "On the left — the clock with the weekday and the glass shortcut folders under it. On the right — weather, calendar, player, to-do list and notes. Widget size adapts to the screen: smaller on a laptop, larger on a big monitor."),
            ("ul", [
                "The widget glass is your wallpaper blurred underneath, like on an iPhone.",
                "Widgets live on the desktop: they stay below your windows and survive “Show desktop” (Win+D).",
                "Each screen setup (laptop, monitor) keeps its own arrangement.",
            ]),
        ], "01-desktop"),
        ("Moving widgets", [
            ("p", "Wallflow tray icon → <b>“Move widgets”</b>. A dashed frame appears around the widgets — drag them with the mouse. When you're done, uncheck the same menu item."),
            ("tip", "Hold <b>Shift</b> while dragging — the widget snaps to the edges and centres of other widgets and to the screen edges. Perfect alignment in a second."),
            ("p", "To put everything back: Settings → Widgets → “Put widgets back to default places”."),
        ], "02-edit"),
        ("Shortcut folders", [
            ("p", "Program shortcuts from the desktop are sorted into folders automatically: Games, Browsers, Chat, Media, Creative, Development, Office, Utilities, Other. New shortcuts land in the right folder by themselves."),
            ("ul", [
                "Click a folder to open it. Opening on mouse hover can be turned on in the settings.",
                "Right-click a program in an open folder: open file location, move to another folder, remove from the folder.",
                "Right-click the folder itself: rename.",
                "You can drop any file straight onto a folder widget.",
            ]),
            ("tip", "Shortcut files are never moved or deleted — folders only display them nicely."),
        ], "03-folder"),
        ("Notes and to-do list", [
            ("p", "Note: click and type. Each note has a title and a colour strip (click it to change the colour). On hover, “A−” and “A+” change the text size. Right-click: text size, width, delete."),
            ("p", "To-do list: type a task and press Enter, click a task to mark it done. The “⋯” button opens the options: text size, spacing, whether done tasks move to the bottom, rename the list, delete done tasks."),
            ("p", "New note: tray → Widgets → New note, or Settings → Add note."),
        ], "04-todo-menu"),
        ("Tray icon", [
            ("p", "Everything important is in the menu of the Wallflow icon next to the Windows clock:"),
            ("ul", [
                "<b>Wallpapers…</b> — the gallery; <b>Next wallpaper</b> and <b>Slideshow</b>.",
                "<b>Move widgets</b> and which widgets to show.",
                "<b>Hide regular desktop icons</b> — uncheck to get the icons back.",
                "<b>Sort shortcuts into folders again</b>, <b>Video wallpaper</b>, <b>Settings</b>, autostart and exit.",
            ]),
        ], "05-tray"),
        ("Settings: widgets and clock styles", [
            ("p", "Checkboxes turn widgets on and off: clock, weather, calendar, folders, player, to-do list. Below are 15 clock styles — click a card to apply it."),
        ], "06-settings-widgets"),
        ("Clock colour, gradient and weather", [
            ("ul", [
                "Day and month names language (Sunday / Воскресенье), 24-hour time, seconds, text shadow.",
                "Clock colour: preset colours or any colour from the palette (rainbow circle).",
                "Gradient: two colours and an angle, plus presets — sunset, neon, ocean and more.",
                "Weather: type a city and press “Save city”. Data from Open-Meteo, free and without a key.",
            ]),
        ], "07-settings-colors"),
        ("Glass and size", [
            ("ul", [
                "<b>Frosted glass</b> (blurred wallpaper), <b>Tinted</b> or <b>Outline only</b>; dark or light tint.",
                "Sliders: blur, tint strength, corner radius, widget size.",
                "<b>“Fit size to the screen”</b> — size follows the laptop or monitor automatically. Moving the size slider turns it off.",
            ]),
        ], "08-settings-glass"),
        ("Colour picker", [
            ("p", "The square sets saturation and brightness, the strip on the right sets the hue. You can also type a HEX code or pick one of the swatches below."),
        ], "09-color-picker"),
        ("Settings: desktop and folders", [
            ("ul", [
                "Hide regular desktop icons (on by default). The icons come back when Wallflow exits.",
                "New shortcuts go into the right folder automatically.",
                "Open folders on click or on hover.",
                "Folder editor: rename, reorder, delete, move selected shortcuts to another folder.",
            ]),
        ], "10-settings-desktop"),
        ("Settings: wallpapers, slideshow and video", [
            ("ul", [
                "Picture position: fill, fit, stretch, center, tile, span across monitors.",
                "Folder for downloaded wallpapers and an optional Wallhaven API key.",
                "Video wallpaper from your own file (MP4, MOV, WMV): pause when a window covers the screen, mute, reliable mode for Windows 11 24H2.",
                "Slideshow: change the wallpaper every N minutes from favorites, Wallhaven or Bing.",
            ]),
        ], "11-settings-wallpapers"),
        ("Settings: general", [
            ("ul", [
                "Start with Windows.",
                "<b>Interface language</b>: Russian or English — switches immediately, no restart.",
                "How much memory Wallflow uses and a “Free memory now” button.",
            ]),
        ], "12-settings-general"),
        ("Wallpaper gallery", [
            ("p", "Tray → Wallpapers…. Wallpapers aren't stored in the app — they're loaded from Wallhaven and Bing."),
            ("ul", [
                "<b>Resolution</b>: “At least” (2K, 4K, 5K, 8K…) or “Exactly”; “Match my screen” button.",
                "<b>Aspect ratio</b>, <b>colour</b>, <b>topics</b> (nature, space, cities, cars, games, cyberpunk, minimalism and some thirty more), categories and sorting.",
                "Search by words, by tag (#mountains) and similar wallpapers (like:id).",
                "Tabs: Browse, Bing daily (4K), Favorites.",
            ]),
        ], "13-gallery"),
        ("Preview and setting a wallpaper", [
            ("p", "Click a wallpaper to open the preview: resolution, size, colours (click a colour to search in it), tags. Buttons: <b>Set as wallpaper</b>, Add to favorites, Download, Similar, Open source page."),
        ], "14-preview"),
        ("Player", [
            ("p", "Shows what is playing in any app: Spotify, Chrome and other browsers, Yandex Music, Telegram. Cover art, progress, previous / pause / next buttons and volume."),
            ("ul", [
                "The volume slider changes the volume of the playing app (like the Windows volume mixer), or the system volume if the app can't be found.",
                "Mouse wheel over the player changes the volume, a click on the speaker mutes.",
            ]),
        ], None),
        ("Questions and tips", [
            ("ul", [
                "<b>My desktop icons are gone.</b> They are hidden on purpose — tray → uncheck “Hide regular desktop icons”. Files are still there; you can see them in File Explorer → Desktop.",
                "<b>Widgets overlap.</b> Settings → “Put widgets back to default places”, or turn on “Fit size to the screen”.",
                "<b>The glass is too dark.</b> Move “Tint strength” to the left or choose the light tint.",
                "<b>Video wallpaper isn't visible on Windows 11 24H2.</b> Turn on “Reliable mode”.",
                "<b>Where are the settings?</b> In %AppData%\\Wallflow: settings.json, the error log error.log and the startup time log startup.log.",
                "<b>How to uninstall.</b> Turn off autostart in the settings, exit Wallflow from the tray, delete the exe and the %AppData%\\Wallflow folder.",
            ]),
        ], None),
    ],
}

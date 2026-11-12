# EasyUI 0.2.0

Окна, модальная навигация, диалоги и круговое меню для **Unity 6.3**, **uGUI 2.0.0** и **Input System 1.20.0**. Один `WindowManager` управляет UI игрока; `UIWindow` находится на префабе окна. Базовый сценарий работает самостоятельно. Дополнительные Easy-пакеты подключаются импортом отдельных samples.

## Установка и первый запуск

1. В **Edit → Project Settings → Player → Other Settings → Active Input Handling** выберите **Input System Package (New)**. Если Unity предложит перезапуск, выполните его.
2. В **Window → Package Management → Package Manager → + → Install package from disk…** выберите `package.json` EasyUI. В Games Factory пакет уже Embedded. Git-адрес после публикации этой версии: `https://github.com/orEEtheHAT/EasyUI.git#v0.2.0`.
3. Откройте **EasyUI → Samples → Getting Started → Import**.
4. Откройте `Assets/Samples/EasyUI/0.2.0/Getting Started/GettingStarted.unity` и нажмите **Play**. Щёлкните Game, чтобы получать обычный физический ввод.
5. **I / Start** открывает инвентарь. Выберите **Settings → Apply settings**. В диалоге исходно выделена **Cancel**. **Стрелки / D-pad / левый стик** перемещают выделение; **Enter / A** подтверждает; **Esc / B** возвращает на один уровень.
6. **Tab / RB** открывает или закрывает круговое меню. **Удержание R / LB** открывает его, отпускание подтверждает выбранный пункт. Мышь или левый стик выбирает направление; **ЛКМ / Enter / A** подтверждает. **Esc / B** отменяет. Центр и недоступный сектор не выбирают пункт.
7. **Space** увеличивает счётчик игрового действия, когда меню закрыто. Пока окно удерживает запрос, счётчик не меняется. После закрытия дождитесь отпускания кнопки: этот же ввод не должен попасть в игру.

Ожидается постоянный HUD, до трёх вложенных окон и счётчик запросов ввода. Закрытие диалога оставляет запросы настроек и инвентаря; закрытие настроек возвращает выделение на кнопку родителя. После последнего окна управление возвращается игре. Кнопка **Choose with radial menu** показывает круговое меню как дочернее окно.

**Tools → EasyUI → Create Getting Started Scene** создаёт отдельную редактируемую копию примера в `Assets/EasyUI Example`, сохраняя текущую открытую сцену. Затем откройте созданный `GettingStarted.unity`.

## Собственное окно

1. **GameObject → EasyUI → Window Manager** создаёт UI игрока. EventSystem и наследник `InputSystemUIInputModule` создаются при первом открытии. Существующий EventSystem можно назначить явно.
2. **GameObject → EasyUI → Window** создаёт панель. Поместите внутрь обычные uGUI-кнопки. Назначьте **First Selected**, либо оставьте пустым для первого доступного элемента. Сохраните панель как префаб, затем удалите её исходный объект из сцены. Для варианта Open(sceneWindow) вместо удаления оставьте объект сцены выключенным.
3. **Create → EasyUI → Window Definition**: назначьте этот префаб. Настройки определения применяются к каждому открытому экземпляру; SO не хранит runtime-состояние.
4. Откройте его из кода, либо добавьте на кнопку **EasyUI → Open Window Button** и назначьте определение. Кнопка внутри окна автоматически использует его менеджер и открывает дочернее окно. Кнопке снаружи назначьте менеджер явно. Для закрытия подключите `UIWindow.Close` к обычному `Button.onClick`.

```csharp
using EasyUI;

WindowHandle inventory = ui.Open(inventoryDefinition);
WindowHandle settings = ui.Open(settingsDefinition, inventory);
settings.Close();        // Только дочернее окно и его запрос.
inventory.Close();       // Окно и все оставшиеся дочерние окна.
inventory.Close();       // Безопасно повторять.
ui.Back();               // Текущий получатель обрабатывает Back один раз за кадр.
```

Для открытия из InputAction передайте `openingAction: action`: переход дождётся отпускания этой кнопки. Для удерживаемого кругового меню открывающую action не передают в этот барьер, чтобы меню оставалось доступным во время удержания.

`ui.Open(sceneWindow)` использует существующее окно сцены и выключает его при закрытии. `ui.Open(prefab)` / `ui.Open(definition)` создаёт экземпляр и уничтожает его при закрытии. Повторный Open того же определения/окна с тем же родителем возвращает прежний handle без перестановки и повторных событий. Open во время затухания отменяет закрытие того же экземпляра. Сменить родителя уже открытого окна нельзя: сначала закройте его.

## Политики и состояние

| Сценарий | Accepts Input | Blocks Lower Windows | Close On Back |
|---|---|---|---|
| Модальное меню/диалог | Да | Да | Да |
| Обычная панель | Да | Нет | По задаче |
| Пассивный HUD/подсказка | Нет | Нет | Нет |

Навигацию получает верхнее принимающее ввод окно. Модальный полноэкранный экран перехватывает клики за его границами. Обычные панели допускают клики по нижним панелям, но клавиатурное выделение остаётся в активном окне. Удалённый или недоступный элемент выделения заменяется **First Selected** или первым доступным Selectable. После изменения динамического набора элементов вызовите `window.RefreshNavigation()`.

**Block Gameplay** и **Cursor** по умолчанию не меняют игру. Если они требуют вмешательства, назначьте Input Provider на менеджере. Отсутствие провайдера возвращает понятную ошибку до создания окна. `TryOpen(definition, out handle, out reason)` позволяет показать её своим UI.

Для частичного ограничения оставьте **Block Gameplay** выключенным и заполните **Blocked Actions**, например `Gameplay/Look`: окно запретит обзор, сохраняя `Gameplay/Move`. Эту возможность поддерживает EasyInput-провайдер; Standalone сообщает, что требуется провайдер именованных actions. Ограничения окон объединяются, закрытие вне очереди снимает только свой список. Для пользовательских карт задайте **Gameplay Maps** и **UI Map** на адаптере. `InputContextRequest.UI()`/`Building()` в EasyInput предназначены для стандартных карт; адаптер EasyUI создаёт настраиваемый запрос напрямую.

`WindowHandle.State`: Opening → Open → Closing → Closed. `IsOpen`, `IsVisible`, `IsActive`, `Requirements`, `AcceptsInput`, `BlocksLowerWindows`, `ClosesOnBack` доступны для игровой логики. Политика копируется при открытии; изменения SO не влияют на существующее окно. `WindowManager.InputReady` сообщает, завершился ли барьер отпускания ввода.

События: `WindowManager.WindowOpened`, `WindowClosed`, `ActiveWindowChanged`; `UIWindow.Opened`, `Closed` и инспекторные **On Opened / On Closed**; `handle.Closed`. Opened означает начало показа, Closed — завершение закрытия. Запрос остаётся у окна до окончания Fade Out. Fade использует unscaled time и работает при `Time.timeScale = 0`. Состояние изменяется до вызова событий; подписчик может открыть или закрыть другое окно. Ошибка одного C#-подписчика не препятствует остальным.

При отключении/уничтожении окна, менеджера или его сцены запросы освобождаются. Дочерние окна закрываются вместе с родителем. Менеджер не использует `DontDestroyOnLoad`: для UI между сценами сохраняйте весь его объект самостоятельно. Один EventSystem должен принадлежать одному менеджеру; для нескольких игроков назначайте отдельные системы событий и UI actions.

## Ввод, диалоги, круговое меню

Для автономного UI добавьте **Standalone Input Provider** на менеджер. В **Gameplay Actions** назначьте игровые actions, которыми этот провайдер должен управлять, либо **Gameplay Gates** с реализациями `IWindowGameplayGate`. Не назначайте туда UI actions. Каждый запрос владеет своим handle. Провайдер сохраняет исходные enabled-состояния и курсор, восстанавливая их после последнего запроса и отпускания ввода. Для нескольких систем ограничений используйте один общий провайдер/арбитр. [Контракт провайдера](Documentation~/InputProvider.md).

Для диалога добавьте **Confirmation Dialog**, назначьте текст и две кнопки, затем:

```csharp
var handle = ui.Open(confirmDefinition, parent);
var dialog = handle.Window.GetComponent<ConfirmationDialog>();
ConfirmationResult result = await dialog.Ask("Применить настройки?");
// Confirmed или Cancelled. Back, уничтожение и закрытие родителя дают Cancelled.
```

Для круга добавьте **Radial Menu** на окно. Пункты имеют ID, подпись, иконку, Available и On Selected. `SetItems(IEnumerable<RadialItem>)` обновляет состав. `SelectionChanged` и `Confirmed` позволяют обновлять описание и выполнять действие. Сектор 0 направлен вверх; порядок — по часовой стрелке. В центре/недоступном секторе `SelectedIndex == -1`. `SelectDirection` и `ConfirmSelection` поддерживают пользовательский источник направления. Компонент **Radial Menu Trigger** связывает определение с InputAction и режимом Press/Hold.

## Дополнительные интеграции

Импортируйте только нужные samples после установки их зависимостей. Базовая runtime-сборка на них не ссылается.

| Sample | Зависимости и настройка |
|---|---|
| **EasyInput** | EasyInput 0.2.0+. Назначьте **EasyInput Window Provider → Player** вместо Standalone. UI map по умолчанию `UI`, игровые карты `Gameplay`, `Building`; подстройте под свой asset. `InputSettingsWindow` на префабе окна создаёт uGUI-настройки, строки привязок, значки, чувствительность, deadzone, инверсию и выбор устройств. Capture/Restore работают со снимком в памяти; сохранение файла принадлежит игре. |
| **EasyLocalization** | EasyLocalization 0.2.0+ и Unity Localization 1.5.13. **Localized Window Text** на uGUI Text использует стандартный `LocalizedString` и `Localization.TextAsync`, обновляется при смене языка. Настройте таблицы в EasyLocalization. |
| **EasyFeedbacks** | EasyFeedbacks 1.8.0+. **Window Feedbacks** на окне или Selectable: пресеты Open, Select, Confirm. На Radial Menu подписывается на его события. Эффекты и музыка остаются ответственностью EasyFeedbacks. |
| **EasyBuilding** | EasyBuilding 0.3.0+. **Building Gameplay Gate** назначается Standalone-провайдеру и использует `BuildingPlacer.SetInputBlocked(owner, blocked)`. Чужие владельцы сохраняются. |
| **EasyResources** | EasyResources 0.3.0+. **Resource Balance Label** показывает доступный остаток; `Bind(container, resource)` меняет источник с отпиской от прежнего. Обновления объединяются в один LateUpdate по событиям. |
| **EasyInteraction** | EasyInteraction 0.3.0+. **Open Window Action** на объекте взаимодействия открывает определение окна. Назначьте менеджер, либо разместите его под `actor.PlayerRoot`. |

В EasyInput-провайдере каждое окно получает собственный `InputContextHandle`. UI actions берутся из runtime-asset игрока; EasyUI не сохраняет пользовательские настройки в SO. Во время переназначения UI защищён от кликов и подтверждений: **Esc / B** сначала отменяет переназначение, последующее отдельное нажатие возвращается по окнам. Компонент локализованной подсказки кнопки принадлежит sample **Localization** в EasyInput; используйте его `TextChanged` внутри окна, если нужна такая подсказка.

Escape зарезервирован самим EasyInput; также зарезервированы все кнопки текущего **UI/Cancel**; в стандартном asset это Escape и gamepad East (B / Circle). Адаптер слушает их независимо от enabled-состояния UI-модуля. Не добавляйте EasyInput `InputUIActions` на тот же EventSystem: EasyUI-провайдер уже назначает actions и защищает окно во время rebinding. `Hidden` означает свободный невидимый курсор, `Inherit` оставляет политику EasyInput; после последнего запроса применяется профиль игрока.

FPS-контроллер подключается через EasyInput к EasyMovement на ECM2; боевые actions принадлежат EasyCombat. EasyUI не реализует движение игрока или боевую логику. Локальная мастерская использует неподвижную обзорную камеру.

## Проверки и ограничения

Для тестов добавьте `com.oreethehat.easy-ui` в `testables` манифеста проекта и установите Unity Test Framework. Тесты адаптера EasyInput находятся внутри его sample. [Результаты проверок и профилирования](Documentation~/Validation.md).

Пакет ориентирован на Screen Space Overlay, uGUI Selectable, один UI-стек на EventSystem и локальную игру. Встроенная анимация — прозрачность CanvasGroup. Сложные переходы, world-space/VR UI, сохранение стека окон и пул префабов пока не предоставляются. Круговое меню рассчитано на компактный список; для больших каталогов используйте прокручиваемую панель. Скрытые окна сцены остаются в памяти до уничтожения владельцем; экземпляры префабов освобождаются при закрытии.

Локальные сцены Games Factory: `Assets/PackageTests/EasyUI/GettingStarted/GettingStarted.unity`, `Cooperation/Workshop.unity`, `Controls/Controls.unity`. Мастерская проверяет готовые пакеты и не входит в публичный sample. Данные обновляются по событиям; Jobs/Burst не используются, поскольку управление uGUI и вводом выполняется на главном потоке.

## Готовые окна интеграции

Sample **Integration Windows** добавляет фабрику префабов выбора действий и построек (список/круг), а также controls → audio с выбором языка. Он использует текущие контракты Interaction Input Source, общей BuildingSession и FeedbackSettings. [Пошаговая настройка, зависимости и точные API](Samples~/Integration%20Windows/README.md). Базовый сценарий не требует этих пакетов.

`RadialMenu.UpdateItem(id, label, available, icon)` меняет представление без пересоздания сектора; `SetItems` сохраняет выбранный ID при перестановке. Для прокручиваемых пользовательских списков добавьте `WindowScrollIntoView` к вертикальному ScrollRect: выделение геймпада останется видимым, ручная прокрутка не сбрасывается каждый кадр.

## Совместимость Samples

Samples этого выпуска требуют EasyUI **0.2.0+**. Core устанавливается без других Easy-пакетов. Установите зависимости только выбранного sample, затем импортируйте его один раз через Package Manager. В таблице указаны поддерживаемые минимумы этой версии адаптеров: у старых выпусков может не быть нужного API. Зависимости Unity из package.json устанавливаются автоматически.

| Sample | Дополнительные требования | Импортировать заранее |
|---|---|---|
| Getting Started | Нет | — |
| EasyBuilding | EasyBuilding 0.3.0+ | — |
| EasyResources | EasyResources 0.3.0+ | — |
| EasyInteraction | EasyInteraction 0.3.0+ | — |
| EasyLocalization | EasyLocalization 0.2.0+, Unity Localization 1.5.13+ | — |
| EasyFeedbacks | EasyFeedbacks 1.8.0+ | — |
| EasyInput | EasyInput 0.2.0+ | — |
| Integration Windows | EasyBuilding 0.3.0+, EasyFeedbacks 1.8.0+, EasyInput 0.2.0+, EasyInteraction 0.3.0+, EasyLocalization 0.2.0+, EasyResources 0.3.0+ | EasyBuilding / EasyResources; EasyUI / EasyBuilding; EasyUI / EasyFeedbacks; EasyUI / EasyInput; EasyUI / EasyInteraction; EasyUI / EasyResources |

Оставляйте одну копию каждой sample-сборки. Существующие импорты могут оставаться в прежней папке Assets: перед повторным импортом сверяйте изменения, сохраняя свои правки и GUID assets/actions.

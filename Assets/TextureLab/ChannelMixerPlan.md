# Texture Lab — план замены Channel Remap на Channel Mixer

## 1. Цель

Полностью заменить пользовательский эффект `Channel Remap` на управляемый художественный `Channel Mixer`, близкий по логике к Photoshop Channel Mixer.

Текущий remap позволяет только выбрать один источник для каждого выходного канала. Новый эффект должен смешивать несколько входных каналов с положительными и отрицательными весами:

```text
Output R = R × RR + G × RG + B × RB + Constant R
Output G = R × GR + G × GG + B × GB + Constant G
Output B = R × BR + G × BG + B × BB + Constant B
```

Это матрица 3×3: три выходных канала, каждый из которых получает отдельные вклады исходных R, G и B. Пользователю не нужно видеть математическую таблицу целиком — в узкой карточке эффекта она представляется тремя переключаемыми секциями `Red / Green / Blue`.

## 2. Принятые продуктовые решения

- Эффект в UI называется `Channel Mixer`.
- Значения вкладов показываются в процентах.
- Диапазон каждого вклада: `−200%…200%`.
- Диапазон `Constant`: `−100%…100%`.
- Identity-настройка ничего не меняет:

  ```text
  Red:   R 100%, G 0%,   B 0%
  Green: R 0%,   G 100%, B 0%
  Blue:  R 0%,   G 0%,   B 100%
  ```

- `Strength` смешивает результат с исходным цветом: `0%` — оригинал, `100%` — полный mixer.
- Отрицательные коэффициенты разрешены и не нормализуются автоматически.
- Для активного output-канала показывается сумма коэффициентов (`Total`). Это подсказка, а не ограничение.
- Кнопка `Normalize` вручную приводит сумму трёх RGB-вкладов активного канала к 100%, если сумма не близка к нулю.
- Кнопка `Reset Channel` сбрасывает только активный выходной канал; `Reset All` возвращает identity.
- После вычисления результат ограничивается диапазоном LDR `0…1`, соответствующим поддерживаемым Texture Lab текстурам.
- Alpha по умолчанию всегда сохраняется без изменений.
- В сворачиваемом `Advanced` появляется `Alpha Mode: Preserve / Mix`. В режиме `Mix` открывается четвёртая секция `Alpha` с коэффициентами входных `R/G/B/A` и `Constant`.
- `Monochrome` использует один набор `R/G/B + Constant`, вычисляет серый канал и записывает его в RGB. Alpha следует выбранному Alpha Mode.
- Все параметры являются частью сериализуемого effect data, участвуют в Duplicate, Undo/Redo и восстановлении сессии.

## 3. Художественные функции

Внутри карточки нужны быстрые локальные рецепты. Это не общая система preset assets из этапа 8, а кнопки, меняющие только текущий Channel Mixer:

- `Identity`;
- `Swap R/G`;
- `Swap R/B`;
- `Swap G/B`;
- `Warm`;
- `Cool`;
- `Sepia`;
- `High Contrast Mono`;
- `Luminance Mono`.

Точные коэффициенты художественных рецептов подбираются на тестовой цветовой и фотографической текстуре. Рецепты должны лишь записывать обычные параметры эффекта, без отдельного runtime-кода в shader.

Не включать сюда Gradient Map, Split Toning, замену теней/светов выбранными цветами, Curves или Palette Quantization. Это нелинейные эффекты с другой моделью и отдельным местом в стеке.

## 4. UI карточки

Основная часть:

```text
Channel Mixer

Output: [ Red ] [ Green ] [ Blue ]

Red Source       +100%
Green Source        0%
Blue Source         0%
Constant            0%
Total             100%

[Normalize] [Reset Channel]
Strength           100%

Monochrome          Off
Recipes             ▼
Advanced            ▼
[Reset All]
```

Три секции output не должны быть раскрыты одновременно: правая панель Texture Lab узкая, поэтому постоянно видимая таблица из девяти слайдеров ухудшит работу со стеком.

Требования к взаимодействию:

- ввод числа рядом со slider;
- цветные подписи R/G/B;
- double-click по числу возвращает его стандартное значение, если UI Toolkit позволяет это сделать без отдельной сложной инфраструктуры;
- изменение любого параметра сразу обновляет preview;
- все изменения регистрируются через существующий `EditEffect` и стандартный Unity Undo;
- смена выбранной UI-секции не меняет effect data и не должна пересчитывать preview.

## 5. Модель данных

Заменить дискретные `ChannelSource` поля на сериализуемые коэффициенты:

```text
Vector3 redOutput      = (1, 0, 0)
Vector3 greenOutput    = (0, 1, 0)
Vector3 blueOutput     = (0, 0, 1)
Vector3 constants      = (0, 0, 0)
float strength         = 1
bool monochrome        = false
Vector3 monochromeMix  = (0.2126, 0.7152, 0.0722)
float monochromeConstant = 0
AlphaMode alphaMode    = Preserve
Vector4 alphaOutput    = (0, 0, 0, 1)
float alphaConstant    = 0
```

Чтобы не ломать `[SerializeReference]` данные текущей Library-сессии, безопаснее временно сохранить внутреннее имя класса `ChannelRemapEffectData`, но изменить его `DisplayName`, поля и поведение. Старые enum-поля Unity проигнорирует, новые поля получат identity defaults. После появления версионирования presets класс можно переименовать с явной миграцией.

`Duplicate()` обязан копировать каждый коэффициент, режим и Strength.

## 6. GPU processing

Переиспользовать существующий `TextureLabChannels.shader`; новый shader не требуется.

Основной RGB-путь:

```text
mixed.r = dot(source.rgb, redOutput)   + constant.r
mixed.g = dot(source.rgb, greenOutput) + constant.g
mixed.b = dot(source.rgb, blueOutput)  + constant.b
result.rgb = saturate(lerp(source.rgb, mixed, strength))
```

Monochrome-путь:

```text
gray = dot(source.rgb, monochromeMix) + monochromeConstant
mixed.rgb = gray
```

Alpha:

```text
Preserve: result.a = source.a
Mix:      result.a = saturate(dot(source.rgba, alphaOutput) + alphaConstant)
```

Все вычисления выполняются одним fragment pass. Никаких дополнительных RenderTexture или CPU readback.

## 7. Изменяемые файлы

- `Packages/com.texturelab.editor/Editor/Model/TextureEffectData.cs`
  - заменить `ChannelSource` и дискретные поля на коэффициенты mixer;
  - добавить `ChannelMixerAlphaMode`;
  - сохранить корректный Duplicate и identity defaults.
- `Packages/com.texturelab.editor/Editor/Processing/TextureProcessor.cs`
  - заменить параметры `ChannelRemapProcessor` на матрицу, constants, strength, monochrome и alpha;
  - processor остаётся одной записью в существующем type → processor словаре.
- `Packages/com.texturelab.editor/Editor/UI/TextureLabWindow.cs`
  - заменить четыре EnumField на переключаемые output-секции, sliders, Total, Normalize, reset, monochrome, recipes и Advanced alpha;
  - локальное состояние выбранной вкладки хранить только в EditorWindow/UI, не в effect data.
- `Packages/com.texturelab.editor/Editor/UI/TextureLabWindow.uss`
  - компактные segmented buttons, цветные channel labels и строки действий.
- `Packages/com.texturelab.editor/Shaders/TextureLabChannels.shader`
  - заменить выбор каналов на матричное смешивание.
- `ARCHITECTURE.md`
  - заменить описание Channel Remap на Channel Mixer и зафиксировать alpha policy.

Не добавлять зависимости, новые runtime assemblies или новые RenderTexture.

## 8. Порядок реализации

1. Зафиксировать identity defaults и формулы RGB/alpha.
2. Заменить effect data, сохранив имя сериализуемого класса для восстановления текущей сессии.
3. Переделать processor и shader одним проходом.
4. Сделать минимальный UI: output selector, коэффициенты, constant, total, strength и reset.
5. Добавить Monochrome и Advanced Alpha Mode.
6. Добавить Normalize и локальные художественные recipes.
7. Добавить USS-оформление без перестройки общего окна.
8. Обновить `ARCHITECTURE.md`.
9. Refresh/compile через Unity MCP, открыть `Tools > Texture Lab`, проверить Console.
10. Передать пользователю на визуальную проверку на обычной цветной текстуре и текстуре с alpha.

## 9. Проверки готовности

- Identity визуально совпадает с входной текстурой.
- Каждый из девяти RGB-коэффициентов независимо влияет на ожидаемый output channel.
- Отрицательные значения работают без shader errors и фиолетового preview.
- Constant корректно осветляет или затемняет выбранный output channel.
- Strength `0%` показывает оригинал, `100%` — mixer.
- Normalize делает Total равным 100% и безопасно игнорирует почти нулевую сумму.
- Monochrome использует заданные веса и не повреждает alpha.
- Alpha Preserve оставляет исходную alpha неизменной после RGB mixing.
- Alpha Mix корректно получает alpha из R/G/B/A и constants.
- Recipes дают разные предсказуемые художественные результаты и могут быть отменены через Undo.
- Duplicate создаёт независимую копию всех параметров.
- Перемещение эффекта относительно Posterize, Palette Quantization и Levels меняет результат в соответствии с порядком стека.
- Настройки переживают закрытие/открытие окна и перезапуск Editor через существующий `TextureLabSession`.
- Unity Console не содержит C# или shader errors.

## 10. Необходимый контекст для новой сессии

Проект:

- Unity `6000.4.0f1`, `GigaTower`, Windows Editor/StandaloneWindows64.
- Главная сцена проекта: `Assets/_Project/Scenes/Game.unity`; Channel Mixer является Editor-only инструментом и сцену не изменяет.
- Embedded package: `Packages/com.texturelab.editor`.
- Исходный общий план: `Assets/TextureLab/Plan.md`.
- Перед работой обязательно прочитать `ARCHITECTURE.md` и проверить через Unity MCP активный instance `GigaTower` с правильным project root.

Текущее состояние Texture Lab:

- UI Toolkit окно открывается через `Tools > Texture Lab`.
- Рабочий stack хранится в `TextureLabSession` через `[SerializeReference]` и восстанавливается из `Library/TextureLabSession.asset`.
- Preview GPU pipeline использует две постоянные ping-pong RenderTexture размером не более 1024 px.
- Effect data отделены от processors; processor выбирается по runtime type в `TextureProcessor`.
- Текущий `Channel Remap` уже добавлен в data, UI, processor и `TextureLabChannels.shader`, но умеет только выбирать `R/G/B/A/Luminance/0/1`. Именно его нужно заменить.
- Все обычные эффекты сохраняют alpha. Только новый Channel Mixer в явном `Alpha Mode: Mix` может её менять.
- На момент создания плана Unity MCP подключён, проект совпадает, compilation idle, Console содержит 0 errors.
- Рабочее дерево уже содержит незакоммиченные изменения Texture Lab; не удалять и не перезаписывать чужие изменения.

Ограничения реализации:

- только обычные LDR Default/Sprite Texture2D;
- исходный asset никогда не изменяется;
- editor session должна восстанавливаться;
- не добавлять persistent automated tests без отдельного запроса;
- после правок обновить `ARCHITECTURE.md`, выполнить Unity refresh/compile и минимальную проверку через MCP;
- если MCP недоступен, stale после retry или подключён к другому проекту — остановиться без правок.

## 11. Граница scope

Этот план заканчивается полностью работающим линейным Channel Mixer. Если после визуального теста нужны выбранные цвета для теней, midtones и highlights, это следует оформить отдельным эффектом `Color Grading` или `Split Toning`, а не усложнять Channel Mixer второй несовместимой моделью.

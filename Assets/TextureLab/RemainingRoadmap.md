# Texture Lab — план оставшейся разработки

## 1. Текущая точка

Готово и проверено пользователем:

- этап 1 — Core;
- этап 2 — Stack UX;
- этап 3 — Pixel processing core;
- этап 4 — Palette engine;
- этап 5 — Dithering.

Реализовано в этапе 6:

- Color Replace;
- White / Value / Blue Noise;
- separable Gaussian Blur;
- базовый Channel Remap;
- сохранение alpha обычными цветовыми эффектами.

Этапы 6B, 7A и 9 завершены и визуально подтверждены пользователем. Этапы 7B, 8 и 10 реализованы; пользовательская проверка Offset, Seam Blend, preset workflow и export выполняется отдельно.

После этого остаются этапы 7–11 и несколько обязательных cross-cutting функций первой версии: preview channels, zoom/quality, сравнение с оригиналом, dirty-state разделение и финальная проверка single-texture workflow.

## 2. Общий порядок

```text
6B  Channel Mixer
 ↓
7A  Preview workspace
 ↓
7B  Offset + Seam Blend
 ↓
8   Presets
 ↓
9   Variations
 ↓
10  Full-resolution Export
 ↓
10B Single-texture stabilization
 ↓
11  Batch Processing
```

Каждый этап завершается Unity compile/Console check через MCP и короткой пользовательской визуальной проверкой. Не объединять несколько крупных этапов в одну поставку: ошибки preview, сериализации и full-resolution export должны локализоваться отдельно.

---

# Этап 6B — завершение Secondary Effects

Подробный план: `Assets/TextureLab/ChannelMixerPlan.md`.

Статус: завершено 4 сентября 2026; Unity compile/Console gate и пользовательская визуальная проверка пройдены.

## Scope

- заменить пользовательский `Channel Remap` на `Channel Mixer`;
- матрица RGB 3×3, отрицательные коэффициенты, constants;
- Strength, Normalize, Reset;
- Monochrome;
- локальные художественные recipes;
- Alpha Preserve по умолчанию и опциональный Alpha Mix;
- сохранить совместимость текущей `[SerializeReference]` сессии.

## Gate

- Identity не меняет изображение;
- отрицательные коэффициенты и recipes визуально работают;
- alpha сохраняется в Preserve;
- Duplicate, Undo и восстановление сессии работают;
- Console содержит 0 C# и shader errors.

---

# Этап 7A — Preview workspace

Сначала завершить preview как рабочее место. Tile Preview и сравнение с оригиналом не являются эффектами и не должны менять export.

Статус: реализовано 4 сентября 2026; Unity compile, Console и GPU display/quality checks пройдены, ожидается пользовательская визуальная проверка workspace.

## 7A.1 Tile Preview

Режимы:

```text
1×1
2×2
3×3
4×4
5×5
6×6
7×7
8×8
```

Реализация — сетка UI Toolkit `Image` от 1 до 64 элементов, использующих один и тот же processed/display RenderTexture. Это не требует дополнительной GPU-обработки и сразу показывает стыки.

Смена tiling меняет только layout/display state и не пересчитывает effect stack.

## 7A.2 Compare Original

- кнопка `Original` переключает Original / Processed обычным кликом;
- Space выполняет такое же переключение;
- потеря фокуса не меняет выбранный режим;
- режим работает и в 1×1, и в tiled preview;
- никаких snapshot или split view в первой версии.

## 7A.3 Zoom

Минимальные режимы:

```text
Fit
25%
50%
100%
200%
```

- `Fit` остаётся default;
- zoom меняет только размер UI изображения внутри viewport/ScrollView;
- zoom не запускает GPU pipeline;
- панорамирование требуется только когда изображение больше viewport; достаточно стандартного ScrollView.

## 7A.4 Preview Channel

Режимы:

```text
RGB
R
G
B
Alpha
Luminance
```

Preview Channel не является effect data и никогда не попадает в preset/export.

Для R/G/B/Luminance выводить выбранный канал как grayscale; Alpha показывать как непрозрачную grayscale-маску. Если UI Toolkit не позволяет применить display material напрямую, использовать один отдельный reusable preview-display RenderTexture. Две основные ping-pong RT pipeline остаются без изменений.

## 7A.5 Preview Quality

```text
512
1024 (default)
2048
```

- ограничение применяется только к realtime preview;
- не увеличивать изображение выше его исходного размера;
- export всегда обрабатывается в исходном resolution;
- quality сохраняется как состояние Editor session, но не входит в presets.

## 7A.6 Dirty states

Разделить обновления:

```text
StackDirty   → заново прогнать effect stack
DisplayDirty → только обновить original/tile/zoom/channel presentation
```

Не добавлять per-effect cache: для текущего лимита 2K и небольшого stack он увеличит сложность и расход VRAM без подтверждённой проблемы.

## Предполагаемые файлы

- `Editor/UI/TextureLabWindow.cs`;
- `Editor/UI/TextureLabWindow.uss`;
- `Editor/Model/TextureLabSession.cs` — только session UI preferences;
- `Editor/Processing/TextureProcessor.cs` — настраиваемый preview max dimension;
- новый компактный preview-only shader/pass только если нужен для channel display.

Выносить `TexturePreviewView` из окна только если изменения делают текущий класс неудобным для сопровождения; заранее создавать остальные классы из примерной структуры исходного плана не нужно.

## Gate

- Tile 8×8 не создаёт 64 копии processed texture;
- кнопка Original и Space предсказуемо переключают original/processed без зависимости от удержания и потери фокуса;
- zoom/tile/channel не пересчитывают stack;
- Alpha preview читаем на прозрачной Sprite/Default texture;
- preview quality меняет только preview resolution;
- после перезапуска Editor session восстанавливается.

---

# Этап 7B — Offset и Seam Blend

Оба инструмента являются обычными reorderable effects и могут присутствовать в stack несколько раз.

Статус: реализовано 4 сентября 2026; Unity compile, Console и GPU checks пройдены, ожидается пользовательская визуальная проверка.

## 7B.1 Offset

Параметры:

```text
Offset X: 0…1
Offset Y: 0…1
Wrap: Repeat (default) / Clamp
[Center Seams] → X 0.5, Y 0.5
```

- Offset переносит RGBA вместе, не отделяя alpha от цвета;
- Repeat использует математический wrap в shader и не зависит от importer wrap mode исходника;
- Clamp нужен для осознанного неврапнутого смещения;
- Center Seams — обычное Undoable изменение двух параметров.

## 7B.2 Seam Blend

Параметры:

```text
Blend Width:    0…0.5
Blend Strength: 0…1
Horizontal
Vertical
Blend Alpha: Off (default)
```

- смешивать противоположные края по плавной маске;
- Horizontal и Vertical независимы;
- RGB смешивается всегда, alpha только при включённом `Blend Alpha`;
- не обещать автоматическое идеальное удаление швов: широкий blend закономерно размывает крупные детали;
- использовать существующий ping-pong pipeline; дополнительная постоянная RT не нужна.

## Предполагаемые файлы

- `Editor/Model/TextureEffectData.cs`;
- `Editor/Processing/TextureProcessor.cs`;
- `Editor/UI/TextureLabWindow.cs`;
- `Shaders/TextureLabSeamless.shader` — Offset и Seam Blend как два passes;
- `ARCHITECTURE.md`.

## Gate

- 0.5/0.5 переносит исходные края в центр;
- Repeat не показывает clamp-полосы независимо от importer settings;
- Horizontal/Vertical blend работают отдельно и вместе;
- alpha не меняется по умолчанию;
- комбинация `Tile Preview + Offset + Seam Blend` позволяет визуально искать швы;
- порядок относительно Blur/Color effects заметно меняет результат.

---

# Этап 8 — Presets

Статус: реализовано 4 сентября 2026; Unity compile/Console gate и создание starter library пройдены, ожидается пользовательская функциональная проверка.

## 8.1 Данные

Создать `TextureLabPreset : ScriptableObject`:

```text
Data Version
[SerializeReference] List<TextureEffectData>
```

Preset хранит только сериализуемый stack:

- порядок;
- enabled/expanded state;
- параметры эффектов;
- palette references;
- seeds;
- Channel Mixer и seamless settings.

Preset не хранит source texture, UI preview state, Material или RenderTexture.

Копирование между session и preset выполняется через независимые `Duplicate()` экземпляры. Нельзя передавать один mutable effect object одновременно preset и session.

## 8.2 Workflow

- `Save Preset As…`;
- `Apply Preset`;
- `Overwrite Preset` с подтверждением;
- `Duplicate Preset` в `Assets`;
- `Rename Preset` через безопасный `AssetDatabase.RenameAsset`;
- `Reset Stack` с Undo;
- ObjectField для выбора пользовательского preset asset.

Apply заменяет только effect stack и не меняет source texture или preview settings. Вся операция — один Undo step.

## 8.3 Starter library

Начальный набор:

```text
PSX Soft
PSX Harsh
Low Color
Retro PC
Dirty Texture
Posterized
Pixel Art
Dreamcast-ish
Dark Horror
```

Built-in presets хранятся как read-only assets внутри embedded package. Для редактирования пользователь создаёт копию в `Assets`.

Не подбирать финальные художественные значения без визуальной проверки пользователя. Сначала подготовить технически валидные варианты, затем отдельным коротким проходом откалибровать их.

## Предполагаемые файлы

- `Editor/Model/TextureLabPreset.cs`;
- `Editor/Presets/TextureLabPresetUtility.cs` при появлении реального повторяющегося asset workflow;
- `Editor/UI/TextureLabWindow.cs`;
- preset `.asset` files в package;
- `ARCHITECTURE.md`.

## Gate

- Save/Apply сохраняют все текущие типы effects и их порядок;
- применение не создаёт shared mutable effect instances;
- palette references сохраняются;
- Apply, Reset и Overwrite корректно участвуют в Undo там, где объект назначения поддерживает Undo;
- built-in preset нельзя случайно перезаписать;
- preset переживает перезапуск Unity.

---

# Этап 9 — Random Variations

Статус: реализовано 4 сентября 2026; Unity compile/Console gate пройдены, ожидается пользовательская функциональная проверка.

## 9.1 Контролируемая рандомизация

В base effect data добавить:

```text
Allow Randomize
```

Диапазоны остаются специфичными для типа эффекта. Первая версия меняет только параметры, для которых небольшой сдвиг художественно безопасен:

- Pixelate — block size/target resolution из допустимых ступеней;
- Posterize — bits;
- Levels — black/white/gamma в валидном порядке;
- Color Adjustments — brightness/contrast/gamma;
- Dither — strength/scale/seed;
- Noise — amount/scale/seed;
- Blur — radius/iterations;
- Palette Quantization — используемый color limit;
- Channel Mixer — strength и малый коэффициентный jitter;
- Offset/Seam Blend — только при явном Allow Randomize.

Color Replace source/replacement colors и palette asset references по умолчанию не рандомизировать.

Для Palette Quantization потребуется параметр `Color Limit: All / N`, потому что существующий `ExtractionColorCount` влияет только на extraction, а не на текущий shader preview. Variation не должна мутировать palette asset.

## 9.2 Генерация

- кнопка `Generate Variations`;
- deterministic generation seed;
- девять независимых клонов текущего stack;
- сетка 3×3 с небольшими preview, например максимум 256 px;
- повторная генерация освобождает старые preview resources;
- клик выбирает вариант, `Apply Variation` заменяет текущий stack одним Undo step;
- закрытие variations без Apply не меняет session.

Не сохранять девять вариантов как assets автоматически.

## 9.3 Производительность

Обрабатывать кандидаты последовательно одним processor context, копируя результаты в девять thumbnail RT. Не создавать отдельный полный pipeline/material set для каждого кандидата.

## Предполагаемые файлы

- `Editor/Model/TextureEffectData.cs`;
- `Editor/Randomization/VariationGenerator.cs`;
- `Editor/UI/VariationsView.cs` только если grid существенно раздувает основное окно;
- `Editor/Processing/TextureProcessor.cs` — явный thumbnail resolution;
- `Editor/UI/TextureLabWindow.cs`;
- `ARCHITECTURE.md`.

## Gate

- одинаковый seed и stack дают одинаковые варианты;
- выключенные для randomization effects не меняются;
- исходный stack не меняется до Apply;
- Apply полностью отменяется одним Undo;
- palette assets не мутируют;
- повторная генерация не накапливает RenderTexture/Material;
- девять preview формируются без заметной блокировки Editor на обычной 2K texture.

---

# Этап 10 — Full-resolution Export

Статус: реализовано 4 сентября 2026; Unity compile/Console gate пройдены, ожидается пользовательская функциональная проверка.

Этот этап начинается только после стабильных presets и single-texture preview workflow.

## 10.1 Processing

- общий processor получает явный режим `Preview` или `Full Resolution`;
- full-resolution path использует исходные width/height, без лимита 512/1024/2048;
- тот же stack и те же processors/shaders используются для preview и export;
- source import `Read/Write` не требуется;
- GPU result считывается в временный CPU `Texture2D` только в конце;
- все временные RT/Texture2D освобождаются в `finally`.

Не создавать отдельную CPU-реализацию эффектов: она неизбежно разойдётся с preview.

## 10.2 Форматы

```text
PNG — RGBA, сохраняет alpha
JPG — RGB, quality setting, предупреждение об удалении alpha
```

JPG warning показывать перед записью. Cancel не должен создавать файл.

## 10.3 Пути

- `Save Next to Source` с suffix `_processed`;
- `Save in Assets…`;
- `Save Anywhere…`.

Если source находится в Packages или другом read-only location, `Save Next to Source` недоступен и UI объясняет причину.

Перед перезаписью существующего файла требуется подтверждение. Запись выполнять через временный файл в целевой директории и последующую замену, чтобы ошибка кодирования/IO не уничтожила существующий asset.

## 10.4 Import Settings

```text
Recommended
Inherit Source
```

Recommended:

- безопасный Default texture import;
- sRGB соответствует цветовой природе source;
- alpha включена для PNG;
- без platform overrides;
- отдельный выбор Point/Bilinear как export option, не как image effect.

Inherit Source копирует только:

- sRGB;
- wrap mode;
- filter mode;
- mip maps;
- безопасный texture type;
- релевантные alpha settings.

Не копировать compression formats, max-size и platform overrides. Перед реализацией сверить точные Unity 6000.4 `TextureImporter` API через Unity reflection/docs.

Если путь внутри `Assets`, выполнить `AssetDatabase.ImportAsset`, применить выбранные settings и выделить новый asset. Для внешнего пути AssetDatabase не используется.

## Предполагаемые файлы

- `Editor/Processing/TextureProcessor.cs` — explicit output size/full-resolution path;
- `Editor/Processing/TextureExporter.cs`;
- `Editor/UI/TextureLabWindow.cs`;
- при необходимости небольшая serializable export-settings model в session;
- `ARCHITECTURE.md`.

## Gate

- экспортированная PNG имеет исходное resolution и alpha;
- JPG имеет выбранное quality и подтверждённо отбрасывает alpha;
- preview limit не влияет на export resolution;
- результат export соответствует preview с учётом разницы resolution-dependent эффектов;
- все три destination workflows работают;
- отмена dialog не меняет файлы;
- ошибка IO не повреждает существующий файл;
- asset внутри Assets автоматически импортируется и выделяется;
- source asset и его importer никогда не изменяются.

---

# Этап 10B — стабилизация первой версии

Перед Batch пройти полный критерий готовности single-texture workflow:

1. Перетащить обычную LDR Default/Sprite texture.
2. Собрать `Pixelate → Noise → Dither → Palette Quantization → Levels`.
3. Переставить, отключить и дублировать effects.
4. Проверить Color Replace, Blur, Channel Mixer, Offset и Seam Blend.
5. Сохранить и применить preset.
6. Сгенерировать 3×3 variations и применить одну.
7. Сравнить original через кнопку и Space.
8. Проверить результат в Tile 3×3 и через preview channels.
9. Экспортировать full-resolution PNG в Assets и JPG наружу.
10. Перезапустить Editor и проверить восстановление unsaved session.

Дополнительно:

- Console без C# и shader errors;
- никакого фиолетового preview;
- Undo/Redo работает для stack, preset apply и variation apply;
- материалы и RT освобождаются при закрытии окна;
- UI остаётся читаемым при минимальном размере окна;
- `ARCHITECTURE.md` соответствует фактическому устройству package.

Batch не начинать, пока этот gate не пройден пользователем.

---

# Этап 11 — Batch Processing

## 11.1 Отдельный режим

Batch располагается в отдельной вкладке/режиме и не усложняет основной single-texture editor.

Входные данные:

```text
Textures[]
Preset
Output folder inside Assets
Format PNG/JPG
Suffix
Import Settings mode
Collision policy
```

- поддержать multi-object drag&drop из Project;
- принимать только те же LDR Default/Sprite Texture2D;
- source list не связан с текущим single-texture source;
- preset обязателен: batch не зависит от несохранённого session stack.

## 11.2 Безопасность

Validation до старта:

- все sources существуют и поддерживаются;
- output находится в разрешённой папке;
- имена после suffix уникальны;
- source и destination не совпадают;
- collision policy известна.

Default collision policy — `Skip Existing`. `Overwrite` включается явно и подтверждается один раз перед запуском.

## 11.3 Выполнение

Для каждой texture:

```text
Validate
Process full resolution
Encode
Atomic write
Import
Apply importer settings
Release resources
```

- показывать cancelable progress;
- отмена завершает текущую безопасную операцию и не начинает следующую;
- ошибка одного файла фиксируется в итоговом отчёте и не обязательно прерывает весь batch;
- всегда очищать progress bar и GPU/CPU resources через `finally`;
- после обработки выполнить один итоговый AssetDatabase refresh/import pass там, где это безопасно.

## 11.4 Результат

Показать summary:

```text
Processed
Skipped
Failed
Cancelled
```

Список ошибок должен содержать source path и краткую причину без огромных stack traces в UI; полные exceptions остаются в Console.

## Предполагаемые файлы

- `Editor/Batch/TextureBatchProcessor.cs`;
- `Editor/UI/BatchView.cs` только если отдельная вкладка оправдывает класс;
- `Editor/Processing/TextureExporter.cs` — переиспользование single export;
- `Editor/UI/TextureLabWindow.cs`;
- `ARCHITECTURE.md`.

## Gate

- один preset одинаково применяется к нескольким textures разных размеров;
- неподдерживаемые inputs отклоняются до обработки;
- Skip/Overwrite работают предсказуемо;
- Cancel не оставляет частично записанный destination;
- ошибка одного source отражается в summary;
- импортированные assets получают выбранные безопасные settings;
- после batch нет утечек RenderTexture/Texture2D/Material;
- single-texture session не изменена.

---

# Что не входит в оставшуюся первую версию

Не включать до завершения этапа 11:

- Floyd–Steinberg;
- Gradient Map и Split Toning;
- Curves/Histogram;
- Sharpen/Sobel;
- normal/height map generation;
- advanced seam removal;
- custom shader API;
- per-effect GPU cache;
- HDR/EXR, normal maps и data textures;
- runtime-компоненты.

Это будущие функции после стабильной первой версии.

---

# Необходимый контекст для продолжения

## Проект

- Unity `6000.4.0f1`;
- проект `GigaTower`;
- active gameplay scene `Assets/_Project/Scenes/Game.unity`;
- Texture Lab — Editor-only embedded package `Packages/com.texturelab.editor`;
- общий продуктовый план: `Assets/TextureLab/Plan.md`;
- план Channel Mixer: `Assets/TextureLab/ChannelMixerPlan.md`.

## Текущая архитектура Texture Lab

- UI Toolkit окно: `Editor/UI/TextureLabWindow.cs`, меню `Tools > Texture Lab`;
- persistent unsaved session: `Editor/Model/TextureLabSession.cs`, файл в `Library/TextureLabSession.asset`;
- polymorphic effect data: `Editor/Model/TextureEffectData.cs`, `[SerializeReference]` list;
- GPU pipeline: `Editor/Processing/TextureProcessor.cs`;
- две reusable ping-pong preview RT, текущий лимит 1024;
- processors выбираются словарём по типу effect data;
- palettes являются отдельными `TextureLabPalette` assets;
- shader assets находятся в `Packages/com.texturelab.editor/Shaders`;
- исходная texture не изменяется и не обязана быть Read/Write Enabled.

## Ограничения работы

- перед каждым этапом прочитать `ARCHITECTURE.md`, проверить Unity MCP instance/project/state;
- если MCP unavailable, wrong project или stale после retry — остановиться без файловых правок;
- сохранять Unity serialization и `.meta`;
- не менять vendor assets;
- не добавлять зависимости;
- использовать существующий processor/ping-pong pipeline;
- после архитектурных изменений обновлять `ARCHITECTURE.md`;
- после code/shader changes выполнять Unity compile, Console check и минимальный релевантный ручной check через MCP;
- persistent automated tests добавлять только по отдельному запросу;
- сохранять и не перетирать существующие незакоммиченные изменения пользователя.

## Состояние на момент создания roadmap

- Unity MCP подключён к правильному `GigaTower` instance;
- Editor idle, active scene `Game`;
- последний Console check показал 0 errors;
- package и планы пока находятся в незакоммиченном рабочем дереве;
- после создания этого roadmap реализация новых этапов не выполнялась.

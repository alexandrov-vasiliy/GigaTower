# Texture Lab — план реализации Unity Editor инструмента

## 1. Назначение

Texture Lab — отдельный пакет для Unity 6 URP, предназначенный для неразрушающей обработки текстур непосредственно внутри Unity Editor.

Основная цель инструмента — не просто техническая коррекция изображений, а быстрый поиск уникального визуального стиля с уклоном в PSX / retro / low-poly эстетику.

Основной workflow:

`Texture2D → Stack of Effects → Preview → Preset / Variations → Export`

Исходный asset никогда не изменяется.

---

# 2. Основные принципы

### Неразрушающая обработка

Все изменения существуют только как стек эффектов.

Пример:

`Pixelate → Noise → Blur → Palette Quantization → Levels`

Пользователь может:

* отключать эффект;
* менять порядок;
* дублировать эффект;
* удалять эффект;
* изменять параметры;
* сохранять весь стек как preset.

Это принципиально важно: например,

`Blur → Quantize`

и

`Quantize → Blur`

дают совершенно разные изображения.

---

# 3. Основное окно

Инструмент открывается примерно через:

`Tools → Texture Lab`

Главное окно:

```text
┌─────────────────────────────────────────────────────┐
│ Texture Lab                                         │
├─────────────────┬───────────────────┬───────────────┤
│                 │                   │ EFFECT STACK  │
│                 │                   │               │
│                 │                   │ ☑ Pixelate    │
│     PREVIEW     │                   │ ☑ Noise       │
│                 │                   │ ☑ Quantize    │
│                 │                   │ ☑ Levels      │
│                 │                   │               │
│                 │                   │ [+ Effect]    │
├─────────────────┴───────────────────┴───────────────┤
│ Original | Tile | Zoom | Preset | Randomize | Save │
└─────────────────────────────────────────────────────┘
```

Интерфейс делать на UI Toolkit.

Главный приоритет UI:

* большое изображение;
* минимум лишних инспекторных элементов;
* понятный стек;
* drag&drop;
* раскрывающиеся карточки эффектов;
* визуально более похож на графический редактор, чем на обычный Unity Inspector.

---

# 4. Выбор исходной текстуры

Источник выбирается только из Unity Project.

Поддержать:

* drag&drop Texture2D в окно;
* ObjectField;
* возможность заменить texture без удаления текущего effect stack.

При смене texture стек эффектов остаётся, чтобы можно было быстро проверять одну обработку на разных изображениях.

---

# 5. Архитектура обработки

Основной processing pipeline работает на GPU.

Используются две RenderTexture:

```text
Source
   ↓
RT A
   ↓
Effect 1
   ↓
RT B
   ↓
Effect 2
   ↓
RT A
   ↓
Effect 3
   ↓
RT B
   ↓
Preview
```

То есть обычный ping-pong:

```text
A → B
B → A
A → B
```

Не создавать новую полноразмерную RenderTexture для каждого эффекта.

Некоторые сложные эффекты могут временно запросить дополнительные RT через RenderTexture pool.

---

# 6. Effect Stack

Нужна базовая абстракция:

```text
TextureEffect
```

Эффект содержит:

```text
ID
Enabled
DisplayName
Parameters
Processing implementation
```

Processor не должен знать внутреннюю реализацию эффектов.

Его задача:

```text
for each enabled effect:
    effect.Process(source, destination)
    swap(source, destination)
```

Один и тот же тип эффекта может присутствовать несколько раз:

```text
Noise
Blur
Noise
Quantize
Blur
```

---

# 7. Effect data и Effect processor нужно разделить

Не хранить runtime Material непосредственно внутри preset.

Например:

```text
PixelateEffectData
```

хранит:

```text
Mode
PixelSize
TargetResolution
SamplingMode
```

А:

```text
PixelateProcessor
```

отвечает за GPU processing.

Таким образом preset содержит только сериализуемые данные.

---

# 8. Pixelate

Один из главных эффектов.

Два режима.

## Pixel Size

```text
1
2
4
8
16
32
64
```

Размер одного виртуального пикселя относительно оригинального изображения.

## Target Resolution

Например:

```text
2048×2048 source

→ 512
→ 256
→ 128
→ 64
→ 32
```

Aspect Ratio сохраняется автоматически.

---

# 9. Pixel Sampling

Pixelate должен иметь два принципиально разных метода.

### Nearest

Берётся один sample на весь блок.

Очень резкий digital / PSX эффект.

### Average

Изображение сначала уменьшается:

```text
2048
↓
128
```

с усреднением цветов.

После этого возвращается к исходному разрешению через Point filtering.

Результат получается значительно мягче и естественнее.

Оба метода обязательны.

---

# 10. Posterization

Posterization уменьшает количество возможных значений каждого канала.

Например:

```text
256 уровней R
→
8 уровней R
```

То же для G и B.

Параметры:

```text
Levels
или
Bits Per Channel
```

Примеры:

```text
RGB888
RGB666
RGB555
RGB444
RGB332
```

Это простой и предсказуемый способ получить ограниченную цветовую глубину.

---

# 11. Palette Quantization

Это отдельный эффект.

Posterization и Palette Quantization НЕ объединять.

Разница:

### Posterization

Ограничивает уровни каждого канала независимо.

Например:

```text
R = 4
G = 4
B = 4
```

теоретически даёт до:

```text
4 × 4 × 4 = 64 цветов.
```

Но фактическая палитра заранее не определена.

### Palette Quantization

Всё изображение должно использовать строго заданный набор цветов:

```text
Color 1
Color 2
Color 3
...
Color 16
```

Каждый pixel заменяется ближайшим цветом палитры.

Для художественной стилизации это гораздо интереснее.

Поэтому Texture Lab должен поддерживать оба подхода.

---

# 12. Palette System

Палитры должны существовать как отдельные assets.

Например:

```text
TextureLabPalette
```

Содержит:

```text
Name
Colors[]
```

Пользователь может:

* создать палитру вручную;
* изменить любой цвет;
* добавить цвет;
* удалить цвет;
* изменить порядок;
* сохранить;
* загрузить;
* импортировать;
* извлечь палитру из texture.

---

# 13. Automatic Palette Extraction

Нужна команда:

```text
Extract Palette
```

Параметр:

```text
Colors:
4
8
16
32
64
```

Перед анализом изображение уменьшается примерно до:

```text
128×128
или
256×256
```

Нет смысла анализировать все четыре миллиона пикселей 2K texture.

Для первой полноценной реализации я бы использовал K-Means с детерминированным seed.

Лучше считать расстояние цветов не просто по RGB, а в perceptual color space.

Можно использовать Oklab.

Это даст более логичное распределение оттенков глазами человека.

---

# 14. Palette Quantization shader

Палитра передаётся GPU как небольшой массив цветов.

Например максимум:

```text
64 colors
```

На каждый pixel:

```text
source color
    ↓
color-space conversion
    ↓
distance to palette colors
    ↓
nearest color
```

Для PSX presets обычно будет достаточно:

```text
8
16
32
цветов.
```

---

# 15. Dithering

На первой версии:

```text
Bayer 2×2
Bayer 4×4
Bayer 8×8
Blue Noise
```

Параметры:

```text
Strength
Scale
Offset / Seed
Monochrome / RGB
```

Floyd–Steinberg оставить на будущее.

---

# 16. Dither должен быть отдельным Effect

Не вшивать dithering непосредственно внутрь Quantize.

Это позволит делать:

```text
Dither
↓
Quantize
```

или:

```text
Quantize
↓
Dither
↓
Quantize
```

и получать разные изображения.

Presets просто будут автоматически ставить эффекты в разумном порядке.

---

# 17. Levels

Параметры:

```text
Black Point
White Point
Gamma
Output Black
Output White
```

Alpha по умолчанию не изменяется.

---

# 18. Brightness / Contrast / Gamma

Один эффект:

```text
Color Adjustments
```

Параметры:

```text
Brightness
Contrast
Gamma
```

При необходимости позже добавить:

```text
Exposure
Saturation
```

Но не раздувать первую версию.

---

# 19. Color Replace

Параметры:

```text
Source Color
Replacement Color
Tolerance
Softness
```

Алгоритм должен давать плавную маску по расстоянию между цветами.

Нужна возможность:

```text
Preview Mask
```

чтобы видеть, какие области изображения попадают под replacement.

---

# 20. Noise / Grain

Эффект:

```text
Noise
```

Минимально:

```text
Amount
Scale
Seed
Monochrome / RGB
```

Типы первой версии:

```text
White Noise
Value Noise
Blue Noise
```

Позже:

```text
Perlin
Film Grain
Ordered Pattern
Custom Noise Texture
```

Seed обязательно должен сохраняться в preset.

---

# 21. Blur

Первая версия:

```text
Gaussian Blur
```

Параметры:

```text
Radius
Iterations
```

Использовать separable blur:

```text
Horizontal
↓
Vertical
```

а не полноценный квадратный kernel.

---

# 22. Channel Tools

Отдельный Effect:

```text
Channel Remap
```

Для каждого выходного канала выбрать источник:

```text
Output R ← R / G / B / A / Luminance / 0 / 1
Output G ← ...
Output B ← ...
Output A ← ...
```

Например:

```text
R → R
G → G
B → B
Luminance → A
```

Нужно также сделать Preview Mode:

```text
RGB
R
G
B
Alpha
Luminance
```

Preview Mode не является эффектом и не влияет на export.

---

# 23. Alpha

Поскольку инструмент должен работать с прозрачными текстурами, alpha нельзя случайно уничтожать.

Для обычных цветовых эффектов default:

```text
Alpha Mode:
Preserve
```

Для некоторых эффектов можно дать:

```text
Preserve
Process
```

Channel Remap может изменять alpha напрямую.

PNG сохраняет alpha.

JPG alpha не поддерживает, поэтому при JPG export пользователь получает предупреждение:

```text
Alpha channel will be discarded.
```

---

# 24. Tile Preview

Это режим Preview, а не Effect.

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

Например:

```text
┌────┬────┬────┐
│    │    │    │
├────┼────┼────┤
│    │    │    │
├────┼────┼────┤
│    │    │    │
└────┴────┴────┘
```

Позволяет моментально видеть seams.

---

# 25. Seamless tools

Поскольку crop/transform в инструменте не нужны, оставить только операции, непосредственно связанные с seamless texture.

## Offset

```text
Offset X
Offset Y
Wrap
```

Полезная кнопка:

```text
Center Seams
```

которая делает:

```text
X = 0.5
Y = 0.5
```

## Seam Blend

Дополнительный фильтр:

```text
Blend Width
Blend Strength
Horizontal
Vertical
```

Он смешивает противоположные края texture.

Важно не обещать идеальный automatic seamless result — на сложных изображениях он будет смазывать элементы.

Главная рабочая комбинация:

```text
Tile Preview
+
Offset
+
Blur / Color correction
+
Seam Blend
```

---

# 26. Preview resolution

Исходные изображения предполагаются максимум примерно 2K.

Тем не менее realtime Preview желательно ограничить:

```text
1024
```

с возможностью:

```text
Preview Quality:
512
1024
2048
```

Default:

```text
1024
```

Экспорт всегда выполняется в полном исходном resolution.

---

# 27. Compare Original

Не делать сложную систему snapshot.

Первая версия использует обычное переключение, а не удержание:

```text
Original button → toggle Original / Processed
```

и:

```text
Space → toggle Original / Processed
```

Этого достаточно.

Позже при необходимости можно добавить split view.

---

# 28. Preset System

Preset — ScriptableObject.

Например:

```text
TextureLabPreset
```

Содержит полностью сериализованный Effect Stack:

```text
Pixelate
Noise
Palette Quantization
Levels
...
```

включая:

```text
Effect order
Enabled state
Effect settings
Palette references
Seeds
```

Пресет не содержит ссылок на runtime Materials или RenderTextures.

---

# 29. Preset workflow

Пользователь может:

```text
Save Preset
Load Preset
Duplicate Preset
Rename Preset
Reset
```

Начальные встроенные presets можно сделать такими:

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

Они нужны в первую очередь как стартовые точки для экспериментов.

---

# 30. Random Variations

Это одна из ключевых экспериментальных функций инструмента.

Кнопка:

```text
Generate Variations
```

Открывает сетку:

```text
A B C
D E F
G H I
```

Каждый вариант получает небольшие случайные изменения параметров текущего stack.

Например:

```text
Pixel Size ±
Palette Size ±
Noise Strength ±
Contrast ±
Gamma ±
Dither Strength ±
Blur ±
```

---

# 31. Randomization Settings

Не рандомизировать всё без контроля.

Каждый effect должен иметь:

```text
Allow Randomize
```

и допустимый диапазон.

Например:

```text
Pixel Size:
4–16

Noise:
0.05–0.25

Palette:
8–32
```

После выбора варианта:

```text
Apply Variation
```

его значения становятся текущими параметрами Effect Stack.

---

# 32. Export

Форматы первой версии:

```text
PNG
JPG
```

Варианты сохранения:

### Save next to source

```text
brick.png
brick_processed.png
```

### Save inside project

Выбрать папку внутри:

```text
Assets/...
```

### Save anywhere

Обычный системный file dialog.

---

# 33. Автоматический импорт

Если destination находится внутри `Assets/`, после сохранения файл автоматически импортируется Unity.

После импорта можно выделить новый asset в Project Window.

---

# 34. Import Settings

Не копировать исходный TextureImporter полностью.

Добавить:

```text
Import Settings:

○ Recommended
○ Inherit Source
```

## Recommended

Texture Lab выставляет безопасные настройки самостоятельно.

## Inherit Source

Копируются только релевантные настройки:

```text
sRGB
Wrap Mode
Filter Mode
Mip Maps
Texture Type — где это безопасно
Alpha-related settings
```

Не копировать автоматически:

```text
platform overrides
compression formats
max-size overrides
```

Это важно, потому что export — новый самостоятельный asset.

---

# 35. Pixel-art / PSX export

Для некоторых presets можно предлагать:

```text
Filter Mode: Point
```

Но это Import Setting, а не часть изображения.

То есть пользователь может отдельно решить:

```text
Processed pixelated image
+
Point Filtering
```

или:

```text
Processed pixelated image
+
Bilinear Filtering
```

---

# 36. Undo / Redo

Изменения Effect Stack должны поддерживать стандартный:

```text
Ctrl+Z
Ctrl+Y
```

Undo должен работать для:

```text
Add Effect
Remove Effect
Move Effect
Enable / Disable
Parameter changes
Preset load
Variation apply
```

Не нужно добавлять RenderTextures в Undo.

Хранить Undo только для сериализованного состояния редактора.

---

# 37. Batch Processing

Batch делать отдельным режимом после готовности основного инструмента.

Он не должен усложнять базовый editor.

Пример:

```text
Batch Tab

Textures:
brick_01
brick_02
wall_01
ground_02

Preset:
PSX Dark

Output:
Assets/Textures/Processed/
```

Для каждой texture:

```text
Load
Process full resolution
Export
Import
```

---

# 38. Package architecture

Сделать полноценный Unity package.

```text
Packages/
com.texturelab.editor/
```

или локально:

```text
Assets/TextureLab/
```

на этапе разработки.

После стабилизации перенести в Package.

---

# 39. Предлагаемая структура

```text
TextureLab/
│
├── Editor/
│   ├── UI/
│   │   ├── TextureLabWindow.cs
│   │   ├── TexturePreviewView.cs
│   │   ├── EffectStackView.cs
│   │   ├── EffectCardView.cs
│   │   ├── PaletteView.cs
│   │   └── VariationsView.cs
│   │
│   ├── Processing/
│   │   ├── TextureProcessor.cs
│   │   ├── RenderTexturePool.cs
│   │   ├── ProcessingContext.cs
│   │   └── TextureExporter.cs
│   │
│   ├── Effects/
│   │   ├── Pixelate/
│   │   ├── Posterize/
│   │   ├── PaletteQuantize/
│   │   ├── Dither/
│   │   ├── Levels/
│   │   ├── ColorAdjustment/
│   │   ├── ColorReplace/
│   │   ├── Noise/
│   │   ├── Blur/
│   │   ├── ChannelRemap/
│   │   └── Seamless/
│   │
│   ├── Presets/
│   ├── Palettes/
│   ├── Randomization/
│   └── Batch/
│
├── Shaders/
│   ├── Pixelate.shader
│   ├── Color.shader
│   ├── PaletteQuantize.shader
│   ├── Dither.shader
│   ├── Noise.shader
│   ├── Blur.shader
│   ├── Channels.shader
│   └── Seamless.shader
│
└── Resources/
```

---

# 40. Shader strategy

Не создавать сотню shaders.

Объединить простые эффекты:

```text
Color.shader

Pass 0 Levels
Pass 1 BrightnessContrastGamma
Pass 2 Posterize
Pass 3 ColorReplace
```

Отдельные shaders оставить там, где алгоритм действительно другой:

```text
Pixelate
PaletteQuantize
Dither
Blur
Noise
ChannelRemap
Seamless
```

---

# 41. CPU / GPU разделение

GPU:

```text
Pixelate
Posterize
Palette mapping
Dither
Levels
Brightness / Contrast / Gamma
Color Replace
Noise
Blur
Channel remap
Seam operations
```

CPU / Editor logic:

```text
Preset serialization
Automatic palette extraction
File export
Random variations
Undo
Asset management
UI
Batch processing
```

Compute Shader на первой версии не является обязательным.

Добавлять его только там, где обычный fragment shader действительно становится ограничением.

---

# 42. Performance

При изменении slider preview должен пересчитываться практически мгновенно.

Не пересчитывать изображение, если UI изменение не влияет на processing.

Например:

```text
Zoom
Preview channel
Window resizing
```

не должны полностью перестраивать stack без необходимости.

Нужен dirty-флаг:

```text
StackDirty
PreviewDirty
```

---

# 43. Effect caching — не делать сразу

Теоретически можно кешировать результат после каждого effect.

Например:

```text
Pixelate cache
Noise cache
Quantize cache
```

Но это резко увеличит расход VRAM.

Для 2K texture и относительно небольшого effect stack обычного последовательного GPU processing будет достаточно.

Кэширование оставить только как будущую оптимизацию, если появятся реальные проблемы.

---

# 44. Первая версия — обязательный scope

Обязательно реализовать:

```text
Editor Window
Texture selection

Effect Stack
Drag reorder
Duplicate
Enable / Disable
Undo / Redo

Pixelate
- Block Size
- Target Resolution
- Nearest
- Average

Posterization / Bit Depth

Palette Quantization
Manual Palette
Automatic Palette Extraction
Palette Assets

Bayer Dither
Blue Noise Dither

Levels

Brightness
Contrast
Gamma

Color Replace

Noise / Grain

Gaussian Blur

Channel Remap

Alpha preservation

Tile Preview

Offset / Seam tools

Presets

Original button
Space → toggle Original / Processed

Random Variations

PNG Export
JPG Export

Save in Assets
Save near source
Save anywhere

Automatic Asset import
```

---

# 45. Планируемые будущие эффекты

После стабильной первой версии:

```text
Floyd–Steinberg Dithering

HSV / HSL
Saturation
Exposure

Gradient Map

Threshold

Sharpen

Sobel / Edge Detection

Custom Palette Import Formats

Advanced Grain

Normal Map Generation

Height Map Generation

Basic Delighting

Advanced Seam Removal

Histogram

Curves

Selective Color

Chromatic Aberration

Color Channel Offset

Compression / JPEG artifact simulation

CRT patterns

PSX texture wobble simulation

Palette cycling

Custom Effect Shader API
```

---

# 46. Отдельно интересные PSX / stylization эффекты

Позже я бы добавил специальную категорию:

```text
Stylize
```

С эффектами, которые не являются обычной коррекцией изображения:

```text
Color Bleeding
Chroma Noise
Palette Drift
Block Compression
Texture Jitter
Ordered Color Noise
Channel Misalignment
Low Precision UV-style filtering simulation
```

Именно такие эффекты со временем смогут отличить Texture Lab от обычного редактора картинок.

---

# 47. Порядок разработки

## Этап 1 — Core

Сделать:

```text
EditorWindow
Texture input
Preview
RenderTexture pipeline
Effect abstraction
Effect stack
```

Проверка:

можно загрузить texture и прогнать через несколько тестовых shader effects.

---

## Этап 2 — Stack UX

Добавить:

```text
Add
Remove
Duplicate
Enable
Drag reorder
Undo
```

До разработки большого числа effects сам stack должен быть полностью удобным.

---

## Этап 3 — Pixel processing core

Реализовать:

```text
Pixelate
Posterize
Levels
Brightness / Contrast / Gamma
```

На этом этапе уже должен существовать рабочий минимальный продукт.

---

## Этап 4 — Palette engine

Добавить:

```text
Palette asset
Manual palettes
Palette extraction
Palette quantization
```

Это один из важнейших этапов проекта.

---

## Этап 5 — Dithering

```text
Bayer
Blue Noise
```

Проверить работу:

```text
Dither → Palette
```

и:

```text
Dither → Posterize
```

---

## Этап 6 — Secondary effects

```text
Color Replace
Noise
Blur
Channels
Alpha
```

---

## Этап 7 — Texture utilities

```text
Tile Preview
Offset
Seam Blend
```

---

## Этап 8 — Presets

Сериализация всего Effect Stack.

Создать стартовую библиотеку PSX / retro presets.

---

## Этап 9 — Variations

Реализовать генерацию 3×3 вариантов и controlled randomization параметров.

---

## Этап 10 — Export

```text
PNG
JPG
Asset import
Import settings
```

---

## Этап 11 — Batch

Только после завершения single-texture workflow.

---

# 48. Критерий готовности первой версии

Инструмент можно считать реально пригодным, когда пользователь способен:

1. Перетащить обычную Texture2D из Project.

2. Добавить:

```text
Pixelate
→ Noise
→ Dither
→ Palette Quantization
→ Levels
```

3. Переставить их местами и мгновенно увидеть изменение.

4. Отключить любой effect.

5. Сохранить комбинацию как preset.

6. Применить другой preset.

7. Сгенерировать несколько случайных variations.

8. Сравнить результат с оригиналом через Space.

9. Проверить texture в режиме 3×3 tiling.

10. Экспортировать готовую 2K PNG обратно в Unity Project.

Если весь этот workflow быстрый и приятный, ядро Texture Lab получилось правильным.

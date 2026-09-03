# Вода RomanLevel — Shader Graph и VFX Graph

Настроено в `Assets/_Project/Scenes/RomanLevel.unity`. Все новые шейдеры сделаны
обычными узлами Shader Graph, эффекты — VFX Graph. Custom Function/HLSL и
ParticleSystem в этом комплекте не используются.

## Объекты и графы

| Объект | Материал / граф | Назначение |
| --- | --- | --- |
| RomanLevelWaterLayer | Shaders/Prototype/TorchMaterials/Torch_RomanWater.mat → RomanTorchWater.shadergraph | Основная вода и поток к колодцу; версия с пиксельным светом факела |
| Roman Water - Lower Pool | RomanPool.mat → RomanWater.shadergraph | Нижний водоём |
| WaterFallMesh | RomanWaterfall.mat → RomanWaterfall.shadergraph | Искажённые струи и пена у основания |
| tracesWater | RomanWaterTraces.mat → RomanWaterTraces.shadergraph | Расходящиеся и растворяющиеся кольца |
| RomanWaterfallVFX | RomanWaterfallImpact.vfx | Капли, всплески, туман |
| waterfall (1) | Тот же VFX Graph с меньшей интенсивностью | Брызги у трубы на верхнем водоёме |

Выходы VFX Graph используют `RomanWaterSpray.shadergraph`. Основной эффект сохранён
в `RomanWaterfallVFX.prefab`. Старый объект `waterfall` в шахте отключён.
Исходные Zowell-графы и прежние waterfall.vfx сохранены как неиспользуемые здесь ассеты.

Меши взяты только из этой папки: RomanLevelWaterLayer.fbx, WaterFallMesh.fbx и
WaterTracerMesh.fbx. `tracesWater` использует исходный Circle из WaterTracerMesh;
нижний водоём — второй экземпляр того же меша. Новых моделей и пакетов нет.

## Шумы из SharedTextures

Все новые графы ссылаются на существующие текстуры:

- `Env/SharedTextures/noises_512x512/512x512/Perlin/Perlin_08-512x512.png`:
  волны, нормали, растворение следов, мягкие брызги.
- `Env/SharedTextures/noises_512x512/512x512/Voronoi/Voronoi_01-512x512.png`:
  рисунок струй, искажение UV, рябь и неровность колец.

Эти ассеты импортированы как sRGB. После выборки узлы Colorspace Conversion
восстанавливают значения маски; общие настройки импорта не изменены.
Новые шумовые текстуры и процедурные узлы генерации шума не добавлялись.

## Настройка

Двойной щелчок по `.shadergraph` открывает узлы и группы. Значения для сцены
меняются в соответствующем `.mat`, свойства сгруппированы по назначению.

- **Цвет воды:** Shallow Color, Deep Color, Depth Distance, Opacity.
- **Свечение (группа Glow):** Glow Enabled включает его; Glow Color (HDR Tint) меняет оттенок, белый сохраняет исходную палитру; Glow Intensity задаёт яркость 0–20. Настройки доступны у воды, водопада и колец пены, а также в варианте RomanTorchWater. Ссылки `_WaterGlow` / `_FoamGlow` и значения существующих материалов сохранены. Отключение Glow не отключает преломление и свет факела. Ореол задаётся существующим Bloom в Volume; самосвечение работает независимо от Bloom.
- **Волны:** Wave Height (метры), Wave Scale, Wave Speed; Normal Tiling и
  Normal Strength управляют мелкой рябью. Flow Speed — прокрутка поверхности.
- **Преломление:** Distortion и Refraction Visibility.
- **Пена воды:** Shore Foam Width, Surface Foam, Foam Color.
- **Водопад:** Water Color, Highlight Color, Fall Speed, Flow Tiling,
  Ribbon Contrast. Отдельный слот **Voronoi Distortion** содержит текстуру,
  которая через Distortion Tiling / Speed / Strength смещает координаты потока.
  При Distortion Strength = 0 это смещение выключается.
- **Переход в воду:** Pool Height, Pool Blend Distance, Impact Foam Height,
  Impact Foam Color. Pool Height сейчас равен −62.12.
- **Следы:** Ring Count, Ring Speed, Ring Sharpness, Ring Distortion, Opacity,
  Foam Color. Кольца движутся наружу и исчезают к краю исходного меша.
- **VFX:** на компоненте VisualEffect доступны Droplet Rate, Mist Rate,
  Splash Rate, Spray Color и Shared Spray Noise. В самом VFX Graph отдельно
  настроены время жизни, начальная скорость, гравитация, размер и затухание
  трёх систем. Эффект имеет прогрев 3 секунды; проверять анимацию в Play Mode.

Для новой палитры согласуйте цвета воды, водопада, следов и Spray Color.
Основная вода RomanLevel теперь использует отдельный вариант из `Shaders/Prototype`;
его Custom Function отвечает только за свет факела. Базовый RomanWater и вариант
с факелом имеют одинаковые настройки Glow, добавленные обычными узлами Shader Graph.
При изменении высоты водоёма переместите pool, tracesWater и VFX, затем измените
Pool Height водопада. Следы расположены на 0.08 м выше нижней воды.
При изменении волн выставьте одинаковые Wave Height / Scale / Speed у RomanPool
и RomanWaterTraces; большие амплитуды требуют большего зазора между мешами.

## Особенности

- Unity 6000.4, URP / Shader Graph / VFX Graph 17.4; используются уже установленные пакеты.
- Depth Texture и Opaque Texture включены в существующем PC_RPAsset.
  Преломление читает непрозрачную сцену; подводного режима нет.
- UV водопада сжаты, UV Circle нулевые. Поэтому проекция в графах основана
  на позиции: мировая XZ для воды, локальная XY для следов, локальная Z
  исходного Blender FBX — высота водопада.
- Вода содержит 130 вершин, Circle — 32. Крупные геометрические волны ограничены
  плотностью исходных мешей; мелкая рябь передаётся нормалями.
- VFX имеет по 512 частиц ёмкости на систему и продолжает симуляцию вне кадра,
  чтобы при подходе к водопаду эффект уже был заполнен.
- Старые Roman*.shader, RomanWaterCommon.hlsl, RomanWaterPattern.asset и
  материалы прежних ParticleSystem заменены графами и удалены из Assets.

## Гайды

- [Toxic Waterfall](https://www.youtube.com/watch?v=uOhWT6TxZgE): слои течения,
  шумовые нормали, цвет и маска основания.
- [Waterfall Effect](https://www.youtube.com/watch?v=uFcTWm40fCA): Voronoi-поток,
  искажение координат, расходящиеся следы и затухание VFX.
- [Cartoon Water & Foam](https://www.youtube.com/watch?v=jBmBb-je4Lg): анимация
  поверхности, нормали из высоты и контактная пена.

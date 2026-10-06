using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using TextureLab.Runtime;
using UnityEngine.UIElements;

namespace TextureLab.Editor
{
    internal sealed class TextureLabExportWindow : EditorWindow
    {
        private Texture2D source;
        private List<TextureEffectData> effects;
        private Action exported;
        private TextureExportFormat format = TextureExportFormat.Png;
        private int jpgQuality = 90;
        private TextureExportImportSettings importSettings = TextureExportImportSettings.InheritSource;
        private FilterMode filterMode = FilterMode.Bilinear;
        private Button nextToSourceButton;
        private Label sourceHint;

        internal static void Open(Texture2D source, IReadOnlyList<TextureEffectData> effects, Action exported)
        {
            var window = GetWindow<TextureLabExportWindow>();
            window.titleContent = new GUIContent("Texture Lab Export");
            window.minSize = new Vector2(420f, 360f);
            window.source = source;
            window.effects = CopyEffects(effects);
            window.exported = exported;
            window.Rebuild();
            window.Show();
        }

        public void CreateGUI() => Rebuild();

        private void Rebuild()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.paddingLeft = 10f;
            rootVisualElement.style.paddingRight = 10f;
            rootVisualElement.style.paddingTop = 10f;
            rootVisualElement.style.paddingBottom = 10f;

            var formatField = new EnumField("Format", format);
            formatField.RegisterValueChangedCallback(evt =>
            {
                format = (TextureExportFormat)evt.newValue;
                Rebuild();
            });
            rootVisualElement.Add(formatField);

            if (format == TextureExportFormat.Jpg)
            {
                var quality = new SliderInt("JPG Quality", 1, 100) { value = jpgQuality, showInputField = true };
                quality.RegisterValueChangedCallback(evt => jpgQuality = evt.newValue);
                rootVisualElement.Add(quality);
            }

            var settings = new PopupField<TextureExportImportSettings>("Import Settings",
                new List<TextureExportImportSettings>
                {
                    TextureExportImportSettings.InheritSource,
                    TextureExportImportSettings.Sprite,
                    TextureExportImportSettings.Atlas,
                    TextureExportImportSettings.TilingTexture
                }, importSettings, ImportSettingsLabel, ImportSettingsLabel);
            settings.RegisterValueChangedCallback(evt =>
            {
                importSettings = evt.newValue;
                Rebuild();
            });
            rootVisualElement.Add(settings);

            rootVisualElement.Add(new HelpBox(ImportSettingsDescription(), HelpBoxMessageType.Info));

            var sourceImporter = source == null ? null
                : AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(source)) as TextureImporter;
            bool inheritFilter = importSettings == TextureExportImportSettings.InheritSource && sourceImporter != null;
            var filterChoices = new List<FilterMode> { FilterMode.Point, FilterMode.Bilinear };
            if (inheritFilter && sourceImporter.filterMode == FilterMode.Trilinear)
                filterChoices.Add(FilterMode.Trilinear);
            var filters = new PopupField<FilterMode>("Filter", filterChoices,
                inheritFilter ? sourceImporter.filterMode : filterMode);
            filters.RegisterValueChangedCallback(evt => filterMode = evt.newValue);
            filters.SetEnabled(!inheritFilter);
            filters.tooltip = inheritFilter
                ? "Filter mode is inherited from the source texture."
                : "Point for crisp pixel art; Bilinear for smooth filtering.";
            rootVisualElement.Add(filters);

            sourceHint = new Label();
            sourceHint.style.whiteSpace = WhiteSpace.Normal;
            sourceHint.style.marginTop = 8f;
            rootVisualElement.Add(sourceHint);

            nextToSourceButton = new Button(SaveNextToSource) { text = "Save Next to Source" };
            rootVisualElement.Add(nextToSourceButton);
            rootVisualElement.Add(new Button(SaveInAssets) { text = "Save in Assets…" });
            rootVisualElement.Add(new Button(SaveAnywhere) { text = "Save Anywhere…" });
            UpdateSourceControls();
        }

        private static string ImportSettingsLabel(TextureExportImportSettings settings) => settings switch
        {
            TextureExportImportSettings.InheritSource => "Inherit Source",
            TextureExportImportSettings.Sprite => "Sprite / 2D Character",
            TextureExportImportSettings.Atlas => "Atlas",
            TextureExportImportSettings.TilingTexture => "Tiling Texture",
            _ => settings.ToString()
        };

        private string ImportSettingsDescription()
        {
            string description = importSettings switch
            {
                TextureExportImportSettings.InheritSource =>
                    "Copies the source's type (Default or Sprite), sRGB, wrap, filter, mipmaps and alpha settings. "
                    + "Without a source importer: Default, Clamp, no mipmaps, selected filter.",
                TextureExportImportSettings.Sprite =>
                    "For a single 2D sprite or character: Sprite (Single), Clamp, no mipmaps. PNG transparency is enabled.",
                TextureExportImportSettings.Atlas =>
                    "For an existing atlas used with mesh UVs: Default, Clamp, no mipmaps to reduce bleeding between regions. "
                    + "Does not pack or slice sprites; leave padding between atlas regions.",
                TextureExportImportSettings.TilingTexture =>
                    "For repeating surfaces such as walls or floors: Default, Repeat, mipmaps enabled. "
                    + "The image must already be seamless.",
                _ => string.Empty
            };
            return description + " Import settings apply only to exports saved inside Assets. "
                + "Compression, platform overrides and sprite slicing are not copied.";
        }

        private void UpdateSourceControls()
        {
            bool canSaveNextToSource = source != null && AssetDatabase.GetAssetPath(source).StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
            nextToSourceButton.SetEnabled(canSaveNextToSource);
            sourceHint.text = canSaveNextToSource
                ? "Exports always process at the source texture's full resolution."
                : "Save Next to Source is unavailable for textures outside Assets.";
        }

        private void SaveNextToSource()
        {
            string sourcePath = AssetDatabase.GetAssetPath(source);
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceFullPath = Path.GetFullPath(Path.Combine(projectRoot, sourcePath));
            string destination = Path.Combine(
                Path.GetDirectoryName(sourceFullPath),
                Path.GetFileNameWithoutExtension(sourceFullPath) + "_processed." + Extension);
            Export(destination);
        }

        private void SaveInAssets()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Export Texture Lab Result",
                source.name + "_processed",
                Extension,
                "Choose an Assets location for the processed texture.",
                "Assets");
            if (!string.IsNullOrEmpty(path))
                Export(ToFullPath(path));
        }

        private void SaveAnywhere()
        {
            string path = EditorUtility.SaveFilePanel(
                "Export Texture Lab Result",
                Application.dataPath,
                source.name + "_processed",
                Extension);
            if (!string.IsNullOrEmpty(path))
                Export(path);
        }

        private void Export(string destination)
        {
            destination = Path.ChangeExtension(destination, Extension);
            if (format == TextureExportFormat.Jpg && !EditorUtility.DisplayDialog(
                    "JPG Drops Alpha",
                    "JPG export permanently drops alpha. Continue?",
                    "Export JPG",
                    "Cancel"))
                return;

            if (File.Exists(destination) && !EditorUtility.DisplayDialog(
                    "Overwrite Texture",
                    $"Replace '{Path.GetFileName(destination)}'?",
                    "Overwrite",
                    "Cancel"))
                return;

            try
            {
                TextureExporter.Export(source, effects, destination, format, jpgQuality, importSettings, filterMode);
                exported?.Invoke();
                ShowNotification(new GUIContent("Export complete."));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                ShowNotification(new GUIContent("Export failed. See Console."));
            }
        }

        private string Extension => format == TextureExportFormat.Png ? "png" : "jpg";

        private static string ToFullPath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static List<TextureEffectData> CopyEffects(IReadOnlyList<TextureEffectData> source)
        {
            var copies = new List<TextureEffectData>(source.Count);
            foreach (TextureEffectData effect in source)
                copies.Add(effect.Duplicate());
            return copies;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextureLab.Editor
{
    internal enum PreviewChannel
    {
        RGB,
        R,
        G,
        B,
        Alpha,
        Luminance
    }

    [FilePath("Library/TextureLabSession.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class TextureLabSession : ScriptableSingleton<TextureLabSession>
    {
        [SerializeField] private Texture2D sourceTexture;
        [SerializeReference] private List<TextureEffectData> effects = new();
        [SerializeField] private int previewTiles = 1;
        [SerializeField] private int previewZoom;
        [SerializeField] private PreviewChannel previewChannel;
        [SerializeField] private int previewMaxDimension = 1024;

        internal Texture2D SourceTexture { get => sourceTexture; set => sourceTexture = value; }
        internal List<TextureEffectData> Effects => effects ??= new List<TextureEffectData>();
        internal int PreviewTiles { get => previewTiles is >= 2 and <= 8 ? previewTiles : 1; set => previewTiles = value; }
        internal int PreviewZoom { get => previewZoom is 25 or 50 or 100 or 200 ? previewZoom : 0; set => previewZoom = value; }
        internal PreviewChannel PreviewChannel { get => previewChannel; set => previewChannel = value; }
        internal int PreviewMaxDimension { get => previewMaxDimension is 512 or 2048 ? previewMaxDimension : 1024; set => previewMaxDimension = value; }

        internal void ReplaceEffects(IEnumerable<TextureEffectData> source)
        {
            effects = new List<TextureEffectData>();
            foreach (TextureEffectData effect in source)
                effects.Add(effect.Duplicate());
        }

        internal void Persist() => Save(true);
    }
}

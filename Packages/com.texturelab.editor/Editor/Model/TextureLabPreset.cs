using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextureLab.Editor
{
    [Serializable]
    [CreateAssetMenu(fileName = "Texture Lab Preset", menuName = "Texture Lab/Preset")]
    public sealed class TextureLabPreset : ScriptableObject
    {
        [SerializeField] private int dataVersion = 1;
        [SerializeReference] private List<TextureEffectData> effects = new();

        internal int DataVersion => dataVersion;
        internal IReadOnlyList<TextureEffectData> Effects => effects ??= new List<TextureEffectData>();

        internal void SetEffects(IEnumerable<TextureEffectData> source)
        {
            effects = CopyEffects(source);
        }

        internal List<TextureEffectData> CreateEffectCopies() => CopyEffects(Effects);

        private static List<TextureEffectData> CopyEffects(IEnumerable<TextureEffectData> source)
        {
            var copies = new List<TextureEffectData>();
            foreach (TextureEffectData effect in source)
                copies.Add(effect.Duplicate());
            return copies;
        }
    }
}

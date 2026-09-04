using System.Collections.Generic;
using UnityEngine;

namespace TextureLab.Editor
{
    [CreateAssetMenu(fileName = "Texture Lab Palette", menuName = "Texture Lab/Palette")]
    public sealed class TextureLabPalette : ScriptableObject
    {
        [SerializeField] private List<Color> colors = new() { Color.black, Color.white };

        internal List<Color> Colors => colors ??= new List<Color>();

        private void OnValidate()
        {
            colors ??= new List<Color>();
            if (colors.Count > 64)
                colors.RemoveRange(64, colors.Count - 64);
            if (colors.Count == 0)
                colors.Add(Color.black);
        }
    }
}

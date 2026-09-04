using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace TextureLab.Editor
{
    internal readonly struct ProcessingContext
    {
        internal ProcessingContext(int originalWidth, int originalHeight)
        {
            OriginalWidth = originalWidth;
            OriginalHeight = originalHeight;
        }

        internal int OriginalWidth { get; }
        internal int OriginalHeight { get; }
    }

    internal interface ITextureEffectProcessor
    {
        void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context);
    }

    internal sealed class TextureProcessor : IDisposable
    {
        private static readonly int PreviewChannelId = Shader.PropertyToID("_PreviewChannel");
        private readonly PixelateProcessor pixelateProcessor;
        private readonly ColorEffectProcessor colorProcessor;
        private readonly PaletteQuantizeProcessor paletteProcessor;
        private readonly DitherProcessor ditherProcessor;
        private readonly NoiseProcessor noiseProcessor;
        private readonly GaussianBlurProcessor blurProcessor;
        private readonly SeamlessProcessor seamlessProcessor;
        private readonly ChannelMixerProcessor channelProcessor;
        private readonly ExposureBrushProcessor exposureBrushProcessor;
        private readonly Material displayMaterial;
        private readonly Dictionary<Type, ITextureEffectProcessor> processors;
        private RenderTexture targetA;
        private RenderTexture targetB;
        private RenderTexture displayTarget;

        internal TextureProcessor()
        {
            pixelateProcessor = new PixelateProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabPixelate.shader"));
            colorProcessor = new ColorEffectProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabColor.shader"));
            paletteProcessor = new PaletteQuantizeProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabPaletteQuantize.shader"));
            ditherProcessor = new DitherProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabDither.shader"));
            noiseProcessor = new NoiseProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabNoise.shader"));
            blurProcessor = new GaussianBlurProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabBlur.shader"));
            seamlessProcessor = new SeamlessProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabSeamless.shader"));
            Shader channelShader = LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabChannels.shader");
            channelProcessor = new ChannelMixerProcessor(channelShader);
            exposureBrushProcessor = new ExposureBrushProcessor(LoadShader("Packages/com.texturelab.editor/Shaders/TextureLabBrushExposure.shader"));
            displayMaterial = new Material(channelShader) { hideFlags = HideFlags.HideAndDontSave };
            processors = new Dictionary<Type, ITextureEffectProcessor>
            {
                [typeof(PixelateEffectData)] = pixelateProcessor,
                [typeof(PosterizeEffectData)] = colorProcessor,
                [typeof(LevelsEffectData)] = colorProcessor,
                [typeof(ColorAdjustmentsEffectData)] = colorProcessor,
                [typeof(ColorReplaceEffectData)] = colorProcessor,
                [typeof(PaletteQuantizeEffectData)] = paletteProcessor,
                [typeof(DitherEffectData)] = ditherProcessor,
                [typeof(NoiseEffectData)] = noiseProcessor,
                [typeof(GaussianBlurEffectData)] = blurProcessor,
                [typeof(OffsetEffectData)] = seamlessProcessor,
                [typeof(SeamBlendEffectData)] = seamlessProcessor,
                [typeof(ChannelRemapEffectData)] = channelProcessor,
                [typeof(ExposureBrushEffectData)] = exposureBrushProcessor
            };
        }

        internal RenderTexture Result { get; private set; }

        internal void Process(Texture2D source, IReadOnlyList<TextureEffectData> effects, int previewMaxDimension)
        {
            if (source == null)
            {
                Result = null;
                return;
            }

            GetPreviewSize(source.width, source.height, previewMaxDimension, out int width, out int height);
            EnsureTargets(width, height);
            Graphics.Blit(source, targetA);

            RenderTexture current = targetA;
            RenderTexture destination = targetB;
            var context = new ProcessingContext(source.width, source.height);

            foreach (TextureEffectData effect in effects)
            {
                if (!effect.Enabled || !processors.TryGetValue(effect.GetType(), out ITextureEffectProcessor processor))
                    continue;

                processor.Process(current, destination, effect, context);
                (current, destination) = (destination, current);
            }

            Result = current;
        }

        internal void ProcessFullResolution(Texture2D source, IReadOnlyList<TextureEffectData> effects)
        {
            Process(source, effects, Mathf.Max(source.width, source.height));
        }

        internal RenderTexture RenderDisplay(Texture source, PreviewChannel channel, int width, int height)
        {
            if (source == null)
                return null;

            EnsureDisplayTarget(width, height);
            displayMaterial.SetInt(PreviewChannelId, (int)channel);
            Graphics.Blit(source, displayTarget, displayMaterial, 1);
            return displayTarget;
        }

        public void Dispose()
        {
            pixelateProcessor.Dispose();
            colorProcessor.Dispose();
            paletteProcessor.Dispose();
            ditherProcessor.Dispose();
            noiseProcessor.Dispose();
            blurProcessor.Dispose();
            seamlessProcessor.Dispose();
            channelProcessor.Dispose();
            exposureBrushProcessor.Dispose();
            UnityEngine.Object.DestroyImmediate(displayMaterial);
            ReleaseTargets();
        }

        private static Shader LoadShader(string path)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            return shader != null ? shader : throw new InvalidOperationException($"Texture Lab shader is missing: {path}");
        }

        private static void GetPreviewSize(int sourceWidth, int sourceHeight, int maxDimension, out int width, out int height)
        {
            float scale = Mathf.Min(1f, Mathf.Max(1, maxDimension) / (float)Mathf.Max(sourceWidth, sourceHeight));
            width = Mathf.Max(1, Mathf.RoundToInt(sourceWidth * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(sourceHeight * scale));
        }

        private void EnsureTargets(int width, int height)
        {
            if (targetA != null && targetA.width == width && targetA.height == height)
                return;

            ReleaseTargets();
            targetA = CreateTarget("Texture Lab Preview A", width, height);
            targetB = CreateTarget("Texture Lab Preview B", width, height);
        }

        private void EnsureDisplayTarget(int width, int height)
        {
            if (displayTarget != null && displayTarget.width == width && displayTarget.height == height)
                return;

            ReleaseTarget(ref displayTarget);
            displayTarget = CreateTarget("Texture Lab Preview Display", width, height);
        }

        private static RenderTexture CreateTarget(string name, int width, int height)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            target.Create();
            return target;
        }

        private void ReleaseTargets()
        {
            ReleaseTarget(ref targetA);
            ReleaseTarget(ref targetB);
            ReleaseTarget(ref displayTarget);
            Result = null;
        }

        private static void ReleaseTarget(ref RenderTexture target)
        {
            if (target == null)
                return;

            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            target = null;
        }
    }

    internal sealed class PixelateProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int VirtualResolutionId = Shader.PropertyToID("_VirtualResolution");
        private readonly Material material;

        internal PixelateProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var pixelate = (PixelateEffectData)effect;
            GetVirtualSize(pixelate, context, out int virtualWidth, out int virtualHeight);
            virtualWidth = Mathf.Clamp(virtualWidth, 1, source.width);
            virtualHeight = Mathf.Clamp(virtualHeight, 1, source.height);

            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            material.SetVector(VirtualResolutionId, new Vector4(virtualWidth, virtualHeight, 0f, 0f));

            if (pixelate.Sampling == PixelSampling.Nearest)
            {
                Graphics.Blit(source, destination, material, 0);
                return;
            }

            RenderTexture reduced = RenderTexture.GetTemporary(
                virtualWidth,
                virtualHeight,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            reduced.filterMode = FilterMode.Point;
            reduced.wrapMode = TextureWrapMode.Clamp;

            try
            {
                Graphics.Blit(source, reduced, material, 1);
                material.SetVector(SourceSizeId, new Vector4(virtualWidth, virtualHeight, 0f, 0f));
                Graphics.Blit(reduced, destination, material, 0);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(reduced);
            }
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);

        private static void GetVirtualSize(PixelateEffectData effect, ProcessingContext context, out int width, out int height)
        {
            if (effect.Mode == PixelateMode.BlockSize)
            {
                int size = Mathf.Clamp(effect.BlockSize, 1, 64);
                width = Mathf.CeilToInt(context.OriginalWidth / (float)size);
                height = Mathf.CeilToInt(context.OriginalHeight / (float)size);
                return;
            }

            int target = Mathf.Clamp(effect.TargetResolution, 16, 2048);
            float scale = Mathf.Min(1f, target / (float)Mathf.Max(context.OriginalWidth, context.OriginalHeight));
            width = Mathf.Max(1, Mathf.RoundToInt(context.OriginalWidth * scale));
            height = Mathf.Max(1, Mathf.RoundToInt(context.OriginalHeight * scale));
        }
    }

    internal sealed class ColorEffectProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int BitsId = Shader.PropertyToID("_Bits");
        private static readonly int InputLevelsId = Shader.PropertyToID("_InputLevels");
        private static readonly int OutputLevelsId = Shader.PropertyToID("_OutputLevels");
        private static readonly int AdjustmentsId = Shader.PropertyToID("_Adjustments");
        private static readonly int ReplaceSourceId = Shader.PropertyToID("_ReplaceSource");
        private static readonly int ReplacementId = Shader.PropertyToID("_Replacement");
        private static readonly int ReplaceSettingsId = Shader.PropertyToID("_ReplaceSettings");
        private readonly Material material;

        internal ColorEffectProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));

            switch (effect)
            {
                case PosterizeEffectData posterize:
                    material.SetVector(BitsId, new Vector4(posterize.RedBits, posterize.GreenBits, posterize.BlueBits, 0f));
                    Graphics.Blit(source, destination, material, 0);
                    break;
                case LevelsEffectData levels:
                    material.SetVector(InputLevelsId, new Vector4(levels.BlackPoint, levels.WhitePoint, levels.Gamma, 0f));
                    material.SetVector(OutputLevelsId, new Vector4(levels.OutputBlack, levels.OutputWhite, 0f, 0f));
                    Graphics.Blit(source, destination, material, 1);
                    break;
                case ColorAdjustmentsEffectData adjustments:
                    material.SetVector(AdjustmentsId, new Vector4(adjustments.Brightness, adjustments.Contrast, adjustments.Gamma, 0f));
                    Graphics.Blit(source, destination, material, 2);
                    break;
                case ColorReplaceEffectData replace:
                    material.SetColor(ReplaceSourceId, replace.SourceColor);
                    material.SetColor(ReplacementId, replace.ReplacementColor);
                    material.SetVector(ReplaceSettingsId, new Vector4(replace.Tolerance, replace.Softness, replace.PreviewMask ? 1f : 0f, 0f));
                    Graphics.Blit(source, destination, material, 3);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(effect), effect.GetType().Name, "Unsupported color effect.");
            }
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class PaletteQuantizeProcessor : ITextureEffectProcessor, IDisposable
    {
        private const int MaximumColors = 64;
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int PaletteId = Shader.PropertyToID("_Palette");
        private static readonly int PaletteCountId = Shader.PropertyToID("_PaletteCount");
        private readonly Vector4[] paletteBuffer = new Vector4[MaximumColors];
        private readonly Material material;

        internal PaletteQuantizeProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var quantize = (PaletteQuantizeEffectData)effect;
            TextureLabPalette palette = quantize.Palette;
            int colorCount = palette == null ? 0 : Mathf.Min(MaximumColors, palette.Colors.Count);
            if (quantize.ColorLimit > 0)
                colorCount = Mathf.Min(colorCount, quantize.ColorLimit);
            if (colorCount == 0)
            {
                Graphics.Blit(source, destination);
                return;
            }

            for (int i = 0; i < colorCount; i++)
                paletteBuffer[i] = palette.Colors[i];

            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            material.SetVectorArray(PaletteId, paletteBuffer);
            material.SetInt(PaletteCountId, colorCount);
            Graphics.Blit(source, destination, material, 0);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class DitherProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int StrengthId = Shader.PropertyToID("_Strength");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int RgbId = Shader.PropertyToID("_Rgb");
        private readonly Material material;

        internal DitherProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var dither = (DitherEffectData)effect;
            float previewScale = source.width / (float)Mathf.Max(1, context.OriginalWidth);
            int scaledPattern = Mathf.Max(1, Mathf.RoundToInt(dither.Scale * previewScale));
            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            material.SetInt(PatternId, (int)dither.Pattern);
            material.SetFloat(StrengthId, dither.Strength);
            material.SetInt(ScaleId, scaledPattern);
            material.SetInt(SeedId, dither.Seed);
            material.SetInt(RgbId, dither.Channels == DitherChannels.RGB ? 1 : 0);
            Graphics.Blit(source, destination, material, 0);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class NoiseProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int NoiseTypeId = Shader.PropertyToID("_NoiseType");
        private static readonly int AmountId = Shader.PropertyToID("_Amount");
        private static readonly int ScaleId = Shader.PropertyToID("_Scale");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");
        private static readonly int RgbId = Shader.PropertyToID("_Rgb");
        private readonly Material material;

        internal NoiseProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var noise = (NoiseEffectData)effect;
            float previewScale = source.width / (float)Mathf.Max(1, context.OriginalWidth);
            int scaledNoise = Mathf.Max(1, Mathf.RoundToInt(noise.Scale * previewScale));
            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            material.SetInt(NoiseTypeId, (int)noise.Type);
            material.SetFloat(AmountId, noise.Amount);
            material.SetInt(ScaleId, scaledNoise);
            material.SetInt(SeedId, noise.Seed);
            material.SetInt(RgbId, noise.Channels == DitherChannels.RGB ? 1 : 0);
            Graphics.Blit(source, destination, material, 0);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class GaussianBlurProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private readonly Material material;

        internal GaussianBlurProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var blur = (GaussianBlurEffectData)effect;
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Default);
            temporary.filterMode = FilterMode.Bilinear;
            temporary.wrapMode = TextureWrapMode.Clamp;

            try
            {
                material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
                material.SetFloat(RadiusId, blur.Radius);
                Texture current = source;
                int iterations = Mathf.Clamp(blur.Iterations, 1, 4);
                for (int i = 0; i < iterations; i++)
                {
                    material.SetVector(DirectionId, Vector2.right);
                    Graphics.Blit(current, temporary, material, 0);
                    material.SetVector(DirectionId, Vector2.up);
                    Graphics.Blit(temporary, destination, material, 0);
                    current = destination;
                }
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temporary);
            }
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class SeamlessProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int OffsetId = Shader.PropertyToID("_Offset");
        private static readonly int SettingsId = Shader.PropertyToID("_Settings");
        private static readonly int BlendAlphaId = Shader.PropertyToID("_BlendAlpha");
        private readonly Material material;

        internal SeamlessProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            switch (effect)
            {
                case OffsetEffectData offset:
                    material.SetVector(OffsetId, new Vector4(offset.OffsetX, offset.OffsetY, 0f, 0f));
                    material.SetVector(SettingsId, new Vector4(0f, 0f, offset.Wrap == OffsetWrapMode.Repeat ? 1f : 0f, 0f));
                    Graphics.Blit(source, destination, material, 0);
                    break;
                case SeamBlendEffectData seamBlend:
                    material.SetVector(SettingsId, new Vector4(
                        seamBlend.BlendWidth,
                        seamBlend.BlendStrength,
                        seamBlend.Horizontal ? 1f : 0f,
                        seamBlend.Vertical ? 1f : 0f));
                    material.SetFloat(BlendAlphaId, seamBlend.BlendAlpha ? 1f : 0f);
                    Graphics.Blit(source, destination, material, 1);
                    break;
            }
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }

    internal sealed class ExposureBrushProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int ExposureMaskId = Shader.PropertyToID("_ExposureMask");
        private readonly BrushStrokeRasterizer rasterizer;
        private readonly Material material;

        internal ExposureBrushProcessor(Shader shader)
        {
            rasterizer = new BrushStrokeRasterizer(shader);
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var brush = (ExposureBrushEffectData)effect;
            RenderTexture mask = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.RHalf,
                RenderTextureReadWrite.Linear);
            mask.filterMode = FilterMode.Point;
            mask.wrapMode = TextureWrapMode.Clamp;

            try
            {
                rasterizer.Rasterize(mask, brush, context);
                material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
                material.SetTexture(ExposureMaskId, mask);
                Graphics.Blit(source, destination, material, 1);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(mask);
            }
        }

        public void Dispose()
        {
            rasterizer.Dispose();
            UnityEngine.Object.DestroyImmediate(material);
        }
    }

    internal sealed class ChannelMixerProcessor : ITextureEffectProcessor, IDisposable
    {
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int RedOutputId = Shader.PropertyToID("_RedOutput");
        private static readonly int GreenOutputId = Shader.PropertyToID("_GreenOutput");
        private static readonly int BlueOutputId = Shader.PropertyToID("_BlueOutput");
        private static readonly int ConstantsId = Shader.PropertyToID("_Constants");
        private static readonly int MixerSettingsId = Shader.PropertyToID("_MixerSettings");
        private static readonly int MonochromeMixId = Shader.PropertyToID("_MonochromeMix");
        private static readonly int AlphaOutputId = Shader.PropertyToID("_AlphaOutput");
        private readonly Material material;

        internal ChannelMixerProcessor(Shader shader)
        {
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void Process(Texture source, RenderTexture destination, TextureEffectData effect, ProcessingContext context)
        {
            var mixer = (ChannelRemapEffectData)effect;
            material.SetVector(SourceSizeId, new Vector4(source.width, source.height, 0f, 0f));
            material.SetVector(RedOutputId, mixer.RedOutput);
            material.SetVector(GreenOutputId, mixer.GreenOutput);
            material.SetVector(BlueOutputId, mixer.BlueOutput);
            material.SetVector(ConstantsId, new Vector4(
                mixer.Constants.x,
                mixer.Constants.y,
                mixer.Constants.z,
                mixer.MonochromeConstant));
            material.SetVector(MixerSettingsId, new Vector4(
                mixer.Strength,
                mixer.Monochrome ? 1f : 0f,
                mixer.AlphaMode == ChannelMixerAlphaMode.Mix ? 1f : 0f,
                mixer.AlphaConstant));
            material.SetVector(MonochromeMixId, mixer.MonochromeMix);
            material.SetVector(AlphaOutputId, mixer.AlphaOutput);
            Graphics.Blit(source, destination, material, 0);
        }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(material);
    }
}

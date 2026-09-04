using System;
using System.Collections.Generic;
using UnityEngine;

namespace TextureLab.Editor
{
    internal static class PaletteExtractor
    {
        private const int AnalysisMaxDimension = 128;
        private const int Iterations = 12;
        private const int DeterministicSeed = 0x544C;

        internal static List<Color> Extract(Texture2D source, int requestedColorCount)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            List<Vector3> samples = ReadSamples(source);
            if (samples.Count == 0)
                return new List<Color> { Color.black };

            int colorCount = Mathf.Clamp(requestedColorCount, 1, Mathf.Min(64, samples.Count));
            List<Vector3> centroids = InitializeCentroids(samples, colorCount);

            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                var sums = new Vector3[colorCount];
                var counts = new int[colorCount];
                var distances = new float[samples.Count];

                for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                {
                    int nearest = FindNearest(samples[sampleIndex], centroids, out float distance);
                    distances[sampleIndex] = distance;
                    sums[nearest] += samples[sampleIndex];
                    counts[nearest]++;
                }

                float largestMove = 0f;
                for (int centroidIndex = 0; centroidIndex < colorCount; centroidIndex++)
                {
                    Vector3 next = counts[centroidIndex] > 0
                        ? sums[centroidIndex] / counts[centroidIndex]
                        : samples[IndexOfLargest(distances)];
                    largestMove = Mathf.Max(largestMove, (centroids[centroidIndex] - next).sqrMagnitude);
                    centroids[centroidIndex] = next;
                }

                if (largestMove < 0.0000001f)
                    break;
            }

            centroids.Sort((left, right) => left.x.CompareTo(right.x));
            var result = new List<Color>(centroids.Count);
            foreach (Vector3 centroid in centroids)
            {
                Color color = OklabToLinearRgb(centroid);
                bool duplicate = result.Exists(existing => new Vector3(
                    existing.r - color.r,
                    existing.g - color.g,
                    existing.b - color.b).sqrMagnitude < 0.000001f);
                if (!duplicate)
                    result.Add(color);
            }

            return result.Count > 0 ? result : new List<Color> { Color.black };
        }

        private static List<Vector3> ReadSamples(Texture2D source)
        {
            float scale = Mathf.Min(1f, AnalysisMaxDimension / (float)Mathf.Max(source.width, source.height));
            int width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
            RenderTexture target = RenderTexture.GetTemporary(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            var readable = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            try
            {
                Graphics.Blit(source, target);
                RenderTexture.active = target;
                readable.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
                readable.Apply(false, false);

                Color[] pixels = readable.GetPixels();
                var samples = new List<Vector3>(pixels.Length);
                foreach (Color pixel in pixels)
                {
                    if (pixel.a >= 0.05f)
                        samples.Add(LinearRgbToOklab(pixel));
                }

                return samples;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        private static List<Vector3> InitializeCentroids(IReadOnlyList<Vector3> samples, int colorCount)
        {
            var random = new System.Random(DeterministicSeed);
            var centroids = new List<Vector3>(colorCount) { samples[random.Next(samples.Count)] };
            var nearestDistances = new float[samples.Count];

            for (int i = 0; i < nearestDistances.Length; i++)
                nearestDistances[i] = float.MaxValue;

            while (centroids.Count < colorCount)
            {
                Vector3 newest = centroids[^1];
                for (int sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
                {
                    float distance = (samples[sampleIndex] - newest).sqrMagnitude;
                    nearestDistances[sampleIndex] = Mathf.Min(nearestDistances[sampleIndex], distance);
                }

                centroids.Add(samples[IndexOfLargest(nearestDistances)]);
            }

            return centroids;
        }

        private static int FindNearest(Vector3 sample, IReadOnlyList<Vector3> centroids, out float nearestDistance)
        {
            int nearest = 0;
            nearestDistance = float.MaxValue;
            for (int i = 0; i < centroids.Count; i++)
            {
                float distance = (sample - centroids[i]).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearest = i;
                nearestDistance = distance;
            }

            return nearest;
        }

        private static int IndexOfLargest(IReadOnlyList<float> values)
        {
            int largestIndex = 0;
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] > values[largestIndex])
                    largestIndex = i;
            }

            return largestIndex;
        }

        private static Vector3 LinearRgbToOklab(Color color)
        {
            float l = 0.4122214708f * color.r + 0.5363325363f * color.g + 0.0514459929f * color.b;
            float m = 0.2119034982f * color.r + 0.6806995451f * color.g + 0.1073969566f * color.b;
            float s = 0.0883024619f * color.r + 0.2817188376f * color.g + 0.6299787005f * color.b;
            float lRoot = Mathf.Pow(Mathf.Max(l, 0f), 1f / 3f);
            float mRoot = Mathf.Pow(Mathf.Max(m, 0f), 1f / 3f);
            float sRoot = Mathf.Pow(Mathf.Max(s, 0f), 1f / 3f);
            return new Vector3(
                0.2104542553f * lRoot + 0.793617785f * mRoot - 0.0040720468f * sRoot,
                1.9779984951f * lRoot - 2.428592205f * mRoot + 0.4505937099f * sRoot,
                0.0259040371f * lRoot + 0.7827717662f * mRoot - 0.808675766f * sRoot);
        }

        private static Color OklabToLinearRgb(Vector3 lab)
        {
            float lRoot = lab.x + 0.3963377774f * lab.y + 0.2158037573f * lab.z;
            float mRoot = lab.x - 0.1055613458f * lab.y - 0.0638541728f * lab.z;
            float sRoot = lab.x - 0.0894841775f * lab.y - 1.291485548f * lab.z;
            float l = lRoot * lRoot * lRoot;
            float m = mRoot * mRoot * mRoot;
            float s = sRoot * sRoot * sRoot;
            return new Color(
                Mathf.Clamp01(4.0767416621f * l - 3.3077115913f * m + 0.2309699292f * s),
                Mathf.Clamp01(-1.2684380046f * l + 2.6097574011f * m - 0.3413193965f * s),
                Mathf.Clamp01(-0.0041960863f * l - 0.7034186147f * m + 1.707614701f * s),
                1f);
        }
    }
}

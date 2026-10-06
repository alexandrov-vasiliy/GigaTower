#ifndef BLURRY_VORONOI_INCLUDED
#define BLURRY_VORONOI_INCLUDED


// ---------------------------------------------------
// HASH
// ---------------------------------------------------

float hash31(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);

    return frac((p.x + p.y) * p.z);
}

float3 hash33(float3 p)
{
    p = float3(
        dot(p, float3(127.1, 311.7, 74.7)),
        dot(p, float3(269.5, 183.3, 246.1)),
        dot(p, float3(113.5, 271.9, 124.6))
    );

    return frac(sin(p) * 43758.5453);
}


// ---------------------------------------------------
// 3D VALUE NOISE
// ---------------------------------------------------

float noise3D(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);

    // smooth interpolation
    f = f * f * (3.0 - 2.0 * f);

    float n000 = hash31(i + float3(0,0,0));
    float n100 = hash31(i + float3(1,0,0));
    float n010 = hash31(i + float3(0,1,0));
    float n110 = hash31(i + float3(1,1,0));

    float n001 = hash31(i + float3(0,0,1));
    float n101 = hash31(i + float3(1,0,1));
    float n011 = hash31(i + float3(0,1,1));
    float n111 = hash31(i + float3(1,1,1));

    float x00 = lerp(n000, n100, f.x);
    float x10 = lerp(n010, n110, f.x);

    float x01 = lerp(n001, n101, f.x);
    float x11 = lerp(n011, n111, f.x);

    float y0 = lerp(x00, x10, f.y);
    float y1 = lerp(x01, x11, f.y);

    return lerp(y0, y1, f.z);
}


// ---------------------------------------------------
// RGB 3D NOISE
//
// Blender Noise Texture Color дает разные значения
// для XYZ/RGB, поэтому создаем три варианта noise.
// ---------------------------------------------------

float3 noiseColor3D(float3 p)
{
    return float3(
        noise3D(p),
        noise3D(p + float3(17.1, 31.7, 47.3)),
        noise3D(p + float3(61.4, 12.8, 93.1))
    );
}


// ---------------------------------------------------
// FBM
// ---------------------------------------------------

float3 fbm3D(float3 p)
{
    float3 result = 0;

    float amplitude = 0.5;
    float frequency = 1.0;

    [unroll]
    for (int i = 0; i < 4; i++)
    {
        result += noiseColor3D(p * frequency) * amplitude;

        frequency *= 2.0;
        amplitude *= 0.5;
    }

    return result;
}


// ---------------------------------------------------
// 3D VORONOI
// Manhattan distance like Blender screenshot
// ---------------------------------------------------

void voronoi3D(
    float3 p,
    out float distanceOut,
    out float3 colorOut,
    out float3 positionOut)
{
    float3 cell = floor(p);
    float3 local = frac(p);

    float minDistance = 99999.0;

    float3 nearestCell = 0;
    float3 nearestPoint = 0;

    [unroll]
    for (int z = -1; z <= 1; z++)
    {
        [unroll]
        for (int y = -1; y <= 1; y++)
        {
            [unroll]
            for (int x = -1; x <= 1; x++)
            {
                float3 neighbour = float3(x, y, z);

                float3 id = cell + neighbour;

                float3 randomPoint = hash33(id);

                float3 diff =
                    neighbour +
                    randomPoint -
                    local;


                // MANHATTAN DISTANCE
                float dist =
                    abs(diff.x) +
                    abs(diff.y) +
                    abs(diff.z);


                if (dist < minDistance)
                {
                    minDistance = dist;

                    nearestCell = id;
                    nearestPoint = id + randomPoint;
                }
            }
        }
    }

    distanceOut = minDistance;

    colorOut = hash33(nearestCell);

    positionOut = nearestPoint;
}


// ---------------------------------------------------
// MAIN FUNCTION FOR SHADER GRAPH
// ---------------------------------------------------

void BlurryVoronoi_float(
    float3 Coordinates,

    float LFFrequency,
    float LFAmplitude,

    float HFFrequency,
    float HFAmplitude,

    float VoronoiFrequency,

    out float Distance,
    out float3 Color,
    out float3 Position)
{
    float3 p = Coordinates;


    // ------------------------------
    // LOW FREQUENCY DISTORTION
    // ------------------------------

    float3 low =
        fbm3D(
            p * LFFrequency
        );


    // аналог Map Range 0..1 -> -1..1
    low = low * 2.0 - 1.0;

    p += low * LFAmplitude;



    // ------------------------------
    // HIGH FREQUENCY DISTORTION
    // ------------------------------

    float3 high =
        fbm3D(
            p * HFFrequency
        );

    high = high * 2.0 - 1.0;

    p += high * HFAmplitude;



    // ------------------------------
    // VORONOI
    // ------------------------------

    voronoi3D(
        p * VoronoiFrequency,
        Distance,
        Color,
        Position
    );
}


#endif
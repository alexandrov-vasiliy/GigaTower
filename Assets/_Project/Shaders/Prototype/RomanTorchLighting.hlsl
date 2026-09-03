#ifndef ROMAN_TORCH_LIGHTING_INCLUDED
#define ROMAN_TORCH_LIGHTING_INCLUDED

float4 _RomanTorchPositionRange;
float4 _RomanTorchSettings; // intervals, virtual screen height, blend, enabled

// A signed correction to URP Lit's direct lighting, connected to Emission.
// Native URP still handles GI, other lights, fog and material surface properties.
void RomanTorchLighting_float(float3 PositionWS, float3 NormalWS, float3 Albedo,
    float Metallic, float Smoothness, float Occlusion, float Alpha, out float3 Correction)
{
    Correction = 0;
#if !defined(SHADERGRAPH_PREVIEW) && defined(_ADDITIONAL_LIGHTS) && defined(UNIVERSAL_LIGHTING_INCLUDED) && (SHADERPASS == SHADERPASS_FORWARD)
    if (_RomanTorchSettings.w < 0.5 || _RomanTorchSettings.z <= 0)
        return;

    float3 normal = NormalizeNormalPerPixel(NormalWS);
    float4 clip = TransformWorldToHClip(PositionWS);
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    uv.y = lerp(uv.y, 1.0 - uv.y, _ProjectionParams.x < 0);
    float height = min(_ScaledScreenParams.y, _RomanTorchSettings.y);
    float2 grid = max(floor(float2(height * _ScaledScreenParams.x / _ScaledScreenParams.y, height)), 1);
    float2 snappedUV = (floor(uv * grid) + 0.5) / grid;
    float2 derivatives = float2(ddx(uv.x), ddy(uv.y));
    float2 offset = (snappedUV - uv) / (sign(derivatives) * max(abs(derivatives), 1e-6));
    float3 pixelPosition = PositionWS + ddx(PositionWS) * offset.x + ddy(PositionWS) * offset.y;
    float3 pixelNormal = normalize(normal + ddx(normal) * offset.x + ddy(normal) * offset.y);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = Albedo;
    surface.metallic = saturate(Metallic);
    surface.smoothness = saturate(Smoothness);
    surface.occlusion = Occlusion;
    surface.alpha = saturate(Alpha);
    BRDFData brdf;
    InitializeBRDFData(surface, brdf);

    InputData inputData = (InputData)0;
    inputData.positionWS = PositionWS;
    inputData.normalWS = normal;
    inputData.normalizedScreenSpaceUV = uv;
    AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData, surface);
    half4 shadowMask = CalculateShadowMask(inputData);
    float3 viewDirection = GetWorldSpaceNormalizeViewDir(PositionWS);
    float referenceDistanceSquared = max(_RomanTorchPositionRange.w * _RomanTorchPositionRange.w * 0.0625, 0.0001);

    // ponytail: this prototype designates one torch by position. Coincident lights
    // would share the effect; use a dedicated rendering-layer marker for that case.
    uint count = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(count)
#if USE_CLUSTER_LIGHT_LOOP
        int index = lightIndex;
#else
        int index = GetPerObjectLightIndex(lightIndex);
#endif
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
        float4 lightPosition = _AdditionalLightsBuffer[index].position;
#else
        float4 lightPosition = _AdditionalLightsPosition[index];
#endif
        float3 difference = lightPosition.xyz - _RomanTorchPositionRange.xyz;
        if (lightPosition.w > 0.5 && dot(difference, difference) < 0.000001)
        {
            Light original = GetAdditionalLight(lightIndex, inputData, shadowMask, ao);
#if defined(_LIGHT_LAYERS)
            if (!IsMatchingLightLayer(original.layerMask, GetMeshRenderingLayer()))
                continue;
#endif
            InputData pixelInput = inputData;
            pixelInput.positionWS = pixelPosition;
            Light pixel = GetAdditionalLight(lightIndex, pixelInput, shadowMask, ao);
            float irradiance = pixel.distanceAttenuation * saturate(dot(pixelNormal, pixel.direction));
            float band = floor(saturate(irradiance * referenceDistanceSquared) * _RomanTorchSettings.x + 0.5)
                / (_RomanTorchSettings.x * referenceDistanceSquared);
            half3 reflectance = brdf.diffuse;
#ifndef _SPECULARHIGHLIGHTS_OFF
            reflectance += brdf.specular * DirectBRDFSpecular(brdf, pixelNormal, pixel.direction,
                GetWorldSpaceNormalizeViewDir(pixelPosition));
#endif
            float3 stepped = reflectance * pixel.color * (band * pixel.shadowAttenuation);
            Correction += (stepped - LightingPhysicallyBased(brdf, original, normal, viewDirection))
                * _RomanTorchSettings.z;
        }
    LIGHT_LOOP_END
#endif
}

#endif

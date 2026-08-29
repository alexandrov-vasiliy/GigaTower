Shader "GigaTower/Particles/Steam Spray"
{
    Properties
    {
        _BaseMap ("Particle Texture", 2D) = "white" {}
        [HDR] _Tint ("Tint", Color) = (0.85, 0.95, 1, 0.55)
        _Brightness ("Brightness", Range(0, 4)) = 1
        _SoftEdge ("Soft Edge", Range(0, 1)) = 0.65
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "SteamSpray"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _Tint;
                half _Brightness;
                half _SoftEdge;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 textureSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float2 centered = input.uv * 2.0 - 1.0;
                half radial = saturate(1.0 - dot(centered, centered));
                radial = smoothstep(0.0, lerp(0.05, 0.95, _SoftEdge), radial);
                half4 color = textureSample * input.color * _Tint;
                color.rgb *= _Brightness;
                color.a *= radial;
                return color;
            }
            ENDHLSL
        }
    }
}

Shader "Hidden/Universal Render Pipeline/Bloom"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
    }

    HLSLINCLUDE

        #pragma multi_compile_local _ _USE_RGBM

        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Filtering.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

        TEXTURE2D_X(_MainTex);
        TEXTURE2D_X(_MainTexLowMip);

        float4 _MainTex_TexelSize;
        float4 _MainTexLowMip_TexelSize;

        float4 _Params; // x: scatter, y: clamp, z: threshold (linear), w: threshold knee

        #define Scatter             _Params.x
        #define ClampMax            _Params.y
        #define Threshold           _Params.z
        #define ThresholdKnee       _Params.w

        half _Offset;

        half4 EncodeHDR(half3 color)
        {
        #if _USE_RGBM
            half4 outColor = EncodeRGBM(color);
        #else
            half4 outColor = half4(color, 1.0);
        #endif

        #if UNITY_COLORSPACE_GAMMA
            return half4(sqrt(outColor.xyz), outColor.w); // linear to γ
        #else
            return outColor;
        #endif
        }

        half3 DecodeHDR(half4 color)
        {
        #if UNITY_COLORSPACE_GAMMA
            color.xyz *= color.xyz; // γ to linear
        #endif

        #if _USE_RGBM
            return DecodeRGBM(color);
        #else
            return color.xyz;
        #endif
        }

        half4 FragPrefilter(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);
            half3 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv).xyz;

        #if UNITY_COLORSPACE_GAMMA
            color = SRGBToLinear(color);
        #endif

            // User controlled clamp to limit crazy high broken spec
            color = min(ClampMax, color);

            // Thresholding
            half brightness = Max3(color.r, color.g, color.b);
            half softness = clamp(brightness - Threshold + ThresholdKnee, 0.0, 2.0 * ThresholdKnee);
            softness = (softness * softness) / (4.0 * ThresholdKnee + 1e-4);
            half multiplier = max(brightness - Threshold, softness) / max(brightness, 1e-4);
            color *= multiplier;

            return EncodeHDR(color);
        }

        struct v2f_DownSample
        {
            float4 positionCS: SV_POSITION;
            float2 uv: TEXCOORD0;
            float4 uv01: TEXCOORD1;
            float4 uv23: TEXCOORD2;
        };

        struct v2f_UpSample
        {
            float4 positionCS: SV_POSITION;
            float2 uv: TEXCOORD0;
            float4 uv01: TEXCOORD1;
            float4 uv23: TEXCOORD2;
            float4 uv45: TEXCOORD3;
            float4 uv67: TEXCOORD4;
        };

        v2f_DownSample Vert_DownSample(Attributes input)
        {
            v2f_DownSample output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

            _MainTex_TexelSize *= 0.5;
            output.uv = input.uv;
            output.uv01.xy = input.uv - _MainTex_TexelSize * float2(1 + _Offset, 1 + _Offset);//top right
            output.uv01.zw = input.uv + _MainTex_TexelSize * float2(1 + _Offset, 1 + _Offset);//bottom left
            output.uv23.xy = input.uv - float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * float2(1 + _Offset, 1 + _Offset);//top left
            output.uv23.zw = input.uv + float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * float2(1 + _Offset, 1 + _Offset);//bottom right

            return output;
        }

        half4 Frag_DownSample(v2f_DownSample input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            float texelSize = _MainTex_TexelSize.x;
            float2 uv = UnityStereoTransformScreenSpaceTex(input.uv);

            half3 color = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv)) * 4;
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.xy));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.zw));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.xy));
            color += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.zw));
            color *= 0.125;

            return EncodeHDR(color);
        }

        v2f_UpSample Vert_UpSample(Attributes input)
        {
            v2f_UpSample output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;

            _MainTex_TexelSize *= 0.5;

            _Offset = float2(1 + _Offset, 1 + _Offset);

            output.uv01.xy = input.uv + float2(-_MainTex_TexelSize.x * 2, 0) * _Offset;
            output.uv01.zw = input.uv + float2(-_MainTex_TexelSize.x, _MainTex_TexelSize.y) * _Offset;
            output.uv23.xy = input.uv + float2(0, _MainTex_TexelSize.y * 2) * _Offset;
            output.uv23.zw = input.uv + _MainTex_TexelSize * _Offset;
            output.uv45.xy = input.uv + float2(_MainTex_TexelSize.x * 2, 0) * _Offset;
            output.uv45.zw = input.uv + float2(_MainTex_TexelSize.x, -_MainTex_TexelSize.y) * _Offset;
            output.uv67.xy = input.uv + float2(0, -_MainTex_TexelSize.y * 2) * _Offset;
            output.uv67.zw = input.uv - _MainTex_TexelSize * _Offset;

            return output;
        }

        half4 Frag_UpSample(v2f_UpSample input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            half3 highMip = 0;
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.xy));
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv01.zw)) * 2;
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.xy));
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv23.zw)) * 2;
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv45.xy));
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv45.zw)) * 2;
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv67.xy));
            highMip += DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv67.zw)) * 2;
            highMip *= 0.0833;

            half3 lowMip = DecodeHDR(SAMPLE_TEXTURE2D_X(_MainTexLowMip, sampler_LinearClamp, input.uv));

            return EncodeHDR(lerp(highMip, lowMip, Scatter));
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "Bloom Prefilter"

            HLSLPROGRAM
                #pragma vertex Vert
                #pragma fragment FragPrefilter
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Downsample"

            HLSLPROGRAM
                #pragma vertex Vert_DownSample
                #pragma fragment Frag_DownSample
            ENDHLSL
        }

        Pass
        {
            Name "Bloom Upsample"

            HLSLPROGRAM
                #pragma vertex Vert_UpSample
                #pragma fragment Frag_UpSample
            ENDHLSL
        }
    }
}

Shader "K4RMA/SwordBodyConsistent"
{
    Properties
    {
        [PerRendererData] _MainTex ("Captured sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _WoodGain ("Own Idle wood gain", Vector) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _WoodGain;
            CBUFFER_END
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o = (Output)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(UnityFlipSprite(v.vertex, unity_SpriteProps.xy));
                o.uv = v.uv;
                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half3 rgb = source.rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = LinearToSRGB(rgb);
                #endif
                // Leave blue cloth, neutral outlines and white blade untouched.
                half wood = smoothstep(1.08h,1.14h,rgb.r/max(rgb.g,0.001h))
                    * smoothstep(1.04h,1.10h,rgb.g/max(rgb.b,0.001h)) * smoothstep(0.25h,0.35h,rgb.r);
                rgb *= lerp(half3(1,1,1),_WoodGain.rgb,wood);
                #ifndef UNITY_COLORSPACE_GAMMA
                rgb = SRGBToLinear(rgb);
                #endif
                return half4(rgb * i.color.rgb, source.a * i.color.a);
            }
            ENDHLSL
        }
    }
}

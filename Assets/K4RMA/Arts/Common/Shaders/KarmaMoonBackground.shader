Shader "K4RMA/MoonBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MoonRegion ("Moon UV center and radii", Vector) = (0.548,0.66,0.0118,0.02095)
        _MoonStretch ("World pixel aspect", Float) = 1
        _DiskOnly ("Isolated disk", Float) = 1
        _SkyRight ("Right sky sample radius", Float) = 1.5
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            // Preserve the background colors with an unlit sprite pass.
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
                float4 _MoonRegion;
                float _MoonStretch;
                float _DiskOnly;
                float _SkyRight;
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
                float2 n = (i.uv - _MoonRegion.xy) / _MoonRegion.zw;
                if (_DiskOnly < 0.5)
                {
                    half4 original = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                    float radius = length(n);
                    if (radius >= 1.6) return original * i.color;
                    float2 leftUV = float2(_MoonRegion.x - _MoonRegion.z * 2.4, i.uv.y);
                    float2 rightUV = float2(_MoonRegion.x + _MoonRegion.z * _SkyRight, i.uv.y);
                    float2 blur = float2(0, _MoonRegion.w * 0.3);
                    half4 leftSky = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, leftUV - blur)
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, leftUV) * 2
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, leftUV + blur)) * 0.25;
                    half4 rightSky = (SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rightUV - blur)
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rightUV) * 2
                        + SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, rightUV + blur)) * 0.25;
                    float leftLight = dot(leftSky.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    float rightLight = dot(rightSky.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    if (rightLight < leftLight * 0.65) rightSky = leftSky;
                    half4 sky = lerp(leftSky, rightSky, saturate((n.x + 2.4) / (2.4 + _SkyRight)));
                    float mask = 1 - smoothstep(1.08, 1.6, radius);
                    // Keep dark foreground details (e.g. the hall rope) in front of the sky.
                    float originalLight = dot(original.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    float skyLight = dot(sky.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                    mask *= smoothstep(0.45, 0.8, originalLight / max(0.0001, skyLight));
                    return lerp(original, sky, mask) * i.color;
                }
                float factor = sqrt(max(_MoonStretch, 0.01));
                float2 corrected = n * float2(factor, 1.0 / factor);
                float distance = length(corrected);
                // Transparent outside the disk: no sky patch, no reconstructed oval, no halo.
                float mask = 1 - smoothstep(0.99, 1.015, distance);
                half4 moon = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex,
                    _MoonRegion.xy + corrected * _MoonRegion.zw);
                moon.rgb = lerp(moon.rgb, half3(0.82h, 0.75h, 0.55h), smoothstep(0.96, 1.0, distance));
                moon.a *= mask;
                return moon * i.color;
            }
            ENDHLSL
        }
    }
}

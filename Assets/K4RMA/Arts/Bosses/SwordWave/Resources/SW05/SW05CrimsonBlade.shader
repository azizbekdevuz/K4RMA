Shader "K4RMA/SW05/CrimsonBlade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Formation ("Harmless sword formation", Range(0,1)) = 1
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
            #include "SW05CrimsonPalette.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Formation;
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
                // Grow the image from the hilt toward its downward tip. The
                // default is fully formed for falling swords and impact frames.
                half boundary = 1 - _Formation;
                half reveal = smoothstep(boundary - 0.025, boundary + 0.025, i.uv.y);
                half edge = (1 - smoothstep(0, 0.035, abs(i.uv.y - boundary)))
                    * step(0.001, _Formation) * (1 - step(0.999, _Formation));
                source.rgb = lerp(source.rgb, half3(1, 0.87, 0.62), edge * 0.85);
                source.a *= lerp(reveal, 1, step(0.999, _Formation));
                return half4(SW05CrimsonPalette(source.rgb), source.a) * i.color;
            }
            ENDHLSL
        }
    }
}

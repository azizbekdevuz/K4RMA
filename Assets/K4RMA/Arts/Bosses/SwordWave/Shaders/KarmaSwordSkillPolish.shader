Shader "K4RMA/SwordSkillPolish"
{
    Properties
    {
        [PerRendererData] _MainTex ("Authored skill sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _Style ("Basic / Stagger / Aimed / Counter / Flash / Scar / Ward", Float) = 0
        [PerRendererData] _Phase ("Cosmetic age", Float) = 0
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
            #include "../Resources/SW05/SW05CrimsonPalette.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Style, _Phase;
            CBUFFER_END
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v); SetUpSpriteInstanceProperties();
                Output o = (Output)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(UnityFlipSprite(v.vertex, unity_SpriteProps.xy));
                o.uv = v.uv; o.color = v.color * _Color * unity_SpriteColor; return o;
            }
            half4 frag(Output i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                tex.rgb = SW05CrimsonPalette(tex.rgb);
                float2 p = i.uv * 2 - 1;
                float grain = 0.5 + 0.5 * sin(i.uv.y * 115 + i.uv.x * 39 - _Phase * 22);
                float sweep = pow(saturate(1 - abs(frac(i.uv.y * 0.65 + i.uv.x * 0.3 - _Phase * 1.8) * 2 - 1)), 12);
                half3 hot = half3(0.55, 1.25, 1.65);
                float energy = 0.09 + sweep * 0.23 + grain * 0.035;
                if (_Style > 0.5 && _Style < 1.5)
                {
                    // Two offset filaments read as a paired, staggered wave.
                    float braid = pow(saturate(cos(i.uv.y * 27 - i.uv.x * 9 + _Phase * 18)), 12);
                    energy = 0.15 + braid * 0.52 + sweep * 0.20;
                    hot = half3(0.48, 1.25, 1.75);
                }
                else if (_Style > 1.5 && _Style < 2.5)
                {
                    // Compressed white cutting spine, azure perimeter retained.
                    float spine = exp(-p.x * p.x * 18);
                    energy = 0.32 + spine * 0.35 + sweep * 0.32;
                    hot = half3(1.3, 1.85, 2.1);
                }
                else if (_Style > 2.5 && _Style < 3.5)
                {
                    // Steel glint sweeps along a counter weapon, not a crescent.
                    energy = 0.10 + sweep * 0.55;
                    hot = half3(0.73, 1.35, 1.55);
                }
                else if (_Style > 3.5 && _Style < 4.5)
                {
                    energy = 0.55 * exp(-_Phase * 13) + grain * 0.08;
                    hot = half3(0.9, 1.65, 2.0);
                }
                else if (_Style > 4.5 && _Style < 5.5)
                {
                    float fissure = exp(-abs(p.y) * 12);
                    float veins = pow(saturate(cos(p.x * 35 - _Phase * 14)), 10);
                    energy = 0.14 + fissure * (0.30 + veins * 0.26);
                    hot = half3(0.35, 1.25, 1.9);
                }
                else if (_Style > 5.5)
                {
                    float angle = atan2(p.y, p.x);
                    energy = 0.12 + pow(saturate(cos(angle * 8 - _Phase * 5)), 16) * 0.42;
                    hot = half3(0.63, 1.45, 1.8);
                }
                // RGB animation never expands/hides the authored alpha edge.
                float mask = saturate(dot(tex.rgb, half3(0.25,0.45,0.3)) * 1.5);
                half3 rgb = lerp(tex.rgb, hot, saturate(energy * mask));
                return half4(rgb * i.color.rgb, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}

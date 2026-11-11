Shader "K4RMA/SW05/EmbeddedWood"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _GroundY ("Wood surface height", Float) = 0.035
        _ContactShadow ("Contact shadow", Float) = 0
        _Dissolve ("Harmless planted sword dissolve", Range(0,1)) = 0
        _BladePresence ("Blade-only presence", Range(0,1)) = 1
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
                float _GroundY, _ContactShadow, _Dissolve, _BladePresence;
            CBUFFER_END
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; float worldY : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o = (Output)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = TransformObjectToHClip(UnityFlipSprite(v.vertex, unity_SpriteProps.xy));
                o.uv = v.uv;
                o.worldY = TransformObjectToWorld(v.vertex).y;
                o.color = v.color * _Color * unity_SpriteColor;
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                clip(i.worldY - _GroundY);
                if (_ContactShadow > 0.5)
                {
                    float2 p = i.uv * 2 - 1;
                    half alpha = exp(-dot(p,p) * 4) * (1-smoothstep(0.8, 1, length(p)));
                    return half4(i.color.rgb, alpha * i.color.a);
                }
                float2 uv = SW05BladeUV(i.uv,_BladePresence);
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                // Dissolve upward within the existing 0.65 s harmless linger.
                float grain = frac(sin(dot(floor(i.uv * float2(90, 220)), float2(12.9898, 78.233))) * 43758.5453);
                float mask = i.uv.y * 0.85 + grain * 0.15;
                source.a *= smoothstep(_Dissolve - 0.03, _Dissolve + 0.03, mask);
                return half4(SW05BladeRadiance(source.rgb,uv,_BladePresence), source.a) * i.color;
            }
            ENDHLSL
        }
    }
}

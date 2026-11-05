Shader "K4RMA/SW05/Celestial"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Float) = 0
        _Mode ("Sigil / Wake / Pillar / Contact", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            // SRPDefaultUnlit works for sprites and the LineRenderer warning outlines.
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
                float _Progress, _Mode;
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
                float2 p = i.uv * 2 - 1;
                float r = length(p), a = atan2(p.y, p.x);
                float fade = 1 - smoothstep(0.35, 1, _Progress);
                float light;
                if (_Mode < 0.5)
                {
                    float segments = smoothstep(0.12, 0.28, abs(sin(a * 4 + _Progress * 5)));
                    float ring = exp(-abs(r - 0.72) * 110) + exp(-abs(r - 0.86) * 150) * 0.58 * segments;
                    float runes = pow(saturate(cos(a * 12 - _Progress * 6)), 18) * exp(-abs(r - 0.79) * 100);
                    float inner = exp(-abs(r - 0.48) * 140) * pow(saturate(cos(a * 4 + _Progress * 4)), 3);
                    float star = exp(-abs(p.x) * 55) * exp(-abs(p.y) * 3)
                               + exp(-abs(p.y) * 55) * exp(-abs(p.x) * 3);
                    light = (ring + runes + inner * 0.33 + star * 0.32 + exp(-r*r*14)*0.08) * fade;
                }
                else if (_Mode < 1.5)
                {
                    // Soft envelope, bright central filament and forked wisps.
                    float taper = saturate(1 - abs(p.y));
                    light = (exp(-p.x*p.x*110) + exp(-p.x*p.x*12)*0.16) * taper*taper * 0.72;
                    light += exp(-abs(abs(p.x) - 0.15 - 0.045*sin(p.y*19 - _Progress*16))*95) * taper * 0.21;
                }
                else if (_Mode < 2.5)
                {
                    float curtain = exp(-p.x*p.x*14) * 0.28;
                    float ribs = pow(saturate(cos(p.x*25 + sin(p.y*4 - _Progress*5))), 24) * 0.42;
                    float dust = pow(saturate(sin(p.x*51 + p.y*23 - _Progress*40)), 45) * 0.33;
                    float envelope = smoothstep(0, 0.15, _Progress) * (1-smoothstep(0.7, 1, _Progress));
                    light = (curtain + ribs + dust) * envelope * (1-p.y*p.y);
                }
                else
                {
                    float radius = lerp(0.12, 0.85, 1-pow(1-saturate(_Progress),3));
                    light = exp(-abs(r-radius)*65) * (1-_Progress)*(1-_Progress);
                    light += exp(-r*r*35) * exp(-_Progress*10) * 0.45;
                }
                light *= 1 - smoothstep(0.9, 1, max(abs(p.x), abs(p.y)));
                half3 tint = lerp(i.color.rgb, half3(1.55,0.78,0.38), saturate(light)*0.38);
                return half4(tint * (1 + light*0.35), saturate(light) * i.color.a);
            }
            ENDHLSL
        }
    }
}

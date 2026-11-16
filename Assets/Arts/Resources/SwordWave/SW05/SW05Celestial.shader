Shader "K4RMA/SW05/Celestial"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Progress ("Progress", Float) = 0
        _Mode ("Sigil / Wake / Pillar / Contact", Float) = 0
        _ClipRect ("Packed energy world bounds", Vector) = (-10000,-10000,10000,10000)
        _FrameUV ("Animated sprite UV rectangle", Vector) = (0,0,1,1)
        _PackStyle ("Packed energy style", Float) = 0
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
                float4 _ClipRect;
                float4 _FrameUV;
                float _PackStyle;
            CBUFFER_END
            struct Input { float3 vertex : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o = (Output)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                bool mesh = _Mode > 5.5 && _Mode < 6.5;
                o.vertex = mesh ? TransformObjectToHClip(v.vertex)
                    : TransformObjectToHClip(UnityFlipSprite(v.vertex, unity_SpriteProps.xy));
                o.uv = v.uv;
                o.world = TransformObjectToWorld(v.vertex).xy;
                o.color = v.color * _Color * (mesh ? half4(1,1,1,1) : unity_SpriteColor);
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                // Procedural SW05 ribbons use vertex color and never depend on
                // SpriteRenderer-only flip/color state or texture coordinates.
                if (_Mode > 5.5 && _Mode < 6.5) return i.color;
                if (_Mode > 6.5)
                {
                    clip(min(i.world.x-_ClipRect.x,_ClipRect.z-i.world.x));
                    clip(min(i.world.y-_ClipRect.y,_ClipRect.w-i.world.y));
                    half4 source = SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                    float2 uv = (i.uv-_FrameUV.xy)/max(_FrameUV.zw,float2(0.0001,0.0001));
                    if (_Mode > 7.5)
                    {
                        float r=length((uv-0.5)*2);
                        half detail=smoothstep(0.2h,0.8h,max(source.r,source.g));
                        half hole=smoothstep(0.19,0.30,r);
                        half ring=exp(-abs(r-0.43)*85);
                        half alpha=source.a*hole*(0.15h+detail*0.85h);
                        if(_PackStyle>0.5)alpha=ring*source.a;
                        return half4(source.rgb*i.color.rgb*(1+detail*0.3),alpha*i.color.a);
                    }
                    // Preserve the supplied animation's outlines and ring detail
                    // while keeping its broad cyan fill translucent over steel.
                    half detail = smoothstep(0.18h,0.78h,source.r);
                    half3 energy = lerp(half3(0.08h,0.65h,1.4h),half3(0.9h,1.25h,1.5h),detail);
                    half fill=_PackStyle>4.5?0.42h:0.28h;
                    if(_PackStyle>1.5&&_PackStyle<2.5)fill=0.10h;
                    half ignition=_PackStyle>4.5?exp(-abs(_Progress-0.3)*9):0;
                    energy=lerp(energy,half3(1.15h,1.4h,1.55h),ignition*0.5h);
                    return half4(energy*i.color.rgb,source.a*(fill+detail*(1-fill))*i.color.a);
                }
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
                    // Short straight cutting filament with angular discharges.
                    float taper = saturate(1 - abs(p.y));
                    float jag = abs(frac(p.y*5 + floor(_Progress*12)*0.37)*2-1);
                    light = (exp(-p.x*p.x*150) + exp(-p.x*p.x*22)*0.14) * taper * 1.1;
                    light += exp(-abs(abs(p.x) - 0.12 - jag*0.16)*95) * taper * 0.65;
                }
                else if (_Mode < 2.5)
                {
                    float curtain = exp(-p.x*p.x*14) * 0.28;
                    float ribs = pow(saturate(cos(p.x*25 + sin(p.y*4 - _Progress*5))), 24) * 0.42;
                    float dust = pow(saturate(sin(p.x*51 + p.y*23 - _Progress*40)), 45) * 0.33;
                    float envelope = smoothstep(0, 0.15, _Progress) * (1-smoothstep(0.7, 1, _Progress));
                    light = (curtain + ribs + dust) * envelope * (1-p.y*p.y);
                }
                else if (_Mode < 3.5)
                {
                    float radius = lerp(0.12, 0.85, 1-pow(1-saturate(_Progress),3));
                    light = exp(-abs(r-radius)*65) * (1-_Progress)*(1-_Progress);
                    light += exp(-r*r*35) * exp(-_Progress*10) * 0.45;
                }
                else if (_Mode < 4.5)
                {
                    // Full-height damage column. The exact rectangle also
                    // supplies the charge mesh's ceiling and side boundaries.
                    float front = lerp(0.12, 0.88, saturate(_Progress * 3));
                    float edge = exp(-abs(abs(p.x) - front) * 32);
                    float body = 1 - smoothstep(0.88, 1, abs(p.y));
                    float onset = exp(-_Progress * 10);
                    float cuts = pow(saturate(cos(p.x*27 + sin(p.y*9-_Progress*25))),12);
                    light = (body * (0.3 + 3.2 * onset + cuts*0.7) + edge*1.8) * (1-smoothstep(0.12, 0.85, _Progress));
                }
                else
                {
                    // Locked rectangular bounds: the bright end bars coincide
                    // with the real zone edges rather than an inset ellipse.
                    float ends = smoothstep(0.88,0.98,abs(p.x));
                    float rails = exp(-abs(abs(p.y)-0.65)*35);
                    float pulse = 0.68 + 0.32*sin(_Progress*22);
                    float chevrons = pow(saturate(cos(p.x*18-abs(p.y)*5-_Progress*12)),12);
                    light = (ends + rails*0.65 + chevrons*0.27 + 0.12)*pulse;
                    light *= 1-smoothstep(0.86,1,abs(p.y));
                }
                if (_Mode < 4.5) light *= 1 - smoothstep(0.9, 1, max(abs(p.x), abs(p.y)));
                half3 tint = lerp(i.color.rgb, half3(0.55,1.25,1.55), saturate(light)*0.38);
                return half4(tint * (1 + light*0.35), saturate(light) * i.color.a);
            }
            ENDHLSL
        }
    }
}

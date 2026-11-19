Shader "K4RMA/Guard/FloatingRing"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original sprite", 2D) = "white" {}
        _RingTex ("Independent wooden ring", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Shield ("Shield ellipse", Vector) = (0,0,1,1)
        _Ring ("Rotation, charge, tilt, enabled", Vector) = (0,0,0,0)
        _TextureAspect ("Texture aspect", Float) = 1
        _Perspective ("Shield foreshortening", Float) = 0.28
        _RearArc ("Rear rim occlusion", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="False" }
        Cull Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_RingTex); SAMPLER(sampler_RingTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _Shield, _Ring;
                float _TextureAspect, _Perspective, _RearArc;
            CBUFFER_END
            struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o=(Output)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=TransformObjectToHClip(UnityFlipSprite(v.vertex,unity_SpriteProps.xy));
                o.uv=v.uv; o.color=v.color*_Color*unity_SpriteColor;
                return o;
            }
            float2 Turn(float2 p,float a)
            {
                float s,c; sincos(a,s,c);
                return float2(c*p.x-s*p.y,s*p.x+c*p.y);
            }
            half4 frag(Output i):SV_Target
            {
                half4 original=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float2 aspect=float2(_TextureAspect,1);
                float2 p=Turn((i.uv-_Shield.xy)*aspect,-_Ring.z)/(_Shield.zw*aspect);
                p/=max(0.1,1-_Perspective*p.x);
                float r=length(p);
                if (_Ring.w<0.5 || r>1.04 || r<0.55) return original*i.color;
                // The windup arm is in front of the far half of the shield.
                if (_RearArc>0.5 && p.x<0.15) return original*i.color;
                float aa=max(fwidth(r),0.003);
                float inner=0.61+0.012*_Ring.y;
                // Original center boss and inner gold circle stay fixed. The independently authored
                // ring carries the outer gold circle and complete straps, with no severed old tips.
                if (r<inner)
                {
                    float shadow=smoothstep(0.55,0.55+aa,r);
                    original.rgb=lerp(original.rgb,half3(0.035,0.029,0.023),shadow);
                    return original*i.color;
                }
                float radius=lerp(0.61,0.985,saturate((r-inner)/(1.04-inner)));
                float2 q=Turn(p/max(r,0.001),-_Ring.x)*radius;
                // The ring's outer circumference occupies 94% of its square source canvas.
                half4 ring=SAMPLE_TEXTURE2D(_RingTex,sampler_RingTex,0.5+q*0.47);
                float blend=smoothstep(inner,inner+aa,r)*ring.a;
                half3 wood=ring.rgb*0.88;
                if(_Ring.w>1.5) wood=lerp(wood,wood*half3(1.35,0.72,0.38),0.65);
                half4 result=half4(lerp(half3(0.035,0.029,0.023),wood,blend),original.a);
                float theta=atan2(p.y,p.x)-_Ring.x;
                float arc=pow(saturate(cos(theta*2)),5);
                float glow=exp(-pow((r-1.01)/0.018,2))*arc*_Ring.y;
                result.rgb=lerp(result.rgb,half3(0.55,0.90,1),glow*0.95);
                return result*i.color;
            }
            ENDHLSL
        }
    }
}
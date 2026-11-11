Shader "K4RMA/SwordRisePolish"
{
    Properties
    {
        _FrameUV ("Frame UV", Vector) = (0,0,1,1)
        _BladeRect ("Blade core UV", Vector) = (0,0,1,1)
        [PerRendererData] _MainTex ("Scar blade", 2D) = "white" {}
        [PerRendererData] _Fade ("Dissipation", Float) = 1
        [PerRendererData] _EnergyLayer ("Separate aura", Float) = 0
        [PerRendererData] _FloorY ("Ground clipping plane", Float) = 0
        [PerRendererData] _AuraTime ("Aura local clock", Float) = 0
        [PerRendererData] _AuraSeed ("Aura variation", Float) = 0
        _Color ("Tint", Color) = (1,1,1,1)
        [PerRendererData] _RiseProgress ("Upward energy front", Float) = -0.15
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
                float _RiseProgress, _Fade, _EnergyLayer, _AuraTime, _AuraSeed, _FloorY;
                float4 _FrameUV, _BladeRect;
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
                o.color = v.color * _Color * unity_SpriteColor;
                o.worldY = TransformObjectToWorld(v.vertex).y;
                return o;
            }
            float AuraHash(float2 p)
            {
                p=frac(p*float2(123.34,456.21));
                p+=dot(p,p+45.32);
                return frac(p.x*p.y);
            }
            float AuraNoise(float2 p)
            {
                float2 cell=floor(p), f=frac(p);
                f=f*f*(3-2*f);
                return lerp(lerp(AuraHash(cell),AuraHash(cell+float2(1,0)),f.x),
                    lerp(AuraHash(cell+float2(0,1)),AuraHash(cell+1),f.x),f.y);
            }
            half4 frag(Output i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;

                // A hard floor keeps the thin emergence point on the planks.
                clip(i.worldY - _FloorY + 0.005);
                float2 uv=(i.uv-_FrameUV.xy)/max(_FrameUV.zw,float2(0.0001,0.0001));
                float2 blade=(uv-_BladeRect.xy)/max(_BladeRect.zw,float2(0.0001,0.0001));
                // Profile positions light without warping the selected sword.
                // Source alpha still defines the solid artwork silhouette.
                float y=saturate(blade.y);
                float width=y<0.05?lerp(0.02,0.50,y/0.05)
                    :y<0.14?lerp(0.50,0.20,(y-0.05)/0.09)
                    :y<0.25?lerp(0.20,0.24,(y-0.14)/0.11)
                    :y<0.50?lerp(0.24,0.20,(y-0.25)/0.25)
                    :y<0.70?lerp(0.20,0.25,(y-0.50)/0.20)
                    :lerp(0.25,0,(y-0.70)/0.30);
                float sword = (1-smoothstep(width-0.012,width+0.012,abs(blade.x-0.5)))
                    * step(0,blade.y) * step(blade.y,1);
                if (_EnergyLayer > 0.5)
                {
                    // Scroll the modulation upward, never the sword texture or
                    // its silhouette. Only a narrow envelope beside the blade
                    // carries light; broad painted ribbons remain subdued.
                    float t=_AuraTime;
                    float edge=abs(blade.x-0.5)-width;
                    float flow=AuraNoise(float2(blade.x*13+_AuraSeed,blade.y*12-t*5.5));
                    float fine=AuraNoise(float2(blade.x*31-_AuraSeed,blade.y*27-t*9));
                    float turbulence=(flow-0.5)*0.014;
                    float filament=1-smoothstep(0.006,0.026,abs(edge-0.050-turbulence));
                    float innerFilament=1-smoothstep(0.003,0.014,abs(edge-0.022+turbulence*0.5));
                    float climbing=smoothstep(0.38,0.78,flow)*lerp(0.65,1,fine);
                    float envelope=exp2(-max(edge,0)*18);
                    float ends=smoothstep(0,0.045,blade.y)*(1-smoothstep(0.96,1.02,blade.y));
                    // A brief upward pulse yields to thin, broken filaments.
                    // Keep the halo narrow so adjacent swords leave readable gaps.
                    float surge=pow(saturate(1-abs(blade.y-(t*5.5+0.08))*4),2)
                        *(1-smoothstep(0.12,0.22,t));
                    float sheath=exp2(-abs(edge-0.018)*75)*0.15;
                    float groundLight=exp2(-max(i.worldY-_FloorY,0)*9)
                        *(1-smoothstep(0.04,0.14,t));
                    float electricity=(filament*climbing*0.55+innerFilament*fine*0.24
                        +sheath*(0.45+surge)+groundLight*filament*0.28)*ends*envelope;
                    color.rgb=lerp(half3(0.08h,0.55h,1.0h),half3(0.58h,0.94h,1.12h),climbing);
                    color.a=electricity*(1-sword)*saturate(_Fade);
                }
                else
                {
                    // Animate the painted discharge independently from the solid metal.
                    // The original silhouette and transparent padding remain intact.
                    float flow = AuraNoise(float2(blade.x*17+_AuraSeed,blade.y*9-_AuraTime*4));
                    float cyan = saturate((color.b-color.r)*3) * saturate((color.g-color.r)*4);
                    float discharge = cyan * (1-sword);
                    color.a *= lerp(1,0.45+0.55*flow,discharge);
                    float glint = pow(saturate(1-abs(blade.y-_RiseProgress)*5),3);
                    float release=1-smoothstep(0.035,0.15,_AuraTime);
                    float edgeLight=pow(saturate(abs(blade.x-0.5)/max(width,0.015)),6);
                    float contactLight=exp2(-max(i.worldY-_FloorY,0)*8)*release;
                    color.rgb += half3(0.12h,0.30h,0.40h)
                        *(glint*0.85+release*edgeLight*0.45+contactLight*0.6)*sword;

                    // Break the expired blade into light, instead of leaving a solid
                    // ghost of the damage region throughout its cosmetic tail.
                    // Release travels from the grounded crossguard to the tip;
                    // small irregular fragments prevent a flat horizontal wipe.
                    float dissolve = y*0.65+AuraNoise(float2(blade.x*24+_AuraSeed,blade.y*38))*0.35;
                    float threshold = (1-saturate(_Fade))*1.15-0.15;
                    float remaining = smoothstep(threshold,threshold+0.10,dissolve);
                    float rim = (1-smoothstep(threshold+0.10,threshold+0.22,dissolve))
                        * remaining * (1-saturate(_Fade));
                    color.rgb += half3(0.12h,0.55h,0.75h)*rim;
                    color.a *= remaining*saturate(_Fade);
                }
                return color;
            }
            ENDHLSL
        }
    }
}

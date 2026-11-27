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
                // Match KarmaSwordScarRise.BladeHalfWidth: measured clean alpha.
                float width=y<0.05?lerp(0,0.066,y/0.05)
                :y<0.065?lerp(0.066,0.366,(y-0.05)/0.015)
                :y<0.092?lerp(0.366,0.497,(y-0.065)/0.027)
                :y<0.10?lerp(0.497,0.289,(y-0.092)/0.008)
                :y<0.12?lerp(0.289,0.217,(y-0.10)/0.02)
                :y<0.16?lerp(0.217,0.149,(y-0.12)/0.04)
                :y<0.20?lerp(0.149,0.196,(y-0.16)/0.04)
                :y<0.35?lerp(0.196,0.142,(y-0.20)/0.15)
                :y<0.50?lerp(0.142,0.139,(y-0.35)/0.15)
                :y<0.56?lerp(0.139,0.154,(y-0.50)/0.06)
                :y<0.65?lerp(0.154,0.135,(y-0.56)/0.09)
                :y<0.82?lerp(0.135,0.121,(y-0.65)/0.17)
                :lerp(0.121,0,(y-0.82)/0.18);
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
                    float turbulence=(flow-0.5)*0.018;
                    // A coherent upward current, broken into bright branches by
                    // noise, reads as electricity instead of a uniform outline.
                    float current=0.5+0.5*sin(blade.y*34-t*19+_AuraSeed);
                    float filament=1-smoothstep(0.006,0.026,abs(edge-0.050-turbulence));
                    float innerFilament=1-smoothstep(0.003,0.014,abs(edge-0.022+turbulence*0.5));
                    float climbing=smoothstep(0.32,0.75,flow)*lerp(0.70,1,fine);
                    float envelope=exp2(-max(edge,0)*18);
                    float ends=smoothstep(0,0.045,blade.y)*(1-smoothstep(0.96,1.02,blade.y));
                    // A brief upward pulse yields to thin, broken filaments.
                    // Keep the halo narrow so adjacent swords leave readable gaps.
                    float surge=pow(saturate(1-abs(blade.y-(t*5.5+0.08))*4),2)
                        *(1-smoothstep(0.12,0.22,t));
                    float sheath=exp2(-abs(edge-0.018)*75)*0.20;
                    float groundLight=exp2(-max(i.worldY-_FloorY,0)*9)
                        *(1-smoothstep(0.04,0.14,t));
                    float electricity=(filament*climbing*(0.62+current*0.18)+innerFilament*fine*0.30
                        +sheath*(0.45+surge)+groundLight*filament*0.34)*ends*envelope;
                    color.rgb=lerp(half3(0.06h,0.43h,0.98h),half3(0.76h,1.02h,1.18h),
                        saturate(climbing*0.8+surge*0.45));
                    color.a=saturate(electricity)*(1-sword)*saturate(_Fade);
                }
                else
                {
                    // Clean texture alpha owns the metal silhouette. External
                    // electricity exists only in the sibling aura and mesh.
                    float glint = pow(saturate(1-abs(blade.y-_RiseProgress)*5),3);
                    float release=1-smoothstep(0.035,0.15,_AuraTime);
                    float edgeLight=pow(saturate(abs(blade.x-0.5)/max(width,0.015)),6);
                    float contactLight=exp2(-max(i.worldY-_FloorY,0)*8)*release;
                    // Silver bevels stay readable; a narrow cyan fuller carries
                    // the animated light, matching the judgement sword's material.
                    float fuller=exp2(-abs(blade.x-0.5)*180)*sword;
                    float traveling=pow(saturate(1-abs(y-_RiseProgress)*7),3);
                    float facets=release>0?AuraNoise(float2(blade.x*32+_AuraSeed,y*19-_AuraTime*3)):0;
                    float bevel=pow(saturate(1-abs(abs(blade.x-0.5)/max(width,0.015)-0.72)*5),3)*sword;
                    float bevelSweep=pow(saturate(1-abs(y-_RiseProgress+abs(blade.x-0.5)*0.7)*9),3);
                    color.rgb+=half3(0.16h,0.30h,0.38h)*bevel*(0.12+bevelSweep*0.75);
                    color.rgb += half3(0.08h,0.20h,0.28h)
                        *(glint*0.55+release*edgeLight*0.35+contactLight*0.45)*sword;
                    color.rgb=lerp(color.rgb,half3(0.78h,1.04h,1.18h),
                        saturate(fuller*(0.18+traveling*0.65)+edgeLight*facets*release*0.12));

                    // Break the expired blade into light, instead of leaving a solid
                    // ghost of the damage region throughout its cosmetic tail.
                    // Release travels from the grounded crossguard to the tip;
                    // small irregular fragments prevent a flat horizontal wipe.
                    // Most blades are fully active. Skip dissolution noise
                    // until a residual renderer actually starts to fade.
                    if(_Fade<0.9999)
                    {
                        float dissolve = y*0.65+AuraNoise(float2(blade.x*24+_AuraSeed,blade.y*38))*0.35;
                        float threshold = (1-saturate(_Fade))*1.15-0.15;
                        float remaining = smoothstep(threshold,threshold+0.10,dissolve);
                        float rim = (1-smoothstep(threshold+0.10,threshold+0.22,dissolve))
                            * remaining * (1-saturate(_Fade));
                        color.rgb = lerp(color.rgb,half3(0.72h,1.06h,1.24h),rim*0.85);
                        color.a *= remaining*saturate(_Fade);
                    }
                }
                color.rgb *= half3(0.85h, 0.87h, 0.92h);
                return color;
            }
            ENDHLSL
        }
    }
}

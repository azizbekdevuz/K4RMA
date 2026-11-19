Shader "K4RMA/Player/ParryFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Age ("Seconds since successful parry", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Age;
            CBUFFER_END
            struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o=(Output)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=TransformObjectToHClip(UnityFlipSprite(v.vertex,unity_SpriteProps.xy));
                o.uv=v.uv; o.color=v.color*unity_SpriteColor; return o;
            }
            half4 frag(Output i):SV_Target
            {
                float t=saturate(_Age/0.3);
                float2 p=(i.uv-0.5)*2;
                float r=length(p), a=atan2(p.y,p.x);
                // Hold the success signal for 0.12 seconds before fading; duration remains 0.3s.
                float fade=1-smoothstep(0.4,1,t);
                float core=exp(-r*r/(0.024*(1-t)+0.003))*fade;
                float longRay=pow(abs(cos(a*2-0.7)),18);
                float shortRay=pow(abs(cos(a*5+0.4)),30)*0.7;
                float rayLength=0.78+0.12*sin(t*3.14);
                float ray=(longRay+shortRay)*pow(saturate(1-r/rayLength),1.1)*fade;
                float ringRadius=0.22+0.62*t;
                float ring=exp(-pow((r-ringRadius)/0.022,2))*smoothstep(0,0.07,t)
                    *(1-smoothstep(0.35,1,t))*0.8;
                float spark=pow(saturate(cos(a*11+t)),55)
                    *exp(-pow((r-(0.33+0.5*t))/0.065,2))*fade;
                float alpha=saturate(core*1.4+ray*2.2+ring+spark)*saturate((1-r)*12);
                half3 color=lerp(half3(1,0.55,0.08),half3(1,0.98,0.83),saturate(core*2+ray));
                return half4(color,alpha)*i.color;
            }
            ENDHLSL
        }
    }
}

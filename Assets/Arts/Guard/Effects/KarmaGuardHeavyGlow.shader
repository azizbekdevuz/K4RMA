Shader "K4RMA/Guard/HeavyGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Molten sword", 2D) = "white" {}
        _Charge ("Charge", Float) = 0
        _Pulse ("Parry cue / impact", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float _Charge, _Pulse;
                float4 _Blade, _PoseRect, _TexSize;
            CBUFFER_END
            struct Input { float3 vertex:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v); SetUpSpriteInstanceProperties();
                Output o=(Output)0; UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex=TransformObjectToHClip(UnityFlipSprite(v.vertex,unity_SpriteProps.xy));
                o.uv=v.uv; o.color=v.color*unity_SpriteColor; return o;
            }
            half4 frag(Output i):SV_Target
            {
                half4 hot=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv);
                float2 p=(i.uv*_TexSize.xy-_PoseRect.xy)/_PoseRect.zw*160;
                p.y=160-p.y;
                float2 edge=_Blade.zw-_Blade.xy;
                float u=saturate(dot(p-_Blade.xy,edge)/dot(edge,edge));
                float bladeMask=1-smoothstep(3,7,length(p-(_Blade.xy+u*edge)));
                float mask=hot.a*bladeMask;
                half3 light=lerp(half3(1,0.34,0.025),half3(1,0.95,0.7),saturate(_Pulse));
                return half4(light*(0.9+_Pulse*2),mask*(_Charge*0.75+_Pulse))*i.color;
            }
            ENDHLSL
        }
    }
}

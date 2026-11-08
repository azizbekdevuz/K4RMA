Shader "K4RMA/SwordRisePolish"
{
    Properties
    {
        _FrameUV ("Frame UV", Vector) = (0,0,1,1)
        _BladeRect ("Blade core UV", Vector) = (0,0,1,1)
        [PerRendererData] _MainTex ("Scar blade", 2D) = "white" {}
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
            #include "../Resources/SW05/SW05CrimsonPalette.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RiseProgress;
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
            half4 frag(Output i) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                color.rgb = SW05CrimsonPalette(color.rgb);
                // A hard floor keeps the thin emergence point on the planks.
                clip(i.worldY + 0.015);
                float2 uv=(i.uv-_FrameUV.xy)/max(_FrameUV.zw,float2(0.0001,0.0001));
                float2 blade=(uv-_BladeRect.xy)/max(_BladeRect.zw,float2(0.0001,0.0001));
                half band = saturate(1 - abs(blade.y - _RiseProgress) * 14);
                // The entire active silhouette stays visible from frame one.
                // Only the internal blue-white energy front travels upward.
                half spine = exp(-abs(blade.x - 0.5) * 18);
                half trailing = saturate(1 - abs(blade.y - (_RiseProgress - 0.16)) * 18);
                // Keep the faceted azure blade and ivory edges readable.
                // The upward glint crosses its interior without a flame-like
                // orange fill, extra columns or delayed active silhouette.
                color.rgb = lerp(color.rgb, half3(1.1h, 1.35h, 1.5h), band * band * (0.45h + spine * 0.45h));
                color.rgb += half3(0.04h, 0.22h, 0.34h) * trailing * spine;
                return color;
            }
            ENDHLSL
        }
    }
}

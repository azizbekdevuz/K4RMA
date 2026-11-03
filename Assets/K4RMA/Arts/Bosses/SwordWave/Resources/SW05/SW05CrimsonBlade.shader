Shader "K4RMA/SW05/CrimsonBlade"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
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
                half4 source = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return half4(SW05CrimsonPalette(source.rgb), source.a) * i.color;
            }
            ENDHLSL
        }
    }
}

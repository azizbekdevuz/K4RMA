Shader "K4RMA/RisingTranslucentFragment"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _MembraneAlpha ("Membrane opacity", Range(0,1)) = 0.14
        _GridAlpha ("Lattice opacity", Range(0,1)) = 0.7
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            // Boss-local sprite material; no scene or camera dependency.
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
                half _MembraneAlpha;
                half _GridAlpha;
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
                // Bright lattice strokes stay readable; dense gold faces become clear film.
                half brightness = dot(source.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                half lattice = smoothstep(0.55h, 0.85h, brightness);
                half opacity = lerp(_MembraneAlpha, _GridAlpha, lattice);
                half3 gold = lerp(half3(1.0h, 0.72h, 0.18h), half3(1.0h, 0.96h, 0.62h), lattice);
                return half4(gold * i.color.rgb, source.a * opacity * i.color.a);
            }
            ENDHLSL
        }
    }
}

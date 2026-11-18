Shader "K4RMA/InheritanceReveal"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        [PerRendererData] _Incoming ("Inherited form", Float) = 0
        _FormAmount ("Progress", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"
               "CanUseSpriteAtlas"="True" "DisableBatching"="True" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _Grip, _Faces;
                float _FormAmount, _Incoming;
            CBUFFER_END
            struct Input { float3 position : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Output { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float2 body : TEXCOORD1; half4 color : COLOR; UNITY_VERTEX_OUTPUT_STEREO };
            Output vert(Input v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                Output o = (Output)0;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                // Ease the neutral recovery arm down around its shoulder, with the feet planted.
                float height = _Grip.z;
                float2 shoulder = float2(-0.15, 0.72) * height;
                float2 arm = (v.position.xy - shoulder) / height;
                float along = dot(arm, float2(0.75, -0.66));
                float across = abs(dot(arm, float2(0.66, 0.75)));
                float weight = smoothstep(0.03, 0.24, along) * (1 - smoothstep(0.10, 0.23, across));
                float sn, cs;
                sincos(-0.20 * weight * (1 - _Incoming) * smoothstep(0, 0.65, _FormAmount), sn, cs);
                o.body = shoulder + height * float2(cs * arm.x - sn * arm.y, sn * arm.x + cs * arm.y);
                // Register the two faces while the stance settles toward the new
                // illustration. Art below the ankles remains at the original pivot.
                float faceY = lerp(_Faces.y, _Faces.w, _Incoming);
                float upperBody = smoothstep(0.12, 0.84, v.position.y / faceY);
                o.body += (_Faces.zw - _Faces.xy) * (smoothstep(0, 1, _FormAmount) - _Incoming) * upperBody;
                o.position = TransformObjectToHClip(UnityFlipSprite(float3(o.body, v.position.z), unity_SpriteProps.xy));
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }
            half4 frag(Output i) : SV_Target
            {
                half4 ink = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                float2 delta = i.body - _Grip.xy;
                // Retract the outgoing blade with the hand as the new grip appears.
                float2 blade = float2(0.83, -0.56);
                float along = max(0, dot(delta, blade));
                float offBlade = abs(dot(delta, float2(-blade.y, blade.x))) / _Grip.z;
                delta -= blade * along * 0.98 * (1 - smoothstep(0.13, 0.27, offBlade));
                float feather = _Grip.z * 0.065;
                float radius = lerp(-feather, _Grip.w + feather, smoothstep(0, 1, _FormAmount));
                float reveal = 1 - smoothstep(radius - feather, radius + feather, length(delta));
                // The outgoing edge lingers under the incoming ink to keep the body solid.
                ink.a *= lerp(1 - smoothstep(0.44, 1, reveal), reveal, _Incoming);
                return ink;
            }
            ENDHLSL
        }
    }
}

// Unlit HDR sprite for neon / fire / magic glow. Values above 1 feed the Bloom pass.
// Works on SpriteRenderers and ParticleSystems (uses vertex color).
Shader "DarkForest/NeonSprite"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (2.5, 0.6, 4.0, 1)
        _Intensity ("Intensity", Range(0, 10)) = 1.5
        _Flatten ("Flatten To Color (1 = neon silhouette)", Range(0, 1)) = 1
        _PulseSpeed ("Pulse Speed", Float) = 2
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0.3
        _FlickerAmount ("Flicker Amount", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend (One = additive)", Float) = 10
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "DarkForestSprite.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float phase : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half _Intensity;
                half _Flatten;
                half _PulseSpeed;
                half _PulseAmount;
                half _FlickerAmount;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 pos = DF_FlipSprite(v.positionOS.xyz);
                o.positionCS = TransformObjectToHClip(pos);
                o.uv = v.uv;
                o.color = DF_SpriteColor(v.color);
                // Sprites are batched, so derive a per-object-ish phase from world position.
                float3 wp = TransformObjectToWorld(v.positionOS.xyz);
                o.phase = floor(wp.x * 0.5) * 1.7 + floor(wp.y * 0.5) * 0.9;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float t = _Time.y;
                half pulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed + i.phase);
                half flicker = 1.0 - _FlickerAmount * saturate(0.5 + 0.5 * sin(t * 23.0 + i.phase * 7.0) * sin(t * 7.3 + i.phase * 3.0));
                half3 baseCol = lerp(tex.rgb, half3(1, 1, 1), _Flatten);
                half3 rgb = baseCol * i.color.rgb * _EmissionColor.rgb * (_Intensity * pulse * flicker);
                return half4(rgb, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
}

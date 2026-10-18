// Scrolling fbm mist band. Noise is sampled in world space so neighbouring fog quads line up.
Shader "DarkForest/MistFog"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite (unused)", 2D) = "white" {}
        [HDR] _Color ("Fog Color", Color) = (0.55, 0.25, 0.85, 1)
        _Density ("Density", Range(0, 2)) = 0.6
        _Scale ("Noise Scale", Float) = 0.18
        _Speed ("Scroll Speed", Float) = 0.35
        _TopFade ("Top Fade", Range(0.05, 1)) = 0.9
        _BottomFade ("Bottom Fade", Range(0, 1)) = 0
        _EdgeFade ("Side Fade", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "DarkForestSprite.hlsl"

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; float2 world : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density, _Scale, _Speed, _TopFade, _BottomFade, _EdgeFade;
            CBUFFER_END

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += a * Noise(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return v;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = DF_SpriteColor(v.color);
                o.world = TransformObjectToWorld(v.positionOS.xyz).xy;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float2 p = i.world * _Scale;
                float n = Fbm(p + float2(t, t * 0.15));
                n = n * 0.65 + 0.35 * Fbm(p * 1.7 - float2(t * 0.6, 0));
                float vertical = 1.0 - smoothstep(0.0, _TopFade, i.uv.y);
                vertical *= _BottomFade > 0.001 ? smoothstep(0.0, _BottomFade, i.uv.y) : 1.0;
                float sides = smoothstep(0.0, _EdgeFade, i.uv.x) * smoothstep(0.0, _EdgeFade, 1.0 - i.uv.x);
                float a = saturate(n * vertical * sides * _Density * 1.6) * i.color.a;
                return half4(_Color.rgb * i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}

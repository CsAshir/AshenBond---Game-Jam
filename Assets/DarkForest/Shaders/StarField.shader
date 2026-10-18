// Additive twinkling pixel stars, drawn in front of an opaque painted sky so the night still sparkles.
Shader "DarkForest/StarField"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite (unused)", 2D) = "white" {}
        _StarDensity ("Star Density", Range(0, 0.2)) = 0.03
        _StarGrid ("Star Grid (cells across)", Float) = 200
        _StarBrightness ("Star Brightness", Range(0, 6)) = 1.6
        _TwinkleSpeed ("Twinkle Speed", Float) = 2.2
        _FadeStart ("Fade Below (uv.y)", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
        Blend One One
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                half _StarDensity, _StarGrid, _StarBrightness, _TwinkleSpeed, _FadeStart;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 grid = float2(_StarGrid, _StarGrid * 0.5625);
                float2 cell = floor(i.uv * grid);
                float h = Hash21(cell);
                if (h <= 1.0 - _StarDensity) return 0;
                float2 local = frac(i.uv * grid) - 0.5;
                float d = max(abs(local.x), abs(local.y));
                float star = step(d, 0.2 + 0.15 * frac(h * 13.0));
                float twinkle = 0.45 + 0.55 * sin(_Time.y * _TwinkleSpeed * (0.5 + h) + h * 80.0);
                half3 tint = lerp(half3(0.8, 0.85, 1.0), half3(1.0, 0.8, 0.7), frac(h * 31.0));
                float fade = smoothstep(_FadeStart, _FadeStart + 0.25, i.uv.y);
                return half4(tint * star * max(twinkle, 0.0) * _StarBrightness * fade, 1);
            }
            ENDHLSL
        }
    }
}

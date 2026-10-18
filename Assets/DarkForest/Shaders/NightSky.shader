// Procedural purple night sky: vertical gradient, horizon fire-glow and twinkling pixel stars.
Shader "DarkForest/NightSky"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite (unused, keeps SpriteRenderer happy)", 2D) = "white" {}
        _TopColor ("Top Color", Color) = (0.02, 0.01, 0.06, 1)
        _MidColor ("Mid Color", Color) = (0.10, 0.03, 0.20, 1)
        _HorizonColor ("Horizon Color", Color) = (0.32, 0.07, 0.30, 1)
        [HDR] _GlowColor ("Horizon Glow (fire)", Color) = (0.9, 0.25, 0.15, 1)
        _HorizonHeight ("Horizon Height", Range(0, 1)) = 0.28
        _GlowWidth ("Glow Width", Range(0.01, 1)) = 0.18
        _StarDensity ("Star Density", Range(0, 0.2)) = 0.035
        _StarGrid ("Star Grid (cells across)", Float) = 220
        _StarBrightness ("Star Brightness", Range(0, 6)) = 2.2
        _TwinkleSpeed ("Twinkle Speed", Float) = 2.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "PreviewType" = "Plane" }
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
                half4 _TopColor, _MidColor, _HorizonColor, _GlowColor;
                half _HorizonHeight, _GlowWidth, _StarDensity, _StarGrid, _StarBrightness, _TwinkleSpeed;
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
                float y = i.uv.y;
                half3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(_HorizonHeight, 0.6, y));
                col = lerp(col, _TopColor.rgb, smoothstep(0.55, 1.0, y));
                col += _GlowColor.rgb * exp(-abs(y - _HorizonHeight) / _GlowWidth) * 0.6;

                // Pixel stars on a grid (aspect ~16:9).
                float2 grid = float2(_StarGrid, _StarGrid * 0.5625);
                float2 cell = floor(i.uv * grid);
                float h = Hash21(cell);
                if (h > 1.0 - _StarDensity)
                {
                    float2 local = frac(i.uv * grid) - 0.5;
                    float d = max(abs(local.x), abs(local.y));
                    float twinkle = 0.55 + 0.45 * sin(_Time.y * _TwinkleSpeed * (0.5 + h) + h * 80.0);
                    float star = step(d, 0.22 + 0.15 * frac(h * 13.0));
                    half3 tint = lerp(half3(0.85, 0.75, 1.0), half3(1.0, 0.7, 0.9), frac(h * 31.0));
                    col += tint * star * twinkle * _StarBrightness * smoothstep(_HorizonHeight, _HorizonHeight + 0.35, y);
                }
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}

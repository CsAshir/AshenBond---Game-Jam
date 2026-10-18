// Unlit parallax background that keeps the painted fire alive in a dark scene:
// - non-fire pixels are darkened by the SpriteRenderer colour (the purple night tint)
// - fire pixels (warm + saturated) skip the tint, get boosted into HDR (bloom) and flicker
// - smoke pixels (desaturated, mid-dark) slowly billow via a rising noise distortion
// - air just above fires shimmers with heat haze
Shader "DarkForest/FireBackdrop"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _FireBoost ("Fire Boost (HDR)", Range(0, 4)) = 1.7
        _FlickerAmount ("Fire Flicker", Range(0, 1)) = 0.35
        _FlickerSpeed ("Flicker Rise Speed", Float) = 2.5
        _SmokeDistort ("Smoke Billow", Range(0, 0.02)) = 0.005
        _SmokeSpeed ("Smoke Rise Speed", Float) = 0.04
        _ShimmerAmount ("Heat Shimmer", Range(0, 0.01)) = 0.0015
        _Blur ("Depth Blur (texels)", Range(0, 8)) = 0
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
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half _FireBoost, _FlickerAmount, _FlickerSpeed, _SmokeDistort, _SmokeSpeed, _ShimmerAmount, _Blur;
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
                return Noise(p) * 0.6 + Noise(p * 2.1 + 7.3) * 0.3 + Noise(p * 4.3 + 3.1) * 0.1;
            }

            // Warm (r >= g >= b), saturated, reasonably bright -> painted fire / fire-lit glow.
            half FireMask(half3 c)
            {
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half sat = (mx - mn) / (mx + 1e-4);
                half warm = step(c.g, c.r) * step(c.b, c.g);
                return smoothstep(0.12, 0.35, c.r) * smoothstep(0.45, 0.75, sat) * warm;
            }

            // Desaturated mid-dark tones -> smoke clouds.
            half SmokeMask(half3 c)
            {
                half mx = max(c.r, max(c.g, c.b));
                half mn = min(c.r, min(c.g, c.b));
                half sat = (mx - mn) / (mx + 1e-4);
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                return (1.0 - smoothstep(0.2, 0.45, sat)) * smoothstep(0.01, 0.04, lum) * (1.0 - smoothstep(0.35, 0.6, lum));
            }

            half4 Sample(float2 uv) { return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv); }

            // Depth-of-field look for far layers: centre + two rings of 8 taps (focus stays on the player's plane).
            half4 SampleBlur(float2 uv)
            {
                if (_Blur <= 0.01) return Sample(uv);
                float tw, th;
                _MainTex.GetDimensions(tw, th);
                float2 r = float2(1.0 / tw, 1.0 / th) * _Blur;
                half4 sum = Sample(uv) * 2.0;
                half w = 2.0;
                [unroll] for (int k = 0; k < 8; k++)
                {
                    float a = k * 0.785398;
                    float2 d = float2(cos(a), sin(a));
                    sum += Sample(uv + d * r * 0.5) * 1.5;
                    sum += Sample(uv + float2(-d.y, d.x) * r) * 1.0;   // rotated outer ring
                    w += 2.5;
                }
                return sum / w;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(DF_FlipSprite(v.positionOS.xyz));
                o.uv = v.uv;
                o.color = DF_SpriteColor(v.color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;

                // Masks from the undistorted image.
                half4 c0 = Sample(uv);
                half smoke = SmokeMask(c0.rgb) * c0.a;
                half fireBelow = max(FireMask(Sample(uv - float2(0, 0.03)).rgb), FireMask(Sample(uv - float2(0, 0.07)).rgb));

                // Smoke billows upward; air above flames wobbles.
                float n1 = Fbm(uv * float2(9, 4.5) + float2(t * 0.02, -t * _SmokeSpeed));
                float n2 = Fbm(uv * float2(9, 4.5) * 1.7 + float2(-t * 0.015, -t * _SmokeSpeed * 1.4) + 13.1);
                float2 offs = (float2(n1, n2) - 0.5) * 2.0 * _SmokeDistort * smoke;
                offs.x += sin(uv.y * 260.0 + t * 7.0) * _ShimmerAmount * fireBelow;
                // No distortion at the image edges, so mirrored copies still meet cleanly.
                offs *= smoothstep(0.0, 0.015, uv.x) * smoothstep(0.0, 0.015, 1.0 - uv.x);

                half4 c = SampleBlur(uv + offs);
                half fire = FireMask(c.rgb);

                // Rising flicker inside the flames, with a little yellow/orange shift.
                float fa = Noise(uv * float2(70, 30) + float2(0, -t * _FlickerSpeed));
                float fb = Noise(uv * float2(23, 11) + float2(t * 0.3, -t * _FlickerSpeed * 0.6));
                float flick = 1.0 + _FlickerAmount * ((fa * 0.6 + fb * 0.4) * 2.0 - 1.0);
                half3 hot = c.rgb * _FireBoost * flick;
                hot.g *= 1.0 + 0.25 * (fa - 0.5);

                half3 cold = c.rgb * i.color.rgb;
                half3 rgb = lerp(cold, hot, fire);
                rgb *= 1.0 - smoke * 0.18 * (n1 - 0.35);   // drifting shadows inside the smoke
                return half4(rgb, c.a * i.color.a);
            }
            ENDHLSL
        }
    }
}

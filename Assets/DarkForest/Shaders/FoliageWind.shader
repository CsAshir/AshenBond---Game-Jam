// Unlit sprite that bends in the wind: the base stays planted, the top sways (squared with height) and the
// leaves flutter. Displacement is done in world space so it works whether sprites are batched or not.
// _Hang = 1 flips the weighting for foliage hanging from above (canopy: the bottom edge swings).
Shader "DarkForest/FoliageWind"
{
    Properties
    {
        [MainTexture] _MainTex ("Sprite", 2D) = "white" {}
        _Sway ("Sway (world units at the top)", Float) = 0.35
        _Speed ("Speed", Float) = 1.1
        _Flutter ("Leaf Flutter", Float) = 0.05
        _Hang ("Hanging (canopy)", Range(0, 1)) = 0
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
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Sway;
                float _Speed;
                float _Flutter;
                float _Hang;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 wp = TransformObjectToWorld(DF_FlipSprite(v.positionOS.xyz));
                float h = lerp(v.uv.y, 1.0 - v.uv.y, _Hang);
                float t = _Time.y * _Speed;
                // Slow gusts shared by the whole forest (travel left to right), plus each tree's own rhythm.
                float gust = 0.55 + 0.45 * sin(t * 0.37 - wp.x * 0.05) * sin(t * 0.23 + 1.3);
                float sway = (sin(t + wp.x * 0.35) * 0.6 + 0.4) * gust;
                float flutter = sin(t * 6.3 + wp.y * 2.1 + wp.x * 1.7) * sin(t * 4.1 + wp.x * 3.3);
                wp.x += sway * _Sway * h * h + flutter * _Flutter * h;
                wp.y += flutter * _Flutter * 0.4 * h;
                o.positionCS = TransformWorldToHClip(wp);
                o.uv = v.uv;
                o.color = DF_SpriteColor(v.color);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return tex * i.color;
            }
            ENDHLSL
        }
    }
}

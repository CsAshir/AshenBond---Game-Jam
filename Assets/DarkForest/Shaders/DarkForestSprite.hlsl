#ifndef DARKFOREST_SPRITE_INCLUDED
#define DARKFOREST_SPRITE_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

// Unity 6 / URP 17 hands a SpriteRenderer's colour and flipX/flipY to the shader per draw
// (unity_SpriteColor / unity_SpriteProps) instead of baking them into the vertices.
// Particle systems leave both unset (zero). A sprite's flip values are always +-1, so the flip
// data tells us reliably whether this draw is a sprite. (A sprite colour of (0,0,0,0) is legitimate:
// it must stay invisible, not fall back to white.)

bool DF_IsSprite()
{
    SetUpSpriteInstanceProperties();
    return abs(unity_SpriteProps.x) > 0.5 && abs(unity_SpriteProps.y) > 0.5;
}

float3 DF_FlipSprite(float3 positionOS)
{
    if (!DF_IsSprite()) return positionOS;
    return float3(positionOS.xy * unity_SpriteProps.xy, positionOS.z);
}

half4 DF_SpriteColor(half4 vertexColor)
{
    return DF_IsSprite() ? vertexColor * unity_SpriteColor : vertexColor;
}

#endif

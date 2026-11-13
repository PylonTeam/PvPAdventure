sampler uSprite : register(s0);

float uWave;
float uOpacity;
float4 uSourceRect;
float2 uImageSize0;

float AlphaAt(float2 pixel)
{
    float2 low = pixel - uSourceRect.xy;
    float2 high = uSourceRect.zw - low;
    float inside = step(0.0, min(low.x, low.y)) * (1.0 - step(min(high.x, high.y), 0.0));
    return tex2D(uSprite, pixel / uImageSize0).a * inside;
}

float4 Ghost(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 sprite = tex2D(uSprite, uv);
    float2 pixel = uv * uImageSize0;
    float nearestAlpha = min(
        min(AlphaAt(pixel + float2(1.5, 0.0)), AlphaAt(pixel - float2(1.5, 0.0))),
        min(AlphaAt(pixel + float2(0.0, 1.5)), AlphaAt(pixel - float2(0.0, 1.5))));
    float innerEdge = smoothstep(0.035, 0.78, sprite.a - nearestAlpha);

    // Match the race trail and spirit animal halo pulse.
    float breath = smoothstep(0.0, 1.0, uWave);
    float interiorFade = lerp(0.333333, 1.0, breath);
    float edgeFade = lerp(0.58, 1.0, breath);
    float fade = lerp(interiorFade, edgeFade, innerEdge * 0.78);

    float3 luminance = float3(0.299, 0.587, 0.114);
    float alpha = sprite.a * vertexColor.a;
    float3 lit = sprite.rgb * vertexColor.rgb;
    float litLuma = dot(lit, luminance);
    float light = saturate(max(vertexColor.r, max(vertexColor.g, vertexColor.b)) / max(vertexColor.a, 0.001));
    float3 tint = float3(0.72, 0.90, 1.0);
    float spiritLight = lerp(0.04, 0.12, breath) * lerp(1.0, 0.28, light);
    float3 interior = lerp(lit, litLuma * tint, 0.42)
        + tint * (dot(sprite.rgb, luminance) * vertexColor.a * spiritLight);
    float whiteEdge = litLuma * 0.91 + alpha * lerp(0.18, 0.43, breath) * lerp(1.0, 0.61, light);
    float darkness = 1.0 - smoothstep(0.0, 0.25, light);
    whiteEdge += alpha * lerp(0.07, 0.12, breath) * darkness;
    float3 body = lerp(interior, whiteEdge.xxx, innerEdge * 0.88);
    return float4(min(body, alpha), alpha) * (uOpacity * fade);
}

technique SpiritAnimalGhost
{
    pass GhostPass
    {
        PixelShader = compile ps_3_0 Ghost();
    }
}

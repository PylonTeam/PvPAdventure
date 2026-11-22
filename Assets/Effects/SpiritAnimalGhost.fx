sampler uSprite : register(s0);

float uWave;
float uOpacity;
float uDimOpacity;

float4 Ghost(float4 vertexColor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float4 sprite = tex2D(uSprite, uv);
    float opacity = lerp(uDimOpacity, uOpacity, uWave);

    float3 luminance = float3(0.299, 0.587, 0.114);
    float alpha = sprite.a * vertexColor.a;
    float3 lit = sprite.rgb * vertexColor.rgb;
    float litLuma = dot(lit, luminance);
    float light = saturate(max(vertexColor.r, max(vertexColor.g, vertexColor.b)) / max(vertexColor.a, 0.001));
    float3 tint = float3(0.72, 0.90, 1.0);
    float spiritLight = lerp(0.04, 0.12, uWave) * lerp(1.0, 0.28, light);
    float3 interior = lerp(lit, litLuma * tint, 0.42)
        + tint * (dot(sprite.rgb, luminance) * vertexColor.a * spiritLight);
    return float4(min(interior, alpha), alpha) * opacity;
}

technique SpiritAnimalGhost
{
    pass GhostPass
    {
        PixelShader = compile ps_3_0 Ghost();
    }
}

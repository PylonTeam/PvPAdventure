sampler uSprite : register(s0);

float4 uFrame;
float2 uFrameSize;
float uTime;
float uHandoff;
float uShine;
float3 uLeftHilt;
float3 uLeftMiddle;
float3 uLeftTip;
float3 uRightHilt;
float3 uRightMiddle;
float3 uRightTip;

float3 BladeRamp(float3 hilt, float3 middle, float3 tip, float height)
{
    // A handful of bands echoes the color blocks in the vanilla PvP blades.
    float band = floor(saturate(height) * 8.0 + 0.5) * 0.125;
    return lerp(lerp(hilt, middle, saturate(band * 2.0)), tip, saturate(band * 2.0 - 1.0));
}

float4 TeamGate(float4 tint : COLOR0, float2 atlasUv : TEXCOORD0) : COLOR0
{
    const float3 violet = float3(0.51, 0.38, 0.77);
    const float3 pearl = float3(0.93, 0.86, 1.0);

    // Lock the sample to its source texel and keep it inside the selected atlas frame.
    float2 texel = clamp(floor((atlasUv - uFrame.xy) / uFrame.zw * uFrameSize), 0.0, uFrameSize - 1.0);
    float4 sprite = tex2D(uSprite, uFrame.xy + (texel + 0.5) / uFrameSize * uFrame.zw);
    float3 original = sprite.rgb / max(sprite.a, 0.001);

    // The native art is painted in 2x2 blocks. Shade the whole block together.
    float2 pixel = floor(texel * 0.5);
    float2 grid = uFrameSize * 0.5;
    float2 position = (pixel + 0.5) / grid;
    float turn = floor(uTime * 12.0) / 12.0;
    float braid = floor(3.0 * sin(floor(pixel.y * 0.5) * 0.81 - turn * 4.4) + 0.5);
    float normalSide = step(0.5, position.x);
    float openSide = step(grid.x * 0.5 + braid, pixel.x);
    float height = 1.0 - smoothstep(0.15, 0.85, position.y);
    float openHeight = height + 0.09 * sin(floor(pixel.x * 0.5) * 0.90 + turn * 4.0);
    float3 left = lerp(BladeRamp(uLeftHilt, uLeftMiddle, uLeftTip, openHeight),
                       BladeRamp(uLeftHilt, uLeftMiddle, uLeftTip, height), uHandoff);
    float3 right = lerp(BladeRamp(uRightHilt, uRightMiddle, uRightTip, openHeight),
                        BladeRamp(uRightHilt, uRightMiddle, uRightTip, height), uHandoff);
    float3 team = lerp(left, right, lerp(openSide, normalSide, uHandoff));

    // Leave the deep violet interior intact. Recolor the source's lavender rim and blue sparks
    // into different ramps so a silver seam never reads like an opaque flat tint.
    float3 hue = lerp(team, violet, 0.20);
    float3 interior = lerp(original, hue * 0.51, 0.46 * step(0.24, original.r));
    float3 rim = hue * 0.89;
    rim = lerp(rim, lerp(hue, pearl, 0.41), step(0.69, original.r));
    rim = lerp(rim, lerp(hue, pearl, 0.72), step(0.80, original.r));
    float spark = step(0.42, original.b - original.r);
    float sparkValue = (step(0.41, original.g) + step(0.72, original.g)) * 0.5;
    float3 fleck = lerp(hue * 0.67, lerp(hue, pearl, 0.53), sparkValue);
    float onRim = step(0.45, original.r);
    float3 color = lerp(lerp(interior, rim, onRim), fleck, spark);

    float highlightY = floor((0.49 + sin(turn * 3.6 + pixel.x * 0.16) * 0.29) * grid.y);
    float sweep = (1.0 - step(1.1, abs(pixel.y - highlightY))) * onRim;
    float star = (1.0 - step(2.1, abs(pixel.y - floor(grid.y * 0.77)))) * onRim;
    color = lerp(color, pearl, 0.17 * sweep + 0.24 * star * uShine);
    return float4(saturate(color) * sprite.a, sprite.a) * tint;
}

technique ReturnGateTeamColor
{
    pass TintGate
    {
        PixelShader = compile ps_3_0 TeamGate();
    }
}

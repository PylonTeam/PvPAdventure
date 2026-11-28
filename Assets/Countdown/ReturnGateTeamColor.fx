float uTime;
float uHandoff;
float uShine;
float uTeamStrength;
float2 uArtCells;
float3 uLeftHilt;
float3 uLeftMiddle;
float3 uLeftTip;
float3 uRightHilt;
float3 uRightMiddle;
float3 uRightTip;

float3 BladeRamp(float3 hilt, float3 middle, float3 tip, float height)
{
    float3 lower = lerp(hilt, middle, smoothstep(0.0, 0.5, height));
    return lerp(lower, tip, smoothstep(0.5, 1.0, height));
}

float2 Aperture(float2 coord)
{
    // Let the upper and lower edges sway around a fixed center.
    float curl = 0.030 * sin(coord.y * 3.6) * cos(uTime * 2.3);
    return float2((coord.x + curl) / (0.47 - 0.07 * coord.y * coord.y), coord.y / 0.71);
}

float4 TeamGate(float4 tint : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    const float3 violet = float3(0.49, 0.27, 0.88);
    const float3 pearl = float3(0.85, 0.75, 1.0);
    float2 coord = uv * 2.0 - 1.0;
    // Use one sample per Terraria art cell for the lit rim. The veil and halo retain a soft falloff.
    float2 detailCoord = ((floor(uv * uArtCells) + 0.5) / uArtCells) * 2.0 - 1.0;
    float distance = length(Aperture(coord));
    float2 detailAperture = Aperture(detailCoord);
    float detailDistance = length(detailAperture);
    float2 normal = detailAperture / max(detailDistance, 0.001);

    float height = smoothstep(-0.70, 0.70, -detailCoord.y);
    float3 left = BladeRamp(uLeftHilt, uLeftMiddle, uLeftTip, height);
    float3 right = BladeRamp(uRightHilt, uRightMiddle, uRightTip, height);
    float3 team = lerp(left, right, smoothstep(-0.16, 0.16, detailCoord.x));
    float teamWeight = lerp(0.26 + 0.42 * uHandoff, 0.78 + 0.14 * uHandoff, uTeamStrength);
    float3 edgeColor = lerp(violet, team, teamWeight);

    float phase = uTime * 3.1;
    float alongRim = dot(normal, float2(sin(phase), cos(phase)));
    float sweep = smoothstep(0.52, 0.96, alongRim);
    float echo = smoothstep(0.81, 0.99, -alongRim);
    float rimDistance = abs(detailDistance - 1.0);
    float veil = (1.0 - smoothstep(0.12, 0.95, distance)) * 0.15;
    float halo = (1.0 - smoothstep(0.02, 0.31, abs(distance - 1.0))) * 0.11;
    // A dim collar outlines the opening while two small highlight clusters travel along its edge.
    float collar = (1.0 - smoothstep(0.065, 0.13, rimDistance)) * 0.21;
    float rim = collar + (1.0 - smoothstep(0.025, 0.082, rimDistance)) * (0.23 + 0.28 * sweep);
    float glint = (1.0 - smoothstep(0.014, 0.041, rimDistance)) *
        (0.04 + 0.24 * sweep + 0.10 * echo + 0.08 * uShine);
    float3 color = float3(0.09, 0.04, 0.18) * veil + edgeColor * (halo + rim) + pearl * glint;
    return float4(saturate(color), saturate(veil + halo + rim + glint)) * tint;
}

technique ReturnGateTeamColor
{
    pass TintGate
    {
        PixelShader = compile ps_3_0 TeamGate();
    }
}

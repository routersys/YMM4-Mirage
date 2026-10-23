Texture2D SourceTexture : register(t0);
SamplerState SourceSampler : register(s0);
Texture2D MapTexture : register(t1);
SamplerState MapSampler : register(s1);

cbuffer Constants : register(b0)
{
    float4 inputBounds : packoffset(c0);
    float halfFieldTangent : packoffset(c1.x);
    float pitchCosine : packoffset(c1.y);
    float pitchSine : packoffset(c1.z);
    float amount : packoffset(c1.w);
    float mapStart : packoffset(c2.x);
    float mapSpan : packoffset(c2.y);
    float mapWidth : packoffset(c2.z);
};

static const float HalfTexel = 0.5;
static const float MinimumDepth = 1e-4;
static const float MinimumShift = 1e-9;
static const int MaximumTaps = 16;

float ReadShift(float elevation)
{
    float position = (elevation - mapStart) / mapSpan;
    if (position < 0.0 || position > 1.0)
        return 0.0;

    float2 mapUv = float2((position * (mapWidth - 1.0) + HalfTexel) / mapWidth, HalfTexel);
    return MapTexture.SampleLevel(MapSampler, mapUv, 0).x;
}

float4 main(
    float4 position : SV_POSITION,
    float4 scenePosition : SCENE_POSITION,
    float4 uv0 : TEXCOORD0
) : SV_TARGET
{
    float4 source = SourceTexture.SampleLevel(SourceSampler, uv0.xy, 0);
    if (amount <= 0.0)
        return source;

    float2 center = 0.5 * (inputBounds.xy + inputBounds.zw);
    float focalLength = 0.5 * (inputBounds.z - inputBounds.x) / halfFieldTangent;
    float3 direction = normalize(float3(scenePosition.xy - center, focalLength));
    float worldDown = direction.y * pitchCosine - direction.z * pitchSine;
    float worldForward = direction.y * pitchSine + direction.z * pitchCosine;
    float elevation = asin(clamp(-worldDown, -1.0, 1.0));
    float azimuth = atan2(direction.x, worldForward);
    float pixelAngle = 1.0 / focalLength;

    float lowShift = ReadShift(elevation - 0.5 * pixelAngle);
    float highShift = ReadShift(elevation + 0.5 * pixelAngle);
    if (abs(lowShift) <= MinimumShift && abs(highShift) <= MinimumShift && abs(ReadShift(elevation)) <= MinimumShift)
        return source;

    float stretch = abs(pixelAngle + highShift - lowShift) / pixelAngle;
    float taps = clamp(ceil(stretch), 1.0, (float)MaximumTaps);
    float sinAzimuth;
    float cosAzimuth;
    sincos(azimuth, sinAzimuth, cosAzimuth);
    float4 sum = float4(0.0, 0.0, 0.0, 0.0);
    [loop]
    for (int tap = 0; tap < MaximumTaps; tap++)
    {
        if (tap >= taps)
            break;

        float sampled = elevation + ((tap + 0.5) / taps - 0.5) * pixelAngle;
        float seen = sampled + ReadShift(sampled);
        float sinSeen;
        float cosSeen;
        sincos(seen, sinSeen, cosSeen);
        float seenDown = -sinSeen;
        float seenForward = cosAzimuth * cosSeen;
        float3 seenDirection = float3(sinAzimuth * cosSeen, seenDown * pitchCosine + seenForward * pitchSine, -seenDown * pitchSine + seenForward * pitchCosine);
        float2 seenPosition = center + focalLength * seenDirection.xy / max(seenDirection.z, MinimumDepth);
        float2 clamped = clamp(seenPosition, inputBounds.xy + HalfTexel, inputBounds.zw - HalfTexel);
        sum += SourceTexture.SampleLevel(SourceSampler, uv0.xy + (clamped - scenePosition.xy) * uv0.zw, 0);
    }

    return lerp(source, sum / taps, amount);
}

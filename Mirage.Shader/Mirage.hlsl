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
    float4 shimmerPhase : packoffset(c3);
    float shimmerAmplitude : packoffset(c4.x);
};

static const float HalfTexel = 0.5;
static const float MinimumDepth = 1e-4;
static const float MinimumShift = 1e-9;
static const int MaximumTaps = 16;
static const float FillRadiusFraction = 0.006;
static const int FillHalfTaps = 4;
static const float FillTaps = 9.0;
static const float FillThreshold = 0.5;
static const float4 ShimmerAzimuthFrequency = float4(400.0, 170.0, 650.0, 95.0);
static const float4 ShimmerElevationFrequency = float4(2570.0, 1330.0, 4000.0, 720.0);
static const float4 ShimmerWeight = float4(1.0, 0.7, 0.5, 0.35);
static const float ShimmerWeightSum = 2.55;
static const float MaximumSine = 0.1;

float ArcSine(float value)
{
    float squared = value * value;
    return value * (1.0 + squared * (1.0 / 6.0 + squared * (3.0 / 40.0 + squared * (15.0 / 336.0))));
}

float ReadShift(float elevation)
{
    float position = (elevation - mapStart) / mapSpan;
    if (position < 0.0 || position > 1.0)
        return 0.0;

    float2 mapUv = float2((position * (mapWidth - 1.0) + HalfTexel) / mapWidth, HalfTexel);
    return MapTexture.SampleLevel(MapSampler, mapUv, 0).x;
}

float ReadFill(float elevation)
{
    float position = (elevation - mapStart) / mapSpan;
    if (position < 0.0 || position > 1.0)
        return 0.0;

    float2 mapUv = float2((position * (mapWidth - 1.0) + HalfTexel) / mapWidth, HalfTexel);
    return MapTexture.SampleLevel(MapSampler, mapUv, 0).y;
}

float Wobble(float elevation, float azimuth)
{
    float4 phase = shimmerPhase + ShimmerAzimuthFrequency * azimuth + ShimmerElevationFrequency * elevation;
    return shimmerAmplitude * dot(sin(phase), ShimmerWeight) / ShimmerWeightSum;
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
    float sineElevation = -worldDown;
    if (abs(sineElevation) > MaximumSine)
        return source;

    float elevation = ArcSine(sineElevation);
    float horizontal = sqrt(1.0 - sineElevation * sineElevation);
    float sinAzimuth = direction.x / horizontal;
    float cosAzimuth = worldForward / horizontal;
    float pixelAngle = 1.0 / focalLength;
    float fillStep = FillRadiusFraction * (inputBounds.z - inputBounds.x) / FillHalfTaps;

    float shake = 0.0;
    if (shimmerAmplitude > 0.0)
        shake = Wobble(elevation, sinAzimuth);

    float lowShift = ReadShift(elevation - 0.5 * pixelAngle + shake);
    float highShift = ReadShift(elevation + 0.5 * pixelAngle + shake);
    if (abs(lowShift) <= MinimumShift && abs(highShift) <= MinimumShift && abs(ReadShift(elevation + shake)) <= MinimumShift)
        return source;

    float stretch = abs(pixelAngle + highShift - lowShift) / pixelAngle;
    float taps = clamp(ceil(stretch), 1.0, (float)MaximumTaps);
    float4 sum = float4(0.0, 0.0, 0.0, 0.0);
    [loop]
    for (int tap = 0; tap < MaximumTaps; tap++)
    {
        if (tap >= taps)
            break;

        float sampled = elevation + ((tap + 0.5) / taps - 0.5) * pixelAngle;
        float lookup = sampled + shake;
        float seen = sampled + ReadShift(lookup);
        float sinSeen;
        float cosSeen;
        sincos(seen, sinSeen, cosSeen);
        float seenDown = -sinSeen;
        float seenForward = cosAzimuth * cosSeen;
        float3 seenDirection = float3(sinAzimuth * cosSeen, seenDown * pitchCosine + seenForward * pitchSine, -seenDown * pitchSine + seenForward * pitchCosine);
        float2 seenPosition = center + focalLength * seenDirection.xy / max(seenDirection.z, MinimumDepth);
        float2 lowerBound = inputBounds.xy + HalfTexel;
        float2 upperBound = inputBounds.zw - HalfTexel;
        if (ReadFill(lookup) > FillThreshold)
        {
            float4 blurred = float4(0.0, 0.0, 0.0, 0.0);
            [unroll]
            for (int offset = -FillHalfTaps; offset <= FillHalfTaps; offset++)
            {
                float2 shifted = clamp(seenPosition + float2(offset * fillStep, 0.0), lowerBound, upperBound);
                blurred += SourceTexture.SampleLevel(SourceSampler, uv0.xy + (shifted - scenePosition.xy) * uv0.zw, 0);
            }

            sum += blurred / FillTaps;
        }
        else
        {
            float2 clamped = clamp(seenPosition, lowerBound, upperBound);
            sum += SourceTexture.SampleLevel(SourceSampler, uv0.xy + (clamped - scenePosition.xy) * uv0.zw, 0);
        }
    }

    return lerp(source, sum / taps, amount);
}

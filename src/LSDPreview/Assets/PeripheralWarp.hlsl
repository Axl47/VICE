// Authored fullscreen shader for the owned LSD pass (Direct3D 11).
// Retain the native pass's property names/binding layout, but reinterpret them
// as aspect ratio, maximum UV displacement, elapsed time and focus radii.
// Draw only to a texture with explicit UVs. Unity's ordinary blit presents it
// to the screen; camera-global _FlipY no longer controls our vertex program.
cbuffer UnityPerMaterial : register(b1)
{
    float _NoiseScale : packoffset(c0.x); // aspect ratio
    float _Blend : packoffset(c0.y);      // maximum displacement / screen height
    float _Amplitude : packoffset(c0.z);  // elapsed seconds
    float2 _PanSpeed : packoffset(c1.x);  // inner and outer focus radii
    float _DoesBounce : packoffset(c1.z); // enable clear center
};
Texture2D<float4> _BlitTexture : register(t0);
SamplerState LinearClamp : register(s0);

struct Varyings
{
    float4 position : SV_POSITION;
    float4 uv : INTERP0;
    float4 unused : INTERP1;
};

Varyings Vert(uint vertexID : SV_VertexID)
{
    Varyings result;
    float2 uv = float2((vertexID << 1) & 2, vertexID & 2);
    result.position = float4(uv.x * 2 - 1, 1 - uv.y * 2, 0, 1);
    result.uv = float4(uv, 0, 0);
    result.unused = 0;
    return result;
}

float4 Frag(Varyings input) : SV_TARGET
{
    float2 uv = input.uv.xy;
    float aspect = max(0.1, _NoiseScale);
    float2 q = (uv - 0.5) * float2(aspect, 1);
    float focus = _DoesBounce > 0.5 ? smoothstep(_PanSpeed.x, _PanSpeed.y, length(q)) : 1;
    float edge = smoothstep(0, 0.015, min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)));
    float t = _Amplitude;
    // Uneven broad waves plus smaller drifting detail. Each axis is bounded
    // by one; division by sqrt(2) bounds the vector length to one.
    float2 broad = float2(sin(q.y * 8 + t * 0.55 + 0.6 * sin(q.x * 4 - t * 0.23)),
                          cos(q.x * 7 - t * 0.45 + 0.6 * sin(q.y * 5 + t * 0.31)));
    float2 detail = float2(sin(q.y * 19 - q.x * 4 - t * 0.70),
                           cos(q.x * 17 + q.y * 5 + t * 0.63));
    float2 flow = (0.72 * broad + 0.28 * detail) / 1.414214;
    float2 ringQ = q - float2(0.34 * sin(t * 0.17), 0.22 * cos(t * 0.13));
    float2 ripple = ringQ / max(length(ringQ), 0.001)
        * sin(length(ringQ) * 14 - t * 0.85 + 0.4 * sin(q.x * 3 + t * 0.17));
    float2 breathing = q / max(0.001, 0.5 * length(float2(aspect, 1))) * sin(t * 0.65);
    // All three vectors have length <= 1. Keep the existing pixel cap while
    // spending most of it on local movement rather than uniform radial zoom.
    float2 motion = 0.70 * flow + 0.20 * ripple + 0.10 * breathing;
    float2 offset = float2(motion.x / aspect, motion.y) * _Blend * focus * edge;
    return _BlitTexture.SampleLevel(LinearClamp, saturate(uv + offset), 0);
}

// Authored fullscreen shader for the owned LSD pass (Direct3D 11).
// Retain the native pass's property names/binding layout, but reinterpret them
// as aspect ratio, maximum UV displacement, elapsed time and focus radii.
cbuffer Globals : register(b0)
{
    float _FlipY : packoffset(c129.x);
};
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
    result.position = float4(uv * 2 - 1, 0, 1);
    result.uv = float4(uv.x, _FlipY < 0.5 ? 1 - uv.y : uv.y, 0, 0);
    result.unused = 0;
    return result;
}

float4 Frag(Varyings input) : SV_TARGET
{
    float2 uv = input.uv.xy;
    float aspect = max(0.1, _NoiseScale);
    float2 q = (uv - 0.5) * float2(aspect, 1);
    float focus = _DoesBounce > 0.5 ? smoothstep(_PanSpeed.x, _PanSpeed.y, length(q)) : 1;
    float edge = smoothstep(0, 0.025, min(min(uv.x, uv.y), min(1 - uv.x, 1 - uv.y)));
    float t = _Amplitude;
    // Each axis is bounded by 1.35; dividing by sqrt(2)*1.35 bounds
    // the vector's pixel length to one before applying the maximum.
    float2 flow = float2(sin(q.y * 10 + t * 0.29) + 0.35 * sin(q.x * 18 - t * 0.19),
                         cos(q.x * 11 - t * 0.24) + 0.35 * sin(q.y * 16 + t * 0.21)) / 1.91;
    float2 offset = float2(flow.x / aspect, flow.y) * _Blend * focus * edge;
    return _BlitTexture.SampleLevel(LinearClamp, saturate(uv + offset), 0);
}

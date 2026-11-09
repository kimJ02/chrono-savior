// LaserBeamSlice.shader의 본문 — 설명은 그 파일 머리.
#ifndef CHRONO_LASER_BEAM_SLICE
#define CHRONO_LASER_BEAM_SLICE

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _MainTex_TexelSize;
    float _SrcRows, _BodyRows, _WindowRows, _EdgeRows, _EdgeAlpha;
    float4 _GlowColor;
    float _Glow1, _Glow2, _TileWorld, _BeamLength, _FadeLength;
CBUFFER_END

struct Attributes
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
};

Varyings vert(Attributes v)
{
    Varyings o;
    o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
    o.color = v.color;
    o.uv = v.uv;
    return o;
}

// 몸체 k줄째(아래에서 0부터, 몸체 줄 수 b) → 텍스처에서 읽을 줄(아래에서 0부터). 위아래 e줄은 1:1, 가운데는 텍스처 가운데를 잘라 쓴다.
float SourceRow(float k, float b)
{
    float texRows = _MainTex_TexelSize.w;
    float bottom = floor(texRows * 0.5) - floor(_SrcRows * 0.5); // 띠의 맨 아래 줄(내용은 세로 가운데)
    float e = min(_EdgeRows, floor(b * 0.5));
    if (k < e) return bottom + k;
    if (k >= b - e) return bottom + _SrcRows - (b - k);
    float crop = floor(((_SrcRows - 2.0 * e) - (b - 2.0 * e)) * 0.5);
    return bottom + e + crop + (k - e);
}

// 선 폭 안 r줄째(아래에서 0부터)의 몸체 색 — 몸체 밖이면 투명
float4 BodyAt(float r, float x)
{
    float b = min(_BodyRows, _SrcRows);
    float k = r - floor((_WindowRows - b) * 0.5);
    if (k < 0.0 || k >= b) return float4(0, 0, 0, 0);
    float sr = SourceRow(k, b);
    float2 uv = float2((x + 0.5) * _MainTex_TexelSize.x, (sr + 0.5) * _MainTex_TexelSize.y);
    float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    if (k < 0.5 || k > b - 1.5) c.a *= _EdgeAlpha; // 가장 바깥 줄은 덜 딱딱하게
    return c;
}

half4 frag(Varyings i) : SV_Target
{
    float r = clamp(floor(saturate(i.uv.y) * _WindowRows), 0.0, _WindowRows - 1.0);
    float x = floor(frac(i.uv.x) * _MainTex_TexelSize.z);
    float4 c = BodyAt(r, x);
    if (c.a < 0.01)
    {
        // 바깥 빛 — 바로 옆(1줄)·한 줄 건너(2줄)에 몸체가 있으면 반투명 빛
        float near1 = max(BodyAt(r - 1.0, x).a, BodyAt(r + 1.0, x).a);
        float near2 = max(BodyAt(r - 2.0, x).a, BodyAt(r + 2.0, x).a);
        float g = near1 > 0.01 ? _Glow1 : (near2 > 0.01 ? _Glow2 : 0.0);
        c = float4(_GlowColor.rgb, g);
    }
    // 끝 흐림 — Tile 모드 UV.x × 텍스처 한 장 길이 = 총구에서 거리
    if (_FadeLength > 0.0)
    {
        float dist = i.uv.x * _TileWorld;
        c.a *= saturate((_BeamLength - dist) / _FadeLength);
    }
    c *= i.color;
    return half4(c);
}

#endif

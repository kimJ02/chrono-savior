// 메카닉 레이저 몸통 — 팩 빔 텍스처(가운데가 밝고 위아래 테두리가 있는 가로 띠)를 **세로 9-slice**로 그린다(2026-11-10 사용자 "레이저가 두껍다" → 개선 B, Codex 합의).
// - 크기를 줄이지 않는다: 위아래 _EdgeRows줄(밝은 테두리·진한 선·줄무늬)은 1:1, 가운데 코어 줄만 잘라 _BodyRows줄로 만든다(줄이면 도트 무늬가 뭉개졌다).
// - 가장 바깥 줄은 _EdgeAlpha만큼 투명, 그 바깥 2줄에 반투명 빛(_Glow1·_Glow2) — 불투명 띠만 있으면 무거워 보였다(다른 게임: 또렷한 몸체 + 얇은 빛).
// - 끝 _FadeLength(유닛) 동안 점점 투명 — 선을 Tile 모드로 깔면 UV.x = 길이 ÷ _TileWorld라 거리로 바꿔 쓴다.
// 선(LineRenderer) 폭은 _WindowRows줄(그림 px × 도트 배율)이고 몸체는 그 가운데. 행 계산은 그림 px 단위라 도트 크기가 다른 그림과 같다.
// 그림 프레임 교체·경로·상태는 C#(`GunnerBeamVfx`)이 맡고, 이 셰이더는 한 픽셀이 무슨 색인지만 정한다. 외주 그림으로 바꿀 땐 텍스처와 줄 수만 바꾸면 된다.
Shader "ChronoSavior/LaserBeamSlice"
{
    Properties
    {
        _MainTex ("빔 텍스처(가로 띠, 내용은 세로 가운데)", 2D) = "white" {}
        _SrcRows ("텍스처의 띠 줄 수", Float) = 16
        _BodyRows ("그릴 몸체 줄 수", Float) = 16
        _WindowRows ("선 폭 줄 수(몸체+빛)", Float) = 20
        _EdgeRows ("1:1로 남길 위아래 줄 수", Float) = 3
        _EdgeAlpha ("가장 바깥 줄 투명도", Float) = 0.6
        _GlowColor ("바깥 빛 색", Color) = (0.31, 0.71, 1, 1)
        _Glow1 ("빛 1줄째 알파", Float) = 0.43
        _Glow2 ("빛 2줄째 알파", Float) = 0.18
        _TileWorld ("텍스처 한 장의 길이(유닛)", Float) = 0.125
        _BeamLength ("보이는 빔 길이(유닛)", Float) = 100
        _FadeLength ("끝 흐림 길이(유닛, 0이면 안 흐림)", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "LaserBeamSlice.hlsl"
            ENDHLSL
        }
    }
}

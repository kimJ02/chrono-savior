// 피격 플래시 — 스프라이트 모양(알파)만 살리고 색은 꼭짓점 색(흰색 × 세기)으로 칠한다(2026-10-24, `Enemies/EnemyHealth`).
// 2026-11-10: Unity 6 URP에선 SpriteRenderer.color·flipX가 정점 색·정점이 아니라 `unity_SpriteColor`·`unity_SpriteProps`로 들어온다 —
// 예전엔 정점 색만 읽어서 잔상이 늘 흰색으로, 뒤집힌 그림은 안 뒤집힌 채로 나왔다(URP Sprite-Unlit-Default와 같은 방식으로 고침).
// 원본은 흰 타원을 덮지만(project_test.html:4799) 그림 몹에선 몸 모양이 자연스럽다. `SpriteRenderer.color`를 곱하는 방식은
// 흰색 그림을 더 밝게 못 해서 그림 몹에선 플래시가 안 보였다. Resources에 둬야 빌드에도 들어간다(`Resources.Load<Shader>`).
Shader "ChronoSavior/SpriteFlash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.color = v.color * unity_SpriteColor;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                return half4(i.color.rgb, i.color.a * a);
            }
            ENDHLSL
        }
    }
}

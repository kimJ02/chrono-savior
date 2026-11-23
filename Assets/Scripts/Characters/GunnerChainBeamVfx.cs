using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 레이저 "광선 연쇄" 한 번 튀기 그림 — 2026-11-23 사용자 "끊겨 보이고 부자연스럽다, 자연스럽게" → GPT와 정함.
    /// 본 레이저(<see cref="GunnerBeamVfx"/>)와 **같은 머티리얼·셰이더(LaserBeamSlice)·그림 조각(팩 laser center start/loop/end)**으로,
    /// 선 하나(LineRenderer, 길이 방향 타일)를 살짝 휜 곡선으로 그려 계단 깨짐·이음매가 없다. 몸체는 본 레이저보다 얇게(8줄, 선 폭 12줄).
    /// 순서: 앞 적 몸 가장자리에서 뻗기(start 프레임, 30fps) → 다음 적 가장자리에 닿으면 작은 적중 불꽃(팩 hit_spark, 빔 방향으로) →
    /// 0.08초 유지(loop, 15fps) → 꼬리부터 다음 적 쪽으로 거둬들이기(end 프레임, 30fps).
    /// 그림만이다(피해는 부르는 쪽). <see cref="Evaluate"/>로 아무 시점이나 그릴 수 있다(편집 화면 캡처용).
    /// </summary>
    public class GunnerChainBeamVfx : MonoBehaviour
    {
        public const float EdgeFps = 30f, LoopFps = 15f, HoldTime = 0.08f;
        public const int SourceRows = 16, BodyRows = 8, WindowRows = 12;
        /// <summary>곡선이 휘는 정도(화면 도트) — 직선이면 비스듬할 때 가장자리가 계단처럼 깨져 보였다.</summary>
        public const float BendDots = 3f;
        public const int SortingOrder = GunnerBeamVfx.SortingOrder;

        static readonly int SrcRowsId = Shader.PropertyToID("_SrcRows"), BodyRowsId = Shader.PropertyToID("_BodyRows"),
            WindowRowsId = Shader.PropertyToID("_WindowRows"), TileWorldId = Shader.PropertyToID("_TileWorld"),
            BeamLengthId = Shader.PropertyToID("_BeamLength"), FadeLengthId = Shader.PropertyToID("_FadeLength");

        /// <summary>
        /// 다음 적에 닿을 때 그림 — 2026-11-23 사용자가 후보 4개(실제 화면 캡처, <c>CaptureUiPreview.RunChainBeamHits</c>) 중 **D "하얀 번개 찌릿"**
        /// (팩 relay_aura, 맞은 적 가운데, 돌리지 않음, 30fps)을 골랐다. 캡처 비교용으로 바꿀 수 있게 static으로 둔다(게임 코드는 안 바꾼다).
        /// </summary>
        public static string HitKey = VfxKeys.MechanicRelayAura;
        public static float HitFps = EdgeFps;
        public static bool HitAtCenter = true, HitRotate = false;

        LineRenderer line;
        Vector2? targetCenter;
        VfxPlayer hit;
        Material mat;
        Sprite[] startF, loopF, endF;
        Vector2 a, b, control;
        float delay, age;
        bool sparked;
        readonly List<Vector3> pts = new List<Vector3>();

        /// <summary>뻗는 시간·사라지는 시간·전체 길이(초).</summary>
        public float ExtendTime => Mathf.Max(0.05f, (startF?.Length ?? 0) / EdgeFps);
        public float EndTime => Mathf.Max(0.05f, (endF?.Length ?? 0) / EdgeFps);
        public float Duration => ExtendTime + HoldTime + EndTime;
        /// <summary>앞 적에서 뻗기 시작해 다음 적에 닿기까지(초) — 다음 튀기를 이 뒤에 건다.</summary>
        public float ArriveTime => delay + ExtendTime;

        /// <summary>
        /// from(앞 적 몸 가장자리)에서 to(다음 적 몸 가장자리)로 한 번 튄다. <paramref name="delay"/>초 뒤에 시작. 머티리얼이 없으면 null.
        /// <paramref name="bendSign"/>: 휘는 쪽(+1·-1) — 연쇄마다 번갈아 주면 지그재그로 읽힌다.
        /// </summary>
        /// <paramref name="hitSpark"/>: 닿을 때 적중 그림(D 하얀 번개)을 띄울지 — 편지 배달처럼 닿은 자리에 다른 그림이 붙으면 끈다.
        public static GunnerChainBeamVfx Play(Material material, Vector2 from, Vector2 to, float delay = 0f, float bendSign = 1f, Vector2? targetCenter = null,
                                              bool hitSpark = true)
        {
            if (material == null) return null;
            var go = new GameObject("GunnerChainBeam");
            RunTransient.Mark(go);
            var v = go.AddComponent<GunnerChainBeamVfx>();
            v.mat = new Material(material) { name = "GunnerChainBeam (instance)" };
            v.line = go.AddComponent<LineRenderer>();
            v.line.sharedMaterial = v.mat;
            v.line.useWorldSpace = true;
            v.line.textureMode = LineTextureMode.Tile;
            v.line.numCapVertices = 0;
            v.line.alignment = LineAlignment.TransformZ;
            v.line.sortingOrder = SortingOrder;
            v.startF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "start"));
            v.loopF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "loop"));
            v.endF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "end"));
            v.a = from;
            v.b = to;
            Vector2 d = to - from;
            Vector2 n = d.sqrMagnitude > 1e-6f ? new Vector2(-d.y, d.x).normalized : Vector2.up;
            v.control = (from + to) * 0.5f + n * DotScale.Units(BendDots) * Mathf.Sign(bendSign == 0f ? 1f : bendSign);
            v.delay = Mathf.Max(0f, delay);
            v.targetCenter = targetCenter;
            v.sparked = !hitSpark; // 안 띄우면 이미 띄운 것으로 친다
            v.mat.SetFloat(SrcRowsId, SourceRows);
            v.mat.SetFloat(BodyRowsId, BodyRows);
            v.mat.SetFloat(WindowRowsId, WindowRows);
            v.line.widthMultiplier = DotScale.Units(WindowRows);
            v.Evaluate(0f);
            return v;
        }

        void Update()
        {
            age += Time.deltaTime;
            Evaluate(age);
            if (age >= delay + Duration) Destroy(gameObject);
        }

        /// <summary>시작부터 <paramref name="time"/>초 뒤의 모습을 그린다.</summary>
        public void Evaluate(float time)
        {
            float t = time - delay;
            if (!Application.isPlaying && hit != null) hit.SetTimeForCapture(t - ExtendTime); // 편집 화면 캡처 — 적중 그림도 시간을 따라간다
            if (t < 0f || t >= Duration || loopF == null || loopF.Length == 0) { line.enabled = false; return; }

            float head, tail;
            Sprite body;
            if (t < ExtendTime)
            {
                float k = t / ExtendTime;
                head = 1f - (1f - k) * (1f - k); // 빠르게 뻗다가 닿을 때 살짝 느려진다
                tail = 0f;
                body = startF.Length > 0 ? startF[Mathf.Min(startF.Length - 1, Mathf.FloorToInt(t * EdgeFps))] : loopF[0];
            }
            else if (t < ExtendTime + HoldTime)
            {
                head = 1f; tail = 0f;
                body = loopF[Mathf.FloorToInt((t - ExtendTime) * LoopFps) % loopF.Length];
                if (!sparked) Spark();
            }
            else
            {
                float k = (t - ExtendTime - HoldTime) / EndTime;
                head = 1f;
                tail = k * k; // 꼬리부터 다음 적 쪽으로 거둬들인다
                body = endF.Length > 0 ? endF[Mathf.Min(endF.Length - 1, Mathf.FloorToInt((t - ExtendTime - HoldTime) * EdgeFps))] : loopF[0];
                if (!sparked) Spark();
            }

            mat.mainTexture = body.texture;
            float tileWorld = DotScale.Units(body.rect.width);
            line.textureScale = new Vector2(1f / tileWorld, 1f);
            mat.SetFloat(TileWorldId, tileWorld);

            // 곡선 위 tail~head 구간을 고른 간격으로(텍스처가 거리로 이어지게)
            pts.Clear();
            const int Samples = 24;
            float len = 0f;
            Vector2 prev = Bezier(tail);
            pts.Add(prev);
            for (int i = 1; i <= Samples; i++)
            {
                Vector2 p = Bezier(Mathf.Lerp(tail, head, i / (float)Samples));
                len += Vector2.Distance(prev, p);
                pts.Add(p);
                prev = p;
            }
            line.enabled = len > 1e-3f;
            line.positionCount = pts.Count;
            line.SetPositions(pts.ToArray());
            mat.SetFloat(BeamLengthId, len);
            mat.SetFloat(FadeLengthId, t < ExtendTime ? Mathf.Min(DotScale.Units(4f), len * 0.5f) : 0f); // 뻗는 동안만 앞끝을 살짝 흐린다
        }

        Vector2 Bezier(float u) => (1f - u) * (1f - u) * a + 2f * (1f - u) * u * control + u * u * b;

        void Spark()
        {
            sparked = true;
            Vector2 dir = b - control;
            Vector2 at = HitAtCenter && targetCenter.HasValue ? targetCenter.Value : b;
            hit = VfxPlayer.Spawn(HitKey, at, DotScale.Value, HitFps, SortingOrder + 1);
            if (hit != null && HitRotate && dir.sqrMagnitude > 1e-6f) hit.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            if (hit != null && !Application.isPlaying) hit.SetTimeForCapture(0f);
        }

        void OnDestroy()
        {
            if (mat == null) return;
            if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat); // 편집 화면 캡처에서도 지운다
        }
    }
}

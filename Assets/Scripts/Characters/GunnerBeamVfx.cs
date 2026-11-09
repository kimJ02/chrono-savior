using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 레이저 빔 그림 — 그림만이다(판정·피해는 <see cref="GunnerLaserBeam"/>).
    /// 2026-11-09 사용자 "레이저가 중간에 뚝 끊긴다" → Codex와 다시 짬: 예전엔 몸통을 조각 스프라이트(타일)로 이어 붙였는데, 프레임이 바뀔 때 길이가
    /// 원래 그림 크기로 돌아가는 프레임이 생기고 곡선(5층)은 조각마다 무늬가 끊겼다. 이제 **선 하나**(<see cref="LineRenderer"/>, 길이 방향 타일)로
    /// 직선·곡선을 끊김 없이 그린다 — Projectiles Pack 4 `laser_beam_B` 파랑 몸통 텍스처를 길이만큼 반복(도트 크기 그대로).
    /// **두께(2026-11-10 사용자 "레이저가 두꺼워 어색하다" → 예시 그림에서 '팩 B형 + 개선 B' 고름, Codex xhigh 합의)**: 몸통은 셰이더
    /// `Assets/Shaders/LaserBeamSlice`(머티리얼 `Assets/Materials/LaserBeam`)가 그린다 — 그림을 줄이지 않고 위아래 테두리 3줄씩은 그대로, 가운데 코어 줄만 잘라
    /// 층마다 <see cref="GunnerLaserBeam.ArtBodyRows"/>줄(1·2층 10, 3층 12, 4층 14, 5층 16), 그 바깥에 반투명 빛 2줄. 끝 흐림도 셰이더가 거리로 한다.
    /// 끝 빛 알갱이는 Particle System 프리팹(<see cref="GunnerAttack.LaserTipPrefab"/>, `Assets/Prefabs/Combat/LaserTipSparks`).
    /// - 켜질 때 start 프레임 → 켜져 있는 동안 loop → 떼면 end 프레임으로 사그라든다(마지막 모양 그대로).
    /// - 총구에 origin 그림, 끝 impact 그림(늘 small — large는 2유닛이라 과했다)은 **목표를 잡았을 때만**. 맞은 적 자리엔 작은 파란 충격(0.5유닛)을
    ///   **적마다 <see cref="SparkInterval"/>초에 한 번만**(2026-11-09 사용자 — 예전엔 빔 끝 물보라를 적마다 피해 틱마다 띄워 큰 덩어리로 겹쳤다).
    /// - 맞을 적이 없으면 빔 대신 총구에 **차징** 그림(기가팩 `scifi_charge_up`을 파랑으로 돌린 것)만 — 적이 광선에 들어오면 그때 빔이 start 프레임부터 뻗는다(2026-11-09 사용자).
    /// - **끝이 뚝 끊기지 않게**(2026-11-09 사용자, Codex 합의 — Positech·Source 엔진처럼 빔 끝을 흐린다):
    ///   · 공간: 허공에서 끝나면 끝 <see cref="FadeLength"/>유닛 동안 점점 투명(목표에 닿아 끝 그림이 붙은 5층 곡선은 그 그림이 끝을 맡아 안 흐린다) + 끝에서 앞으로 흩어지는 빛 알갱이.
    ///   · 떼면: end 프레임을 팩 권장 15fps(<see cref="EndFps"/>, 약 0.33초)로 돌리며 앞끝부터 총구로 빨려 들어간다(예전엔 30fps로 0.17초 만에 꺼졌다).
    ///   · 맞을 적이 없어지면(0.35초 유지 뒤): 같은 방식으로 빨려 들어간 다음 차징 모습으로 이어진다(예전엔 빔이 꺼지고 차징이 툭 나타났다).
    ///   얇아지게(폭 줄이기)는 안 한다 — 줄무늬 도트가 세로로 찌그러진다.
    /// - 그림이 없으면 하늘색 선(판정 굵기).
    /// 선은 머티리얼이 필요해 메카닉 키트의 씬 연결 칸(<see cref="GunnerAttack.BeamMaterial"/>)을 복제해 쓴다.
    /// </summary>
    public class GunnerBeamVfx : MonoBehaviour
    {
        public const float LoopFps = 15f, EdgeFps = 30f, EndFps = 15f;
        /// <summary>빔 끝이 투명해지는 길이(유닛) — 빔이 짧으면 길이의 절반까지만.</summary>
        public const float FadeLength = 0.75f;
        /// <summary>선 폭(그림 줄 수) — 가장 두꺼운 몸체 16줄 + 위아래 빛 2줄씩. 셰이더가 이 안에 몸체를 가운데 놓고 바깥은 투명하게.</summary>
        public const int WindowRows = 20;
        /// <summary>팩 B형 몸통 텍스처의 띠 줄 수(텍스처 세로 가운데에 있다).</summary>
        public const int SourceRows = 16;
        readonly List<Vector2> basePath = new List<Vector2>();
        readonly List<Vector3> linePts = new List<Vector3>();
        Color baseColor = Color.white;
        ParticleSystem tipSparks;
        int bodyRows = SourceRows;
        bool thenCharge;
        Vector2 chargeAt, chargeDir;
        /// <summary>같은 적에게 적중 충격을 다시 띄우기까지(초).</summary>
        public const float SparkInterval = 0.25f;
        readonly Dictionary<Collider2D, float> lastSpark = new Dictionary<Collider2D, float>();
        public const int SortingOrder = 3;

        LineRenderer line;
        Material mat;
        SpriteRenderer origin, impact, charge;
        Sprite[] chargeF;
        bool charging;
        float chargeT;
        public const float ChargeFps = 15f;
        Sprite[] startF, loopF, endF, originF, impactF;
        bool hasArt, ending;
        int lastShownFrame = -1;
        float t;
        Vector2 dirStart, dirEnd;
        bool impactOn;
        float impactT;
        SpriteRenderer muzzleGlow, muzzleCore;
        /// <summary>
        /// 총구 그림(팩 origin 34줄)은 켜지는 순간 섬광으로만, 목표 끝 그림(impact 58줄)은 닿는 순간에만 — 얇아진 몸체(10~16줄)에 계속 붙어 있으면
        /// 시작점·끝점이 부풀어 보였다(2026-11-10 캡처, Codex 합의). 그 뒤 총구엔 몸체만 한 작은 빛이 남아 빔이 총구에서 떨어져 보이지 않게 한다.
        /// </summary>
        public const float MuzzleFlashTime = 0.1f, ImpactFlashTime = 0.15f;
        // 총구 빛 — 빔과 같은 색 구성: 바깥은 셰이더 빛 띠 색(반투명), 가운데는 빔 코어 색
        static readonly Color MuzzleGlowColor = new Color(0.31f, 0.71f, 1f, 0.43f), MuzzleCoreColor = new Color(0.89f, 0.95f, 0.95f, 1f);

        /// <summary>그림 담당을 만든다 — 머티리얼이 없으면(씬 없이 만든 테스트 키트) null. 끝 빛 알갱이 프리팹은 없어도 된다(알갱이만 빠진다).</summary>
        public static GunnerBeamVfx Create(Material material, ParticleSystem tipPrefab = null)
        {
            if (material == null) return null;
            var go = new GameObject("GunnerBeam");
            RunTransient.Mark(go);
            var v = go.AddComponent<GunnerBeamVfx>();
            v.mat = new Material(material) { name = "GunnerBeam (instance)" };
            v.line = go.AddComponent<LineRenderer>();
            v.line.sharedMaterial = v.mat;
            v.line.useWorldSpace = true;
            v.line.textureMode = LineTextureMode.Tile;
            v.line.numCapVertices = 0;
            v.line.alignment = LineAlignment.TransformZ;
            v.line.sortingOrder = SortingOrder;
            v.origin = NewCap(go.transform, "Origin");
            v.impact = NewCap(go.transform, "Impact");
            v.charge = NewCap(go.transform, "Charge");
            var mg = new GameObject("MuzzleGlow", typeof(SpriteRenderer));
            mg.transform.SetParent(go.transform, false);
            v.muzzleGlow = mg.GetComponent<SpriteRenderer>();
            v.muzzleGlow.sortingOrder = SortingOrder + 1;
            var mc = new GameObject("MuzzleCore", typeof(SpriteRenderer));
            mc.transform.SetParent(go.transform, false);
            v.muzzleCore = mc.GetComponent<SpriteRenderer>();
            v.muzzleCore.sortingOrder = SortingOrder + 2;
            if (tipPrefab != null)
            {
                v.tipSparks = Instantiate(tipPrefab, go.transform);
                v.tipSparks.name = "TipSparks";
                SetEmitting(v.tipSparks, false);
            }
            go.SetActive(false);
            return v;
        }

        static SpriteRenderer NewCap(Transform parent, string name)
        {
            var c = new GameObject(name, typeof(SpriteRenderer));
            c.transform.SetParent(parent, false);
            c.transform.localScale = new Vector3(DotScale.Value, DotScale.Value, 1f);
            var sr = c.GetComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrder;
            return sr;
        }

        /// <summary>지금 보이나·끝 그림이 붙어 있나·선 점 개수(테스트용).</summary>
        public bool Visible => gameObject.activeSelf;
        public bool ImpactVisible => impact != null && impact.enabled;
        /// <summary>빔 대신 차징 모습인가(테스트용).</summary>
        public bool Charging => gameObject.activeSelf && charging;
        public LineRenderer Line => line;
        /// <summary>지금 그리는 빔 길이(유닛, 빨려 들어가는 중엔 줄어든다 — 테스트용).</summary>
        public float VisibleLength { get; private set; }
        /// <summary>끝 빛 알갱이(테스트용 — 프리팹이 없으면 null).</summary>
        public ParticleSystem TipSparks => tipSparks;
        /// <summary>지금 그리는 몸체 줄 수(테스트용).</summary>
        public int BodyRows => bodyRows;
        /// <summary>몸통 머티리얼(복제본, 테스트용).</summary>
        public Material BodyMaterial => mat;

        /// <summary>
        /// 이번 프레임의 빔 — 경로(총구부터 끝까지, 직선이면 두 점), 층, 끝 그림을 붙일지(목표를 잡았을 때). 판정 굵기는 그림이 없을 때만 쓴다.
        /// </summary>
        public void Show(IReadOnlyList<Vector2> path, int tier, bool showImpact, float hitWidth)
        {
            if (path == null || path.Count < 2) return;
            if (!gameObject.activeSelf || ending || charging)
            {
                gameObject.SetActive(true);
                ending = false;
                charging = false;
                thenCharge = false;
                t = 0f; // 처음부터(start 프레임) — 차징하다가 적이 들어와도 여기서 빔이 뻗기 시작한다
            }
            if (startF == null) LoadFrames();
            bodyRows = GunnerLaserBeam.ArtBodyRows(tier);
            basePath.Clear();
            for (int i = 0; i < path.Count; i++) basePath.Add(path[i]);
            dirStart = (path[1] - path[0]).normalized;
            dirEnd = (path[path.Count - 1] - path[path.Count - 2]).normalized;
            if (showImpact && !impactOn) impactT = 0f; // 목표에 막 닿았다 — 끝 그림을 짧게
            impactOn = showImpact;
            if (!hasArt)
            {
                line.widthMultiplier = hitWidth * 2f;
                baseColor = new Color(GunnerAttack.LaserColor.r, GunnerAttack.LaserColor.g, GunnerAttack.LaserColor.b, 0.85f);
            }
            lastShownFrame = Time.frameCount;
            Apply();
        }

        /// <summary>맞을 적이 없다 — 빔 대신 총구에서 차징하는 모습만.</summary>
        public void ShowCharging(Vector2 at, Vector2 dir)
        {
            if (chargeF == null) chargeF = VfxFrames.Get(VfxKeys.MechanicCharge);
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
            // 빔이 켜져 있었으면 바로 바꾸지 않고 총구로 빨려 들어간 뒤 차징으로 이어진다
            if (gameObject.activeSelf && !charging && hasArt)
            {
                if (!ending) BeginEnd();
                thenCharge = true;
                chargeAt = at;
                chargeDir = dir.normalized;
                lastShownFrame = Time.frameCount;
                return;
            }
            if (!gameObject.activeSelf || !charging)
            {
                gameObject.SetActive(true);
                ending = false;
                charging = true;
                chargeT = 0f;
            }
            line.enabled = false;
            origin.enabled = impact.enabled = muzzleGlow.enabled = muzzleCore.enabled = false;
            charge.enabled = chargeF.Length > 0;
            SetEmitting(tipSparks, false);
            charge.transform.position = at + dir.normalized * DotScale.Units(6f); // 총구 바로 앞
            lastShownFrame = Time.frameCount;
            ApplyCharge();
        }

        void ApplyCharge()
        {
            if (!charge.enabled) return;
            charge.sprite = chargeF[Mathf.FloorToInt(chargeT * ChargeFps) % chargeF.Length];
        }

        /// <summary>쏘기를 멈췄다 — end 프레임으로 사그라든 뒤 숨는다.</summary>
        public void Release()
        {
            thenCharge = false; // 빨려 들어가는 중에 떼면 차징 없이 끝난다
            if (!gameObject.activeSelf || ending) return;
            if (charging) { charging = false; gameObject.SetActive(false); return; } // 차징만 하다 뗐으면 바로 끈다
            if (!hasArt) { gameObject.SetActive(false); return; }
            BeginEnd();
        }

        void BeginEnd()
        {
            ending = true;
            t = 0f;
            Apply();
        }

        /// <summary>떼거나 적을 잃었을 때 사그라드는 시간(초) — end 프레임 수 ÷ <see cref="EndFps"/>.</summary>
        public float EndDuration => endF != null && endF.Length > 0 ? endF.Length / EndFps : 0f;

        /// <summary>바로 숨긴다(캐릭터 전환 등).</summary>
        public void HideNow()
        {
            ending = false;
            charging = false;
            thenCharge = false;
            gameObject.SetActive(false);
        }

        /// <summary>빔에 맞은 적 자리에 작은 충격(그림이 없으면 아무것도 안 한다).</summary>
        public void Spark(Collider2D enemy, Vector2 at, Vector2 dir)
        {
            if (enemy != null)
            {
                if (lastSpark.TryGetValue(enemy, out float last) && Time.time - last < SparkInterval) return;
                lastSpark[enemy] = Time.time;
                if (lastSpark.Count > 64) lastSpark.Clear(); // 죽은 적이 쌓이지 않게(대충 비워도 충격이 한 번 더 뜰 뿐이다)
            }
            var p = VfxPlayer.Spawn(VfxKeys.MechanicHitSpark, at, DotScale.Value, EdgeFps, SortingOrder + 1);
            if (p != null && dir.sqrMagnitude > 1e-6f) p.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }

        void LoadFrames()
        {
            startF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "start"));
            loopF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "loop"));
            endF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "end"));
            originF = VfxFrames.Get(VfxKeys.MechanicLaser("origin", "loop"));
            impactF = VfxFrames.Get(VfxKeys.MechanicLaser("impact", "loop"));
            hasArt = loopF.Length > 0;
            mat.mainTexture = null;
            baseColor = Color.white;
        }

        void Update()
        {
            t += Time.deltaTime; // 일시정지·히트스톱 땐 같이 멈춘다
            impactT += Time.deltaTime;
            if (charging) { chargeT += Time.deltaTime; ApplyCharge(); return; }
            Apply();
        }

        // 키트가 이번 프레임에 안 그렸으면(캐릭터가 바뀌어 키트가 꺼졌다 등) 사그라든다 — 모든 Update 뒤에 본다
        void LateUpdate()
        {
            if (!ending && lastShownFrame != Time.frameCount && Time.deltaTime > 0f) Release();
        }

        void Apply()
        {
            line.enabled = true;
            charge.enabled = false;
            if (!hasArt)
            {
                origin.enabled = impact.enabled = muzzleGlow.enabled = muzzleCore.enabled = false;
                return;
            }
            Sprite body;
            float alpha = 1f, keep = 1f;
            if (ending)
            {
                int i = Mathf.FloorToInt(t * EndFps);
                if (i >= endF.Length) { FinishEnd(); return; }
                body = endF[i];
                float k = Mathf.Clamp01(t / EndDuration);
                keep = 1f - k;            // 앞끝부터 총구로 빨려 들어간다
                alpha = 1f - 0.5f * k;    // 같이 조금씩 옅어진다
            }
            else
            {
                int i = Mathf.FloorToInt(t * EdgeFps);
                body = i < startF.Length ? startF[i] : loopF[Mathf.FloorToInt((t - startF.Length / EdgeFps) * LoopFps) % loopF.Length];
            }
            // 몸통 — 텍스처 한 장(16px)을 도트 배율 그대로 길이 방향으로 반복. 선 폭은 몸체+빛 20줄, 몸체 줄 수·끝 흐림은 셰이더가
            mat.mainTexture = body.texture;
            float tileWorld = DotScale.Units(body.rect.width);
            line.widthMultiplier = DotScale.Units(WindowRows);
            line.textureScale = new Vector2(1f / tileWorld, 1f);
            mat.SetFloat(SrcRowsId, SourceRows);
            mat.SetFloat(BodyRowsId, bodyRows);
            mat.SetFloat(WindowRowsId, WindowRows);
            mat.SetFloat(TileWorldId, tileWorld);
            // 목표에 닿아 끝나면(5층 곡선) 아주 짧게만 흐린다 — 길게 흐리면 목표 앞에 희뿌연 판이 보였다
            BuildLine(keep, alpha, impactOn && !ending ? DotScale.Units(4f) : FadeLength);

            int loopI = Mathf.FloorToInt(t * LoopFps);
            PlaceCap(origin, originF, loopI, basePath[0], dirStart, +1f, !ending && t < MuzzleFlashTime, alpha);
            PlaceCap(impact, impactF, loopI, basePath[basePath.Count - 1], dirEnd, -1f, impactOn && !ending && impactT < ImpactFlashTime, alpha);
            // 총구의 작은 빛 — 가운데 밝은 원(몸체 줄 수 − 2px 지름) + 바깥 반투명 빛(몸체 + 4px), 빔 머리처럼 둥글게
            float glowR = DotScale.Units(bodyRows * 0.5f + 2f), coreR = DotScale.Units(bodyRows * 0.5f - 1f);
            muzzleGlow.enabled = muzzleCore.enabled = VisibleLength > 0.01f;
            muzzleGlow.sprite = DotShapes.Ellipse(glowR, glowR);
            muzzleCore.sprite = DotShapes.Ellipse(coreR, coreR);
            muzzleGlow.transform.position = muzzleCore.transform.position = basePath[0];
            muzzleGlow.color = new Color(MuzzleGlowColor.r, MuzzleGlowColor.g, MuzzleGlowColor.b, MuzzleGlowColor.a * alpha);
            muzzleCore.color = new Color(MuzzleCoreColor.r, MuzzleCoreColor.g, MuzzleCoreColor.b, MuzzleCoreColor.a * alpha);

            // 끝 빛 알갱이 — 흐려지는 끝에서 앞으로 흩어진다(빨려 들어갈 땐 새로 안 생기고 남은 것만 사라진다).
            // 목표 끝 그림이 붙은 5층 곡선은 그 그림이 끝을 맡는다
            SetEmitting(tipSparks, !ending && !impactOn && VisibleLength > 0.01f);
        }

        static readonly int SrcRowsId = Shader.PropertyToID("_SrcRows"), BodyRowsId = Shader.PropertyToID("_BodyRows"),
            WindowRowsId = Shader.PropertyToID("_WindowRows"), TileWorldId = Shader.PropertyToID("_TileWorld"),
            BeamLengthId = Shader.PropertyToID("_BeamLength"), FadeLengthId = Shader.PropertyToID("_FadeLength");

        static void SetEmitting(ParticleSystem ps, bool on)
        {
            if (ps == null) return;
            var em = ps.emission;
            em.enabled = on;
        }

        /// <summary>사그라들기가 끝났다 — 적을 잃어서였으면 차징으로 이어지고, 아니면 숨는다.</summary>
        void FinishEnd()
        {
            ending = false;
            if (thenCharge)
            {
                thenCharge = false;
                charging = true;
                chargeT = 0f;
                line.enabled = false;
                origin.enabled = impact.enabled = muzzleGlow.enabled = muzzleCore.enabled = false;
                SetEmitting(tipSparks, false);
                charge.enabled = chargeF != null && chargeF.Length > 0;
                charge.transform.position = chargeAt + chargeDir * DotScale.Units(6f);
                ApplyCharge();
                return;
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 경로를 같은 간격으로 다시 찍어 선을 만든다 — 앞에서부터 <paramref name="keep"/>(0~1)만큼만, 끝 <see cref="FadeLength"/>는 점점 투명.
        /// 끝 흐림은 셰이더가 UV(Tile 모드 = 거리)로 한다 — 정점 간격을 고르게 둬 곡선에서도 UV가 거리와 맞게.
        /// </summary>
        void BuildLine(float keep, float alpha, float fadeLength)
        {
            float total = 0f;
            for (int i = 1; i < basePath.Count; i++) total += Vector2.Distance(basePath[i - 1], basePath[i]);
            float len = total * Mathf.Clamp01(keep);
            VisibleLength = len;
            linePts.Clear();
            if (len < 1e-3f) { line.positionCount = 0; return; }
            float fade = Mathf.Min(fadeLength, len * 0.5f);
            // 점 간격 — 곡선이 각지지 않게 0.125유닛 이하, 흐림 구간엔 3칸 이상
            int n = Mathf.Max(2, Mathf.CeilToInt(len / Mathf.Min(0.125f, Mathf.Max(fade, 0.01f) / 3f)));
            float step = len / n;
            int fadeSegs = Mathf.Clamp(Mathf.RoundToInt(fade / step), 1, n - 1);
            int seg = 1;
            float segStart = 0f;
            for (int k = 0; k <= n; k++)
            {
                float d = k * step;
                while (seg < basePath.Count - 1 && segStart + Vector2.Distance(basePath[seg - 1], basePath[seg]) < d)
                {
                    segStart += Vector2.Distance(basePath[seg - 1], basePath[seg]);
                    seg++;
                }
                float sl = Vector2.Distance(basePath[seg - 1], basePath[seg]);
                Vector2 p = sl > 1e-6f ? Vector2.Lerp(basePath[seg - 1], basePath[seg], Mathf.Clamp01((d - segStart) / sl)) : basePath[seg];
                linePts.Add(p);
            }
            line.positionCount = linePts.Count;
            for (int i = 0; i < linePts.Count; i++) line.SetPosition(i, linePts[i]);

            var col = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
            line.startColor = line.endColor = col;
            mat.SetFloat(BeamLengthId, len);
            mat.SetFloat(FadeLengthId, fade);

            Vector2 a = linePts[linePts.Count - 2], b = linePts[linePts.Count - 1];
            Vector2 dir = (b - a).sqrMagnitude > 1e-8f ? (b - a).normalized : dirEnd;
            if (tipSparks != null)
            {
                // 흐려지는 구간(끝 쪽 fade 길이) 안에서 생겨 앞으로 흩어진다 — 모양 상자를 그 구간에 맞춘다
                var tf = tipSparks.transform;
                tf.position = b;
                tf.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                var shape = tipSparks.shape;
                // 빔 끝에 붙어 나오게 — 흐림 구간 끝 절반에서만
                shape.position = new Vector3(-fade * 0.25f, 0f, 0f);
                shape.scale = new Vector3(fade * 0.5f, DotScale.Units(bodyRows) * 0.7f, 0f);
            }
        }

        /// <summary>총구·끝 그림 — 그 끝점에서 빛줄기 쪽으로 그림 반 폭만큼 들여 놓는다(<paramref name="inward"/> +1 = 앞으로, −1 = 뒤로).</summary>
        static void PlaceCap(SpriteRenderer sr, Sprite[] frames, int index, Vector2 at, Vector2 dir, float inward, bool on, float alpha)
        {
            sr.enabled = on && frames.Length > 0;
            if (!sr.enabled) return;
            sr.sprite = frames[index % frames.Length];
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
            float half = sr.sprite.bounds.size.x * DotScale.Value * 0.5f;
            sr.transform.position = at + dir * (half * inward);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.color = new Color(1f, 1f, 1f, alpha);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}

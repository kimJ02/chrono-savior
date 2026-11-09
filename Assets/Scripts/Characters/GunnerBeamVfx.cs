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
    /// 직선·곡선을 끊김 없이 그린다 — Projectiles Pack 4 `laser_beam_A` 파랑 몸통 텍스처를 길이만큼 반복(도트 크기 그대로).
    /// - 켜질 때 start 프레임 → 켜져 있는 동안 loop → 떼면 end 프레임으로 사그라든다(마지막 모양 그대로).
    /// - 총구에 origin 그림, 끝 impact 그림(늘 small — large는 2유닛이라 과했다)은 **목표를 잡았을 때만**. 맞은 적 자리엔 작은 파란 충격(0.5유닛)을
    ///   **적마다 <see cref="SparkInterval"/>초에 한 번만**(2026-11-09 사용자 — 예전엔 빔 끝 물보라를 적마다 피해 틱마다 띄워 큰 덩어리로 겹쳤다).
    /// - 맞을 적이 없으면 빔 대신 총구에 **차징** 그림(기가팩 `scifi_charge_up`을 파랑으로 돌린 것)만 — 적이 광선에 들어오면 그때 빔이 start 프레임부터 뻗는다(2026-11-09 사용자).
    /// - 굵기: 1~4층 small(띠 26px → 0.4유닛), 5층 large(50px → 0.78유닛). 그림이 없으면 원본처럼 하늘색 선(판정 굵기).
    /// 선은 머티리얼이 필요해 메카닉 키트의 씬 연결 칸(<see cref="GunnerAttack.BeamMaterial"/>)을 복제해 쓴다.
    /// </summary>
    public class GunnerBeamVfx : MonoBehaviour
    {
        public const float LoopFps = 15f, EdgeFps = 30f;
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
        bool hasArt, large, ending;
        int lastShownFrame = -1;
        float t;
        Vector3[] pts = new Vector3[0];
        Vector2 dirStart, dirEnd;
        bool impactOn;

        /// <summary>그림 담당을 만든다 — 머티리얼이 없으면(씬 없이 만든 테스트 키트) null.</summary>
        public static GunnerBeamVfx Create(Material material)
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
                t = 0f; // 처음부터(start 프레임) — 차징하다가 적이 들어와도 여기서 빔이 뻗기 시작한다
            }
            bool wantLarge = GunnerLaserBeam.LargeArt(tier);
            if (startF == null || wantLarge != large) LoadFrames(wantLarge);
            if (pts.Length != path.Count) pts = new Vector3[path.Count];
            for (int i = 0; i < path.Count; i++) pts[i] = path[i];
            line.positionCount = pts.Length;
            line.SetPositions(pts);
            dirStart = (path[1] - path[0]).normalized;
            dirEnd = (path[path.Count - 1] - path[path.Count - 2]).normalized;
            impactOn = showImpact;
            if (!hasArt)
            {
                line.widthMultiplier = hitWidth * 2f;
                line.startColor = line.endColor = new Color(GunnerAttack.LaserColor.r, GunnerAttack.LaserColor.g, GunnerAttack.LaserColor.b, 0.85f);
            }
            lastShownFrame = Time.frameCount;
            Apply();
        }

        /// <summary>맞을 적이 없다 — 빔 대신 총구에서 차징하는 모습만.</summary>
        public void ShowCharging(Vector2 at, Vector2 dir)
        {
            if (chargeF == null) chargeF = VfxFrames.Get(VfxKeys.MechanicCharge);
            if (!gameObject.activeSelf || !charging)
            {
                gameObject.SetActive(true);
                ending = false;
                charging = true;
                chargeT = 0f;
            }
            line.enabled = false;
            origin.enabled = impact.enabled = false;
            charge.enabled = chargeF.Length > 0;
            if (dir.sqrMagnitude < 1e-8f) dir = Vector2.right;
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
            if (!gameObject.activeSelf || ending) return;
            if (charging) { charging = false; gameObject.SetActive(false); return; } // 차징만 하다 뗐으면 바로 끈다
            if (!hasArt) { gameObject.SetActive(false); return; }
            ending = true;
            t = 0f;
            Apply();
        }

        /// <summary>바로 숨긴다(캐릭터 전환 등).</summary>
        public void HideNow()
        {
            ending = false;
            charging = false;
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

        void LoadFrames(bool wantLarge)
        {
            large = wantLarge;
            startF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "start", large));
            loopF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "loop", large));
            endF = VfxFrames.Get(VfxKeys.MechanicLaser("center", "end", large));
            originF = VfxFrames.Get(VfxKeys.MechanicLaser("origin", "loop", large));
            impactF = VfxFrames.Get(VfxKeys.MechanicLaser("impact", "loop", false)); // 끝 그림은 늘 small
            hasArt = loopF.Length > 0;
            mat.mainTexture = null;
            line.startColor = line.endColor = Color.white;
        }

        void Update()
        {
            t += Time.deltaTime; // 일시정지·히트스톱 땐 같이 멈춘다
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
                origin.enabled = impact.enabled = false;
                return;
            }
            Sprite body;
            float alpha = 1f;
            if (ending)
            {
                int i = Mathf.FloorToInt(t * EdgeFps);
                if (i >= endF.Length) { gameObject.SetActive(false); ending = false; return; }
                body = endF[i];
                alpha = 1f - (float)i / endF.Length;
            }
            else
            {
                int i = Mathf.FloorToInt(t * EdgeFps);
                body = i < startF.Length ? startF[i] : loopF[Mathf.FloorToInt((t - startF.Length / EdgeFps) * LoopFps) % loopF.Length];
            }
            // 몸통 — 텍스처 한 장(16px)이 0.25유닛이 되게 길이 방향으로 반복, 굵기는 텍스처 높이 그대로(도트 배율)
            mat.mainTexture = body.texture;
            line.widthMultiplier = DotScale.Units(body.rect.height);
            line.textureScale = new Vector2(1f / DotScale.Units(body.rect.width), 1f);
            line.startColor = line.endColor = new Color(1f, 1f, 1f, alpha);

            int loopI = Mathf.FloorToInt(t * LoopFps);
            PlaceCap(origin, originF, loopI, pts[0], dirStart, +1f, !ending, alpha);
            PlaceCap(impact, impactF, loopI, pts[pts.Length - 1], dirEnd, -1f, impactOn && !ending, alpha);
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

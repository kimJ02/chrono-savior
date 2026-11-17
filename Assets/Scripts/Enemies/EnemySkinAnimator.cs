using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 몹 지역 그림 재생(2026-11-17, 사용자 "2스테이지 몬스터 적용"). 몹 프리팹(`Enemy_Base` — 종류별 프리팹은 이걸 물려받는다)에 **배치**돼 있고,
    /// 스폰 때 `Systems/EnemySetup`이 그 지역 그림(<see cref="RegionSkin"/>)을 넘겨야(<see cref="Play"/>) 켜진다 — 안 넘기면(1지역 등) 아무것도 안 한다.
    ///
    /// - 움직임: 되풀이 그림을 게임 시간으로 넘긴다(히트스톱·일시정지 땐 같이 멈춘다).
    /// - 피격: 직접 타격마다(`CombatEvents.EnemyDamaged` — 지속 피해는 안 올라온다) <see cref="HitDuration"/>초 피격 그림.
    ///   흰 깜빡임(`EnemyHealth`)은 그대로 위에 겹친다.
    /// - 방향: 그림이 왼쪽을 보면(<see cref="RegionSkin.facesLeft"/>) 오른쪽으로 갈 때 뒤집는다(`EnemyMove.Direction`).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemySkinAnimator : MonoBehaviour
    {
        /// <summary>피격 그림이 보이는 시간(초) — 보스 피격 그림(`BlobLordBoss.HitFlashDuration`)과 같게.</summary>
        public const float HitDuration = 0.15f;

        SpriteRenderer sr;
        EnemyMove mover;
        RegionSkin skin;
        float t, hitTimer;

        /// <summary>지금 재생 중인 지역 그림(없으면 null — 테스트용).</summary>
        public RegionSkin Skin => skin;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            mover = GetComponent<EnemyMove>();
        }

        void OnEnable() => CombatEvents.EnemyDamaged += HandleDamaged;
        void OnDisable() => CombatEvents.EnemyDamaged -= HandleDamaged;

        /// <summary>이 그림으로 재생을 시작한다(스폰 때 한 번).</summary>
        public void Play(RegionSkin regionSkin)
        {
            skin = regionSkin;
            t = Random.value * (skin != null && skin.move != null ? skin.move.Duration : 0f); // 한 웨이브가 똑같이 날갯짓하지 않게 시작점만 흩는다
            hitTimer = 0f;
            Refresh(0f);
        }

        void HandleDamaged(GameObject go)
        {
            if (go == gameObject && skin != null && skin.hit != null) hitTimer = HitDuration;
        }

        void LateUpdate() => Refresh(Time.deltaTime);

        /// <summary>그림을 고른다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            if (skin == null || sr == null || skin.move == null) return;
            t += dt;
            if (hitTimer > 0f) hitTimer = Mathf.Max(0f, hitTimer - dt);
            var frame = hitTimer > 0f ? skin.hit : skin.move.FrameAt(t);
            if (frame != null) sr.sprite = frame;
            int dir = mover != null ? mover.Direction : 0;
            if (dir != 0) sr.flipX = skin.facesLeft ? dir > 0 : dir < 0;
        }

        /// <summary>피격 그림이 지금 보이는가(테스트용).</summary>
        public bool ShowingHit => hitTimer > 0f;
    }
}

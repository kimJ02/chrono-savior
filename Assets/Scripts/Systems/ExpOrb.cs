using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 경험치 구슬 — 원본 `pickups`의 `kind:'exporb'`(project_test.html:1557 정의,
    /// 드랍 `:1860`, 갱신/획득 `updatePickups` `:4452`) 그대로.
    ///
    /// **50마리째 처치마다 하나 떨어지고, 가까이 가면 자석처럼 끌려온다.** 획득하면
    /// **현재 레벨의 필요 경험치 25%**를 준다 — 고정값이 아니라 비율이라, 레벨이 높을수록
    /// 한 개의 가치가 같이 커진다(원본이 후반에도 구슬을 의미 있게 유지하는 방식이다).
    ///
    /// 사냥 중에만 존재하므로 <see cref="RunTransient"/>를 달고 나온다 — 로비로 나가면 사라진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class ExpOrb : MonoBehaviour
    {
        // 원본 CONFIG.orb(project_test.html:697) — 거리는 100px = 1유닛.
        /// <summary>이 마리 수마다 하나 떨어진다. 원본 `every: 50`.</summary>
        public const int DropEveryKills = 50;
        /// <summary>이 거리 안에 들어오면 끌려오기 시작한다. 원본 `magnetR: 150`.</summary>
        public const float MagnetRange = 1.5f;
        /// <summary>이 거리 안이면 획득. 원본 `collectR: 44`.</summary>
        public const float CollectRange = 0.44f;
        /// <summary>획득 시 주는 경험치 = 현재 레벨 필요량 × 이 비율. 원본 `expPct: 0.25`.</summary>
        public const float ExpPercent = 0.25f;
        /// <summary>경험치 하한. 원본 `Math.max(10, ...)`(`:4466`).</summary>
        public const int MinExp = 10;

        /// <summary>끌려오는 속도. 원본 `const v = 640 * dt`(`:4458`) ÷100.</summary>
        public const float MagnetSpeed = 6.4f;
        /// <summary>플레이어의 "몸통 중심" 보정. 원본은 `player.y - 40`을 기준으로 거리를 잰다.</summary>
        public const float PlayerCenterOffset = 0.4f;
        /// <summary>드랍 위치를 시체보다 이만큼 위로. 원본 `y: min(e.y, groundY) - 40`(`:1861`).</summary>
        public const float DropHeight = 0.4f;

        /// <summary>보이는 지름. 원본 `r: 14`(`:697`) ÷100 × 2.</summary>
        const float Diameter = 0.28f;

        const float BobSpeed = 3f;      // 원본 `Math.sin(o.t * 3)`
        const float BobAmplitude = 0.08f; // 원본 `* 8`px

        float age;
        float baseY;
        Transform player;

        // 구성은 프리팹(Assets/Prefabs/Combat/ExpOrb)에 있다(2026-11-06 생성 책임 2-3) — 그림 자식(구운 보라 구슬 그림, 색 흰색 — 그림에 이미 칠해져 있다)·RunTransient.
        [SerializeField] SpriteRenderer visualRenderer;

        /// <summary>
        /// 구슬 하나를 떨군다. 원본은 죽은 자리에서 살짝 위에 놓고, **지면 아래로는 안 내려간다**
        /// (`Math.min(e.y, groundY)` — 원본은 Y+가 아래라 min이지만 우리 좌표계에선 max다).
        /// </summary>
        /// <param name="player">끌려갈 플레이어 — 만드는 쪽(스포너)이 넣는다(2026-11-05 2b — 예전엔 Player 태그로 찾았다). 없으면 제자리에 떠 있기만 한다.</param>
        /// <remarks>떨구는 쪽(스포너)이 들고 있는 프리팹을 세운다(2026-11-06 생성 책임 2-3). 프리팹이 없으면 오류.</remarks>
        public static ExpOrb Spawn(ExpOrb prefab, Vector3 deathPosition, Transform player)
        {
            if (prefab == null) { Debug.LogError("[ExpOrb] 구슬 프리팹이 없다 — EnemySpawner의 orbPrefab을 확인"); return null; }
            float y = Mathf.Max(deathPosition.y, FieldBounds.GroundY) + DropHeight;
            var orb = Instantiate(prefab, new Vector3(deathPosition.x, y, 0f), Quaternion.identity); // RunTransient — 로비로 나가면 사라진다
            orb.name = prefab.name;
            // 그림마다 픽셀 크기가 다르니 **지름을 기준으로** 스케일을 맞춘다 — 프리팹의 그림을 갈아끼워도 구슬 크기(원본 지름)가 그대로다.
            var sprite = orb.visualRenderer.sprite;
            float h = sprite != null ? sprite.bounds.size.y : 1f;
            orb.visualRenderer.transform.localScale = Vector3.one * (h > 0.0001f ? Diameter / h : Diameter);
            orb.baseY = y;
            orb.player = player;
            return orb;
        }

        void Update()
        {
            if (player == null) return;

            age += Time.deltaTime;

            Vector2 target = (Vector2)player.position + Vector2.up * PlayerCenterOffset;
            Vector2 here = transform.position;
            float d = Vector2.Distance(target, here);

            if (d < MagnetRange && d > 0.01f)
            {
                // 원본은 거리에 무관한 **일정 속도**로 끌어당긴다(가속이 아니다).
                Vector2 next = here + (target - here) / d * (MagnetSpeed * Time.deltaTime);
                transform.position = next;
            }
            else
            {
                // 자석 범위 밖에선 제자리에서 위아래로 떠 있는다.
                transform.position = new Vector3(
                    transform.position.x,
                    baseY + Mathf.Sin(age * BobSpeed) * BobAmplitude,
                    0f);
            }

            if (d < CollectRange) Collect();
        }

        /// <summary>원본 `:4465`~`:4478` — 경험치를 주고 사라진다.</summary>
        void Collect()
        {
            var profile = ProfileService.Current;
            // 원본 `max(10, round(curExpNeed() * 0.25))` — **현재 레벨의 필요 경험치 기준**이라
            // 레벨이 오를수록 구슬 하나의 가치도 같이 커진다.
            // 수행의 굴레 — 구슬에도 경험치 배수가 붙는다(원본 `curExpNeed() * expPct * expMultAll()` :4466).
            // 27레벨부터 `int`를 넘으니 `long`(`PlayerProfile.RequiredExp`). `round`는 JS 식(.5는 올림) — C# 반올림은
            // .5를 짝수 쪽으로 보내서 4레벨 구슬(4082 × 0.25 = 1020.5)이 1020이었다(원본 1021).
            double raw = System.Math.Floor(PlayerProfile.RequiredExp(profile.level) * (double)ExpPercent
                                           * CombatModifiers.ExpMultiplier + 0.5);
            long exp = System.Math.Max(MinExp, raw >= long.MaxValue ? long.MaxValue : (long)raw);

            profile.AddExp(exp);
            RunState.RegisterReward(0, exp); // 결과 화면 집계
            Destroy(gameObject);
        }
    }
}

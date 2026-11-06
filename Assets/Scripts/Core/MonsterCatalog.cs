using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 몬스터 목록 — 잡몹 종류별 데이터(<see cref="EnemyData"/>)와 보스·성소 그림. 스폰하는 쪽(`Systems/EnemySpawner`, 전투 씬)과
    /// 보여 주는 쪽(`UI/Screens/StageSelectScreen`, 상주 씬)이 **같은 에셋**을 참조한다(2026-11-05 씬 분할 2차 2a).
    /// 예전엔 이 값들이 스포너 컴포넌트(씬)에 있어서 스테이지 선택 화면이 씬을 뒤져 스포너를 찾았다 — 로비가 전투 씬과 떨어지면 못 찾는다.
    ///
    /// 보스·성소는 프리팹·그림만 여기 둔다(수치는 코드 — `Enemies/Boss`·`Systems/EnemySpawner`·`Enemies/Shrine`). 그래서 따로 정의 SO를 두지 않는다.
    /// 잡몹 프리팹은 종류별 데이터(<see cref="EnemyData.prefab"/>)가 가리킨다(2026-11-05 생성 책임 2-2 — 예전엔 기본 프리팹 하나에 스포너가 행동 부품을 붙였다).
    /// 에셋: `Assets/Data/Enemies/MonsterCatalog.asset`.
    /// </summary>
    [CreateAssetMenu(menuName = "ChronoSavior/Monster Catalog", fileName = "MonsterCatalog")]
    public class MonsterCatalog : ScriptableObject
    {
        [Tooltip("스폰할 잡몹 종류들(Assets/Data/Enemies/, BuildEnemyData가 생성). 지역별로 무엇이 나올지는 Systems/EnemySpawnTable이 정한다.")]
        public EnemyData[] types;

        [Tooltip("보스 프리팹(Enemy_Boss — Boss 부품·보스 표시).")]
        public GameObject bossPrefab;

        [Tooltip("성소 프리팹(Enemy_Shrine — Shrine 부품·움직이지 않는 구조물).")]
        public GameObject shrinePrefab;

        [Tooltip("보스 그림(전 지역 같음). 비어 있으면 프리팹의 기본 스프라이트를 색만 칠해서 쓴다.")]
        public Sprite boss;

        [Tooltip("성소 그림. 비어 있으면 프리팹의 기본 스프라이트를 색만 칠해서 쓴다.")]
        public Sprite shrine;

        [Tooltip("보스 그림의 화면 크기(가로·세로, 월드 유닛) — 판정(Core.EntitySizeConfig.BossWidth/Height)과 따로(2026-11-06 3.5-d). 그림을 바꾸면 이것만. 지금은 원본 크기 1.30×1.50.")]
        public Vector2 bossSize = new Vector2(1.30f, 1.50f);

        [Tooltip("성소 그림의 화면 크기(가로·세로, 월드 유닛) — 판정과 따로. 지금은 원본 크기 0.64×0.96.")]
        public Vector2 shrineSize = new Vector2(0.64f, 0.96f);

        /// <summary>그 종류의 데이터 — 없으면 null.</summary>
        public EnemyData Find(EnemyType type)
        {
            if (types == null) return null;
            foreach (var d in types)
                if (d != null && d.type == type) return d;
            return null;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// 이 발판 묶음이 **어느 무대의 것인지** 표시한다. 원본이 런을 시작할 때
    /// `platforms = mode === 'normal' ? NORMAL_PLATFORMS : BOSS_PLATFORMS`(project_test.html:4316)로
    /// 통째로 갈아끼우는 것에 대응한다.
    ///
    /// 씬에는 두 묶음이 **다 만들어져 있고** 한쪽만 켜진다 — 런타임에 발판을 생성/파괴하면
    /// 물리 콜라이더가 프레임 중간에 사라져 서 있던 대상이 튀거나 빠진다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlatformSet : MonoBehaviour
    {
        static readonly List<PlatformSet> All = new List<PlatformSet>();

        [Tooltip("체크하면 보스전 무대. 해제하면 일반 사냥 무대.")]
        public bool bossArena;

        void Awake() => All.Add(this);
        void OnDestroy() => All.Remove(this);

        /// <summary>
        /// 무대를 바꾼다. <see cref="FieldLayout.SetBossArena"/>도 같이 불러서 **좌표 데이터와
        /// 실제 콜라이더가 어긋나지 않게** 한다 — 따로 두면 스포너는 좁은 맵을 보는데 발판은
        /// 넓은 배치로 남는 식의 조용한 불일치가 생긴다.
        /// </summary>
        /// <summary>발판을 전부 숨긴다 — 걸어 다니는 로비(2026-10-24)는 땅만 쓴다. 사냥을 시작하면 `Activate`가 다시 켠다.</summary>
        public static void HideAll()
        {
            FieldLayout.SetBossArena(false);
            foreach (var set in All)
                if (set != null) set.gameObject.SetActive(false);
        }

        public static void Activate(bool boss)
        {
            FieldLayout.SetBossArena(boss);
            foreach (var set in All)
                if (set != null) set.gameObject.SetActive(set.bossArena == boss);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// **런 중에만 존재하는 오브젝트** 표식 — 적·투사체·장판처럼 사냥이 시작되거나 끝날 때
    /// 전부 사라져야 하는 것들. 원본 `startRun()`의
    /// `enemies = []; projectiles = []; zones = []; pickups = []; particles = []`
    /// (project_test.html:4318)에 대응한다.
    ///
    /// 원본은 전역 배열을 비우면 끝이지만 우리는 그런 중앙 목록이 없다. 그래서 `CombatDirector`가
    /// **타입 목록을 들고 다니는 대신**(새 투사체가 생길 때마다 거기를 고쳐야 하고, 빠뜨려도
    /// 아무 에러가 안 난다) 이 표식 하나만 보게 했다.
    ///
    /// ⚠️ **런 중에 `Instantiate`/`new GameObject`로 뭔가를 만든다면 여기에 이 컴포넌트를 붙일 것.**
    /// 안 붙이면 로비로 나갔다 다시 들어와도 그 오브젝트가 필드에 남아 있는다 —
    /// 섬영·드루이드 투사체를 만들 때도 마찬가지다(`docs/worksplit.md` 공유 계약).
    ///
    /// 등록은 정적 목록으로 한다 — `FindObjectsByType`은 런 시작마다 씬 전체를 훑어야 해서
    /// 필드에 몹이 20마리 넘게 깔린 상태에선 눈에 띄는 비용이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunTransient : MonoBehaviour
    {
        static readonly List<RunTransient> Active = new List<RunTransient>();

        /// <summary>지금 필드에 남아 있는 런 오브젝트 수(테스트·디버그용).</summary>
        public static int ActiveCount => Active.Count;

        void Awake() => Active.Add(this);
        void OnDestroy() => Active.Remove(this);

        /// <summary>
        /// 붙어 있는 오브젝트를 붙여 준 대상에 표식만 다는 헬퍼. 이미 붙어 있으면 그대로 둔다
        /// (`DisallowMultipleComponent`라 두 번 붙이면 경고가 난다).
        /// </summary>
        public static void Mark(GameObject go)
        {
            if (go != null && go.GetComponent<RunTransient>() == null) go.AddComponent<RunTransient>();
        }

        /// <summary>
        /// 전부 제거. 원본 `startRun()`의 배열 비우기와 같다.
        /// 뒤에서부터 지우는 이유는 `OnDestroy`가 <see cref="Active"/>에서 자기를 빼기 때문 —
        /// 앞에서부터 돌면 인덱스가 밀린다.
        /// </summary>
        public static void DestroyAll()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                var t = Active[i];
                if (t != null) Object.Destroy(t.gameObject);
                else Active.RemoveAt(i);
            }
            Active.Clear(); // Destroy는 프레임 끝에 반영돼서 OnDestroy가 아직 안 불린다
        }

        /// <summary>테스트 격리용(정적 목록이라 테스트끼리 샌다).</summary>
        public static void Reset() => Active.Clear();
    }
}

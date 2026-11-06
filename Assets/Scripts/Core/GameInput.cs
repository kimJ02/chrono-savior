using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 모든 키 입력은 여기를 통해서만 읽는다. 원본 `KEYMAP`(project_test.html:1007)의 액션 이름을
    /// 그대로 프로퍼티로 옮겼다 — CLAUDE.md "입력 처리" 규칙("스크립트에서 Input.GetKey(KeyCode.X)를
    /// 직접 호출하지 않는다")이 실제로는 안 지켜지고 있던 걸 발견해서(`CharacterMover2D`/`MageAttack`/
    /// `PlayerAttack` 세 곳이 직접 호출 중이었음) 여기로 전부 모았다.
    ///
    /// 원본 KEYMAP은 점프에 KeyC와 Space 둘 다 매핑하지만, 이 프로젝트는 **의도적으로 C만 쓴다**
    /// (`docs/sprints/01-combat-core.md`·`CLAUDE.md`에 명시된 의도적 편차 — 되돌리지 말 것).
    ///
    /// Unity의 `Input.GetKey`/`GetKeyDown` 자체가 이미 "누르고 있음/방금 눌림"을 프레임 단위로
    /// 구분해주므로, 원본처럼 별도 Set 기반 상태 관리를 재구현할 필요는 없다 — 여기서는 키 이름만
    /// 한 곳에 모으는 게 목적이다.
    /// </summary>
    public static class GameInput
    {
        // ---- 이동 (원본 left/right/up/down) ----
        public static bool Left => Input.GetKey(KeyCode.LeftArrow);
        public static bool Right => Input.GetKey(KeyCode.RightArrow);
        public static bool Up => Input.GetKey(KeyCode.UpArrow);
        public static bool Down => Input.GetKey(KeyCode.DownArrow);

        // ---- 전투 키(점프 C · 공격 Z · 스킬 X)는 사냥 중에만 읽힌다 ----
        // 2026-10-25 사용자 "로비 들어갔을 때 스킬이 한 번 써진다": 메뉴의 "확인"도 Z라(<see cref="MenuConfirmDown"/>) 결과 창에서
        // Z로 로비에 들어가면 같은 키가 공격으로도 읽혀, 시간이 멈춘 로비에 탄·스킬이 나가 있었다. 캐릭터 스크립트(팀원 것 포함)는
        // 화면 상태를 안 보고 키만 읽어서(원래 계약은 "사냥 중에만 돈다" — `GameState` 주석), 키를 읽는 이 한 곳에서 막는다.
        //   - 사냥 중이 아니면(로비·결과·타이틀·일시정지) 안 눌린 것으로 본다.
        //   - 로비·결과·타이틀에서 사냥으로 들어오는 순간 누르고 있던 키는 **한 번 뗐다 다시 눌러야** 읽힌다 — 스테이지 선택에서
        //     Z로 "원정 떠나기"를 누르자마자 공격이 나가지 않게. 일시정지에서 돌아올 땐 안 잠근다(누르던 차지를 그대로 이어 간다).
        static readonly KeyCode[] CombatKeys = { KeyCode.C, KeyCode.Z, KeyCode.X };
        static readonly bool[] combatLatched = new bool[3];
        static int latchedVersion = -1;

        static bool CombatKey(int index, bool down)
        {
            if (!GameState.IsRunning) return false;
            // 히트스톱(사냥 중 timeScale 0) 동안 **누른 순간**은 버린다 — 원본은 멈춘 동안 갱신을 건너뛰고 프레임 끝에
            // 눌림(`Input.pressed`)을 비운다(:7168·:1019). 누르고 있는 것(held)은 그대로 — 막으면 차지 중인 키가 뗀 것처럼 보인다(2026-11-07).
            if (down && Time.timeScale <= 0f) return false;
            if (latchedVersion != GameState.Version)
            {
                latchedVersion = GameState.Version;
                bool fromMenu = GameState.Previous != GameScene.Pause;
                for (int i = 0; i < CombatKeys.Length; i++) combatLatched[i] = fromMenu && Input.GetKey(CombatKeys[i]);
            }
            var key = CombatKeys[index];
            if (combatLatched[index])
            {
                if (Input.GetKey(key)) return false;
                combatLatched[index] = false;
            }
            return down ? Input.GetKeyDown(key) : Input.GetKey(key);
        }

        // ---- 점프 (원본 jump — 이 프로젝트는 C 전용, 의도적 편차) ----
        public static bool JumpHeld => CombatKey(0, false);
        public static bool JumpDown => CombatKey(0, true);

        // ---- 공격 (원본 atkBow, 마법사 Z) ----
        public static bool AttackHeld => CombatKey(1, false);
        public static bool AttackDown => CombatKey(1, true);

        // ---- 스킬 (원본 ult, X) ----
        public static bool UltDown => CombatKey(2, true);

        // ---- 일시정지 (원본 :1026 — ESC로 run ↔ pause 토글) ----
        /// <summary>
        /// ESC. 원본은 사냥 중 ESC로 일시정지하고, 그 화면에서 "게임으로 돌아가기 / 로비로 돌아가기"를
        /// 고른다(`:1027`~`:1028`). **ESC 자체가 로비로 나가는 키가 아니다** — 한 단계 거친다.
        /// </summary>
        public static bool PauseDown => Input.GetKeyDown(KeyCode.Escape);

        /// <summary>
        /// 상호작용(보스 포탈 입장). **원본에 없는 키다** — 원본은 보스전을 로비에서 고르므로
        /// 필드에 상호작용할 대상이 없다. 사용자 지시(2026-09-16)로 포탈 방식이 되면서 필요해졌다.
        ///
        /// ↑키를 쓰지 않은 이유: 마법사 중력 계열이 "↑ 위로 발사"에 이미 쓰고 있어(`:2169` 계열)
        /// 포탈 위에서 위로 쏘려다 입장해 버린다.
        /// </summary>
        public static bool InteractDown => Input.GetKeyDown(KeyCode.F);

        // ---- 메뉴(Figma UI, 2026-10-24) — 원본은 마우스로 누르는 HTML 버튼이라 키 조작이 따로 없다 ----
        public static bool MenuLeftDown => Input.GetKeyDown(KeyCode.LeftArrow);
        public static bool MenuRightDown => Input.GetKeyDown(KeyCode.RightArrow);
        public static bool MenuUpDown => Input.GetKeyDown(KeyCode.UpArrow);
        public static bool MenuDownDown => Input.GetKeyDown(KeyCode.DownArrow);
        /// <summary>고른 버튼 누르기 — Enter·Space·Z.</summary>
        public static bool MenuConfirmDown =>
            Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Z);
    }
}

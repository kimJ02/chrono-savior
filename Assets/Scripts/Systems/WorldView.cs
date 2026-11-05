using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// **화면이 월드를 읽는 유일한 창구**(2026-11-05 씬 분할 2차 2b, `HANDOFF.md` "연결 규칙" 2). 화면(상주 씬 `00_Persistent`)은 월드 오브젝트
    /// (전투 씬의 플레이어·보스·포탈·몹, 로비 건물)를 직접 찾지도, 그 구현 타입을 알지도 않는다 — 월드 쪽 담당이 자기를 여기 꽂고
    /// (<see cref="ICombatView"/> = `CombatDirector`, <see cref="ILobbyView"/> = `UI.LobbyWorld`), 화면은 읽기 계약으로 물어본다.
    ///
    /// 값을 복사해 넣는 갱신 루프가 없다 — 담당이 물을 때 계산하므로 늘 최신이고, 월드가 내려가면(<c>OnDisable</c>) 빼기만 하면 비워진다.
    /// 월드가 없으면(타이틀·전환 중) 해당 칸이 null이고, 화면은 그 표시를 숨긴다(다시 찾는 예비 경로는 없다).
    /// </summary>
    public static class WorldView
    {
        /// <summary>떠 있는 전투 씬의 담당 — 없으면 null.</summary>
        public static ICombatView Combat { get; private set; }

        /// <summary>떠 있는 로비 월드 — 없으면 null.</summary>
        public static ILobbyView Lobby { get; private set; }

        /// <summary>전투 담당을 꽂는다. 이미 다른 담당이 꽂혀 있으면 오류(전투 씬이 두 번 떴다)이고 바꾸지 않는다.</summary>
        public static void Attach(ICombatView view) => Combat = AttachOne(Combat, view, "전투");

        /// <summary>자기일 때만 뺀다 — 다른 담당이 이미 꽂혀 있으면 건드리지 않는다.</summary>
        public static void Detach(ICombatView view)
        {
            if (ReferenceEquals(Combat, view)) Combat = null;
        }

        public static void Attach(ILobbyView view) => Lobby = AttachOne(Lobby, view, "로비");

        public static void Detach(ILobbyView view)
        {
            if (ReferenceEquals(Lobby, view)) Lobby = null;
        }

        static T AttachOne<T>(T current, T view, string what) where T : class
        {
            if (current != null && !ReferenceEquals(current, view) && !(current is Object o && o == null))
            {
                Debug.LogError($"[WorldView] {what} 창구에 이미 다른 담당이 꽂혀 있다 — 월드가 두 번 떴는지 확인");
                return current;
            }
            return view;
        }

        /// <summary>테스트 격리용 — 다른 정적 상태의 `Reset()`과 같다.</summary>
        public static void Reset()
        {
            Combat = null;
            Lobby = null;
        }
    }

    /// <summary>전투 화면(HUD)이 읽는 것 — 전투 씬의 담당(`CombatDirector`)이 구현한다. 전부 읽기 전용이고, 물을 때 계산한다.</summary>
    public interface ICombatView
    {
        float PlayerHp { get; }
        float PlayerMaxHp { get; }
        Vector2 PlayerPosition { get; }

        /// <summary>지금 캐릭터 키트의 Z·X 칸과 버프 줄 — 이미 있는 HUD 계약(`Core/ISkillSlotSource`). 없으면 null.</summary>
        ISkillSlotSource PlayerKit { get; }

        /// <summary>보스 포탈 — 일반 필드에서만 <see cref="PortalView.Visible"/>.</summary>
        PortalView Portal { get; }

        /// <summary>보스 체력 비율 0~1 — 보스가 살아 있을 때만 값이 있다.</summary>
        float? BossHpRatio { get; }

        /// <summary>살아 있는 적 전부(잡몹·소환·성소·보스)의 위치를 <paramref name="into"/>에 담는다 — 먼저 비운다. 미니맵이 일정 간격으로 부른다.</summary>
        void CopyEnemyPositions(List<Vector2> into);
    }

    /// <summary>보스 포탈의 지금 상태(값).</summary>
    public readonly struct PortalView
    {
        public readonly bool Visible, InRange, Unlocked;
        public readonly Vector2 Position;

        public PortalView(bool visible, bool inRange, bool unlocked, Vector2 position)
        {
            Visible = visible; InRange = inRange; Unlocked = unlocked; Position = position;
        }
    }

    /// <summary>로비 화면(HUD)이 읽는 것 — 로비 월드(`UI.LobbyWorld`)가 구현한다.</summary>
    public interface ILobbyView
    {
        /// <summary>캐릭터가 앞에 서 있는 건물 번호(`LobbyWorld.Buildings`) — 없으면 -1.</summary>
        int NearBuilding { get; }

        /// <summary>로비에서 걷는 캐릭터의 위치 — 없으면 null.</summary>
        Vector2? AvatarPosition { get; }
    }
}

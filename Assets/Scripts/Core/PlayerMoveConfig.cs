namespace ChronoSavior.Core
{
    /// <summary>
    /// 플레이어 기본 이동 속도 — 전투의 플레이어(`Characters/CharacterMover2D`)와 로비 아바타(`UI/Lobby/LobbyAvatar`)가 같이 쓴다(2026-11-05 씬 분할 2차 3-1).
    /// 예전엔 전투 씬 Player의 `CharacterMover2D.moveSpeed`(씬에 저장된 값)였고 로비가 그 컴포넌트를 찾아 읽었다 — 로비가 다른 씬이 되면 못 읽는다.
    /// </summary>
    public static class PlayerMoveConfig
    {
        /// <summary>원본 `CONFIG.player.moveSpeed` 270px/s ÷100. 골드 강화(ms) 배수는 사냥 중 이동에만 곱한다(로비 걸음엔 안 곱한다 — 예전과 같다).</summary>
        public const float BaseSpeed = 2.7f;
    }
}

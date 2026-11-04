namespace ChronoSavior.Core
{
    /// <summary>
    /// "붙잡기"류 판정(현재는 섬영 칼날폭풍 4층 "강제 연행"뿐)에서 제외돼야 하는 대상임을 표시하는
    /// 마커 인터페이스 — 멤버가 없다. 원본 `bladeStormUpdate`의 붙잡기 후보 필터
    /// `e.boss || e.shrine`(project_test.html:2694)에 대응한다.
    ///
    /// 이 인터페이스가 Core에 있는 이유는 <see cref="IDamageable"/>/<see cref="ISpawnProtectable"/>와
    /// 같다 — `Characters`는 asmdef 계층 규칙상 `Enemies`를 직접 참조할 수 없어서, 구체 타입
    /// (`Boss`/`Shrine`) 없이 "붙잡을 수 없는 대상인가"만 물을 자리가 필요하다. 일반 피해 판정
    /// (`IDamageable`/`ISpawnProtectable`)과는 완전히 별개다 — 보스·성소는 이 마커가 있어도 회전베기·
    /// 궤적·낙하 충격 등 다른 모든 공격에는 그대로 맞고 죽는다. 오직 "붙잡혀서 플레이어를 따라
    /// 끌려다니는" 동작에서만 제외된다.
    ///
    /// **소유권 예외(2026-09-30)**: `Core/`는 팀원(이 파일 작성자) 담당이 아니지만
    /// (`docs/worksplit.md` §6 "읽기만"), 이 마커를 구현해야 하는 `Boss.cs`/`Shrine.cs`
    /// (팀장 담당 `Enemies/`)를 향해 "붙잡을 수 없다"는 신호를 보낼 자리가 Core 말고는 없다 —
    /// 사용자 확인(섬영 4층 강제 연행 작업 중 "Core에 마커 인터페이스 추가(추천)")을 받고 최소
    /// 침습(멤버 없는 빈 인터페이스 하나 + `Boss.cs`/`Shrine.cs`에 한 줄씩)으로 추가했다. 팀장 확인
    /// 필요 — PROGRESS.md "확인 필요" 참고.
    /// </summary>
    public interface IGrabExempt
    {
    }
}

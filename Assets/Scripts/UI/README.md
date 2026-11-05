# UI 레이어 (asmdef 4층)

HUD·로비·결과 화면. CLAUDE.md 계층표의 **최상위 층**이라 0~3층(`Core`/`World`·`Combat`/
`Characters`·`Enemies`/`Systems`)을 전부 참조할 수 있고, **아무도 이 층을 참조하지 않는다.**

그래서 게임 로직을 여기에 두면 안 된다 — UI는 아래 층의 상태를 **읽어서 그리고**,
버튼을 누르면 아래 층의 **공개 메서드를 부르기만** 한다.
(예: 사냥 시작은 여기서 계산하지 않고 `Systems/GameFlow.StartRun(region, mode, done)`을 부른다 — 씬 불러오기가 끝나면 `done`으로 성공·실패를 받는다.)

월드(전투·로비 씬)는 **`Systems/WorldView` 창구로만 읽는다**(`WorldView.Combat`·`WorldView.Lobby`) — 화면은 상주 씬(`00_Persistent`)에 있고 월드 씬은 따로 뜨고 내려가므로,
씬을 뒤져 찾지 않는다(`Tests/PlayMode/WorldLookupTests`가 막는다). 화면 상태·시간(`GameState.Set`·`Time.timeScale`)도 바꾸지 않는다 — `GameFlow`에 요청한다(`StateOwnershipTests`).

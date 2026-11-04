> ⚠️ **대체됨 — 현재 스펙 아님** (2026-11-04, 팀장 세션). 이 제안을 바탕으로 Claude·Codex가 코드와 네 차례 대조해 고친 최종안이
> 루트 `HANDOFF.md` '1회차 합의'다. **뒤집힌 결정**(이 문서를 보고 되살리지 말 것):
>
> | 이 제안 | 최종 | 이유 |
> |---|---|---|
> | 씬 4개(UI·Title·Lobby·Combat) | **3개**(`00_Persistent`·`10_Lobby`·`20_Combat`) | 타이틀은 UI가 화면 전체를 덮어(`TitleMenu.cs` 배경 640×360) 월드 씬이 비게 된다 — 타이틀은 상주 씬의 UI |
> | 월드 씬마다 카메라 | **카메라 하나를 상주 씬에** | 겹쳐 로드하는 동안 카메라·AudioListener가 둘이 되거나 하나도 없어 화면이 깜빡인다 |
> | UI 씬 부팅은 `PixelCanvas.Create()` 직접 호출 | **UI 판을 씬에 배치**(Create는 테스트·캡처 전용) | 씬 파일이 유일한 원본 |
> | 정적 리스트는 `sceneUnloaded`에서 `ClearAllForTests()`로 정리 | **불필요** | 정적 목록은 전부 `OnDestroy`에서 스스로 빠진다(확인함) — 대신 정적 이벤트 구독 대칭을 규칙으로 |
> | 별도 Boot 씬 없이 Title이 첫 씬 | **`00_Persistent`가 첫 씬, `GameBoot`가 세이브 로드 → 월드 로드** | 세이브 로드가 생성 순서에 기대던 문제(`AutoSave.Awake`)를 없앤다 |

# [제안] 씬 4분할 (UI / Title / Lobby / Combat)

> **2026-11-04 — 나(팀원)와 사용자가 대화로 정리한 제안.** 아직 시작 안 했고, 팀장 리뷰 전이다.
> 시작하기 전에 `docs/worksplit.md` 기준으로 담당을 정하고 들어갈 것 — 이 작업은 `Core/GameState`,
> `UI/Pixel/PixelCanvas`, 전투 쪽 정적 상태(`GravityWellZone.Active` 등) 전부를 건드려서 팀원·팀장
> 파일 양쪽에 걸친다.

## 배경 — 지금 구조

`Assets/Scenes/`엔 사실상 `CombatCore.unity` 하나뿐이다(`SampleScene.unity`는 기본 빈 씬, 안 씀).
타이틀·로비·전투·일시정지·결과 다섯 화면 전부 이 씬 하나 안에서 `Core/GameState`의
`GameScene { Lobby, Run, Pause, Result, Title }`(`GameState.cs:8`) 값만 바꿔가며 구현돼 있다.
**`SceneManager.LoadScene`은 지금까지 한 번도 안 쓰였다** — 찾아본 범위에선 관련 호출이 전혀 없다.

UI도 씬에 배치된 게 아니다. `UI/Pixel/PixelCanvas.Install()`이
`[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`로 Play 시작할 때 `PixelUI`라는 오브젝트를 직접
만들고(`PixelCanvas.cs:37-54`), 그 안에 `TitleMenu`·`LobbyScreen`·`CombatHud`·`PauseMenu`·
`RunResultScreen` 같은 `PxScreen` 화면들이 전부 붙는다. 각 화면은 `WantsVisible` 하나로
`GameState.Current`를 보고 스스로 켜지고 꺼진다(예: `TitleMenu.cs:17`) — **이 로직은 씬을 나눠도
안 바꿔도 된다.**

## 제안하는 씬 구성

1. **UI** — `PixelCanvas` + 모든 `PxScreen` 화면. 게임 시작할 때 한 번 additive로 로드해서 **끝까지
   안 내린다.**
2. **Title** — 월드(배경·카메라)만. 지금 `TitleMenu`가 하던 일은 그대로 UI 씬 쪽 담당.
3. **Lobby** — 월드만.
4. **Combat** — 월드만. **Pause·Result는 따로 씬으로 안 만든다** — UI 씬 안의 오버레이 화면으로
   그대로 둔다(뒤에 멈춘 전투 월드가 보여야 하는 화면이라 분리하면 오히려 손해).

## 로딩 흐름

`LoadSceneMode.Single`을 쓰면 UI 씬까지 같이 내려가므로 전부 **Additive**로만 전환한다. 씬마다
전환 코드를 따로 짜지 말고 공용 헬퍼(가칭 `SceneFlow.GoTo(target)`) 하나로 통일:

1. UI 씬이 안 떠 있으면 먼저 additive 로드 (최초 부팅 때 1회 — 별도 Boot 씬 없이 Title이 첫 씬이면
   거기서 자동으로 처리됨)
2. 목표 월드 씬 additive 로드
3. 로드 끝나면 이전 월드 씬 언로드 + `SceneManager.SetActiveScene(새 씬)`
4. `GameState.Set(...)` 호출(순서·호출 주체는 `RunController`/화면 전환 코드와 맞춰서 정할 것)

## 확인된 위험 두 가지

### 1. 정적 상태 정리

`GravityWellZone.Active` 같은 `static List<...>`가 여러 군데 있다(전투 쪽에 더 있을 수 있음 —
전수조사 필요). Combat 씬을 진짜로 언로드하면 그 안 오브젝트는 Unity가 지워주지만, 저런 정적
리스트의 참조는 안 지워진다 → 죽은 오브젝트 참조로 인한 null 에러 위험.

**대응**: `SceneManager.sceneUnloaded` 이벤트에 Combat 씬 언로드를 걸어서, 테스트 격리용으로
이미 만들어둔 `ClearAllForTests()`류 함수들을 실전에서도 그대로 호출한다. 테스트 때 쓰던 정리
로직을 재활용하는 것이라 새로 짤 양이 많지 않다.

### 2. `PixelCanvas.Install()`의 Player 태그 조건

`PixelCanvas.cs:41` — `GameObject.FindGameObjectWithTag("Player") == null`이면 캔버스를 아예 안
만든다. **이건 PlayMode 테스트(플레이어 없는 빈 씬)에서 UI가 안 생기게 막는 의도된 안전장치라
건드리면 안 된다.** 문제는 Title 씬처럼 Player 오브젝트가 없는 월드 씬에서 이 자동생성 경로에
의존하면 UI가 영영 안 뜬다는 것.

**대응**: 실제 게임의 UI 씬 부팅 스크립트는 이 자동생성(`Install()`)에 기대지 말고
`PixelCanvas.Create()`를 직접 호출해서 만든다. 테스트는 여전히 `Install()`의 Player 태그 체크를
그대로 타니까 영향 없음.

## 그 외 확인된 것 (추가 검증 불필요)

- `Canvas.renderMode`는 기본 `RenderMode.ScreenSpaceOverlay`(`PixelCanvas.cs:64`) — 카메라 참조가
  없어서 월드 씬 카메라가 바뀌어도 UI 쪽을 다시 연결해줄 필요 없음. (`UseCamera()`로 Camera 모드로
  바꾸는 경로는 `Editor/CaptureStagePreview.cs` 등 에디터 캡처 도구 전용, 실제 플레이와 무관.)
- `ProfileService`/`RunState`/`GameState`는 순수 정적 C# 클래스라 씬을 내려도 자동으로 살아남는다
  — `DontDestroyOnLoad` 안 걸어줘도 됨.
- 기존 코드에 `SceneManager.LoadScene` 호출이 없어서(위 참고) 씬 전환 때문에 고쳐야 할 기존
  코드가 적다 — 마이그레이션 범위가 좁은 편.

## 권장 진행 순서 (단계적으로)

한 번에 다 가지 말 것:

1. **1단계**: UI만 먼저 씬으로 분리. 타이틀·로비·전투는 지금처럼 한 씬(World)에 그대로 둔 채
   검증 — Canvas가 여전히 뜨는지, 화면 전환이 그대로 되는지만 확인.
2. **2단계**: 1단계가 안정되면 World를 Title/Lobby/Combat 셋으로 쪼개고, 이번엔 "정적 상태 정리"
   (위험 1번)에 집중해서 검증 — 전투 들어갔다 나오는 걸 여러 번 반복하면서 에러 안 나는지 확인.

이렇게 나누면 "UI를 씬으로 분리"라는 새 패턴과 "정적 상태 정리"라는 위험을 한 번에 같이 안
터뜨리고 따로 검증할 수 있다.

## 완료 기준 (DoD) — 초안

- [ ] UI 씬이 게임 시작 시 한 번만 로드되고 끝까지 유지된다
- [ ] 타이틀 → 로비 → 전투 → 로비 왕복해도 UI(특히 Pause·Result)가 정상 동작한다
- [ ] 전투를 여러 번 반복 입장/퇴장해도 정적 리스트발 null 에러가 없다
- [ ] PlayMode 테스트 전체 통과(특히 UI 캔버스 자동생성 안전장치가 테스트 씬에서 여전히 안 켜지는지)
- [ ] 빌드 세팅에 새 씬 4개 등록, 기존 `CombatCore.unity`/`SampleScene.unity` 정리 여부 결정

## 범위 밖 (이번엔 안 함)

- Result 화면을 별도 씬으로 분리하는 것 — 지금은 UI 씬 안 오버레이로 유지, 연출이 커지면 그때 재검토
- 로비 내부 하위 화면(각성·유물·업적·스테이지 선택 등)을 추가로 씬 분할하는 것 — 전부 UI 패널이라
  씬까지 쪼갤 이유가 아직 없음

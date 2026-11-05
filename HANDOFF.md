# 현재 스프린트 — 구조 정리 (씬·코드) (2026-11-04 ~)

> **목표(2026-11-04 사용자): 성능이 아니라 '처음부터 깔끔한 코드·씬 구조'.** 교수님 리뷰 기준으로 파일·코드·씬 구조가 깔끔해야 한다.
> 게임 동작·수치는 원본(`reference/project_test.html`) 기준 그대로다 — 구조 정리로 동작이 바뀌면 안 된다(바뀌면 짚는다).
> **협업**: Claude = 조사·구현·PlayMode 테스트·커밋 / Codex = 설계 비판·리뷰(`AGENTS.md`, 읽기 전용). 합의는 Codex가 '합의'라고 답할 때까지 반복했다.
> 직전 스프린트(직업별 스킬)는 `docs/sprints/08-class-skills.md`(완료 기록). 팀원 제안 `docs/sprints/06-scene-split.md`는 이 문서로 **대체됨**.
>
> 기준: 깔끔한 코드 = 패턴이 많은 코드가 아니라 **읽는 사람이 바로 따라갈 수 있는 코드.** 작은 프로젝트에 과한 계층·추상화 금지.

## 정리 순서와 상태

| 순위 | 작업 | 상태 |
|---|---|---|
| 0 | 규칙 정하기(아래) | ✅ 합의 |
| 1 | 옛 IMGUI 화면 제거(`TitleScreen`·`LobbyScreen`·`GameHud`·`ResultScreen`·`RegressionWindow`·`UiTheme`·`UiTextures`·`Sprites/UI/`·`BuildUiTextures`) | ✅ 씬 분할 1차에 포함 |
| 2 | 오브젝트 생성 책임 정리(씬 배치 / 기능별 생성 코드) — 일괄 프리팹화는 하지 않음 | 씬 원본과 어긋나던 2곳 ✅(2차 전 보완) / 목록화 할 일 |
| 3 | 씬 분할 — **1차**(상주 씬 분리) ✅ / **2차**(로비 분리·CombatDirector) 단계 0·1·2a·2b ✅, 3·4 할 일 | 진행 중 |
| 4 | 전역 상태 정리(저장·런·설정 경계, `ProfileService.Current` 117곳, UI의 월드 탐색) | 할 일 |
| 5 | 큰 클래스 분리(구조 안정 후). 팀원 파일은 팀원 확인 필요 | 할 일 |

회차 계획(Codex와 영역별 검토): **1 씬·부팅 ✅** / 2 전역 상태·결합 / 3 UI / 4 Systems·Enemies / 5 Characters·Combat·World

## 0단계 규칙(결정 사항)

1. **씬 파일이 유일한 원본.** 씬을 코드로 통째로 만들던 `BuildPartAScene`은 지웠고, `Editor/SceneSetupValidator`(메뉴 `ChronoSavior/씬 구성 검증`)가 검사만 한다.
2. 씬 이름·폴더·화면별 책임, 상주 오브젝트 소유권, 로드·언로드 순서를 정한다(아래 1회차).
3. 재사용 구성이 있는 오브젝트는 프리팹, 조정할 공유 설정은 ScriptableObject를 **우선** — 일회성 효과·정당한 런타임 추가는 예외로 두고 담당 위치·이유를 기록. 세이브는 별도 흐름(`ProfileService` 구조는 4단계에서).
4. 씬 배치와 코드 생성을 나누고, `AddComponent`를 허용하는 위치와 이유를 기록(2단계에서 목록화).
5. 코드·네임스페이스·Assets 폴더 규칙. `Resources`, `Sprites/`↔`Resources/Art` 중복은 참조 확인 후 정리(`SampleScene`은 1차에서 삭제).
6. 기능 제거·씬 분할마다 '그대로 동작해야 할 것'과 확인 방법을 먼저 정한다. asmdef 계층은 순환이 없으니 유지.

## 1회차 합의 — 씬·부팅 구조 (2026-11-04 세 차례 검토, Codex '1회차 최종 합의')

> 처음 합의안을 실제 코드와 세 번 대조해 구멍 23개(Claude 19 + Codex 4)를 찾아 고쳤다. 아래가 최종이다.

| 항목 | 결정 |
|---|---|
| 씬 | **3개**: `00_Persistent`(빌드 첫 씬) / `10_Lobby` / `20_Combat`. 번호는 폴더 정렬용, 진입 순서는 Build Settings에 명시. **Title 씬은 두지 않는다** — 타이틀은 UI가 화면 전체를 덮어(`TitleMenu.cs:38~39`) 월드 씬이 비게 되므로 `00_Persistent`의 전체 화면 UI로 |
| 00_Persistent | GameFlow·UI 캔버스(모든 화면·일시정지·결과 오버레이)·EventSystem·AutoSave·HitSound·**공용 카메라 하나**(PixelPerfectCamera·AudioListener·CameraFollow2D). 월드 씬은 카메라 대신 '추적 대상·경계'만 제공, 타이틀에선 카메라 끔, 언로드 때 대상 참조를 비움. 상주 오브젝트가 두 번 로드되지 않게 부팅 규칙을 둔다 |
| 10_Lobby | 로비 월드(건물·땅) + **로비 아바타**(걷기·그림만, Player 태그 없음). 전투를 끝낸 뒤의 독립 모드(진입은 전부 `EnterLobby()`가 필드를 지움). timeScale 0·unscaled 걷기 유지 |
| 20_Combat | 필드·발판 두 벌·보스 포털·스포너·StageVisuals·플레이어·CombatDirector. 보스 필드 진입은 같은 씬 안에서(발판 전환) |
| RunController 분리 | **GameFlow(상주)**: `Start`·`EnterLobby`·`Pause`·`Resume`·`EndRun`·`StartRun`(날짜 차감·`RunState.Begin`·전투 씬 진입), `defaultRegion`·`LastEndReason`. `GameState.Set`과 **timeScale 대입은 여기서만**. **CombatDirector(전투 씬)**: 필드 초기화·`RunState.Tick`·보스 종료 타이머·`EnterBossField`·`ResetPlayer`·처치·사망·피격 연출·전투 이벤트 구독. 히트스톱은 `RequestHitstop(duration)`으로 요청만, 런 종료는 원인만 전달. 정적 이벤트는 `OnEnable`/`OnDisable` 대칭 구독·해제 |
| timeScale | GameFlow가 unscaled 시간으로 히트스톱 만료 관리. 만료 시 `GameState.Run`일 때만 1로. Pause·Lobby·Title·Result 진입 때 히트스톱 취소 |
| 전환 | GameFlow에 '전환 중' 상태 하나 — 입력·중복 전환 잠금. 순서: 새 씬 로드 → 준비 확인 → 활성 씬 지정 → `GameState.Set` → 이전 씬 언로드. 실패 시 잠금·상태 복구 |
| 재도전 | 결과 화면 '다시 원정'(`RunResultScreen.cs:129`)은 `20_Combat`을 언로드 후 다시 로드(깨끗한 상태) |
| 에디터 Play | 에디터 전용(`#if UNITY_EDITOR`): `playModeStartScene = 00_Persistent`, Play 직전 열려 있던 씬 경로를 `SessionState`에 남기고 GameFlow가 에디터에서만 그 씬으로 바로 진입(값은 쓰고 지움). 빌드 동작은 안 바뀜. 평소처럼 `20_Combat`을 열고 Play해도 동작 |
| 캐릭터 그림 | `PlayerRig.characterSprites`를 ScriptableObject `CharacterArtSet` 하나로 빼고 PlayerRig·로비 아바타·에디터 도구가 같이 참조 |
| 자동 설치 제거 | `PixelCanvas.Install()`·`StageVisuals.Install()` 삭제 → 씬 배치. `PixelCanvas.Init()/Create()`는 '씬 배치용 초기화'와 '테스트용 생성'으로 경계를 나눔. StageVisuals의 런타임 그림 생성은 생성 책임·입력 데이터를 기록 |
| UI 연결 | UI 11곳의 `FindFirstObjectByType<RunController>`는 분할과 **같은 단계에서** GameFlow 참조로(정적 전역 조회로 만들지 않고 연결 경계 명시) |
| AutoSave | 상주 씬. Result 상태 변경(저장)이 전투 씬 언로드보다 먼저 일어나는지 보장 |
| BuildPartAScene | 생성하지 않고 씬 필수 구성 검증만 |
| 로비 캐릭터 선택 | 지금은 로비 창이 전투 `PlayerRig`를 찾아 고른다(`CharacterSelectScreen.cs:118,126,140,159~160`) → 로비 씬엔 없음. **로비는 프로필 값만 바꾸고 저장**, `PlayerRig`는 전투 씬 로드 때 프로필을 읽어 키트를 켠다(이미 `PlayerRig.cs:68,122` 구조). 데이터는 셋으로 나눈다: 그림(`CharacterArtSet`) / 캐릭터 정의·해금 조건(별도 데이터) / 보유 상태(`ProfileService`) |
| 스테이지 선택 그림 | 지금은 스포너에서 몬스터·보스 그림을 가져온다(`StageSelectScreen.cs:175~184`) → 로비엔 없음. 그림을 `EnemyData`(기존 SO 확장)와 보스 정의 데이터로 옮겨 스포너·스테이지 선택이 같이 읽음 |
| 활성 씬·소유 | Unity는 새 오브젝트를 **활성 씬**에 만든다(월드 쪽 런타임 생성 18파일). 월드 씬 로드 직후 그 씬을 활성으로, Persistent 생성물은 Persistent 부모 아래로. 테스트: 전투→로비 후 Persistent에 월드 잔존물 없음 |
| 전환 중 월드 겹침 | 겹쳐 로드하는 동안 이전·새 월드가 동시에 돌면(특히 재도전 전투→전투) 몹·물리가 같은 좌표에서 섞인다 → 전환 시작 때 이전 월드를 멈추고(비활성), 새 월드 준비가 끝난 뒤 켠다. 테스트 포함 |
| UI 월드 탐색 | Persistent UI가 월드를 찾는 곳은 **24곳**(RunController·EnemySpawner 11 + Boss·BossPortal·PlayerRig·EnemyHealth·Player 태그). 원칙: UI는 월드를 직접 찾지 않는다. 24곳을 일괄 치환하지 말고 UI별 필요한 정보만 월드 쪽 등록·이벤트로 받음. 전투 씬이 없을 때도 안전 |
| 카메라 대상 | `CameraFollow2D`의 Player 태그 탐색 → 활성 월드가 추적 대상(전투=플레이어, 로비=아바타)·경계를 명시적으로 넘김 |
| 부팅 순서 | 세이브 로드가 지금은 '씬 빌더가 AutoSave를 먼저 만든다'는 생성 순서에 기댄다(`AutoSave.cs:18~24`). → GameFlow 부팅 단계가 **세이브 로드 → 타이틀(또는 에디터 지정 씬)** 을 명시적으로, 모든 소비자(UI·PlayerRig)의 `Awake` 전에 프로필 준비를 보장. AutoSave는 저장만. 테스트 포함 |
| 문서 | 착수 때 이 계획을 `HANDOFF.md`(현재 스프린트)로 옮긴다(`docs/sprints/`는 아카이브라는 CLAUDE.md 규칙). 팀원 제안 `06-scene-split.md` 맨 위엔 '대체됨' 배너 + 뒤집힌 결정표(4분할→3분할, 이유), 팀원에게 인수인계 때 알림 |
| 착수 조건 | CLAUDE.md '대규모 리팩터링 절차': 팀원 미병합 작업 전부 병합(원격엔 없음 확인, **팀원 로컬 미푸시 작업 확인 전엔 시작 안 함**) → `PROGRESS.md`에 '리팩터 진행 중 — 다른 세션 커밋 금지' 선언 → 한 세션 단독·`git mv` → 배치 컴파일 + PlayMode 전체 통과 → 즉시 main 병합·선언 해제 |
| 고칠 도구 | ApplyCharacterArt·ApplyTempCharacterArt(**팀원 확인 필요**)·CaptureStagePreview·CaptureUiPreview·MechanicTestMenu → 새 씬/프리팹/SO 대상으로 |
| 테스트 | RunController 직접 생성 4파일 이전. 새로: 씬 왕복(타이틀→로비→전투→결과→로비)·재도전·UI/EventSystem 중복 없음·일시정지 중 히트스톱 만료 시 timeScale 0 유지·씬 로드 실패·연속 전환. `SkillSlotTests.cs:264` 씬 경로, Build Settings 갱신 |


## 씬 분할 1차 — 결과 (2026-11-04)

- 씬: `Assets/Scenes/00_Persistent.unity`(빌드 0번) + `20_Combat.unity`(빌드 1번, 옛 `CombatCore` — GUID 유지). 로비는 아직 `20_Combat` 안.
- `RunController` → **`GameFlow`**(이름만, 기능은 2차에서 CombatDirector로 나눈다). **`GameBoot`** 신설(세이브 로드 → 월드 씬 로드 → 활성 씬 지정).
- UI 판은 씬에 놓인 `PixelUI`(화면 13개, `PxScreens.All`)이고 `Flow`로 GameFlow에 연결. 화면은 씬을 뒤지지 않는다.
- 카메라(`PixelPerfectCamera` 포함)는 `00_Persistent`, 플레이어가 `CameraFollow2D.Track`으로 스스로 등록.
- `PixelCanvas.Install`·`StageVisuals.Install` 삭제. `LobbyWorld`·`StageVisuals`는 `20_Combat`, `HitSound`는 `00_Persistent/Audio`.
- **버그 수정**: 보스 발판 세트가 꺼진 채 저장돼 Awake가 안 불려 보스 무대에 발판이 안 나왔다 → 두 세트를 켠 채 저장, `PlatformSet.Awake`가 현재 무대를 따른다.
- 에디터 Play: `Editor/PlayFromPersistent`가 어느 씬을 열어 두든 `00_Persistent`부터 시작.
- 테스트: `Systems/GameBootTests`(실제 씬 부팅·활성 씬·공용 오브젝트 하나씩·카메라 추적·보스 발판·타이틀→로비→사냥→결과).

## 2차 전 보완 (2026-11-05, Codex 판정 '보완 후 착수')

1. ✅ **씬이 원본인데 실행 중에 붙이던 것 제거** — `CameraFollow2D.Awake`가 `PixelPerfectCamera`를 붙이고 값을 덮어써서 씬엔
   기본값(100PPU·320×180)이 저장돼 있었다 → 씬에 128PPU·960×540·`UpscaleRenderTexture`·`StretchFill`·카메라 크기 2.109375를 저장하고
   Awake를 지웠다. `PlayerRig.Awake`가 붙이던 `PlayerAfterimages`·`BladeSlashVfx`는 씬의 Player에 배치. 둘 다 `SceneSetupValidator`가 검사하고
   `GameBootTests`가 실제 씬에서 확인한다. 투사체·이펙트처럼 실행 중에 생겼다 사라지는 것은 정당한 런타임 생성(순위 2에서 목록화).
2. ✅ **상태 소유권·UI↔월드 데이터 계약(결정만, 구현은 2차 단계에서)** — Codex '보완 2 합의'. 아래 표.
3. 팀원과 씬 편집 시점 — 사용자: 신경 쓰지 않는다(팀원이 git에서 받아 적용).

### 상태 소유권 (static은 유지 — 쓰기 위치만 좁힌다)

| 상태 | 쓰는 곳(여기서만) | 결정 |
|---|---|---|
| `GameState` | `GameFlow`만 `Set` | 그대로 |
| `RunState` | `Begin`·`MarkOver` = `GameFlow` / `Tick`·`EnterBossField` = `CombatDirector` / 처치·보상 = 그 원인 오브젝트(적·성소·구슬) / 사망 원인 = `PlayerHealth` | 처치·보상을 한곳으로 모으지 않는다(원본도 처치 지점에서 바로 집계) |
| `ProfileService` | 통째 교체·초기화는 `SaveService`만 | `Current`를 공개 필드 → **읽기 전용 프로퍼티**. '새 원정'은 `GameFlow.StartNewExpedition()`이 맡고 `TitleMenu`는 직접 `Reset()`하지 않는다. 프로필 안쪽 필드(111곳)는 지금 바꾸지 않는다 |
| `CombatModifiers` | 전투 처리·버프·새 런 초기화 = `CombatDirector` | 그대로 |
| `RunTransient` | 등록·해제 = 생성물 자신 / `DestroyAll` = `CombatDirector`(런 경계) | 그대로 |
| `CombatEvents`·`RunEvents` | 발행 = 사건을 일으킨 오브젝트 / 구독 = `CombatDirector` 등 처리부 | `Reset()`은 부팅·테스트에서만 |

### 연결 규칙 (2026-11-05 재검토 — 사용자 "정말 깔끔한 구조인지 다시 확인", Codex '재검토' 결론)

1. **같은 씬 안은 직렬화 참조**(씬이 원본 — 인스펙터에 보인다). 예: `StageVisuals`→`PlatformSet`·`BossPortal`, `BossPortal`→Player는 모두 `20_Combat` 안이라
   `[SerializeField]`로 잇는다. 같은 씬 안에서 찾지 않는다.
2. **UI ↔ 월드는 `WorldView` 하나로.** UI(`00_Persistent`)는 월드 컴포넌트(`Boss`·`EnemyHealth`·`BossPortal`·`PlayerRig`)를 직접 알지 않고,
   `Systems`의 읽기 전용 값 묶음 `WorldView`만 읽는다. 값을 채우는 건 그 월드의 담당 — 전투는 `CombatDirector`(같은 씬 참조와 `EnemySpawner`가 이미
   들고 있는 목록으로 — 탐색이 Director로 옮겨가지 않는다), 로비는 `LobbyWorld`. 상태가 바뀔 때나 HUD 갱신 주기(미니맵 0.15초)에만 채우고 매 프레임 복사하지 않는다.
   - 왜 '등록 목록을 UI가 직접 읽기'가 아닌가: 그러면 UI가 전투 구현 타입에 묶인다(Claude 대안을 Codex가 반대, Claude도 동의).
   - **값과 읽기 계약을 섞는다(2026-11-05 실행 계획 검토)**: HUD는 플레이어 HP·스킬 칸·버프 줄도 읽는데 이것까지 값으로 복사하면 복사 코드가 커지고
     이미 있는 HUD용 계약(`ISkillSlotSource`)이 무의미해진다. 그래서 체력·스킬은 **읽기 전용 인터페이스**(`IHealthView`(새로, Core)·`ISkillSlotSource`)로,
     포털·위치·미니맵 적 좌표는 **값**으로(적 좌표는 0.15초마다 `EnemySpawner`의 기존 목록으로 — `EnemyHealth.Active` 같은 중복 목록은 만들지 않는다).
     게시 메서드는 공개지만 부르는 곳은 `CombatDirector`·`LobbyWorld`뿐(`internal`은 UI asmdef인 LobbyWorld가 못 부른다). 월드가 내려가면 인터페이스 참조까지 비운다.
3. **월드 안의 정적 등록 목록은 소유자 내부용**(`GunnerDrone`·`GunnerTurret`·`GravityWellZone`·`RunTransient`·`PlatformSet`·`PxScreen`). 이름을 한꺼번에 바꾸지 않는다.
   단 **밖에 공개된 가변 목록은 읽기 전용으로**(`GravityWellZone.Active`가 public 가변 `List`). 새로 만들거나 고치는 등록 API는 목록 `Active`(`IReadOnlyList`)·
   하나 `Current`로 이름을 맞춘다.
4. **카메라 대상**은 지금처럼 대상이 등록(`CameraFollow2D.Track`). 로비는 가운데 앵커가 등록하고 `CameraFollow2D`의 `GameState.Lobby` 특례를 없앤다.
5. **월드가 없을 때**(타이틀·씬 전환 중): 월드가 내려갈 때 `WorldView`를 비우고 UI는 보스·미니맵·근접 표시를 숨긴다. 다시 찾는 예비 경로는 없다.
6. **검증**: UI 소스의 `FindFirstObjectByType`·`FindObjectsByType`·`FindGameObjectWithTag` 금지(소스 훑기 테스트). 런타임 전체 금지는 과하다 —
   씬 경계에서 한 번 찾는 곳(예: `PixelCanvas.EnsureEventSystem`)만 이유를 주석에 적고 허용. `WorldView` 채우기·비우기, 월드 없을 때 UI 안전성 테스트.

### 경계 (재검토)

- `GameBoot` = 처음 세이브 로드·월드 진입 / `GameFlow` = 게임 상태·시간(timeScale)·씬 흐름 / `CombatDirector` = 전투 틱·전투 이벤트·필드.
  Director는 줄 수만 보고 더 쪼개지 않는다(타격감 처리도 전투 이벤트라 Director에 둔다).
- 씬 3개(00/10/20)는 수명과 역할이 달라서 이 규모에서도 과하지 않다.

## 씬 분할 2차 — 할 일 (순서 = 커밋 단위, 2026-11-05 Codex와 실행 계획 합의)

진행 방식: 단계마다 feature 브랜치 → 구현 → verify 폴더에서 배치 컴파일·씬 구성 검증·PlayMode 전체 → diff 발췌로 Codex 구현 리뷰 → 고침 → 사용자 확인 후 `main`.

0. **세이브 교체 경계** — `ProfileService.Current`를 읽기 전용 프로퍼티로, 통째 교체 `Replace(PlayerProfile)`는 `SaveService`만. '새 원정'은
   `GameFlow.StartNewExpedition()`이 흐름을 맡고 `SaveService`의 새 프로필 기능을 부른다(`TitleMenu`의 직접 `Reset()` 제거). 프로필 안쪽 필드 111곳은 손대지 않는다.
1. ✅ **`CombatDirector` 추출(코드 + `20_Combat`에 오브젝트 하나)** — 2026-11-05, 로그 11-05 (6)

   > **의도적 편차 — 보스 격파 뒤 1.6초 대기(사용자 2026-11-05 "지금대로").** 원본(:4289 `setTimeout(() => { if (state.scene === 'run') endRun('bossdead'); }, 1600)`)은
   > 실제 시간으로 재서 ① 그 순간 일시정지 중이면 런을 아예 안 끝내고 ② 대기 중에도 런 시간이 흘러 시간 초과가 먼저 날 수 있다. 우리는 ① 사냥 중일 때만 재고
   > (일시정지 동안 멈췄다 이어 감) ② 대기 중엔 런 시간을 안 흘린다 — 보스를 잡았는데 시간 초과나 '안 끝남'으로 뒤집히지 않게.
   > 반대로 **처치 멈춤(0.25초)이 대기 중에 풀리는 건 원본대로**(단계 1에서 바로잡음 — 예전엔 결과 화면까지 1.6초 내내 멈춰 있었다).

   - 연결: **`GameFlow` → Director는 명시 호출**(Director가 `CombatDirector.Current`로 등록, `BeginField(mode)`·`ClearField()`), **Director → `GameFlow`는 이벤트**
     (`RunEvents.RunEndRequested(RunEndReason)` 추가 — 새 버스를 만들지 않는다. 히트스톱은 기존 `CombatEvents.RequestHitstop`). 한 방향만 직접 안다.
   - Director: `RunState.Tick`·`CombatModifiers.Tick`·`HitFeel.TickRedFlash`, 보스 격파 지연(멈춘 시간 1.6초, 그동안 Tick 안 함)→종료 요청, 사망·타임아웃→종료 요청,
     처치 연출(`KillFeel` 이동)·간격 둔 흔들림·성소 버프, 보스 포털 요청(해금 확인 포함)→`EnterBossField`, 필드 정리(발판 세트·`RunTransient.DestroyAll`·
     `CombatModifiers.ResetForRun`·`HitFeel.Reset`·플레이어 리셋 — Player는 같은 씬 직렬화 참조).
   - `GameFlow`에 남음: ESC, `GameState.Set`, timeScale, 히트스톱 만료·쉬는 시간, `StartRun`(날짜 차감·`RunState.Begin` → Director.BeginField → Run → timeScale 1)·
     `Pause`·`Resume`·`EndRun`(`MarkOver`·`LastEndReason`)·`EnterLobby`·`EnterTitle`(Director.ClearField — 3단계 뒤엔 전투 씬이 내려가 필요 없어짐)·`StartNewExpedition`. 옛 주석 갱신.
   - 테스트: GameFlow를 만들던 9파일은 Director도 만드는 공용 헬퍼로. 테스트는 공개 API를 넓히지 말고 `CombatEvents` 경로로. 새로: 구독·해제 대칭, Director 없을 때 GameFlow 안전, 보스 격파 지연 중 타임아웃 안 됨.
2. ✅ **2a 데이터**(2026-11-05, 로그 11-05 (8)~(11)) — 캐릭터 선택은 프로필만(`PlayerRig` 탐색·`Select`/`SelectNext` 삭제), 몬스터 그림은 `Core/MonsterCatalog` 에셋
   (스포너·스테이지 선택이 같이 봄), 캐릭터 그림은 `Core/CharacterArtSet` 에셋(그림·색만). **정한 것**: 스테이지 선택 카드 목록은 스폰표 복사본이 아니라 표시 정책(칸 5개)으로 두고
   테스트로 묶는다 / 캐릭터 정의 SO(이름·해금)는 해금 조건이 생길 때 / 보스 정의 SO는 안 만든다(그림 하나뿐) / 그림 도구 둘은 같은 에셋을 고친다.
   ✅ **2b 연결**(2026-11-05, 로그 11-05 (13)~(17), Codex "2b 통과" — 커밋 순서대로):
   1. `GravityWellZone.Active` → `IReadOnlyList`, 없애기는 `Vanish()`(목록에서 즉시 빼고 Destroy)(지금 `MageSkillEffects`가 목록을 직접 지운다).
   2. **같은 씬은 직렬화, 실행 중 생기는 것은 만드는 쪽이 넘긴다**: `StageVisuals`(Ground·발판 2·포털)·`BossPortal`(Player)·`LobbyWorld`(Player)·`LobbyHud`(같은 씬 카메라)
      → `[SerializeField]`. 경험치 구슬·몹(`EnemyMove`)은 **스포너가 생성 때 Player를 넣는다**(태그 탐색 제거 — 예외를 두면 같은 규칙이 두 갈래가 된다, 테스트 13파일은 대상을 직접 넣게).
      다른 씬의 카메라(`StageVisuals`의 `Camera.main`)는 `CameraFollow2D`가 자기 카메라를 등록해 `CurrentCamera`로.
   3. **`WorldView` = 읽기 계약 창구**(위 연결 규칙 2를 이렇게 구체화 — 값을 복사해 넣는 루프 대신 물을 때 계산): `Systems/WorldView`에 `ICombatView Combat`(구현 `CombatDirector`)·
      `ILobbyView Lobby`(구현 `LobbyWorld`), `Attach`는 중복이면 오류·`Detach`는 자기일 때만. `ICombatView` = 플레이어 체력·위치·`ISkillSlotSource` 키트, 포털(struct), 보스 체력 비율,
      `CopyEnemyPositions(list)`(먼저 비움). 미니맵은 지금처럼 **살아 있는 적 전부**(잡몹·소환·성소·보스) — `EnemyHealth`가 활성 목록에 등록하고 Director가 HUD 주기(0.15초)에 복사.
      `ILobbyView` = 근접 건물·아바타 위치. `CombatHud`·`LobbyHud`는 이것만 읽고 없으면 숨긴다.
      **구현하며 정한 것(2026-11-05)**: `Systems`는 `Characters`를 참조하지 않는다(asmdef — 기존 `IRunResettable`·`CombatEvents.PlayerDied`와 같은 방식). 그래서 Director가 플레이어를
      읽는 계약을 `Core`에 뒀다 — `IHealthView`(PlayerHealth 구현)·`ISkillSlotHost`(PlayerRig 구현, 지금 키트의 칸), 그리고 HUD 계약 `ISkillSlotSource`·`SkillSlotState`를
      `Characters` → `Core`로 옮겼다(제공은 Characters, 읽기는 Systems·UI라 아래층 계약). 보스 체력은 스포너가 표시한 `EnemyHealth.isBoss`로 찾는다.
   4. 로비 카메라: `LobbyWorld`의 가운데 앵커를 로비 진입 때 `Track(anchor, snap)`(즉시), 사냥 시작 때 `CombatDirector.BeginField`가 `Track(player)`(지금처럼 부드럽게).
      `CameraFollow2D`의 `GameState.Lobby` 분기 2곳 삭제 — 로비 대상은 움직이지 않는 앵커라 멈춘 시간에도 따라갈 게 없다(3단계 로비 아바타도 카메라는 앵커).
      `LobbyWorld`는 `git mv` → `UI/Lobby/`(네임스페이스·meta 유지).
   검증: 런타임 코드 전체의 월드 탐색 금지(소스 훑기 — 남는 예외는 `PixelCanvas.EnsureEventSystem` 하나, 이유 기록), 미니맵 점 수·포털·보스 표시·카메라 전환이 예전과 같은지,
   `WorldView` 붙이기·떼기·월드 없을 때 HUD 안전, 검증기는 새 직렬화 참조 전부.
3. **`10_Lobby` + 씬 전환**(2026-11-05 Codex와 계획 합의 — 커밋 순서대로, 플레이 안 되는 중간 커밋은 만들지 않는다):
   0. **발판 무대 전환을 전투 담당으로** — `PlatformSet.All`(정적, 꺼진 세트도 들고 있음)은 재도전 때 옛·새 전투 씬이 겹치면 옛 씬 발판까지 다시 켠다(Codex 지적).
      → `CombatDirector`가 같은 씬의 두 세트를 직렬화로 들고 직접 켜고 끈다(연결 규칙 1), 정적 목록·`HideAll` 삭제. `FieldLayout.SetBossArena`(좌표 데이터)는 그대로.
   1. **로비 아바타** — `LobbyAvatar`(걷기·그림만: 프로필 캐릭터의 `CharacterArtSet` 그림, 높이 맞추기는 `PlayerRig`와 **공용 함수**, 걸음 속도도 공용 값 — 복제 금지).
   2. **`10_Lobby` 씬 만들기·검증**(아직 실행 경로에 안 잇는다): LobbyWorld·CameraAnchor·아바타·땅(그림 크기 입력용 — `StageVisuals.BuildGround`가 판정 상자 크기를 읽음)·
      StageVisuals(땅·배경만). 검증기: 빌드 목록 `[00_Persistent, 10_Lobby, 20_Combat]`, 로비 필수 구성·전투 오브젝트 없음, 직렬화 참조.
   3. **씬 로더 계약**: `IWorldSceneLoader`(실제 = `SceneManager` 경계만 감쌈, 테스트용 = 성공·실패·결과 제어). 기존 GameFlow 단위 테스트는 가짜 로더, 씬 왕복·재도전은 실제 씬 통합 테스트.
      직렬화 플래그(`loadWorldScenes`)는 씬 설정 실수로 이어져서 안 쓴다.
   4. **GameBoot·GameFlow·빌드 목록 전환**(한 커밋): GameBoot = 세이브만(타이틀엔 월드 없음). GameFlow 전환 코루틴 — 잠금(중복·입력 무시) → 이전 월드 루트 비활성 →
      새 씬 로드 → **실패**(예외·씬 없음·전투면 Director 없음)면 이전 `GameState`·`timeScale`·활성 씬·루트별 `activeSelf` 복구 → 성공이면 **`sceneLoaded`로 받은 새 Scene 핸들**로
      활성 씬 지정(재도전은 이름이 같아 이름으로 찾으면 안 된다) → 전투면 날짜 차감·`RunState.Begin`·`BeginField`·Run, 로비면 일반 무대로 되돌리고 `LobbyWorld.EnterLobby`(앵커 스냅)를
      **전환 안에서** 끝낸 뒤 Lobby → 이전 씬은 **이전 핸들로** 언로드 → 잠금 해제. '새 원정'은 로비 로드 성공 **뒤에** 세이브 삭제(실패하면 진행을 잃지 않게).
      새 씬 로드 중 `Awake`·`OnEnable`에서 생기는 오브젝트의 씬 소속을 확인(부모 지정)·테스트.
   5. **에디터 진입**: `PlayFromPersistent`가 열린 씬을 `SessionState`에 쓰고 `GameFlow`가 `#if UNITY_EDITOR`에서 한 번 읽고 지운다 — 10_Lobby → 로비, 20_Combat → `defaultRegion` 사냥
      (**디버그 진입은 날짜를 안 줄인다**), 그 밖 → 타이틀. 빌드 부팅은 늘 타이틀.
   로비 timeScale은 **3단계에선 0 유지**(4단계에서 시간 쓰는 곳 확인 뒤 판단 — 씬 분리와 시간 규칙을 섞지 않는다).
   **재검토로 고친 것(2026-11-05, 사용자 "다시 확인" — Claude가 코드 대조로 찾고 Codex가 다듬음)**:
   - **계층**: `GameFlow`(Systems)는 `LobbyWorld`(UI)를 부를 수 없다 → **로비 씬 = 로비 모드**라 `LobbyWorld`가 `OnEnable`에서 스스로 준비(건물·카메라 앵커 스냅·아바타 시작 자리).
     준비는 여러 번 불려도 같게, **아바타 시작 자리는 처음 로드 때만**(전환 실패로 다시 켜질 땐 위치 유지). 예전의 'GameState를 보고 EnterLobby'는 삭제, 걷기·건물 입력은
     `GameState == Lobby && !GameFlow.IsTransitioning`일 때만. 로비의 전역 `PlatformSet.HideAll` 호출도 삭제(3-0).
   - **잠금 범위**: 전환 중엔 시작·로비·타이틀·새 원정뿐 아니라 **Pause·Resume·ESC·EndRun**도 막는다(일시정지 메뉴 "로비로" 전환 중 ESC → Resume → 꺼진 월드에서 시간이 흐르던 구멍). 화면 버튼 입력도.
   - **동기 완료**: 로더 작업이 이미 끝났으면 코루틴이 yield하지 않는다(전환 앞부분에 무조건 yield 금지) → 가짜 로더 테스트는 지금처럼 동기 단언. 비동기 완료·실패는 통합 테스트로.
   - **`ClearField` 삭제**: 성공한 전환은 씬 언로드가 정리한다. 실패 복구를 위해 전환 시작 전에 필드를 지우지 않는다. `BeginField`의 초기화·보스 필드 내부 정리는 유지.
     '필드가 비워진다' 단언(GameFlowTests 2개)은 실제 씬 통합 테스트로 옮긴다.
   - **화면과 실패**: '원정 떠나기'·'다시 원정'은 **전환 결과를 받아 성공 때만 창을 닫고, 실패하면 공용 팝업으로 알린다**(요청 즉시 닫으면 실패를 알 수 없다).
   - **실패 복구 목록**에 `FieldLayout.IsBossArena`도(로비 로드 전에 일반 무대로 바꾸므로).
   - **이동 속도**: 기본 이동 속도를 `Core` 설정값 하나로(원본 CONFIG.player.moveSpeed 270÷100), 플레이어·로비 아바타가 같이 쓰고 `CharacterMover2D`의 중복 필드는 없앤다.
   - **에디터 캡처 도구**: 로비 캡처는 `10_Lobby`를 연다(3-2). **검증기**: 씬별 필수·금지 오브젝트(StageVisuals 규칙도 씬별 — 로비엔 포탈·발판 없음), 빌드 순서.
   테스트: 로더 실패·Director 누락·연속 입력·재도전 씬 핸들·활성 씬·실제 씬 왕복·세이브 보존·두 월드 발판/물리 겹침 없음·전환 실패 뒤 정적 등록·카메라 대상 복구·로비에서 캐릭터 바꾸면 아바타 그림.
4. **마감** — UI 월드 탐색 0곳(소스 훑기 테스트), 로비 timeScale(0→1)은 게임 시간을 쓰는 곳을 전부 확인한 뒤에만, 전체 회귀.

## 성능(나중에) — 구조 정리 뒤에 본다

### 측정 (할 일)

- [ ] 개발 빌드 + Profiler로 최악 장면 측정(최대 몬스터 + 폭발형 스킬 동시)
- [ ] 기록: 프레임 시간, 프레임당 GC 할당, Draw Call, 웨이브 시작·보스 등장 순간 스파이크

| 항목 | 측정값(전) | 측정값(후) |
|---|---|---|
| 평균 프레임 시간 | | |
| 최대 프레임 시간(스파이크) | | |
| 프레임당 GC 할당 | | |
| Draw Call / Batches | | |

### 후보 (코드에서 확인된 것)

| # | 후보 | 위치 | 고칠 방향 | 난이도 | 담당 |
|---|---|---|---|---|---|
| 1 | 생성·파괴 반복 | `Destroy` 60곳, `new GameObject`/`Instantiate` 42곳 | 자주 나오는 것만 `ObjectPool` | 중 | 설계 Codex → 구현 Claude |
| 2 | 물리 질의 GC | `OverlapCircleAll` 18곳(그중 `BladeCombat.cs` 6곳은 팀원 담당) | 버퍼 재사용 `OverlapCircle(ContactFilter2D, buffer)` | 하 | Claude |
| 3 | UI 매 프레임 탐색 | `CombatHud.cs:323`(보스), `:401`(미니맵 0.15초마다 전체 탐색) | 캐시 / 등록 목록 | 하 | Claude |
| 4 | 매 프레임 GetComponent | `EnemyMove.OverlapsTarget()` | 대상 바뀔 때만 캐시 | 하 | Claude |
| 5 | Draw Call | 이펙트 팩 텍스처가 제각각 | Sprite Atlas | 하~중 | Claude |
| 6 | UI 캔버스 재구성 | HUD가 한 캔버스 | 자주 바뀌는 HUD 분리 | 중 | 설계 Codex |

### 빌드 설정

- [ ] Incremental GC
- [ ] IL2CPP
- [ ] targetFrameRate / VSync
- [ ] 텍스처 Mipmap 끄기, 오디오 로드 방식


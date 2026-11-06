# 상태 소유권 표 (2026-11-06, 정리 순서 4)

> 실행 중에 바뀌는 상태마다 **누가 갖고(소유자) · 누가 바꾸라고 하고(변경 요청자) · 언제 생기고 비워지며(수명) · 누가 읽는지**를 적는다.
> static인지는 따로 적는다 — static이라서 나쁜 게 아니라, **바꾸는 길이 하나이고 수명이 분명하면** 된다.
> 규칙: 상태는 **자기 변경을 스스로 책임지고**(의미 있는 메서드로만 바뀐다), 사건이 생긴 쪽이 그 메서드를 부른다. 모든 쓰기를 한 관리자에 몰지 않는다.
> 계획·진행은 `HANDOFF.md` '3. 4 상태 소유권'. 표가 코드와 어긋나면 이 표를 먼저 고친다.

## 1. 게임 흐름·런

| 상태 | static | 소유자(바꾸는 코드) | 변경 요청자 | 수명·초기화 | 읽는 곳 | 검증 |
|---|---|---|---|---|---|---|
| 화면 상태 `GameState`(Current·Previous·IsTransitioning·전환 횟수 `Version`) | ✓ | `GameFlow`만 | 화면 버튼·전투 사건(이벤트로) | 앱 전체. 테스트만 `Reset` | 화면 전부·전투 | `StateOwnershipTests`(소스 검사) |
| 시간 정지 `Time.timeScale` | (엔진) | `GameFlow`만 | 히트스톱 요청(`CombatEvents.RequestHitstop`) | — | 엔진 | `StateOwnershipTests` |
| 런 기록 `RunState`(지역·모드·남은 시간·처치·살기·보상·이번 원정 최고 콤보·끝남) | ✓ | `RunState` 메서드 | `GameFlow`(시작·끝·되돌리기)·`CombatDirector`(시간·보스 필드)·`EnemySpawner`·`Shrine`·`ExpOrb`(처치·보상)·`CombatModifiers.AddCombo`(최고 콤보)·`PlayerHealth`(죽은 원인) | `GameFlow.StartRun`의 `Begin`(실패하면 `Restore`) | HUD·결과 화면·전투 | 런 사이클 테스트 |
| 보스 무대 `FieldLayout.IsBossArena` + 필드 좌표 `FieldBounds` | ✓ | `FieldLayout.SetBossArena` 한 경로 | `GameFlow`(로비 = 일반 무대)·`CombatDirector`(보스 필드) | 전환 실패면 `GameFlow`가 되돌림 | 스폰·카메라·발판·화면 | `MinX`·`GroundY` 상수, `MaxX`는 읽기 전용(`SetMaxX`는 `SetBossArena`만) — 무대 전환 테스트 |
| 전투 배수 `CombatModifiers`(콤보·연쇄·성소 버프) | ✓ | `CombatModifiers` 메서드 | `EnemyHealth`(피해)·`EnemySpawner`(처치)·`Shrine`(버프)·`CombatDirector`(시간) | `CombatDirector.BeginField`의 `ResetForRun` | 피해 계산·HUD·결과 화면 | 4-1(최고 콤보 초기화) |
| 타격감 `HitFeel`(흔들림·붉어짐) | ✓ | `HitFeel` 메서드(요청형) | 전투 쪽 여러 곳(`Shake` 요청) | `BeginField`의 `Reset` | 카메라·HUD | — |
| 전투 담당 등록 `CombatDirector.Current` | ✓ | `CombatDirector` 자신(OnEnable/OnDisable) | — | 전투 씬이 켜져 있는 동안 | `GameFlow` | 씬 전환 테스트 |

## 2. 저장되는 것

| 상태 | static | 소유자 | 변경 요청자 | 수명 | 읽는 곳 | 검증 |
|---|---|---|---|---|---|---|
| 프로필 `ProfileService.Current`(레벨·골드·캐릭터·지역 진행·아이템·통계…) | 참조만 ✓ | 통째 교체: `SaveService`만(`Replace`). 값 변경: **`PlayerProfile` 메서드**(`SpendDay`·`AddGold`·`AddExp`·`RegisterRegionKill`·`RecordEnemyKill`·`RecordCombo`·`RecordBossKill`(통계 + `MarkBossCleared` — 정복 표시만은 테스트 준비)·`RecordShrineBroken`·`TryPayForPull`·`SetSelectedCharacter`·`TryBuyUpgrade`·`TryLearn*Tier`·`DoRegression`)·뽑힌 아이템은 `ItemInventory.RecordPull` | 전투(보상)·화면(구매·습득·선택) | 세이브 파일 — `GameBoot`가 불러오고 `AutoSave`가 화면이 바뀔 때 저장 | 거의 전부(110곳 — 읽기는 그대로 둔다) | 새 메서드 동작 테스트(`AchievementTests`·`ItemAndGachaTests`)·`DefinedApis` 목록. **직접 대입 예외**: 세이브 이전(`SaveService`)·사냥 시작 실패 롤백(`GameFlow`의 `doomDays` 복원)·에디터 전용 'SP +1' 버튼(`AwakenScreen`, `#if UNITY_EDITOR`)·테스트 준비 |
| 설정 `GameSettings`(전체 화면·해상도·음량) | ✓ | `GameSettings` 속성(PlayerPrefs) | 설정 창 | PlayerPrefs | 소리·화면 | — |

## 3. 등록소·창구 (켜져 있는 동안만 들어 있다)

| 상태 | 소유자 | 수명 | 읽는 곳 |
|---|---|---|---|
| `WorldView.Combat`·`Lobby` | 전투 담당·로비 월드(OnEnable 붙이기/OnDisable 떼기) | 그 월드 씬이 켜져 있는 동안 | 화면(HUD·로비) |
| `CameraFollow2D.Target`·`CurrentCamera` | 카메라(자기 등록)·대상은 그 화면 담당이 `Track` | 대상이 꺼지면 `Untrack` | 배경·HUD 좌표 |
| `EnemyHealth.Active`·`RunTransient.Active`·`GravityWellZone`·`GunnerDrone`·`GunnerTurret` 목록 | 각 컴포넌트(생길 때 넣고 없어질 때 뺌) | 그 오브젝트가 살아 있는 동안(씬이 내려가면 같이) | 미니맵·필드 정리·스킬 |
| `PxScreen` 목록·화면별 `instance`(`Open()` 진입점) | 각 화면(상주 씬) | 앱 전체 | UI |
| `UiModal`(창 깊이) | `PxScreen`만 Push/Pop | 창이 떠 있는 동안 | ESC 처리 |

## 4. 이벤트 버스

`CombatEvents`·`RunEvents`·`EnemySpawnRequestBus`·`ProfileService.Replaced`·`GameState.Changed`, 그리고 프로필 인스턴스 이벤트 `PlayerProfile.LeveledUp`(`PlayerHealth`가 지금 프로필에 붙는다) — 구독은 `OnEnable`, 해제는 `OnDisable`(대칭). 테스트는 각 `Reset()`으로 비운다. 왕복 뒤 구독자 수는 `GameBootTests.LobbyRunLobby_RoundTrip_LeavesOnlyLobbyState`가 본다.

## 5. 캐시·테스트 전용 (상태라기보다 보관)

- 그림 캐시: `PxArt`(UI 그림·톤·9슬라이스)·`StageVisuals.partials`(잘린 타일)·`Px.Font`·`GravityBurstRing.material` — 같은 입력이면 같은 결과, 지워도 다시 만든다.
- `SaveService.PathOverride` — 테스트가 세이브 경로를 임시 파일로 돌린다(`TestSaveSandbox`). 게임에선 비어 있다.
- 각 정적 클래스의 `Reset()` — 테스트 격리용(게임 흐름에선 부르지 않는다: `CombatModifiers.ResetForRun`·`HitFeel.Reset`처럼 게임이 부르는 초기화는 위 표의 '수명' 칸).

## 6. 정리할 것 (4-1~4-5)

- ✅ 왕복 수명(4-5): 로비 → 사냥 → 로비 뒤 로비 창구·카메라 기준점은 남고, 전투 창구·담당 등록·몹·중력장·드론·포탑·런 오브젝트는 비며, 정적 이벤트·프로필 이벤트 구독자 수가 왕복 전과 같다 — `GameBootTests.LobbyRunLobby_RoundTrip_LeavesOnlyLobbyState`. `RunState`·`CombatModifiers` 변경 API는 검토만(이미 사건 이름 메서드뿐).

- ✅ `CombatModifiers.MaxCombo`가 런마다 안 지워져 결과 화면 '원정 통계 최대 콤보'가 세션 전체 최고가 된다 → 원정마다 초기화(4-1). 재점검에서 **사냥 준비가 실패하면 지난 원정 값까지 지워지는** 문제가 나와 `RunState.MaxCombo`로 옮겼다(런 기록이라 `Begin`이 지우고 `Restore`가 되돌린다).
- ✅ `RunProgress.TotalKills`는 세기만 하고 읽는 곳이 없다, `RunProgress.RegionLv`는 `RunState.Region`의 별칭 → 삭제(4-2). 난이도·보상은 `RunState.Region`을 직접 읽는다.
- ✅ `FieldBounds.MinX`·`MaxX`·`GroundY`가 public 가변 필드 → `MinX`·`GroundY`는 상수, `MaxX`는 읽기 전용 + `SetMaxX`(4-3).
- ✅ 재점검(2026-11-06)에서 지역 변수를 거친 통계 쓰기 2곳을 더 찾았다(처음 전수 조사가 `ProfileService.Current.x`·`profile.x` 꼴만 봤다): 평생 최고 콤보(`CombatModifiers`) → `RecordCombo`, 누적 처치·정예(`EnemySpawner`) → `RecordEnemyKill`. 새로 찾을 땐 `var stats = …` 같은 별칭도 같이 검색할 것.
- ✅ 프로필 직접 대입: 캐릭터 선택(`PlayerRig`·`CharacterSelectScreen`) → `SetSelectedCharacter`, 뽑기 재화·통계(`GachaService`) → `TryPayForPull` + `ItemInventory.RecordPull`(천장), `stats.shrines++`(`Shrine`)·`stats.bosses++`(`EnemySpawner`) → `RecordShrineBroken`·`RecordBossKill`(4-4). 'SP +1 테스트 버튼'은 이미 에디터 전용이라 예외로 둔다. 직렬화 필드라 public은 그대로(세이브 형식 유지) — 게임 코드는 메서드로만.

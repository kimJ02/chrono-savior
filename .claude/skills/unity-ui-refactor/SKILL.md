---
name: unity-ui-refactor
description: Chrono Savior(Unity) UI 구조를 분석하고, 코드로 만드는 UI를 Scene/Prefab 기반으로 단계적으로 옮기는 계획·실행·검증을 안전하게 진행한다. "UI 리팩터링", "UI를 프리팹으로", "하드코딩 UI 정리", "PxScreen 구조 개선"을 요청받을 때 사용한다.
---

# unity-ui-refactor — UI 구조 분석·전환 스킬

이 스킬은 **Chrono Savior 저장소 전용**이다. 작업 전에 루트 `CLAUDE.md`(특히 "UI 리팩터링 안전 규칙" 절)를
먼저 읽고, 이 스킬과 충돌하면 **`CLAUDE.md`가 우선**한다.

## 0. 이 프로젝트의 UI가 지금 어떻게 생겼는지 (전제)

분석 전에 아래 사실을 실제 코드로 다시 확인한다. 아래 내용은 2026-11-22 기준이고, 바뀌었을 수 있다.

- **프레임워크**: uGUI(`UnityEngine.UI`, 레거시 `Text`). UI 툴킷은 쓰지 않는다.
- **판**: `Assets/Scripts/UI/Pixel/PixelCanvas.cs`. 캔버스 하나 위에 640×360 Figma 판을 얹고, 층은 `Hud/Window/Popup/Fx`로 나뉜다.
  게임에서는 `Assets/Scenes/00_Persistent.unity`에 판과 모든 화면 컴포넌트가 놓여 있다.
- **화면**: `Assets/Scripts/UI/Screens/*.cs`는 모두 `PxScreen`을 상속한다(`Pixel/PxScreen.cs`).
  - `Build()`는 처음 한 번 실행되며, `Px.Image/Text/Button/IconButton` 등으로 **Figma 좌표 상수를 넣어 런타임에 그림·글자를 만든다.**
  - `WantsVisible`로 켜고 끄고(게임 상태를 보고), `Refresh()`로 매 프레임 값을 갱신하며, `AddNav`로 키보드 선택을 붙인다. `Modal`일 때는 ESC를 먼저 가져간다(`UiModal`).
- **씬이 없는 경로**: 테스트·배치 캡처는 `PixelCanvas.Create()`가 판을 통째로 코드로 만든다(`PxScreens.AddAll`).
  **화면을 프리팹으로 옮기면 이 경로도 같이 고쳐야 한다.** 빠뜨리면 테스트와 Figma 비교 캡처가 깨진다.
- **UI 층 규칙**(`Assets/Scripts/UI/README.md`):
  - UI는 asmdef 4층(최상위)이다. 게임 로직을 두지 않는다.
  - 월드는 `Systems/WorldView`로만 읽는다.
  - 상태·시간은 `GameFlow`에 요청한다.
  - 이 규칙들은 `WorldLookupTests`·`StateOwnershipTests`·`CreationOwnershipTests`가 소스를 훑어 강제한다.

## 1. 절대 규칙 (모든 단계 공통)

1. **기존 기능을 임의로 삭제하지 않는다.** 화면·버튼·키보드 조작·툴팁·글자 하나라도 없애야 하면 먼저 사용자에게 묻는다.
2. **`.meta` 파일을 임의로 삭제·재생성·직접 편집하지 않는다.** 에셋을 옮길 때는 에셋과 `.meta`를 같이 `git mv` 한다.
   `git status`에 의도하지 않은 `.meta` 추가·삭제·GUID 변경이 보이면 **즉시 멈추고 보고한다.**
3. **씬·프리팹의 참조를 보호한다.**
   - `.unity`/`.prefab` YAML은 손으로 고치지 않는다. 생성·수정은 Unity 에디터나 에디터 스크립트로만 한다.
   - `MonoBehaviour` 클래스 이름과 파일 이름을 바꾸지 않는다. 바꾸면 씬이 들고 있는 스크립트 GUID 연결이 끊긴다.
   - `[SerializeField]` 필드 이름을 바꿀 때는 `[FormerlySerializedAs]`를 붙인다.
4. **변경 전에 항상 Git 상태를 확인한다**(2절의 사전 점검).
5. **별도 작업 브랜치(`feature/ui-refactor-<단위>`)에서만 수정한다.** `main`에 직접 커밋하지 않는다.
6. **전체 UI를 한 번에 교체하지 않는다.** 한 번에 **화면 하나(또는 공용 부품 하나)** 만 다룬다.
7. **분석·계획 단계에서는 파일을 하나도 수정하지 않는다.** 읽기와 보고서 출력만 한다.
8. **Unity 에디터나 배치 테스트로 확인하지 못한 항목은 "미검증"으로 보고한다.** 짐작으로 "될 것"이라고 쓰지 않는다.

## 2. 사전 점검 (매 작업 시작 시, 수정 없음)

```bash
git status --porcelain          # 작업 트리가 깨끗한지 확인. 더러우면 멈추고 사용자에게 보고
git branch --show-current       # 지금 브랜치
git fetch && git status -sb     # origin과의 차이
git log --oneline -5
git branch -a --no-merged main  # 다른 세션의 미병합 브랜치. UI 파일을 건드리는 게 있으면 보고
```

- `Temp/UnityLockfile`이 있으면 Unity 에디터가 프로젝트를 열고 있는 것이다. 배치 검증이 불가능하다는 사실을 기록한다(`CLAUDE.md` 협업 규칙 7).
- `PROGRESS.md`와 `HANDOFF.md`를 읽고, **UI 리팩터링이 지금 스프린트 범위 안인지** 확인한다.
  범위 밖이면 분석 보고서까지만 만들고, 실행 여부는 사용자에게 묻는다.
- `docs/worksplit.md`에서 대상 화면의 담당자를 확인한다. 팀원 담당이면 "팀원 확인 필요"로 표시한다.

## 3. 분석 단계 (읽기 전용)

### 3-1. 목록 만들기

- 코드로 만드는 UI 찾기:
  ```bash
  grep -rn "class .* : PxScreen" Assets/Scripts/UI
  grep -rn "Px\.\(Image\|Text\|Fill\|FullFill\|Button\|TextButton\|IconButton\|Node\|Stretch\)" Assets/Scripts --include=*.cs
  grep -rn "new GameObject\|AddComponent<\|Instantiate(" Assets/Scripts/UI
  ```
- 씬·프리팹 기반 UI 찾기:
  ```bash
  grep -ln "m_Script: {fileID: 11500000, guid:" Assets/Scenes/*.unity
  # 화면 스크립트의 .meta guid → 씬에서 그 guid를 쓰는 곳
  ls Assets/Prefabs
  ```
- UI 밖에서 UI를 만드는 곳(`Systems`의 데미지 숫자·월드 위 글자 등)도 따로 표시한다.

### 3-2. 화면마다 분류

| 분류 | 판단 기준 | 목표 구조 |
|---|---|---|
| **정적** | 좌표·그림·글자가 고정이고, 켜고 끄기만 함(예: 일시정지 틀, 타이틀 배경·버튼) | 화면 프리팹(또는 `00_Persistent` 안의 계층)으로 옮기고, 스크립트는 참조와 이벤트만 담당 |
| **동적** | 개수·내용이 데이터에 따라 바뀜(캐릭터 목록, 유물 칸, 업적 줄, HUD 수치) | 항목 하나를 재사용 프리팹으로 만들고, `Bind(data)`로 채우는 뷰 + 데이터 바인딩 |
| **혼합** | 틀은 정적이고 안쪽에 동적 목록이 있음 | 틀은 프리팹, 목록은 항목 프리팹 + 바인딩 |
| **유지** | 옮겨도 이득이 없거나 위험이 큼(캡처·테스트 전용 경로, 월드 좌표를 따라다니는 글자 등) | 그대로 두고, 그 이유를 보고서에 적음 |

### 3-3. 화면마다 반드시 기록할 것 (보존 목록)

- 공개 API(다른 파일이 부르는 메서드·프로퍼티)와 호출하는 곳
- 버튼별 콜백(예: `Flow?.Resume()`, `SettingsWindow.Open()`)과 키보드 내비 순서·배치(`Layout`, `NavColumns`, `DefaultNav`)
- `WantsVisible` 조건, `Modal`/ESC 처리, `OnShown`/`OnHidden`에서 하는 일
- 사용하는 그림 키(`PxArt.Get("...")`), 글자 크기·색, Figma 노드 번호 주석
- 이 화면을 언급하는 테스트(`grep -rn "<화면이름>" Assets/Tests`)

### 3-4. 분석 보고서 형식 (사용자에게 출력. 파일로 저장하지 않음)

```
## UI 분석 보고서 (날짜, 브랜치, 커밋 해시)
- 사전 점검 결과: 작업 트리 / 미병합 브랜치 / 에디터 잠금 / 스프린트 범위
- 화면 목록 표: 화면 | 파일·크기 | 분류 | 의존(테스트·호출처) | 담당 | 위험도
- 화면별 보존 목록 (3-3)
- 테스트·캡처 경로 영향 (PixelCanvas.Create, 캡처 도구)
- 확인 필요 / 원본과 다른 점
```

## 4. 계획 단계 (읽기 전용)

- 작업 단위 = **화면 1개 또는 공용 부품 1개.** 위험이 낮고 의존이 적은 것부터 한다(예: `PauseMenu` → `TitleMenu` → … → `CombatHud`는 마지막).
- **첫 단위는 공용 기반부터** 한다(예: 프리팹이 `PxScreen`의 `Build()` 대신 쓸 연결 방식, 동적 항목용 `Bind` 패턴).
  이 기반은 시그니처를 문서로 고정한 뒤 진행한다(`CLAUDE.md` 협업 규칙 4).
- 단위마다 아래를 적는다:
  - 목표 구조
  - 건드릴 파일 / 건드리면 안 되는 파일
  - 새로 생기는 에셋(예상 경로)
  - 보존 목록 대조 방법
  - 검증 항목
  - 되돌리기 방법(`git revert <커밋>`)
- `00_Persistent.unity`를 건드리는 단위는 **한 세션만, 순차적으로** 진행한다.
- 계획은 사용자 승인을 받은 뒤 `HANDOFF.md`(현재 스프린트 범위)와 `docs/worksplit.md`(분업이 있을 때)에 적는다.
  이건 문서 수정이므로 실행 단계가 시작된 뒤에 한다.

## 5. 실행 단계 (단위마다 반복)

1. 사전 점검(2절)을 다시 하고, `git switch -c feature/ui-refactor-<단위>`로 브랜치를 만든다(이미 있으면 그 브랜치로 이동).
2. 프리팹·씬 수정은 **Unity 에디터, 또는 이유가 적힌 에디터 스크립트(`Assets/Scripts/Editor`, 네임스페이스 `ChronoSavior.Editor`)** 로만 한다.
   씬을 코드로 통째로 다시 만드는 도구는 만들지 않는다(`CLAUDE.md`: 씬 파일이 유일한 원본).
3. 스크립트는 **동작을 그대로 두고 만드는 방식만 바꾼다.** 공개 API·콜백·내비 순서·`WantsVisible`은 그대로 둔다.
   Figma 좌표 주석은 프리팹 쪽 설명이나 커밋 메시지로 옮긴다.
4. 실행 중에 프리팹을 `Instantiate`하는 코드를 새로 만들면 `Tests/PlayMode/CreationOwnershipTests` 목록에 생성 이유를 추가한다.
5. 렌더 순서(`sortingOrder`)나 새 레이어·태그가 필요하면 `CLAUDE.md` 예약표에 먼저 적는다.
6. 화면 하나가 눈에 보이게 동작하면 그 단위로 커밋한다. 커밋 전에 `PROGRESS.md`를 갱신한다.
7. `git status`와 `git diff --stat`으로 **의도하지 않은 파일(특히 `.meta`, 다른 씬, `ProjectSettings`)이 없는지** 확인한다.

## 6. 검증 단계

| 항목 | 방법 | 결과 표기 |
|---|---|---|
| 컴파일 | 배치모드 실행 로그에 컴파일 에러 없음 | 통과 / 실패 / 미검증 |
| PlayMode 테스트 **전체** | `CLAUDE.md` "테스트" 절의 명령(결과 파일명은 `*results*.xml`, `*.log`) | 통과 수 / 전체 수 |
| 씬 구성 | `-executeMethod ChronoSavior.Editor.SceneSetupValidator.RunBatch` | 통과 / 실패 / 미검증 |
| 겉모습 | 변경 전후 같은 상태로 캡처해서 비교(Figma와 비교) | 일치 / 차이 목록 / 미검증 |
| 동작 | 보존 목록(3-3)의 콜백·키보드·ESC·켜고 끄기를 하나씩 확인 | 항목별 표기 |
| 참조 | 씬·프리팹에 Missing Script·Missing Reference가 없음(에디터 콘솔·인스펙터) | 확인 / 미검증 |

- 에디터가 잠겨 있거나 배치를 돌릴 수 없으면 해당 항목을 **"미검증"**으로 적고, 커밋 메시지와 `PROGRESS.md`에도 그 사실을 남긴다.
- 최종 보고는 "검증됨 / 미검증 / 실패" 세 묶음으로 나눠서 한다.

## 7. 멈추고 사용자에게 물어야 하는 경우

- 작업 트리가 깨끗하지 않거나, UI 파일을 건드리는 미병합 브랜치가 있을 때
- 기능을 없애거나 동작·겉모습이 달라지는 변경이 필요할 때(원본·Figma와의 차이 포함)
- `.meta`·GUID·씬 참조에 예상하지 못한 변화가 생겼을 때
- UI 리팩터링이 지금 `HANDOFF.md` 범위 밖일 때
- 팀원 담당 파일을 건드려야 할 때

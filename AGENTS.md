# AGENTS.md — Codex용 작업 지침

이 저장소의 기본 수칙은 **`CLAUDE.md`** 에 있다. Codex도 작업 전에 반드시 `CLAUDE.md`를 먼저 읽고 그대로 따른다.
이 파일은 Codex가 Claude와 같이 일할 때 추가로 지킬 것만 적는다.

## CLAUDE.md 핵심 규칙 요약 (읽기가 막힐 때 대비)

- **원본이 기준선이다.** `reference/project_test.html`(원본 게임)과 같은 동작이 기본. 원본과 다른 건 팀 회의에서 정한 개선뿐이고,
  그 밖의 차이를 발견하면 사용자에게 알린다. 임의 단순화 금지.
- **HANDOFF.md 범위 밖 시스템은 손대지 않는다.** 현재 상태는 `PROGRESS.md`, 범위·확정 수치는 `HANDOFF.md`.
- 의존 계층(asmdef): Core ← World·Combat ← Characters·Enemies ← Systems ← UI. 아래 층이 위 층을 참조하면 안 된다.
- 기능 단위로 커밋하고, 커밋 전에 `PROGRESS.md`를 갱신한다(이건 Claude 몫).

## 역할

- 이 저장소에서 Codex는 **설계·리뷰 담당**이다. 구현·Unity 테스트 실행·커밋은 Claude가 맡는다.
- Claude가 `codex exec`로 맡기는 일은 주로 두 가지다.
  1. **설계 제안** — 예: 오브젝트 풀링 구조, 물리 질의 공통화, 캔버스 분리 방안
  2. **리뷰** — Claude가 만든 변경(diff)의 버그·성능·원본과의 동작 차이 지적
- 답은 **한국어**로, 파일 경로와 줄 번호(`Assets/Scripts/...cs:123`)를 붙여 구체적으로 쓴다.

## 하지 말 것

- **커밋·push·브랜치 생성·병합 금지.** 코드 수정이 필요하면 수정안(diff)을 답으로 제시만 한다.
- **팀원 담당 파일은 고치자는 제안도 따로 표시한다.** `Assets/Scripts/Characters/Blade*`, `Druid*` 등은
  `docs/worksplit.md` 기준 팀원 담당이다. 바꿔야 하면 "팀원 확인 필요"라고 적는다.
- `reference/project_test.html`(원본 게임)은 읽기만 한다. 수정 금지.
- `BuildPartAScene.Build` 같은 씬을 다시 만드는 에디터 도구는 실행하지 않는다.
- Unity 에디터·배치 테스트를 직접 돌리지 않는다(같은 프로젝트를 동시에 열면 Library가 꼬인다).

## 원본 기준선

- 동작은 원본(`reference/project_test.html`)과 같아야 한다. 최적화 때문에 게임 동작·수치가 달라지면 안 된다.
- 원본과 달라지는 제안이라면 반드시 "동작 변경" 이라고 명시한다.

## 같이 쓰는 문서

- 최적화 작업은 `HANDOFF.md`에 측정 결과·할 일·담당·결과를 적는다.
  Codex는 읽고 제안만 하고, 문서 갱신은 Claude가 한다.

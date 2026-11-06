# 진행 상황

> 새 세션은 이 파일부터 읽고 이어서 작업할 것. 작업할 때마다 여기를 갱신한다.


## 지금 상태 (2026-11-05)

> **최종 목표가 바뀌었다(2026-09-30)**: 원본을 그대로 옮기는 게 아니라, **원본을 기준으로 팀 회의에서 정한 개선을 반영한다.**
> 기본값은 여전히 원본 동작이고, 원본과 다른 점을 발견하면 사용자에게 짚는다(`CLAUDE.md` 작업 규칙 첫 항목).

**지금은 구조 정리 스프린트(2026-11-04~, [`HANDOFF.md`](HANDOFF.md))** — 성능이 아니라 '처음부터 깔끔한 코드·씬 구조'(교수님 리뷰 기준).
게임 이름 Chrono Savior(네임스페이스 `ChronoSavior.*`), 씬 분할 1차 ✅(상주 `00_Persistent` + 전투 `20_Combat`), **2차 단계 0(세이브 교체 경계)·1(`CombatDirector`)·2a(캐릭터·몬스터 그림을 데이터로)·2b(연결 규칙·`WorldView`)·3(`10_Lobby` 분리 + 씬 전환 — 씬 3개, 월드는 `GameFlow`가 불러오고 내림)·4(마감 — 전환 중 화면 입력 잠금·로비 timeScale 0 유지·상태 소유권 검사·정리·캡처 도구) ✅ — 씬 분할 끝** —
**정리 순서 2(생성 책임) ✅**(2026-11-06 — 부품은 씬에, 몹 종류·전투 물체는 프리팹, 생성 담당 목록 `CreationOwnershipTests`). **정리 순서 3.5(에셋 교체 경계) ✅**(2026-11-06 — `docs/asset-swap-points.md`·`docs/pixel-standard.md`). PlayMode 473/473. 다음은 정리 순서 4(상태 소유권) → 5(책임별 분리), VFX가 오면 도트 재작업(`HANDOFF.md` 순서 5). 아래 "게임 한 바퀴"는 10/24까지의 기능 현황이다.

**게임 한 바퀴가 전부 돈다.** 전투 코어 · 마법사 5차 스킬트리 · **메카닉 5층 스킬트리(신규)** · 섬영 0차 ·
몬스터 7종 · 성소 · 보스 · 런 사이클 · 로비 7탭 · 세이브 · 전투 배수 체인 · 경험치 구슬 · 지역 진행 ·
시간 회귀 · 아이템/가챠 · 업적 · 전투 HUD 스킬 슬롯까지 `main`에 병합·검증 완료(`main` 기준 PlayMode 256/256).
**2026-09-30 병합: `feature/gunner-tiers`** — 메카닉 스킬트리(로비에서 습득까지) + 플레이어 크기 0.5·공격 높이 발 기준
+ 캐릭터 선택이 프로필과 어긋나던 버그·보스 격파가 다음 사냥에 새던 버그 수정 + 메카닉 테스트용 에디터 메뉴
+ 전투 HUD 스킬 슬롯(Z·X·C 쿨다운, Figma 그림) + 문서를 노션 일정표 기준으로 정리(아래 로그 09-28 (5)~09-30 (2)).
**2026-10-14 병합 → `main`**: 섬영 집중·칼날폭풍 1~5층 + 캐릭터·고블린·박쥐 아트(팀원) + 캐릭터별 레벨·경험치·SP·
SP +1 테스트 버튼·마법사 버그 5건·경험치 넘침 멈춤·대붕괴 폭발 고리(나) + 로비 섬영 전문화 탭·섬영 업적(나, 병합 때). 로그 10-14 (1).
**2026-10-24 병합 → `main`**: Figma 1스테이지 화면 1단계 — C안 카메라(960×540px를 정수배로, Y도 따라감) + 배경·땅·발판·캐릭터·
보스 포탈(1.5배) Figma 원본 그림 + 타격감 원본 값(공격별 히트스톱·피격 플래시 버그·화면 흔들림·붉어짐·무적 깜빡임) +
실제 맵 발판 검사·캐릭터 발 높이(나). `main` 기준 PlayMode 357/357. 로그 10-24 (1)~(3).
(드루이드는 기획에서 제외되어 앞으로도 안 만든다.)

**성장 고리가 닫혔다**: 사냥 → 지역 100마리 토벌 → 보스 → 다음 지역 → (지수 입장료 벽) →
**시간 회귀** → 시간의 파편 → **뽑기** → 아이템(영구) → 더 깊은 지역.

`Assets/Scenes/00_Persistent.unity`(또는 `20_Combat`)를 열고 Play하면 **타이틀**부터 시작한다 — 에디터 Play는 어느 씬을 열어 두든
`00_Persistent`부터(`Editor/PlayFromPersistent`). 타이틀 → 새 원정(세이브 삭제)/불러오기 → 로비.

- **로비 탭 7개** — 캐릭터 선택 / 스테이지 / 강화 / 전문화 / 시간 회귀 / 기원 / 업적.
- **사냥 중** — ←/→ 이동, **C** 점프(원본은 C+Space지만 **의도적 편차로 C만**), **↓** 발판 관통 낙하,
  **Z** 공격, **X** 전문화 스킬(빌드를 1층 이상 찍어야 나간다). **ESC**로 일시정지 → 로비.
- 90초가 끝나거나 죽으면 결과 화면 → 로비. **진행은 저장된다**(타이틀 "새 원정"이 지운다).
- **전투 배수가 붙는다** — 콤보(피해 +1.2%/콤보) · 살기(+0.8%/처치) · 연쇄 처치 골드 보너스.
  **내 레벨이 몹보다 낮으면 레벨당 -5%**(하한 25%, 보스는 50%)라 상위 지역은 강화만으로 안 뚫린다.

몹은 3.6초마다 웨이브 스폰되고, **지역별 해금표**(원본 `rollSpawnType`)에 따라 종류가 정해진다 —
1지역은 박쥐·고블린만, 지역이 오르면 멧돼지→해골 궁수→슬라임이 차례로 추가되고 2지역부터 오우거도 섞인다.
8% 확률로 **엘리트**(체력×4·피해×1.5·크기×1.35·보상×5)가 섞인다.

### 다음 할 일

**스펙: [`HANDOFF.md`](HANDOFF.md) / 일정표·분업·계약: [`docs/worksplit.md`](docs/worksplit.md) / 원본 대조: [`docs/original-parity.md`](docs/original-parity.md)**

**순서·날짜는 노션 일정표(2026-09-23)가 정본이다** — 옮겨 적은 표는 [`docs/worksplit.md`](docs/worksplit.md) 1절.
예전 "단계 1/2/3"(9/9 회의)은 더 쓰지 않는다(2026-09-30 정리 — 일정표의 "2단계"와 이름이 겹쳐 헷갈렸다).
옛 단계 1(모든 캐릭터 0차 + 모든 몬스터)은 2026-09-28에 끝났다 — 섬영 0차 병합, 드루이드는 기획 제외.

**지금: 구조 정리 스프린트** — 씬 분할·생성 책임 끝, 다음(정리 순서 4 전역 상태·5 큰 클래스)은 사용자와 정한다([`HANDOFF.md`](HANDOFF.md) "씬 분할 2차", 단계마다 Codex 구현 리뷰 → 사용자 확인 후 `main`).
아래 1~8은 **이전(10/14 프로토타입·10/24 Figma 1단계까지)에 정한 기능 작업** — 구조 정리가 끝나면 이어 간다.

**(이전) 10/14 프로토타입 마감일** — ① 직업별 스킬은 마법사·메카닉·섬영 5층 전부 `main`(10/14).

1. **[팀원] 섬영 tier1+ (집중·칼날폭풍) — 완료.** 단계 2 몫. **1~5층 전부 PlayMode 264/264
   검증 완료(2026-09-30, `feature/blade-tier1` 브랜치) — ✅ 2026-10-14 `main` 병합(아래 4번
   참고). 소유권 밖 예외 2건은 팀장 확인 필요(아래 "확인 필요" 참고).** 254(1~4층) + 신규 10(5층),
   회귀 없음. 집중은 2층부터 가속 배율이 한 번 더 곱해지고(`accelMul2`) 3층부터 궤적 지속·피해·반경이
   각각 `t3Life`(1.45)·`t3Dmg`(1.8)·`t3R`(1.3)배로 커지고, 4층부터는 시전 즉시 가속(근사 — 아래 참고) +
   궤적이 끝날 때 남은 궤적 위 적을 한 번 더 베는 "궤적 파열" 폭발이 붙고, **5층부터는 궤적이 아직
   안 끝났어도 재시전하면 새 궤적을 깔기 전에 남은 궤적을 먼저 찢고, 최고 속도로 적을 스칠 때마다
   (적당 0.5초 간격) 집중 쿨타임을 0.55초씩 깎는다("패스 틱", 갈래 공통 아님 — 집중 전용).** 칼날폭풍은
   2층부터 착지 시 광역 낙하 충격(`bladeStormLand`), 3층부터 맵 가장자리에서 종료 대신 반대 방향으로
   반사, 4층부터 스치는 적을 붙잡아 끌고 다니다(최대 6명) 종료 시 낙하시켜 착지 피해를 주고, **5층부터는
   활공 중 점프키로 종료 대신 위로 튀어 오르며("곡예비행") 칼날폭풍 쿨타임을 즉시 돌려준다** — 이걸로
   섬영 두 갈래 모두 1~5차 스킬트리가 기능적으로는 끝났다("차례대로" 진행 완료, 로비 갈래 선택 UI는
   별개 — 아래 참고). 최고 속도 충돌 무적(`bladeCrashImmune`, "집중 2층 또는 칼날폭풍 3층")도 칼날폭풍
   쪽 절반까지 실제로 걸린다(칼날폭풍 중엔 물리 속도를 일부러 0으로 비워서 `ComputeSpeedRatio()`를
   못 쓰길래, `UpdateStorm`이 매 프레임 따로 계산해 둔 속도 비율을 `Update()`가 다음 프레임에 읽는
   식으로 연결). **4층 "즉시 최고 속도" 가속은 근사다**: `CharacterMover2D.inertialVx`가
   private이라 속도를 못 박지 못해서, 대신 다음 물리 스텝 한 번만 `AccelMultiplier`를 2000배로
   올려 사실상 즉시 최고 속도에 도달하게 했다 — 단, 이동 방향키를 누른 채로 캐스팅할 때만 걸리고
   무입력/반대 입력 중엔 원본처럼은 안 걸린다(`BladeCombat.cs`의 `UpdateFocus` 주석 참고). **로비 섬영
   전문화 탭은 ✅ 2026-10-14(나, 병합 때 추가)** — 섬영 칸의 SP로 찍는다.
2. **[나] 메카닉 5층 — 끝났다(2026-09-30 `main` 병합)** — 레이저 빔·드론, 설치기·링크·내부 장판, 충전
   스택·부품, X 쿨다운까지 원본대로. 빌드는 `PlayerProfile.gunnerBranch/gunnerTier`에 저장되고 로비 전문화 탭에서
   SP로 배운다. HUD 스킬 슬롯·충전/부품 표시도 붙었다(09-29 (1)). **남은 것**: "부품 필요" 같은 X 실패 안내
   문구(원본 `showSkillMsg`). 수동 테스트: 에디터 메뉴 **`ChronoSavior/메카닉 테스트/…`**(Play 중).
3. ✅ **[나] 전문화 탭 "SP +1" 테스트 버튼** — 에디터에서만 보인다. 누르면 지금 캐릭터 레벨 +1(= SP +1). ✅ 10/14 `main`.
4. ✅ **섬영 + 내 브랜치 `main` 병합(2026-10-14)** — 팀원이 10/14 아침 섬영 1~5층·아트를 `main`에 올렸고, 내가
   `fix/mage-original-behavior`를 합쳤다(로그 10-14 (1)).
5. ✅ **[나] 캐릭터별 레벨·경험치·SP — 원본대로**(09-30, 10/14 `main`). 골드·골드 강화·파편·유물은 공용.
   섬영도 섬영 칸을 쓴다(10/14 병합 때 `bladeSpUsed`를 섬영 칸으로 옮김).
6. **[팀 전체] ②·③ 주에 무엇을 할지** — 몬스터·전투·플랫폼은 원본 이식본이 이미 있다(아래 "확인 필요").
7. **[나] 원본 대조로 정한 고칠 범위**(10/03 전 직업 대조, 사용자 확정 — 차이 목록은 `docs/original-parity.md` 8-1):
   - ✅ **묶음 1 마법사 동작 버그 5건**(10/03, `fix/mage-original-behavior`) — 로그 10-03 (1).
   - ✅ **묶음 2 맞을 때 멈춤 규칙**(10/24) — 피해 함수에 "멈춤 길이" 칸 + 마법사·메카닉·처치·성소·연쇄 처치를 원본 값으로
     (로그 10-24 (2)). **남은 것**: 섬영(팀원 파일 `BladeCombat` — 원본 값은 `docs/original-parity.md` 8-1,
     팀원에게 부탁할지 내가 고칠지 사용자 결정 대기). 곁가지로 균열의 각인이 틱 피해에도 걸리게.
   - **묶음 3 스킬 안내 문구**(원본 `showSkillMsg`) — 짧게 뜨는 안내 칸 + 마법사·메카닉 문구. 섬영은 계약만.
   - **묶음 4 HUD에서 빠진 것** — 보스 체력바(② 몬스터 주간에 추천), 결과 화면 지역 토벌 진행도, 버프 줄 조건부 표시.
   - **묶음 5 아직 없는 원본 게임플레이** — 연쇄 처치 슬로모(③), 조건부 특수 아이템 11종(⑦ 유물 주간), 섬영 1층~(팀원).
   - **묶음 6 그림·연출** — **시기 미정(사용자 결정)**. 화염 폭발·불길·~~순간이동 잔상~~(✅ 10/24 잔상)·중력점 고리·대붕괴, 레이저 3겹·
     드론·설치기 모양·연결선/장판, 회전베기, 공통(피해 숫자·불티·효과음 — 화면 흔들림은 ✅ 10/24).
   - 추천 순서: 묶음 1 → 4(보스 체력바) → 3.
8. **[나] Figma 화면 적용**(사용자 지시 10/23 "Figma에 나온 대로", 게임 이름 CHRONO SAVIOR) — ✅ 1단계(카메라·배경·땅·발판·
   캐릭터·포탈) 10/24 `main`. 다음: 2단계 HUD → 3단계 몹 11종 그림 → 나머지 창(타이틀·일시정지·스테이지 선택·설정 등).
   **10/24 Figma 13페이지 전부 확인**(정리 그림 `claude작업물/chrono_preview/Figma_UI_전체.png`) — 640×360 도트 8개(HUD·타이틀·
   일시정지·공용 팝업·회귀 결과·각성창·스테이지 선택·유물 개방), 뼈대만 5개(캐릭터 선택·원정 강화·원정 기록·전투 패배·로비).
   사용자 "다 넣을거야". 640판으로 정해진 것: 일시정지는 정보 없이 아이콘 3개, 스테이지 선택엔 보스 도전 버튼이 없다(포탈 유지),
   회귀 결과 라벨은 "최고 스테이지·최고 레벨". 640판 HUD엔 경험치 바가 없다(숨긴 옛 1920판엔 맨 아래 경험치 바·체력 숫자) —
   사용자 "체력·경험치도 넣어야" → 자리는 답 대기. 그 밖에 답 대기: 폰트 파일, 로비(걸어 다니는 로비로?), 스테이지 ↻ 버튼 뜻.

## 작업 재개 방법 (인수인계)

```bash
git clone https://github.com/kimJ02/chrono-savior && cd chrono-savior
git checkout main            # 2026-11-05: 구조 정리 진행 중 — 끝난 단계는 전부 main
```

읽는 순서: `CLAUDE.md`(작업 규칙) → 이 파일 → `HANDOFF.md`(지금 스프린트) → `docs/worksplit.md`(일정표·분업).

**배치 검증 명령** (Unity 에디터를 반드시 닫고 실행 — 열려 있으면 프로젝트가 잠겨 크래시한다):

```bash
# 컴파일 + 씬 구성 검사(씬은 고치지 않는다 — 문제가 있으면 종료 코드 1)
"/c/Program Files/Unity/Hub/Editor/6000.0.75f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:\dev\chrono-savior" -executeMethod ChronoSavior.Editor.SceneSetupValidator.RunBatch \
  -logFile "C:\dev\chrono-savior\build.log"
```

```bash
# PlayMode 테스트 전체
"/c/Program Files/Unity/Hub/Editor/6000.0.75f1/Editor/Unity.exe" -batchmode -nographics \
  -projectPath "C:\dev\chrono-savior" -runTests -testPlatform PlayMode \
  -testResults "C:\dev\chrono-savior\test_results.xml" -logFile "C:\dev\chrono-savior\test.log"
```

`-executeMethod`는 **네임스페이스 전체 경로**가 필요하다(`ChronoSavior.Editor.SceneSetupValidator.RunBatch`).
결과 파일명은 `.gitignore`에 걸리는 패턴(`*.log`, `*results*.xml`)을 쓸 것.

## 확인 필요 / 막힌 것

- **[팀원, 소유권 밖 수정 — 10/14 병합됨] `Assets/Editor/BuildEnemyData.cs`에 `SpriteEnabledTypes`
  조건 추가(2026-10-07) — 팀장 확인 필요.** 고블린·박쥐만 정식 아트(사용자 제공)를 쓰도록 1줄
  조건부 변경, 나머지 5종은 그대로 도형. `Enemies/`는 팀장 담당 영역이라 동작엔 문제없지만 병합 전
  확인 바람. 자세한 경위·새로 만든 `ApplyCharacterArt.cs`(캐릭터 그림 적용 메뉴)는 아래 로그
  10-07 (1) 참고.

- **[팀원, 소유권 밖 수정 — 10/14 병합됨] `Core/PlayerProfile.cs`에 섬영 전용 필드 추가(2026-09-29,
  `feature/blade-tier1`) — 팀장 확인 필요.** `docs/worksplit.md`/`HANDOFF.md`는 "캐릭터별 SP/
  branch/tier 일반화는 단계2에서 메카닉 스킬트리와 같이 처리한다"고 정해 뒀고 `Core/`는 팀원에게
  "읽기만"인데, 사용자 확인("내가 PlayerProfile에 섬영 전용 필드 추가")으로 최소 침습(마법사 필드
  안 건드림, 섬영 전용 SP 풀 분리)으로 먼저 추가했다. 팀장이 실제 일반화 작업을 할 때 이 필드들이
  그 구조로 흡수/정리될 걸 전제로 한 임시 형태 — 병합 전에 팀장이 봐 줘야 한다. 자세한 경위는 아래
  로그 09-29 (2) 참고.
  - **2026-10-07 추가**: `feature/gunner-tiers`가 같은 파일에 거의 같은 구조(`GunnerBranch`/
    `gunnerBranch`/`gunnerTier`)를 먼저 `main`에 넣어서, 섬영 브랜치를 `main`에 합치는 과정에서
    이 파일이 실제로 충돌했다 — 거너·블레이드 둘 다 살려서 풀었다(기계적 병합만, 설계 재검토는
    아님). 이 과정에서 드러난 것: **거너는 SP를 `spUsed`로 마법사와 공유하는데 섬영은
    `bladeSpUsed`로 따로 쓴다** — 서로 다른 날짜에 독립적으로 추가되며 정책이 갈렸다. 팀장이
    캐릭터별 `PlayerProfile` 일반화를 할 때 이 불일치도 같이 정리해야 한다. 병합 자체(충돌 해결)와
    이 설계 리뷰(팀장 확인)는 별개다 — 전자는 끝났고 후자는 여전히 열려 있다.
- **[팀원, 소유권 밖 수정 — 10/14 병합됨] `Core/IGrabExempt.cs`(신규) + `Enemies/Boss.cs`·
  `Enemies/Shrine.cs`에 한 줄씩(2026-09-30, `feature/blade-tier1`) — 팀장 확인 필요.** 섬영
  칼날폭풍 4층 "강제 연행"(붙잡기)이 보스·성소는 못 붙잡아야 하는데(원본 `e.boss || e.shrine`
  제외, project_test.html:2694) `Characters`는 asmdef 계층 규칙상 `Enemies`의 `Boss`/`Shrine`
  타입을 직접 볼 수 없다. 3가지 방안(Core 마커 인터페이스 / 제외 없이 진행 / 칼날폭풍 4층 붙잡기
  자체를 보류)을 물어봤고 **"Core에 마커 인터페이스 추가(추천)"**로 확정 — `Core/IGrabExempt.cs`에
  멤버 없는 빈 인터페이스 하나를 추가하고, `Boss`/`Shrine`가 그걸 구현하도록 클래스 선언에 한 줄씩만
  보탰다(`Boss`/`Shrine`의 기존 로직·필드는 전혀 안 건드림). `IDamageable`/`ISpawnProtectable`과
  같은 자리·같은 이유(Core는 두 도메인이 공통으로 참조하는 계층)다. 자세한 경위는 아래
  로그 09-30 (4) 참고. **2026-10-07 추가**: `main`에서 이 두 파일은 건드려진 적이 없어 충돌 없이
  그대로 합쳐졌다 — 설계 리뷰(팀장 확인)만 남았다.
- **일정표와 저장소가 안 맞는다 — 팀 확인 필요 (2026-09-30)**
  - **② 몬스터(10/3~10/8) · ③ 전투+플랫폼(10/9~10/14)**: 원본 이식본이 이미 있다(몹 7종·엘리트·보스·성소·스폰표,
    전투 코어·넉백·무적·히트스톱·배수 체인, 발판·아래키 낙하·보스 포탈). 이 두 주에 할 일 — 확인·보완만 할지,
    팀 설계로 다시 만들지 — 을 정해야 한다.
  - **⑥~⑧ 스탯+강화 · 유물 · 가챠**도 원본 이식본이 있다. Figma(시공 유물 25종, 확률 노말 70·레어 27·에픽 2·
    레전드 1%)는 원본(아이템 35종, 천장 20)과 다르다 — 원본을 유지할지 팀 설계로 바꿀지.
  - 옛 단계 3(밸런스, 기획자 상윤)은 일정표에 자리가 없다. 9/28에 권한 "10/12~14 통합·버그 수정" 행은 노션에
    들어갔는지 모른다.
  - 팀원 파일 두 곳이 `HANDOFF.md`를 가리킨다(`Characters/BladeCombat.cs:16`, `Tests/PlayMode/Characters/BladeCombatTests.cs:153`).
    둘 다 0차 정의·수치 얘기인데 그 내용은 `docs/worksplit.md` 1·4절에 그대로 있고, 옛 스펙은
    `docs/sprints/05-all-characters-tier0.md`로 갔다 — 팀원이 다음에 그 파일을 고칠 때 경로만 바꾸면 된다.

- **메카닉 스킬트리 — 2026-09-30 `main` 병합. 남은 편차·주의** (2026-09-28)
  - ~~SP를 마법사와 같이 쓴다~~ → ✅ 09-30 캐릭터별 레벨·경험치·SP(원본대로, `feature/char-progress`).
  - **총알 적중 히트스톱이 원본과 다르다(0차부터 있던 편차)**: 원본 총알·드론 탄은 `noHitstop`인데,
    `IDamageable`에 "넉백은 있고 히트스톱은 없는" 경로가 없어 `TakeDamageWithKnockback`이 적중마다
    0.045초 히트스톱을 건다. 빔·설치기·장판 틱은 `TakeTickDamage`라 원본대로 히트스톱이 없다.
    고치려면 `Core/IDamageable`+`Enemies/EnemyHealth`를 바꿔야 해서 이번엔 손대지 않았다.
  - **표시는 최소한만**: 빔·링크는 얇은 막대, 드론·설치기는 작은 원. 장판 내부 채우기·명중 이펙트는 없다.
  - `GunnerSpecConfig`의 공개 함수는 `OriginalFidelityTests`의 호출부 검사 목록에 안 넣었다(공용 테스트
    파일이라 병렬 작업 중 충돌 방지) — 대신 `GunnerSkillTreeTests`가 수치가 아니라 **실제 적중**으로 확인한다.

- **HUD 스킬 슬롯 (2026-09-29) — 화면은 배치로 못 본다(IMGUI), 에디터에서 눈으로 확인할 것**
  - **섬영 칸엔 쿨다운이 안 뜬다**: `BladeCombat`은 팀원 파일이고 1차(집중·칼날폭풍)를 작업 중이라 손대지 않았다.
    팀원이 `ISkillSlotSource`만 구현하면 바로 뜬다(`docs/worksplit.md` 공용 계약 표 참고).
  - **아이콘은 Figma 기본 3종(칼·마법진·화살표)을 모든 캐릭터가 같이 쓴다** — 원본은 캐릭터·갈래마다 이모지가
    다르다(`WEAPONS[무기].icon`, `BRANCH_ICONS`). 캐릭터별 그림이 나오면 `UiTextures`에 칸을 늘리면 된다.
  - **원본과 다르게 둔 것(Figma 그림 때문)**: 키 글자(Z·X·C)를 칸 **밑**에 뒀다(원본은 칸 안 아래). 현재 무기
    칸의 하늘색 테두리(`.slot.cur`)는 안 그렸다 — Figma 틀은 세 칸이 똑같다.
  - 부품 0개(`noStack`)일 때 원본은 아이콘 **글자색**을 회색으로 바꾸는데(이모지라 실제로는 거의 안 바뀐다),
    우리는 쿨다운 때처럼 아이콘을 흐리게 한다.
- **눈으로 봐야만 잡히는 것들이 계속 나온다 — 이 항목을 섣불리 지우지 말 것.**
  지금까지 원본과 다른 점은 **거의 전부 사용자가 직접 플레이해보고 발견**했다(스폰 위치·이동/점프
  디테일·공격 판정·카메라·물리 밀어내기·체력 표시·R키 부활·**로비 가독성**·**성소 크기**).
  자동 테스트로는 못 잡는 종류다.
- **배치 모드는 `-nographics`라 IMGUI가 렌더링되지 않는다.** UI 작업(로비·결과·HUD 테마, 2026-09-10)은
  **컴파일과 테스트만 확인했고 화면은 못 봤다.** 특히 로비 탭 7개가 가로로 넘치는지, HUD 콤보가
  우상단에서 잘리는지는 계산으로 확인이 안 된다.
- **원격에 stale 브랜치 4개** — `feature/kill-rewards` · `monster-combat` · `player-field` ·
  `run-cycle`. **전부 `main` 대비 고유 커밋 0개**(2026-09-12 재확인). 네 개 다 **팀원이 올린
  브랜치**라, 로컬에 같은 이름으로 작업 중일 수 있어 임의로 지우지 않았다 —
  팀원 확인 후 한 줄로 정리:
  `git push origin --delete feature/kill-rewards feature/monster-combat feature/player-field feature/run-cycle`
  *(2026-09-12: 내가 만든 `feature/run-cycle-impl`은 삭제 완료. 브랜치가 많아 보기 힘들다는
  팀원 의견에 따라 정리했다 — 내용은 `main`에 전부 들어가 있어 잃은 것은 없다.)*
- **캐릭터 컨트롤러는 자체 구현이다.** 초기 스펙은 에셋스토어 컨트롤러 재사용을 전제했지만 배치 환경에서
  인증 없이 받을 수 없어 `Characters/CharacterMover2D.cs`를 직접 짰다. 나중에 실제 에셋으로 바꾸기로 하면
  이 파일 하나만 교체하면 되도록 다른 스크립트와 결합시키지 않았다.
- **섬영 0차 + 소유권 밖 파일 3건(`PlayerHealth.Heal`·`Combat/MageSkillEffects.cs` 중력장 버그 수정·
  `Assets/Editor/BuildPartAScene.cs` 씬 부착) — 팀장 확인 거쳐 `main` 병합 완료(2026-09-28).**
  세 파일 다 `docs/worksplit.md`상 팀원 담당 목록 밖이라 병합 전 팀장 확인을 거쳤다(사용자가
  직접 확인하고 옴). 병합 후 배치로 PlayMode 다시 돌려 **217/217 유지 확인됨.** 자세한 경위는
  아래 로그 09-28 (1)~(3) 참고.
- **몹 여러 마리가 연달아 죽을 때 화면이 잠깐 멎는 게 "렉"처럼 느껴진다(사용자 플레이 중 발견,
  2026-09-28) — 버그 아니고 의도된 히트스톱.** `RunController.Hitstop()`이 처치마다 0.085초
  `Time.timeScale=0`으로 멈추는데 `Mathf.Max`로 겹치는 방식이라(원본 `state.freeze=Math.max(...)`
  그대로), 완전히 동시가 아니라 몇 프레임에 걸쳐 연달아 죽으면 죽을 때마다 갱신되며 체감 정지
  시간이 길어진다. **당장은 조정하지 않기로 함**(사용자 결정) — 나중에 필요하면 "짧은 시간 안의
  연쇄 처치는 한 번만 걸리게" 하는 식으로 손볼 여지는 있다(원본에 없는 변경이라 디자인 판단 필요).

## 로그 (최신이 위)

- **2026-11-06 (56)** — **[나] 4-2: 쓰이지 않는 `RunProgress` 삭제 (`feature/state-ownership`, PlayMode 473/473).** `TotalKills`는 세기만 하고 읽는 곳이 없었고 `RegionLv`는 `RunState.Region`의 별칭이었다 — `EnemySpawner`가 `RunState.Region`을 직접 읽는다(동작 같음). 테스트에서만 쓰던 `DifficultyScalingConfig.KillsPerRegionLevel`도 삭제.
  위임만 확인하던 `RegionLv_FollowsRunRegion_NotKillCount` 테스트 삭제(474→473), 처치 수 확인은 `RunState.Kills`로. 테스트 클래스 이름 `RunProgressAndRewardsTests` → `RegionScalingAndRewardsTests`.
- **2026-11-06 (55)** — **[나] 4-1: 결과 화면 '최대 콤보'를 원정마다 (`feature/state-ownership`, PlayMode 474/474).** `CombatModifiers.MaxCombo`를 `ResetForRun`이 지운다(예전엔 테스트용 `Reset`만 지워 결과 화면에 그 세션 전체 최고가 보였다).
  Figma가 추가한 '원정 통계'의 의미를 바로잡은 것(원본 결과 화면엔 콤보 표시 없음). 업적용 평생 최고는 프로필 `stats.maxCombo` 그대로. `CombatModifierTests.MaxCombo_IsPerExpedition_ButProfileKeepsTheLifetimeBest`.
- **2026-11-06 (54)** — **[나] 정리 순서 4(상태 소유권) 시작 — 4-0 상태 표 (`feature/state-ownership`, 문서만).** 실행 중 바뀌는 정적 상태를 전수 조사해 `docs/state-ownership.md`(분류·static·소유자·변경 요청자·수명·읽기·검증).
  Codex 합의 계획(`HANDOFF.md`): 상태가 자기 변경을 책임지고 사건 쪽이 의미 있는 메서드를 부른다(한 관리자에 몰지 않음). 찾은 것: 결과 화면 '최대 콤보'가 원정마다 안 지워짐, `RunProgress.TotalKills` 읽는 곳 없음, `FieldBounds` public 가변 필드, 프로필 직접 대입 몇 곳.
- **2026-11-06 (53)** — **[나] 정리 순서 3.5(에셋 교체 경계) 완료 — Codex xhigh 종합 리뷰 '통과 — 병합 가능'**(PlayMode 473/473, 씬 검증 통과, 기준 캡처 동일, 진짜 세이브 해시 그대로). `HANDOFF.md` 3.5 ✅, `CLAUDE.md`·지금 상태 갱신.
- **2026-11-06 (52)** — **[나] 3.5 재확인(xhigh) 반영 (`feature/asset-seams`, PlayMode 473/473).** 보고서의 임시 도형 수를 별도 표시로(중력 착탄 설명 행이 섞이던 것), 테스트용 섬영 베기 프레임은 **만든 그림**으로(특정 파일에 기대지 않게 — 옛 폴더 import 테스트 1개 삭제),
  씬에 연결된 프레임은 `GameBootTests`가 '비지 않음·이름순·Point 필터·같은 크기·PPU' 규칙으로 확인. `pixel-standard.md` 임시 도형 몹 줄 출처를 `visualHeight`로.
- **2026-11-06 (51)** — **[나] 3.5 종합 리뷰(Codex xhigh) 반영 (`feature/asset-seams`, PlayMode 474/474, 씬 검증 통과).** 막던 2건: `docs/pixel-standard.md`가 3.5-d 이전 출처를 가리키던 것 → 지금 출처(`StageArtSet`·`heights`·`visualHeight`·`bossSize`)로,
  도트 배율 보고서가 옛 파일 경로로 섬영 베기 프레임을 읽던 것 → Player에 **실제로 연결된** 프레임을 읽고, 경고 합계를 표와 같게(임시 도형 수 따로). 같이: 기준점(피벗)은 가운데 규칙을 문서에, 포탈 배율 0 이하 검증,
  섬영 베기 프레임은 '지금과 같은 크기·PPU일 때만 코드 수정 없이'라고 표에 명시, 씬 Player의 프레임이 이름순이고 테스트 프레임과 같은지·효과 머티리얼 연결을 `GameBootTests`가 확인.
- **2026-11-06 (50)** — **[나] 3.5-g: UI 그림 목록 + 교체 방식 결정 (`feature/asset-seams`, 문서만).** Codex 합의 — **경로 키 유지**(`PxArt.Get("키")` 한곳이라 같은 키로 덮어쓰면 코드 수정 없이 교체, UiArtSet은 원본이 둘이 되고 64곳을 고쳐 과함),
  화면 배치(Figma 좌표 `Build()`)는 그대로. `docs/asset-swap-points.md` UI 절: 파일 54개 전부 사용(변수로 고르는 아이콘 포함)·빠진 파일 0·9-슬라이스 테두리·글꼴, 제작 상태 칸은 팀원 확인 필요. **3.5 a~g 끝.**
- **2026-11-06 (49)** — **[나] 3.5-f: 도트 배율 보고서 (`feature/asset-seams`, 에디터 도구만).** `Editor/DotScaleReport`(메뉴 `ChronoSavior/도트 배율 보고서`, 배치 `RunBatch -reportOut`) — 그림 묶음·캐릭터·몹(엘리트 포함)·보스/성소·효과의
  도트 배율(`128 × 화면 크기 ÷ 그림 픽셀`)을 표로, 정수가 아니면 ⚠(지금은 경고만, 임시 도형은 따로 표시). 결과가 `docs/pixel-standard.md` 2절 표와 같다. 최종 그림이 오면 '정수가 아니면 실패'로 강화.
- **2026-11-06 (48)** — **[나] 3.5-e: 효과 에셋을 연결 칸으로 + 교체 지점 표 (`feature/asset-seams`, PlayMode 474/474, 씬 검증 통과, Codex 통과).** 씬·프리팹이 주인인 효과 — 피격(`EnemyHealth`, `Enemy_Base`)·차지(`MageChargeFlash`)·잔상(`PlayerAfterimages`) 머티리얼은
  공유 에셋 `Assets/Materials/SpriteFlash`(예전 `Resources.Load<Shader>` + 실행 중 `new Material`), 섬영 베기 프레임은 Player의 `BladeSlashVfx.frames`(예전 `Resources.LoadAll` 폴더). 비면 효과를 건너뛰고 검증기가 잡는다.
  정적 함수가 만드는 효과(중력 착탄·소용돌이·폭발 고리·레이저 구간)는 **`docs/asset-swap-points.md`**에 '실제 효과가 오면 효과 프리팹 + 쏘는 쪽이 넘김'으로. 이 표가 '무엇을 바꾸려면 어디를'의 한곳(UI는 팀원 제작 중이라 따로).
- **2026-11-06 (47)** — **[나] 3.5-d: 그림 크기와 판정 크기 분리 (`feature/asset-seams`, PlayMode 474/474, 씬 검증 통과, Codex 통과).** 그림의 화면 크기를 **월드 유닛으로 그림 데이터에** —
  `CharacterArtSet.heights`(지금 0.5)·`EnemyData.visualHeight`(지금 판정 지름 — `BuildEnemyData`, 비면 판정 지름)·`MonsterCatalog.bossSize/shrineSize`(1.3×1.5·0.64×0.96). 판정은 `EntitySizeConfig`·`colliderRadius` 그대로(콜라이더는 배율 역보정 유지).
  `PlayerRig.spriteHeight` 삭제. 지금 화면 크기 그대로(기준 캡처 바이트 동일). 테스트: 그림 높이·PPU를 바꿔도 판정 불변(플레이어·몹). 그림을 자식으로 떼는 구조 변경·VFX 크기는 보류(Codex: 지금은 불필요).
- **2026-11-06 (46)** — **[나] 3.5-c: 타격음 검증이 특정 파일을 요구하지 않게 (`feature/asset-seams`, PlayMode 472/472, 씬 검증 통과).** 검증기는 클립이 연결됐는지·소리 장치 설정만 본다. 테스트만 쓰던 `HitSound.ClipPath` 삭제 —
  테스트는 `AudioClip.Create`로 만든 짧은 소리를 넣는다(파일 존재 테스트 1개 삭제 → 472). 외주·구매 소리를 어디에 둬도 된다.
- **2026-11-06 (45)** — **[나] 3.5-b: 스테이지 그림 묶음 `StageArtSet` (`feature/asset-seams`, PlayMode 473/473, 씬 검증 통과, Codex 통과).** `World/StageArtSet`(땅 윗면·흙·발판·배경·포탈 + 그 그림에 딸린 숫자:
  배경 배율·보여 줄 범위·풀밭 높이·포탈 배율), `Assets/Data/Stages/StageArt_Forest`, `StageVisuals.art`(로비·전투) — `Resources` 경로 문자열·그림 전용 상수(640/360·190/450·181.8·BackgroundScale·BossPortal.ArtScale) 삭제.
  그림을 바꿀 땐 이 에셋만. 묶음 칸이 비면 오류(포탈은 포탈 있는 씬만), 검증기가 두 씬 확인. 기준 캡처와 바이트 비교 — 스테이지 3장 동일(각성·유물개방·업적은 캡처마다 뽑기가 달라 원래 매번 다름).
- **2026-11-06 (44)** — **[나] 3.5-a: 기준 캡처 + 현재 픽셀 기준 문서 (`feature/asset-seams`).** `docs/baseline-2026-11-06/`(스테이지 3장·UI 화면별 360 + 전투HUD·로비 1080), `docs/pixel-standard.md`
  (카메라 규격 확인 사실, 도트 배율 = `128 × 화면 크기 ÷ 그림 픽셀 수`, 지금 그림 전부의 배율 표, UI는 640×360 판 별도, 미정 항목, 제안은 '확정 아님'). 처음 말한 '몹 도트 4~7'은 잘못 읽은 것 — 실제 고블린 2.0·박쥐 1.48(나머지는 임시 도형).
- **2026-11-06 (43)** — **[나] 경험치 구슬 원본 보라색 복구 (`feature/asset-seams`, PlayMode 473/473, 씬 검증 통과).** 사용자 결정 — `Prefabs/Combat/ExpOrb`의 그림 색을 원본 `#c9a2ff`로(임시 흰 원이라 예전엔 흰색으로 보였다).
  `ExpOrbTests.Orb_IsOriginalPurple`. 코드는 사용자가 전부 총괄하므로 팀원 담당이던 코드도 확인 없이 진행(HANDOFF 반영). Codex는 평소 high, 종합 리뷰 때만 xhigh.
- **2026-11-06 (42)** — **[나] 결론·방향 재검토(Codex) + 사용자 결정 반영(`HANDOFF.md` '중간 점검과 남은 계획').** 사용자 결정: 경험치 구슬 보라색 복구 / UI는 팀원 제작 중(들어감·Figma만·제작 중 섞임) /
  외주 VFX 규격 미정 / **5단계까지 먼저, VFX 대비 사전 작업은 그동안, 도트 재작업은 VFX 납품 뒤**. Codex 수정: 도트 진단은 '현재 구성에 한정'해 전달, 4는 static 여부·쓰기 주체·수명을 따로 축으로, UI는 3.5에서 목록만·`CombatHud` 분리는 팀원과 먼저,
  규격 문서는 '현재 기술 기준'(확인된 사실과 미정 분리, 정수 배율은 제안). 순서: 구슬 → 3.5(a~g) → 4 → 5 → [VFX 납품 뒤] 시각 재작업.
- **2026-11-06 (41)** — **[나] 1~3 중간 점검 + 남은 계획 Codex 합의(`HANDOFF.md` '중간 점검과 남은 계획').** 사용자 새 기준: 그림·효과·소리는 외주/구매로 갈아끼운다 → **정리 순서 3.5 '에셋 교체 경계'** 신설
  (`StageVisuals` 경로 읽기 → `StageArtSet`, 타격음 검증의 경로 요구 제거, 효과·소리 교체 지점 표 — 미리 큰 틀은 안 만든다). 4는 '변경 권한·수명' 기준(ProfileService 쓰기만 소유 API로), 5는 책임 기준. 경험치 구슬 흰색(원본 보라)은 사용자 결정 대기.
- **2026-11-06 (40)** — **[나] 정리 순서 2(생성 책임) 완료 — Codex 종합 리뷰 '통과 — 병합 가능'** (PlayMode 472/472, 씬 검증 통과, UI 캡처 32장·스테이지 3장, 진짜 세이브 해시 그대로). `HANDOFF.md` 2 ✅, `CLAUDE.md`·이 파일 지금 상태 갱신.
- **2026-11-06 (39)** — **[나] 2-3c: 경험치 구슬 프리팹화 + 생성 담당 목록 확정 (`feature/creation-ownership`, PlayMode 472/472, 씬 검증 통과).** `Assets/Prefabs/Combat/ExpOrb`(그림 자식·RunTransient),
  `ExpOrb.Spawn(prefab, …)`, `EnemySpawner.orbPrefab`(`orbSprite` 대체). 장판 둘(`FireTrailZone`·`GravityWellZone`)은 코드 유지 — 구성이 루트+컴포넌트뿐이라 프리팹으로 얻는 게 없다(이유를 목록에).
  ⚠️ 확인: 씬의 구슬 그림이 흰 `Circle`이라 구슬이 **흰색**으로 보인다(원본은 보라 `#c9a2ff` — 예전 코드도 '그림이 있으면 흰색'이라 원래 그랬음, 이번에 바꾸지 않음). 사용자에게 짚기.
- **2026-11-06 (38)** — **[나] 2-3b: 메카닉 드론·포탑 프리팹화 (`feature/creation-ownership`, PlayMode 472/472, 씬 검증 통과, Codex 통과).** `Assets/Prefabs/Combat/GunnerDrone`·`GunnerTurret`(그림 자식·RunTransient),
  `GunnerDrone.Summon(prefab, …)`·`GunnerTurret.Spawn/Place(prefab, …)`, `GunnerAttack.dronePrefab`·`turretPrefab`(Player). 드론의 탄은 주인의 탄 프리팹. `bulletSprite`는 레이저 구간·연결선 그림(그림만 있는 효과)용으로만 남음.
- **2026-11-06 (37)** — **[나] 2-3a: 투사체 3종 프리팹화 (`feature/creation-ownership`, PlayMode 472/472, 씬 검증 통과, Codex 통과).** `Assets/Prefabs/Combat/GunnerBullet`·`MageBolt`·`EnemyBolt`
  (판정 상자·리지드바디·그림 자식·RunTransient를 프리팹에, 크기·색·속도는 쏠 때). `Spawn(prefab, …)`이 첫 인자로 프리팹을 받고(없으면 오류), 쏘는 쪽이 직렬화로 들고 넘긴다 —
  `GunnerAttack.bulletPrefab`(드론도 이걸로)·`MageAttack.boltPrefab`(Player, 20_Combat)·`EnemyShooterMotion`·`Boss.boltPrefab`(몹 프리팹). 그림 필드 `MageAttack.boltSprite`·`Boss.projectileSprite` 삭제.
  검증기가 탄 프리팹 연결·내부 참조·RunTransient를 본다. 테스트는 `TestPrefabs`(실제 프리팹을 읽음) + `.Equipped()`로 테스트 무기·몹에 실제 탄 프리팹을 끼운다.
- **2026-11-05 (36)** — **[나] 2-2b: 스포너를 종류별 프리팹으로 전환 (`feature/creation-ownership`, PlayMode 472/472, 씬 검증 통과, Codex 통과).** 스포너는 `PrefabOf(data)`(데이터 프리팹, 없으면 기본)로 세우고,
  성소·보스는 몬스터 목록의 프리팹(없으면 오류 — 부품 없는 보스를 조용히 세우지 않는다). 실행 중 `AddComponent`는 엘리트 표식만 남음. `EnemyMove.RefreshTypeBehaviour` 삭제(Awake에서 한 번 — '다시 불러야 하는' 순서 의존 없앰),
  데이터 값(박쥐 높이·궁수 탄 그림·색)은 `ConfigureTypeBehaviour`. 테스트: 꺼 둔 채 부품을 다 붙이고 켜는 방식(프리팹과 같게), 실제 전투 씬 스포너로 종류별 행동·보스·성소 구성 확인(`GameBootTests`). `docs/worksplit.md`의 옛 규칙 문구 갱신.
- **2026-11-05 (35)** — **[나] 2-2a: 몹 종류별 프리팹 + 데이터 연결 (`feature/creation-ownership`, PlayMode 471/471, 씬 검증 통과).** 몹 구성 방식은 Codex와 결정 — **종류별 프리팹 변형**
  (타입 행동 구성을 프리팹에 드러내고 스포너는 세우기·지역 스탯만, 엘리트 표식만 생성 담당 예외. 'Start에서 찾기'는 구성이 코드에 숨고 순서에 기대서 탈락).
  `Assets/Prefabs/Enemies/`(기본 `Enemy_Base`를 옮김 — GUID 유지) + 변형 `Enemy_Flyer`·`Charger`·`Shooter`·`Splitter`·`Shrine`(구조물·Kinematic·중력 0)·`Boss`(isBoss). `EnemyData.prefab`·`MonsterCatalog.bossPrefab/shrinePrefab`,
  `BuildEnemyData`가 종류별 프리팹을 이어 준다(`PrefabFor`). 검증기: 종류마다 그 행동 부품만 있는지·보스·성소 구성. **아직 스포너는 옛 방식**(다음 2-2b에서 전환·`RefreshTypeBehaviour` 삭제).
- **2026-11-05 (34)** — **[나] 2-1: 스스로 붙이던 부품을 씬 배치로 (`feature/creation-ownership`, PlayMode 471/471, 씬 검증 통과, Codex 통과).** `MageChargeFlash`는 20_Combat Player에(예전엔 `MageAttack.Awake`가 붙임),
  `HitSound`는 `[RequireComponent(AudioSource)]`+직렬화 클립 — 00_Persistent `Audio`에 소리 장치(자동 재생 끔·2D)와 클립 참조(`Resources.Load` 제거), 로비 `CameraAnchor`는 10_Lobby `LobbyWorld`의 자식(위치는 준비 때 계산).
  검증기가 셋 다 본다. **어느 씬에도 없던 옛 범용 근접 공격 `Combat/PlayerAttack`과 전용 테스트 삭제(사용자 승인)** — 스폰 무적 테스트는 `ISpawnProtectable` 상태로 다시 씀. 생성 담당 목록에서 C 분류 0곳.
- **2026-11-05 (33)** — **[나] 정리 순서 2(생성 책임) 시작 — 2-0 목록·정책 테스트 (`feature/creation-ownership`).** 실행 코드의 `AddComponent`·`new GameObject`·`Instantiate`를 전수 조사(34파일)해
  A~F로 분류, Codex와 진행 합의(`HANDOFF.md` '정리 순서 2'). `Tests/PlayMode/CreationOwnershipTests` — 생성 담당 목록(분류·이유)이 유일한 원본, 목록에 없는 새 담당·더는 안 만드는 담당 둘 다 실패.
  다음: 2-1 씬에 놓인 오브젝트가 스스로 붙이던 부품(`MageChargeFlash`·`LineRenderer`·`AudioSource`·로비 `CameraAnchor`)을 씬 배치로.
- **2026-11-05 (32)** — **[나] 씬 분할 2차 4단계(마감) 완료 — 전체 회귀·Codex 종합 리뷰 '통과 — 병합 가능'** (씬 검증 통과, PlayMode 473/473, UI 캡처 32장·스테이지 3장 눈 확인, 진짜 세이브 해시 그대로).
  `HANDOFF.md` 씬 분할 ✅·'4단계로 미룸' 문구 정리, `CLAUDE.md` 진행 상태 갱신.
- **2026-11-05 (31)** — **[나] 4-3: 캡처 도구 첫 장 잘림 수정 (`feature/scene-split-finish`, 에디터 도구만).** 원인: 씬을 열 때 PixelPerfectCamera가 배치 창 비율에 맞춰 카메라 Viewport Rect를 잘라 두어
  첫 렌더만 1920×810으로 잡혔다(로그로 확인 — 그 뒤 장은 첫 렌더 때 PPC가 다시 맞춤). `CaptureUiPreview.Render`·`CaptureStagePreview`가 렌더 전에 `cam.rect = (0,0,1,1)`. UI 캡처 32장·스테이지 3장 다시 찍어 확인, 씬 파일 변경 없음.
- **2026-11-05 (30)** — **[나] 4-2: 정리 (`feature/scene-split-finish`, PlayMode 473/473).** 호출부 없는 `GameFlow.StartRun()`(인자 없는 임시 진입점) 삭제, `defaultRegion` 설명을 지금 쓰임(에디터 전투 진입)으로,
  `UI/README.md`의 옛 이름(`RunController`)을 `GameFlow.StartRun(region, mode, done)`으로 고치고 화면 규칙 두 줄(월드는 `WorldView`로만, 상태·시간은 `GameFlow`에 요청), `LobbyWorld.OnDestroy`(자식 오브젝트 중복 파괴) 삭제.
- **2026-11-05 (29)** — **[나] 4-1: 로비 timeScale 0 유지 결정 + 상태 소유권 소스 검사 (`feature/scene-split-finish`, PlayMode 473/473).** 게임 시간을 쓰는 실행 코드를 전부 확인 —
  전투 씬 것·흐름·카메라 보간뿐이고 로비 씬엔 없다(물리 물체 없음, 걷기·화면은 실제 시간). 원본도 사냥이 아니면 dt = 0이라 그대로 둔다(근거: `GameFlow.EnterLobbyThen` 주석·`HANDOFF.md` 4단계).
  `StateOwnershipTests` — `GameState.Set`·`SetTransitioning`·`Time.timeScale` 대입이 `GameFlow` 밖(정의 `Core/GameState` 제외)에 있으면 실패(지금 0곳 — 규칙을 잠금).
- **2026-11-05 (28)** — **[나] 4-0: 화면을 옮기는 동안 화면 입력 잠금 (`feature/scene-split-finish`, PlayMode 472/472).** 4단계 진행은 Codex와 합의(4-0 입력 잠금 / 4-1 로비 timeScale 0 유지·근거 기록 / 4-2 정리 / 4-3 캡처 도구 첫 장 / 4-4 회귀·종합 리뷰).
  `PixelCanvas.AcceptsInput`(= 전환 중 아님) 하나를 키(`PxScreen.HandleKeys` — 설정 창 등 `OnKeys`도 이 안)와 마우스(판의 `GraphicRaycaster.enabled`)가 같이 본다 — 프로필을 직접 바꾸는 버튼(캐릭터 선택 등)까지 전환 중엔 안 눌린다.
  EventSystem 전체를 끄지 않는 이유: 포인터 상태·입력 재개까지 건드려 범위가 크다(Codex). 테스트: `PixelUiTests` 전환 중 입력 닫힘·재개, `GameBootTests`의 모든 전환 뒤(성공·실패) 클릭 다시 받음.
- **2026-11-05 (27)** — **[나] 씬 분할 2차 3단계 `main` 병합**(`feature/lobby-scene`, 3-0~3-5 + 종합 리뷰 반영, PlayMode 471/471, 사용자 OK). `HANDOFF.md` 3단계 ✅·'구현하며 정한 것', `CLAUDE.md` 씬 구성(3씬)·진행 상태 갱신.
  최종 점검에서 UI 캡처 도구를 다시 돌려 30장 정상 확인(첫 장 전투 HUD 1080만 UI가 밀려 찍힘 — 도구 문제로 보임, 4단계에서 확인).
- **2026-11-05 (26)** — **[나] 3단계 종합 리뷰 반영 (`feature/lobby-scene`, PlayMode 471/471, Codex '통과 — 병합 가능').** ① 사냥에 들어가다 필드 준비가 예외로 실패하면 날짜와
  **런 상태 전체**를 되돌린다(`RunState.Capture/Restore` 스냅샷 — 재도전 실패 시 결과 화면의 지난 런 기록 유지). ② 필드 정리는 **그 씬 것만**(`RunTransient.DestroyAll` → `DestroyAllIn(Scene)`) — 재도전 중 멈춰 둔 옛 전투 씬은
  새 씬의 필드 준비가 건드리지 않고, 씬이 내려갈 때 같이 사라진다. ③ ②를 하자 **숨어 있던 실제 버그**가 드러남: 전투 씬이 뜬 첫 프레임(흐름이 활성 씬을 정하기 전)에 스포너가 웨이브 타이머 초기값 0 때문에
  멈춘 시간에도 한 무리를 만들었고, 그게 이전 활성 씬(타이틀에서 들어가면 상주 씬)에 생겨 안 지워졌다(전역 정리가 가리고 있었음). 고침: 불러온 씬은 `sceneLoaded`에서 곧바로 활성 씬(`IWorldScenes.Load` 계약, 흐름의 새 월드 Activate 호출 삭제),
  `EnemySpawner.waveTimer` 초기값 = `FirstWaveDelay`. 낡은 주석(`LobbySceneTests`) 갱신. **4단계로 넘긴 것**: 전환 중 UI 버튼 입력 전체 잠금, 로비 timeScale 0 유지 판단.
- **2026-11-05 (25)** — **[나] 3-5: 에디터에서 연 씬으로 바로 진입 (`feature/lobby-scene`, PlayMode 469/469, Codex 리뷰 통과).** `Editor/PlayFromPersistent`가 Play 직전 열어 둔 게임 씬 이름을
  `SessionState`(`GameFlow.EditorEntryKey`)에 적고(게임 씬이 아니면·Play가 끝나면 지움), 상주 씬의 `GameFlow.Start`가 `#if UNITY_EDITOR`로 한 번 읽고 지운다 — `10_Lobby` → 로비, `20_Combat` → 사냥(`defaultRegion`, 날짜 안 씀), 그 밖 → 타이틀.
  테스트가 빈 씬에 만든 흐름은 읽지 않는다(`gameObject.scene`이 `00_Persistent`일 때만). `Systems/EditorEntryTests`(로비·사냥·없음·상주 씬 밖 무시). **3단계 구현 끝 — 다음: 3단계 전체 Codex 리뷰 → 사용자 확인 후 `main` 병합 → HANDOFF/CLAUDE 갱신.**
- **2026-11-05 (24)** — **[나] 3-4: 씬 전환 연결 (`feature/lobby-scene`, PlayMode 465/465, 씬 구성 검증 통과, Codex 리뷰 2회 반영).**
  리뷰 반영: 전환이 실패해 다시 켜진 전투 씬이 카메라를 다시 잡음(`CombatDirector.OnEnable`), 로더·활성화·멈추기·들어가기 예외도 실패로(잠금 해제·복구, 필드 준비 예외면 깎은 날짜 반환),
  복구 순서(시간·무대 값 → 활성 씬 → 이전 월드 켜기, 새 월드는 먼저 멈춤), 내리기는 실제로 내려간 뒤에만 로더가 잊고 못 내린 월드는 다음 전환에 다시 내림,
  화면 쪽 실패 알림 `CommonPopup.AlertIfTransitionFails`(타이틀 불러오기·새 원정(캐릭터 선택은 성공 뒤)·일시정지/결과 '로비로'·스테이지 입장). **보류**: 전환 중 UI 버튼 입력 전체 잠금(4단계 최종 점검 후보). `GameFlow`가 월드 씬을 불러오고 내린다 —
  타이틀(월드 없음) → `EnterLobby`/`StartNewExpedition`(`10_Lobby`) → `StartRun`(`20_Combat`, 재도전도 새로 불러옴) → 이전 월드 내림. 순서: 잠금(`GameState.IsTransitioning`) → 이전 월드 멈춤 →
  불러오기 → 활성 씬 → 들어가기(날짜 차감·`RunState.Begin`·`BeginField`·세이브 삭제는 **불러오기 성공 뒤에만**) → 이전 월드 내림. 실패하면 화면·시간·보스 무대 값·이전 월드(다시 켜고 활성 씬)로 되돌리고 `done(false)`.
  전환 중엔 시작·로비·타이틀·일시정지·재개·ESC·런 종료·히트스톱을 받지 않는다. `GameBoot`는 세이브만(월드 로드·`WorldScene` 삭제). `CombatDirector.ClearField`·`HidePlatforms` 삭제(전투 씬째 내려가 정리됨).
  `LobbyWorld`는 켜질 때 스스로 준비(`OnEnable` → `EnterLobby`, 건물·캐릭터 자리는 처음 한 번 — 흐름이 화면 층을 부르지 않게), 입력은 로비·전환 아님일 때만. `20_Combat`에서 `LobbyWorld` 제거(검증: `None<LobbyWorld>`).
  `StageSelectScreen.TryEnter(flow, s, entered)` — 성공한 뒤에만 닫고 실패면 공용 팝업. 테스트: `TestFlow`는 가짜 로더(`FakeWorldScenes`, `HoldNext`·`Release` 추가), `GameFlowTests` 실패 복구·새 원정 실패 시 세이브 유지·전환 중 잠금,
  `GameBootTests`를 진짜 전환 기준으로 다시 씀(부팅=타이틀·월드 없음, 타이틀→로비→사냥→결과→재도전→로비에서 월드 하나·활성 씬, 전투를 나가면 런 오브젝트 사라짐, 실제 로더로 실패 복구, 전투 씬 구성은 사냥에 들어가서). 다음: 3-5(에디터에서 연 씬으로 바로 진입).
- **2026-11-05 (23)** — **[나] 3-3: 씬 로더 계약 (`feature/lobby-scene`, PlayMode 454/454).** `Systems/WorldScenes` — `IWorldScenes`(불러오기·내리기·활성 씬·멈추기), 실제 구현 `SceneWorldScenes`
  (`sceneLoaded`로 받은 **그 Scene**을 핸들 번호에 묶음 — 재도전처럼 같은 이름 씬이 둘이어도 안 헷갈림, 멈출 때 켜져 있던 루트만 기억해 다시 켬, 빌드 목록에 없는 씬은 예외 없이 실패).
  테스트용 `FakeWorldScenes`(즉시 끝남·실패 지정). 아직 `GameFlow`에 안 이었다(3-4). `Systems/WorldScenesTests`(실제 씬: 같은 이름 둘·핸들별 내리기·활성 씬·멈춤 복구·없는 씬).
- **2026-11-05 (22)** — **[나] 3-2: `10_Lobby` 씬 만들기·검증 (`feature/lobby-scene`, PlayMode 451/451, 씬 구성 검증 '문제 없음').** 로비 월드·땅(판정 꺼 둠 — 땅 그림 크기 입력용)·스테이지 그림(땅·배경만)·
  그림만 있는 아바타. 빌드 목록 `[00_Persistent, 10_Lobby, 20_Combat]`. **아직 게임 흐름엔 안 이었다**(GameBoot는 여전히 20_Combat만 — 거기 로비 월드도 3-4까지 남는다).
  검증기: 세 씬·빌드 순서·로비 필수/금지 오브젝트. **캡처 도구 수리**: 2b 이후 새로 만든 빈 StageVisuals로 그리고 있었고(연결이 없어 땅·발판 그림이 안 깔림) 로비 캡처는 1차 분할 때부터
  판에서 LobbyWorld를 찾고 있었다(없음) → 씬에 놓인 StageVisuals, 로비는 10_Lobby에서. `UI/LobbySceneTests`(실제 씬 파일 불러와 구성·창구·내리면 비워짐).
- **2026-11-05 (21)** — **[나] 3-1: 로비 아바타 (`feature/lobby-scene`, PlayMode 447/447).** `UI/Lobby/LobbyAvatar` — 그림만 있는 로비 캐릭터(키트·체력·물리·Player 태그 없음), 세이브의 지금 캐릭터를 따라
  `CharacterArtSet`에서 그림·색을 입는다(캐릭터 선택 다음 프레임). 높이 맞추기는 `EntitySizeConfig.ScaleToHeight`로 빼서 `PlayerRig`와 같이 쓰고, 기본 이동 속도는 `Core/PlayerMoveConfig.BaseSpeed`
  하나로(원본 270÷100) — `CharacterMover2D.moveSpeed` 필드와 로비가 그걸 찾아 읽던 것 삭제. 아직 씬에 안 놓는다(3-2). `UI/LobbyAvatarTests`.
- **2026-11-05 (20)** — **[나] 3-0: 발판 무대 전환을 전투 담당으로 (`feature/lobby-scene`, PlayMode 445/445, 씬 구성 검증 '문제 없음').** `PlatformSet`의 정적 목록·`Activate`·`HideAll` 삭제 —
  재도전 때 옛·새 전투 씬이 겹치면 옛 씬 발판까지 다시 켰다(Codex 지적). `CombatDirector`가 같은 씬의 두 세트를 직렬화로 들고 `ShowArena`로 켜고 끈다(좌표 데이터 `FieldLayout`도 같이).
  `PlatformSet`은 표시 + 씬이 뜰 때 지금 무대에 맞춰 스스로 켜고 끄기만. 로비(아직 같은 씬)의 발판 숨기기는 `ClearField`로(3-4에서 함수째 사라짐). 테스트의 무대 되돌리기는 `FieldLayout.SetBossArena(false)`로.
- **2026-11-05 (19)** — **[나] 3단계 계획 재검토(문서만).** 사용자 "다시 확인". Claude가 코드와 대조해 10개 찾음 — 가장 큰 것: 계획대로면 `GameFlow`(Systems)가 `LobbyWorld`(UI)를 불러야 해서
  **컴파일이 안 된다**(→ 로비가 OnEnable에서 스스로 준비), 일시정지 메뉴 "로비로" 전환 중 ESC면 꺼진 월드에서 시간이 흐름(→ 잠금에 Pause·Resume·ESC·EndRun), 비동기 전환이면 기존 흐름 테스트
  7파일의 동기 단언이 다 깨짐(→ 즉시 끝난 로더면 yield 안 함). Codex가 동의하고 다듬음: 실패 복구 땐 아바타 위치 유지·보스 무대 값 복구, 시작 화면은 성공 때만 닫고 실패는 팝업. `HANDOFF.md` 3 "재검토로 고친 것".
- **2026-11-05 (18)** — **[나] 3단계 계획 합의(문서만).** Codex '수정 후 착수' — 내가 놓친 위험: 정적 발판 목록이 재도전 때 옛 씬 발판을 되살림(→ 0단계로 먼저 고침), 재도전은 씬 이름이 같아
  **씬 핸들**로 구분, 전환 실패 때 복구할 것 목록, '새 원정'은 로비 로드 성공 뒤 세이브 삭제, 로드 중 생성물의 씬 소속. 테스트는 씬 로더 계약(가짜 로더) + 실제 씬 통합 테스트로 나눈다.
  로비 timeScale은 3단계에선 0 유지. `HANDOFF.md` "씬 분할 2차" 3.
- **2026-11-05 (17)** — **[나] 2b 최종 Codex 리뷰 반영 (PlayMode 445/445, Codex "2b 통과").** 카메라 '바로 붙기' 요청이 다음 대상까지 남아 사냥 시작 때 플레이어에게
  순간 이동할 수 있던 것 → 요청을 그 대상에 묶음(다른 대상으로 바꾸거나 빼면 취소, `HitFeelTests.Camera_SnapRequest_AppliesOnlyToItsOwnTarget`). 소스 검사 설명을 실제 동작대로
  (줄 전체가 주석인 줄만 뺀다). **2b 끝** — 다음은 3단계(`10_Lobby` 분리 + 씬 전환).
- **2026-11-05 (16)** — **[나] 2b-4: 로비 카메라 기준점 + 런타임 전체 씬 찾기 금지 (`feature/world-links`, PlayMode 444/444, 씬 구성 검증 '문제 없음').**
  카메라 대상은 **지금 화면의 담당이 정한다** — 로비는 `LobbyWorld`의 가운데 기준점(`CameraAnchor`)에 바로 붙고(`Track(anchor, snap: true)`, 시간이 멈춘 화면이라),
  사냥은 `CombatDirector.BeginField`가 플레이어를 넘긴다(예전처럼 부드럽게). `CameraFollow2D`의 '지금 로비인지' 분기 2곳과 `PlayerRig`의 자기 등록을 지웠다.
  `LobbyWorld` → `UI/Lobby/`(`git mv`). 소스 검사를 `WorldLookupTests`로 바꿔 **`Assets/Scripts` 전체**에서 씬 찾기 금지 — 예외는 `PixelCanvas.EnsureEventSystem` 하나(이유 기록).
  **2b-3 Codex 리뷰 반영**: 전투 씬이 사라지면 HUD에 지난 포탈 점·적 점이 남던 것 비움, 로비 창구 붙이기·떼기와 로비 월드 없을 때 로비 HUD 안전 테스트.
- **2026-11-05 (15)** — **[나] 2b-3: 화면 창구 `WorldView` (`feature/world-links`, PlayMode 442/442, 씬 구성 검증 '문제 없음').** `Systems/WorldView` — 전투 씬 담당(`CombatDirector`)이
  `ICombatView`를, 로비 월드가 `ILobbyView`를 OnEnable/OnDisable로 꽂고 뺀다(중복이면 오류). 화면은 계약으로 **물을 때** 읽는다(복사 루프 없음):
  플레이어 체력·위치·키트, 포탈(일반 필드에서만), 보스 체력(`EnemyHealth.isBoss`), 살아 있는 적 위치(새 `EnemyHealth.Active` 등록 목록, 0.15초마다), 로비 근접 건물·캐릭터 위치.
  `CombatHud`·`LobbyHud`의 태그·타입 탐색과 `LobbyWorld.Instance`가 사라졌다. **계획과 달라진 것**: `Systems`는 `Characters`를 참조하지 않아서(기존 방식) 플레이어를 읽는 계약을
  `Core`에 — `IHealthView`(PlayerHealth)·`ISkillSlotHost`(PlayerRig), HUD 계약 `ISkillSlotSource`·`SkillSlotState`는 `Characters` → `Core`로 `git mv`.
  테스트: 새 `Systems/WorldViewTests`(창구 규칙, 담당이 내주는 값 = 예전 탐색 결과, 전투 씬 없을 때 HUD 안전), `GameBootTests`(실제 씬에서 창구), 월드 탐색 금지 대상에 HUD 둘.
- **2026-11-05 (14)** — **[나] 2b-2: 같은 씬은 직렬화, 생성물엔 스포너가 플레이어를 넣는다 (`feature/world-links`, PlayMode 438/438, 씬 구성 검증 '문제 없음').**
  씬 탐색을 걷어 냈다: `StageVisuals`(땅·발판 2·포탈 → 직렬화, 다른 씬 카메라는 `CameraFollow2D.CurrentCamera` 등록), `BossPortal`·`LobbyWorld`(Player 직렬화), `LobbyHud`(같은 씬 카메라 직렬화).
  실행 중 생기는 몹·경험치 구슬은 **스포너가 만들 때 Player를 넣는다**(`EnemySpawner.SpawnMonsterAt` — 잡몹·꼬마 슬라임·성소·보스가 다 지남, `ExpOrb.Spawn(…, player)`) — 몹이 Player 태그로 찾던 것 삭제.
  **테스트에서 발견**: 몹 추적 테스트가 대상 없이도 통과하고 있었다(배회 방향이 무작위라 50%로 플레이어 쪽) → 대상을 직접 넣게. 새 테스트 `SpawnedMonsters_AreGivenThePlayerToChase`.
  검증기: 새 직렬화 참조 전부(Player·Ground·발판 세트·포탈·카메라). 월드 탐색 금지 검사 대상에 2b 변경 파일 5개 추가.
- **2026-11-05 (13)** — **[나] 2b-1: 중력점 목록 읽기 전용 (`feature/world-links`, PlayMode 437/437).** `GravityWellZone.Active`(공개 가변 `List`) → `IReadOnlyList`,
  없애기는 `Vanish()`(목록에서 즉시 빼고 Destroy — 대붕괴가 목록을 직접 고치던 두 줄, 수명 끝, 개수 상한이 같이 씀). 이름을 `Collapse`로 하지 않은 건 스킬 '대붕괴'와 헷갈려서.
  `Combat/GravityWellZoneTests`(즉시 빠짐, 밖에 가변 List로 안 열림).
- **2026-11-05 (12)** — **[나] 2b 계획 합의(문서만).** 사용자 "2b 진행 방식 Codex랑 상의". 코드 전수(런타임 탐색 14곳)를 보고 짠 안 → Codex '수정 후 착수'.
  `WorldView`를 값 복사 루프 대신 **읽기 계약 창구**(Director·LobbyWorld가 꽂고, 화면은 물을 때 읽음)로 바꾸자는 Claude 제안을 Codex가 받음. Codex 보완: 미니맵은 스포너 목록이 아니라
  살아 있는 적 전부(지금 동작 유지 — `EnemyHealth` 활성 목록), 실행 중 생기는 몹·구슬도 스포너가 Player를 넣는다(태그 예외를 안 둠), 다른 씬 카메라는 `CameraFollow2D` 등록. `HANDOFF.md` "2b 연결".
- **2026-11-05 (11)** — **[나] 2a Codex 구현 리뷰 반영 (PlayMode 435/435, Codex "2a 통과").** ① 안전장치 검사가 `TestSaveSandbox.Path`만 보고 **지금 쓰는 경로**는 안 봤다 →
  `TestSaveSandboxTests`가 활성 경로를 보고, 테스트 순서에 기대지 않게 **소스 검사**(테스트 코드에 `PathOverride = null` 금지, 경로를 바꾸면 안전장치 경로로 되돌리기)를 더했다.
  ② 스테이지 카드 검사의 스폰 표본 0.001 → 0.0001 간격(원본 최소 구간 0.04). CLAUDE.md 테스트 규칙에 안전장치·배치 저장 함정을 남김.
- **2026-11-05 (10)** — **[나] 2차 단계 2a-3: 캐릭터 선택 = 프로필만 (`feature/data-art`, PlayMode 434/434, 씬 구성 검증 '문제 없음').** 로비 캐릭터 선택이 씬에서 `PlayerRig`를
  찾아 `Select`하던 것 → **세이브의 `character` 값만 바꾸고 저장**, 플레이어는 이미 있던 경로(`PlayerRig.Update`가 프로필을 따라감)로 바뀐다. 캐릭터를 바꾸는 길이 하나가 돼서
  `PlayerRig.Select`·`SelectNext`·`AvailableCharacters`를 지우고 `HasKit`은 내부로. 검증기: 캐릭터 선택에서 고를 수 있는 캐릭터(`CharacterSelectScreen.Selectable`)마다 Player에 키트.
  테스트: `UI/CharacterSelectTests`(고르면 프로필·저장·다음 프레임 키트 전환, 잠긴 카드 무시), 지운 API를 쓰던 테스트 3개는 프로필 경로로, 월드 탐색 금지 목록에 캐릭터 선택 추가.
- **2026-11-05 (9)** — **[나] 2차 단계 2a-2: 캐릭터 그림 에셋 (`feature/data-art`, PlayMode 432/432, 씬 구성 검증 '문제 없음').** `PlayerRig.characterSprites/characterColors`(씬 배열)
  → Core SO `CharacterArtSet`(`Assets/Data/Characters/CharacterArtSet.asset`, 씬의 지금 값 그대로 — 마법사·메카닉·섬영 `chara_temp_01`, 드루이드 `char_druid`, 색 흰색).
  로비 아바타(3단계)도 같은 에셋을 읽는다. 그림 적용 메뉴 두 개(`ApplyCharacterArt`·`ApplyTempCharacterArt`)는 **열린 씬 대신 에셋을 고치고 바로 저장**(`Editor/CharacterArtAsset`),
  캡처 도구 둘도 에셋을 읽는다. 검증기: 연결·배열 길이·키트 있는 캐릭터의 그림. 씬은 다시 저장해 코드에서 지운 옛 필드 값(스포너 3개·PlayerRig 2개)을 떨궜다.
  테스트: `Characters/CharacterArtTests`(읽기 규칙·갈아입기·그림 높이 = 콜라이더 지름), `GameBootTests`(실제 씬에서 저장된 캐릭터 그림으로 시작).
- **2026-11-05 (8)** — **[나] 2차 단계 2a-1: 몬스터 목록 에셋 + 테스트 세이브 안전장치 (`feature/data-art`, PlayMode 429/429, 씬 구성 검증 '문제 없음').**
  **몬스터 목록**: 스포너 씬 필드(`enemyTypes`·`bossSprite`·`shrineSprite`) → Core SO `MonsterCatalog`(`Assets/Data/Enemies/MonsterCatalog.asset`, 씬의 기존 값을 그대로 옮김 —
  7종·보스·성소 그림). 스포너(전투 씬)와 스테이지 선택 화면(상주 씬)이 같은 에셋을 본다 → **스테이지 선택이 더 이상 스포너를 찾지 않는다.**
  카드 목록(`MonstersOf`)은 스폰표 복사본이 아니라 표시 정책(칸 5개)으로 두고 `UI/StageSelectShowcaseTests`가 '보여 주는 건 실제로 나온다·칸이 남으면 다 보여 준다·순서'로 묶는다.
  `UI/UiWorldLookupTests`(옮긴 화면의 월드 탐색 금지 — 4단계에서 UI 전체로). 검증기: 두 씬이 같은 카탈로그·종류마다 하나.
  **테스트 세이브 안전장치**: `Tests/PlayMode/TestSaveSandbox`(NUnit `SetUpFixture`)가 테스트 전체 동안 세이브 경로를 임시 파일로 — `PixelUiTests`가 아직 진짜 세이브를
  백업·복원하고 있었고 2a는 저장을 부르는 화면 테스트가 늘어서. 안전장치가 꺼져 있으면 지우기 전에 멈춘다.
  **배치 저장 함정 발견**: 씬을 배치로 열어 저장하면 PixelPerfectCamera가 창 크기에 맞춰 잘라 낸 Viewport Rect(y 0.125·높이 0.75)가 같이 저장돼 실제 화면에 검은 띠가 생길
  뻔했다 → 되돌리고, 검증기가 **씬 파일의** Viewport Rect가 0,0,1,1인지 본다(메모리 값은 열 때마다 바뀌어서 파일을 읽는다).
- **2026-11-05 (7)** — **[나] 단계 1 자체 리뷰(사용자 "자세히 확인") — 코드 문제 없음, 문서 정리.** 확인한 것: `timeScale`·`GameState.Set` 대입은 `GameFlow`에만,
  히트스톱 요청 경로(0 이하 무시) 그대로, 시작 순서(날짜·`RunState.Begin` → 필드 → 사냥) 예전과 같음, 부팅 첫 프레임에 담당이 없어 타이틀 정리를 건너뛰는 건 정적 값이 새로 시작해
  무해, 일시정지·로비 복귀·재도전 경로, 새 파일 컴파일 경고 0. **고친 것**: `PROGRESS.md` "지금 상태"가 10/24에 멈춰 있었다(`CombatCore.unity`를 열면 로비부터 —
  씬 분할 1차 때 갱신 누락) → 지금 사실로, `CombatEvents` 주석(처치 멈춤 경로), `EnterBossField`가 공개인 이유, HANDOFF·CLAUDE 진행 상태.
  **원본과 다른 점 2개 발견(예전부터, 1단계와 무관 — 사용자 결정 대기)**: 원본은 보스 격파 뒤 1.6초를 실제 시간으로 재고(`setTimeout`) 그 순간 일시정지 중이면
  런을 아예 안 끝낸다(:4289 `if (state.scene === 'run')`) / 그 1.6초 동안 런 시간이 계속 흘러 시간 초과가 먼저 날 수 있다. 우리는 일시정지 동안 대기도 멈추고, 대기 중엔 런 시간을 안 흘린다.
  → **사용자 "지금대로"** — 의도적 편차로 `HANDOFF.md` 단계 1·`CombatDirector.HandleBossDefeated` 주석에 남김.
- **2026-11-05 (6)** — **[나] 씬 분할 2차 단계 1: `CombatDirector` 추출 (`feature/combat-director`, PlayMode 423/423, 씬 구성 검증 '문제 없음').**
  `GameFlow`(335줄)에서 전투 책임을 떼어 `Systems/CombatDirector`(전투 씬 `20_Combat`의 오브젝트, Player는 같은 씬 직렬화 참조)로:
  필드 준비·정리(`BeginField`·`ClearField`), 런 타이머·콤보·붉어짐 Tick, 보스 포털 판단·`EnterBossField`, 보스 격파 지연(1.6초), 처치 연출(`KillFeel`)·간격 둔 흔들림·성소 버프,
  사망·시간 초과·격파 → **`RunEvents.RunEndRequested`**(새로)로 종료 요청. `GameFlow`엔 화면 흐름·`GameState.Set`·timeScale·히트스톱 만료만 남았다(Player 태그 탐색도 사라짐).
  연결은 한 방향: `GameFlow`가 `CombatDirector.Current`를 부르고, 담당은 이벤트로만 알린다. 담당이 둘이면 뒤엣것은 오류를 남기고 꺼진다. 전투 씬 없이 `StartRun`하면 오류만 남기고 안 들어간다.
  **동작 변화(원본 쪽으로, 사용자에게 보고)**: 예전엔 보스 격파 지연 중 흐름이 히트스톱 만료까지 건너뛰어 **보스 처치 멈춤이 결과 화면까지 1.6초 내내 안 풀렸다** —
  이제 0.25초 뒤 풀리고 결과 화면까지 월드가 움직인다(원본 `state.freeze` :1847 → `setTimeout 1600` :4289와 같다).
  테스트: 새 `Systems/CombatDirectorTests`(등록·해제, 중복 거부, 꺼지면 사건 안 닿음, 전투 씬 없을 때, 격파 지연 중 시간 초과 안 됨, 멈춘 동안 타이머·콤보 그대로, 처치→히트스톱 경로),
  `GameFlow`를 만들던 4파일은 `TestFlow.Create(player)` 도우미로, `GameBootTests`에 실제 씬 배치 확인. Codex 구현 리뷰 2회.
- **2026-11-05 (5)** — **[나] 섬영 테스트 간헐 실패 원인 수정 (PlayMode 415/415, 섬영 테스트 3회 반복 54/54).** `Focus_Tier3_TrailHits_EnemyWithinBoostedRadius`가
  전체 실행 중 가끔 '3층 궤적 반경 안의 적이 안 맞았다'로 실패. **원인**: 테스트가 섬영·적을 기본 물리 레이어에 만들어서, 0.89 거리(반지름 합 1.0)로 겹친 콜라이더를
  물리 단계가 한 번이라도 돌면 서로 밀어냈다 — 적이 3층 판정 여유(0.05) 밖으로 밀려났고, 그 프레임에 물리 단계가 도는지가 타이밍에 달려 가끔만 실패.
  **고침**: 테스트도 실제 게임처럼 Player(6)/Enemy(7) 레이어(서로 물리 충돌 꺼짐, `BladeSkillTreeTests`·`BladeCombatTests`), 반경 경계를 재는 테스트 4개는 적 몸을 고정(`pinned`).
- **2026-11-05 (4)** — **[나] 씬 분할 2차 단계 0: 세이브 교체 경계 (`feature/profile-boundary`, PlayMode 415/415, Codex "단계 0 통과").** 사용자 "0단계 시작".
  `ProfileService.Current`를 공개 필드 → 읽기 전용 프로퍼티, 통째 교체는 `Replace`(불러오기)·`Reset`(새 프로필)이고 실행 코드에선 `SaveService`만 부른다
  (`SaveServiceTests.OnlySaveService_ReplacesOrResetsTheProfile`가 소스를 훑어 검사 — `using static`·별칭 우회도 금지). 타이틀 '새 원정'은
  `GameFlow.StartNewExpedition()`(세이브 삭제 → 로비)으로 — 화면이 프로필을 직접 초기화하지 않는다.
  **버그 수정(Codex 리뷰가 짚음)**: `PlayerHealth`가 처음 프로필의 레벨업에만 구독해서 '새 원정' 뒤 그 사냥 중 레벨업해도 최대 체력 재계산·회복이 안 됐다
  (런을 새로 시작할 땐 `ResetForRun`이 다시 계산해 가려져 있었다) → `ProfileService.Replaced` 이벤트로 새 프로필에 갈아탄다.
  **테스트 안전**: `SaveServiceTests`가 진짜 세이브 경로에 쓰고 백업으로 되돌리던 방식(테스트가 죽으면 진행 손실 위험) → 테스트 전용 임시 경로.
- **2026-11-05 (3)** — **[나] 씬 분할 2차 실행 계획 합의(문서만).** 사용자 "2차를 어떻게 진행할지, 지금 방식이 맞는지 Codex와 상의". GameFlow 실제 코드를 읽고 짠
  구체안 → Codex '수정 후 착수'. 정한 것: 단계 0(세이브 교체 경계)을 앞에 떼기, GameFlow→Director는 명시 호출·Director→GameFlow는 `RunEvents.RunEndRequested`,
  보스 격파 지연은 Director, 2단계를 2a(데이터)/2b(연결)로, 타이틀에선 월드를 안 띄움. **지난 합의 수정**: `WorldView`는 값만이 아니라 체력·스킬 칸은 읽기 계약
  (`IHealthView`·`ISkillSlotSource`), 위치·미니맵은 값 — HUD가 HP·스킬 칸·버프 줄도 읽는다는 걸 실제 코드에서 확인해서. `HANDOFF.md` "씬 분할 2차".
- **2026-11-05 (2)** — **[나] 구조 재검토 — 연결 규칙 확정(문서만).** 사용자 "정말 깔끔한 구조인지 다시 한번 확인". Claude가 'UI도 등록 목록을 직접 읽자(이미 9곳이 쓰는 패턴)'를
  제안했고 Codex가 'UI가 전투 구현 타입에 묶인다'고 반대 → `WorldView` 유지로 결론(Claude 동의). 대신 Claude 지적 3개 반영: 같은 씬 안은 직렬화 참조(탐색 금지),
  `WorldView`는 Director가 같은 씬 참조·스포너 목록으로 채워 탐색이 옮겨가지 않게, 공개된 가변 목록(`GravityWellZone.Active`)은 읽기 전용. `HANDOFF.md` "연결 규칙"·"경계".
- **2026-11-05 (1)** — **[나] 2차 전 보완 1: 씬이 원본인데 실행 중에 붙이던 것 제거 (`fix/scene-owned-components`, PlayMode 411/411, 씬 구성 검증 '문제 없음').**
  사용자 "보완 먼저 하자". Codex 판정 '보완 후 착수'의 1번. **찾은 문제**: `00_Persistent`의 `PixelPerfectCamera`에 기본값(100PPU·320×180·자르기 없음)이
  저장돼 있었고 진짜 값(128PPU·960×540)은 `CameraFollow2D.Awake`가 실행 중에 덮어쓰고 있었다 — 씬이 원본이라는 규칙과 정반대. → 씬에 진짜 값과 카메라 크기
  2.109375를 저장하고 Awake를 지웠다. `PlayerRig.Awake`가 붙이던 잔상(`PlayerAfterimages`)·섬영 베기 효과(`BladeSlashVfx`)도 씬의 Player에 배치하고 코드를 지웠다.
  `SceneSetupValidator`가 카메라 값·Player 효과를 검사하고, 카메라 테스트는 빈 오브젝트 대신 **실제 씬을 부팅하는** `GameBootTests`로 옮겼다
  (`SharedCamera_IsPixelPerfect960x540_AsSavedInTheScene`·`Player_HasItsEffects_FromTheScene`). 씬 값은 일회용 에디터 스크립트로 한 번 맞추고 그 스크립트는 지웠다.
  보완 2(상태 소유권·UI↔월드 `WorldView`)는 Codex '보완 2 합의' — 결정만 `HANDOFF.md` "2차 전 보완"에, 구현은 2차 단계에서.
  **팀원 주의**: 씬을 받으면 Player에 컴포넌트 2개가 늘고 카메라 값이 바뀐다(게임 화면은 그대로 — 원래 실행 중에 같은 값으로 덮어쓰던 것).
- **2026-11-04 (6)** — **[나] 섬영 HUD 칸·버프 줄 (`feature/blade-hud`, PlayMode 409/409).** 사용자 "섬영도 내가 작업할 때는 고쳐줘, 지금해" — 팀원 파일 `BladeCombat`에
  `ISkillSlotSource`를 붙였다(지금까지 섬영만 Z·X 칸이 비고 버프 줄 캐릭터 몫이 없었다). Z = `cdTimer / spin.cd(0.13)` — 원본 분모는 공격속도로 줄어든 값이
  아니라 기본 쿨다운 고정(:6322). X = 칼날폭풍 갈래면 `stormCd/8`, 아니면 `focusCd/11`(:6341~6345, 갈래 없음도 원본처럼 집중 쪽 = 빈 칸).
  버프 줄 = 원본 :6293~6301 순서·조건 그대로(속도 %·피해 ×·초과 이속 → 화력·【최고 속도】·집중/궤적 남은 초·칼날폭풍·연행 수). 원본 기호 ⚡🕛✂️🌪️는
  도트 글꼴에 없어 뺐고 ×·→·【】는 글꼴에 있어 그대로. 테스트 `SkillSlotTests.Blade_SlotsAndBuffLine_FollowOriginal`, HUD 사용처 검사에 `.BuffText` 추가.
- **2026-11-04** — **[나] 게임 이름 변경: 요괴전선 → Chrono Savior (`chore/rename-chrono-savior`).** 사용자 "컨셉이 바뀌었으니 yokai 이름 전부 바꿔, 팀원 파일 포함 지금 바로".
  네임스페이스·asmdef `YokaiFront.*` → `ChronoSavior.*`(asmdef 8개 파일 이름까지), 에디터 메뉴 `YokaiFront/` → `ChronoSavior/`, 셰이더 이름 `ChronoSavior/SpriteFlash`,
  게임 문구 '요괴' → '몬스터'(업적·아이템 '몬스터 유인향'·결과 화면 등), 제품명 `My project` → `Chrono Savior`·회사명 `DefaultCompany` → `PigeonHelicopter`
  (세이브 폴더가 `LocalLow/PigeonHelicopter/Chrono Savior/`로 바뀜 — 이 PC의 기존 세이브는 복사해 둠). 저장소 `kimJ02/yokai_unity` → `kimJ02/chrono-savior`.
  **안 바꾼 것**: `reference/project_test.html`(원본 — 수정 금지, 원본 이름 '요괴전선'은 원본을 가리킬 때만 남김), 몬스터 고유 이름(멧돼지 등)·업적 '몬스터 학살'은 그대로(사용자 확인 필요).
  **팀원 주의**: 내일 받을 때 `using YokaiFront…` 가 남은 작업 중 파일이 있으면 `ChronoSavior`로 바꿔야 컴파일된다.
- **2026-11-04 (5)** — **[나] 전투 HUD 버프 줄 (`feature/hud-buff-line`, PlayMode 408/408).** 사용자 "버프 표시줄 추가". 원본 `#buffs`(:495, 색 #8ef0c8)처럼
  상태창 바로 아래(Figma엔 자리 없음 — 임시 위치 `CombatHud.BuffBox`). 문구는 원본 `buffTxt`(:6283~6318) 순서·조건 그대로(`CombatHud.BuffLine`) —
  피해 배율(>1.01)·살기·성소 가호·성소 경고·회귀 부족·캐릭터별(마법사 차지율, 메카닉 충전·부품·설치기·링크). 원본 기호(⚔·殺·⛩·⚠)는 도트 글꼴에 없어 글자로.
  ~~확인 필요(팀원): 섬영은 아직 `BuffText`가 없다~~ → ✅ 로그 11-04 (6)에서 붙임.
- **2026-11-04 (4)** — **[나] 씬 분할 1차 (`main`, PlayMode 407/407).** 사용자 "처음부터 깔끔한 구조, 교수님이 보기에 완벽하게" — Codex와 4차례 검토·3차례 구현 리뷰 끝에 '통과'.
  씬 `CombatCore` 하나 → `00_Persistent`(GameBoot·GameFlow·AutoSave·UI 판 PixelUI·EventSystem·공용 카메라·Audio) + `20_Combat`(월드, 옛 CombatCore GUID 유지). `SampleScene` 삭제.
  `RunController` → `GameFlow`(이름만), `GameBoot` 신설(세이브 로드 → 월드 로드 → 활성 씬), 옛 IMGUI 7파일·`Sprites/UI`·`BuildUiTextures`·`BuildPartAScene` 삭제,
  씬 검사 `Editor/SceneSetupValidator`(메뉴 `ChronoSavior/씬 구성 검증`), 에디터 Play는 `00_Persistent`부터(`Editor/PlayFromPersistent`, 테스트 실행엔 안 걸림).
  화면은 판에 연결된 GameFlow만(`PxScreen.Flow`), 카메라 대상은 플레이어가 등록(`CameraFollow2D.Track`).
  **버그 2개 수정**: ① 보스 발판 세트가 꺼진 채 저장돼 보스 무대에 발판이 안 나왔다(9/10부터) → 켠 채 저장 + `PlatformSet.Awake`가 무대를 따름.
  ② 시간이 멈춘 화면에서 카메라가 목표에 정확히 서 있으면 `SmoothDamp`가 NaN → dt>0일 때만. 새 테스트 `GameBootTests`(실제 씬 부팅, 테스트 전용 세이브 경로).
  **팀원 주의**: `RunController`가 `GameFlow`로 바뀌었다. 씬은 이제 손으로 고치는 게 원본 — 고친 뒤 '씬 구성 검증'. 계획·2차 할 일은 `HANDOFF.md`.
- **2026-11-04 (3)** — **[나] 지역 이름도 컨셉에 맞게 (`main`).** 사용자 "지역 이름도 바꾸자". 1 대나무 숲→푸른 초원, 2 버려진 신사→버려진 마을,
  4 홍등 거리→고블린 소굴, 8 저승 문턱→망자의 묘지, 9 백귀야행→마왕성(3·5·6·7 안개 골짜기·폐허 성곽·설산 고개·화염 동굴은 그대로).
  업적 '숲의 수호자'→'초원의 수호자', '백귀야행 종결'→'마왕 토벌'.
- **2026-11-04 (2)** — **[나] 몬스터 이름도 컨셉에 맞게 (`chore/rename-chrono-savior`).** 사용자 "몬스터 고유 이름도 직관적인 이름으로".
  화면 이름: 도깨비불→박쥐, 오니→고블린, 대오니→오우거, 돌진귀→멧돼지, 사수귀→해골 궁수, 분열귀→슬라임, 새끼→꼬마 슬라임(업적 '백귀 참살'→'몬스터 학살', '백귀 토벌자'→'보스 토벌자').
  코드는 행동 기준 이름: `EnemyType.Wisp/Oni/BigOni` → `Flyer/Grunt/Brute`(나머지 Charger·Shooter·Splitter·Splitlet 그대로, enum 순서 유지 → 직렬화 값 안 바뀜),
  `EnemyWispMotion` → `EnemyFlyerMotion`, 데이터 `Wisp/Oni/BigOni.asset` → `Flyer/Grunt/Brute.asset`, 스프라이트 `enemy_wisp/oni/bigoni` → `enemy_flyer/grunt/brute`,
  기본 몹 프리팹 `Enemy_Oni.prefab` → `Enemy_Base.prefab`(.meta 같이 옮겨 GUID 유지). 원본 키 대응표는 `docs/original-parity.md` 맨 위.

- **2026-10-28 (2)** — **[나] 로비 한 화면 (`feature/gravity-impact-vfx`, PlayMode 400/400).** 사용자 "맨 왼쪽에서 나와 던전까지 오래 걸린다, 캐릭터가 건물보다 너무 작다, 한눈에 들어오게".
  건물을 96×104도트(0.75×0.81유닛, 캐릭터의 약 1.6배 — 틀은 9-슬라이스)로 줄여 필드 가운데에 ±0.9·±2.7 간격으로 모음, 캐릭터는 가운데서 나오고
  건물 줄 안(가운데 ±3.3)에서만 걷는다, 로비 카메라는 가운데 고정(`CameraFollow2D`). 들어가기 거리 1.1 → 0.6.

- **2026-10-28 (1)** — **[나] 화면 흔들림 키움 (`feature/gravity-impact-vfx`, PlayMode 400/400).** 사용자 "적 타격할 때 흔들림 올려줘"·"블랙홀 X에 흔들림".
  흔들림 전체 1.5배(화면에 옮길 때만) · 평타·무기 틱 적중에 흔들림(5px·0.14초, 모든 캐릭터 같은 값, 0.25초 간격 — 원본엔 없음) · 대붕괴 5/9 → 12/18. 원본 대조표 9절에 기록.

- **2026-10-26 (2)** — **[나] 로비에 섬영 잔상이 굳어 남던 버그 · 타격 소리 최소 간격 0.12초 · 레이저 등 무기 틱에도 소리 (`feature/gravity-impact-vfx`, PlayMode 399/399).**
  잔상·베기 그림은 시간이 멈춘 로비(dt 0)에서 안 사라져 하늘에 하얗게 줄지어 남았다 → 로비·타이틀로 나가면 지운다. 타격 소리는 마지막 소리에서 0.12초 안이면 건너뛴다(`HitSound.MinGap`).
  레이저·포탑·연결장·섬영 궤적·불길 장판·중력장은 틱 피해라 타격 알림이 없었다 → `CombatEvents.EnemyTickDamaged`(화상 틱은 빠짐)를 새로 두고 소리가 듣는다.

- **2026-10-26 (1)** — **[나] 마법사 깜빡임은 완충 때 한 번만 · 블랙홀 없이 X 누르면 터지던 것 막음 (`feature/gravity-impact-vfx`, PlayMode 396/397).**
  원본은 중력점이 없으면 정면에 한 번 터뜨리는데(`gravityCollapse` :2081) 사용자가 버그로 봐서 범위 안에 중력점이 없으면 X가 안 나가고 쿨다운도 안 쓴다.
  실패 1개는 섬영 팀원 테스트 `Focus_Tier3_TrailHits…` — 흔들리는 테스트(섬영 테스트만 따로 2번 돌리면 47/47).

- **2026-10-25 (4)** — **[나] 몹끼리 충돌 없앰·로비 스킬 버그·입장료·히트스톱 간격·마법사 차지 깜빡임 (`feature/gravity-impact-vfx`, PlayMode 396/396).** 사용자 7가지 요청.
  - **몹끼리 물리 충돌 없앰**(사용자 정정 "겹쳐도 된다, 충돌이 문제였다" — 처음엔 옆으로 밀어내기까지 넣었다가 뺐다): 몹끼리 충돌이 켜져 있어
    원형끼리 밀리다 위로 타고 올라가 쌓였다(중력장 셋을 깐 3스테이지 측정: 올라탄 쌍 최대 6). 몹 몸마다 접촉을 꺼서 원본처럼 겹쳐 지나간다.
  - **플레이어가 몹 위에 올라타기**: 재현 안 됨 — 레이어 충돌표에서 Player↔Enemy가 꺼져 있고 실험해도 몹을 통과한다. 사용자에게 상황을 물어봄(몹이 몹 위에 올라타던 것이면 위에서 고침).
  - **로비 들어갈 때 스킬이 나가던 버그**: 메뉴 "확인"도 Z라 결과 창에서 Z로 로비에 들어가면 같은 키가 공격으로 읽혔다. 전투 키(C·Z·X)는 사냥 중에만 읽히고
    (`GameInput`), 메뉴에서 사냥으로 들어오는 순간 누르던 키는 한 번 떼야 읽힌다(일시정지에서 돌아올 땐 안 잠금). 마법사는 사냥 밖에서 차지를 쏘지 않는다.
  - **입장료 없앰**(`RegionConfig.EntryFee` = 0, 원본 곡선은 `OriginalEntryFee`) · **히트스톱 쉬는 시간 0.25초**(`RunController.HitstopGap`, 성소·보스 처치는 예외)
    · **마법사 차지 단계(원본 50%·99%)마다 몸이 하얗게 한 번 깜빡**(`MageChargeFlash`) · **마법진 삭제**(팩 그림·라이선스 파일까지).
  - 주의: 섬영 `BladeSkillTreeTests.Storm_Tier4_GrabCapRespected`는 **원래부터 흔들리는 테스트** — 한 프레임 사이에 물리 계산(0.02초)이 끼면 겹쳐 둔 몹이 퍼져
    5마리만 잡힌다(그대로 재현: 변경 전 코드도 5번 중 2번 실패). 팀원 파일이라 안 건드림 — `WaitForFixedUpdate` 없이 프레임 하나에 기대는 구조라 팀원이 고쳐야 한다.

- **2026-10-25 (3)** — **[나] 받은 무료 팩으로 그림·소리.** 섬영 회전베기 베기 효과(Slash 1 · color4 — `main`) · 적 타격 소리(Kenney `footstep_carpet_002` — `main`)
  · 블랙홀 마법사 착탄 폭발 + 중력장이 남아 있는 동안 소용돌이(Gigapack violet, 회전 없음 · 보라 선 고리 삭제 — `feature/gravity-impact-vfx`). 판정은 그대로, 그림·소리만.
  섬영은 팀원 파일(`BladeCombat`)을 안 건드리고 밖에서 읽는다(`cdTimer` 오름 = 휘두름). 팩 라이선스는 각 폴더에 동봉.
- **2026-10-28 (2)** — **[나] (1)번 수정이 깨뜨린 PlayMode 테스트 수습 — 가드 위치를 좁혔다.** 사용자가 PlayMode를 돌려 쿨다운·장판·차지감속·드론 쿨다운 테스트 다수 실패를 보고(`Cooldown_ScalesWithAttackSpeedUpgrade`·`Field_ClosedTriangle_DamagesEnemyInside`·`Field_TwoLinkedTurrets_DamageEnemyOnLink`·`LaserBuildBullet_UsesLaserTierCooldownAndDamage`·`MageAttack_Charging_HalvesMoveSpeed`·`CooldownItem_ShortensGunnerCooldown`·`LaserDroneUlt_ShowsCooldownOnXSlot_AndCountsDown`).
  - **원인**: (1)번에서 `Update()`(섬영은 `LateUpdate()`도) 맨 앞에 넣은 `if (!GameState.IsRunning) return;`가 매 프레임 쿨다운·피해량 재계산, `UpdateField`/`UpdateFocus`/`UpdateStorm` 같은 지속 효과 처리까지 통째로 막아 버렸다. PlayMode 테스트는 `GameObject`를 실제 씬에 띄우고 `yield return null`/`WaitForSeconds`로 **진짜 `Update()`가 돌게** 해서 이 값들을 확인하는데, 테스트의 `GameState.Current`는 기본값 `Lobby`라(아무 테스트도 `GameState.Set(Run)`을 안 부른다) 저 가드에 전부 걸렸다.
  - **고침**: 가드를 함수 맨 앞이 아니라 **"새 입력을 읽어 액션을 거는" 지점만** 감싸게 좁혔다 — 마법사·메카닉은 공격/스킬 트리거 블록만 `if (GameState.IsRunning) { ... }`로, 쿨다운·피해량 재계산과 `UpdateField`/`UpdateBeam`은 그대로 매 프레임 돈다. 섬영은 Z 회전베기·X 스킬 진입 두 블록만 감싸고, `UpdateFocus`·`ApplyDirectionalInvuln`·`ApplyCrashImmunity`·`UpdatePassTick`은 그대로 — `LateUpdate()`의 `UpdateStorm`/`UpdateFalling`도 가드를 뺐다(테스트가 `TryStormStart`를 리플렉션으로 직접 불러 놓고 이 콜백이 자연스럽게 도는 것에 의존해서). Run을 벗어났는데 storming이 true로 남는 위험은 (1)번에서 넣은 `GameState.Changed` 구독(`OnSelected()` 재호출)이 이미 처리한다.
  - **주의**: 여전히 이 세션은 PlayMode를 못 돌린다 — 위 7개가 다시 통과하는지, 그리고 전체 스위트(지난 375/375)가 깨진 게 더 없는지 사용자 쪽에서 재확인 필요.

- **2026-10-28 (1)** — **[나] 로비 들어가면 스킬이 한 번 나가고 그 스프라이트가 안 사라지는 버그 수정.** 사용자 "마을에 처음 왔을 때 스킬이 딱 한 번 써지고 그 스킬 스프라이트가 계속 남아있다".
  - **원인**: `MageAttack`/`GunnerAttack`/`BladeCombat` 세 키트 모두 `Update()`에 `GameState.IsRunning` 가드가 없었다 — `GameState.cs`·`RunController.cs`가 명시한 "적·스폰너·투사체·장판엔 가드 안 넣어도 된다"(timeScale=0이 Time.deltaTime을 묶어 버리니까) 전제가 **발사 트리거 자체**엔 안 맞았다: `if (!held) Fire(...)`(마법사)·`held || AttackDown`(메카닉)·`AttackHeld`(섬영) 전부 `Time.deltaTime`과 무관한 bool 비교라 `timeScale=0`이어도 그대로 걸린다. 거기에 10-24에 추가된 `GameInput.MenuConfirmDown`이 메뉴 확인에 **Z를 공격키와 공유**해서, 로비 진입 직후 메뉴(결과창 등)를 확인하려고 누른 Z가 동시에 공격으로 읽혔다. 쏘고 난 투사체(`MageProjectile`/`GunnerBullet`)는 자기 `Update()`가 scaled `Time.deltaTime`으로 움직여서 `timeScale=0`인 로비에서 그대로 멈춰 화면에 박혔다.
  - **고침**: 세 파일 `Update()`(`BladeCombat`은 칼날폭풍을 처리하는 `LateUpdate()`도 같이) 맨 앞에 `docs/worksplit.md` 계약 그대로 `if (!GameState.IsRunning) return;` 추가. 추가로 `GameState.Changed`를 구독해서 Run을 벗어나는 즉시 차지(마법사)·광선·장판(메카닉)·집중/칼날폭풍(섬영) 상태를 정리(`OnSelected`/`OnDeselected`가 이미 하던 정리를 재사용) — "스킬 쓰는 중에 죽어서 Run이 끝난" 경우까지 커버한다.
  - **주의**: 이 세션은 Unity 빌드·PlayMode 테스트를 돌릴 방법이 없다(디바이스 브릿지로 파일만 고쳤다) — 에디터에서 컴파일 확인 및 PlayMode 전체(지난 375/375) 재실행이 꼭 필요하다.

- **2026-10-25 (2)** — **[나] UI 흐림 고침 — 판을 화면에 꽉 채우고 글꼴을 번짐 없이 (`feature/figma-ui`, PlayMode 375/375).** 사용자 "UI 창들이 다 흐릿하다"(타이틀·각성·로비 건물·스테이지 선택 스크린샷).
  - 원인 셋: ① UI 판을 정수배로만 키워서 에디터 Game 창(857×482)에선 1배(640×360)로 작게 그려졌다 — 8~12px 글자가 뭉개짐.
    ② 글꼴이 기본 가져오기 설정(Smooth = 안티에일리어싱)이라 작은 글자 가장자리가 회색으로 번졌다. ③ 에디터 Game 창의 "Low Resolution Aspect Ratios"가
    켜져 있어(Windows 125%) 80% 크기로 그린 그림을 1.25배 늘려 보여준다(스크린샷에서 도트가 7·8픽셀로 번갈아 커진 것으로 확인).
  - 고친 것: ① `PixelCanvas.ScaleFor` = 화면을 꽉 채우는 배율(720p·1080p·1440p는 그대로 딱 정수배 — 정수배에 2% 안으로 가깝고 2픽셀 넘게 안 넘치면 정수배).
    ② 글꼴 `fontRenderingMode: 2`(Hinted Raster, 번짐 없음) — 이 글꼴은 외곽선이 원래 계단 모양이라 큰 글자도 그대로 깔끔하고, 글꼴 안 gasp 표도 9~16px는 흑백을 권한다.
    로비 건물 간판(월드 캔버스)도 같이 선명해진다. ③은 에디터 설정이라 코드로 안 건드리고 사용자에게 끄는 법을 안내.
  - `CaptureUiPreview -captureSizes 857x482,1072x602` — 창 크기별로도 찍는다. 사용자 스크린샷과 같은 곳을 나란히 놓고 확인(지금 / 고친 뒤 에디터 설정 그대로 / 저해상도 끄면).
  - 주의: 섬영 `BladeSkillTreeTests.Storm_Tier4_GrabCapRespected`가 두 번 우연히 실패(겹쳐 놓은 적 8마리가 그 한 프레임에 물리 계산이 끼면 밀려나서 5마리만 잡힘).
    UI·글꼴과 무관 — 변경 전 코드 375/375, 같은 코드 재실행 375/375. 팀원 테스트라 안 건드렸다(`WaitForFixedUpdate` 없이 프레임 하나에 기대는 구조).

- **2026-10-25 (1)** — **[나] Figma UI 13페이지 전부 적용 + 걸어 다니는 로비 + 멸망까지 7일 (`feature/figma-ui`, PlayMode 375/375).** 사용자 "Figma UI 13페이지 다 넣을 거야" (10/24).
  - **바탕**: IMGUI(글자를 키워서 흐림·배치 캡처 불가) 대신 **uGUI 640×360 판을 정수배로**(1080p ×3) — `UI/Pixel/`(`PixelCanvas`·`Px`·`PxArt`·`PxButton`·`PxScreen`·`PxTooltip`).
    Figma 원본 PNG(`download_assets` rawImages)를 `Resources/Art/Figma/UI/`에, 버튼 올림·눌림·비활성은 원본 밝기만 바꿔 만든다. 글꼴 DNF Bit Bit v2(네오플 공식, 저작권 고지 동봉).
    화면은 `CaptureUiPreview`로 게임 화면 위에 찍어 Figma 그림과 나란히 비교(자리 1~3도트 안). 창이 떠 있으면 ESC는 창 몫(`Core/UiModal`).
  - **끝난 화면**: 일시정지(정보 없이 아이콘 3개) · 설정 창(Figma에 없음 — 화면 모드·해상도·음량·조작, ↻로 닫기) · 전투 HUD(빨간 칸 = 체력+숫자, 하늘·어두운 칸 비움,
    남은 초·COMBO·스킬 칸·실제 미니맵, **경험치 = 맨 아래 바**(옛 1920판 색), 임시로 토벌 진행·포탈 안내) · 타이틀 · 공용 팝업 · 시간 회귀(Figma 라벨) ·
    유물 개방(35종 두 쪽, 등급 ◆, 확률은 원본 55·30·12·3%) · 각성창(전문화 트리 금테/은테/회색 + [스탯]=원정 강화). 예전 IMGUI 화면은 Figma 화면이 있으면 안 그린다.
  - **이어서 끝낸 것(10-25)**: 스테이지 선택(왼쪽 목록은 옛 1920판, ↻ = 창 닫기) · 캐릭터 선택(뼈대 → 공용 부품, 무기 아이콘) · 원정 기록/전투 패배(뼈대) ·
    업적 창(Figma에 없음) · **걸어 다니는 로비**(`LobbyWorld` — 시간은 멈춘 채 캐릭터만 ←/→로 걷고 건물 앞 F/↑, 발판 숨김) · **멸망까지 7일**
    (`PlayerProfile.doomDays`, 스테이지에 들어갈 때마다 -1 `RunController.StartRun`, 회귀하면 7일, 0일이면 입장 불가) · 새 원정은 캐릭터 선택부터.
  - **사용자에게 물을 것**: 보스 체력바 자리(Figma 상자 = 화면 가운데라 가림) · HUD의 레벨·골드·버프 줄(Figma에 없음) · 유물 확률(Figma 옛 판 70·27·2·1%) ·
    메카닉·섬영 스킬 아이콘(Figma엔 마법사 것만) · 게임 캐릭터 그림 교체(Figma 캐릭터가 10/14 팀원 판에서 바뀜, 초상화만 새 그림) · 0일이 되면 어떻게 되는지.
  - 주의: 섬영 `BladeSkillTreeTests.Focus_Tier3_TrailHits…`가 한 번 우연히 실패(따로 돌리면 47/47) — 팀원 테스트, 타이밍에 따라 흔들린다.

- **2026-10-24 (5)** — **[나] 잔상 — 원본 `player.afterimages` (`fix/text-spawn-blink`, PlayMode 365/365).** 사용자 "섬영 깜빡임 대신 잔상으로".
  섬영이 빠를 때(속도 비율 0.55 초과 → 프레임마다 비율×0.7)·칼날폭풍 활공(80%)·마법사 순간이동 출발 자리에 푸른 반투명 그림
  (투명도 0.3 → 0, 0.25초, `Characters/PlayerAfterimages` — `PlayerRig`가 붙인다). 섬영 상태는 `BladeCombat`을 안 건드리고 밖에서 읽는다
  (물리 속도 ÷ 최고 속도, 칼날폭풍 = 물리 속도 없이 위치만 옮기는 상태). 원본 확률은 60fps 한 프레임 기준이라 프레임 길이에 맞춰 바꾼다.
  원본 궁극기 잔상은 우리 게임에 궁극기가 없어서 없다. 테스트 `AfterimageTests` 4개.

- **2026-10-24 (4)** — **[나] 글자 받침 잘림 · 스폰 반투명 유령 · 섬영 상시 무적 깜빡임 (`fix/text-spawn-blink`, PlayMode 361/361).**
  - **계기**: 사용자 "환생할 때 글자 깨짐 / 적 스폰 중일 때 효과 / 섬영 기본 공격 때 깜빡이는 건 다른 방식으로".
  - **글자**: 깨진 게 아니라 칸(15)이 글자(13px)보다 낮아 **받침이 잘렸다**(회귀 결과 통계 이름, HUD 줄). 기본 글자 스타일을
    넘쳐 그리기로(`UiTheme`, `TextClipping.Overflow`). 흐린 건 그대로 — Figma UI(도트 폰트·정수배) 때 같이.
  - **스폰**: 원본 "반투명 유령 상태" `globalAlpha = 0.4 + 0.1·sin(10t)`(:4773)이 빠져 있었다 → 스폰 보호(2초) 동안 몸 반투명
    (`EnemyMove.SpawnGhostAlpha`, 스포너가 칠한 색은 그대로).
  - **섬영 깜빡임**: 10-24 (2)에 넣은 무적 깜빡임이 섬영 방향키 무적(`BladeCombat`이 매 프레임 2프레임어치씩 주는 무적)에도
    걸려서 움직이는 내내 깜빡였다. 원본은 그 무적이 타이머가 아니라 맞는 순간의 조건 검사라 안 깜빡인다(:1883·:1889) →
    0.1초 이하로 주는 무적은 깜빡임에서 뺐다(`PlayerHealth.BlinkRemaining`). 맞은 뒤·부활·순간이동 깜빡임은 원본대로. 팀원 파일은 안 건드림.
  - 테스트 4개 추가(`SpawnGhostTests` 2, `HitFeelTests` 2).
  - **같은 날 Figma 13페이지 전부 확인** — 다음 할 일 8번, 정리 그림 `claude작업물/chrono_preview/Figma_UI_전체.png`.

- **2026-10-24 (3)** — **[나] 실제 맵 발판으로 발판 동작 검사 + 캐릭터 발 높이를 Figma대로 (`feature/figma-stage1`, PlayMode 357/357).**
  - **계기**: 사용자 "플랫폼 제대로 잘 작동하는지도 확인". 기존 발판 테스트는 테스트용 발판 하나로만 쟀다 → `PlatformLayoutTests`(7개)가
    `BuildPartAScene`과 똑같이 땅 + 일반 발판 15개 + 보스 발판 3개를 만들고 Figma 타일까지 깐 뒤 진짜 `CharacterMover2D`로 잰다:
    모든 발판의 타일 끝·윗면 = 판정(1px 안), 15개 발판 전부 위에서 착지, 실제 점프 속도로 1층을 아래에서 뚫고 올라가 착지,
    아래키 통과로 땅까지 → 통과 뒤 다시 단단해짐, 맨 바닥에선 통과 안 됨, 발판 끝 바깥 허공에 안 섬. **전부 통과.**
  - 아래키 통과를 `CharacterMover2D.TryStartDropThrough()`로 떼어 냈다(동작은 그대로 — 키보드 입력 없이 테스트가 부른다).
  - **캐릭터가 2px 떠 보였다**: 캐릭터 그림 아래에 투명 2px가 있어서. Figma "스테이지 HUD"는 캐릭터 그림 바닥이 타일 윗면보다 3px 아래
    (발이 풀 속으로 1px) → 캐릭터 그림 피벗을 3px 위로(`char_*.png.meta` 0.546875, `BuildPrototypeSprites`도). 판정은 그대로.
  - 몹 그림도 아래 투명 줄이 있다(슬라임 10px·도마뱀 5px·멧돼지 4px) — 3단계(몹 그림)에서 같은 식으로 맞출 것.

- **2026-10-24 (2)** — **[나] 타격감을 원본 값으로 — 히트스톱 공격별·피격 플래시 버그·화면 흔들림·화면 붉어짐·무적 깜빡임 (`feature/figma-stage1`, PlayMode 350/350).**
  - **계기**: 사용자 "2단계 전에 히트스톱·플래시 적용됐는지 + 타격감 효과 확인" → 원본과 전부 대조(히트스톱·플래시·넉백·흔들림·피해 숫자·
    파편·효과음·피격 연출·깜빡임·연쇄·체력바). 사용자 "1·2·3 적용, 나머지는 나중에".
  - **피격 플래시 버그 2개**: (1) 몹이 생길 때(Awake) "되돌릴 색"을 잡았는데 그땐 아직 프리팹 색(빨강)이라 — 스포너가 흰색·종류색을 칠하는
    건 그 뒤 — **한 번 맞으면 빨갛게 물들었다**. (2) 색을 흰색 쪽으로 섞는 방식이라 **흰색 그림(정식 아트)에선 안 보였다**. →
    `sr.color`는 안 건드리고 자식 렌더러가 몸 모양 흰 실루엣을 덮는다(`Resources/Shaders/SpriteFlash`, 세기·속도는 원본).
  - **히트스톱**: `IDamageable.TakeDamageWithKnockback`에 마지막 인자 `hitstop`(기본 0.045) → `CombatEvents.RequestHitstop` →
    `RunController.Hitstop`. 값은 `Core/HitFeel`. 처치 멈춤은 `RunController.KillFeel`(보스 0.25·성소 0.12·엘리트/일반 0.085).
  - **화면 흔들림**(`HitFeel.Shake` → `CameraFollow2D`가 따라가는 위치 위에 얹음, 히트스톱 중에도) · **화면 붉어짐**(`GameHud`) ·
    **무적 깜빡임**(`PlayerHealth`) — 원본 수치 그대로. 흔들림 세기만 화면 높이 비율로.
  - **안 한 것**: 섬영(팀원 파일 `BladeCombat`)의 멈춤 길이·흔들림 — 원본 값은 `docs/original-parity.md` 8-1에 적어 둠. 피해 숫자·몹 체력바
    (2단계 HUD·폰트와 같이), 파편·효과음(외주·재료 필요), 연쇄 처치 슬로모(묶음 5).
  - 테스트 `HitFeelTests` 11개 — 공격별 멈춤, 처치 연출 값, 플래시가 몹 색을 안 바꾸고 흰 실루엣으로 덮였다 사라짐, 흔들림 덮어쓰기·감쇠,
    카메라 흔들림 범위·복귀, 피격 붉어짐·감쇠, 무적 깜빡임. 셰이더가 실제로 그려지는지는 `CaptureStagePreview.RunFlashCheck`로 눈 확인.

- **2026-10-24 (1)** — **[나] Figma 1스테이지 화면 1단계 — C안 카메라 + 배경·땅·발판·캐릭터를 Figma 원본 그림으로 (`feature/figma-stage1`, PlayMode 338/338).**
  - **결정**: 10/23 미리보기(A Figma 그대로 / C 절충 / C+몹 2배 / 지금 카메라)에서 사용자가 **C안** 선택. 게임 이름은 **CHRONO SAVIOR**
    확정, UI는 Figma대로. 캐릭터는 전 직업 같은 그림(의도), 모션·스킬 이펙트는 아직 외주 전, 배경은 1스테이지만.
  - **카메라**(`World/CameraFollow2D` + `StageArtConfig`): 시작할 때 `PixelPerfectCamera`(URP 2D)를 붙인다 — 128px/유닛, 960×540,
    `UpscaleRenderTexture`, `StretchFill`. Y도 따라간다(서 있을 때 지면이 화면 아래 27.5%, 공중에선 머리·발이 화면 여유를 넘을 때만).
  - **그림**(`World/StageVisuals`): 씬 파일은 그대로 두고(충돌 방지) 씬이 열리면 땅·발판 판정 상자의 선을 끄고 Figma 타일을 한 장씩
    깐다(끝은 픽셀 단위로 잘라 판정 끝과 맞춘다). 배경은 3배로 깔고 카메라 범위를 그림 안쪽으로 옮겨 계산해 끝에서도 가장자리가 안 보인다.
    `SpriteRenderer` 타일 모드는 안 쓴다(캡처에서 엉뚱한 그림이 찍혔다 — 아래 캡처 주의).
  - **그림 가져오기**: `Editor/FigmaArtImporter`(Point·무압축·밉맵 없음·FullRect, `ground_*`만 64px/유닛). **모양을 2D로 박아야 한다** —
    메타가 빈 새 그림이 큐브맵으로 들어가 `Resources.Load`가 null이었다. 캐릭터 `char_*`는 100px/유닛·흐림 필터(Bilinear)였다 →
    128·Point·무압축(0.78배로 줄여 그리던 것이 1배가 됐다). `BuildPrototypeSprites` 메뉴도 `char_*`는 이렇게 들어가게.
  - **화면 확인 방법**: `Editor/CaptureStagePreview`(그래픽 있는 배치 실행, `-captureOut`) — 씬을 열어 960×540으로 찍는다.
    배치 편집 화면 렌더에선 **SRP 배처를 끄고 찍어야** 한다(켜 두면 모든 스프라이트가 첫 텍스처로 찍혔다 — 캡처할 때만 끈다).
  - 테스트 `StageVisualsTests` 9개 — 카메라 설정·Y 따라가기, 배경 가장자리, 타일 폭·윗면이 판정과 일치, 그림 픽셀/유닛·필터.
  - **같은 날 이어서**: 보스 포탈 = Figma 그림(32×64)을 **1.5배**로(사용자 결정, 1·1.5·2배 캡처 비교 후), 잠김은 어둡게·열림은
    그림 색 그대로 밝기만 깜빡. 발판 위 카메라는 지금 방식(서 있는 높이만큼 따라감) 유지 — "1층까지는 그대로"·"절반만" 안도
    캡처로 보여 드림(`CaptureStagePreview.RunCameraOptions`·`RunPortalSizes`).
  - **남은 것**: 2단계 HUD(Figma 640×360 — 초상화·빨간 바 = 체력·남은 초·콤보·스킬 칸·미니맵), 3단계 몹 11종 그림(⑤ 몬스터 템플릿과),
    몹 그림(`enemy_*`)도 아직 100px/유닛·흐림 필터.

- **2026-10-14 (1)** — **[나] 섬영 1~5층·아트(팀원, `main`)에 내 브랜치를 합쳐 `main`으로 (PlayMode 329/329).**
  - 팀원이 10/14 아침 `main`에 섬영 집중·칼날폭풍 1~5층(10/07)과 캐릭터·고블린·박쥐 아트(`sprite`)를 올렸다.
    내 `fix/mage-original-behavior`(캐릭터별 성장·SP +1·마법사 버그 5건·경험치 넘침·대붕괴 고리)를 합쳤다. 충돌 3곳:
    - `Combat/GravityWellZone.cs` — 중력점 높이 버그를 둘이 따로 같은 줄로 고쳤다(나 10/03, 팀원 10/07). 주석만 하나로.
    - `Core/PlayerProfile.cs` — 섬영 SP를 **섬영 성장 칸**으로 옮겼다(원본 `charProg('blade')`, 사용자 결정 "캐릭터별 분리는
      원본대로"). `bladeSpUsed`·`BladeSpAvailable`은 이름 그대로 프로퍼티가 돼 섬영 칸을 가리킨다. 시간 회귀는 원본대로
      SP 0, 봉인이 있으면 갈래·층만 유지. 그래서 팀원 테스트 3개(`TryLearnBladeTier_*`)의 준비 줄만 "섬영 칸 레벨"로 바꿨다.
    - `PROGRESS.md` — 양쪽 로그를 다 살렸다(내 09-30 (3)(4)는 팀원 번호와 겹쳐 (6)(7)로).
  - **병합 때 같이 넣은 것**: 로비 전문화 탭 **섬영**(원본 `SPEC.blade` 문구, 집중·칼날폭풍 1~5층 — 지금까진 게임 안에서
    섬영 스킬을 배울 방법이 없었다), 업적 섬영(원본 `spec1`·`specAll`에 섬영 포함 + `blade5` "질풍의 극의" `:823`).
  - 팀원 확인 요청 3건(`IGrabExempt`+`Boss`·`Shrine` 한 줄, `PlayerProfile` 섬영 필드, `BuildEnemyData` 아트 조건)은 받았다.

- **2026-10-07 (2)** — **[팀원] 중력장(GravityWellZone) 위치 버그 재발견 + 수정.**
  사용자가 플레이 테스트 중 "전에 중력장이 맞은 몬스터 위치가 아니라 땅에서 생성되는 거 고쳤었는데
  지금 보니까 안 고쳐졌다"고 보고. 원인 추적: 09-28에 `MageSkillEffects.DetonateGravityOrb`에서
  고친 것과 **완전히 같은 버그**(원본 `y=Math.min(y,groundY-20)`을 Y+가 위인 이 포트에서 부호 반전
  없이 그대로 옮겨서, 어느 층에서 터뜨리든 중력점이 맵 맨 밑바닥 아래로 끌려가던 것)가
  `GravityWellZone.SpawnOrMerge`(`Combat/GravityWellZone.cs:43`)에 **별도로 복사돼 있었는데 그쪽은
  안 고쳐진 채** 남아 있었다 — 그래서 피해 판정(OverlapCircle)은 맞는 위치에서 일어나는데, 실제로
  보이는 중력점 오브젝트만 계속 땅 밑에 생겼다. `DetonateGravityOrb`와 같은 수정(`Mathf.Min`→
  `Mathf.Max`, 부호 반전)을 적용. 기존 중력장 테스트들은 전부 Y좌표를 직접 비교하지 않아서 이 버그를
  못 잡았었다 — 회귀 방지용 테스트 `GravityWellZone_SpawnsAtHitFloorHeight_NotPulledToBottom`
  (`Tests/PlayMode/Characters/MageSkillTreeTests.cs`) 추가, 3층 발판 높이에서 터뜨렸을 때 중력점이
  그 높이 그대로 생기는지 검증.

- **2026-10-07 (1)** — **[팀원, 소유권 밖 수정] 캐릭터·고블린·박쥐 정식 아트 교체 + 적용 경로 신설.**
  사용자가 실제 캐릭터/몬스터 그림 파일을 구해 와서 교체 요청. `Assets/Sprites/Prototype/`의
  `char_mage`·`char_gunner`·`char_blade`·`char_druid`·`enemy_oni`·`enemy_wisp` PNG를 같은 파일명으로
  덮어쓰는 것까진 `docs/prototype-sprites.md` 절차 그대로였는데, **PNG만 바꿔선 게임에 반영되지
  않음을 발견** — `PlayerRig.characterSprites`(4칸)와 `EnemyData.sprite`가 2026-09-16 결정("일단
  스킨 씌우지 말고 네모 세모 동그라미로만")으로 코드에서 강제로 비워져 있었다
  (`PlayerRig.ApplySprite`는 배열이 비면 아무것도 안 함, `BuildEnemyData.Build()`는 매번
  `asset.sprite = null`을 강제 적용).
  - **[소유권 밖, 팀장 확인 필요] `Assets/Editor/BuildEnemyData.cs`**: `asset.sprite = null` 고정을
    `SpriteEnabledTypes`(HashSet) 조건부로 바꿔서, 정식 아트가 들어온 종류(지금은 고블린·박쥐)만
    `BuildPrototypeSprites.Load(...)`를 쓰고 나머지 5종(오우거·멧돼지·해골 궁수·슬라임·꼬마 슬라임)은 기존대로
    도형+색을 쓰게 했다. 한 종류 더 켜려면 그 HashSet에 `EnemyType.___` 한 줄만 추가하면 된다.
    `Enemies/`는 `docs/worksplit.md`상 팀장 담당이라 — 동작 자체는 메뉴(`ChronoSavior/Build Enemy
    Data`) 재실행만 하면 되는 1줄짜리 조건 변경이지만, 병합 전 팀장이 봐 줘야 한다.
  - **새 파일 `Assets/Editor/ApplyCharacterArt.cs`**: `PlayerRig.characterSprites`는 씬
    (`CombatCore.unity`)에 직렬화된 값이라 스크립트만 고쳐선 못 채운다. `BuildPartAScene.Build()`를
    다시 돌리면 씬 전체(바닥·발판·카메라·스포너·RunController까지)를 통째로 새로 지어 덮어쓰므로
    과하다고 판단해서, 열려 있는 씬의 Player만 찾아 `characterSprites` 4칸(마법사·메카닉·섬영·
    드루이드)을 `Assets/Sprites/Prototype/`의 `char_*` 그림으로 채우는 전용 메뉴
    `ChronoSavior/Apply Character Art (Player)`를 새로 만들었다. 실행 후 씬 저장(Ctrl+S) 필요 —
    `PlayerRig.Awake()`가 Play 시작마다 `characterSprites` 값으로 SpriteRenderer를 덮어쓰기 때문에,
    SpriteRenderer에 직접 드래그하는 방식은 Play를 누르는 순간 되돌아간다(실제로 사용자가 겪음).
  - **남은 것**: 오우거·멧돼지·해골 궁수·슬라임·꼬마 슬라임 5종은 아직 도형 그대로. 그림이 생기면
    `BuildEnemyData.cs`의 `SpriteEnabledTypes`에 한 줄씩 추가 + 해당 PNG를 같은 파일명으로
    덮어쓰면 된다.

- **2026-10-06 (2)** — **[나] 대붕괴 폭발 고리 — 원본 `gravityBurst` 그림(:5239) (PlayMode 281/281).** 사용자 "X를 눌러도 터진 게
  안 보인다" → `Combat/GravityBurstRing`: 보라 원판 + 0.90→1.08배로 퍼지는 고리(수명 0.42+순번×0.04초), 임계면 굵은 고리·
  도는 바퀴살 6개 + 바깥 고리(0.58초). 원본 가산 혼합 대신 보통 반투명. 넉백이 작은 건 원본도 같다(일반 16px·임계 26px,
  측정한 우리 값 0.14칸) — 버그 아님. 테스트 `GravityBurstRingTests` 2개.
- **2026-10-06 (1)** — **[나] 경험치가 `int`를 넘쳐 게임이 멈추던 버그 (`fix/mage-original-behavior`, PlayMode 279/279).**
  - **증상**: 사용자가 SP +1로 마법사를 32레벨까지 올리고 중력점을 모아 대붕괴(X)를 쓰자 에디터가 "응답 없음"이 됐다
    (몇 분 뒤 풀렸지만 그 사이 레벨이 이상한 값이 된다).
  - **원인**: 필요 경험치 `PlayerProfile.RequiredExp`가 `int`였다. 원본 곡선 `floor(700 × 1.8^(l-1))`은 27레벨에서 30억이라
    `int`를 넘고, 넘친 값이 음수가 돼 `AddExp`의 `while (exp >= 필요량)`이 끝나지 않았다 — 처치·구슬로 경험치가 들어오는
    순간 멈춘다. SP +1 버튼 때문에 드러났지만 **정상 플레이로도 27레벨이면 똑같이 났을 버그**다(구슬이 필요량의 25%라
    레벨이 올라도 레벨업 속도가 일정하다).
  - **고친 것**: 경험치·필요량·결과 화면 합계(`RunState.ExpEarned`)를 `long`으로, 필요량은 원본처럼 double로 계산한다(예전
    `float`는 3레벨이 2267 — 원본 2268, 15레벨부터 수~수천씩 어긋났다). 구슬 반올림도 JS식(.5 올림)으로 — 4레벨 구슬
    1020 → 1021(원본). `long`도 넘는 65레벨부터는 최댓값에서 멈추고(원본은 계속 커지지만 레벨업 속도는 같다), 1레벨
    미만(깨진 세이브)은 1레벨로 친다 — 둘 다 반복이 반드시 끝나게.
  - 테스트 `ExpCurveTests` 6개 — 전부 예전 코드였다면 실패한다(곡선 값, 27·32레벨 레벨업, `long` 끝, 구슬 반올림·30레벨 구슬).
  - 남은 같은 종류 차이: 처치 경험치 반올림(`Systems/EnemySpawner`)은 아직 C#식 — `docs/original-parity.md` 8-1.

- **2026-10-03 (1)** — **[나] 마법사를 원본(8/21 최종판)대로 바로잡음 — 동작 버그 5건 (`fix/mage-original-behavior`, PlayMode 273/273).**
  - **계기**: 사용자 "마법사 오류가 있다 / 모든 직업 스킬을 최종 HTML과 비교" → 전 직업 대조(결과는 `docs/original-parity.md`
    8-1). 원본은 이 PC의 요괴전선 HTML 중 최신인 `Downloads/project_test.html`(8/21)이고 `reference/project_test.html`과 바이트까지
    같다. 8/5판은 마법사 폭발 X가 "가장 가까운 각인 적 폭발"이었고 8/21판에서 순간이동(불길 이동)으로 바뀌어 사용자 기억과 달랐다.
    (`samurai-yokai-hunt/index.html`은 검·수리검·사슬낫·활의 다른 프로토타입이라 대조 대상이 아니다.)
  - **고친 것**: (1) 중력점 생성 높이 부호 — `Min`이라 늘 바닥 아래(-0.2)에 생겼다(09-28 `DetonateGravityOrb`만 고쳐졌었다).
    (2) 중력 노출이 초당 0.35씩 빠지게(원본 :1727). (3) 마법탄 지형 규칙 — 중력탄은 발판을 지나 바닥·벽·천장·적·수명에서만,
    폭발탄은 벽·천장·발판(원본 `groundYBelow`), 0차 탄은 지형 통과. 콜라이더 대신 좌표로 잰다(`FieldLayout.SurfaceBelow`).
    (4) 순간이동 착지 — 원본처럼 맵 안 0.28, 천장 아래, "지금 발 높이 아래의 발판·바닥"보다 위(밟던 발판을 못 뚫는다),
    불길 줄은 발+0.28. (5) 위·아래로 똑바로 쏜 탄은 바라보는 쪽에서 나가고 옆으로 밀지 않는다(원본 `kbDir: sign(vx)` = 0).
  - **정정**: 처음 대조 때 "폭발탄을 위로 쏘면 발판 밑면에서 터지는 게 원본과 다르다"고 했는데, 원본 식을 다시 계산하니 원본도
    발판 바로 밑(한 프레임 이동분 + 0.12 안)에서 터진다 — 터지는 자리만 발판 위(0.10)로 맞춘다. 그대로 옮겼다.
  - **천장은 원본 절댓값이 아니라 맨 위 발판 기준**: 층간 1.35 편차 때문에 원본 값(바닥 위 5.56·5.00)을 그대로 쓰면 우리 맨 위
    발판 윗면(5.475)보다 낮아진다 → 마법탄 6.685, 순간이동 6.125(`FieldLayout.ProjectileCeilingY`·`TeleportCeilingY`, 9절 편차에 적음).
  - 테스트 `MageOriginalBehaviorTests` 11개 — 각각 예전 동작이면 실패하는 값(중력점 높이, 노출, 발판 통과·천장 폭발, 착지, 탄 위치).
  - 남은 고칠 범위(묶음 2~6)는 위 "다음 할 일" 7번.

- **2026-10-01 (1)** — **[나] 최종 목표 변경 반영 + 작업 정리 (문서만, 게임 코드 변경 없음).**
  - **최종 목표 변경**(사용자, 9/30 회의 후): "원본 그대로 옮기기"가 아니라 **원본을 기준으로 회의에서 정한 개선을 반영**한다.
    원본과 같은 부분이 많으니 기본값은 원본 동작이고, 원본과 다른 부분은 짚는다. `CLAUDE.md` 작업 규칙 첫 항목·프로젝트
    상태, `HANDOFF.md` 머리말, `docs/worksplit.md` 1절, `docs/original-parity.md` 머리말을 고쳤다.
  - **작업 정리**: `feature/char-progress`를 백업으로 원격에 올렸다(`main`은 그대로 — 위 "다음 할 일" 4번).
    `main`에 다 들어간 로컬 브랜치를 지웠다(`git branch -d` — 합쳐진 것만 지워진다).
  - 팀원 섬영은 10/1 아침 기준 아직 원격에 없다.

- **2026-09-30 (7)** — **[나] 캐릭터별 레벨·경험치·SP — 원본대로 (`feature/char-progress`, PlayMode 262/262).**
  - **사용자 결정**: "캐릭터별 분리는 원본대로" — 09-29의 "골드까지 나눈다"를 바꿨다. 원본 `meta.charProgress`
    (`:1124`~`:1265`)는 **레벨·경험치·쓴 SP만** 캐릭터별이고 골드·골드 강화·시간의 파편·아이템·지역 진행·업적은 공용이다.
  - **구조**: `PlayerProfile.charProgress`(캐릭터 4칸, 원본 `makeCharProgress`) + `ProgressOf(id)`/`Progress`(원본 `charProg`).
    예전 필드 `level`·`exp`·`spUsed`는 **지금 고른 캐릭터 칸을 가리키는 프로퍼티**로 바꿨다 — 이름이 공유 계약이라 UI·보상·
    레벨 페널티·경험치 구슬·HUD·테스트 호출부가 하나도 안 바뀌고, 원본처럼 전부 "지금 캐릭터" 기준이 된다.
    `TryLearn*Tier`도 그대로다(원본 `learnSkill`도 `spAvail()` = 지금 캐릭터).
  - **시간 회귀**: 원본 `resetAllCharLevels()`·`resetAllCharSp()`처럼 **모든 캐릭터**를 1레벨·SP 0으로. **원본대로 바로잡은
    것 하나**: '각인의 봉인'이 있으면 쓴 SP까지 남기던 걸 0으로(원본은 봉인과 상관없이 `resetAllCharSp()` — 확인창 문구만
    "전문화 유지"다). 빌드(갈래·층)는 그대로 남는다. 그 동작을 고정하던 테스트(`KeepSpec_…`)도 원본대로 고쳤다.
  - **옛 세이브**: 맨 위 `level`·`exp`·`spUsed`를 `SaveService`가 **저장 당시 고른 캐릭터**에게 옮긴다(원본 `loadMeta` `:1234`).
    나머지 캐릭터는 1레벨부터. 안 옮기면 `JsonUtility`가 옛 값을 말없이 버린다.
  - **로비**: 캐릭터 탭에 캐릭터마다 `Lv · SP`(원본 캐릭터 카드 `:6418`·정보 패널 `:6424`), 머리말·전문화 탭에 누구 몫인지
    이름. 메카닉 테스트 메뉴는 메카닉 SP만 맞춘다(마법사 몫을 더하던 계산 제거). SP +1 버튼은 지금 캐릭터 레벨을 올린다.
  - 테스트: `CharacterProgressTests` 4개(캐릭터별 레벨·경험치·SP / 골드·강화 공용 / 회귀는 모든 캐릭터·봉인은 빌드만 /
    빈 칸 채우기) + `SaveServiceTests` 2개(캐릭터마다 저장·불러오기, 옛 세이브를 저장 당시 캐릭터로). 기존 테스트 2개는
    "캐릭터를 먼저 고른다"로 순서만 고쳤다(`SaveThenLoad_RestoresProgress`, `LearnGunnerTier_…`).
  - **팀원 영향**: `main`에 올라가면 섬영 브랜치가 `PlayerProfile` 변경을 같이 받는다 — 섬영 필드는 그냥 추가하면 된다
    (`docs/worksplit.md` 8절). 9/30 팀원 안내문의 "골드까지 나눈다·섬영 병합 뒤에 한다" 줄은 이걸로 바뀌었다.

- **2026-09-30 (6)** — **[나] 전문화 탭 "SP +1" 테스트 버튼 — 에디터 전용 (`feature/sp-test-button`, PlayMode 256/256).**
  - 사용자 요청(09-29): 스킬을 한 층씩 찍어 보며 테스트하려고. 로비 전문화 탭 SP 줄 밑 버튼 — 누를 때마다 레벨 +1
    (= SP +1, 원본 `spTotal() = level - 1` `:1258`). SP만 따로 더하는 칸을 세이브에 만들지 않으려고 레벨을 올렸다.
    경험치는 그대로라 다음 레벨 요구치 아래에 남는다. 누르면 바로 저장한다(스킬을 찍을 때처럼).
  - `#if UNITY_EDITOR`라 빌드엔 없다(`Editor/MechanicTestMenu`와 같은 취급 — CLAUDE.md의 "임시 디버그 키 금지"는 실제
    게임에 들어가는 것을 막는 규칙). 레벨이 오르면 몹 레벨 페널티가 줄고 경험치 구슬이 커지니 밸런스 볼 때는 쓰지 말 것.
  - 머리말 도우미(`DrawSpecHeader`)에 붙어서 마법사·메카닉 탭에 다 뜨고, 섬영 탭도 같은 도우미로 만들면 저절로 뜬다.
  - IMGUI 버튼이라 배치 테스트로는 못 누른다 — 컴파일과 기존 테스트만 확인했다. 화면은 에디터에서 눈으로 볼 것.

(2026-10-07 `main` 병합 중 재정렬: 09-29·09-30 날짜는 두 브랜치가 각자 (1)(2)...로 번호를
매겨서 겹쳤다 — 아래서 하나의 순서로 다시 번호 붙였다. 내용은 전부 그대로 보존, 번호만 바뀜.
09-28 이전은 원래부터 충돌이 없었다.)

- **2026-09-30 (5)** — **[팀원] 섬영 tier5(곡예비행·쿨타임 환급) 구현. `feature/blade-tier1`에
  이어 커밋, `main` 미병합. PlayMode 264/264 검증 완료(254 + 신규 10, 회귀 없음) — 이걸로 섬영
  1~5차 스킬트리(집중·칼날폭풍 두 갈래 모두) 완성.**
  - 사용자 지시 "넘어가자"(5단계, 마지막 층)로 착수. 원본 확인: `bladeFocusStart()`의 5층
    재시전-궤적파열 (`if(tier>=5 && trail.length>1) bladeRipTrail()`, :2526), `bladePassTick()`
    전체(:2594~2608, `F.cdRedPerPass=0.55`), `bladeStormUpdate()`의 5층 곡예비행 분기
    (:2662~2673, `S.hopVy=-720`·`S.hopSpdK=0.55`). `bladePassTick()`이 메인 루프 두 갈래(스토밍
    중/평상시) 양쪽에서 매 프레임 무조건 불린다는 것도 재확인(:2795~2815, :2865~2880) — `UpdateFalling`과
    같은 패턴이라 `BladeCombat.Update()`에 조건 없이 연결했다.
  - **Y축 부호 반전 주의(이번에도)**: 원본 `hopVy=-720px/s`(Y+아래 좌표계에서 음수=위로 솟구침)를
    이 포트(Y+가 위)에 맞춰 `+7.2`(=720÷100, 부호 반전)로 변환. `hopSpdK`(0.55)는 곱해지는
    `stormSpd`가 이미 포트 단위라 추가 스케일링 없이 그대로 사용.
  - **테스트 불가능 지점을 발견해서 프로덕션 코드를 리팩터링함**: 곡예비행은 점프키
    (`GameInput.JumpDown`)로 트리거되는데, `Core/GameInput.cs`를 읽어 확인해 보니 실제 Unity
    `Input`을 직접 읽는 얇은 래퍼라 테스트 주입 지점이 전혀 없다(이 프로젝트 다른 스킬 진입도
    전부 `Update()`를 안 거치고 `TryFocusStart(tier)`/`TryStormStart(tier)`처럼 조건을 매개변수로
    받는 private 메서드를 리플렉션으로 직접 호출해 검증하는 것과 같은 이유). 같은 패턴을 확장해
    곡예비행 실행부를 `TryStormHop(bool jumpPressed)`로 분리하고 `UpdateStorm`은
    `if (TryStormHop(GameInput.JumpDown)) return;` 한 줄만 남겼다 — 실제 동작(런타임에 `GameInput`을
    읽는 것)은 그대로고, 테스트는 `jumpPressed`를 직접 넘겨 도약 자체(쿨타임 반환·상승 속도 공식·
    칼날폭풍 종료·붙잡힌 적 낙하 전환)를 검증할 수 있게 됐다.
  - `Assets/Tests/PlayMode/Characters/BladeSkillTreeTests.cs`에 5층 테스트 10개 추가: 5층 재시전이
    남은 궤적을 찢는지(+4층 회귀), 패스 틱이 최고 속도로 스친 적마다 집중 쿨타임을 깎는지, 같은
    적은 0.5초 안에 중복 적용 안 되는지(+3층 회귀, +칼날폭풍 갈래 회귀), 곡예비행이 칼날폭풍을
    끝내고 쿨타임을 즉시 돌려주는지(+1층 회귀), 상승 속도가 `stormSpd`에 비례하는 공식대로
    나오는지, 곡예비행으로 종료할 때도 붙잡힌 적이 낙하 목록으로 넘어가는지(`EndStorm()` 경유
    확인).
  - **264/264 전부 통과(2026-09-30, 회귀 없음)** — 섬영 두 갈래(집중·칼날폭풍) 1~5차 스킬트리가
    기능적으로 완성됐다(로비 갈래 선택 UI는 이 몫 밖 — 위 "다음 할 일" 참고). 남은 건 `main` 병합
    전 팀장이 "소유권 밖 예외 2건"(`PlayerProfile.cs`, `IGrabExempt.cs`+`Boss.cs`+`Shrine.cs`)을
    확인하는 것뿐.

- **2026-09-30 (4)** — **[팀원] 섬영 tier4(궤적 파열·즉시 가속·강제 연행) 구현.
  `feature/blade-tier1`에 이어 커밋, `main` 미병합. PlayMode 254/254 검증 완료(242 + 신규 12,
  회귀 없음).**
  - 사용자 지시 "넘어가자"(4단계)로 착수. 원본 확인: `bladeFocusStart()`의 4층 즉시 가속
    (`p.vx = (p.runDir||p.facing) * bladeMaxSpeed()`, project_test.html:2529), `bladeRipTrail()`의
    4층 파열 폭발(:2570~2589, `ripR = bladeTrailR()*1.7`, `F.ripDmg=1.35`), `bladeStormUpdate()`의
    4층 강제 연행(:2691~2707, 반경 74px·최대 6명·랜덤 오프셋), `bladeStormEnd()`의 낙하 전환
    (:2630~2637), `bladeFallingUpdate()`(:2724~2737, 플랫폼 무시하고 낙하 후 착지 충격).
  - **착수 전 걸림돌**: 강제 연행은 보스·성소를 붙잡으면 안 되는데(`e.boss || e.shrine`, :2694)
    `Characters`가 `Enemies`의 구체 타입을 못 본다. 사용자에게 3지선다로 물어봤고 **"Core에 마커
    인터페이스 추가(추천)"**로 확정 — `Core/IGrabExempt.cs`(신규, 소유권 밖) + `Boss.cs`/`Shrine.cs`
    한 줄씩(위 "확인 필요" 참고, 팀장 확인 필요). `EnemyMove`엔 애초에 "붙잡힌 상태" 개념이 없어서
    (매 프레임 자기 AI로 움직임) 그 파일은 손대지 않고, 대신 칼날폭풍 활공(`WritePosition`)과
    똑같은 기법 — `LateUpdate`에서 매 프레임 붙잡힌 적의 `Rigidbody2D.position`을 직접 덮어써서
    그 프레임의 `EnemyMove.FixedUpdate` 결과를 이긴다 — 을 적 쪽에도 그대로 적용했다
    (`WriteEnemyPosition`). `Collider2D`/`Rigidbody2D`만 다루므로 asmdef 경계를 안 넘는다.
  - **4층 "즉시 최고 속도"는 완전히 못 옮기고 근사했다**: `CharacterMover2D.inertialVx`가
    private이라(팀장 소유, 안 고침) 속도를 순간이동시키듯 못 박는다 — 대신 캐스팅 시
    `pendingInstantAccel` 플래그를 세우고, 다음 `UpdateFocus` 호출에서 딱 한 번
    `AccelMultiplier`를 2000배로 올려 물리 스텝 한 번 안에 최고 속도까지 밀어붙인다(이미 `maxS`가
    상한이라 오버슈트는 없음). **알려진 한계**: `CharacterMover2D.ComputeInertialVx`가 이 배율을
    "같은 방향으로 계속 달리는 중"일 때만 보기 때문에, 이동 방향키를 누른 채로 캐스팅할 때만
    원본처럼 즉시 최고 속도가 나가고 무입력/반대 입력 중엔 평소 가속으로만 붙는다 — 그 파일을
    고치지 않고 낼 수 있는 최선으로 받아들였다.
  - **붙잡기 오프셋 Y축 부호 반전 주의**: 원본 `oy = rand(-24,12)`px(Y+가 아래)를 그대로 값만
    ÷100 하면 안 되고, 이 포팅(Y+가 위)에 맞춰 부호도 뒤집어야 한다 — `-24(원본 위쪽)→+0.24`,
    `+12(원본 아래쪽)→-0.12`로 변환했다(`BladeCombat.cs`의 `StormGrabOffsetYMin/Max` 주석 참고).
  - **원본과 다르게 받아들인 단순화 2건**: (1) 붙잡힌 적의 Y 클램프 — 원본 `clamp(p.y+oy, 60,
    groundY)`의 위쪽(60) 한도는 이 포트의 `FieldBounds`에 대응하는 천장 좌표가 없어 뺐다(바닥
    하한만 유지). (2) 원본 `e.kbx = 0`(붙잡는 동안 넉백 상태 리셋)은 `EnemyMove`의 private
    넉백 필드라 직접 못 지운다 — 매 프레임 위치·속도를 강제로 덮어쓰므로 잔여 넉백이 있어도 화면엔
    안 드러나 받아들였다.
  - 회전베기(`SpinHit`)의 기존 `skipGrabbed` 매개변수(1층부터 시그니처만 있었음)를 이번에 실제로
    구현 — 붙잡힌 적은 칼날폭풍 유지 틱에서 제외된다(:2486).
  - `Assets/Tests/PlayMode/Characters/BladeSkillTreeTests.cs`에 4층 테스트 12개 추가: 즉시가속
    배율이 캐스팅 직후 한 번만 걸리는지(+3층 회귀), 궤적 파열이 3층 반경 밖·4층 반경 안 적을
    맞히는지(+3층엔 안 터지는 회귀), 가까운 적이 붙잡히는지(+1층 회귀), `IGrabExempt` 적은 안
    붙잡히는지, 동시 최대 연행 수(6명) 제한, 붙잡힌 적이 회전베기 유지 틱을 피하는지, 칼날폭풍
    종료 시 붙잡힌 적이 낙하 목록으로 넘어가는지, 낙하한 적이 착지 시 피해를 받는지.
  - **1차 실행에서 254개 중 2개 실패, 둘 다 테스트 설계 문제였고 프로덕션 코드는 안 고침**:
    (1) `Focus_Tier3_DoesNotGetTier4InstantAccel`이 기대값을 `1.7`로 잘못 적어 놓음 — 3층은 이미
    2층 가속 보너스(`accelMul2`)를 포함해 `1.7×1.4=2.38`이 맞는 값인데(확인하려던 건 "4층
    즉시가속 2000배가 안 섞였는가"였음), 기대값을 1층 기본값으로 잘못 씀 → `1.7f*1.4f`로 수정.
    (2) `Storm_Tier4_..._DealsDamageOnLanding`/`..._EndReleasesGrabbedEnemyToFalling`이 착지
    지점 바로 옆에서 붙잡힌 적을 그대로 큰 dt로 착지시켰는데, 같은 프레임에 먼저 도는 2층 착지
    광역 피해(`StormLand`)가 그 적을 죽여서 "죽지 않은 적만 낙하로 넘긴다"(원본 `!e.dead`,
    `bladeStormEnd()`가 `bladeStormLand()` 다음에 도는 순서 그대로) 규칙에 걸려 낙하 목록에 안
    들어간 것 — 원본도 같은 순서라 정상 동작. 두 메커니즘이 한 시나리오에 얽혀 있던 게 문제라,
    `EndStorm()`을 리플렉션으로 직접 불러 해제 로직만 격리하도록 테스트를 다시 짰다. 재실행 결과
    **254/254 전부 통과.**
  - **다음**: 5층(곡예비행·쿨타임 환급) 착수 — 다음 세션 첫 일감.

- **2026-09-30 (3)** — **[팀원] 섬영 tier3(짙은 잔영·벽 반사) 구현. `feature/blade-tier1`에 이어 커밋,
  `main` 미병합. PlayMode 242/242 검증 완료(235 + 신규 7, 회귀 없음).**
  - **1차 확인 때 "미실행(Not Run) 7개"로 보고됨** — 정확히 이번에 추가한 신규 테스트 개수와 일치.
    콘솔에 컴파일 에러가 없는 걸 먼저 확인(사용자 확인)한 뒤, Unity Test Runner 창이 새로 추가된
    테스트 목록을 아직 못 읽어 온 것으로 추정 — 새로고침(창 껐다 켜기)하고 재실행하도록 안내했고,
    재실행 결과 242/242 전부 통과. 코드 문제가 아니라 에디터 UI 캐시 문제였다 — 다음에 테스트를
    새로 추가한 직후 "일부 테스트가 미실행으로 남는다"는 보고를 받으면 이 절차부터 확인할 것.
  - 사용자 지시 "넘어가자"(3단계)로 착수. 원본 확인: `bladeTrailLife/Dmg/R()`이 `bladeTier('move')>=3`
    이면 `t3Life`(1.45)·`t3Dmg`(1.8)·`t3R`(1.3)을 곱한다(project_test.html:2537~2539) — 지속시간은
    `bladeFocusStart()`가 캐스팅 순간 한 번만 계산해 담아 두지만(:2530), 피해·반경은 `bladeTrailUpdate()`
    가 매 틱 새로 계산한다(:2558) — 이 차이를 그대로 옮겼다(지속시간은 `TryFocusStart`가 시작 시
    한 번, 피해·반경은 `UpdateTrail`이 매 틱). 칼날폭풍 벽 반사는 `bladeStormUpdate()`의 벽 체크
    분기(:2681~2687) — `tier>=3`이면 `st.dir *= -1`로 반사하고 이번 프레임 나머지(회전베기 유지·
    착지 판정)가 그대로 이어지고, 아니면 원래대로 종료.
  - **원본과의 순서 차이를 하나 더 발견해서 이번에 같이 고침**: 원본은 `p.y`를 벽 체크보다 먼저
    무조건 갱신하는데(:2680→:2681), 1~2층 때 옮긴 `UpdateStorm`은 `pos.y` 갱신을 "벽에 안 부딪힌
    경우"로만 묶어 놨었다 — 지금까지는 벽에 부딪히면 항상 그냥 끝나 버려서 차이가 안 보였지만, 3층
    반사가 생기면 "벽에 부딪힌 바로 그 프레임에도 하강·회전베기 유지·착지 판정이 계속 이어져야" 하므로
    `pos.y` 갱신을 벽 체크 앞으로 옮기고 반사 후에도 로직이 계속 흐르게 고쳤다(`BladeCombat.cs` 주석
    "순서 주의" 참고). 기존 1~2층 테스트는 벽 근처를 아예 안 다뤄서 이 재배치로 깨지는 게 없다.
  - **칼날폭풍 3층에서 `bladeCrashImmune`의 나머지 절반을 실제로 열었다**: 2층 때 조건식(`집중≥2
    OR 칼날폭풍≥3`)은 이미 넣어 뒀지만, 칼날폭풍 중엔 물리 속도를 일부러 0으로 비우는 우리 포팅
    방식 때문에 `ComputeSpeedRatio()`가 항상 0을 내서 칼날폭풍 쪽은 죽어 있었다. `UpdateStorm`이
    매 프레임 `stormSpeedRatio` 필드에 직접 계산한 속도 비율을 담아 두고, `Update()`가 storming 중엔
    그 값을 대신 읽게 연결했다(원본은 `player.vx`를 그냥 직접 읽으니 이 구분이 필요 없지만, 우리는
    물리 엔진을 우회하는 기법을 쓰다 보니 생긴 포팅 전용 배선). 한 프레임 지연이 있지만
    `ApplyCrashImmunity` 자체가 이미 "감소 타이머로 상시를 근사"하는 방식이라 무시할 오차.
  - `Assets/Tests/PlayMode/Characters/BladeSkillTreeTests.cs`에 3층 테스트 7개 추가: 궤적 지속시간이
    3층부터 늘어나는지(+1층 회귀), 궤적 반경이 3층부터 커지는지(1층 반경 밖·3층 반경 안 딱 그 사이
    거리에 적을 둬서 반경 보너스만 격리), 칼날폭풍이 벽에서 3층엔 반사되고(방향 반전까지 확인) 1층엔
    그대로 끝나는지(회귀), 칼날폭풍 3층 최고 속도에서 실제로 무적이 걸리는지(리플렉션 호출이 아니라
    진짜 `yield return null`로 컴포넌트의 `Update()`를 한 번 돌려서 배선 자체를 검증).
  - **다음**: 4층(집중 즉시 가속+궤적 파열 폭발, 칼날폭풍 강제 연행) 착수 — 다음 세션 첫 일감.

- **2026-09-30 (2)** — **[나] `feature/gunner-tiers` → `main` 병합·push (메카닉 5층 + 로그 09-28 (5)~09-30 (1) 전부).**
  - **병합 전 확인**: 원격 `main`은 그대로(`82d9bb1`). 팀원은 9/29 15:33(한국 시간)에 `feature/blade-tier1`을 만들었지만
    커밋은 아직 없다(GitHub 활동 기록) — 섬영 1차를 로컬에서 작업 중일 가능성이 높아서 사용자 확인 후 올렸다. 팀원
    브랜치에 커밋이 쌓이기 전에 `main`을 받게 하는 쪽이 충돌이 가장 작다.
  - 병합 결과 코드는 `1e27aba`에서 배치로 돌린 PlayMode 256/256과 같다(그 뒤 커밋은 문서·주석뿐).
  - **팀원 안내**: 섬영 브랜치에 `main`을 먼저 받을 것 — 겹치는 파일과 푸는 법은 `docs/worksplit.md` 8절.

- **2026-09-30 (1)** — **[나] 문서를 노션 일정표 기준으로 정리 (게임 코드 변경 없음 — 테스트 주석 한 줄만).**
  - **계기**: 사용자 질문 "분업 문서가 아니라 내가 만들어 달라고 한 계획표를 따라가고 있는 것 아닌가" → 확인해 보니 맞았다.
    분업 문서의 진행 순서("단계 1 → 2 → 3")는 9/9 회의안에서 멈춰 있었고(머리말은 아직 "단계 1 ← 지금 여기"),
    9/23 노션 일정표는 저장소 어디에도 없었다(이 파일에 "10/14 프로토타입, 구두 합의" 한 줄뿐). 실제 작업(9/28
    섬영=팀원·메카닉=나 분담)은 일정표 ①을 따랐다. 게다가 **"단계 2"가 두 개였다** — 분업 문서의 단계 2(캐릭터별
    5층 = 지금 하는 일)와 일정표의 2단계(10/15~ 맵 템플릿부터). 그래서 "캐릭터별 레벨·SP 분리는 단계 2에서"를
    "나중에"로 읽게 됐다.
  - **한 일**: `HANDOFF.md`(단계 1 스펙) → `docs/sprints/05-all-characters-tier0.md`로 옮기고 완료 배너와 "나중에 바뀐
    결정" 표를 달았다. 새 `HANDOFF.md` = 스프린트 ① 직업별 스킬 구현. `docs/worksplit.md` 1절을 일정표(①~⑩, 날짜·
    저장소 상태)로 바꾸고, 9/28 분담(메카닉 = 내 표 11번, 섬영 1층~ = 팀원 표 C)과 팀원의 `PlayerProfile` 수정 예외를
    적었다. 7절 캐릭터별 진행 분리(골드 포함 결정, 시기 미정), 8절 병합 순서, 9절 범위 밖·미완 목록 갱신.
    `CLAUDE.md` "지금 프로젝트 상태"와 이 파일 "다음 할 일"도 일정표 기준으로. `CharacterKitTests` 주석의
    "`HANDOFF.md` 1번" → 아카이브 경로(CLAUDE.md "코드 주석에서 스펙을 인용할 땐 아카이브 경로" 규칙).
  - **발견한 불일치**: 일정표 ②·③·⑥~⑧이 저장소에 이미 있는 원본 이식본과 겹친다 → 위 "확인 필요".

- **2026-09-29 (3)** — **[팀원] 섬영 tier2(예리한 감각·칼날폭풍 낙하 충격) 구현.
  `feature/blade-tier1`에 이어 커밋, `main` 미병합. PlayMode 235/235 검증 완료(2026-09-30).**
  - **1차 실행에서 235개 중 1개 실패**: `Storm_FailsWhileGrounded`(tier2가 아니라 1층 때 만든
    기존 테스트, 로직 미변경) — `Expected: False, But was: True`(지상인데 칼날폭풍이 시작됨).
    원인은 코드가 아니라 테스트 타이밍: 바닥 콜라이더를 만들고 `yield return null;`(한 프레임)만
    기다린 뒤 바로 `IsGrounded()`(Physics2D.OverlapCircle)를 호출해서, 물리 씬에 콜라이더가 등록되기
    전에 판정이 돌아버릴 수 있었다. `PlayerAttackTests.Jump_RisesThenReturnsToGround`(:123-124)가
    이미 같은 이유로 `WaitForFixedUpdate()`를 두 번 기다리는 관례를 쓰고 있어서 그대로 맞춰
    고쳤다(`BladeSkillTreeTests.cs`, 프로덕션 코드는 무관). 재실행 결과 **235/235 전부 통과** —
    사용자 확인("문제없어").
  - 사용자 지시 "2단계로 넘어가자"로 착수. 원본 확인: `SPEC.blade.move/conv.tiers[1]`
    (project_test.html:938·945), `bladeMove`의 `accelMul * (t2 ? accelMul2 : 1)`(:2462),
    `bladeStormUpdate` 착지 분기의 `t≥2 → bladeStormLand()` 호출(:2644~2646), `bladeStormLand()`
    본체(:2640~2657), `bladeCrashImmune()`의 "집중 2층 또는 칼날폭풍 3층" 조건(:2444~2446).
  - `BladeCombat.cs`에 2층 상수 6개(`TopAtRatio`·`FocusAccelMul2`·`StormLandRadius`·
    `StormLandDmgMult`·`StormLandKnockback`·`StormLandYOffset`) 추가. `UpdateFocus`가
    `ProfileService.Current.bladeTier>=2`면 `accelMul2`를 한 번 더 곱하고, `UpdateStorm`의
    착지 분기가 같은 조건이면 새 `StormLand(speedRatio)` 메서드(피해·넉백만, 연출은 범위 밖 —
    프로젝트 관례)를 부른 뒤 `EndStorm()`한다. `Update()`에 `bladeCrashImmune` 계산 +
    `ApplyCrashImmunity(bool)`(= `ApplyDirectionalInvuln`과 같은 감소 타이머 근사 기법 재사용)도
    추가 — 조건식 자체는 원본과 동일하게 "집중≥2 OR 칼날폭풍≥3"을 다 넣어 뒀지만, 칼날폭풍 중엔
    `ComputeSpeedRatio()`가 물리 속도(칼날폭풍 중엔 일부러 0으로 비움)를 보므로 항상 0을 내 칼날폭풍
    쪽 절반은 3층을 만들 때까지 실질적으로 안 걸린다 — 코드 주석에 다음 손볼 지점으로 남겨 둠.
  - **알려진 한계(그대로 인정)**: 칼날폭풍 낙하 충격의 착지 지점은 `UpdateStorm`이 이미 계산해 둔
    `nx`(착지 순간 x좌표) 기준이라 정확하지만, 낙하 도중 회전베기 유지 틱과 착지 충격이 같은 프레임에
    겹치면(큰 dt) 둘 다 한 프레임에 들어갈 수 있다 — 원본도 프레임 단위 처리라 동일한 특성이고
    실제 플레이 dt(고정 프레임)에서는 문제 없다.
  - `Assets/Tests/PlayMode/Characters/BladeSkillTreeTests.cs`에 2층 테스트 6개 추가: 집중
    가속 배율이 `1.7×1.4=2.38`로 커지는지(+1층 회귀로 2.38이 안 나오는지), `ApplyCrashImmunity(bool)`이
    무적을 걸고/안 거는지, 칼날폭풍 착지 충격이 1층엔 안 들어가고 2층엔 들어가는지(회전베기 유지 틱
    간격 0.11초보다 짧은 dt=0.05로 착지시켜, 같은 프레임에 섞여 들어올 수 있는 회전 틱 피해와 착지
    충격 피해를 격리했다). SP 경제 테스트는 1층 것 3개를 그대로 두고 안 건드림 — 2층 동작 테스트는
    `TryLearnBladeTier`가 아니라 `ProfileService.Current.bladeBranch`/`bladeTier`를 직접 대입해서
    SP 세팅 없이 "그 티어에서 스킬이 맞는지"만 격리해서 봤다.
  - **다음**: 3층(집중 t3Life/t3Dmg/t3R 궤적 강화, 칼날폭풍 벽 반사) 착수 — 다음 세션 첫 일감.

- **2026-09-29 (2)** — **[팀원] 섬영 tier1(집중·칼날폭풍) 구현. `feature/blade-tier1`, `main` 미병합.**
  - 사용자 지시 "섬영 차례대로 시작해보자"로 착수. 원본 조사: `bladeTier(branch) = curTier('blade',
    branch)`(공용 `curTier` 헬퍼, project_test.html:1309·1315), `meta.skills.blade={branch,tier,
    element}`, `SPEC.blade.move/conv.tiers[]`(:933-949, 두 갈래 다 층당 SP 1~5), `learnSkill()`이
    갈래·SP·순서를 한 곳에서 검사(:7011) — 마법사와 완전히 같은 구조.
  - **착수 전 걸림돌 발견**: 갈래/티어/SP 저장 자리가 `Core/PlayerProfile.cs`(마법사 전용
    `mageBranch`/`mageTier`/`spUsed`) 하나뿐인데, `docs/worksplit.md` 7·9절과 `HANDOFF.md`가 이미
    "캐릭터별 SP/branch/tier 일반화는 단계2에서 메카닉 스킬트리와 같이 처리한다"고 못 박아 뒀고
    `Core/` 전체가 팀원 파일 목록엔 "읽기만"이다. 사용자에게 물어봤고(4지선다), **"내가
    PlayerProfile에 섬영 전용 필드 추가"**로 확정.
  - **`Core/PlayerProfile.cs`(소유권 밖, 팀장 확인 필요)** — `BladeBranch{None,Focus,Storm}` +
    `bladeBranch`/`bladeTier`/`bladeSpUsed`(마법사와 **별도 SP 풀**) + `TryLearnBladeTier()`(
    `TryLearnMageTier`와 완전히 같은 규칙: 갈래 최초 고정, 1→5 순서만, SP 부족하면 거부) 추가.
    마법사 필드는 전혀 안 건드림. `DoRegression()`(회귀)도 대칭으로 갱신 — 안 하면 회귀할 때
    마법사 스킬은 초기화되는데 섬영만 안 지워지는 비대칭 버그가 생겨서 같이 처리했다. 각인의
    봉인(keepSpec)도 마법사와 동일하게 섬영 진행을 유지시킨다.
  - **`Characters/BladeCombat.cs`(내 파일)** — 집중: X로 가속 배율(`CharacterMover2D.
    AccelMultiplier`, 그 파일이 이미 노출해 둔 공개 필드라 안 고치고 씀) 2초 상승 + 궤적(지나온
    경로) 3.2초간 0.22초마다 지속피해, 재사용 즉시 취소(궤적은 안 건드림), 쿨다운 11초. 칼날폭풍:
    공중 전용, 30도 대각선 활공 + 0.11초마다 회전베기 유지(흡혈 제외), 벽/바닥 도달·재사용·
    3.2초 경과 시 종료. **칼날폭풍의 "발판 통과"는 `CharacterMover2D.FixedUpdate`를 안 건드리고
    `LateUpdate`에서 매 프레임 `Rigidbody2D.position`을 직접 덮어써서(원본 `MageAttack.Teleport`와
    같은 기법을 반복) 구현** — 물리 충돌 자체가 안 걸린다. **좌표계 함정 주의**: 원본은 Y+가
    아래라 하강이 `y += vy*dt`인데, 이 포팅은 Y+가 위라 `y -= vy*dt`로 뒤집었다(이번 세션 중력장
    버그와 같은 종류 — 이번엔 처음부터 뒤집어서 씀).
  - **알려진 단순화(1층 한계, 나중에 보완 여지)**: (1) 칼날폭풍 지상 시전 시 원본은 'retry'(선입력
    유지)인데 여기선 그냥 무시. (2) 칼날폭풍 종료 후 원본은 활공 중 속도가 그대로 관성 이동에
    이어지는데, `CharacterMover2D.inertialVx`가 캡슐화돼 있어(그 파일 소유권 밖) 활공 자체 속도가
    아니라 활공 중 입력 기반으로 독립 계산된 관성 속도가 이어받는다 — 부드럽게 이어지긴 하나
    원본만큼 정확한 속도 승계는 아니다.
  - **기존 0차 테스트 호환 유지**: `SpinAttack(float)`을 `SpinHit(dmgMult,radius,skipGrabbed,
    lifesteal)`로 일반화(Z 공격·칼날폭풍 유지 틱이 공유)하면서, 기존 `BladeCombatTests.cs`가
    리플렉션으로 찾는 `SpinAttack(float)` 시그니처는 얇은 래퍼로 그대로 남겨 안 깨지게 함.
  - **`Assets/Tests/PlayMode/Characters/BladeSkillTreeTests.cs`(신규, 11개 테스트)** — SP 경제 3개
    (갈래 고정·순서, SP 부족 거부, **마법사와 SP 풀 분리 확인**), 집중 4개(1층 미만 무효, 가속
    배율 적용·지속시간 만료, 재사용 취소 시 궤적 유지, 쿨다운, 궤적 지속피해), 칼날폭풍 4개(1층
    미만 무효, 지상에서 실패, 공중에서 발동+하강, 바닥 도달 시 종료). **커밋 후 배치로 PlayMode
    재검증 — 228/228 통과(217 + 신규 11, 2026-09-29)**, 기존 테스트도 안 깨짐. 병합 전 팀장 확인
    (위 "확인 필요"의 `PlayerProfile.cs` 항목)만 남았다.
  - **로비 UI(갈래 선택 화면)는 여전히 없다** — `UI/LobbyScreen.DrawSpecTab`이 마법사 전용으로
    하드코딩돼 있고 UI는 이 담당 몫이 아니라(worksplit.md) 안 건드림. 실제 플레이에서 섬영 갈래를
    고를 방법이 아직 없다 — 이번 증분은 데이터/전투 로직까지만.

- **2026-09-29 (1)** — **[나] 전투 HUD 스킬 슬롯 — 모든 쿨다운이 보이게 (PlayMode 256/256).**
  - **계기**: 사용자 발견 "레이저 찍고 X 쓰면 쿨타임이 안 보인다" → "쿨타임 있는 캐릭터는 전부 보이게".
  - **모양은 Figma, 동작은 원본**: 왼쪽 아래 Z·X·C 세 칸. 틀·아이콘은 Figma "보스 HUD"(124:3)의 181:2~181:4
    그림(32×32)을 `Assets/Sprites/UI/skill_slot_*.png`로 받아 **정확히 2배**(64×64)로 그린다(좌표도 Figma ×2).
    쿨다운 표시는 원본 `setSlotCooldown`·`fmtCooldown`(:6244~:6254) 그대로 — 어두운 덮개가 아래에서 차오르고,
    남은 초(1초 이상 올림 정수, 미만 소수 한 자리)가 뜨고, 아이콘이 34%로 흐려진다. 메카닉 X 칸엔 충전 스택·부품
    `n/최대`(부품 0개면 붉게). 버프 줄에도 원본처럼 캐릭터 몫을 붙였다(마법사 차지율·폭발 빌드, 메카닉
    `충전 n/최대` 또는 `부품 · 적중 k/10 · 설치기 a/b · 링크`).
  - **구조**: 키트가 `Characters/ISkillSlotSource`(Z 칸·X 칸·버프 글)를 구현하고 HUD는 `PlayerRig.CurrentKit`에서
    읽는다. `ICharacterKit`에 넣지 않은 건 구현 안 한 키트(섬영)가 컴파일 에러 없이 그대로 돌게 하려는 것.
    덮개 비율의 분모는 원본 `p.ultCdMax`처럼 **쿨다운을 건 순간의 값**을 키트가 따로 들고 있다.
  - 그림 연결은 기존 Figma 그림과 같은 경로: `UiTextures` 필드 3개 + `BuildPartAScene` 연결 3줄 +
    `BuildUiTextures.Names` + **씬(`CombatCore.unity`)의 `UiTextures` 블록에 참조 3줄**(씬 재생성 없이 직접 추가).
    그림이 없으면 같은 색 도형으로 대신 그린다.
  - 테스트 `SkillSlotTests` 9개 — `fmtCooldown` 규칙, 레이저 드론 X 직후 덮개 100%·"11"·시간에 맞춰 감소,
    설치기 부품 표시·`noStack`, 0차는 스택 없음, 마법사 X 쿨다운, `CurrentKit`이 캐릭터 전환을 따라가는지 +
    **연결 검사 2개**(IMGUI·씬은 배치로 눈으로 못 보니 소스를 직접 본다): `GameHud`가 키트 슬롯 값·그림 3장을 실제로
    읽는지, 씬 `UiTextures`에 그림 3장의 guid가 꽂혀 있는지(빠지면 에러 없이 도형 대체 그림으로 조용히 바뀐다).
  - 마법사 차지 문구의 원본 앞 글자 '満'은 뺐다(한국어 윈도 기본 글꼴에 없는 일본 신자체 — HUD의 다른 줄도 '殺'·'⚔'를 뺀 상태).
  - 검증: 에디터가 꺼져 있었지만 같은 방식으로 `C:\dev\chrono-savior_verify`(worktree)에서 배치 전체 실행.

- **2026-09-28 (8)** — **[나] 보스를 잡은 다음 사냥이 입장 직후 "보스 격파"로 끝나던 버그 수정 + 메카닉 테스트 메뉴 (PlayMode 247/247).**
  - **원인**: 보스가 죽으면 `RunController`가 결과 화면을 **두 번** 예약했다 — `RunEvents.BossDefeated`(스포너 :422) →
    `bossDeadTimer`(멈춘 시간 기준, 사냥 시작·로비 진입 때 초기화)와 `CombatEvents.EnemyKilled`(보스) →
    `Invoke(EndRunAfterBoss, 1.6)`(**게임 시간 기준, 취소 안 됨**). 처치 히트스톱이 `timeScale = 0`을 걸고, 타이머
    분기가 히트스톱 해제보다 먼저 `return`하므로 `Invoke`는 한 번도 안 흐른 채 결과·로비를 지나 **다음 사냥에서
    시간이 흐르자마자** `EndRun(BossDead)`를 불렀다.
  - **수정**: 중복인 `Invoke` 경로(`EndRunAfterBoss`·`BossClearDelay`)를 없앴다. 보스 격파 → 런 종료는 타이머 한 곳뿐.
    회귀 테스트 `BossDefeatCarryOverTests` — 실제처럼 두 이벤트를 다 올리고, 결과 → 로비 → 다음 사냥이 2.5초 넘게 이어지는지.
  - **메카닉 테스트 메뉴**(`Assets/Editor/MechanicTestMenu.cs`, 사용자 요청 "테스트할 수 있게"): Play 중
    `ChronoSavior/메카닉 테스트/레이저 N층·설치기 N층·0차로 되돌리기·SP 넉넉히`. 진짜 세이브에 쓰고 SP 장부도 맞춘다
    (로비 전문화 탭에 그대로 보인다). 에디터 전용이라 빌드에 안 들어가고 게임 코드·씬은 무변경 — CLAUDE.md의 "임시
    디버그 키 금지"는 게임 안 입력 얘기라 해당하지 않는다.
  - 검증: 사용자가 에디터를 켜 둔 상태라 `C:\dev\chrono-savior_verify`(worktree)에서 배치 전체 실행.

- **2026-09-28 (7)** — **[나] 전문화 탭 버그 수정 + 메카닉 빌드를 로비에서 배우게 (PlayMode 246/246).**
  - **사용자 발견**: 메카닉을 골랐는데 전문화 탭에 마법사 트리가 뜨고, 사냥에선 메카닉 공격이 나가고, 로비로
    돌아오니 캐릭터 선택이 마법사로 돼 있었다.
  - **원인**: 선택 캐릭터의 출처는 세이브(`PlayerProfile.character`)인데 키트(`PlayerRig`)가 따로 놀 수 있었다 —
    ① 프로필이 통째로 바뀔 때(자동 불러오기 — `AutoSave.Awake`와 `PlayerRig.Awake` 순서 미보장, 타이틀 "새 원정",
    로비 "전체 초기화") 키트를 다시 맞추지 않았고, ② 이미 켜진 키트를 다시 고르면 `Select`가 프로필을 안 고치고
    끝났다. 전문화·캐릭터 탭은 프로필을, 사냥은 키트를 보니 서로 다른 캐릭터가 된다.
  - **수정**: `PlayerRig.Update`가 프로필의 캐릭터를 따라간다(키트 없는 캐릭터면 프로필을 지금 키트로 되돌림),
    `Select`는 같은 캐릭터여도 프로필을 다시 쓴다. 테스트 `PlayerRigProfileSyncTests` 3개.
  - **메카닉 전문화**: `PlayerProfile.gunnerBranch/gunnerTier` + `TryLearnGunnerTier`(원본 `learnSkill` 규칙, 층 비용
    1~5) — `GunnerBranch` enum은 세이브에 들어가야 해서 Core로 옮겼다. 시간 회귀 때 비우고 각인의 봉인이면 유지
    (원본 doRebirth는 모든 무기의 빌드를 비운다). 로비 전문화 탭이 **지금 고른 캐릭터**의 트리를 그린다(마법사/메카닉,
    그 외엔 "아직 없음"). `GunnerAttack`의 임시 인스펙터 필드(`branch`/`tier`)는 없앴다 — 이제 프로필만 본다.
  - 업적: 원본대로 `spec1`(어느 캐릭터든 1층)·`specAll`(어느 캐릭터든 5층)·`mechanic5` 추가 — 섬영 항은 트리가 생기면.
  - 같이 발견한 "보스 격파가 다음 사냥 직후 뜨는" 버그는 원인만 확인하고 아직 안 고쳤다(위 "확인 필요").

- **2026-09-28 (6)** — **[나] 플레이어 크기 1.00 → 0.50(몬스터와 1대1) + 공격 높이를 원본대로 "발" 기준으로 (PlayMode 241/241).**
  - 사용자 지시: "캐릭터가 너무 크다, 몬스터랑 1대1 크기로" → `EntitySizeConfig.PlayerHeight` 0.50(고블린 높이와 같음).
  - 씬에 `PlayerRig.spriteHeight: 1`이 직렬화돼 있어서 상수만 바꾸면 게임에 반영이 안 됐다 →
    `PlayerRig.Awake`에서 상수를 읽게 했다(씬 파일은 무변경).
  - **원본 `p.y`는 발이다**(`entRect` :1003). 그런데 Unity 기준점은 몸 중심이라 `p.y - 38`(메카닉 총구)·
    `p.y - 36`(마법탄)을 중심에서 재고 있었다 → **키 0.5 고블린 머리 위로 공격이 지나갔다**(크기를 줄이기 전에도).
    `Characters/PlayerBody.Feet`을 만들고 메카닉(총알·빔·설치기·드론)과 마법사(마법탄·아래 조준 중력점)를
    발 기준으로 바꿨다. 마법사 중력점은 "중심에서 0.34 **아래**"로 옮겨져 있던 걸 원본대로 "발에서 0.34 **위**"로.
    마법사 수정은 사용자 확인 후 진행("마법사도 고치고").
  - 테스트 +3: 메카닉·마법사가 같은 바닥의 고블린 크기 적을 정면으로 맞히는지, `Feet` 계산.
  - 검증: 사용자가 에디터를 켜 둔 상태라 `C:\dev\chrono-savior_verify`(git worktree, 분리된 HEAD)에 같은 변경을
    올려 배치로 전체 실행 — 241/241. (CLAUDE.md 협업 규칙 7 "다른 폴더에서 실행" 방식)
  - 아직 발 기준으로 안 바꾼 곳(영향 작음): 마법사 순간이동 궤적(원본 `p.y - 28`, 키 0.5에선 차이 0.03)·
    대붕괴 탐색 중심(원본 `p.y - 42`, 탐색 반높이 4.2라 영향 미미). **섬영 키트도 원본 `p.y - N`을 쓰면
    `PlayerBody.Feet`에서 잴 것.**

- **2026-09-28 (5)** — **[나] 단계 2 착수: 메카닉 스킬트리 로직 (`feature/gunner-tiers`, PlayMode 238/238).**
  - 사용자 지시: "메카닉 로직만". 팀원이 섬영 작업 중이라 **`Characters/Gunner*`와 새 테스트 파일만** 만들고 고쳤다 —
    이동(`CharacterMover2D`)·도형·마법사·`PlayerProfile`·`LobbyScreen`·씬·`BuildPartAScene`은 무변경.
  - 원본 `SPEC.gunner`(project_test.html:915) 두 갈래 5층 전부를 코드 기준으로 옮겼다(설명문과 다른 곳은 코드를 따름 —
    설치기 1층부터 3개·링크·장판, 충전 스택은 1층부터 드론 지속시간으로 전환):
    - **레이저 빌드**: Z를 누르는 동안 관통 광선(`updateGunnerBeam` :2224) — 3층부터 콘 안 적에게 유도, 5층 곡선.
      적중마다 충전 스택 +1. X는 스택을 전부 써서 레이저 드론 소환(`summonLaserDrone` :2363, 5층 2기, 4층부터 추적탄).
    - **설치기 빌드**: Z는 0차 총알 + 적중 10회마다 부품 1개(런 시작 시 1개). X는 부품 1개로 설치기(`placeGunnerTurret`
      :2349, 한도 3/4/5, 초과 시 가장 오래된 것 교체). 2개 이상 링크되면 연결선 위·닫힌 도형 안에 장판 피해(:3766).
    - 수치는 전부 `GunnerSpecConfig` 한 곳. 새 파일: `GunnerSpecConfig`·`GunnerLaserBeam`·`GunnerDrone`·`GunnerTurret`·
      `GunnerField`·`GunnerTargets`. `GunnerBullet`에 유도·스택 적립 추가, 관통을 다 쓴 뒤 같은 물리 스텝에
      한 번 더 맞던 문제도 막았다(원본은 적중 즉시 제거).
  - **테스트 21개 신규**(`GunnerSkillTreeTests`) — 수치가 아니라 실제로 적이 맞는지로 확인: 빔 사거리·등 뒤 제외·
    3층 유도·5층 곡선, 스택/부품 규칙, 드론 소환·교체·사거리, 설치기 부품 소모·위치·쿨다운·한도 교체·반경,
    링크/내부 장판(링크 한도 밖이면 연결 안 됨), 볼록 껍질·점-다각형 판정. 배치로 **PlayMode 전체 238/238 통과**.
  - 씬 재생성(`BuildPartAScene.Build`)은 **돌리지 않았다** — 씬은 이번에 바뀔 게 없고, 돌리면 씬 파일이 통째로 바뀐다.


- **2026-09-28 (4)** — **`feature/blade-tier0` → `main` 병합 완료. 단계 1 종료.**
  - 사용자가 팀장한테 소유권 밖 수정 3건(`PlayerHealth.Heal`·`MageSkillEffects.cs` 중력장 버그·
    `BuildPartAScene.cs` 섬영 부착)을 확인받고("확인했어") GitHub Desktop으로 병합 진행.
    커밋은 작업별로 4개로 나눠서 올림(섬영 0차 구현 / 중력장 버그 수정 / 씬 부착 / 문서).
  - 병합 후 배치로 PlayMode 전체를 다시 돌려 **217/217 통과 재확인**(중간에 Unity 에디터가 열린
    채로 배치를 돌려 테스트가 멈춘 적이 있었는데, 에디터를 닫고 재실행해서 해결된 것으로 보인다).
  - **이로써 단계 1(모든 캐릭터 0차 + 모든 몬스터)이 종료됐다** — 드루이드 제외, 나머지는 전부
    `main`에 있다. 다음은 단계 2(캐릭터별 5차까지) — 팀원 쪽은 섬영 tier1+, 팀장 쪽은 메카닉
    스킬트리.

- **2026-09-28 (3)** — **[팀원, 소유권 밖 수정] `BuildPartAScene.cs`에 `BladeCombat` 부착 — 사용자 지시로 진행.**
  - 사용자가 "컴포넌트 부착 지금 하자"고 직접 지시. `Assets/Editor/BuildPartAScene.cs`를 열어보니
    마법사·메카닉 키트를 붙이는 바로 그 자리에 "섬영·드루이드 키트(팀원 작업)가 생기면 여기에
    같이 붙일 것"이라는 주석이 이미 있었다 — 팀장이 미리 자리를 비워둔 지점.
  - `go.AddComponent<BladeCombat>();`를 그 자리에 추가. 드루이드는 기획 제외라 더 안 붙인다는
    주석도 같이 남겼다. `rig.characterSprites`/`characterColors` 배열은 이미 4칸(캐릭터 4종) 다
    채워져 있어 손댈 게 없었다(섬영 색은 이미 "흰빛"으로 지정돼 있었음).
  - **`Assets/Scenes/CombatCore.unity`·`Assets/Editor/BuildPartAScene.cs`는 `docs/worksplit.md` 8절상
    팀장 전담 파일이다** — 소유권 밖이지만 사용자가 명시적으로 지시해서 진행했다. `main` 병합 전
    팀장 확인 필요.
  - 사용자가 Unity 에디터에서 `Tools > ChronoSavior > Build Part A Scene` 실행 + 씬 저장까지 마침 —
    **로비 캐릭터 선택에 섬영이 정상적으로 뜨고 고를 수 있는 것까지 확인됨("잘 나오네", 2026-09-28).**
    섬영 0차가 처음으로 실제 씬에서 눈으로 확인됐다 — 관성 이동/회전베기 실플레이 검증은 아직
    안 했으니 다음에 직접 움직여보고 이상 있으면 알려줄 것.

- **2026-09-28 (2)** — **[팀원, 소유권 밖 수정] 중력장이 항상 맵 맨 밑바닥에만 생기던 버그 수정.**
  - 사용자가 실제 플레이 중 발견: 마법사 중력 빌드로 어느 층에서 중력탄을 터뜨려도 중력장이 항상
    맵 1층 바닥에만 생김.
  - **원인**: `MageSkillEffects.DetonateGravityOrb`의 착탄 지점 Y 클램프가 원본 JS
    `y = Math.min(y, groundY - 20)`(project_test.html:2023 부근)를 부호 그대로 베껴왔는데, 원본은
    **Y+가 아래인 캔버스 좌표계**라 이게 "바닥 아래로 못 내려가게" 막는 상한이었다. 이 Unity 포팅은
    Y+가 위(`FieldBounds.GroundY = 0f`, 발판이 위로 갈수록 Y가 커짐)인데 부호를 안 뒤집고
    `Mathf.Min`을 그대로 써서, `GroundY - 0.2 = -0.2`가 모든 유효 Y(≥0)보다 항상 작으니 **폭발
    지점이 무조건 -0.2로 강제되고 있었다** — 몇 층에서 터뜨리든 결과가 같았던 이유.
    같은 파일 안 `MageAttack.cs`의 순간이동 착지 클램프(`Mathf.Max(toPos.y, FieldBounds.GroundY)`)는
    이미 올바르게 방향을 뒤집어 놨던 걸 보고 패턴을 확인했다.
  - **수정**: `Mathf.Min(pos.y, GroundY - clearance)` → `Mathf.Max(pos.y, GroundY + clearance)`.
    "바닥보다 아래로는 못 내려간다"는 원본 의도는 그대로 유지, 실제 착탄 층 Y는 이제 그대로 쓰인다.
  - 기존 `MageSkillTreeTests`(중력 관련 5개)는 착탄 Y값 자체를 검증하지 않아(개수·hp·스택만 확인)
    이 수정으로 깨지지 않는 것 확인. **배치 재검증은 아직 안 함 — 사용자 확인 필요.**
  - **`docs/worksplit.md`상 팀원 담당 파일이 아니다**(`Combat/MageSkillEffects.cs`는 마법사 스킬,
    팀장 소유) — 사용자가 "바로 고쳐도 문제없으면 고쳐달라"고 명시적으로 지시해서 진행했다.
    `main` 병합 전 팀장 확인 필요.

- **2026-09-28** — **[팀원] 섬영 0차 재작성 (`feature/blade-tier0`) — 드루이드는 기획 제외, 구 브랜치 폐기 (PlayMode 217/217).**
  - **드루이드가 기획에서 빠졌다**(사용자 확정) — `DruidAttack.cs`는 더 이상 만들 필요가 없다.
    단계 1 남은 조각은 이제 섬영 하나뿐.
  - **구 브랜치(`feature/blade-druid-tier0`) 작업이 사라져 있었다.** 09-12·09-23·09-28 세 번의
    확인 시점마다 로컬 체크아웃을 직접 대조했는데, 어느 시점부터 `BladeCombat.cs`·`DruidAttack.cs`·
    `PlayerHealth.Heal()`이 전부 없어지고 로컬이 09-12 이전 `main` 상태로 되돌아가 있었다
    (원인 불명 — 리베이스/체크아웃 실수로 추정되나 확정 못 함). 복구를 더 시도하는 대신
    **사용자 판단으로 그 브랜치는 버리고 지금 `main` 기준으로 새로 쓰기로 함** — 사용자가 GitHub
    Desktop에서 구 브랜치를 삭제하고 `main`에서 `feature/blade-tier0`를 새로 만듦.
  - **재작성 전 공유 파일 재확인**: `PlayerRig.cs`가 팀장 쪽에서 바뀌어 있었다(스프라이트 표시용
    `ApplySprite()` + `SelectNext()` 순환 전환 추가) — `ICharacterKit` 계약 자체는 그대로라 섬영
    구현엔 영향 없음. `CharacterMover2D`/`ICharacterKit`/`CharacterId`는 무변경. `PlayerHealth.cs`는
    예상대로 `Heal()` 없는 원본 상태로 되돌아가 있었다.
  - **`BladeCombat.cs` 재작성** — 원본 `bladeMove`/`bladeSpinTick`/`bladeLifesteal`/`bladeDmgMult`/
    `bladeDirInvuln`(project_test.html:2420~2517)을 줄 번호 다시 대조해서 옮겼다:
    관성 이동은 `CharacterMover2D`의 Inertial 모드가 맡고 이 키트는 매 프레임 `MoveScale`(이속 캡,
    `bladeMsK`)만 넣는다 · 회전베기(Z, cd0.13/dmg0.42/r1.05) · 속도→피해(정지×1→최고속×2.4,
    100% 넘긴 이속은 surplus로 추가 환산) · 흡혈 15%(회전베기 총 피해 기준) · 방향키 상시 피격무적
    (티어 무관 — `GrantInvuln`을 매 프레임 갱신하는 근사, 한계는 코드 주석에 명시).
    **재작성하며 하나 더 반영함**: `Core.CombatModifiers.MoveSpeedMultiplier`(성소 버프의 플레이어
    이속 배수)가 이미 존재하길래 `bladeMsRaw` 계산에 곱해 넣었다 — 예전 버전엔 없었을 수도 있는
    항목이라 참고용으로 남긴다.
  - **`PlayerHealth.Heal(float)` 다시 추가** — `min(maxHp, hp+amount)` 클램프뿐인 최소 구현(이전과
    동일한 결정, 사용자 확인 이력 있음). **팀장 확인 필요**(공용 파일) — 아래 "확인 필요" 참고.
  - **`BladeCombatTests.cs` 7개 신규**(리플렉션으로 `SpinAttack(float)`/`SpeedDamageMultiplier(float)`/
    `ApplyDirectionalInvuln(bool)` 직접 호출 — `Z`/방향키 실제 입력은 시뮬레이트 불가라
    `GunnerAttackTests`/`PlayerAttackTests`와 같은 관례를 따름): 캐릭터/이동모드 선언, 공격속도에
    따른 쿨다운 스케일, 정지·최고속 피해배수(1×/2.4×), 반경 안 피해 + 스폰무적/사거리밖 스킵,
    흡혈량(피해 합계의 15%), 방향키 홀드 시 무적 부여.
    **사용자가 배치로 PlayMode 전체를 돌려 확인 — 217/217 통과, 컴파일 에러 없음(2026-09-28).**
  - **아직 안 한 것**: 씬에 `BladeCombat` 실제 부착(팀장 소유 파일), `main` 병합(팀장 확인 후).
    Oct 14 프로토타입 완성 목표 하에서의 전체 일정 재조정은 팀 연락 결과가 오면 다시 정리.

- **2026-09-12** — **원격 브랜치 정리 + 팀원 진행 확인.**
  - `origin/feature/run-cycle-impl` 삭제. 이건 **내가 09-10 새벽에 만든 WIP 세이브포인트**였다
    (컴파일이 깨진 상태로 중간 저장 + 이어서 할 일을 커밋 메시지에 남긴 것). 그 뒤 런 사이클을
    제대로 완성해 `main`에 병합했으므로(`9d5dd80`·`808e323`) 고유 커밋이 0개다.
    삭제 전에 `git merge-base --is-ancestor`로 `main`이 해당 커밋을 포함하는지 확인했다.
  - **정정**: 09-10 문서에 이 브랜치를 "팀원이 올린 마지막 흔적일 수 있다"고 적었는데 **틀렸다.**
    작성자가 내 계정이었고, 같은 날 확인한 팀원 커밋 8개 목록에도 없었다.
  - **팀원 진행 상황**: 원격 브랜치·PR·포크·전체 ref를 훑어본 결과 **섬영·드루이드 작업은 아직
    올라오지 않았다.** `Blade*`/`Druid*` 파일은 모든 ref를 통틀어 한 번도 추가된 적이 없고,
    팀원 마지막 커밋은 **2026-09-08**(`1d940d6`)이다. PR 0개, 포크 없음.
  - 남은 stale 브랜치 4개는 전부 팀원 것이라 손대지 않았다.

- **2026-09-10 (4)** — **원본 편차 10건 수정 · UI 가독성 · 덩치 정리 (PlayMode 196/196).**
  - **감사에서 한 가지 패턴이 보였다: 10건 중 5건이 "값은 정의해 놓고 호출부를 안 만든 것"이었다.**
    쿨감 아이템·살기 공속·성소 스폰 보호·보스 레벨 하한·구운 그림 3장. 상수도 함수도 멀쩡히 있으니
    **컴파일러도 기존 테스트도 아무 말을 안 한다** — 아무도 "그 값을 쓰는지"는 안 보기 때문이다.
    → `OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`가 **소스를 직접 훑어** 정의 파일
    밖에 호출부가 있는지 본다. 리플렉션으로는 못 잡는다(안 불리는 메서드도 타입엔 멀쩡히 존재한다).
    이 검사가 처음 돌자마자 하나 잡았다 — 목록에 넣은 `SplitSpawnProtect`가 같은 파일 안에서만
    쓰여서, "정의와 사용처가 같은 파일인 건 넣지 않는다"는 기준을 주석에 못 박았다.
  - **고친 편차**: 보스 레벨 페널티 하한(원본은 보스만 0.5, `:1652`) · 쿨감 아이템 미연결 ·
    구슬 경험치 배수 누락(`:4466`) · 웨이브 상한이 보스·성소까지 셈(`:3978`은 둘을 제외) ·
    보스 부하가 슬라임 꼬마 슬라임용 0.35초 보호를 받음(원본은 기본 2초, `:4278`) ·
    성소 4건(스폰 보호 미적용 / 보스전에도 등장 / 보호 중에도 적 버프 / 살아 있으면 타이머 정지).
  - **작업 중 테스트가 두 개를 더 잡았다**: ①스폰속도 아이템을 보스전 잡몹 간격에도 곱하고 있었다
    (원본 `:4444`는 `minionInterval`을 그대로 쓴다). ②보스전 잡몹 게이트가 원본과 구조가 달랐다 —
    원본은 `if (잡몹 < 4) spawnWave(2)`인 **문지기**인데, 상한을 웨이브 크기로 넘기고 있었다.
  - **`EnemyMove.isStructure` 추가** — 성소를 `enabled = false`로 세웠는데, 원본은 `spawnInvuln`을
    깎은 **뒤에** 성소를 건너뛴다(`:4023`→`:4024`). 컴포넌트를 끄면 Update가 안 돌아 스폰 보호가
    영영 안 풀린다.
  - **UI 가독성**(사용자 지적: *"로비 버튼도 투명하고 흰 글씨인데 배경이 몬스터 잡는 화면이라 보기 힘들다"*) —
    IMGUI 기본 스킨이 **밝은 판에 검은 글씨**를 전제로 만들어져 있는 게 원인. 원본이 이 문제를 푸는
    방식은 **자리마다 다르다**: ①읽어야 할 덩어리는 판으로 덮고(로비 본문 `rgba(14,11,20,.92)` `:180`,
    결과 화면은 화면 전체 `rgba(5,4,10,.88)` `:223`), ②덮을 수 없는 헤더·HUD는 글자에 그림자
    (`text-shadow:0 1px 2px #000`), ③HUD 수치는 바로 보여준다(바 자체가 작은 대비 영역이 된다).
    HUD에 판을 깔면 정작 봐야 할 사냥터가 가려지니 셋 다 필요하다. → `UI/UiTheme`.
  - 결과 화면은 가운데 420×260 상자에서 **화면 전체**로 바꿨다(원본 `#result`는 `inset:0`).
    막은 디자인 좌표가 아니라 **실제 화면 좌표**로 그린다 — 16:9가 아니면 레터박스로 사냥터가 비친다.
  - HUD는 원본 배치 그대로(좌상단 바 2개+정보줄, 상단 가운데 타이머, 우상단 콤보, 우하단 안내).
    콤보 라벨은 콤보 수가 아니라 **지금 배수**를 보여준다(원본 `:1627`). 처치 줄에 지역 토벌
    진행도가 붙었다(`:6271`).
  - **덩치**(사용자 지적: *"성소가 너무 작다 / 몬스터는 반으로 / 플레이어는 1.5배"*) —
    성소가 작아 보인 진짜 원인은 프리팹 기본 스프라이트가 **32×32짜리 내장 이미지**라, 거기 맞춰
    손으로 고른 `localScale`(1.3×1.9)이 실제로는 0.42×0.61유닛이었던 것 — **고블린보다도 작았다.**
    값이 세 군데(EnemyData·PlayerRig·스포너)에 흩어져 있어 비교가 안 되던 게 근본 문제라
    **`Core/EntitySizeConfig` 한 곳으로 모았다.** 몹은 원본 크기(반지름 0.25), 성소·보스는 원본
    크기(0.64×0.96 / 1.30×1.50), 플레이어만 사용자 지정 1.5(원본의 2.7배 — 문서에 명시).
  - 문서 정리: `HANDOFF.md` 재작성(절 번호가 1→9→8→7로 꼬여 있었고 "범위 밖"이 전부 옛날 내용),
    `docs/worksplit.md`에서 끝난 "로비 — 다음 작업" 절 삭제 + 확정 사항 갱신 + 신규 계약 3건 추가.

- **2026-09-10 (3)** — **성장 고리 완성: 구슬 · 성소 · 지역진행+보스 · 스프라이트 · 낙하/보스무대 ·
  시간 회귀 · 아이템/가챠 · 업적 (PlayMode 187/187).**
  - **경험치 구슬** — 50마리째마다 드랍. 획득량이 고정값이 아니라 **현재 레벨 필요 경험치의 25%**라
    후반에도 의미가 유지된다.
  - **성소** — 버프 방향이 둘이다: 살아 있으면 **적**이 강해지고, 부수면 그 힘이 **플레이어**에게
    15초간 넘어온다. 한쪽만 넣으면 "빨리 부술수록 이득"이라는 긴장이 사라진다.
  - **지역 진행도 + 보스** — 100마리 토벌 → 보스 해금 → 격파 → 다음 지역. 임시 전체 개방
    (`debugUnlockAllRegions`)을 **삭제**했다. 보스는 발판 위로 도망가면 원거리로 전환하고,
    내려찍기는 충격파 + 발밑 즉시 판정을 둘 다 한다(둘 중 하나만이면 발밑이 안전지대가 된다).
  - **프로토타입 그림** — 원본엔 이미지 파일이 0개다(전부 Canvas 드로잉 코드). 로컬 HTTP로 띄워
    `draw*()`를 호출하고 알파 크롭해 PNG 14장으로 구워냈다. 콜라이더와 스프라이트가 같은
    GameObject에 있어 `localScale`이 둘 다에 걸리므로, **콜라이더 radius를 역으로 나눠** 월드
    반지름을 유지한다. 절차: `docs/prototype-sprites.md`.
  - **아래키 관통 낙하** — 4층 맵인데 내려올 방법이 걸어서 떨어지기뿐이었다. 레이어를 끄면 적까지
    통과하고 플레이어 콜라이더를 끄면 바닥까지 빠져서, **밟고 있는 원웨이 발판만** 콜라이더 쌍으로 무시한다.
  - **보스 전용 무대** — 맵 2600→1800, 발판 15→3개. 좁은 무대에서 피하는 게 보스전 설계다.
    바닥 스폰 그리드도 맵 폭을 따라 줄어든다(고정 배열이면 맵 밖에 스폰된다).
  - **시간 회귀** — 입장료·몹 체력이 지수라 한 생의 강화만으로는 반드시 벽에 부딪히고, 그 벽을 넘는
    유일한 수단이다. 장벽은 **세 개가 동시에** 걸린다(몹 체력 ×2.8 · 몹 피해 ×1.45 · 내 피해 ×0.62).
  - **아이템 35종 + 가챠** — 합연산과 곱연산을 갈라 집계한다(섞으면 후반 배수가 완전히 달라진다).
    가챠는 **등급을 먼저 뽑고 그 안에서 균등** — 등급이 곧 확률 손잡이다. 천장 20회.
    조건부 특수 아이템 11종은 피격 순간의 대상/플레이어 상태가 필요해서 아직 미연결(커밋 메시지에 목록).
  - **업적 32종** — 누적 통계는 회귀해도 안 지워진다. 판정할 수 없는 업적(메카닉·섬영 5층)은
    목록에서 뺐다 — 항상 거짓인 줄이 섞이면 버그인지 미구현인지 구분이 안 된다.
  - **원본 재대조로 잡은 편차 2건**: 살기(fury) 공속이 **어디에도 안 붙어 있었다**(원본은 `statAs()`
    안에 있다 — 스탯에 넣으니 앞으로 들어올 섬영·드루이드도 자동 적용). 첫 웨이브가 원본보다 3초 늦었다.

- **2026-09-10 (2)** — **전투 배수 체인 + 세이브/로드 (PlayMode 130/130).** 섬영·드루이드가
  붙기 전에 해두면 이득인 기반부터 처리했다 — 모든 캐릭터의 피해가 `DamageCalculator` 한 곳을
  지나가므로 **키트 쪽은 아무것도 안 해도** 콤보·살기·레벨페널티가 적용된다.
  - `Core/CombatModifiers` — 콤보(3초 창, 피해 +1.2%·골드 +0.8%/콤보) · 살기(처치당 피해 +0.8%,
    공속 +0.4%, 런 내내 누적) · 연쇄 처치(0.8초 안 3킬부터 골드 보너스) · 레벨 페널티 · 성소 버프 수신.
  - **레벨 페널티가 핵심 변화다** — 내 레벨이 몹보다 낮으면 레벨당 -5%(하한 25%)이고 **높아도 이득은
    없다**(상한 1). 상위 지역을 강화만으로 못 뚫게 만드는 장치라 완화하면 지역 압력이 사라진다.
  - 곱하는 위치를 나눴다: 대상과 무관한 항(콤보·살기·성소)은 `DamageCalculator`, 대상에 달린 항
    (취약·레벨페널티)은 맞는 쪽 `EnemyHealth`. 계산기가 대상 참조 없는 순수 함수라서.
  - 히트스톱은 `RunController.Hitstop()` — `timeScale`을 만지는 유일한 주체가 거기다.
    **지속 피해 틱엔 안 걸린다**(원본 `!opts.isBurnTick`) — 장판 위 적이 여럿일 때 틱마다 0.045초씩
    멈추면 게임이 사실상 정지한다. `IDamageable.TakeTickDamage()`를 새로 둬서 갈랐다.
  - `CombatEvents.EnemyKilled`를 **아무도 발행하지 않고 있었다** — 이제 `EnemyHealth.Die()`가 쏜다.
  - **세이브/로드** — `Systems/SaveService`(순수) + `AutoSave`(컴포넌트). 나눈 이유는 테스트다:
    자동 로드가 컴포넌트에 안 섞여 있으면 PlayMode 테스트가 개발자의 진짜 세이브를 덮어쓴다.
    기본값 위에 덮어쓰므로(`FromJsonOverwrite`, 원본 `Object.assign(defaultMeta(), d)`) 나중에
    필드가 늘어도 옛 세이브가 열린다.
  - **테스트가 잡은 것**: 레벨 페널티가 붙자 기존 테스트 4개가 깨졌다 — "최대체력+1" 오버킬이
    더 이상 즉사가 아니게 된 것이라, 페널티가 실제로 작동한다는 증거다.
  - `docs/original-parity.md`를 현재 상태로 전면 갱신했다(09-08 기준이라 읽으면 오해할 내용이 많았다).

- **2026-09-10** — **런 사이클 + 로비 UI (PlayMode 113/113).** 게임이 원본과 같은 흐름으로 돌게 됐다.
  - **런 사이클** (`Systems/RunController`) — `startRun`/`pauseRun`/`resumeRun`/`endRun`/`toLobby`
    (:4293·:4332·:4346·:4354·:4382). ESC는 **일시정지**고 로비로는 거기서 한 단계 더 간다(:1027).
  - **일시정지는 `Time.timeScale = 0`** (확정 설계). 원본은 한 개의 `frame()` 루프라 `dt=0`으로
    한 번에 멈추는데(:7169) 우리는 컴포넌트마다 Update가 따로 돈다. 이 방식 덕에 적·스폰너·투사체에
    `IsRunning` 가드를 하나도 안 넣어도 된다 — 그게 실제로 성립하는지 테스트로 못 박았다.
  - **`Core/RunTransient`** — 런 중 생성물(적·투사체·장판) 표식. `RunController`가 타입 목록을 들고
    다니면 새 투사체가 생길 때마다 고쳐야 하고 **빠뜨려도 아무 에러가 안 나서** 표식 방식으로 갔다.
    ⚠️ 섬영·드루이드 투사체에도 붙여야 한다.
  - **로비 UI** (`Assets/Scripts/UI/`) — 캐릭터 선택/스테이지/강화/전문화 4탭 + HUD + 결과 화면.
    IMGUI로 만들었다(렌더링·비주얼이 범위 밖이라 uGUI 씬 빌더 수백 줄은 어차피 나중에 갈린다).
    UI 층은 아래 층을 읽고 공개 메서드를 부르기만 해서, 나중에 uGUI로 바꿔도 이 층 밖은 안 바뀐다.
  - **임시방편 3종 삭제** — R키 부활(`PlayerDeathHandler`), Tab 캐릭터 전환·숫자키 강화·6/7 빌드
    선택(`PlayerDebugController`). 전부 원본에 없고, 이제 정식 자리가 생겼다.
  - **`RunProgress.RegionLv`가 진짜 지역 위임이 됐다.** 그동안은 "누적 처치 100 = 가상 지역"이라
    한 판 안에서 몹이 계속 세졌는데, 원본 주석 "몬스터 레벨은 지역에만 의존"(:3925)과 이제야 맞는다.
  - **눈에 안 띄었을 고장 3건**: ①결과 화면이 읽는 `RunState.Kills`/`GoldEarned`/`ExpEarned`에
    스포너가 값을 안 넣고 있었다(에러 없이 전부 0으로만 떴을 것). ②`CharacterMover2D.Teleport`가
    `rb.position`만 바꿔서 `transform`이 다음 물리 스텝까지 안 따라왔다. ③`BuildPartAScene`이
    `BuildRunController()`를 `NewScene()` 앞에서 불러 만든 오브젝트가 씬 교체와 함께 버려졌다.
  - 지역 해금은 원본 규칙(이전 지역 보스 격파)을 그대로 넣되, 보스가 없어 1지역에서 못 나가므로
    `debugUnlockAllRegions`로 임시 개방했다 — 보스 구현 시 삭제. 원본 규칙 자체는 테스트가 검증한다.

- **2026-09-10** — **마법사 스킬트리 전수 대조 + 누락 1건(중력 취약) 구현 (PlayMode 97/97).**
  사용자가 "마법사 1~5차를 구현해줘"라고 해서 **먼저 실제 상태를 확인**했더니 양쪽 갈래 5티어가
  이미 다 들어가 있었다(티어별 SP 학습 `TryLearnMageTier`, 효과 공식 `MageSpecConfig`,
  X 스킬 불길이동·대붕괴 전부). 원본 `SPEC.bow`(`:897`~`:914`)의 티어 설명을 한 줄씩 대조한 결과
  **딱 하나가 빠져 있었다 — 중력 계열의 취약(`vulnT`)**.
  - 원본: 중력점 안에 있으면 `gravityExpose += dt*(1+stack*0.16)`, 누적 1.25를 넘으면 취약 3초,
    취약한 적은 **모든 피해를 1.2배**로 받는다(`vulnMult` `:1663`). 4층 설명에 적혀 있지만
    **코드엔 티어 게이트가 없어서** 실제로는 중력 1층부터 걸린다 — 설명이 아니라 코드를 따랐다.
  - `Core/IVulnerable`(Combat이 Enemies를 못 보는 계층 규칙 때문에 필요) + `EnemyHealth` 구현 +
    `GravityWellZone`이 노출을 누적. 배수는 `EnemyHealth.TakeDamageWithKnockback` 한 곳에서 곱한다 —
    `DamageCalculator`는 대상 참조가 없는 순수 계산기라 거기선 못 곱하고, 호출부 10곳에 대상을
    넘기는 건 더 나빴다. 대신 반올림이 두 번 일어나 원본과 **드물게 1 차이**가 날 수 있다(주석에 명시).
  - `gravityExpose`는 원본에서 **감쇠하지 않는다** — 한 번 임계치를 넘긴 적은 이후 중력점에 닿을
    때마다 즉시 다시 취약해진다. 감쇠를 넣으면 중력 빌드 후반 체감이 달라져서 그대로 뒀다.
  - 나머지 티어는 전부 원본과 일치 확인(3층 "충전 없이도 착탄 폭발" = `charged || tier >= 3`,
    4층 쿨감 `0.78`/`0.68`, 5층 관통 +2·폭발 반경 +0.40·화상 4스택 등).

- **2026-09-09** — **몹 7종 행동 + 지역별 스폰표 + 엘리트 (PlayMode 94/94, `fb5e094`).** 단계 1의 몬스터 쪽 완료.
  - **종류별 행동을 인터페이스로 분리** — `IEnemyMotion`(수평)·`IEnemyVerticalMotion`(비행)·`IEnemyContactDamageModifier`(돌진 ×1.4). `EnemyMove`는 중력·발판 반전·넉백·접촉처럼 **타입 공통인 것만** 계속 맡는다(원본도 `updateEnemies`가 타입 분기 뒤에 공통 코드를 돈다, `:4112`~`:4148`). 고블린·오우거·꼬마 슬라임은 붙는 컴포넌트가 없다 — 수치만 다르기 때문(CLAUDE.md SO 규칙의 '수치만' 쪽).
  - **순환참조 회피** — 슬라임이 꼬마 슬라임을 낳을 때 `Enemies`(2층)가 `Systems`(3층) 스포너를 직접 부르면 컴파일이 통째로 깨진다. CLAUDE.md에 미리 확정돼 있던 대로 `Core/EnemySpawnRequestBus`로 요청만 던진다(식별자는 `string` 대신 `EnemyType` enum으로 바꿨다 — 오타가 컴파일에서 걸리게).
  - **`Instantiate` 뒤 컴포넌트 부착 함정** — 스포너는 인스턴스를 만든 **뒤에** 종류별 컴포넌트를 붙이는데, 그 시점엔 `EnemyMove.Awake`가 이미 끝나 캐시한 참조가 null로 남는다. `RefreshTypeBehaviour()`를 만들어 마지막에 부르게 했다. 안 부르면 박쥐이 중력에 떨어지고 멧돼지가 그냥 걷는, **에러 없이 조용히 틀리는** 상태라 전용 테스트로 고정했다.
  - **원본 재확인으로 잡은 편차 5건**: ①멧돼지 상태 전이 — 원본은 속도를 정한 **뒤에** 상태를 바꿔서(`:4065`~`:4082`) 예고 진입 프레임은 아직 걷고 질주 마지막 프레임은 한 번 더 질주한다(우리 쪽은 그 한 프레임이 0이라 끝에서 멈칫했다). ②몸집을 콜라이더 반지름으로만 주면 스프라이트는 그대로라 오우거가 "보이는 것보다 넓게 때리는" 몹이 된다 → `transform.localScale`로 통일하고 `EnemyMove.WorldRadius` 추가. ③이동속도 편차 `rand(0.9,1.1)`(`:3960`) 누락 — 없으면 한 웨이브가 한 덩어리로 몰려온다. ④초기 방향(`:3959`)·돌진 첫 쿨다운(`:3966`) 무작위 누락. ⑤꼬마 슬라임 스폰 Y가 지면 아래로 내려갈 수 있었다(원본 `Math.min(e.y, groundY)`).
  - **지역별 해금표** `Systems/EnemySpawnTable`(원본 `rollSpawnType` `:3933`) — 균등 랜덤을 대체. 꼬마 슬라임은 이 표에 **절대** 안 들어간다(슬라임이 죽을 때만). 몹 레벨 `enemyLv()`는 안 넣었다 — 표가 쓰는 유일한 조건 `lv>=4`가 지역 조건과 등가라서(`regionBaseLv` `:972`), 없는 개념을 새로 들일 이유가 없었다.
  - **엘리트** `Enemies/EnemyElite` 표식 + 스포너가 수치를 곱한다(8%, 체력×4·피해×1.5·크기×1.35·보상×5, **골드는 확률 무시하고 항상 드랍** `:1818`). 꼬마 슬라임은 승격 제외(원본 `noElite`). 8% 확률이 기존 테스트 기대값을 **가끔씩만** 깨뜨려서(EXP 320이 384로) `EnemySpawner.eliteChanceOverride`를 넣었다 — 음수면 설정값을 쓰고, 테스트·밸런스 실험에서만 0/1로 고정한다.

- **2026-09-09** — **단계 1 착수: 캐릭터 뼈대 + 메카닉 0차 + EnemyData SO 전환 (PlayMode 77/77).** 세 덩어리를 순서대로 `main`에 병합·push.
  - **캐릭터 뼈대(착수 전제)** — `Core/CharacterId` + `CharacterStats.StatMultiplier`(원본 `CHAR_STAT_MULT` `:1282`, 섬영만 1.5이고 **statAtk·statMaxHp에만** 곱한다), `CharacterMover2D.MoveMode.Inertial`(원본 `bladeMove` `:2454`), `Characters/PlayerRig`+`ICharacterKit`(원본 `updatePlayer` 무기별 분기 `:3509`), Tab 캐릭터 전환(임시). 키트를 컴포넌트로 붙이기만 하면 `PlayerRig`가 자동 등록해서 팀원이 그 파일을 고칠 필요가 없다.
  - **테스트가 실제 버그를 잡음**: JS `Math.sign(0)`은 0인데 Unity `Mathf.Sign(0f)`은 1이라, 정지 상태의 첫 입력이 "이미 달리던 중"으로 오판돼 baseSpeed에 한 프레임 가속이 더 붙었다(원본은 정확히 baseSpeed). 명시적 부호 비교로 교체.
  - **메카닉 0차** — `GunnerAttack`/`GunnerBullet`. 원본 `gunnerFire()`(`:2188`)의 `laserTier==0` 분기 그대로(cd 0.24/statAs, 탄속 13.2, 수명 0.62, 피해 statAtk×0.82, 관통 1, 넉백 kb:95를 총알 진행 방향으로). **0차엔 스택이 아예 안 쌓인다** — `addGunnerStack`이 갈래 미선택이면 즉시 반환(`:1414`).
  - **`EnemyData` SO 전환** — CLAUDE.md 규칙("2번째 적 종류를 만들기 전에 SO 전환을 끝낸다") 이행. `Core/EnemyData`+`EnemyType` 7종, `Assets/Editor/BuildEnemyData`가 원본 `CONFIG.enemyBase`(`:707`~`:714`)·넉백 배율(`:1678`) 그대로 에셋 7개를 생성. `DifficultyScalingConfig`에서 고블린 기본 스탯을 걷어내 **지역 배율만** 맡게 하고(원본도 `enemyBase[type] × scale` 두 층, `:3958`), `EnemySpawner`가 스폰 시 스탯·몸집·색·넉백저항을 주입 + 처치 보상도 종류별 값을 쓴다.
  - **테스트 2건의 숨은 문제도 같이 수정**: 총알 테스트가 적 콜라이더를 0.3으로 둬서 실제 게임에선 맞는 공격이 테스트에서만 빗나갔다(실제 프리팹은 0.5). 대붕괴 테스트는 3중첩 피해가 치명타를 만나면 고블린을 한 방에 죽여 파괴된 오브젝트를 참조하는 **플래키 테스트**였다 — "죽었거나 체력이 깎였거나"로 완화.
  - **남긴 판단**: 우리 포트는 플레이어·고블린을 원본(34×56, 42×46px)보다 크게(지름 1유닛) 잡아둔 계통 편차가 있다. 이번에 바꾸면 스폰 높이·충돌·기존 테스트가 한꺼번에 흔들려서, 고블린을 현재 값에 고정하고 **종류 간 상대 크기만** 원본 비율로 환산했다. 절대 크기 재조정은 별도 작업.

- **2026-09-09** — **회의 결과로 진행 순서 전면 재편 — "캐릭터·몬스터 폭 먼저, 깊이는 나중".** 기존 계획은 메타 시스템(런사이클→로비UI→세이브→배수체인→아이템)을 먼저 쌓는 순서였는데, 회의에서 **① 모든 캐릭터를 0차(=기본공격만)까지 + 모든 몬스터를 프로토타입 그대로 → ② 캐릭터 하나씩 5차까지 심화 → ③ 완성된 캐릭터부터 밸런스**로 바꿨다. 밸런스를 잡으려면 비교 대상이 다 있어야 하기 때문(캐릭터 하나만 깊게 파 놓으면 "캐릭터가 센 건지 몹이 약한 건지"를 못 가른다).
  - **"0차" 정의 확정**(사용자 확인): 각 캐릭터의 **기본공격만 있는 상태**. 원본이 `tier < 1`이면 X 스킬을 막는 그 상태다(`:2169`/`:2741`/`:3385`). 기본공격이 채우는 자원(메카닉 스택·드루이드 마나)은 기본공격의 일부라 같이 이식하되 소비처가 전부 X 스킬이라 0차엔 쌓이기만 한다.
  - **마법사는 이미 5차까지 끝나 있어 되돌리지 않는다** — 단계 2의 첫 번째 심화 캐릭터가 이미 마법사인 셈.
  - **분업 재배치**: 팀원이 캐릭터 2종(섬영·드루이드)을 맡으므로 균형상 **몹 7종·엘리트를 내가 가져왔다.** 도메인이 갈려서(나=`Enemies/`+`Characters/Gunner*`·뼈대, 팀원=`Characters/Blade*`·`Druid*` 신규 파일) 오히려 충돌이 없다.
  - **착수 전제 재정의**: 캐릭터를 4종으로 늘리려면 플레이어 뼈대가 먼저다 — `Core/CharacterId`(+원본 `CHAR_STAT_MULT` `:1282`, 섬영만 1.5이고 statAtk·statMaxHp에만 곱함), `CharacterMover2D` **관성 이동 모드**(원본 `bladeMove` `:2454` — 방향 전환이 감속이 아니라 "속도 유지한 채 즉시 반전"인 게 핵심), `PlayerRig`+`ICharacterKit`.
  - **원본 조사에서 확인한 것**: `CONFIG.blade.brake`(2200)는 정의만 있고 `bladeMove`가 실제로 안 쓰는 **죽은 설정값**이다(`CONFIG.souls`와 같은 종류) — 이식하지 말 것.
  - **캐릭터별 레벨/SP 분리(원본 `charProgress` `:1249`)는 단계 2로 미룸** — 0차 검증엔 지장이 없고, 지금 가르면 기존 코드·테스트가 광범위하게 바뀐다. `branch`/`tier` 일반화와 같이 처리한다.
  - **런 사이클은 폐기가 아니라 보류** — `HANDOFF.md`에 있던 스펙을 `docs/sprints/04-run-cycle.md`로 옮기고 "⏸️ 착수 전 보류, 단계 3 직전 실행" 배너를 달았다. 계약 4종은 이미 `main`에 있다.

- **2026-09-09** — **레포 전체 정리 — 낡은 문서·브랜치·죽은 참조 제거 + 인수인계 정비.** 사용자 지시("구버전 내용 싹 없애고, 인수인계 받은 쪽이 바로 재개할 수 있게"). 문서가 9개로 늘면서 **어느 게 최신인지 알 수 없는 상태**가 핵심 문제였다 — 특히 완료된 스프린트 스펙이 루트 `HANDOFF.md`에 그대로 남아 있어서, 새 세션이 읽으면 **이미 폐기된 결정(R키 부활·숫자키 강화)을 현행 스펙으로 오해**하게 돼 있었다.
  - **아카이브**: 완료된 `HANDOFF.md`(성장곡선 검증) → `docs/sprints/03-growth-curve.md`, `docs/sprint2-handoff-split.md` → `docs/sprints/03-growth-curve-worksplit.md`. 아카이브 4개 전부 상단에 "⚠️ 완료된 기록, 현재 스펙 아님" 배너를 달고, **뒤집힌 결정 2건은 표로 명시**(R키 부활·숫자키 강화 → 폐기).
  - **`HANDOFF.md` 신규 작성** — 현재 스프린트(런 사이클) 스펙. 원본 줄 인용·완료 기준(DoD)·범위 밖·이미 만들어진 계약 목록까지.
  - **`docs/ROADMAP.md` 삭제** — `docs/original-parity.md`와 내용이 겹쳐 어느 쪽이 최신인지 헷갈렸고, 2절(현재 구현 상태)·4절(스프린트 순서)은 이미 사실과 달랐다. 고유하게 쓸모 있던 "원본 위치 색인"만 `original-parity.md` 부록으로 옮김.
  - **죽은 참조 제거**: 존재하지 않는 `HANDOFF_sprint2_draft.md`를 가리키던 코드 주석, 이동된 `docs/sprint2-handoff-split.md`를 가리키던 코드/테스트 주석을 전부 현행 경로로 교체.
  - **`.gitignore` 구멍 수정**: `*_results*.xml`만 있어서 실제로 쓰던 `testresults_*.xml` 파일명이 안 걸렸다(매번 수동으로 지우고 있었음). `*results*.xml`로 확장.
  - **브랜치 정리**: `main`에 전부 병합된 로컬 브랜치 5개 삭제. `feature/run-cycle`의 WIP(`Core/IRunResettable`)는 계약 성격이라 `main`에 병합. 원격 stale 브랜치 4개는 팀원이 쓸 수 있어 손대지 않고 "확인 필요"에 남김.
  - **`PROGRESS.md` 재작성**: 완료된 체크리스트 3개·과거 분업 기록·이미 틀린 "확인 필요" 항목(예: "접촉 데미지 미구현" — 실제로는 구현됨)을 걷어내고 **작업 재개 방법(클론→읽는 순서→배치 검증 명령)** 섹션을 추가. 로그는 날짜 기록이라 전부 보존.

- **2026-09-08** — **원본 전수 대조 + 전체 분업안 문서화.** 사용자가 "원본이랑 똑같이 만들어달라고 했는데 지금 게임은 다르다"고 지적 — 확인해보니 맞았다: (a) **R키 부활은 원본에 없다**(죽으면 `endRun('dead')`→결과→로비, `:1927`. 부활은 아이템 '최후의 발악' 보유 시 자동 발동뿐, `:1917`), (b) **강화는 로비에서 하는 것**(ESC는 일시정지 `:1026`, 강화/전문화는 로비 탭 `:6974`)인데 우리는 인게임 숫자키로 대체해뒀다. 둘 다 스프린트 2에서 "런 사이클이 아직 없으니 테스트가 끊기지 않게" 넣은 임시방편이었는데, 그 임시방편이 원본과의 차이로 쌓이고 있었다.
  - **근본 원인**: 원본은 로비↔사냥(90초)↔결과를 오가는 게임인데 우리는 끝없는 전투 씬 하나뿐 — `state.scene`(`:1466`)·런 타이머·결과 정산·세이브가 통째로 없어서 위 두 문제가 파생됐다.
  - 원본 7199줄을 시스템 단위로 다시 훑어 현재 구현(30파일 2636줄)과 전수 대조 → **[`docs/original-parity.md`](docs/original-parity.md)** 작성. 비주얼/렌더링(원본 1734줄)은 사용자 확인대로 대조 제외.
  - 2026-09-08 채팅에서 확정했던 전체 분업(7항목/9항목)이 **어느 파일에도 안 남아 있어 통째로 유실**된 것을 발견. 상대 세션은 채팅을 못 보므로 문서에 없는 분업은 존재하지 않는 것과 같다 → `CLAUDE.md`에 **"분업안 — 작성 위치와 형식"**(필수 9항목: 머리말/착수전제/공유계약/트랙별담당/건드리는·안되는 파일/DoD/확정사항/병합순서/범위밖) 규칙을 신설하고, 그 형식대로 **[`docs/worksplit.md`](docs/worksplit.md)**(단일 living 문서)를 작성. 스프린트마다 새 분업 파일을 만들지 않기로 함.
  - **분업 축**: 나 = 게임 구조·플레이어·메타(런사이클→UI→세이브→배수체인→구슬→아이템/가챠/시간 회귀→메카닉), 팀원 = 적·월드·나머지 캐릭터(몹6종→엘리트→성소→보스→지역스폰→출혈→섬영→드루이드→비기). 씬(`CombatCore.unity`)·`BuildPartAScene.cs` 소유권은 나, 새 프리팹은 팀원 자유.
  - **다음**: 착수 전제 3파일(`GameState`/`RunState`/`CombatEvents`)을 `main`에 올리고 팀원에게 연락 → 런 사이클 착수.

- **2026-09-08** — **`Core/DifficultyScalingConfig.cs`로 스케일링/보상 상수 분리 — `feature/kill-rewards` 브랜치.** 팀원이 `docs/sprint2-handoff-split.md`에 "0. 착수 전 필독"을 추가해 지시: 원본(`project_test.html`) 밸런스 자체가 완전히 정리된 게 아니라서(예: 아무 데서도 안 읽는 죽은 설정값 `CONFIG.souls`/`CONFIG.scale.soulGrow`를 실제로 발견, `:701`,`:727`) 나중에 직접 세밀하게 조정할 가능성이 높다는 판단 — 그때 로직 코드를 안 뒤지고 숫자만 한 파일에서 바꾸게 하려는 목적. 값 자체는 변경 없음, 위치만 이동.
  - `Core/DifficultyScalingConfig.cs` 신설 — `KillsPerRegionLevel`(100)/`HpPerRegion`(2.15)/`DmgPerRegion`(1.4)/`RewardGrow`(1.42)/`OniBaseHp`(38)/`OniBaseDmg`(13)/`OniBaseExp`(8)/`OniGoldMin,Max`(5,10)/`GoldDropChance`(0.75) 상수 + `ScaledHp()`/`ScaledDmg()`/`RewardMultiplier()` 계산 함수. 문서에 있는 코드 그대로 사용.
  - `Systems/RunProgress.cs`에서 자체 `KillsPerRegionLevel` 상수 제거하고 `DifficultyScalingConfig.KillsPerRegionLevel` 참조로 교체.
  - `Systems/EnemySpawner.cs`에 하드코딩했던 필드 9개(`baseEnemyHp`/`baseEnemyDamage`/`hpPerRegion`/`dmgPerRegion`/`rewardGrow`/`baseExp`/`goldDropChance`/`goldMin`/`goldMax`)를 전부 제거하고 `ApplyRegionScaling()`/`HandleEnemyDied()`가 `DifficultyScalingConfig`를 직접 호출하도록 정리 — 로직 자체(SetMaxHp 경유, Died 구독, RegisterKill/AddExp/AddGold 순서)는 그대로.
  - **테스트도 매직넘버 제거**: `RunProgressAndRewardsTests.cs`의 기대값 계산(hp/dmg 스케일링, EXP)을 리터럴 대신 `DifficultyScalingConfig`의 상수·함수를 직접 참조하도록 갱신 — 나중에 Config 값이 바뀌어도 테스트가 안 깨지고 같이 따라간다.
  - **배치 컴파일·PlayMode 재검증은 이 세션이 직접 못 함 — 사용자 확인 필요.** 확인되면 `main`에 병합.

- **2026-09-08** — **트랙 A(처치 보상 + 난이도 스케일링) 구현 — `feature/kill-rewards` 브랜치.** `docs/sprint2-handoff-split.md` 트랙 A 스펙 그대로 반영, 0단계에서 준비된 `EnemyHealth.SetMaxHp()`/`Died`·`ProfileService.Current`를 그대로 사용.
  - `Systems/RunProgress.cs` 신설 — 가상 지역 레벨 정적 카운터(`FieldBounds`와 같은 패턴). `TotalKills`/`RegionLv`(=1+TotalKills/100, 원본 regionKillTarget project_test.html:699)/`RegisterKill()`/`Reset()`.
  - `Systems/EnemySpawner.cs`에 스폰 직후 훅 2개 추가: `ApplyRegionScaling()`이 `EnemyHealth.SetMaxHp(38×2.15^(RegionLv-1))`(필드 직접 대입 대신 반드시 SetMaxHp 경유 — CurrentHp 미갱신 함정 회피) + `EnemyMove.attackPower = 13×1.4^(RegionLv-1)`을 계산해 적용하고 `EnemyHealth.Died += HandleEnemyDied` 구독. `HandleEnemyDied`가 `RunProgress.RegisterKill()` + `ProfileService.Current.AddExp(round(8×1.42^(RegionLv-1)))`(항상 지급) + 75% 확률로 `AddGold(round(randInt(5,10)×같은 배율))` 호출(원본 killEnemy(), project_test.html:1793).
  - **건드리지 않은 것(계약대로)**: `Core/PlayerProfile.cs`(메서드 호출만, 필드·이름 변경 없음), `Characters/` 전체, `Enemies/EnemyHealth.cs`·`Enemies/EnemyMove.cs`(0단계에서 이미 준비된 훅/필드를 호출만 함 — 소스 수정 없음).
  - **테스트 신규 4건**(`Assets/Tests/PlayMode/Systems/RunProgressAndRewardsTests.cs`): `RegionLv` 100마리 단위 증가 검증, 스폰 시 hp/dmg 스케일링 공식 검증, 처치 시 EXP 결정적 지급 검증, 40마리 연속 처치로 골드 확률 드랍 통계적 검증(0드랍 확률 0.25^40≈0).
  - **컴파일 에러 1건 + 테스트 실패 1건 발견·수정**: 테스트 파일에 `using ChronoSavior.Systems;` 누락으로 `EnemySpawner` 타입을 못 찾은 컴파일 에러(사용자가 Safe Mode 진입 후 에러 메시지 전달해 확인) → using 추가로 해결. `SpawnWave_ScalesEnemyStatsByRegionLevel` 테스트가 "1마리만 스폰" 기대했는데 2마리 나옴 → `EnemySpawner.waveTimer` 기본값이 0이라 수동 `SpawnWave()` 호출과 별개로 그 프레임의 자동 `Update()`가 웨이브를 한 번 더 터뜨린 것(프로덕션 버그 아님, 테스트가 스포너를 새로 만들 때 흔히 걸리는 함정) — 테스트 헬퍼에서 `waveTimer`를 리플렉션으로 크게 밀어 자동 웨이브만 차단(컴포넌트 자체를 끄면 `aliveMonsters.RemoveAll(null)` 같은 다른 Update 로직까지 죽어서 다른 테스트가 깨짐).
  - **아직 못 한 것 — 사용자 확인 필요**: 이 세션은 이 컴퓨터에서 git/Unity 배치모드를 직접 실행할 수 없어(파일 읽기/쓰기만 가능) 배치 컴파일·PlayMode 테스트 실행·커밋은 전부 사용자가 GitHub Desktop/에디터로 직접 해야 한다. **팀원이 `docs/sprint2-handoff-split.md`에 "0. 착수 전 필독"을 추가해 스케일링/보상 상수를 `Core/DifficultyScalingConfig.cs`로 분리하라고 지시 — 이 `main` 병합 끝나는 대로 반영 예정(값 자체는 변경 없음, 위치만 이동).**
- **2026-09-08** — **트랙 B 완료: 레벨업 + 골드 강화 5종 + 파생 스탯 적용 (PlayMode 40/40).** `feature/level-gold-upgrades` 브랜치에서 작업, 착수 전 팀원(트랙 A) 커밋이 아직 없음을 `git fetch`로 확인.
  - **레벨업**: `PlayerProfile.AddExp()`가 원본 `gainExpMeta()`(`:1440`)를 그대로 옮김 — 초과분 이월, 한 번의 지급으로 여러 레벨 가능, 실제로 레벨이 오를 때만 `LeveledUp` 이벤트 발생.
  - **골드 강화**: `Core/GoldUpgrade.cs`(비용표, 원본 `upCost` `:1268`) + `PlayerProfile.TryBuyUpgrade()`. `Core/PlayerStatCalculator.cs`(파생 스탯 5종, 원본 `:1284~1288`).
  - **원본을 다시 읽어 확인한 비직관적 동작**(그대로 구현): 골드 강화(hp)를 사도 **즉시 반영되지 않는다** — 원본은 레벨업 때만(`:1850`) 최대체력을 재계산하고 풀피로 채운다. `PlayerHealth`가 `PlayerProfile.LeveledUp`을 구독해서 그 순간에만 재계산하도록 만듦(매 프레임 폴링이었으면 원본과 다른 동작이 될 뻔함 — 구현 전에 원본을 직접 실행 로직까지 추적해서 확인).
  - **스탯 연결**: `MageAttack`(공격력→`baseDamage`, 공격속도→`cooldown`, 매 프레임 프로필에서 재계산해서 자기 참조 누적 없음), `MageProjectile`(치명타 확률을 `PlayerStatCalculator`에서), `CharacterMover2D`(이동속도 배율). `Combat.DamageCalculator`에 치명타 확률을 인자로 받는 오버로드 추가(기존 호출부 하위호환 유지).
  - **확정 사항 구현**: `PlayerDeathHandler`(HP0 → 정지, `Revive()` 공개 메서드로 R키 처리와 테스트 양쪽에서 호출), `PlayerDebugController`(숫자키 1~5 구매 + `OnGUI` 디버그 표시 — 골드/레벨/강화단계/다음비용).
  - **테스트**: 신규 10건(레벨업 공식·이월·이벤트, 강화 비용 공식·구매 성공실패, 파생 스탯 공식, 치명타 확률 0/1 경계, 레벨업시에만 체력반영 통합, MageAttack 스탯반영 통합, 사망→정지→부활 통합). `Core.ProfileService`에 정적 싱글턴 오염 방지용 `Reset()` 추가하고 모든 신규 테스트가 `[SetUp]`/`[TearDown]`에서 호출(EnemyHealth 테스트 오염을 이미 겪은 것과 같은 함정 사전 차단).
  - **막혔던 것**: 1건 실패 — `CharacterMover2D`가 `Collider2D`를 요구하는데 테스트가 안 붙여서 `CheckGrounded()`에서 NRE. 테스트 버그였고 콜라이더 추가로 해결. 이동속도 통합 테스트 하나는 애초에 `Assert.Pass()`로 항상 통과하는 가짜 테스트라 삭제(Input 시뮬레이션이 안 돼서 실제 검증이 불가능했음 — 없는 것보다 정직하게 빼는 게 낫다고 판단).
  - 에디터 직접 플레이 확인, 팀원 트랙 A 완료는 아직.
- **2026-09-08** — **스프린트 2 초안 접수 → 검토 → 팀 분업 확정.** 팀원이 `HANDOFF_sprint2_draft.md`("성장곡선 검증" — 체력/피격무적·처치보상·레벨EXP·골드강화5종·가상지역레벨·검증방법)를 `origin/main`에 push.
  - **겹치는 작업 발견**: 초안 "1. 체력 시스템" 항목이 바로 아래 로그(같은 날짜, 원본 전수 분석 로드맵)에서 이미 `feature/health-damage`에 구현·검증까지 끝낸 것과 사실상 동일 범위. 이 사실을 확인하고 사용자에게 알림.
  - 초안이 "확인 필요"로 남긴 3건을 논의해 확정: HP 0 처리(정지+R키 재시작 — 원안의 "로그만"은 죽을 때마다 에디터 재시작해야 해서 검증 자체를 방해한다고 판단해 변경), 강화 입력(숫자키+`OnGUI` 디버그 표시 — 표시가 없으면 "체감 벽" 검증이 불가능해서 원안에 추가), `regionLv` 트리거(누적 처치 100마리당 1 — 원본 `regionKillTarget`과 가장 가까움, 시간/레벨 기준은 원본에 없는 설계라 기각).
  - **트랙 분업 확정**: 트랙 A(팀원, Enemies/Systems 도메인) = 처치 보상 + 난이도 스케일링. 트랙 B(나, Core/Characters 도메인) = 레벨업 + 골드 강화 + 스탯 적용. 근거: 팀원은 최근(`d27af46`) `Enemy_Base.prefab`/물리 세팅을 직접 다뤘고, 나는 `PlayerHealth`/`MageAttack`/`CharacterMover2D`를 이번 세션에서 직접 만들어 가장 잘 앎. 상세 분업표는 `docs/sprint2-handoff-split.md`.
  - **공유 계약**: `Core.PlayerProfile`(`level, exp, gold, spUsed, upgrades{atk,hp,ms,atkSpeed,crit}`, `as`는 C# 예약어라 `atkSpeed`로 대체) + `Core.ProfileService.Current` 정적 접근자. 트랙 A는 `AddGold()`/`AddExp()` 메서드만 호출(필드 직접 증가 금지 — 레벨업 판정은 트랙 B 소유). `EnemyHealth`에 스탯 배율 setter(`SetMaxHp` 등) 필요 — `Awake()`가 이미 돈 뒤 `maxHp` 필드만 바꾸면 `CurrentHp`엔 반영 안 되는 이 프로젝트 단골 함정 주의(RequireComponent 자동보충·AddComponent Awake타이밍과 같은 종류).
  - 이어서 0단계(`feature/health-damage` 병합)를 바로 진행 — 아래 이 로그 항목 참고.
- **2026-09-03** — **원본 전수 분석 → 포팅 로드맵 작성 + 스프린트 2(체력&데미지) 확정·구현.** 사용자가 "프로토타입 전체적으로 분석하고 어떻게 업데이트할지 문서로 작성"을 요청.
  - `docs/ROADMAP.md` 신설 — 원본 7199줄을 전 구간 훑어 시스템 전수 목록화(씬 4상태 머신 / 런 루프 / 전투 코어 / 캐릭터 4종 / 적 7종+보스 / 성장 3계층 / 시간 회귀·가챠 / 세이브), 현재 Unity 구현과의 갭, 의존성 기반 권장 스프린트 순서를 정리. *(→ 2026-09-09에 `docs/original-parity.md`로 통합되고 삭제됨)*
  - **검증**: 문서에 쓴 원본 인용 68개를 전부 실제 파일과 재대조해 일치 확인. 2절(현재 구현 상태) 주장도 코드로 검증(스크립트 10개, `UI` 폴더 없음, `Destroy()` 직접 호출 2곳, `IDamageable`/`GameInput`/`PlayerProfile` 미구현, `PlayerAttack` 씬 미사용, 보스 코드 전무). 작성 중 오류 1건 발견·수정(아이템 개수를 26종으로 잘못 적었으나 실제 35종 — 등급별 9/14/8/4).
  - **핵심 결론**: 거의 모든 시스템(보상·런 종료·원소·엘리트/보스 hp배수·아이템 스탯)이 체력에 의존 → 순서가 사실상 강제됨. 체력/데미지 → 런 사이클 → 세이브/로비까지 해야 "게임 루프"가 처음 성립.
  - 사용자가 이 순서를 승인하고 1번부터 착수 지시 → **스프린트 2(체력 & 데미지) 확정 후 구현.** 신설: `Core/IDamageable`(피격 인터페이스) · `Combat/DamageCalculator`(치명타 10%·배수 1.7·±10% 난수·최소 1) · `Enemies/EnemyHealth`(체력 38·넉백·피격 플래시) · `Characters/PlayerHealth`(체력 100·무적 0.9초·피격 반동). 공격 스크립트를 `Destroy()` 직접 호출 → `IDamageable.TakeDamage()`로 전환, `EnemyMove.TryAttack()` 빈 스텁 → 실제 접촉 데미지.
  - **원본을 다시 읽어 확인한 비대칭 2건**(그대로 구현): ① 적 넉백은 AI 속도에 *더해지는* 별도 성분 + 초당 9배율 감쇠(`:4021`,`:4056`) — 그냥 속도에 넣으면 `FixedUpdate`가 다음 프레임에 지운다. ② 반면 **플레이어 수평 넉백은 유지하면 안 된다** — 원본도 다음 프레임 이동 입력이 vx를 덮어써서(`:1548`, 피격 중 이동잠금 없음) 사실상 1프레임짜리다. 우리 `CharacterMover2D`도 vx를 덮어쓰므로 아무 처리 안 하는 게 원본과 같음. 수직 튕김만 남는다.
  - **원본과 어긋나 있던 것 하나 같이 수정**: 스폰 보호 중 적이 계속 움직이고 접촉 데미지까지 줬는데, 원본은 "상호 무적 — 움직이지도, 때리지도, 맞지도 않는다"(`:4022~4023`)로 갱신 자체를 건너뛴다. `EnemyMove`가 보호 중엔 AI·접촉·수평이동을 전부 스킵하도록 수정.
  - **테스트**: 신규 10건 추가(적 체력/스폰무적/넉백/투사체 통합/데미지 공식, 플레이어 체력/무적창/반동/사망이벤트/접촉). 기존 4건은 "한 방에 파괴"를 전제로 짜여 있어 새 동작에 맞게 갱신 — 각 테스트가 원래 검증하던 것(사거리·태그 필터, 관통 횟수, 스폰 무적)은 그대로 두고 기대값만 "파괴됨"→"피해 입음"으로 바꿨다.
  - **막혔던 것**: 1차 실행에서 8건 실패. 그중 `EnemyMove_Chases...`는 단독 실행하면 통과 → **테스트 오염**이었다(내 신규 테스트가 중력 켜진 적을 같은 좌표에 남겨서 다른 테스트의 몹을 물리로 밀어냄). 프로덕션 버그 아님. 신규 테스트에 `[TearDown]` 정리 + 중력 off + 스폰 무적 해제를 넣어 해결. 접촉 테스트는 Player(6)/Enemy(7) 레이어를 실제 게임과 동일하게 지정해야 겹친 상태가 유지된다(팀원이 만든 레이어 분리 덕).
  - PlayMode 30/30. `feature/health-damage` 브랜치에 커밋(`c4518ad`) — 당시엔 `main` 미병합. CLAUDE.md "문서 구조" 규칙대로 HANDOFF.md를 `docs/sprints/01-combat-core.md`로 옮기고 이 스펙으로 교체하려 했으나, 같은 날 팀원 초안이 도착해 스프린트 2 범위 자체가 위(9월8일 로그) 항목처럼 재확정됨.
- **2026-09-08** — **0단계 완료: `feature/health-damage` → `main` 병합.** 위 스프린트 2 구현을 그대로 `main`에 반영(`--no-ff` 병합). PROGRESS.md만 충돌(로그 두 버전 병합, "다음 할 일" 새로 씀) — 코드 파일은 전부 깨끗하게 병합됨. 다음은 `Core/PlayerProfile` 스캐폴딩.
- **2026-08-29** — **버그 수정: 플레이어·몬스터가 서로 겹치지 못하고 물리로 밀어내는 문제.** 사용자가 리팩터링 이후분을 직접 플레이해보다 발견해 지적. 원본(`project_test.html:4148` `updateEnemies()`)은 물리엔진이 없어 `rectsOverlap()`로 접촉만 검사해 `damagePlayer()`를 부르고 끝 — 겹쳐도 밀어내는 로직 자체가 없다. 반면 Unity 쪽은 발판 착지를 위해 Player·Enemy 둘 다 `Rigidbody2D`(Dynamic) + 막힌(non-trigger) `CircleCollider2D`를 쓰는데, 같은 물리 레이어(`Default`, 0번)라 겹치는 순간 Physics2D가 자동으로 밀어냈다 — 원본엔 없는, 실제 물리엔진 도입(2025-08-25) 부작용이었음(그동안 안 걸리다가 이번에 처음 지적됨).
  - 콜라이더를 트리거로 바꾸면 발판 충돌까지 깨지므로, 새 물리 레이어 `Player`(6)·`Enemy`(7) 신설(`CLAUDE.md` "번호/이름 예약표" 갱신) 후 Player 오브젝트·`Enemy_Base` 프리팹에 각각 배정, **Physics2D Layer Collision Matrix에서 Player×Enemy 충돌만 비활성화**(Player×Ground, Enemy×Ground는 유지)하는 방식으로 해결.
  - 접촉 데미지 판정은 원래도 물리 충돌과 무관하게 `EnemyMove.OverlapsTarget()`의 수동 거리 계산으로 처리되고 있어 코드 수정은 불필요 — 순수 프로젝트 세팅(TagManager/Physics2DSettings) 변경만으로 해결됨.
  - 사용자가 Unity 에디터에서 직접 레이어·콜리전 매트릭스 설정 후 플레이로 재확인 완료. 코드 변경이 없어 컴파일/PlayMode 테스트에는 영향 없음(재실행 안 함 — 다음 코드 변경 시 같이 재검증).
  - `main`에서 바로 진행(팀원이 동시에 이 레포를 안 건드리고 있음을 확인함, CLAUDE.md 협업규칙 3번 예외 — 이미 병합된 기능의 소규모 후속 수정).
- **2026-08-26** — **대규모 리팩터링 완료 — 폴더/네임스페이스/asmdef 분리 + `Monster`→`Enemy` 리네임.** 사용자가 "리펙터링 ㄱㄱㄱ"로 승인, CLAUDE.md "대규모 리팩터링 절차"(미병합 브랜치 0개 확인 → 진행 중 선언 → 단일 커밋 → 배치 검증 → 병합)를 그대로 따라 진행. 착수 전 미병합 브랜치 0개 확인(`feature/monster-combat`/`feature/player-field` 둘 다 `main` 대비 unique 커밋 없음).
  - **폴더 = 네임스페이스 = asmdef 이름 일치**: `Assets/Scripts/`를 `Core/World/Combat/Characters/Enemies/Systems` 6개 폴더로 분리(전부 `git mv`로 이동해 히스토리 보존), 각각 `ChronoSavior.<도메인>` 네임스페이스 + 같은 이름의 asmdef 신설. 계층(`Core`←`World`/`Combat`←`Characters`/`Enemies`←`Systems`)대로 `references` 설정, 같은 층(`Characters`↔`Enemies`)은 서로 참조 안 함. 기존 단일 `ChronoSavior.Runtime.asmdef`는 삭제.
  - **`Core/ISpawnProtectable.cs` 신설** — 리팩터 도중 실제 계층 위반을 발견: `MageProjectile`(`Characters`)·`PlayerAttack`(`Combat`)이 `MonsterMove`(`Enemies` 예정) 타입을 직접 `GetComponent`하고 있었다. `Enemies`를 모르는 도메인이 스폰 무적 상태만 물을 수 있게 `Core`에 인터페이스를 만들고 `EnemyMove`가 구현, 공격 스크립트는 `GetComponent<ISpawnProtectable>()`로 전환.
  - **`Monster`→`Enemy` 리네임**: `MonsterMove.cs`→`EnemyMove.cs`, `MonsterSpawner.cs`→`EnemySpawner.cs`(클래스명도 동일하게), `Monster.prefab`→`Enemy_Base.prefab`(내부 `m_Name`도 갱신). `BuildPartAScene.cs`의 프리팹 경로 상수·타입 참조 전부 갱신.
  - `Assets/Editor/BuildPartAScene.cs`도 `ChronoSavior.Editor` 네임스페이스로 감쌈 — **배치모드 `-executeMethod`는 이제 `ChronoSavior.Editor.BuildPartAScene.Build`처럼 전체 경로가 필요**(옛 짧은 이름은 "class could not be found"로 실패).
  - `Assets/Tests/PlayMode/`도 도메인별 하위 폴더(`Combat/Characters/Enemies/`)로 재배치, `MonsterSpawnAndMoveTests.cs`→`Enemies/EnemySpawnAndMoveTests.cs`로 리네임하며 내부 타입 참조 갱신, 전부 `ChronoSavior.Tests.PlayMode` 네임스페이스로 감쌈. `ChronoSavior.PlayModeTests.asmdef`의 참조를 옛 `ChronoSavior.Runtime` 대신 새 6개 asmdef로 교체.
  - **1차 배치 검증에서 실제 컴파일 에러 발견**: `EnemySpawnAndMoveTests.cs`에 `using ChronoSavior.Enemies;`를 빠뜨려서 `EnemyMove`를 못 찾음(CS0246) — 추가하고 재검증. 두 번째 시도에선 컴파일은 통과했지만 `-executeMethod BuildPartAScene.Build`(옛 이름)가 네임스페이스 이동 때문에 실패 — `ChronoSavior.Editor.BuildPartAScene.Build`로 고쳐서 씬 재생성 성공.
  - 최종 배치 컴파일 + PlayMode 테스트 전체 재실행 → **20/20 통과**(리팩터 전과 동일한 개수, 회귀 없음).
  - ScriptableObject 데이터 전환·`GameInput` 중앙화는 CLAUDE.md 규칙상 **이번 리팩터 범위 밖**(SO는 "2번째 무기/적 종류를 만들기 전" 시점으로, GameInput은 새 코드부터 — 둘 다 명시적으로 유보).
  - `CLAUDE.md`에 "문서 3종 동기화" 규칙 신설(사용자가 세 문서 갱신이 잘 안 지켜지는 것 같다고 지적) + "지금 프로젝트 상태"를 리팩터 완료 상태로 갱신.
  - `origin/main`에 이 리팩터 커밋 push까지 완료.
- **2026-08-26** — **"만들어진 것 전체를 원본과 대조" 전수 감사 — 플레이어 이동/점프 디테일 4건 발견해 이식.** 사용자가 지금까지 구현된 전체를 원본과 다시 대조해달라고 요청 → 원본 플레이어 업데이트 루프(`bowFire` 호출부 포함 3500~3560행)를 처음부터 재검토:
  - 원본에 `bladeMove()`(관성 가속 이동)가 있어서 "이것도 빠졌나" 싶었는데, 확인해보니 **블레이드 캐릭터 전용**(`if (meta.weapon==='blade') updateBladePlayer()`로 분기)이고 활(마법사, 우리 스코프)은 원래 즉시-속도 이동이 맞다 — 헛다리 아니었음, 기존 구현 그대로 유지.
  - **코요테 타임(0.10초)·점프 입력 버퍼(0.13초)** 완전히 빠져있었음(`CharacterMover2D`에 추가, 원본 `COYOTE_T`/`INPUT_BUF_T`) — 이전엔 "grounded인 바로 그 프레임에 keydown"만 인정해서 원본보다 조작감이 빡빡했다.
  - **차지 중 이동속도 50% 감소**(원본 `chargeSlow`) 완전히 빠져있었음 — `CharacterMover2D.SpeedMultiplier`를 추가하고 `MageAttack`이 매 프레임 갱신하도록 연결. 처음엔 `charging` 플래그를 프레임 끝에서 읽어서 테스트가 실패했는데(리플렉션으로 charging=true만 주입하면 같은 프레임에서 "방금 뗐다"로 오인돼 즉시 발사되며 charging이 다시 꺼짐), 원본처럼 프레임 시작 시점 상태를 기준으로 삼도록 순서를 바꿔서 해결 — 실제 게임에서도 더 정확한 순서.
  - **낙하 종단속도 15유닛/s**(원본 1500px/s) 상한 없었음 — 실제 물리 중력만 쓰면 최상단 발판(5.4유닛)에서 떨어질 때 이론상 ≈16.8유닛/s까지 나서 원본보다 세게 떨어졌다. `CharacterMover2D`/`MonsterMove` 둘 다 추가.
  - **필드 X 경계 여백**: 플레이어 0.24유닛(원본 24px), 몬스터 0.3유닛(원본 30px, 서로 다른 값) — 몬스터 쪽은 경계 clamp 자체가 아예 없어서 배회 중 필드 밖으로 나갈 수 있었다.
  - PlayMode 테스트 5건 추가(`MovementFidelityTests.cs`) — 코요테 타임 안/밖, 차지 감속, 종단속도, 경계 여백. 종단속도 테스트는 처음에 실패했는데, 물리 스텝이 클램프보다 한 스텝 늦게 반영돼 매 스텝 중력만큼(26×fixedDeltaTime≈0.52유닛/s) 일시 초과하는 게 정상이라 허용치를 그에 맞게 조정 — 실제 게임에선 감지 불가능한 서브프레임 오차. 전체 **20/20 통과**.
  - `HANDOFF.md` 1번에 상세 기록. 아직 에디터로 직접 확인 전, `origin`에도 push 안 함(로컬에 8개 커밋 쌓임 — 플레이 확인되면 한 번에 push 권장).
- **2026-08-26** — **카메라 크기를 원본 캔버스 비율(1280×720px)에 맞게 재계산.** 사용자가 "원본은 맵이 크고 넓직한데 여긴 너무 작아 보인다, 원본과 맵을 똑같이 만들어달라"고 지적 — 확인해보니 카메라 `orthographicSize=5.5`가 원본 캔버스 크기랑 아무 관계 없이 "발판이 다 보이는 정도"로 임의로 잡은 값이었다. 원본은 캔버스가 1280×720px 고정이라 항상 맵(2600px)의 49%만 화면에 보이는데(나머지는 스크롤로 드러남), 이전 값은 맵의 75%(19.6/26유닛)를 한 번에 보여주고 있었다 — 원본보다 훨씬 더 넓게 보여줘서 오히려 "안 커 보인다"는 역설이 생긴 것.
  - 100px=1유닛 규칙을 카메라에도 그대로 적용: 세로 절반 크기 = 720px÷100÷2 = 3.6유닛이 원본 그대로의 기준값. 다만 발판 층간 간격을 넓힌 우리 필드(사용자가 이미 명시적으로 요청한 편차, HANDOFF.md 1번)에 이 값을 그대로 쓰면 최상단 발판(5.40유닛) 위에 선 캐릭터 머리가 화면 위로 살짝 잘린다 — 그래서 하단 여백(바닥 아래 1유닛)은 원본 그대로 유지하고, 상단만 우리 발판 높이 기준으로 재계산해서 `orthographicSize=3.84`(원본 3.6에서 소폭 확대), 카메라 Y=2.84로 최종 확정.
  - 가로세로 비율은 유지돼 필드/화면 비(26/12.8≈2.03)가 원본(2600/1280≈2.03)과 일치 — 스크롤되는 느낌까지 원본과 같아짐.
  - `BuildPartAScene.cs`만 수정(씬 재생성), 코드 로직 변경 없어 PlayMode 테스트는 그대로 **15/15 통과**.
  - `HANDOFF.md` 1번에 재개정 기록. 아직 에디터로 직접 확인 전, `origin`에도 push 안 함.
- **2026-08-26** — **플레이어 기본공격(마법사) 사거리 재검증 + 판정 크기를 원본과 일치시킴.** 사용자가 "공격이 맵 끝까지 간다, 원본은 사정거리가 있는 걸로 안다"고 지적 → 원본 `bowFire()`와 화살 업데이트 루프(3595~3690행)를 다시 정밀 대조:
  - **사거리(life) 자체는 실제로 원본과 일치했다** — `life = B.range/B.speed`(880/1080≈0.8148초, 차지와 무관하게 고정)를 새 PlayMode 테스트(`Projectile_DespawnsAfterFixedLife_NotOnFieldEdge`)로 직접 검증: 예상 수명 전엔 살아있고, 8.8~11.4유닛(필드 26유닛의 절반 이하)만 이동한 뒤 정확히 그 시점에 사라짐. **버그 아님.** 원본 자체가 캔버스 1280px 중 사거리 880px(68.75%)라 화면 대부분을 가로지르는 게 원래 그렇게 설계된 것 — "맵 끝까지 가는 것처럼 보인다"는 원본과 같은 체감.
  - **진짜 빠져있던 것을 찾음 — 판정(히트박스) 크기**: 원본은 `rectsOverlap(x-20*size, y-15*size, 40*size, 30*size, ...)` — 40×30px(0.4×0.3유닛) 직사각형이고 `size=1+chargeK*0.9`로 차지할수록 커진다. 기존 구현은 반지름 0.5(지름 1유닛) 고정 원이라 원본보다 훨씬 크고 차지에 반응하지 않았다. `MageProjectile`을 `BoxCollider2D`(0.4×0.3, 차지 시 최대 0.76×0.57)로 교체, `MageAttack`도 `sizeMul=1+chargeK*0.9` 공식을 그대로 전달하도록 수정. 판정과 시각(스프라이트)을 자식 오브젝트로 분리해서 스케일 이중 곱 버그도 같이 예방.
  - PlayMode 테스트 1건 추가, 전체 **15/15 통과**.
  - **확인 필요로 남긴 것**: 원본은 순수 수직 조준(위/아래만) 시 투사체 스폰 X 오프셋이 캐릭터의 "마지막 좌우 방향"(`p.facing`, 이동뿐 아니라 대각선 조준으로도 갱신됨)을 따르는데, 우리 `CharacterMover2D.Facing`은 이동 입력으로만 갱신돼서 순수 수직 조준 직후의 스폰 위치가 미묘하게 다를 수 있다(좌우 0.52유닛 차이, 코스메틱 수준). 지금은 안 고침 — 필요하면 나중에.
  - 아직 에디터로 직접 플레이 확인 전, `origin`에도 push 안 함.
- **2026-08-26** — **몬스터가 발판 스폰 포인트를 골라도 실제로는 바닥에만 나던 문제 + 이동 AI를 원본 그대로 재구현.** 사용자가 "왜 적이 바닥 축에서 스폰되냐, 발판 위에도 소환된다며 거짓말했냐, 제대로 작업해봐"라고 지적 — 확인해보니 실제로 X좌표만 발판 위치에서 빌리고 Y는 항상 GroundY로 고정해뒀던 게 맞았다(발판에 스폰된다는 말과 실제 동작이 달랐음). 사용자가 이어서 "이건 프로토타입이 아니라 실제 구현이니 원본과 세세한 부분까지 그대로 만들어야 한다, 단순화는 나중에 지시하겠다"고 명확히 함 — 원본 `updateEnemies()`를 처음부터 끝까지 다시 읽고 몹 관련 물리/AI를 전부 이식:
  - **몹도 실제 중력을 받는다**(`e.vy += gravity*dt`) — `Monster.prefab`에 `Rigidbody2D` 추가, `MonsterMove`가 `transform.position +=` 대신 물리 기반 이동으로 전환. (막혔던 지점: `[RequireComponent(typeof(Rigidbody2D))]`를 붙이면 프리팹 파일에 실제로 없어도 로드 시점에 엔진이 메모리상으로만 자동 보충해서 "이미 있음" 체크가 거짓으로 통과 — 항상 로드→설정→저장하도록 `EnsureMonsterPrefabPhysics()` 수정.)
  - **스폰 위치가 실제 발판 높이를 쓴다** — 발판 스폰 포인트는 `FieldLayout.PlatformLandingY()`(발판 윗면+몹 반지름)로 스폰, 이제 물리로 그 위에 실제로 선다.
  - **추적/배회 AI**: 원본 고블린 분기 그대로 — 플레이어가 추적범위(300px=3유닛) 안이면 방향만 맞추고 계속 걷는다(도달해도 안 멈춤). 범위 밖이면 1.5~3.5초마다 무작위로 방향을 바꾸며 배회. 이전엔 항상 무조건 플레이어를 향해 걷다가 접촉 시 멈추는, 원본에 없는 동작이었음.
  - **발판 가장자리 반전**(원본 주석 "발판 위 몹은 가장자리에서 되돌아간다 — 내려오지 않는다"): 발판 위에서 정지 중이고 가장자리 14px 안쪽이면 그 자리에 고정하고 안쪽으로 방향 반전. 몹이 실제로 발판 위에 계속 남아있게 됨(전 버전은 이 로직이 없어서 그냥 물리로 굴러떨어지게 뒀었는데, 원본과 다른 동작이었음).
  - **접촉 데미지**: 원본은 매 프레임 겹침 판정 + 무쿨다운(반복 방지는 플레이어 무적시간이 담당) — `contactDistance`로 멈춰서 쿨다운 도는 방식을 걷어내고 겹치는 동안 매 프레임 호출로 교체(실제 데미지는 Health 없어서 여전히 스텁).
  - `FieldLayout.cs`에 `PlatformLandingY`/`PlatformLeftX`/`PlatformRightX` 헬퍼 추가(스포너·이동 AI가 공유).
  - PlayMode 테스트 재정비: 발판 착지 물리 검증, 가장자리 반전 검증(신규), 추적범위 안/밖 방향 검증을 우연히 기본값과 맞아떨어지던 약한 테스트에서 실제로 반대 방향 이동을 요구하는 강한 테스트로 교체. **14/14 전체 통과.**
  - `HANDOFF.md` 2번을 실제 구현 상태로 갱신, "프로토타입처럼 단순화하지 않는다"는 작업 방침을 `CLAUDE.md`에도 기록.
  - 아직 에디터로 직접 플레이 확인 전, `origin`에도 push 안 함.
- **2026-08-26** — **스폰/히트 로직을 원본과 다시 대조해서 빠진 부분 보강.** 사용자가 직접 플레이해보고 "적 스폰 로직이 이상하다, 원본과 너무 다르다"고 지적 → 원본 `spawnWave()`/`dealDamage()`를 다시 읽어서 확인:
  - **스폰 위치**: 필드 전체 균등 랜덤이었던 걸 원본처럼 "발판 15개 중심 + 바닥 그리드 6곳" 총 21개의 정해진 스폰 포인트 중 하나를 골라 그 폭 안에서 흔드는 방식으로 교체(`MonsterSpawner.PickSpawnPointX`). 발판 좌표 데이터를 `BuildPartAScene.cs`와 중복으로 안 두려고 `FieldLayout.cs`로 공용화.
  - **웨이브 내 간격 체크**: 원본은 `spawnWave()` 호출마다 `placed=[]`를 새로 시작해서 이전 웨이브 몹과는 안 겹쳐도 되는데, 지금까진 전체 생존 몹과 비교해서 더 엄격했다 — 원본대로 이번 웨이브 안에서만 체크하도록 수정, 간격 기준도 원본 실측치(52px→0.52유닛, X축 전용)로 교체.
  - **스폰 무적(`spawnInvuln`)**: 원본은 스폰 직후 2초간(`CONFIG.run.spawnProtect`) 모든 피해 판정 함수가 대상을 건너뛰는데 이게 통째로 빠져 있었다. `MonsterMove`에 `IsSpawnProtected` 추가하고 `PlayerAttack`/`MageProjectile` 둘 다 파괴 전에 확인하도록 연결(관통 카운트도 원본처럼 안 깎임).
  - PlayMode 테스트 2건 추가(스폰 포인트가 균등 랜덤이 아니라 정해진 창 안에만 찍히는지, 스폰 무적 중엔 공격이 안 통하고 무적 풀리면 통하는지). 처음엔 무적 테스트가 실패했는데 원인은 `AddComponent`가 `Awake()`를 동기 실행해서 그 뒤에 바꾼 public 필드가 반영이 안 된 테스트 코드 버그였음(private 타이머를 리플렉션으로 직접 세팅하도록 수정) — 프로덕션 코드 문제 아니었음. 재실행 **12/12 전체 통과**.
  - 아직 에디터로 직접 플레이 확인 전, `origin`에도 push 안 함.
- **2026-08-26** — **Part B 병합 후 남은 수정 5건 + 테스트 완료.** 사용자가 "Part B가 수정 다 했다는데 남은 게 있다"고 지적, 직접 고쳐달라고 요청해서 진행:
  - `MonsterSpawner.cs`: `FieldBounds.Min/Max`(없어진 Vector2 API) → `RandomX()`/`GroundY`로 교체(컴파일 에러였음)
  - `MonsterMove.cs`: 추적을 X축만으로 제한(Y는 `GroundY` 유지), `moveSpeed` 76→0.76(÷100 축척)
  - `Monster.prefab`: `moveSpeed` 0.76로, 색을 흰색→빨강(0.85, 0.2, 0.2)으로(HANDOFF.md 4번 "아군/적군 색 구분" 위반이었음 — 병합 후 코드 diff 재확인 중 새로 발견)
  - `BuildPartAScene.cs`: `BuildMonsterSpawner()` 추가해 씬에 스포너 등록(프리팹 참조 연결)
  - PlayMode 테스트 신설(`MonsterSpawnAndMoveTests.cs`): 웨이브 스폰이 필드 경계 안·GroundY에 배치되는지, 몹이 X축으로만 쫓아가고 Y는 그대로인지(발판 위 플레이어를 시뮬레이션해서 예전 버그가 재발하면 바로 잡히게 설계)
  - Unity 에디터가 닫혀 있는 걸 확인 후 배치 컴파일 + PlayMode 테스트 전체 실행 → 처음엔 신설 테스트 1건 실패(비활성화한 프리팹 템플릿을 Instantiate하면 인스턴스도 비활성 상태로 복사돼 `FindGameObjectsWithTag`가 못 찾음 — 내 테스트 코드 버그, 프로덕션 코드 문제 아니었음). `aliveMonsters` 리스트를 리플렉션으로 직접 읽는 방식으로 고쳐 재실행 → **10/10 전체 통과**.
  - 아직 `origin`에는 push 안 함 — 사용자 최종 확인 대기.
- **2026-08-25** — **Part B 브랜치를 `main`에 병합(로컬, 아직 push 안 함).** `PROGRESS.md` 한 건만 충돌(예상된 정상 상황, CLAUDE.md 협업규칙 6번대로 로그는 양쪽 보존·상태 섹션은 새로 작성). **단, Part B 로그에 "고쳤다"고 적힌 수정 3건이 실제 커밋(`ffbf18d`)에는 들어있지 않은 것을 파일 diff로 확인함** — CLAUDE.md 협업규칙 3번("로그 말고 실제 diff로 재확인")이 바로 이 사례에서 나왔다. 미반영 항목은 아래 "확인 필요"에 목록으로 정리했고, 이번엔 사용자가 Unity에서 직접 수정하기로 함.
- **2026-08-25** — **발판을 원웨이로 수정 + 층간 간격 확대.** 사용자가 플레이해보고 "플랫폼에 머리를 박으면 안 되는데(점프할 땐 통과, 밟을 수는 있게), 그리고 발판 사이 간격이 너무 작다"고 지적. 두 가지 다 원본과의 실제 차이였음:
  - 막힌(solid) `BoxCollider2D`였던 발판을 `PlatformEffector2D`(`useOneWay=true`) + `Collider2D.usedByEffector=true`로 교체 — 원본처럼 아래/옆에서는 통과하고 위에서 떨어질 때만 착지된다. 원본의 `dropTimer`(아래로 뛰어내리기)까지는 재현 안 함(범위 밖).
  - 발판 층간 간격을 원본 값(1.0~1.1유닛)에서 1.35유닛으로 넓힘 — **사용자가 명시적으로 요청한 의도적 편차**(원본 그대로 되돌리지 말 것). X 배치(발판 개수·폭·좌우 위치)는 원본 그대로 유지, Y(층 높이)만 조정. 점프 최대 높이(1.772유닛) 대비 76% 지점이라 여유 있게 닿는다.
  - 카메라 Y 위치를 2.5→3으로 살짝 올림(발판이 더 높아져서). PlayMode 테스트 1건 추가(`Player_PassesThroughOneWayPlatformFromBelow_ThenLandsOnTopFromAbove` — 아래서 위로 지나갈 때 안 막히는지 + 위에서 착지하는지 둘 다 확인). **8/8 전체 통과.**
  - `HANDOFF.md` 1번의 "단순화" 문구를 실제 구현 상태로 갱신.
- **2026-08-25** — **Part B: `main` 병합 + 신규 `FieldBounds` API 대응** ⚠️ *아래 항목 중 코드 수정 3건은 실제 커밋에 반영되지 않았음(바로 위 로그 참고) — 기록만 남기고 실제 수정은 별도로 진행.* `git merge origin/main`에서 충돌 5건 발생, 다음과 같이 해결:
  - `Assets/Scripts.meta`, `Assets/Scripts/FieldBounds.cs.meta` — GUID 충돌, `main` 쪽 GUID로 통일
  - `Assets/Scripts/FieldBounds.cs` — Part B가 넣어둔 임시 스텁을 버리고 `main`(Part A) 버전으로 완전 교체
  - `ProjectSettings/TagManager.asset` — `Enemy` 태그·`Ground` 레이어는 자동으로 잘 합쳐졌고, `serializedVersion: 2` vs `3` 한 줄만 `3`(main)으로
  - `PROGRESS.md` — 이 파일. 양쪽 로그 다 보존해서 병합
  - 충돌 해결만으론 컴파일이 안 돼서 추가로 고침: `MonsterSpawner.TryGetSpawnPosition()`이 옛 `FieldBounds.Min/Max`(Vector2) API를 쓰고 있었는데, 새 `FieldBounds`엔 그 필드가 없어져서 `FieldBounds.RandomX()`/`FieldBounds.GroundY`를 쓰도록 수정
  - 인터페이스 계약 위반도 같이 수정: `MonsterMove`의 추적 로직이 X·Y 둘 다 플레이어를 쫓아가고 있어서, 플레이어가 발판 위에 있으면 몬스터가 공중으로 떠오르는 문제가 있었음 — X축만 이동하고 Y는 `FieldBounds.GroundY`에 고정하도록 수정
  - 병합 중 추가로 발견한 버그: `Monster.prefab`/`MonsterMove.cs`의 `moveSpeed`가 원본 고블린 스탯(76, px/s 스케일)을 캐릭터 이동속도와 같은 규칙(100px=1유닛, ÷100)으로 축척하지 않고 그대로 쓰고 있었음 — 26유닛 필드를 0.34초에 주파하는 셈이라 명백한 버그. `0.76`로 수정.
- **2026-08-25** — **물리엔진 실물 전환 + 원본 발판 15개 + 마법사(bow) 차지샷 구현.** 사용자 피드백("왜 이렇게 퀄리티가 낮지? 물리엔진이랑 마법사 캐릭터 기본공격을 구현해줘봐 그리고 플랫폼도 구현해줘")으로 이번 스프린트 범위를 확장(HANDOFF.md 1·3번 개정, 위 표 참고).
  - `CharacterMover2D`: 손으로 적분하던 중력(vy 변수)을 없애고 `Rigidbody2D`(실제 gravityScale) + `Physics2D.OverlapCircle` 접지 판정으로 교체. 전역 중력은 `Physics2D.gravity=(0,-26)`(원본 2600px/s² 축척).
  - `Ground` 물리 레이어 신설(`BuildPartAScene.EnsureGroundLayer`가 TagManager.asset에 직접 등록) — 바닥·발판만 여기 소속, 접지 판정이 플레이어/적/투사체를 오탐하지 않게.
  - 원본 `NORMAL_PLATFORMS`(15개, 4개 층) 좌표를 100px=1유닛로 환산해 실물 `BoxCollider2D` 발판으로 배치. **원웨이(아래서 통과) 로직은 이번엔 뺌** — 막힌 콜라이더로 단순화(위 HANDOFF.md 표 아래 단순화 항목 참고).
  - 필드 폭을 원본 그대로(`FieldBounds.MinX/MaxX` = 0/26, mapW 2600px)로 넓히고, 카메라를 고정 → X축 스크롤(`CameraFollow2D`, 플레이어를 따라가되 필드 경계에서 clamp)로 바꿈 — 넓어진 필드를 고정 카메라로 다 담으면 캐릭터가 너무 작아져서.
  - `MageAttack`/`MageProjectile` 신설: 원본 `bowFire()`/`CONFIG.bow` 차지 공식(데미지·관통·탄속 전부 차지율에 비례) 그대로 이식, ↑/↓로 상하 조준까지 지원. 근접형 `PlayerAttack`을 씬에서 빼고 이걸로 교체(코드는 남겨둠, 재사용 대비).
  - PlayMode 테스트 3건 추가(`PhysicsAndMageTests.cs`: 발판 착지, 풀차지/무차지 탄속·관통 비교, pierce 정확히 base+1타에서 멈추는지) + 기존 점프 테스트를 새 Rigidbody2D 기반 구현에 맞게 재작성. **7/7 전체 통과.**
- **2026-08-25** — **버그 수정: 조작키가 원본과 다름 + 필드 구조 자체가 잘못됨.** 사용자 피드백으로 발견. 원본 `KEYMAP`은 ArrowLeft/Right(좌우) · KeyC/Space(점프) · KeyZ(공격)인데 임의로 WASD+Space/좌클릭으로 구현했었다. 더 근본적으로, "점프"가 있다는 것 자체가 원본이 횡스크롤 플랫포머(X=좌우, Y=고정바닥+점프 중력)라는 뜻인데 `FieldBounds`를 top-down 자유이동 사각형(Vector2 Min/Max)으로 잘못 설계했었다 — HANDOFF.md의 "평면 아레나(점프 없음)" 결정 자체가 안일했음. `FieldBounds`를 `MinX/MaxX/GroundY`로, `CharacterMover2D`에 실제 점프 물리(원본 상수를 100px=1유닛로 축척: moveSpeed 2.7, jumpSpeed 9.6, gravity 26) 추가, `PlayerAttack` 트리거를 Z 단독으로 수정. PlayMode 테스트도 점프 물리(실제 컴포넌트를 FixedUpdate로 직접 구동, private 필드는 리플렉션으로 점프 시작 상태만 주입)까지 추가해 재검증 — 통과. `HANDOFF.md` 1·3번 항목도 같이 수정.
- **2026-08-25** — **Part A 완료, `main` 병합.** `FieldBounds.cs`(경계+clamp), `CharacterMover2D.cs`(이동), `PlayerAttack.cs`(공격 1개, Space/좌클릭)을 만들고, `Assets/Editor/BuildPartAScene.cs`(배치 실행용 씬 조립 스크립트)로 `CombatCore.unity`(Field/Camera/Player) 생성. 절차적 원형 스프라이트(`Circle.png`) 생성. TagManager에 `Enemy` 태그 등록.
  - **검증 과정에서 실제 이슈 하나 발견**: 처음엔 Edit Mode에서 `-executeMethod`로 물리 쿼리(`Physics2D.OverlapCircleAll`)를 직접 돌려 확인하려 했는데, 사거리 안의 Enemy가 안 죽는 것으로 나왔다(오탐). 원인은 Edit Mode에서는 Physics2D 월드가 제대로 안 돌고 `Destroy()`도 다음 프레임에 반영이 안 돼서였다 — 실제 `PlayerAttack` 로직 버그가 아니었다. `Assets/Tests/PlayMode/`에 PlayMode 테스트 2건(`PlayerAttackTests.cs`)을 만들어 `-runTests -testPlatform PlayMode`로 재검증 → 2/2 통과. **Edit Mode 배치 실행은 "씬/에셋이 예상대로 만들어졌는지" 구조 확인용으로만 쓰고, 실제 게임 로직(물리·충돌·Destroy) 검증은 항상 PlayMode 테스트로 할 것** — Part B도 동일하게 적용.
- **2026-08-25** — `Assets/Prefabs/Monster.prefab` 에디터에서 제작(SpriteRenderer+CircleCollider2D+`MonsterMove`, Tag=`Enemy`), `ProjectSettings/TagManager.asset`에 `Enemy` 태그 추가. 임시 GameObject에 `MonsterSpawner` 올리고 프리팹 연결해서 Play 테스트 — 웨이브 스폰 정상 동작 확인. 커밋 전.
- **2026-08-25** — Part B(몬스터&스폰) 착수. `Assets/Scripts/MonsterSpawner.cs`(웨이브 스폰: 3.6초 간격, 웨이브당 최대 7마리, 전체 상한 22, 최소 간격 유지 랜덤 배치·10회 재시도), `Assets/Scripts/MonsterMove.cs`(가장 가까운 Player 태그로 직선 이동 + 접촉 시 정지, 실제 데미지는 미구현) 작성. 브랜치 단독 컴파일용 `Assets/Scripts/FieldBounds.cs` 임시 스텁 추가(Part A 병합 시 교체 예정). `Monster.prefab`은 에디터 작업으로 남김.
- **2026-08-25** — 레포 세팅 완료. `HANDOFF.md`(스펙), `CLAUDE.md`(작업 규칙), `reference/project_test.html`(원본 참고) 커밋. 코드 작업은 아직 시작 전.

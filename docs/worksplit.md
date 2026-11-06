# 분업안 (living document)

> ⚠️ **2026-11-04~07 구조 정리 스프린트에서 파일·씬 이름이 바뀌었다** — 아래 표·소유권 문구는 그 전(~10/02) 기록이라 옛 이름이 남아 있다.
> 읽을 때 바꿔 읽을 것: `Scenes/CombatCore.unity` → `00_Persistent`·`10_Lobby`·`20_Combat` 3씬 · `Editor/BuildPartAScene.cs`(씬을 코드로 만들던 도구) → **삭제**, 씬 파일이 원본(`Editor/SceneSetupValidator`가 검사만) ·
> `Systems/RunController` → `Systems/GameFlow`(흐름) + `Systems/CombatDirector`(전투) · `UI/LobbyScreen`·`UiTheme` 등 옛 IMGUI 화면 → **삭제**, Figma `UI/Screens/*`(`PxScreen`) ·
> `Enemy_Oni.prefab` → `Prefabs/Enemies/Enemy_Base`(+ 종류별 프리팹). 진행 상태 칸(예: "섬영 진행 중")도 그때 기준이다 — 지금 상태는 `PROGRESS.md`.

> **이 파일이 분업의 유일한 출처다.** 채팅에서 정한 분업은 여기 적히기 전까지 존재하지 않는 것으로 친다.
> 형식 규칙은 `CLAUDE.md` "분업안 — 작성 위치와 형식". 완료 항목은 지우지 말고 `✅`로 표시한다.

## 1. 머리말

- **확정 날짜**: 분업 2026-09-09(회의) · **진행 순서·날짜 2026-09-23(노션 일정표 — 아래 표가 정본)**
- **근거 문서**: [`docs/original-parity.md`](original-parity.md) · 원본 `reference/project_test.html`
- **참여**: 나(사용자 세션) / 팀원(다른 클로드 세션). 밸런스 조정은 기획자 상윤.
- **기준**(2026-09-30 최종 목표 변경): 수치·동작의 기준은 원본이고, **바꾸는 건 회의에서 정한 개선뿐이다** — 그 밖에
  원본과 다른 점을 발견하면 짚는다(개선으로 둘지 원본대로 고칠지는 사용자가 정한다).

### 일정표 (노션, 2026-09-23) — 순서·날짜의 정본

> **2026-09-30 정리**: 예전 "단계 1 → 2 → 3"(9/9 회의)은 더 쓰지 않는다. 일정표의 "2단계"(10/15~)와 이름이
> 겹쳐 헷갈렸다 — 앞으로는 아래 번호(①~⑩)와 이름으로만 부른다. 옛 단계 1(모든 캐릭터 0차 + 모든 몬스터)은
> 2026-09-28에 끝났고(`docs/sprints/05-all-characters-tier0.md`), 옛 단계 2(캐릭터 하나씩 5층)가 아래 ①이다.
> 옛 단계 3(밸런스, 기획자 상윤)은 일정표에 따로 없다 — 어디에 들어가는지 팀 확인 필요.

**프로토타입 — 마감 10/14(수)** · 노션 할당 김민준·제로플(개발 2명 공동)

| # | 항목 | 기간 | 저장소 상태 (2026-09-30) |
|---|---|---|---|
| ① | 각 직업별 스킬 구현(드루이드 제외) | 9/23~10/2 | **지금 여기** — 스펙 `HANDOFF.md`. 마법사 5층 ✅ · 메카닉 5층 ✅(9/30 `main` 병합) · 섬영 0차 ✅ → 1층~ 팀원 진행 중(`feature/blade-tier1`) |
| ② | 몬스터 구현(1차) · 몬스터 전투 시스템 | 10/3~10/8 | ⚠️ **원본 이식본이 이미 있다** — 몹 7종·엘리트·보스·성소·지역별 스폰표(4절 3~6·10번). 이 주에 할 일은 팀 확인 필요 |
| ③ | 전투 시스템 구현 + 플랫폼 | 10/9~10/14 | ⚠️ **이미 있다** — 전투 코어(`IDamageable`·`DamageCalculator`)·넉백·무적·히트스톱·배수 체인(4절 8번), 발판·아래키 낙하·보스 포탈(10번). 팀 확인 필요 |

> 9/28에 권한 "10/12~14 통합·버그 수정" 행은 노션에 들어갔는지 모른다.

**본개발 — 12월 첫째 주까지** (순서는 사용자 확정)

| # | 항목 | 기간 | 저장소 상태 (2026-09-30) |
|---|---|---|---|
| ④ | 맵 템플릿 제작 | 10/15~10/25 | 없음 — 지금은 고정 무대 하나(`World/FieldLayout`의 일반·보스 발판) |
| ⑤ | 몬스터 템플릿(2차) | 10/26~11/1 | 몹 수치는 이미 SO(`EnemyData`, 4절 4번). 템플릿 범위(행동·스폰 목록 ID 등)는 미정 |
| ⑥ | 스탯 + 강화 시스템 | 11/2~11/8 | 원본 파생 스탯 5종 + 골드 강화 5종이 있다(모든 캐릭터 공용 — 원본대로). 캐릭터별 레벨·경험치·SP는 ✅ 09-30(7절) |
| ⑦ | 유물 템플릿 및 시스템 | 11/9~11/15 | 원본 아이템 35종이 있다 — Figma "시공 유물"(25종)과 다르다 |
| ⑧ | 가챠 시스템 | 11/16~11/22 | 원본 가챠(천장 20)가 있다 — Figma 확률(노말 70·레어 27·에픽 2·레전드 1%, 1회·10회)과 다르다 |
| ⑨ | 스토리 컷씬 (JSON) | 11/23~11/29 | 없음 |
| ⑩ | 통합 · 버그 수정 | 11/30~ | 노션엔 끝 날짜가 없다 |

**"0차"란 = 기본공격만 있는 상태.** 전문화(스킬트리)를 아직 안 찍은 상태로, 원본에서 `tier <= 0`이면
X 스킬이 막히고(`tryMageSkill` `:2169`, `tryBladeSkill` `:2741`, `tryDruidSkill` `:3385`)
**Z 기본공격 + 그 캐릭터의 이동 방식**만 남는다. 티어 보너스도 전부 빠진다.
(기본공격이 채우는 자원 — 메카닉 스택·드루이드 마나 — 은 기본공격의 일부라 같이 이식하되,
소비처가 전부 X 스킬이라 0차에선 쌓이기만 한다. 원본도 동일.)

> **마법사는 이미 5층까지 끝났다**(폭발·중력 두 갈래 전부). 되돌리지 말고 그대로 둔다.

---

## 2. 착수 전제 (선행 작업 — 내가 먼저 처리, 팀원은 대기)

캐릭터를 4종으로 늘리려면 **플레이어 오브젝트 뼈대**가 먼저 있어야 한다. 팀원의 섬영/드루이드는
이 뼈대에 컴포넌트를 얹는 방식이라, 아래가 `main`에 올라간 뒤 착수한다.

1. `Core/CharacterId.cs` — 캐릭터 4종 enum + 캐릭터별 스탯 배수(원본 `CHAR_STAT_MULT` `:1282`)
2. `Characters/CharacterMover2D`에 **관성 이동 모드** 추가 (섬영용, 원본 `bladeMove` `:2454`)
3. `Characters/PlayerRig.cs` — 선택된 캐릭터의 키트만 켜는 전환기
4. `Core/PlayerStatCalculator`에 캐릭터 배수 반영

**팀원은 이 커밋이 `main`에 올라갔다는 연락을 받은 뒤 시작할 것.**
(몹 작업은 이 뼈대와 무관하므로 언제든 시작 가능 — 다만 이번 판에서 몹은 내 담당으로 옮겼다, 4절 참고)

---

## 3. 공유 계약 (임의로 이름/타입 바꾸지 말 것)

### `Core/CharacterId.cs` — 내가 소유
```csharp
namespace ChronoSavior.Core
{
    /// 원본 CHARACTERS(:848) / WEAPONS(:841). 캐릭터 = 무기 1:1이라 하나로 합쳤다.
    public enum CharacterId { Mage, Gunner, Blade, Druid }

    public static class CharacterStats
    {
        /// 원본 CHAR_STAT_MULT(:1282) — 섬영만 1.5. statAtk·statMaxHp에만 곱한다(ms/as/crit엔 안 곱함).
        public static float StatMultiplier(CharacterId id) => id == CharacterId.Blade ? 1.5f : 1f;
    }
}
```

### `Characters/CharacterMover2D` — 내가 소유, **섬영이 모드만 바꿔 쓴다**
```csharp
public enum MoveMode { Instant, Inertial }   // Instant=마법사/메카닉, Inertial=섬영

public MoveMode moveMode = MoveMode.Instant;

// --- 관성 모드 파라미터 (원본 CONFIG.blade :613, 100px=1유닛) ---
public float inertialBaseSpeed = 3.0f;   // baseSpeed 300
public float inertialMaxSpeed  = 9.0f;   // maxSpeed 900
public float inertialAccel     = 5.2f;   // accel 520
public float inertialDecel     = 9.0f;   // decel 900

/// 관성 모드에서 이동 파라미터 전체에 곱해지는 배율. 섬영은 min(1, statMs)를 넣는다
/// (원본 bladeMsK() — 이속 100%를 넘는 분은 속도가 아니라 피해로 간다).
public float MoveScale = 1f;
/// 가속에만 추가로 곱해지는 배율. 원본은 가속을 이속의 **제곱**으로 키우고(bladeAccelK),
/// 집중(focus) 중엔 여기에 더 곱한다 — 티어 구현 시 섬영 키트가 매 프레임 세팅한다.
public float AccelMultiplier = 1f;

/// 마지막으로 달리던 방향(원본 p.runDir). 관성 모드의 가속/반전 판정에 쓰인다.
public int RunDir { get; }
```
⚠️ **관성 모드의 방향 전환은 감속이 아니라 "속도를 유지한 채 즉시 반전"이다**(원본 `:2469-2474`).
`CONFIG.blade.brake`(2200)는 정의만 있고 실제로 안 쓰이는 죽은 값이니 이식하지 말 것.

### `Characters/PlayerRig.cs` — 내가 소유, **팀원은 자기 키트를 등록만 한다**
```csharp
/// 선택된 캐릭터의 키트 컴포넌트만 켜고 나머지는 끈다(원본 updatePlayer의 무기별 분기 :3509~3511).
public class PlayerRig : MonoBehaviour
{
    public CharacterId Current { get; }
    public void Select(CharacterId id);   // 키트 enable/disable + 이동 모드 + 스탯 배수 갱신
}

/// 캐릭터 키트가 구현하는 계약. PlayerRig가 이걸로 켜고 끈다.
public interface ICharacterKit
{
    CharacterId Character { get; }
    void OnSelected();     // 이 캐릭터로 전환됐을 때(자원 초기화 등)
    void OnDeselected();
}
```
팀원은 `BladeCombat`/`DruidAttack`이 `ICharacterKit`을 구현하고 플레이어 오브젝트에 붙이기만 하면 된다.

### 이미 있는 것 (다시 만들지 말 것)
| 파일 | 용도 | 소유 |
|---|---|---|
| `Core/GameInput` | **모든 키 입력.** `Input.GetKey`를 스크립트에서 직접 부르지 말 것 | 공용 |
| `Core/IDamageable`(+`TakeDamageWithKnockback`) | 피해 전달 | 공용 |
| `Core/IElementAfflictable` | 화상(`ApplyBurn`). 출혈은 섬영 담당자가 추가 | 공용 |
| `Core/PlayerStatCalculator` | 파생 스탯 5종 | 나 |
| `Core/GameState`·`RunState`·`CombatEvents`·`IRunResettable` | 런 사이클 계약(구현은 나중) | 나 |
| `Combat/DamageCalculator` | 치명타·±10% 난수 | 공용 |
| `Enemies/EnemyHealth.SetMaxHp`/`Died` | 스탯 주입·처치 알림 | 공용 |
| `Core/RunTransient` | **런 중 생성물 표식** — 아래 ⚠️ 참고 | 공용 |
| `Core/CombatModifiers` | 콤보·살기·연쇄·레벨페널티·성소버프 배수 | 나 |
| `Core/IDamageable.TakeTickDamage` | 지속 피해 틱(넉백·히트스톱 없음). 장판/도트는 이쪽으로 | 공용 |
| `Systems/SaveService` | 세이브/로드. 프로필을 바꾼 뒤 부르면 저장된다 | 나 |
| `Core/IRunResettable` | 런 시작 시 초기화(`ResetForRun`) — 키트가 구현하면 자동 호출 | 공용 |
| `Systems/RunController` | 로비↔사냥↔결과 전이. UI만 부른다 | 나 |
| `Core/EntitySizeConfig` | **덩치의 단일 출처** — 플레이어·몹·성소·보스 크기 | 나 |
| `UI/UiTheme` | 로비·결과·HUD 공통 색/판/글자(원본 CSS 이식) | 나 |
| `Characters/ISkillSlotSource` | **HUD 스킬 슬롯(Z·X 쿨다운·스택)과 버프 줄의 캐릭터 몫** — 키트가 구현하면 HUD에 뜬다(아래 참고) | 공용 |
| `Core/EnemySpawnRequestBus.Request` | **인자 3개다** — (위치, 종류, **스폰 보호 시간**) | 나 |

> **섬영 키트에 HUD 쿨다운 붙이기** (2026-09-29, 팀장이 `BladeCombat`은 안 건드리고 남겨둠):
> `BladeCombat`에 `ISkillSlotSource`를 구현하면 끝이다 — HUD·씬은 손댈 필요 없다. 원본 값은 syncHUD(:6321~:6348):
> Z 칸 = `SkillSlotState.Cooldown(cdTimer, SpinCd)`(**원본은 공속 반영 전 0.13으로 나눈다**, `p.atkCds.blade / CONFIG.blade.spin.cd`),
> X 칸 = 갈래가 칼날폭풍이면 `stormCd / storm.cd`, 아니면 `focusCd / focus.cd`, 버프 글 = 원본 :6293~:6300(속도 %·피해 배수 등).

⚠️ **단골 함정 1**: `Instantiate`/`AddComponent` 직후 필드만 바꾸면 이미 실행된 `Awake()`에 반영이 안 된다.
적 체력은 반드시 `EnemyHealth.SetMaxHp()`로 바꿀 것(이 함정으로 이미 3번 깨졌다).

⚠️ **단골 함정 2 — 값을 정의해 놓고 호출부를 안 만드는 것.** 2026-09-10 감사에서 나온 편차 10건 중
**절반이 이 모양이었다**(쿨감 아이템·살기 공속·성소 스폰 보호·보스 레벨 하한). 상수도 함수도 멀쩡히
있으니 컴파일러는 아무 말 안 하고, 그 값을 직접 검사하는 단위 테스트도 통과한다 — 아무도 그걸
**"쓰는지"**는 안 보기 때문이다.
→ 그래서 `Tests/PlayMode/OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`가 소스를 직접 훑어
호출부 없는 API를 잡는다. **정의와 사용처가 다른 파일인 공개 API를 새로 만들면 그 목록에 추가할 것.**

⚠️ **런 중에 만드는 오브젝트에는 `Core/RunTransient`를 붙일 것** — 섬영 참격 판정체, 드루이드 늑대,
투사체, 장판 전부 해당한다. 안 붙이면 로비로 나갔다 다시 들어와도 그게 필드에 남아 있는다.
**에러가 안 나는 종류의 버그**라 눈으로는 늦게 발견된다. (`RunTransient.Mark(go)` 또는 `AddComponent`)

✅ **공속·피해 배수는 키트가 신경 쓸 게 없다.** 살기(fury) 공속은 원본과 똑같이 **`statAs()` 안**에
들어 있으므로(`PlayerStatCalculator.ComputeAttackSpeedMultiplier`), 쿨다운을 `기본값 / statAs`로만
계산하면 자동으로 붙는다. 피해 쪽(콤보·살기·레벨페널티)도 `DamageCalculator`가 전부 처리한다.
*(2026-09-10 이전 판에는 "키트가 직접 곱하라"고 적혀 있었는데, 원본을 다시 보니 스탯 안에 있는 게
맞아서 그쪽으로 고쳤다 — 키트마다 곱하는 걸 잊는 실수가 원천적으로 안 생긴다.)*

⚠️ **게임이 멈춰 있을 때(`Time.timeScale = 0`)를 전제로 짜지 말 것** — 반대로, 그 덕분에 키트 코드에
`IsRunning` 검사를 **넣을 필요가 없다.** 로비·일시정지에선 `Time.deltaTime`이 0이라 쿨다운도 안 돈다.
UI처럼 멈춰도 돌아야 하는 것만 `Time.unscaledDeltaTime`을 쓴다.

---

## 4·5·6. 트랙별 담당 / 건드리는 파일 / 완료 정의

> **이번 판에서 몹 담당이 바뀌었다.** 팀원이 캐릭터 2종(섬영·드루이드)을 맡으므로 균형상 몹 7종·엘리트를
> 내가 가져온다. 도메인이 갈려서(나=`Enemies/`, 팀원=`Characters/Blade*`·`Druid*`) 오히려 충돌이 없다.

### 나 — 뼈대 · 메카닉 · 몬스터 전부

**건드리는 파일**: `Core/` 전체, `Characters/CharacterMover2D`·`PlayerRig`·`Gunner*`·`MageAttack`,
`Enemies/` 전체, `Systems/EnemySpawner`, **`Assets/Scenes/CombatCore.unity` + `Assets/Editor/BuildPartAScene.cs`(씬 소유권 = 나)**

**건드리면 안 되는 파일**: `Characters/Blade*`, `Characters/Druid*`(팀원이 새로 만드는 파일)

| # | 항목 | 원본 | 완료 정의(DoD) |
|---|---|---|---|
| 1 ✅ | **착수 전제** — `CharacterId`+스탯배수, 관성 이동 모드, `PlayerRig`+`ICharacterKit`, 캐릭터 전환(임시 디버그 키) | `:1282`, `:2454`, `:3509` | 팀원이 키트를 붙일 수 있는 상태로 `main` 병합 + 연락 |
| 2 ✅ | **메카닉 0차** — Z 총알(속도 13.2, 수명 0.62, 피해 0.82, 관통 1, cd 0.24/statAs), 적중 시 스택 +1 | `gunnerFire` `:2188` | 마법사와 전환해가며 둘 다 쏴진다 |
| 3 ✅ | **몹 6종 추가** — wisp(비행·수직추적), bigOni(넉백저항 0.4), charger(조준→돌진→탈진 상태기계), shooter(거리유지+투사체), splitter(죽으면 꼬마 슬라임 2), splitlet | `CONFIG.enemyBase` `:707`~`:714`, charger `:716`, shooter `:717`, 분열 `:1840`, wisp `:700` | 6종이 각자 원본 AI대로 움직인다. 종류별 PlayMode 테스트 |
| 4 ✅ | **`EnemyData` ScriptableObject 전환** | — | ⚠️ **3번보다 먼저.** CLAUDE.md 규칙: "2번째 적 종류를 만들기 전에 SO 전환을 끝낸다" |
| 5 ✅ | **엘리트** — 8% 확률, 체력×4·피해×1.5·보상×5·크기×1.35 | `:696`, `:3949` | 엘리트가 섞여 나오고 보상이 5배 |
| 6 ✅ | **지역별 스폰 테이블** — `rollSpawnType(region)` 해금표 | `:3933`, `enemyLv` `:3924` | 지역이 오르면 몹 구성이 원본대로 바뀐다 |
| 7 ✅ | **런 사이클 + 로비** — 로비↔사냥↔결과, 캐릭터 선택/스테이지/강화/전문화 탭 | `:4293`, `:6378`, `:6974` | 로비에서 고르고 찍고 들어갔다가 결과 보고 돌아온다 |
| 8 ✅ | **전투 배수 체인 + 세이브** — 콤보·살기·연쇄처치·레벨페널티·히트스톱, JSON 세이브 | `:1296`, `:1651`, `:1213` | 모든 피해가 체인을 지나가고, 껐다 켜도 진행이 남는다 |
| 9 ✅ | **시간 회귀 · 뽑기 · 업적** — 회귀 장벽, 아이템 35종, 가챠(천장 20), 업적 32종 | `:6509`, `:758`, `:802` | 성장 고리가 닫혔다 |
| 10 ✅ | **구슬 · 성소 · 지역진행+보스 · 프로토타입 그림 · 아래키 낙하 · 보스 무대** | `:4452`, `:3996`, `:4155` | — |
| 11 ✅ | **메카닉 5층** *(① 직업별 스킬 구현, 2026-09-28 팀 분담)* — 레이저(빔·드론)·설치기(부품·링크·장판), 로비에서 SP로 습득, HUD 스킬 슬롯 | `SPEC.gunner` `:915`, `updateGunnerBeam` `:2224`, `tryGunnerSkill` `:2390` | 두 갈래 5층이 원본대로 — ✅ 2026-09-30 `main` 병합 |

> **캐릭터 선택 탭은 `PlayerRig`가 붙어 있는 키트를 자동으로 읽는다** — 섬영·드루이드 키트를
> 붙이기만 하면 **로비를 다시 만들 필요 없이** 목록에 뜬다.

#### 3~6번 결과 (2026-09-09 병합 `fb5e094`)

- 7종 전부 원본 행동으로 구현·씬 등록 완료. PlayMode **94/94**.
- 행동은 `Enemies/IEnemyMotion` 계약을 구현한 **종류 전용 컴포넌트**로 분리했고(CLAUDE.md
  "SO는 수치만, 행동이 다르면 스크립트 분리"), 고블린·오우거·꼬마 슬라임은 붙는 게 없다(기본 보행 + 수치만 다름).
- ~~스포너가 `Instantiate` 뒤에 컴포넌트를 붙이고 `EnemyMove.RefreshTypeBehaviour()`를 부른다~~ → **2026-11-05 바뀜**: 종류별 행동 부품은
  **종류별 프리팹**(`Assets/Prefabs/Enemies/Enemy_Flyer` 등, `EnemyData.prefab`)에 들어 있고 `RefreshTypeBehaviour`는 없앴다(`HANDOFF.md` '정리 순서 2'). 새 몹 종류는 프리팹 변형을 만들 것.
- 몸집은 콜라이더 반지름이 아니라 `transform.localScale`로 준다. 판정은 `EnemyMove.WorldRadius`로 읽을 것.
- `EnemySpawner.eliteChanceOverride`(음수면 설정값) — 엘리트 확률을 0/1로 고정해야 하는 테스트·밸런스용 스위치.

### 팀원 — 섬영 · 드루이드

**건드리는 파일**: `Characters/Blade*`, `Characters/Druid*`(전부 신규 파일), `Core/IElementAfflictable`(출혈 추가 시),
**`Core/PlayerProfile`의 섬영 갈래·층 필드**(2026-09-28 팀원 연락 — 아래 `Core/` 읽기 전용 규칙의 예외. 메카닉 브랜치도
같은 파일을 고쳐서 병합 때 충돌이 난다 → 8절)

**건드리면 안 되는 파일**: `Assets/Scenes/CombatCore.unity`, `Assets/Editor/BuildPartAScene.cs`(씬은 내 소유 —
키트를 씬에 붙여야 하면 나한테 요청), `Enemies/` 전체, `Systems/EnemySpawner`, `Core/` 전체(읽기만),
`Characters/CharacterMover2D`·`PlayerRig`·`MageAttack`·`Gunner*`

| # | 항목 | 원본 | 완료 정의(DoD) |
|---|---|---|---|
| A ✅ | **섬영 0차** — 관성 이동(`CharacterMover2D.MoveMode.Inertial` 사용, `MoveScale`에 `min(1,statMs)`), 회전베기 Z(cd 0.13, 피해 0.42, 반경 1.05), 속도→피해 배수(정지 ×1 → 최고속 ×2.4), 평타 흡혈 15%, 방향키 누르는 동안 피격 무적 | `CONFIG.blade` `:613`, `bladeMove` `:2454`, `bladeSpinTick` `:2510`, `bladeDmgMult` `:2439`, `bladeDirInvuln` `:2448` | 달릴수록 세지고, 방향 전환이 감속 없이 즉시 뒤집힌다. PlayMode 테스트 |
| B ✗ | **(기획 제외, 2026-09-28)** 드루이드 0차 — 클로 3타 콤보 Z(콤보창 0.5초, 3타째가 마무리), 마나(최대 20, 평타 적중 시 8% 회복) | `CONFIG.druid` `:644`, `druidClaw` `:3272` | 3타 콤보가 돌고 마나가 찬다. PlayMode 테스트 |

| C | **섬영 1층~** *(① 직업별 스킬 구현, 2026-09-28 팀 분담)* — 집중(X)·칼날폭풍 두 갈래, 로비에서 SP로 습득 | `SPEC.blade`, `bladeFocusStart` `:2521`, `storm` `:636` | 진행 중(`feature/blade-tier1`) |

> A·B는 사용자 확인(2026-09-08): **팀원이 자기 방식대로 구현한다 — 내가 미리 맞출 필요 없음.**
> 단 위 "공유 계약"의 이름·시그니처만 지켜줄 것.

---

## 7. 확정된 세부 결정 (다시 묻지 말 것)

- **0차엔 X 스킬이 없다.** 원본이 `tier < 1`이면 스킬을 막고 메시지만 띄운다 — 그대로 이식한다.
- **마법사는 이미 5차까지 완료.** 0차로 되돌리지 않는다.
- **캐릭터별 진행 분리 — 원본대로(사용자 결정 2026-09-30, 구현 `feature/char-progress`)**: **레벨·경험치·쓴 SP만
  캐릭터마다 따로**(원본 `meta.charProgress` `:1124`~`:1265`). 골드·골드 강화·시간의 파편·아이템·지역 진행·업적은
  **모든 캐릭터가 같이 쓴다.** 09-29에 "골드까지 나눈다"고 했다가 09-30에 원본대로 바꿨다 — 골드는 편차가 아니다.
  시간 회귀는 **모든 캐릭터**를 1레벨·SP 0으로 되돌리고, '각인의 봉인'은 빌드(갈래·층)만 남긴다(쓴 SP는 봉인이 있어도
  0 — 원본 `resetAllCharSp()`가 봉인과 상관없이 돈다). 옛 세이브의 레벨·경험치·SP는 저장 당시 고른 캐릭터에게 옮긴다.
- **캐릭터 전환은 로비 "캐릭터 선택" 탭이 한다.** 임시 디버그 키(Tab 전환·숫자키 강화·R키 부활)는
  전부 **삭제됐다** — 원본에 없던 것들이고 이제 정식 자리가 있다. 되살리지 말 것.
- **런 사이클·세이브·로비는 완료다.** 계약(`GameState`/`RunState`/`CombatEvents`/`IRunResettable`)만
  있던 상태에서 구현까지 끝났다(`808e323`·`ffabf52`).
- 밸런스 수치는 원본 그대로. 숫자는 로직에 박지 말고 설정 클래스/SO 한 곳에 모은다.
- **덩치는 예외 — 사용자가 직접 지정한 값이다**(2026-09-10, 플레이어는 2026-09-28 재지정). 플레이어 높이
  **0.5(고블린과 1대1)**, 몹은 원본 크기, 성소·보스는 원본 크기. 전부 `Core/EntitySizeConfig` 한 곳에 있다.
  원본 `p.y - N` 꼴 높이(총구 등)는 **발** 기준이라 `Characters/PlayerBody.Feet`에서 잴 것(섬영 키트 포함).

## 8. 병합 순서 + 씬/프리팹 소유권

```
[나] 착수 전제(1번) → main       ← ✅ 완료, 팀원 연락 완료
        ↓ (여기서부터 동시 진행)
[나]   2 메카닉 0차 ✅ → 4 EnemyData SO ✅ → 3 몹 6종 ✅ → 5 엘리트 ✅ → 6 지역 스폰표 ✅
       → 7 런 사이클 + 로비 ✅
       → 8 전투 배수 체인 + 세이브 ✅
       → 9 시간 회귀·뽑기·업적 ✅ → 10 구슬·성소·지역진행·보스 ✅
[팀원] A 섬영 0차 ✅(2026-09-28 병합) · B 드루이드 0차 ✗(기획 제외)
        ↓
① 직업별 스킬 구현(~10/2): [나] 11 메카닉 5층 ✅ main 병합(9/30) / [팀원] C 섬영 1층~ 진행 중(feature/blade-tier1)
        ↓ 섬영 브랜치도 main에 (아래 "① 병합 순서")
② 몬스터 → ③ 전투+플랫폼 → 10/14 프로토타입 → ④~⑩ (1절 일정표)
```

- **① 병합 순서 — 메카닉이 먼저 들어갔다(2026-09-30).** 팀원은 `feature/blade-tier1`에서 `main`을 받아
  (`git fetch origin` → `git merge origin/main`) 충돌을 풀고 올린다. 겹치는 파일:
  - `Core/PlayerProfile.cs` — 메카닉 갈래·층(`gunnerBranch`/`gunnerTier`·`TryLearnGunnerTier`·`DoRegression`)과
    섬영 필드가 같은 자리에 붙는다. **양쪽 다 살린다.**
  - `UI/LobbyScreen.cs` — 전문화 탭이 캐릭터별 함수로 나뉘었다(`DrawSpecTab` → `DrawMageSpec`·`DrawGunnerSpec`,
    공통 도우미 `DrawSpecHeader`·`DrawBranchRows`). 섬영 탭은 같은 도우미로 `DrawBladeSpec`을 하나 더 만들면 된다.
  - `Core/Achievements.cs`(전문화 업적이 마법사·메카닉 층을 센다) · `PROGRESS.md`(로그는 양쪽 다 남긴다).
  - **캐릭터별 레벨·경험치·SP(7절)는 사용자 지시로 먼저 들어간다**(2026-09-30, `feature/char-progress`). 섬영 브랜치는
    `PlayerProfile`에서 이것도 같이 받는다 — `level`/`exp`/`spUsed`가 **지금 고른 캐릭터 칸을 가리키는 프로퍼티**가
    됐다(이름은 그대로라 호출부는 안 바뀐다). 섬영 갈래·층 필드는 마법사·메카닉처럼 그냥 추가하면 되고,
    `TryLearn*Tier`도 같은 모양(`SpAvailable`·`spUsed` = 지금 캐릭터 몫)이면 섬영 SP는 섬영 몫이 된다.

- **씬(`CombatCore.unity`)과 `BuildPartAScene.cs`는 나만 만진다.** 팀원이 키트를 씬에 붙여야 하면 요청할 것.
- **새 프리팹을 만드는 건 자유**(충돌 안 남). 기존 `Enemy_Base.prefab` 수정은 이번 판에선 나.
- 새 공용 폴더가 필요하면 각자 만들지 말고 **`main`에 폴더만 먼저 만드는 작은 커밋**을 올린다(`.meta` add/add 충돌 방지).
- `PROGRESS.md`는 병합 때 거의 항상 충돌한다 — 정상이다. 로그는 양쪽 다 남기고 상태 섹션만 새로 쓴다.

## 9. 범위 밖 (지금 스프린트 ①에서 하지 않는다)

- ~~X 스킬·전문화 티어 전부~~ → **지금 스프린트(①)다**(2026-09-30 정리). 마법사·메카닉 5층 ✅, 섬영 진행 중.
- ~~캐릭터별 레벨·경험치·SP 분리~~ → ✅ 2026-09-30 원본대로(7절, `feature/char-progress`).
- **조건부 특수 아이템 11종** — 정의는 다 있고 가챠에서도 나오지만 효과가 안 붙어 있다(아래 참고).
- 사운드·파티클·화면 흔들림·데미지 숫자 팝업
- 원본 렌더링 코드 자체(`:4482`~`:6215`)의 이식. **그림은 이미 PNG로 구워 쓰고 있다**
  (`docs/prototype-sprites.md`) — 원본 Canvas 드로잉을 옮기는 게 범위 밖이라는 뜻이다.

### 미완 목록 (범위 밖이 아니라 "아직 안 한 것")

| 항목 | 상태 |
|---|---|
| 섬영 1층~ (집중·칼날폭풍) | 팀원 진행 중(①). 0차는 ✅ 2026-09-28. 드루이드는 기획 제외 |
| 조건부 특수 아이템 11종 | `ambush`·`giantSlayer`·`crowd`·`execute`·`lastStand`·`airDmg`·`killSpeed`·`killRage`·`plague`·`deathBlast`·`lifesteal` — 피해 계산에 **대상/플레이어 컨텍스트**를 넘겨야 붙는다 |
| 보스 체력바 | 원본 `#bossbar`(`:49`). HUD에 자리만 없다. (스킬 쿨다운 슬롯은 ✅ 2026-09-29) |
| 연쇄 처치 슬로모 | 원본 연출 |
| uGUI 전환 | 지금은 IMGUI. UI 층 밖은 안 바뀌게 짜여 있다 |

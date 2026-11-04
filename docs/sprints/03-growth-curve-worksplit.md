# [아카이브] 스프린트 3 분업 — 성장곡선 검증 ✅ 완료(2026-09-08)

> ## ⚠️ 완료된 분업의 기록이다. **현재 분업은 `docs/worksplit.md`가 소유한다.**
> 여기 적힌 트랙 A/B는 둘 다 구현·검증·`main` 병합이 끝났고, 아래 "공유 계약" 코드 블록은
> 전부 실제 코드가 됐다(`Core/PlayerProfile`, `Core/DifficultyScalingConfig`, `Systems/RunProgress` 등).
> 또한 아래 "확정된 세부 결정"의 **R키 부활·숫자키 강화는 이후 폐기됐다** —
> `docs/sprints/03-growth-curve.md` 상단 표 참고.
>
> 근거였던 초안 `HANDOFF_sprint2_draft.md`는 당시 `HANDOFF.md`로 승격됐고, 지금은
> `docs/sprints/03-growth-curve.md`로 아카이브돼 있다(같은 문서).

## 0단계 (선행 작업 — 내가 처리, 팀원은 대기)

1. `feature/health-damage` → `main` 병합
   - 초안의 "1. 체력 시스템" 항목(`IDamageable`, 플레이어 체력 100/무적 0.9초, 고블린 체력 38)이 여기 이미 구현·검증(PlayMode 30/30) 완료돼 있음 — 처음부터 새로 만들지 말 것.
2. `Core/PlayerProfile.cs` 필드 스캐폴딩(로직 없이 필드만) — 아래 "공유 계약" 그대로.

**팀원은 이 두 커밋이 `main`에 올라갔다는 연락을 받은 뒤 착수할 것.**

---

## 공유 계약 (임의로 이름/타입 바꾸지 말 것)

### `Core/PlayerProfile.cs`
```csharp
namespace ChronoSavior.Core
{
    public class PlayerProfile
    {
        public int level = 1;
        public int exp = 0;
        public int gold = 0;
        public int spUsed = 0;
        public UpgradeLevels upgrades = new UpgradeLevels();

        // 트랙 A는 이 두 메서드만 호출한다. gold/exp 필드를 직접 += 하지 말 것
        // — exp 쪽은 레벨업 판정이 같이 걸려야 해서 트랙 B가 내부 로직을 채운다.
        public void AddGold(int amount) => gold += amount;
        public void AddExp(int amount) { /* 트랙 B가 구현 */ }
    }

    [System.Serializable]
    public class UpgradeLevels
    {
        // 원본 필드명 그대로 쓰고 싶었으나 `as`는 C# 예약어라 `atkSpeed`로 대체함(원본 CONFIG.upgrades.as).
        public int atk, hp, ms, atkSpeed, crit;
    }
}
```

### 접근 방법
```csharp
Core.ProfileService.Current.AddGold(5);
Core.ProfileService.Current.AddExp(8);
Core.ProfileService.Current.upgrades.atk // 강화 단계 읽기
```
`ProfileService`는 `FieldBounds`처럼 정적 클래스 하나 — 씬에서 오브젝트 찾아다닐 필요 없음.

### `Enemies/EnemyHealth.cs`에 추가되는 것 (내가 0단계에서 같이 넣어둠)
```csharp
public void SetMaxHp(float newMaxHp) { maxHp = newMaxHp; CurrentHp = newMaxHp; }
public event System.Action<EnemyHealth> Died; // Die() 안에서 Destroy 직전에 Invoke
```

⚠️ **이 프로젝트 단골 함정**: `Instantiate` 직후 `enemyHealth.maxHp = X`처럼 필드만 바꾸면 `Awake()`가 이미 실행돼서 `CurrentHp`는 옛날 값(38) 그대로 남는다(`RequireComponent` 자동보충·`AddComponent` Awake타이밍과 같은 종류의 함정, 이 프로젝트에서 이미 3번 겪음). **반드시 `SetMaxHp()`를 통해서 바꿀 것.**

---

## 트랙 A (팀원) — 처치 보상 + 난이도 스케일링

**건드리는 파일**: `Core/DifficultyScalingConfig.cs`(신규, 숫자만 — 아래 0번), `Systems/RunProgress.cs`(신규), `Enemies/EnemyHealth.cs`(이미 있는 `SetMaxHp`/`Died` 호출만), `Enemies/EnemyMove.cs`(`attackPower` 배율), `Systems/EnemySpawner.cs`(스폰 시 배율 적용 한 줄)
**건드리면 안 되는 파일**: `Core/PlayerProfile.cs`(필드 추가/이름변경 금지, 메서드 호출만), `Core/GoldUpgrade.cs`·`Core/PlayerStatCalculator.cs`(트랙 B 소유), `Characters/` 전체

### 0. ⚠️ 착수 전 필독 — 숫자는 반드시 한 곳에 모아둘 것 (2026-09-08 추가)

**원본 밸런스 자체가 완전히 정리된 건 아니다** — 확인해보니 `CONFIG.souls`/`CONFIG.scale.soulGrow`(구버전 "혼 강화" 잔재, 지금은 아무 데서도 안 읽는 죽은 설정값)처럼 리밸런스 후 정리 안 된 게 실제로 있다(`project_test.html:701,:727`, 참조 안 하는 것 직접 확인함). **지금은 원본 수치를 그대로 쓰지만, 나중에 우리가 직접 세밀하게 조정할 걸 전제로 구조를 짠다** — "값은 원본 그대로, 위치만 나중에 한 파일만 고치면 되게" 하는 게 이번 요구사항이다.

그래서 아래 2·3번처럼 로직 코드 안에 `Mathf.Pow(2.15f, ...)`를 직접 박지 말고, **먼저 이 파일부터 만들 것**:

```csharp
// Core/DifficultyScalingConfig.cs — 트랙 B의 Core/GoldUpgrade.cs·PlayerStatCalculator.cs와 같은 패턴
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 난이도 스케일링 + 처치 보상 수치를 전부 여기 모았다. 나중에 밸런스를 조정할 때 이 파일
    /// 상수만 바꾸면 되고 EnemySpawner/EnemyMove 로직 코드는 안 건드려도 된다.
    /// 지금 값은 전부 원본 그대로(project_test.html:699,:709,:712,:727) — 임의로 바꾸지 말 것,
    /// 조정은 나중에 실제 플레이해보고 사용자가 결정한다.
    /// </summary>
    public static class DifficultyScalingConfig
    {
        public const int KillsPerRegionLevel = 100; // 원본 regionKillTarget(:699)

        public const float HpPerRegion = 2.15f;     // CONFIG.scale.hpPerRegion(:727)
        public const float DmgPerRegion = 1.4f;     // CONFIG.scale.dmgPerRegion(:727)
        public const float RewardGrow = 1.42f;      // CONFIG.scale.rewardGrow(:727)

        public const float OniBaseHp = 38f;         // CONFIG.enemyBase.oni.hp(:709)
        public const float OniBaseDmg = 13f;        // CONFIG.enemyBase.oni.dmg(:709)
        public const float OniBaseExp = 8f;         // CONFIG.enemyBase.oni.exp(:709)
        public const int OniGoldMin = 5;            // CONFIG.enemyBase.oni.gold[0](:709)
        public const int OniGoldMax = 10;           // CONFIG.enemyBase.oni.gold[1](:709) — 포함 상한
        public const float GoldDropChance = 0.75f;  // CONFIG.goldDropChance(:712)

        public static float ScaledHp(int regionLv) => OniBaseHp * Mathf.Pow(HpPerRegion, regionLv - 1);
        public static float ScaledDmg(int regionLv) => OniBaseDmg * Mathf.Pow(DmgPerRegion, regionLv - 1);
        public static float RewardMultiplier(int regionLv) => Mathf.Pow(RewardGrow, regionLv - 1);
    }
}
```

아래 2·3번의 예시 코드는 이 클래스를 쓰도록 갱신했다 — 그대로 따를 것.

### 1. `regionLv` 카운터 (`Systems/RunProgress.cs` 신규)
```csharp
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    public static class RunProgress
    {
        public static int TotalKills { get; private set; }
        public static int RegionLv => 1 + TotalKills / DifficultyScalingConfig.KillsPerRegionLevel;
        public static void RegisterKill() => TotalKills++;
    }
}
```

### 2. 몹 스탯 스케일링 (`EnemySpawner`가 스폰 직후 호출)
```csharp
var health = enemy.GetComponent<EnemyHealth>();
health.SetMaxHp(DifficultyScalingConfig.ScaledHp(RunProgress.RegionLv));
var move = enemy.GetComponent<EnemyMove>();
move.attackPower = DifficultyScalingConfig.ScaledDmg(RunProgress.RegionLv);
health.Died += HandleEnemyDied; // 아래 3번
```

### 3. 처치 보상 (`EnemyHealth.Died` 구독)
```csharp
void HandleEnemyDied(EnemyHealth enemy)
{
    RunProgress.RegisterKill();
    float sc = DifficultyScalingConfig.RewardMultiplier(RunProgress.RegionLv);
    Core.ProfileService.Current.AddExp(Mathf.RoundToInt(DifficultyScalingConfig.OniBaseExp * sc));
    if (Random.value < DifficultyScalingConfig.GoldDropChance)
    {
        int gold = Random.Range(DifficultyScalingConfig.OniGoldMin, DifficultyScalingConfig.OniGoldMax + 1);
        Core.ProfileService.Current.AddGold(Mathf.RoundToInt(gold * sc));
    }
}
```

### 확정된 세부 결정
- `regionLv` 트리거: **누적 처치 수**(위 코드 그대로) — 시간/레벨 기준 아님
- 엘리트·연쇄처치·살기 보너스는 범위 밖 — "몹 1마리 = 고정 공식" 루프만
- **숫자는 전부 `Core/DifficultyScalingConfig.cs` 하나에만** — `EnemySpawner`/`RunProgress`/보상 계산 로직 안에 매직넘버를 직접 쓰지 말 것(위 0번 참고)

---

## 트랙 B (나) — 레벨업 + 골드 강화 + 스탯 적용

**건드리는 파일**: `Core/PlayerProfile.cs`(로직 채움), `Characters/CharacterMover2D.cs`·`MageAttack.cs`·`PlayerHealth.cs`(스탯 배율 연결), 디버그 입력·표시용 신규 파일
**건드리면 안 되는 파일**: `Enemies/` 전체, `Systems/EnemySpawner.cs`

### 1. 레벨업 (`PlayerProfile.AddExp` 내부)
```
필요경험치(l) = floor(700 × 1.8^(l-1))   // expCurve, project_test.html:706
```
초과분 이월, 한 프레임에 여러 레벨 가능(원본 `gainExpMeta`, `:1440`).

### 2. 골드 강화 5종 (원본 `CONFIG.upgrades`, `:731`)
| 스탯 | 효과(레벨당) | 비용 base | 비용 mult |
|---|---|---|---|
| atk | +3 | 22 | ×1.14 |
| hp | +25 | 20 | ×1.14 |
| ms | +4%p | 18 | ×1.22 |
| atkSpeed | +4%p | 18 | ×1.22 |
| crit | +2%p | 25 | ×1.28 |

비용 공식: `cost(lv) = floor(base × mult^lv)`

### 3. 파생 스탯 → 기존 컴포넌트 연결
```
statAtk    = 10 + upgrades.atk × 3        → MageAttack.baseDamage
statMaxHp  = round(100 + upgrades.hp × 25) → PlayerHealth.maxHp
statMs     = 1 + upgrades.ms × 0.04        → CharacterMover2D.moveSpeed 배율
statCrit   = 0.10 + upgrades.crit × 0.02   → 치명타 확률(Combat.DamageCalculator 쪽 연동 필요)
```

### 확정된 세부 결정
- HP 0 처리: **정지 + R키로 그 자리에서 재시작**(체력 꽉 채움) — 죽을 때마다 에디터 재시작해야 하면 "체감 벽" 검증 자체가 안 됨
- 강화 입력: **숫자키 1~5** + **`OnGUI` 최소 디버그 텍스트**(현재 골드/레벨/각 강화 단계 표시) — 표시가 없으면 체감 검증이 불가능해서 초안에 추가함

---

## 병합 순서

```
feature/health-damage → main          (내가 지금 처리)
PlayerProfile 스캐폴딩 → main          (내가 이어서 처리, 작은 커밋 1개)
        ↓ (여기서부터 동시 진행 가능)
트랙 A ─────────────┐
트랙 B ─────────────┤→ 먼저 끝난 쪽 main 병합 → 나중 쪽은 main 받아서 병합
```

씬(`CombatCore.unity`)과 `BuildPartAScene.cs`는 둘 다 건드릴 수 있으니(디버그 UI 오브젝트, RunProgress 등) **나중에 병합하는 쪽이 씬 조립 부분만 마지막에 한 번 정리**할 것 — 동시에 손대지 말 것(CLAUDE.md 협업규칙 1번).

## 검증 방법 (공통)
- 수치(공식)는 각자 PlayMode 테스트로
- "5지역쯤(`RegionLv≈5`) 체감 벽" 여부는 둘 다 합쳐진 뒤 **직접 플레이**로 확인 — 자동 테스트로는 "체감"을 못 잡음

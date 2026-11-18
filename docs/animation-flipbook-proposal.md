# 캐릭터·보스 애니메이션 방식 제안: Animator 대신 코드 플립북으로 통일

> **상태(2026-11-17): 트랙 A와 마법사 차지 그림은 끝났다** — 사용자 결정으로 사용자 세션이 진행(`PROGRESS.md` 로그 132). 보스(트랙 B의 보스 항목)는 팀원이 보스 작업 때 같이 본다.
> 실제로 정한 것 중 아래와 다른 점: 96×48 재캔버스 대신 **묶음별 기준점**(같은 효과), 공용 타입에 `SpriteFlipbook.FrameAtHeld`·`CharacterFlipbook.PlayAndHold/ReleaseHeldPose` 추가(GPT 상의), 살수 공격은 묶음 하나(팀원이 좌우반전 12장을 되돌림).
> 아래는 제안 당시 기록이다.
> 쓴 날: 2026-11-17. 근거: main `668231c`·`057c9c8`(2026-11-16)을 코드 리뷰(Claude + GPT)한 결과.
> 읽는 사람: 팀원, 그리고 맥락이 전혀 없는 다른 클로드 세션. 채팅 내용을 몰라도 이 문서만 읽으면 작업할 수 있게 썼다.

---

## 0. 먼저 사과와 배경

- 2026-11-15에 우리 쪽(사용자 + Claude + Codex)이 **"캐릭터 애니메이션은 Animator 말고 코드 플립북으로 한다"**고 정했다.
  그런데 **이 결정을 어느 문서에도 적지 않았다.** 그래서 팀원은 모르는 채로 Animator 방식을 만들었다.
  이번 어긋남은 팀원 잘못이 아니라 **문서를 안 남긴 우리 쪽 누락**이다.
- 팀원이 만든 것 중 **프레임 그림, 상태별 그림 규칙, 보스 연출, 마법사 차지 구체·일렁임, 탄 그림 대/소 구분은 대부분 그대로 살린다.**
  바꾸는 건 "프레임을 누가 어떻게 넘기느냐"(Animator 부분)뿐이다.

## 1. 왜 바꾸자고 하나 (리뷰에서 나온 문제)

| 심각도 | 지금 상태 | 문제 |
|---|---|---|
| 높음 | 플레이어·로비는 Animator, 보스(`BlobLordBoss.UpdateBodySprite`)는 코드로 프레임 넘김 | 한 게임 안에 애니메이션 방식이 둘 |
| 높음 | 공격·피격 모션을 **효과음 이벤트**(`SfxEvents.MageBasicAttack` 등)로 켬 | 소리를 바꾸거나 빼면 모션도 사라진다. 메카닉 레이저처럼 계속 나가는 공격은 효과음 타이밍과 모션 길이가 안 맞는다 |
| 높음 | `CharacterBase.controller`에 차지 상태를 손으로 추가했지만 생성 메뉴(`BuildCharacterAnimations`)는 그걸 모름 | 메뉴를 다시 누르면 컨트롤러를 지우고 새로 만들어서 **차지 모션이 사라진다**. 저장소 루트 `Claude outputs/`에 같은 스크립트 사본도 있다 |
| 높음 | `PlayerRig`·`LobbyAvatar`가 `AddComponent<Animator>()` | 생성 담당 규칙 위반. `CreationOwnershipTests` 실패 |
| 높음 | main에 테스트 4개가 새로 깨진 채 올라감 | 아래 6절 |
| 중간 | 크기를 `chara_temp_01`(옛 임시 그림) 기준으로 한 번 정하고, 실제로는 다른 크기의 프레임이 그려짐 | 프레임 캔버스가 32×48 / 50×48 / 70×48로 제각각이라 위치·크기가 어긋날 수 있다 |
| 중간 | 보스·마법사 구체 그림을 `Resources.Load("Art/...")` 경로 문자열로 불러옴 | 경로 문자열 로드는 **git 밖 라이선스 이펙트만의 예외**였다. 일반 그림은 에셋에 직렬화 참조로 연결하는 게 규칙 |
| 중간 | 보스 그림 갱신이 `GetHorizontalSpeed`(물리 `FixedUpdate`가 부름) 안에 있음 | 그림 갱신이 물리 주기에 묶인다 |
| 중간 | 죽은 보스가 사망 그림 1초 동안 콜라이더·`Enemy` 태그를 유지 | 레이저·유도탄 같은 대상 고르기가 시체를 겨눌 수 있다 |
| 낮음 | 보스 사망 연출이 `WaitForSeconds`(시간 배율을 따름) | 타격 멈춤 중엔 사망 연출도 멈춘다. 의도인지 정해야 한다 |
| 낮음 | 마법사 구체와 일렁임이 같은 `sortingOrder` | 어느 쪽이 앞인지 코드에 안 정해져 있다 |

**코드 플립북으로 가는 이유:** 이 게임은 공격 중, 차지 중, 이동 잠금, 타격 멈춤(timeScale 0), 로비의 실제 시간 같은 상태를 **이미 코드가 다 알고 있다.**
Animator를 쓰면 그 상태를 매 프레임 Animator 파라미터로 다시 복사해야 하고, 전이 규칙은 컨트롤러 파일(손으로 고치기 어렵고, 병합하면 깨지는 YAML)에 따로 산다.
코드 플립북은 "지금 상태 → 이 프레임 묶음 → 시간으로 넘김"이라 상태가 한 군데에만 있고, 테스트로 확인하기도 쉽다.
보스는 이미 이 방식이라 그대로 합치면 된다.

**잘한 점(유지):** 보스 사망을 `Core/IDeathAnimated`로 뒤집은 것(층 규칙을 지킴), 탄 그림 키 선택을 `MageProjectile.BoltKey` 한 곳에 모은 것,
로비에서 애니메이션이 멈추던 문제를 실제 시간으로 푼 것, 프레임을 처음 한 번만 불러 두는 캐시.

---

## 2. 착수 전제 (먼저 main에 있어야 하는 것)

1. **팀원이 이 문서에 동의**하고, 아래 3절 계약과 7절 결정을 확인한다. 바꾸고 싶은 데가 있으면 이 문서를 고쳐서 커밋한다.
2. 그동안 팀원은 `PlayerRig.cs`·`LobbyAvatar.cs`·`CharacterBase.controller`·`BuildCharacterAnimations.cs`를 **더 고치지 않는다**(트랙 A가 바꿀 파일).
3. 트랙 A가 시작할 때 `PROGRESS.md`에 "애니메이션 플립북 전환 진행 중 — 위 파일 손대지 말 것"을 적고 커밋·푸시한다.

## 3. 공유 계약 — 이 이름·시그니처 그대로 쓸 것 (임의로 이름·타입 바꾸지 말 것)

```csharp
// Core/SpriteFlipbook.cs — namespace ChronoSavior.Core
// 프레임 묶음 하나. 그림은 직렬화 참조(경로 문자열 아님).
[CreateAssetMenu(menuName = "ChronoSavior/Sprite Flipbook")]
public class SpriteFlipbook : ScriptableObject
{
    public Sprite[] frames;
    public float fps = 12f;
    public bool loop = true;
    public Sprite FrameAt(float t);   // loop면 반복, 아니면 마지막 프레임에서 멈춤
    public float Duration { get; }    // frames.Length / fps
}

// Core/CharacterPose.cs — namespace ChronoSavior.Core
public enum CharacterPose { Idle, Move, Rise, Fall, Charge, Attack, Hit }

// Core/CharacterAnimSet.cs — namespace ChronoSavior.Core
// 캐릭터 하나의 모든 포즈. Assets/Data/Characters/Anim_<캐릭터>.asset
public class CharacterAnimSet : ScriptableObject
{
    public SpriteFlipbook idle, move, rise, fall, charge, hit;
    public SpriteFlipbook[] attacks;   // 살수처럼 번갈아 쓰는 공격(1~6 / 7~12)은 여러 개, 나머지는 1개
    public SpriteFlipbook Get(CharacterPose pose, int attackIndex = 0);  // 없으면 idle
}
// CharacterArtSet: animators[](RuntimeAnimatorController) 대신 animSets[](CharacterAnimSet) — AnimSetOf(id)

// Characters/CharacterFlipbook.cs — namespace ChronoSavior.Characters
// 씬의 Player, 로비 아바타 프리팹에 **배치**한다(코드로 AddComponent 하지 않음).
public sealed class CharacterFlipbook : MonoBehaviour
{
    public bool useUnscaledTime;                         // 로비 true, 전투 false
    public void SetAnimSet(CharacterAnimSet set);        // 캐릭터를 바꿀 때
    public void SetLoopPose(CharacterPose pose);         // 매 프레임: Idle/Move/Rise/Fall/Charge 중 하나
    public void PlayOnce(CharacterPose pose, int attackIndex = 0); // Attack/Hit — 끝나면 루프 포즈로 돌아감
    public CharacterPose Current { get; }                // 테스트용
}
```

**포즈 우선순위(위가 이김):** `Hit` > `Attack` > `Charge` > `Rise`/`Fall` > `Move` > `Idle`

**누가 신호를 주나 (효과음 이벤트에 기대지 않는다):**

| 신호 | 보내는 곳 |
|---|---|
| 루프 포즈(Idle/Move/Rise/Fall/Charge) | `PlayerRig.LateUpdate`가 `CharacterMover2D`(접지·속도)와 현재 키트(`IsCharging`)를 보고 매 프레임 `SetLoopPose` |
| 공격 | 각 공격 스크립트가 **발사·베기 시점에 직접** `PlayOnce(Attack, index)` — `MageAttack`(발사), `GunnerAttack`(사격·레이저), `BladeCombat`(베기, 손 번갈아 index 0/1) |
| 피격 | `PlayerHealth`가 피해를 실제로 받았을 때 `PlayOnce(Hit)` |
| 로비 | `LobbyAvatar`가 위치 변화로 Idle/Move만 |

좌우 반전은 지금처럼 `SpriteRenderer.flipX`(그림이 왼쪽을 보는 게 기본, 오른쪽일 때 뒤집음). `localScale`은 건드리지 않는다.

## 4. 트랙별 담당 (제안 — 바꿔도 됨)

### 트랙 A — 사용자 세션(Claude): 플레이어·로비 플립북 전환
1. 3절의 `SpriteFlipbook`·`CharacterPose`·`CharacterAnimSet`·`CharacterFlipbook` 추가.
2. 캐릭터 프레임 전부를 **공통 캔버스 96×48**(가운데 정렬, 발 위치 동일)로 다시 맞춘다. 원본 프레임은 그대로 두고 맞춘 사본을 만든다. 겹쳐 보기 캡처로 발·몸 위치가 안 튀는지 확인한다.
3. 팀원 클립의 프레임 순서·fps를 그대로 옮긴 `CharacterAnimSet` 3개(마법사·메카닉·살수). fps: 서기 8, 이동 12, 점프/하강 10, 피격 12, 공격 12, 마법사 차지 6.
4. `PlayerRig`·`LobbyAvatar`에서 Animator 코드를 빼고 `CharacterFlipbook`으로. 크기는 96×48 캔버스 높이 기준으로 한 번 정한다.
5. 공격·피격 신호를 3절 표대로 각 스크립트에서 직접.
6. Animator 관련 에셋(`Assets/Data/Characters/Animations/`·`Animators/`)과 `Editor/BuildCharacterAnimations.cs`, 루트 `Claude outputs/` 사본 삭제.
7. 깨진 테스트 복구 + 새 테스트(포즈 우선순위, 공격 1회 재생 후 복귀, 로비 실제 시간, 캐릭터 전환).

### 트랙 B — 팀원: 보스·마법사 차지 그림 정리
1. `BlobLordBoss`: `Resources.Load` 경로 대신 프리팹에 `SpriteFlipbook` 필드(대기·튀어오르기·투척·이동·피격·사망)로 연결. 상태별 프레임 고르는 규칙은 **그대로**.
2. 보스 그림 갱신을 `GetHorizontalSpeed`에서 꺼내 `LateUpdate`로.
3. 보스 사망 연출 중: 콜라이더를 끄거나 대상 고르기에서 빠지게(7절 결정대로).
4. `MageChargeVfx`: 구체 11장·일렁임 6장을 `Resources.Load` 대신 직렬화 필드(`SpriteFlipbook` 둘)로. 일렁임을 구체 앞(+1)에 둘지 뒤에 둘지 정해서 `sortingOrder`를 다르게.
5. `docs/asset-swap-points.md`의 보스·차지 줄을 새 연결 위치로 고친다.

## 5. 건드리는 파일 / 건드리면 안 되는 파일

| | 트랙 A (Claude) | 트랙 B (팀원) |
|---|---|---|
| 건드림 | `Core/SpriteFlipbook.cs`·`CharacterPose.cs`·`CharacterAnimSet.cs`(새), `Core/CharacterArtSet.cs`, `Characters/CharacterFlipbook.cs`(새), `PlayerRig.cs`, `UI/Lobby/LobbyAvatar.cs`, `MageAttack.cs`·`GunnerAttack.cs`·`BladeCombat.cs`·`PlayerHealth.cs`(신호 한 줄씩), `Assets/Data/Characters/*`, 캐릭터 프레임 사본, 관련 테스트, **씬 `20_Combat`·`10_Lobby`(컴포넌트 배치)** | `Enemies/BlobLordBoss.cs`, `Prefabs/Enemies/Enemy_BlobLord.prefab`, `Characters/MageChargeVfx.cs`, 보스·차지 `SpriteFlipbook` 에셋, `MageChargeVfxTests.cs`, 보스 테스트, `docs/asset-swap-points.md` |
| 손대지 말 것 | 보스·`MageChargeVfx` | `PlayerRig`·`LobbyAvatar`·씬 파일·`CharacterArtSet` |

`SpriteFlipbook`은 트랙 A가 **먼저 main에 작은 커밋으로** 올린다(트랙 B도 쓰니까). 트랙 B는 그걸 받은 뒤 시작.

## 6. 완료 정의 (DoD)

- 전체 PlayMode 테스트 통과(남는 실패는 팀장 결정 대기인 `DotScaleTests` 마법사 0.5 하나만).
- 지금 main에서 새로 깨진 테스트 4개가 고쳐짐:
  `CreationOwnershipTests.OnlyListedOwners`(PlayerRig·LobbyAvatar — 이제 `AddComponent`가 없어서 자연히 통과, `GameplaySfx`는 이유를 적어 목록에 추가),
  `GameBootTests.LobbyAvatar_WearsTheSavedCharacter`·`Player_WearsTheSavedCharactersArt_FromTheArtAsset`·`LobbySceneTests.Avatar_WearsTheProfileCharacter`(정지 그림 대신 "그 캐릭터의 애니메이션 묶음을 입었나"를 본다).
- 씬 구성 검증(`ChronoSavior/씬 구성 검증`) 통과.
- 캡처로 확인: 세 캐릭터 서기·이동·점프·하강·공격·피격, 마법사 차지, 좌우 반전, 로비 걷기, 보스 상태별 그림·사망.
- 레포 안에 Animator 컨트롤러·애니메이션 클립이 남지 않음. `Resources.Load("Art/...")`가 일반 그림에 남지 않음.

## 7. 확정할 세부 결정 (팀원 의견 받아서 결론만 남긴다)

| # | 질문 | 제안 |
|---|---|---|
| 1 | 점프/하강 8장을 한 루프로 돌릴지, 올라갈 때(앞 4장)·내려올 때(뒤 4장)로 나눌지 | **나눈다**(속도 y 부호로 Rise/Fall) |
| 2 | 공격 중 또 공격하면 처음부터 다시 재생할지 | 팀원이 정한 대로 **끝까지 보여주고 무시**. 단 살수는 손 번갈아 다음 묶음으로 |
| 3 | 메카닉 레이저(누르는 동안 계속)는 | 레이저가 켜져 있는 동안 공격 마지막 프레임을 **유지** |
| 4 | 피격 모션이 공격 모션을 끊을지 | **끊는다**(우선순위 Hit > Attack) |
| 5 | 보스 사망 그림 중 시체를 칠 수 있나 | **못 친다** — 죽는 순간 콜라이더 끔 |
| 6 | 보스 사망 연출이 타격 멈춤 중에도 흐를지 | **같이 멈춘다**(게임 시간). 지금 동작 유지 |
| 7 | 마법사 일렁임은 구체 앞인가 뒤인가 | 그림 보고 팀원이 정함 |
| 8 | 워리어 그림(`Warrior_Unmapped`, 100×60) | 이번 범위 밖, 매핑 미정 그대로 |

## 8. 병합 순서 + 씬·프리팹 소유권

1. 트랙 A: `SpriteFlipbook`만 담은 작은 커밋 → main.
2. 트랙 B: 보스·차지 정리 → main (프리팹 `Enemy_BlobLord`는 트랙 B만 만진다).
3. 트랙 A: 플레이어·로비 전환 → main (씬 `20_Combat`·`10_Lobby`는 트랙 A만 만진다).
- main에 합치기 전엔 각자 `git fetch && git rebase origin/main` 후 **전체 테스트**. 테스트를 못 돌렸으면 합치지 않는다.

## 9. 범위 밖 (이번에 안 하는 것)

- 다른 몹 애니메이션, 워리어 매핑, 드루이드.
- 도트 배율(배율 2 vs Transform 1) 문제 — 팀장 결정 대기.
- 새 그림 그리기. 지금 있는 프레임만 쓴다.
- 이펙트 팩(라이선스 VFX) 쪽 구조 — 지금 방식(키로 불러오기) 그대로.

---

### 팀원이 먼저 할 일 (별개)
이펙트가 안 보이는 건 코드 문제가 아니라 **이펙트 팩을 안 가져와서**다. `docs/vfx-credits.md`의 팩 4개(zip 5개)를 한 폴더에 모아
Unity 메뉴 `ChronoSavior → VFX 팩 가져오기`로 그 폴더를 고르면 된다.

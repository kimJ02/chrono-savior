# 에셋 교체 지점 표 (2026-11-06)

> 그림·효과·소리는 **외주/구매로 갈아끼운다**(지금 것은 임시). 이 표는 "무엇을 바꾸려면 어디를 바꾸나"를 한곳에 모은 것이다.
> 도트 크기 기준은 [`pixel-standard.md`](pixel-standard.md), 진행 계획은 `HANDOFF.md` '중간 점검과 남은 계획'.
> **교체 = 에셋(그림 파일·데이터 칸·프리팹)만 바꾸고 코드는 안 고친다**가 목표다. '코드 수정 필요'라고 적힌 곳만 예외로 남아 있다.

## 바로 바꿀 수 있는 곳 (에셋만)

| 무엇 | 바꾸는 곳 | 같이 볼 값 |
|---|---|---|
| 스테이지 땅·발판·배경·보스 포탈 그림 | `Assets/Data/Stages/StageArt_Forest`(`StageArtSet`) — 지역이 늘면 에셋을 하나 더 | 배경 배율·보여 줄 범위·풀밭 높이, 포탈 배율(같은 에셋) |
| 캐릭터 그림(마법사·메카닉·섬영) | `Assets/Data/Characters/CharacterArtSet` | 캐릭터별 색(`tints`)·화면 높이(`heights`, 판정과 따로) |
| 잡몹 그림 | `Assets/Data/Enemies/<종류>`(`EnemyData.sprite`) — 지금 대부분 임시 도형 | 그림 화면 높이(`visualHeight`, 판정 `colliderRadius`와 따로). ⚠️ `BuildEnemyData`를 다시 돌리면 덮어쓴다 — 그 도구의 값도 같이 고칠 것 |
| 보스·성소 그림 | `Assets/Data/Enemies/MonsterCatalog`(`boss`·`shrine`) | 그림 화면 크기(`bossSize`·`shrineSize`, 판정과 따로) |
| 몹 종류별 구성(행동 부품) | `Assets/Prefabs/Enemies/Enemy_*` | — |
| 투사체·구슬·드론·포탑 그림 | `Assets/Prefabs/Combat/*`의 `Visual` 자식 그림 | 크기는 쏠 때 코드가 넣는다(판정 상자와 같은 크기) — 그림 비율이 달라지면 확인 |
| 타격음 | `00_Persistent` › `Audio`의 `HitSound.clip` | 소리 크기는 설정 창 효과음 값 |
| 섬영 베기 효과 프레임 | `20_Combat` › `Player`의 `BladeSlashVfx.frames` | 배율 `BladeSlashVfx.Scale`(코드 상수 2) |
| 피격·차지·잔상 깜빡임 머티리얼 | `Assets/Materials/SpriteFlash`(몹 프리팹 `Enemy_Base`, Player의 `MageChargeFlash`·`PlayerAfterimages`) | 셰이더 `Assets/Resources/Shaders/SpriteFlash` |

## 코드 수정이 필요한 곳 (실제 효과가 오면 바꾼다)

| 무엇 | 지금 | 바꿀 방법(그때) |
|---|---|---|
| 중력 착탄 폭발·중력장 소용돌이 그림 | `GravityImpactVfx`가 `Resources/Art/Figma/Vfx/GravityImpact`를 **폴더 경로로** 읽는다(소용돌이도 같은 그림). 정적 함수(`MageSkillEffects`)가 그때그때 만들어 그림을 넘길 주인이 없다 | 효과 프리팹으로 만들고, 쏘는 쪽(`MageAttack`)이 들고 넘긴다(투사체와 같은 방식). 크기는 스킬 반경에 맞춰 늘리는 지금 방식을 다시 볼 것(`pixel-standard.md`) |
| 대붕괴 폭발 고리(원판·선) | `GravityBurstRing`이 `Shader.Find("Sprites/Default")`로 머티리얼을 만들고 메시·선을 코드로 그린다 | 외주 효과가 그림/프레임이면 효과 프리팹으로 교체 |
| 레이저 구간·설치물 연결선 | `GunnerLaserBeam`·`GunnerAttack`이 `GunnerAttack.bulletSprite`를 코드로 늘려 그린다 | 외주 효과 형식에 맞춰 프리팹/프레임으로 |
| 처치 연출·화면 흔들림 등 | 코드(`HitFeel`) — 그림 없음 | 필요하면 효과 프리팹 추가 |

## UI (팀원 제작 중 — 따로)

- UI 그림은 `Assets/Resources/Art/Figma/UI/`를 **경로 문자열로** 읽는다(`UI/Pixel/PxArt.Get("…")` 64곳, 글꼴 `Px.Font` = `Resources/Fonts/DNFBitBitv2`). 화면 코드 `Build()`가 Figma 좌표로 짓는다.
- 지금 상태: 프로젝트에 들어간 것 / Figma에만 있는 것 / 제작 중인 것이 섞여 있다. **같은 파일 이름으로 덮어쓰면** 코드 수정 없이 바뀐다(경로가 같으면).
- 교체 방식(경로 유지 vs 데이터 에셋)은 UI 그림이 정리된 뒤 정한다. UI 기준은 월드와 별도(640×360 판·100 PPU, `pixel-standard.md` 3절).

## 바꾼 뒤 확인

1. 메뉴 `ChronoSavior/씬 구성 검증` — 빈 칸·빠진 연결을 잡는다.
2. PlayMode 테스트 전체.
3. 캡처 도구(`CaptureStagePreview`·`CaptureUiPreview`)로 `docs/baseline-2026-11-06/`과 나란히 비교.
4. 도트가 어색하면 `pixel-standard.md`의 배율 표를 다시 계산한다.

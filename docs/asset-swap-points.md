# 에셋 교체 지점 표 (2026-11-06)

> 그림·효과·소리는 **외주/구매로 갈아끼운다**(지금 것은 임시). 이 표는 "무엇을 바꾸려면 어디를 바꾸나"를 한곳에 모은 것이다.
> 도트 크기 기준은 [`pixel-standard.md`](pixel-standard.md), 진행 계획은 `HANDOFF.md` '중간 점검과 남은 계획'.
> **교체 = 에셋(그림 파일·데이터 칸·프리팹)만 바꾸고 코드는 안 고친다**가 목표다. '코드 수정 필요'라고 적힌 곳만 예외로 남아 있다.

## 바로 바꿀 수 있는 곳 (에셋만)

| 무엇 | 바꾸는 곳 | 같이 볼 값 |
|---|---|---|
| 스테이지 땅·발판·배경·보스 포탈 그림 | `Assets/Data/Stages/StageArt_Forest`(`StageArtSet`) — 지역이 늘면 에셋을 하나 더 | 배경 배율·보여 줄 범위·풀밭 높이, 포탈 배율(같은 에셋) |
| 캐릭터 그림(마법사·메카닉·섬영) | `Assets/Data/Characters/CharacterArtSet` | 캐릭터별 색(`tints`)·화면 높이(`heights`, 판정과 따로). 네 번째 칸(드루이드)은 기획 제외라 비워 둬도 된다(선택 화면에 안 나온다). 지금 캐릭터 그림은 피벗이 가운데가 아니다(`pixel-standard.md` 5절) |
| 잡몹 그림 | `Assets/Data/Enemies/<종류>`(`EnemyData.sprite`) — 지금 대부분 임시 도형. ⚠ 해골 궁수의 화살은 **궁수 그림·종류색을 그대로 물려받는다**(`EnemySetup.ConfigureTypeBehaviour`) — 궁수 그림을 바꾸면 화살도 바뀐다(따로 그리려면 그때 화살 프리팹 칸을 만든다). 에디터 도구 `BuildEnemyData`는 수치만 다시 쓰고 그림 칸(`sprite`·`visualHeight`)은 새로 만들 때만 채운다(바꾼 그림을 덮어쓰지 않는다) | 그림 화면 높이(`visualHeight`, 판정 `colliderRadius`와 따로) |
| 보스·성소 그림 | `Assets/Data/Enemies/MonsterCatalog`(`boss`·`shrine`) | 그림 화면 크기(`bossSize`·`shrineSize`, 판정과 따로) |
| 몹 종류별 구성(행동 부품) | `Assets/Prefabs/Enemies/Enemy_*` | — |
| 투사체·구슬·드론·포탑 그림 | `Assets/Prefabs/Combat/*`의 `Visual` 자식 그림 | 크기는 쏠 때 코드가 넣는다(판정 상자와 같은 크기) — 그림 비율이 달라지면 확인. 보스 탄 색은 `Enemy_Boss`의 `Boss.projectileColor` |
| 타격음 | `00_Persistent` › `Audio`의 `HitSound.clip` | 소리 크기는 설정 창 효과음 값 |
| 섬영 베기 효과 프레임 | `20_Combat` › `Player`의 `BladeSlashVfx.frames`(이름순) | ⚠ 배율 `BladeSlashVfx.Scale` = 2·재생 시간 `Life`·앞쪽 거리 `ForwardOffset`은 코드 상수 — **지금과 같은 크기·PPU(128×128·128)·피벗의 프레임만** 코드 수정 없이 바뀐다. 외주 효과는 이 셋을 다시 맞춘다 |
| 피격·차지·잔상 깜빡임 머티리얼 | `Assets/Materials/SpriteFlash`(몹 프리팹 `Enemy_Base`, Player의 `MageChargeFlash`·`PlayerAfterimages`) | 셰이더 `Assets/Resources/Shaders/SpriteFlash` |

## 라이선스 이펙트(구매·무료 팩) — git 밖, 키로 (2026-11-07)

공개 저장소라 팩 그림은 커밋하지 않는다(`HANDOFF.md` 0-3, 약관 `docs/vfx-credits.md`). **월드 그림은 직렬화 참조**라는 위 원칙의 예외다.

| 무엇 | 바꾸는 곳 | 같이 볼 값 |
|---|---|---|
| 어느 팩의 어느 프레임을 쓰나 | `Assets/Editor/VfxPackImporter`의 목록(코드) → 메뉴 `ChronoSavior/VFX 팩 가져오기`로 다시 푼다 | 키 이름은 `Combat/VfxKeys` 상수(목록과 어긋나면 컴파일 에러) |
| 그림 파일 | `Assets/Resources/Vfx/<키>/frame_000…`(git 밖) | 가져오기 설정은 `VfxImportSettings`(Point·무압축·밉맵 없음·PPU 128 — 배율 1 = 그림 한 픽셀이 격자 한 칸) |
| 재생 | 실행 코드는 `Combat/VfxFrames.Get(키)`(없으면 빈 배열 → 지금 임시 그림)·`Combat/VfxPlayer`(단순 재생기) | 위치·크기·언제는 그 효과의 담당 코드 |

## 코드 수정이 필요한 곳 (실제 효과가 오면 바꾼다)

| 무엇 | 지금 | 바꿀 방법(그때) |
|---|---|---|
| 중력 착탄 폭발·중력장 소용돌이 그림 | `GravityImpactVfx`가 `Resources/Art/Figma/Vfx/GravityImpact`를 **폴더 경로로** 읽는다(소용돌이도 같은 그림, 프레임은 **파일 이름순**·15fps — 번호는 0을 채운 자릿수로). 정적 함수(`MageSkillEffects`)가 그때그때 만들어 그림을 넘길 주인이 없다 | 효과 프리팹으로 만들고, 쏘는 쪽(`MageAttack`)이 들고 넘긴다(투사체와 같은 방식). 크기는 스킬 반경에 맞춰 늘리는 지금 방식을 다시 볼 것(`pixel-standard.md`) |
| 대붕괴 폭발 고리(원판·선) | `GravityBurstRing`이 `Shader.Find("Sprites/Default")`로 머티리얼을 만들고 메시·선을 코드로 그린다 | 외주 효과가 그림/프레임이면 효과 프리팹으로 교체 |
| 레이저 구간·설치물 연결선 | `GunnerLaserBeam`·`GunnerAttack`이 `GunnerAttack.bulletSprite`를 코드로 늘려 그린다 | 외주 효과 형식에 맞춰 프리팹/프레임으로 |
| 처치 연출·화면 흔들림 등 | 코드(`HitFeel`) — 그림 없음 | 필요하면 효과 프리팹 추가 |

## UI (팀원 제작 중 — 따로)

**교체 방식(2026-11-06 Codex 합의)**: 지금처럼 **경로 키 유지** — `UI/Pixel/PxArt.Get("키")`가 `Assets/Resources/Art/Figma/UI/<키>.png`를 읽는 한곳이라,
**같은 키(같은 폴더·파일 이름)로 덮어쓰면 코드 수정 없이 바뀐다.** 새 그림이 필요하면 그 화면에서 키를 추가하고 이 표를 고친다.
화면 배치(Figma 좌표)는 각 화면의 `Build()` 코드에 그대로 둔다 — 그림 교체와 배치 변경은 따로 한다. UI 기준은 월드와 별도(640×360 판·**100 PPU**·Point 필터, `pixel-standard.md` 3절).

- 파일 54개 = 전부 쓰인다(코드에 키를 적은 곳 27개 + 변수로 고르는 곳 7곳). 빠진 파일 0(2026-11-06).
- **제작 상태**(프로젝트에 들어감 / Figma에만 / 제작 중)는 팀원에게 확인해서 아래 표의 '상태' 칸에 채운다 — 지금은 전부 '프로젝트에 있음(임시일 수 있음)'.
- 9-슬라이스로 늘려 쓰는 그림: `Common/button`(테두리 4px)·`Stage/frame_info`(8px) — 바꿀 때 테두리 두께를 같게.
- **색으로 칸을 찾는 그림**(바꿀 때 그 색·자리를 지킬 것): `Hud/status_frame`은 빨간 칸(#EA323C)이 체력, 하늘 칸(#0CF1FF)은 빈칸으로 바꿔 그린다(`CombatHud.BuildStatusFrame`, 칸 자리 x 66~188·y 3~14). `Stage/progress`는 빨간 칸의 범위를 읽어 진행도를 채운다(스테이지 선택·결과 화면).

| 키(폴더/파일) | 쓰는 화면 | 상태 |
|---|---|---|
| `Common/button` · `button_long` · `button_small` · `cursor` · `icon_frame` · `title_plate` · `window_frame` | 거의 모든 화면(공용) | 확인 필요 |
| `Title/background` · `Title/logo` | 타이틀 | 확인 필요 |
| `Hud/portrait` · `slot_frame` · `status_frame` · `boss_bar` · `icon_attack` · `icon_jump` · `icon_skill` · `boss_warning` | 전투 HUD(아이콘은 칸 순서로 고름) · 초상은 캐릭터 선택·각성도 · `boss_warning`은 로비 던전 건물 | 확인 필요 |
| `Icons/close` · `exit` · `flag` · `gear` · `play` · `shard` · `shard_dark` · `skull` · `star` · `weapon_blades` · `weapon_hammer` · `weapon_hat` · `weapon_spanner` | 창 닫기·일시정지 메뉴·캐릭터 선택(무기)·로비 건물·시간 회귀 등(표에서 고름) | 확인 필요 |
| `Awaken/node_open` · `node_learned` · `node_locked` · `skill_window` · `stat_window` | 각성(스킬트리) | 확인 필요 |
| `Skills/mage_explosion_1~5` · `Skills/mage_gravity_1~5` | 각성 스킬 아이콘(`"Skills/mage_explosion_" + 층`) — 메카닉·섬영 아이콘은 아직 없음 | 확인 필요 |
| `Stage/frame_info` · `frame_list` · `boss_frame` · `mob_frame` · `progress` | 스테이지 선택 · `frame_info`는 로비 건물·캐릭터 선택도 · `progress`는 결과 화면도 | 확인 필요 |
| `Pause/band` | 일시정지 | 확인 필요 |
| `Regression/frame` | 시간 회귀 | 확인 필요 |
| `Relic/panel` · `Relic/slot` | 유물 개방(뽑기) | 확인 필요 |
| 글꼴 `Assets/Resources/Fonts/DNFBitBitv2` | 모든 화면(`UI/Pixel/Px.Font`) | 확인 필요 |

## 바꾼 뒤 확인

1. 메뉴 `ChronoSavior/씬 구성 검증` — 빈 칸·빠진 연결을 잡는다.
2. PlayMode 테스트 전체.
3. 캡처 도구(`CaptureStagePreview`·`CaptureUiPreview`)로 `docs/baseline-2026-11-06/`과 나란히 비교.
4. 메뉴 `ChronoSavior/도트 배율 보고서`로 새 그림의 도트 배율을 본다(정수가 아니면 ⚠) — 기준은 `pixel-standard.md`.

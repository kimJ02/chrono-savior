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
| 1지역 보스(덩어리 지배자) 그림 — 지금 기본 도형 | `Assets/Prefabs/Enemies/Enemy_BlobLord`의 몸 그림·`HeldBoulder` 자식, `Prefabs/Combat/BlobLordBoulder`의 `Visual`, `Prefabs/Combat/GroundWarning`(빨간 경고) — 원본 그림 `Assets/Sprites/Pixel/BlobLord*.png` | 화면 크기 `BlobLordBoss.VisualSize`(판정 `HitRadius`와 따로). 경고 그림은 반경 1.8(`GroundWarning.BakedRadius`)에 맞춰 그린 것 — 바꾸면 가로 폭이 판정 지름과 같은지 확인 |
| 몹 종류별 구성(행동 부품) | `Assets/Prefabs/Enemies/Enemy_*` | — |
| 투사체·구슬·드론·포탑 그림 | `Assets/Prefabs/Combat/*`의 `Visual` 자식 그림 | 크기는 쏠 때 코드가 넣는다(판정 상자와 같은 크기) — 그림 비율이 달라지면 확인. 보스 탄 색은 `Enemy_Boss`의 `Boss.projectileColor` |
| 타격음 | `00_Persistent` › `Audio`의 `HitSound.clip` | 소리 크기는 설정 창 효과음 값 |
| 살수(섬영) 베기 효과 | 키 `assassin/slash_1~3`(Slash Vol 2 무속성 3종, 휘두를 때마다 번갈아 — `Editor/VfxPackImporter` 목록) | ⚠ 배율 `BladeSlashVfx.Scale` = 2·재생 시간 `Life`·앞쪽 거리 `ForwardOffset`은 코드 상수. 2026-11-07부터 씬 연결이 아니라 키(라이선스 그림이라 git 밖) |
| 피격·차지·잔상 깜빡임 머티리얼 | `Assets/Materials/SpriteFlash`(몹 프리팹 `Enemy_Base`, Player의 `MageChargeVfx`·`PlayerAfterimages`) | 셰이더 `Assets/Resources/Shaders/SpriteFlash` |

## 라이선스 이펙트(구매·무료 팩) — git 밖, 키로 (2026-11-07)

공개 저장소라 팩 그림은 커밋하지 않는다(`HANDOFF.md` 0-3, 약관 `docs/vfx-credits.md`). **월드 그림은 직렬화 참조**라는 위 원칙의 예외다.

| 무엇 | 바꾸는 곳 | 같이 볼 값 |
|---|---|---|
| 어느 팩의 어느 프레임을 쓰나 | `Assets/Editor/VfxPackImporter`의 목록(코드) → 메뉴 `ChronoSavior/VFX 팩 가져오기`로 다시 푼다 | 키 이름은 `Combat/VfxKeys` 상수(목록과 어긋나면 컴파일 에러) |
| 그림 파일 | `Assets/Resources/Vfx/<키>/frame_000…`(git 밖) | 가져오기 설정은 `VfxImportSettings`(Point·무압축·밉맵 없음·PPU 128 — 배율 1 = 그림 한 픽셀이 격자 한 칸) |
| 재생 | 실행 코드는 `Combat/VfxFrames.Get(키)`(없으면 빈 배열 → 지금 임시 그림)·`Combat/VfxPlayer`(단순 재생기) | 위치·크기·언제는 그 효과의 담당 코드 |
| 마법사 차지 오라 | `Characters/MageChargeVfx`(키 `mage/charge_large`, 몸 뒤, 갈래 색 `FireTint`·`GravityTint`) | 크기 고정(월드 배율 1) |
| 마법사 탄 그림 | `Characters/MageProjectile.ShowArt`(폭발 `mage/fire_projectile`·중력 `mage/gravity_orb_small`, 0차는 프리팹의 원) | 판정 폭에 가장 가까운 정수 배율(`VfxPlayer.WholeScale`), 판정 상자는 그대로 |
| 마법사 불길 | `Combat/FireTrailZone`(키 `mage/fire_trail`) | `FlameScale`·`FadeTime`·밑동 높이(`MageSpecConfig.TeleportFlameLineHeight`) |
| 마법사 폭발(화염·중력 착탄·대붕괴) | `Combat/MageSkillEffects.ShowExplosion`(키 `mage/fire_explosion`·`mage/gravity_explosion`) | 화염·착탄은 판정 지름에 가장 가까운 정수 배율, 대붕괴는 중첩마다 두 칸씩(`CollapseArtScale`). 대붕괴는 `mage/collapse_explosion`(+임계 `mage/collapse_critical`)만 — 원본의 원판·선 고리는 2026-11-09 사용자 결정으로 뺐다 |
| 마법사 중력장 구체 | `Combat/GravityVortexVfx`(키 `mage/gravity_orb_large`) | 배율 = 중첩 수(1~5) — 끌어당기는 판정 반경과 따로 |
| 메카닉 레이저 | 그림 `Characters/GunnerBeamVfx`(키 `mechanic/laser_b_{origin·center·impact}_{start·loop·end}`, 팩 B형 small), 몸통 머티리얼 `Assets/Materials/LaserBeam`(셰이더 `Assets/Shaders/LaserBeamSlice` — Player `GunnerAttack.beamMaterial` 칸), 끝 빛 알갱이 Particle System 프리팹 `Assets/Prefabs/Combat/LaserTipSparks`(`laserTipPrefab` 칸) | 2026-11-10: 몸통은 텍스처를 줄이지 않고 위아래 3줄 1:1·가운데만 잘라 층별 10/10/12/14/16줄(`GunnerLaserBeam.ArtBodyRows`) + 바깥 반투명 빛 2줄, 끝 0.75유닛 흐림. 총구 그림은 켜질 때 0.1초 섬광, 그 뒤 총구 원형 빛. 목표 끝 그림은 닿을 때 0.15초. 외주 빔으로 바꿀 땐 텍스처(띠가 세로 가운데, 프레임마다 단독 텍스처)와 머티리얼 줄 수만. 그림 없으면 하늘색 선 |
| 메카닉 드론 | `Characters/GunnerDrone`(프리팹 `Prefabs/Combat/GunnerDrone` — 추진 배기 Particle System `Exhaust` 자식), 탄 `GunnerBullet.Spawn(artKey: mechanic/pellet, withTrail, hitArtKey: mechanic/drone_hit)`(꼬리 = 탄 프리팹 `Trail` TrailRenderer), 발사 섬광 키 `mechanic/drone_muzzle` | 2026-11-10 D-B '발사 강조형'(사용자): 몸 둘레 원·호 없음, 끝나기 1초 전 깜빡. 판정 상자와 그림은 따로 |
| 메카닉 중계기 머리 | `Characters/GunnerTurret.AttachRelayHead`(키 `mechanic/relay_head`, 머리 둘레 전기 `mechanic/relay_aura`) | 높이 `RelayHeadHeight`. 2026-11-10 I-B '전기 아크형'(사용자): 늘 깔린 범위 원·링크 사거리 점선 원 없음 — 맞는 순간 적 자리 전기(`Characters/GunnerZap`, 키 `mechanic/zap`, 적마다 0.25초에 한 번) + 때린 곳(연결선·도형 변·중계기 머리)에서 적까지 짧은 전기(`Characters/GunnerZapArc`, LineRenderer — 그림 없이) |
| 메카닉 중계선·내부 장판 | `Characters/GunnerFieldVfx`(Player에 놓임, 머티리얼 칸 `Assets/Materials/VfxUnlit`) | 2026-11-10 I-B: 중계선 = 링크마다 LineRenderer 셋(판정 경로 그대로 1px 기준선 + 흔들리는 주 아크 2px + 보조 아크 1px, 15fps로 모양 바뀜, 양 끝 고정), 판정 폭 띠 없음. 내부 = 옅은 푸른 메시 + 흰 점 6개 |
| 살수 집중·궤적·칼날폭풍 | `Characters/BladeVfx`(Player에 놓임 — 칸: 선 머티리얼 `VfxUnlit`, 잔상 머티리얼 `SpriteFlash`, 착지 파편 프리팹 `Prefabs/Combat/BladeLandDebris`) | 2026-11-10 사용자 확정 — 집중 'B 잔상 복제'(남색 배경 어둡게·켤 때 청록 하트비트 `assassin/heartbeat`·파란 잔상 3·속도선·옅은 궤적 점·파열 흰 잔상 + `assassin/rip_hit`), 칼날폭풍 '잔상 연쇄 파열 + 착지 폭발'(회전베기 `assassin/spin` 틱마다 좌우 반전·`assassin/crescent`·잔상 사슬 → 끝나고 0.35초 뒤 `assassin/bolt_1·2`+`assassin/burst_spark`로 터짐·착지 `assassin/land_ring`·`land_burst`·`land_dust` + 바닥 충격파(착지 판정 반경) + 파편). 그림은 `BladeCombat`의 알림 이벤트(`TrailRipHit`·`StormSpun`·`StormLanded`·`StormEnded`)만 듣는다 |

## 코드 수정이 필요한 곳 (실제 효과가 오면 바꾼다)

| 무엇 | 지금 | 바꿀 방법(그때) |
|---|---|---|
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

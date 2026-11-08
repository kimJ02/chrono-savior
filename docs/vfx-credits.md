# 이펙트 팩 — 출처·약관·크레딧 (2026-11-07)

> 이펙트 그림은 **git에 넣지 않는다**(저장소가 공개 — 팩 내용물 공개 재배포 금지). 무엇을 쓰는지는 `Assets/Editor/VfxPackImporter`의 목록이 원본이고,
> 각자 아래 팩을 받아 메뉴 `ChronoSavior/VFX 팩 가져오기`로 `Assets/Resources/Vfx/`(git 밖)에 푼다. 배경·결정은 `HANDOFF.md` 0절.

## 팩

| 팩 | 받는 곳 | 약관 요약 | 크레딧 |
|---|---|---|---|
| Super Pixel Effects Gigapack (Free Version) v3.2.0 | https://untiedgames.itch.io/super-pixel-effects-gigapack | 게임에 넣어 배포 OK(상업 포함). 팩 내용물을 공개적으로 재배포·에셋 스토어 재업로드 금지. 원문 http://untiedgames.com/files/license.txt | **필수** |
| Super Pixel Projectiles Pack 4 | https://untiedgames.itch.io/super-pixel-projectiles-pack-4 | 위와 같음 | **필수** |
| VFX Slash Vol 2 (+ update_1) | https://kiddolink.itch.io/vfx-slash-vol-2-pixel-art-effects | 상업 사용·수정 OK, 재판매·재배포 금지(zip엔 약관 파일 없음 — 페이지 기준) | 권장 |
| Pixel Fire Asset Pack v1.2 | https://devkidd.itch.io/pixel-fire-asset-pack | 개인·상업 무료, 그대로·수정해서 판매 금지 | 선택(넣는다) |

## 게임 안 크레딧 문구

```
Pixel Art Effects
  Super Pixel Effects Gigapack, Super Pixel Projectiles Pack 4 — Will Tice / unTied Games
  VFX Slash Vol 2 — kiddolink
  Pixel Fire Asset Pack — devkidd
```

> **예전에 공개 저장소에 들어가 있던 라이선스 그림**(2026-10-25 커밋 — 기가팩 `stylized_explosion_002_large_violet` 10장 → `Resources/Art/Figma/Vfx/GravityImpact`, Frostwindz 'Pixel Art Animations - Slashes' 9장 → `.../BladeSlash`)은 2026-11-07에 저장소에서 뺐다(사용자 결정 — 기록은 옛 커밋에 남는다. 완전히 지우려면 저장소를 비공개로). 중력 착탄은 같은 그림을 키로, 베기는 Slash Vol 2로 바꿨다 — Frostwindz 팩은 더 쓰지 않는다.

## 쓰는 효과

가져오기 도구의 목록이 채워질 때 여기 같이 적는다(키 → 팩 · 원본 폴더).

| 키 | 팩 | 원본 |
|---|---|---|
| `mage/charge_large·charge_small` | Gigapack | Sci-fi/scifi_charge_up_001 (large·small, yellow) |
| `mage/charge_large_violet` | Gigapack | 위 large_yellow의 색상을 +225° 돌린 파생 그림(가져오기 도구가 만든다) |
| `mage/fire_projectile` | Projectiles Pack 4 | pj4_energy_blast_A_small_red (문서 예시의 혜성 모양 빨강, 2026-11-08) |
| `mage/fire_trail·fire_trail_small` | Pixel Fire | fire asset red / Group 4 - 1·Group 4 - 3 |
| `mage/gravity_orb_small·large` | Projectiles Pack 4 | pj4_dark_matter (small·large, violet) |
| `mage/fire_explosion` | Gigapack | Explosions/stylized_explosion_001_large_yellow (폭발 17종을 게임 배율로 비교해 고름, 2026-11-08) |
| `mage/gravity_explosion` | Gigapack | Explosions/stylized_explosion_002_large_violet |
| `mage/collapse_explosion` | Gigapack | Impacts/symmetrical_impact_002_large_blue → 보라 파생(+75°) |
| `mage/collapse_critical` | Gigapack | Impacts/symmetrical_impact_006_large_yellow → 보라 파생(+225°) |
| `mechanic/laser_*` | Projectiles Pack 4 | pj4_laser_beam_A (small·large, blue) origin·center·impact × start·loop·end |
| `mechanic/pellet` | Projectiles Pack 4 | pj4_energy_pellet_small_yellow |
| `mechanic/relay_head` | Projectiles Pack 4 | pj4_plasma_ball_small_blue (설치기 몸체 위 — 몸체 안테나 그림은 팩에 없어 지금 몸체) |
| `assassin/slash_1~3` | Slash Vol 2 | spritesheet_96x96 1·3·5줄(무속성) |
| `assassin/spin` | Slash Vol 2 update_1 | spritesheet_96x96 17줄(회전베기 7장 — 불투명 갈색 바탕을 가져오기 도구가 뺀다) |
| `assassin/heartbeat` | Gigapack | Sci-fi/scifi_heartbeat_fast_001_large_yellow |
| `assassin/splatter` | Gigapack | Splatters/directional_splatter_001_small_red |
| `assassin/smoke` | Gigapack | Smoke Bursts/looping_smoke_burst_002_small_gray |

목록은 효과를 입히며 바뀐다 — 바뀌면 이 표도 같이(원본은 `Editor/VfxPackImporter`).

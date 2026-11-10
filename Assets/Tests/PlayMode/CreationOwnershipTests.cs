using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **실행 중에 오브젝트를 만들거나 부품을 붙이는 곳은 정해진 담당뿐이다**(`HANDOFF.md` 0단계 규칙 3·4, 정리 순서 2 '생성 책임', 2026-11-05).
/// 기본은 **씬 배치**(씬이 원본)와 **프리팹**(재사용되는 구성)이다. 코드로 짓는 건 아래 목록의 담당만, 이유를 적어서 —
/// 새 파일이 `AddComponent`·`new GameObject`·`Instantiate`를 쓰면 이 목록에 담당과 이유를 먼저 적어야 통과한다.
/// 이 목록이 생성 책임의 **유일한 원본**이다(문서에 따로 옮겨 적지 않는다 — `HANDOFF.md`는 분류와 정책만).
///
/// 대상은 `Assets/Scripts`(실행 코드)뿐 — 에디터 도구·테스트의 생성은 정책 밖이다. 주석·문자열은 세지 않는다(`SourceScan`)(`WorldLookupTests`와 같은 방식).
/// 개수는 세지 않는다(호출이 하나 늘 때마다 깨지는 검사는 오래 못 간다) — 새 **담당**이 생기는 것을 막는다.
/// </summary>
public class CreationOwnershipTests
{
    /// <summary>분류 — `HANDOFF.md` "2단계 생성 책임"의 A~F와 같다.</summary>
    enum Kind
    {
        /// <summary>A. 실행 중 생기는 전투 물체 — 프리팹을 세운다(투사체 3종·구슬·드론·포탑, 프리팹은 만드는 쪽이 넘긴다). 장판 둘만 코드(이유는 각 줄).</summary>
        CombatObject,
        /// <summary>B. 몹 세우기 — 종류별 프리팹을 Instantiate(2-2에서 정리: 행동 부품은 프리팹에, 엘리트 표식만 실행 중).</summary>
        EnemyAssembly,
        /// <summary>D. 그림만 있는 일회성 효과·효과 풀(판정 없음). 정당한 코드 생성.</summary>
        VisualEffect,
        /// <summary>E. UI를 코드로 짓기(Figma 좌표 그대로 — 화면마다 프리팹을 두면 좌표가 두 곳이 된다). 정당한 코드 생성.</summary>
        UiBuild,
        /// <summary>F. 크기 입력으로 그림을 깔기(땅 타일·발판·배경). 정당한 코드 생성.</summary>
        Procedural,
        /// <summary>표식 붙이기 헬퍼 — 생성 담당이 부르는 공용 함수.</summary>
        Helper,
    }

    /// <summary>`Assets/Scripts/` 기준 경로 → (분류, 담당·이유).</summary>
    static readonly Dictionary<string, (Kind kind, string why)> Owners = new Dictionary<string, (Kind, string)>
    {
        // A. 전투 물체 — 각 클래스의 정적 생성 함수가 본체·그림 자식·판정·RunTransient를 짓는다
        ["Characters/GunnerBullet.cs"]     = (Kind.CombatObject, "메카닉 탄 — 프리팹(Prefabs/Combat/GunnerBullet)을 세운다. 프리팹은 쏘는 쪽(GunnerAttack·드론)이 넘긴다. 드론 탄 그림(라이선스 프레임)이 있으면 그림 자식에 재생기(VfxPlayer)를 붙인다"),
        ["Characters/MageProjectile.cs"]   = (Kind.CombatObject, "마법사 마탄 — 프리팹(Prefabs/Combat/MageBolt)을 세운다. 프리팹은 MageAttack이 넘긴다. 갈래별 탄 그림(라이선스 프레임)이 있으면 그림 자식에 재생기(VfxPlayer)를 붙인다"),
        ["Enemies/EnemyBolt.cs"]           = (Kind.CombatObject, "적 탄 — 프리팹(Prefabs/Combat/EnemyBolt)을 세운다. 프리팹은 쏘는 몹(해골 궁수·보스 프리팹)이 넘긴다"),
        ["Enemies/BoulderProjectile.cs"]   = (Kind.CombatObject, "덩어리 지배자 조약돌 — 프리팹(Prefabs/Combat/BlobLordBoulder)을 세운다. 프리팹은 보스 프리팹(Enemy_BlobLord)이 넘긴다(2026-11-10)"),
        ["Enemies/BlobLordBoss.cs"]        = (Kind.VisualEffect, "바닥 빨간 경고 2개(점프·투척) — 프리팹(Prefabs/Combat/GroundWarning)을 보스가 살아 있는 동안 하나씩 세워 재사용, 그림만(판정은 보스가)(2026-11-10)"),
        ["Systems/ExpOrb.cs"]              = (Kind.CombatObject, "경험치 구슬 — 프리팹(Prefabs/Combat/ExpOrb)을 세운다. 프리팹은 떨구는 스포너가 넘긴다"),
        ["Characters/GunnerDrone.cs"]      = (Kind.CombatObject, "메카닉 드론 — 프리팹(Prefabs/Combat/GunnerDrone)을 세운다. 프리팹은 GunnerAttack이 넘긴다. 문서 드론 그림(라이선스 프레임 재생기)·빛 원·돌아가는 호 자식(2026-11-09)"),
        ["Characters/GunnerTurret.cs"]     = (Kind.CombatObject, "메카닉 설치 포탑 — 프리팹(Prefabs/Combat/GunnerTurret)을 세운다. 프리팹은 GunnerAttack이 넘긴다. 중계기 머리 그림(라이선스 프레임 재생기 자식)·공격 범위 타원·링크 사거리 점선 원을 붙인다"),
        ["Combat/FireTrailZone.cs"]        = (Kind.CombatObject, "화염 장판 — 코드로 짓는다: 구성이 루트+이 컴포넌트(+RunTransient)뿐이고 에디터에서 조정할 판정 부품이 없다(값은 MageSpecConfig). 그림은 PathFlame이 따로(2026-11-09). 마법사 스킬이 깐다"),
        ["Combat/GravityWellZone.cs"]      = (Kind.CombatObject, "중력장 — 코드로 짓는다: 구성이 루트+이 컴포넌트(+RunTransient)뿐, 그림은 소용돌이 효과(D)가 따로. 값은 MageSpecConfig. 마법사 스킬이 연다"),

        // B. 몹 조립
        ["Systems/EnemySpawner.cs"]        = (Kind.EnemyAssembly, "몹·성소·보스를 종류별 프리팹(EnemyData.prefab·MonsterCatalog)에서 세운다"),
        ["Systems/EnemySetup.cs"]          = (Kind.EnemyAssembly, "세운 몹을 데이터로 완성한다 — 실행 중 붙이는 건 스폰 때 확률로 정해지는 엘리트 표식(EnemyElite)뿐(2026-11-06 5-1, 스포너에서 떼어 냄)"),

        // C. 스스로 부품 붙이기 — 2-1에서 전부 씬 배치로 옮겼다(MageChargeFlash(지금 MageChargeVfx)·HitSound 소리 장치·로비 CameraAnchor, 안 쓰던 PlayerAttack 삭제). 새로 생기면 안 된다.

        // D. 그림만 있는 효과
        ["Characters/BladeSlashVfx.cs"]    = (Kind.VisualEffect, "섬영 베기 그림 풀"),
        ["Characters/PlayerAfterimages.cs"] = (Kind.VisualEffect, "잔상 그림 풀"),
        ["Characters/MageChargeVfx.cs"]    = (Kind.VisualEffect, "차지 오라(키로 불러온 프레임 재생기)·완충 깜빡임 그림 자식"),
        ["Characters/GunnerBeamVfx.cs"]    = (Kind.VisualEffect, "레이저 빔 그림(선 하나·총구·끝 그림) — 그림만, 판정은 GunnerLaserBeam(2026-11-09 떼어 냄)"),
        ["Characters/GunnerZapArc.cs"]     = (Kind.VisualEffect, "설치기 적중 분기 아크(LineRenderer 둘) — 그림만, GunnerZap이 풀로 돌려 쓴다(2026-11-10)"),
        ["Characters/GunnerFieldVfx.cs"]   = (Kind.VisualEffect, "설치기 링크선(레이저 그림 타일)·내부 장판 칠하기(메시)·흰 점 — 그림만, 2026-11-08 GunnerAttack에서 떼어 냄"),
        ["Characters/BladeVfx.cs"]         = (Kind.VisualEffect, "살수 집중 B(배경 어둡게·하트비트·잔상·속도선·궤적 점·파열 흰 잔상)·칼날폭풍(회전베기·잔상 사슬·착지 충격파·파편 프리팹) — 그림만(2026-11-08, 2026-11-10 다시)"),
        ["Enemies/EnemyHealth.cs"]         = (Kind.VisualEffect, "피격 깜빡임 그림 자식·화상 불꽃(라이선스 프레임 재생기 자식)·불씨 점(2026-11-09)"),
        ["Combat/GravityVortexVfx.cs"]     = (Kind.VisualEffect, "중력장 암흑 구체 그림(키로 불러온 프레임 재생기 자식)·끌어당김 점·중첩 반짝임"),
        ["Combat/RangeRing.cs"]            = (Kind.VisualEffect, "광역 공격 판정 범위 고리(실행 중에 그린 도트 원) — 그림만(2026-11-09 도트 통일)"),
        ["Enemies/EnemyElite.cs"]          = (Kind.VisualEffect, "엘리트 오라·왕관(원본 drawEnemy :4780)·보라 불씨 — 그림만(2026-11-09)"),
        ["Combat/VfxDots.cs"]              = (Kind.VisualEffect, "그림 없는 점 입자(화상 불씨·블랙홀 끌어당김·중첩 반짝임·돌진 불씨) — 그림만(2026-11-09)"),
        ["Combat/PathFlame.cs"]            = (Kind.VisualEffect, "화염돌진이 지나간 바닥 불꽃 하나(점화·사그라듦) — 그림만, 판정은 FireTrailZone(2026-11-09)"),
        ["Combat/FireDashVfx.cs"]          = (Kind.VisualEffect, "화염돌진 그림 담당(출발 퍼프·분사·바닥 불꽃 점화·도착 불꽃) — 그림만(2026-11-09)"),
        ["Combat/VfxPlayer.cs"]            = (Kind.VisualEffect, "라이선스 이펙트(키로 불러온 프레임)를 한 번·반복 재생하는 단순 재생기 — 그림만, 판정 없음(2026-11-07 VFX 스프린트)"),

        // E. UI 짓기
        ["UI/Pixel/Px.cs"]                 = (Kind.UiBuild, "UI 노드 공용 생성(Image·Text·외곽선·버튼) — 모든 화면이 여기로 짓는다"),
        ["UI/Pixel/PxButton.cs"]           = (Kind.UiBuild, "버튼 눌림 표현용 CanvasGroup"),
        ["UI/Pixel/PixelCanvas.cs"]        = (Kind.UiBuild, "Create는 씬 없는 테스트·캡처 전용(게임은 00_Persistent에 판이 놓임), 화면 컴포넌트 목록 붙이기, 테스트용 EventSystem"),
        ["UI/Screens/AchievementsScreen.cs"] = (Kind.UiBuild, "업적 칸 버튼"),
        ["UI/Screens/AwakenScreen.cs"]     = (Kind.UiBuild, "스킬트리 스크롤 가림막"),
        ["UI/Screens/CharacterSelectScreen.cs"] = (Kind.UiBuild, "캐릭터 목록 가림막"),
        ["UI/Screens/CombatHud.cs"]        = (Kind.UiBuild, "미니맵 가림막·글자 그라데이션"),
        ["UI/Screens/StageSelectScreen.cs"] = (Kind.UiBuild, "스테이지 칸 버튼"),
        ["UI/Screens/TitleMenu.cs"]        = (Kind.UiBuild, "타이틀 메뉴 버튼"),
        ["UI/Lobby/LobbyWorld.cs"]         = (Kind.UiBuild, "로비 건물(월드 캔버스, 건물 표 Buildings에서 짓는다 — 건물 그림이 생기면 씬/프리팹으로). 카메라 기준점은 씬에 놓인다"),

        // F. 크기 입력으로 그림 깔기
        ["World/StageVisuals.cs"]          = (Kind.Procedural, "Ground·발판 판정 크기에서 땅 타일·발판 그림·배경을 깐다(그림 크기 = 판정 크기)"),

        // 헬퍼
        ["Core/RunTransient.cs"]           = (Kind.Helper, "RunTransient.Mark — 생성 담당이 런 오브젝트 표식을 붙일 때"),
    };

    static readonly Regex Creation = new Regex(@"\bAddComponent\s*[<(]|\bnew\s+GameObject\s*\(|\bGameObject\s+\w+\s*=\s*new\s*\(|\bInstantiate\s*[<(]");

    static bool Creates(string source) => Creation.IsMatch(SourceScan.CodeOnly(source)); // 주석·문자열을 뺀 코드 전체(여러 줄에 나눈 호출도)

    [Test]
    public void OnlyListedOwners_CreateObjectsOrAttachComponents()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        var creators = new HashSet<string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(root.Length + 1).Replace('\\', '/');
            if (Creates(File.ReadAllText(file))) creators.Add(rel);
        }
        var unlisted = creators.Where(f => !Owners.ContainsKey(f)).Select(f => $"{f}: 목록에 없는 생성 담당 — 씬 배치·프리팹으로 하거나, 이 목록에 분류와 이유를 적을 것");
        var stale = Owners.Keys.Where(f => !creators.Contains(f)).Select(f => $"{f}: 이제 아무것도 만들지 않는다 — 목록에서 뺄 것");
        CollectionAssert.IsEmpty(unlisted.Concat(stale).ToList());
    }

    [Test]
    public void EveryOwner_HasAReason()
    {
        CollectionAssert.IsEmpty(Owners.Where(o => string.IsNullOrWhiteSpace(o.Value.why)).Select(o => o.Key).ToList());
    }
}

}

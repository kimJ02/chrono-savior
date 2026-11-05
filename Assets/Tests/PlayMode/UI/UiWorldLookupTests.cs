using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **화면은 월드를 찾지 않는다**(`HANDOFF.md` "연결 규칙"). 화면(상주 씬 `00_Persistent`)이 씬을 뒤져 월드 오브젝트(전투 씬)를 찾으면,
/// 로비가 전투 씬과 떨어지는 순간(씬 분할 2차 3단계) 못 찾는다. 소스를 직접 훑어 막는다 — `OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`와 같은 방식.
/// 지금은 옮기기를 끝낸 화면만 검사한다. 씬 분할 2차 4단계에서 `UI/` 전체로 넓힌다.
/// </summary>
public class UiWorldLookupTests
{
    /// <summary>월드 탐색을 걷어 낸 파일(`Assets/Scripts/` 기준 경로). 씬 분할 2차 2b 끝에 런타임 전체로 넓힌다.</summary>
    static readonly string[] Migrated =
    {
        "UI/Screens/StageSelectScreen.cs",     // 2a — 몬스터 그림은 MonsterCatalog 에셋에서
        "UI/Screens/CharacterSelectScreen.cs", // 2a — 고르기는 프로필만(PlayerRig는 그 값을 따라간다)
        "UI/Screens/LobbyWorld.cs",            // 2b — Player는 같은 씬 직렬화 참조
        "World/StageVisuals.cs",               // 2b — 땅·발판·포털은 직렬화, 카메라는 CameraFollow2D.CurrentCamera
        "World/BossPortal.cs",                 // 2b — Player는 직렬화
        "Enemies/EnemyMove.cs",                // 2b — 쫓을 대상은 스포너가 넣는다
        "Systems/ExpOrb.cs",                   // 2b — 끌려갈 플레이어는 스포너가 넣는다
        "UI/Screens/CombatHud.cs",             // 2b — WorldView.Combat만 읽는다
        "UI/Screens/LobbyHud.cs",              // 2b — WorldView.Lobby만 읽는다, 카메라는 같은 씬 직렬화
    };

    static readonly Regex WorldLookup = new Regex(@"\bCamera\.main\b|\b(FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindObjectOfType|FindObjectsOfType|FindGameObjectWithTag|FindGameObjectsWithTag|FindWithTag)\b|GameObject\.Find\s*\(");

    [Test]
    public void MigratedScreens_DoNotSearchTheWorld()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        var offenders = Migrated
            .Where(rel => WorldLookup.IsMatch(File.ReadAllText(Path.Combine(root, rel))))
            .ToArray();
        CollectionAssert.IsEmpty(offenders, "이 화면은 월드를 찾으면 안 된다 — 에셋·프로필·WorldView로 읽을 것");
    }
}

}

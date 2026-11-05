using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **실행 코드는 씬을 뒤져 오브젝트를 찾지 않는다**(`HANDOFF.md` "연결 규칙", 2026-11-05 씬 분할 2차 2b). 같은 씬은 직렬화 참조, 실행 중 생기는 것은 만드는 쪽이 넘기고,
/// 화면은 `Systems/WorldView`로만 월드를 읽는다. 찾기가 남아 있으면 씬이 나뉠 때(로비 씬 분리 등) 조용히 못 찾는다 — 소스를 직접 훑어 막는다
/// (`OriginalFidelityTests.DefinedApis_AreActuallyCalledSomewhere`와 같은 방식). 에디터 도구(`Assets/Editor`)는 대상이 아니다.
/// </summary>
public class WorldLookupTests
{
    /// <summary>이유를 적어 둔 예외 — `Assets/Scripts/` 기준 경로 → 허용하는 찾기 수와 이유.</summary>
    static readonly Dictionary<string, (int count, string why)> Allowed = new Dictionary<string, (int, string)>
    {
        ["UI/Pixel/PixelCanvas.cs"] = (1, "EnsureEventSystem — 씬 없이 판을 만드는 테스트·캡처에서만 EventSystem이 이미 있는지 본다(게임 씬엔 놓여 있다)"),
    };

    static readonly Regex WorldLookup = new Regex(@"\bCamera\.main\b|\b(FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindObjectOfType|FindObjectsOfType|FindGameObjectWithTag|FindGameObjectsWithTag|FindWithTag)\b|GameObject\.Find\s*\(");

    /// <summary>
    /// **줄 전체가 주석인 줄**(`//`·`///`로 시작)만 세지 않는다 — 예전 방식을 설명하는 글이 걸리지 않게. 코드 뒤에 붙은 주석과 블록 주석(`/* */`)은 센다
    /// (그런 자리에는 옛 API 이름을 쓰지 말 것 — 단순하게 두려고 C# 구문 분석은 안 한다).
    /// </summary>
    static int CountLookups(string source) =>
        source.Split('\n').Where(l => !l.TrimStart().StartsWith("//")).Sum(l => WorldLookup.Matches(l).Count);

    [Test]
    public void RuntimeCode_DoesNotSearchTheScene()
    {
        string root = Path.Combine(Application.dataPath, "Scripts");
        var problems = new List<string>();
        foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            string rel = file.Substring(root.Length + 1).Replace('\\', '/');
            int n = CountLookups(File.ReadAllText(file));
            int allowed = Allowed.TryGetValue(rel, out var a) ? a.count : 0;
            if (n != allowed) problems.Add($"{rel}: 씬 찾기 {n}곳(허용 {allowed}) — 같은 씬은 직렬화, 생성물은 만드는 쪽이 넘기고, 화면은 WorldView로");
        }
        CollectionAssert.IsEmpty(problems);
    }
}

}

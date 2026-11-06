using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 라이선스 이펙트 그림의 바탕(2026-11-07 VFX 스프린트 2단계, `HANDOFF.md` 0-3) — 키 규칙, 가져오기 목록이 모든 키를 덮는지,
/// 그림이 **없어도** 안전한지(빈 배열·null), 그림이 **있으면** 가져오기 설정이 도트용인지, 단순 재생기가 끝나면 사라지는지.
/// 그림을 보는 검사는 팩을 안 가져온 클론에선 '미설치로 생략'(Inconclusive)이다.
/// </summary>
public class VfxTests
{
    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && (go.name.StartsWith("Test") || go.name.StartsWith("Vfx_"))) Object.Destroy(go);
        RunTransient.Reset();
    }

    [Test]
    public void Keys_FollowTheNamingRule_AndAreUnique()
    {
        var keys = VfxKeys.All;
        var rule = new Regex(@"^(mage|mechanic|assassin)/[a-z0-9]+(_[a-z0-9]+)*$");
        foreach (var k in keys) Assert.IsTrue(rule.IsMatch(k), $"키 규칙(<캐릭터>/<효과>_…, 소문자 snake_case)에 안 맞는다: {k}");
        Assert.AreEqual(keys.Length, keys.Distinct().Count(), "같은 키가 두 번 있다");
    }

#if UNITY_EDITOR
    /// <summary>가져오기 도구의 목록이 모든 키를 **한 번씩** 덮는다(어느 팩에서 오는지 모르는 키가 없게).</summary>
    [Test]
    public void ImporterManifest_CoversEveryKeyOnce()
    {
        var type = System.Type.GetType("ChronoSavior.Editor.VfxPackImporter, Assembly-CSharp-Editor");
        Assert.IsNotNull(type, "가져오기 도구(Editor/VfxPackImporter)가 없다");
        var manifest = ((System.Collections.Generic.IEnumerable<string>)type.GetProperty("ManifestKeys").GetValue(null)).ToArray();
        CollectionAssert.AreEquivalent(VfxKeys.All, manifest, "목록과 키가 다르다");
        Assert.AreEqual(manifest.Length, manifest.Distinct().Count(), "목록에 같은 키가 두 번 있다");
    }

    /// <summary>그림이 들어와 있으면 키마다 프레임이 있고, 가져오기 설정이 도트용(Point·밉맵 없음·무압축·PPU 128)이다.</summary>
    [Test]
    public void InstalledFrames_AreImportedForPixelArt()
    {
        Assume.That(VfxKeys.All.Any(VfxFrames.Has), "이펙트 팩을 안 가져온 클론 — 미설치로 생략(메뉴 ChronoSavior/VFX 팩 가져오기)");
        foreach (var key in VfxKeys.All)
        {
            var frames = VfxFrames.Get(key);
            Assert.Greater(frames.Length, 0, $"{key}: 프레임이 없다 — 팩을 다시 가져올 것");
            for (int i = 0; i < frames.Length; i++)
                Assert.AreEqual($"frame_{i:000}", frames[i].name, $"{key}: 프레임 이름·순서");
            var path = UnityEditor.AssetDatabase.GetAssetPath(frames[0]);
            var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(path);
            Assert.AreEqual(FilterMode.Point, importer.filterMode, $"{key}: 필터");
            Assert.IsFalse(importer.mipmapEnabled, $"{key}: 밉맵");
            Assert.AreEqual(UnityEditor.TextureImporterCompression.Uncompressed, importer.textureCompression, $"{key}: 압축");
            Assert.AreEqual(128f, importer.spritePixelsPerUnit, $"{key}: PPU(월드와 같은 128)");
        }
    }
#endif

    [Test]
    public void MissingKey_GivesNoFrames_AndSpawnsNothing()
    {
        var frames = VfxFrames.Get("mage/no_such_effect");
        Assert.IsNotNull(frames);
        Assert.AreEqual(0, frames.Length);
        Assert.IsNull(VfxPlayer.Spawn("mage/no_such_effect", Vector3.zero, 1f, 12f, 6), "그림이 없으면 세우지 않는다(부르는 쪽이 임시 그림)");
    }

    static Sprite[] SyntheticFrames(int n)
    {
        var tex = new Texture2D(4, 4);
        return Enumerable.Range(0, n).Select(i => Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 128f)).ToArray();
    }

    /// <summary>한 번 재생은 프레임을 넘기다 끝나면 사라지고, 끝 무렵엔 투명해진다(fadeOut). 반복 재생은 계속 돈다.</summary>
    [UnityTest]
    public IEnumerator Player_PlaysOnceThenDisappears_OrLoops()
    {
        var once = new GameObject("TestVfxOnce", typeof(SpriteRenderer)).AddComponent<VfxPlayer>();
        once.Play(SyntheticFrames(4), 20f, false, 0.1f, Color.white); // 0.2초
        var looping = new GameObject("TestVfxLoop", typeof(SpriteRenderer)).AddComponent<VfxPlayer>();
        looping.Play(SyntheticFrames(3), 20f, true, 0f, Color.white);

        float until = Time.time + 1f;
        float minAlpha = 1f;
        while (once != null && Time.time < until)
        {
            minAlpha = Mathf.Min(minAlpha, once.GetComponent<SpriteRenderer>().color.a);
            yield return null;
        }
        Assert.IsTrue(once == null, "한 번 재생이 끝났는데 남아 있다");
        Assert.Less(minAlpha, 1f, "끝 무렵에 투명해지지 않았다");
        Assert.IsTrue(looping != null && !looping.Finished, "반복 재생은 계속 돈다");
    }
}

}

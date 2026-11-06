using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>적 타격 소리(2026-10-25) — 직접 타격 이벤트마다 한 번, 너무 붙으면 건너뛴다, 효과음 0이면 안 난다.</summary>
public class HitSoundTests
{
    int savedSfx;

    [SetUp]
    public void Setup() { savedSfx = GameSettings.SfxVolume; GameSettings.SfxVolume = 8; CombatEvents.Reset(); }

    [TearDown]
    public void Teardown()
    {
        GameSettings.SfxVolume = savedSfx;
        CombatEvents.Reset();
        GameState.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name == "TestHitSound") Object.DestroyImmediate(go);
    }

    /// <summary>
    /// 게임에선 00_Persistent의 Audio에 소리 장치·클립이 놓여 있다(씬 구성 검증이 클립이 연결됐는지 본다). 테스트는 **어느 소리인지와 상관없이**
    /// 짧은 소리를 만들어 넣는다 — 소리 파일은 외주·구매로 갈아끼울 예정이라 특정 파일에 기대지 않는다(2026-11-06 3.5-c). 소리 장치는 RequireComponent가 붙인다.
    /// </summary>
    static HitSound NewHitSound()
    {
        var hs = new GameObject("TestHitSound").AddComponent<HitSound>();
        hs.Clip = AudioClip.Create("TestHit", 441, 1, 44100, false);
        return hs;
    }

    [UnityTest]
    public IEnumerator EnemyDamaged_PlaysOnce_AndCloseHitsAreSkipped()
    {
        var hs = NewHitSound();
        yield return null;
        CombatEvents.RaiseEnemyDamaged(null);
        CombatEvents.RaiseEnemyDamaged(null); // 같은 프레임 — 건너뜀
        Assert.AreEqual(1, hs.Played);
        yield return new WaitForSecondsRealtime(HitSound.MinGap + 0.05f);
        CombatEvents.RaiseEnemyDamaged(null);
        Assert.AreEqual(2, hs.Played);
    }

    [UnityTest]
    public IEnumerator WeaponTick_LikeLaser_Plays_BurnTick_DoesNot()
    {
        var hs = NewHitSound();
        yield return null;
        CombatEvents.RaiseEnemyTickDamaged(null);
        Assert.AreEqual(1, hs.Played, "레이저 같은 무기 틱에도 소리가 난다");

        // 실제 몹 — 화상 틱은 EnemyTickDamaged를 안 부른다
        int ticks = 0;
        CombatEvents.EnemyTickDamaged += _ => ticks++;
        var enemy = new GameObject("TestHitSoundEnemy");
        enemy.AddComponent<CircleCollider2D>();
        var health = enemy.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
        health.TakeTickDamage(1f);
        Assert.AreEqual(1, ticks, "무기 틱 피해는 알린다");
        GameState.Set(GameScene.Run); // 화상은 사냥 중에만 탄다
        health.ApplyBurn(3);
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(1, ticks, "화상 틱은 소리용 알림을 안 보낸다");
        Object.DestroyImmediate(enemy);
    }

    [UnityTest]
    public IEnumerator SfxVolumeZero_IsSilent()
    {
        GameSettings.SfxVolume = 0;
        var hs = NewHitSound();
        yield return null;
        CombatEvents.RaiseEnemyDamaged(null);
        Assert.AreEqual(0, hs.Played);
    }
}

}

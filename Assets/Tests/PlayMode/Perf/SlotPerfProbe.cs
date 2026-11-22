using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 렉 측정(2026-11-20 사용자 "살짝씩 렉이 있다") — **평소 테스트 묶음에서는 돌지 않는다**([Explicit]).
/// 실제 게임 씬을 띄우고 세이브 슬롯 3(중력 법사 풀세팅·유물 전부 각성·몬스터 4배)으로 2-1에 들어가,
/// Z(최대 차지 발사)와 X(붕괴)를 쿨타임마다 대신 누르며 30초 동안 1초마다 기록한다. 플레이어는 죽지 않게 무적.
/// 비교용으로 같은 조건에서 유물만 뺀 판도 잰다.
/// 실행: `-runTests -testPlatform PlayMode -testFilter ChronoSavior.Tests.PlayMode.SlotPerfProbe` + 환경변수 CHRONO_PERF_OUT(결과 폴더).
/// </summary>
[Explicit, Category("Perf")]
public class SlotPerfProbe
{
    const float Seconds = 30f;
    string tempSave;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        tempSave = Path.Combine(Path.GetTempPath(), $"ChronoSavior_Perf_{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(tempSave);
        SaveService.SlotDirOverride = tempSave;
        SaveService.ClearSlotForTests();
        ResetStatics();
        yield return SceneManager.LoadSceneAsync(GameFlow.PersistentScene, LoadSceneMode.Additive);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        SaveService.PathOverride = TestSaveSandbox.Path;
        SaveService.SlotDirOverride = TestSaveSandbox.SlotDir;
        SaveService.ClearSlotForTests();
        Time.timeScale = 1f;
        var persistent = SceneManager.GetSceneByName(GameFlow.PersistentScene);
        if (persistent.isLoaded) yield return SceneManager.UnloadSceneAsync(persistent);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && (s.name == GameFlow.LobbyScene || s.name == GameFlow.CombatScene)) yield return SceneManager.UnloadSceneAsync(s);
        }
        try { if (Directory.Exists(tempSave)) Directory.Delete(tempSave, true); } catch (IOException) { }
        ResetStatics();
    }

    static void ResetStatics()
    {
        GameState.Reset(); RunState.Reset(); CombatEvents.Reset(); RunEvents.Reset(); WorldView.Reset();
        RunTransient.Reset(); CombatModifiers.Reset(); HitFeel.Reset(); ProfileService.Reset();
        FieldLayout.SetBossArena(false);
    }

    [UnityTest] public IEnumerator Slot3_Full() => Measure("슬롯3_전체", withRelics: true);
    [UnityTest] public IEnumerator Slot3_NoRelics() => Measure("슬롯3_유물없음", withRelics: false);

    IEnumerator Measure(string label, bool withRelics)
    {
        var preset = SavePresets.Build(3);
        if (!withRelics) preset.items = new ItemInventory();
        File.WriteAllText(SaveService.SlotPath(3), JsonUtility.ToJson(preset));
        Assert.IsTrue(SaveService.SelectSlot(3));

        var flow = PixelCanvas.Instance.Flow;
        bool? ok = null;
        Assert.IsTrue(flow.StartStage(2, 1, r => ok = r, spendDay: false));
        float until = Time.realtimeSinceStartup + 15f;
        while (ok == null && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual(true, ok, "2-1 입장 실패");
        yield return null;

        var mage = Object.FindFirstObjectByType<MageAttack>();
        var health = Object.FindFirstObjectByType<PlayerHealth>();
        Assert.IsNotNull(mage, "법사가 없다");
        var fire = typeof(MageAttack).GetMethod("FireAimed", BindingFlags.NonPublic | BindingFlags.Instance);
        var skill = typeof(MageAttack).GetMethod("TryUseSkill", BindingFlags.NonPublic | BindingFlags.Instance);
        var cdField = typeof(MageAttack).GetField("cdTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var ultField = typeof(MageAttack).GetField("ultCdTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        using var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        using var physRec = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics2D.Simulate");
        using var updRec = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate");
        using var gcCollectRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect");

        var sb = new StringBuilder();
        sb.AppendLine($"# {label} — 2-1, 몬스터 배율 {preset.spawnScale}, 유물 {(withRelics ? "전부 각성" : "없음")}");
        sb.AppendLine("초,프레임,평균ms,최대ms,33ms넘은프레임,GC할당KB,GC수거횟수,GC수거ms,물리ms,스크립트Updatems,새오브젝트(대략),적,이펙트,범위고리,유물탄,유물선,유물장,처치/초");

        int facing = 1;
        float t = 0f, secT = 0f;
        int frames = 0, slow = 0;
        float sumMs = 0f, maxMs = 0f;
        long gcBytes = 0; double physNs = 0, updNs = 0, gcNs = 0;
        int gc0 = System.GC.CollectionCount(0);
        int kills0 = RunState.Kills;
        int idPrev = ProbeId();
        int sec = 0;

        while (t < Seconds && GameState.IsRunning)
        {
            // 무적(깜빡임 없는 짧은 무적을 매 프레임) + 공격·스킬을 쿨마다
            if (health != null) health.GrantInvuln(0.1f);
            var p = ProfileService.Current;
            if ((float)cdField.GetValue(mage) <= 0f)
            {
                facing = -facing;
                fire.Invoke(mage, new object[] { 1f, p.mageBranch, p.mageTier, (float)facing, 0f });
                cdField.SetValue(mage, mage.cooldown);
            }
            if ((float)ultField.GetValue(mage) <= 0f) skill.Invoke(mage, new object[] { p, p.mageBranch, p.mageTier });

            yield return null;
            float dt = Time.unscaledDeltaTime;
            t += dt; secT += dt; frames++;
            float ms = dt * 1000f;
            sumMs += ms; if (ms > maxMs) maxMs = ms; if (ms > 33.4f) slow++;
            gcBytes += gcRec.Valid ? gcRec.LastValue : 0;
            physNs += physRec.Valid ? physRec.LastValue : 0;
            updNs += updRec.Valid ? updRec.LastValue : 0;
            gcNs += gcCollectRec.Valid ? gcCollectRec.LastValue : 0;

            if (secT >= 1f)
            {
                sec++;
                int id = ProbeId();
                int created = Mathf.Max(0, (idPrev - id) / 2);
                idPrev = id;
                var all = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
                int enemies = all.Count(x => x.CompareTag("Enemy"));
                int vfx = all.Count(x => x.name.StartsWith("Vfx_"));
                int rings = all.Count(x => x.name.StartsWith("RangeRing"));
                int proj = all.Count(x => x.name == "RelicProjectile");
                int lines = all.Count(x => x.name == "RelicLine" && x.gameObject.activeInHierarchy);
                int zones = all.Count(x => x.name == "RelicPullZone");
                int kills = RunState.Kills - kills0; kills0 = RunState.Kills;
                int gcs = System.GC.CollectionCount(0) - gc0; gc0 = System.GC.CollectionCount(0);
                sb.AppendLine($"{sec},{frames},{sumMs / frames:0.0},{maxMs:0.0},{slow},{gcBytes / 1024},{gcs},{gcNs / 1e6:0.0},{physNs / 1e6:0.0},{updNs / 1e6:0.0},{created},{enemies},{vfx},{rings},{proj},{lines},{zones},{kills}");
                secT = 0f; frames = 0; slow = 0; sumMs = 0f; maxMs = 0f; gcBytes = 0; physNs = updNs = gcNs = 0;
            }
        }

        string dir = System.Environment.GetEnvironmentVariable("CHRONO_PERF_OUT");
        if (string.IsNullOrEmpty(dir)) dir = Application.temporaryCachePath;
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"perf_{label}.csv"), sb.ToString(), new UTF8Encoding(true));
        Debug.Log("PERF\n" + sb);
        Assert.Greater(sec, 5, "측정이 너무 짧게 끝났다(판이 끝났거나 죽음)");
    }

    /// <summary>지금 새로 만들면 받을 인스턴스 번호 — 실행 중 만든 오브젝트는 번호가 계속 줄어든다(두 칸씩). 1초 차이 ≈ 그동안 만든 오브젝트 수.</summary>
    static int ProbeId()
    {
        var go = new GameObject("PerfProbe");
        int id = go.GetInstanceID();
        Object.Destroy(go);
        return id;
    }
}

}

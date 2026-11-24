using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
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
/// 실제 게임으로 메카닉 설치 순간을 찍는다(2026-11-24 사용자 "설치할 때 VFX 어떻게 되는지 gif로") — **평소 테스트 묶음에서는 돌지 않는다**([Explicit]).
/// 편집 화면 캡처는 시간이 안 흘러 설치기·장판·아크가 실제처럼 안 움직이므로, 실제 플레이 상태에서 1/30초 고정 시간으로 돌리며 카메라를 그대로 찍는다.
/// 설치 갈래 직업 테스트 세팅(최종 빌드 + 유물 전부)으로 2-1에 들어가, 플레이어를 세 자리로 옮겨 가며 X(설치)를 실제로 누른다. 플레이어는 무적.
/// 실행: `-runTests -testPlatform PlayMode -testFilter ChronoSavior.Tests.PlayMode.InstallerArrivalCapture` + 환경변수 CHRONO_CAPTURE_OUT(그림 폴더).
/// </summary>
[Explicit, Category("Capture")]
public class InstallerArrivalCapture
{
    string tempSave;

    [UnitySetUp]
    public IEnumerator Setup()
    {
        tempSave = Path.Combine(Path.GetTempPath(), $"ChronoSavior_Cap_{System.Guid.NewGuid():N}");
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
        Time.captureFramerate = 0;
        SaveService.PathOverride = TestSaveSandbox.Path;
        SaveService.SlotDirOverride = TestSaveSandbox.SlotDir;
        SaveService.ClearSlotForTests();
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

    [UnityTest]
    public IEnumerator Capture()
    {
        string outDir = System.Environment.GetEnvironmentVariable("CHRONO_CAPTURE_OUT");
        if (string.IsNullOrEmpty(outDir)) outDir = Path.Combine(Path.GetTempPath(), "ChronoSavior_InstallerCapture");
        Directory.CreateDirectory(outDir);

        var preset = SavePresets.BuildClassTest(CharacterId.Gunner, 1); // 설치 갈래
        File.WriteAllText(SaveService.SlotPath(SavePresets.ClassTestSlot), JsonUtility.ToJson(preset));
        Assert.IsTrue(SaveService.SelectSlot(SavePresets.ClassTestSlot));
        var flow = PixelCanvas.Instance.Flow;
        bool? ok = null;
        Assert.IsTrue(flow.StartStage(2, 1, r => ok = r, spendDay: false));
        float until = Time.realtimeSinceStartup + 15f;
        while (ok == null && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual(true, ok, "2-1 입장 실패");

        var kit = Object.FindFirstObjectByType<GunnerAttack>();
        var health = Object.FindFirstObjectByType<PlayerHealth>();
        Assert.IsNotNull(kit, "메카닉이 없다");
        var skill = typeof(GunnerAttack).GetMethod("TryUseSkill", BindingFlags.NonPublic | BindingFlags.Instance);
        var ult = typeof(GunnerAttack).GetField("ultCdTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        var stacks = typeof(GunnerAttack).GetProperty("Stacks");
        var rb = kit.GetComponent<Rigidbody2D>();
        var cam = Camera.main;

        Time.captureFramerate = 30;
        // 몹이 깔릴 때까지 조금 기다린다
        for (int i = 0; i < 60; i++) { health?.GrantInvuln(0.2f); yield return null; }

        var rt = new RenderTexture(1920, 1080, 24);
        var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        int frame = 0;
        float[] xs = { 4.5f, 9.5f, 14.5f };
        foreach (float x in xs)
        {
            // 그 자리로 옮기고 잠깐 선다
            rb.position = new Vector2(x, FieldBounds.GroundY + 0.6f);
            rb.linearVelocity = Vector2.zero;
            for (int i = 0; i < 6; i++) { health?.GrantInvuln(0.2f); yield return null; Shot(cam, rt, tex, outDir, frame++); }
            stacks.SetValue(kit, Mathf.Max(1, kit.Stacks));
            ult.SetValue(kit, 0f);
            skill.Invoke(kit, null);                       // X — 실제 설치
            for (int i = 0; i < 30; i++) { health?.GrantInvuln(0.2f); yield return null; Shot(cam, rt, tex, outDir, frame++); }
        }
        Object.Destroy(rt);
        Object.Destroy(tex);
        Debug.Log($"[InstallerArrivalCapture] {frame}장 저장: {outDir}");
    }

    static void Shot(Camera cam, RenderTexture rt, Texture2D tex, string dir, int frame)
    {
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        var active = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = active;
        File.WriteAllBytes(Path.Combine(dir, $"f_{frame:000}.png"), tex.EncodeToPNG());
    }
}

}

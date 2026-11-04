using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 에디터에서 Play를 누르면 **게임 씬(`20_Combat` 등)을 열어 두었어도 `00_Persistent`부터 시작한다**(2026-11-04 씬 분할).
    /// 게임은 `00_Persistent`(UI·흐름·카메라)가 먼저 떠야 돌아가는데, 평소처럼 `20_Combat`만 열고 Play하면 UI가 없다.
    /// 빌드에는 영향이 없다(에디터 전용). `00_Persistent`의 `GameBoot`가 월드 씬을 불러온다.
    ///
    /// ⚠️ **테스트 실행에는 걸지 않는다.** 시작 씬을 늘 지정해 두면 Test Runner도 그 씬으로 Play해서 실제 게임이 뜨고
    /// `GameBoot`가 개발자의 진짜 세이브를 읽는다(2026-11-04 실제로 겪음 — 배치 테스트가 멈췄다). Test Runner는 저장되지 않은
    /// 임시 씬에서 Play하므로, **Build Settings에 있는 게임 씬이 활성일 때만** 이번 Play에 한해 지정하고 끝나면 지운다.
    /// </summary>
    [InitializeOnLoad]
    static class PlayFromPersistent
    {
        static PlayFromPersistent()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                string active = SceneManager.GetActiveScene().path;
                bool gameScene = System.Array.Exists(EditorBuildSettings.scenes, s => s.path == active);
                EditorSceneManager.playModeStartScene = gameScene
                    ? AssetDatabase.LoadAssetAtPath<SceneAsset>(SceneSetupValidator.PersistentPath)
                    : null;
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                EditorSceneManager.playModeStartScene = null;
            }
        }
    }
}

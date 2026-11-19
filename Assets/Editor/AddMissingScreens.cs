using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.UI;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// `00_Persistent`의 판(PixelCanvas)에 화면 목록(<see cref="PxScreens.All"/>) 중 빠진 화면 컴포넌트를 붙이고 씬을 저장한다.
    /// 새 화면을 목록에 넣었을 때 한 번 돌린다(2026-11-19 세이브 슬롯 창). 배치: `-executeMethod ChronoSavior.Editor.AddMissingScreens.Run`.
    /// </summary>
    public static class AddMissingScreens
    {
        [MenuItem("Chrono Savior/판에 빠진 화면 붙이기")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            var canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PixelCanvas>(true)).FirstOrDefault();
            if (canvas == null) { Debug.LogError("[AddMissingScreens] 00_Persistent에 PixelCanvas가 없다"); return; }
            int added = 0;
            foreach (var type in PxScreens.All)
            {
                if (canvas.GetComponent(type) != null) continue;
                canvas.gameObject.AddComponent(type);
                added++;
                Debug.Log($"[AddMissingScreens] {type.Name} 붙임");
            }
            if (added > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[AddMissingScreens] 끝 — {added}개 붙임");
        }
    }
}

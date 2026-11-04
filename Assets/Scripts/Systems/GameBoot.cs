using UnityEngine;
using UnityEngine.SceneManagement;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 게임 시작 담당 — **`00_Persistent`(빌드 첫 씬)에만 놓인다**(2026-11-04 씬 분할). 하는 일, 순서대로:
    /// 1. **세이브 불러오기** — 다른 모든 컴포넌트의 Awake보다 먼저(<c>DefaultExecutionOrder(-1000)</c>).
    ///    예전엔 `AutoSave.Awake`가 '씬 빌더가 이 오브젝트를 먼저 만든다'는 생성 순서에 기대고 있었다.
    /// 2. **월드 씬(<see cref="WorldScene"/>)을 겹쳐 불러오고, 다 불러오면 그 씬을 활성 씬으로 정한다.** Unity는 새 오브젝트를
    ///    활성 씬에 만들기 때문에, 이걸 안 하면 사냥 중 생기는 투사체·이펙트가 `00_Persistent`에 쌓인다.
    ///
    /// <see cref="GameFlow"/>(상태 전환)는 PlayMode 테스트가 빈 씬에 직접 만들어 쓰므로 세이브·씬 로드를 하지 않는다 —
    /// 그건 이 컴포넌트가 있는 실제 게임에서만 일어난다(테스트가 개발자의 세이브를 읽지 않게, `AutoSave` 주석 참고).
    /// 1차 분할에선 월드가 한 씬(로비·전투가 같이)이라 시작할 때 한 번만 불러온다. 로비를 나누는 2차에서 전환 로드를 GameFlow가 맡는다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public class GameBoot : MonoBehaviour
    {
        /// <summary>로비·전투가 들어 있는 월드 씬 이름(Build Settings 1번).</summary>
        public const string WorldScene = "20_Combat";

        void Awake()
        {
            if (SaveService.Load()) Debug.Log($"[GameBoot] 세이브 불러옴 — {SaveService.SavePath}");

            var world = SceneManager.GetSceneByName(WorldScene);
            if (world.isLoaded) { SceneManager.SetActiveScene(world); return; } // 에디터에서 두 씬을 같이 열어 둔 경우
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.LoadScene(WorldScene, LoadSceneMode.Additive);
        }

        void OnDestroy() => SceneManager.sceneLoaded -= HandleSceneLoaded;

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != WorldScene) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.SetActiveScene(scene);
        }
    }
}

using UnityEngine;

namespace ChronoSavior.Enemies
{
    // 몹과 보스가 LateUpdate에서 정한 마지막 그림과 방향으로 실루엣을 그린다.
    [DefaultExecutionOrder(100)]
    public class EnemyOverlayFollower : MonoBehaviour
    {
        SpriteRenderer body;
        SpriteRenderer overlay;

        public void Initialize(SpriteRenderer source, SpriteRenderer target)
        {
            body = source;
            overlay = target;
            Sync();
        }

        void OnEnable()
        {
            if (body != null) Sync();
        }

        void LateUpdate() => Sync();

        void Sync()
        {
            overlay.sprite = body.sprite;
            overlay.flipX = body.flipX;
            overlay.flipY = body.flipY;
            overlay.sortingLayerID = body.sortingLayerID;
            overlay.sortingOrder = body.sortingOrder + 1;
            overlay.enabled = body.enabled;
        }
    }
}

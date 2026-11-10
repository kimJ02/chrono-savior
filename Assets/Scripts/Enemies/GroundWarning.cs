using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 바닥 빨간 경고 — 공격이 떨어질 자리를 미리 보여 준다(2026-11-10 팀원 명세 "덩어리 지배자" 공통 규칙: 모든 공격 전에 빨간 경고,
    /// **판정과 같은 크기**). 그림은 납작한 빨간 타원 한 장(`Sprites/Pixel/BlobLordWarning.png`, 반경 <see cref="BakedRadius"/>에 맞춰 그림) —
    /// 횡스크롤에서 바닥 위의 원은 납작한 타원으로 보인다.
    ///
    /// 상태는 셋: **따라감**(천천히 맥박) → **고정**(진하게, 빠른 깜빡임 — "이제 안 따라온다"는 신호) → **끝**(하얗게 번쩍이고 사라짐).
    /// 판정은 쓰는 쪽(<see cref="BlobLordBoss"/>)이 한다 — 이건 그림만.
    /// 구성은 프리팹(`Prefabs/Combat/GroundWarning`)에 있다.
    /// </summary>
    public class GroundWarning : MonoBehaviour
    {
        /// <summary>그림 한 장이 나타내는 반경(유닛) — 도트 배율 2로 키웠을 때 가로 반지름. 다른 반경은 가로만 늘린다.</summary>
        public const float BakedRadius = 1.8f;
        /// <summary>바닥 타일(-5) 위, 캐릭터·적(0) 뒤 — CLAUDE.md 정렬표 -2.</summary>
        public const int SortingOrder = -2;
        public const float FlashTime = 0.18f;

        [SerializeField] SpriteRenderer sr;

        enum Mode { Hidden, Tracking, Locked, Flash }
        Mode mode = Mode.Hidden;
        float t;

        public bool Visible => mode != Mode.Hidden;
        public bool IsLocked => mode == Mode.Locked;
        public float Radius { get; private set; }
        public Vector2 Point => transform.position;

        void Awake()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();
            sr.sortingOrder = SortingOrder;
            Hide();
        }

        /// <summary>바닥 점 <paramref name="point"/>(윗면 높이)에 반경 <paramref name="radius"/>로 띄운다 — 따라가는 상태로.</summary>
        public void Show(Vector2 point, float radius)
        {
            Radius = radius;
            float sx = DotScale.Value * radius / BakedRadius;
            transform.localScale = new Vector3(sx, DotScale.Value, 1f);
            transform.position = point;
            mode = Mode.Tracking; t = 0f;
            sr.enabled = true;
            Apply();
        }

        public void MoveTo(Vector2 point) => transform.position = point;

        /// <summary>자리를 고정한다 — 이후로는 안 움직인다(그림만 바뀐다. 움직임을 멈추는 건 부르는 쪽).</summary>
        public void Lock() { if (mode == Mode.Tracking) { mode = Mode.Locked; t = 0f; Apply(); } }

        /// <summary>공격이 떨어졌다 — 하얗게 번쩍이고 <see cref="FlashTime"/>초 뒤 사라진다.</summary>
        public void Flash() { if (mode != Mode.Hidden) { mode = Mode.Flash; t = 0f; Apply(); } }

        public void Hide()
        {
            mode = Mode.Hidden;
            if (sr != null) sr.enabled = false;
        }

        void Update()
        {
            if (mode == Mode.Hidden) return;
            t += Time.deltaTime;
            if (mode == Mode.Flash && t >= FlashTime) { Hide(); return; }
            Apply();
        }

        void Apply()
        {
            switch (mode)
            {
                case Mode.Tracking: // 천천히 맥박 — 아직 따라온다
                    sr.color = new Color(1f, 1f, 1f, 0.55f + 0.25f * Mathf.Sin(t * 10f));
                    break;
                case Mode.Locked:   // 진하게, 빠르게 깜빡 — 고정됐다
                    sr.color = Mathf.Repeat(t, 0.1f) < 0.05f ? Color.white : new Color(1f, 0.75f, 0.75f, 1f);
                    break;
                case Mode.Flash:    // 하얗게 → 사라짐
                    sr.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(t / FlashTime));
                    break;
            }
        }
    }
}

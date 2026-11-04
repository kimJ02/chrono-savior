using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 블랙홀(중력) 마법사의 공격이 적중한 자리에 한 번 터지는 폭발 — 2026-10-25 사용자가 받은
    /// "Super Pixel Effects Gigapack (Free Version)"의 `stylized_explosion_002_large_violet`(10프레임, 96px, 15fps).
    /// **그림만이다** — 피해와 끌어당기는 중력장(보라 고리)은 <see cref="MageSkillEffects.DetonateGravityOrb"/>가 그대로 한다.
    /// 그림 한 변이 폭발 지름(반지름 × 2)이 되도록 키운다.
    /// </summary>
    public class GravityImpactVfx : MonoBehaviour
    {
        /// <summary>팩 안내대로 15fps — 10프레임이라 0.667초.</summary>
        public const float Fps = 15f;
        const string Folder = "Art/Figma/Vfx/GravityImpact";
        const int SortingOrder = 5; // 중력점 고리(3)·대붕괴 고리(4) 위

        static Sprite[] frames;
        SpriteRenderer sr;
        float age;

        public static float Life => (frames != null && frames.Length > 0 ? frames.Length : 10) / Fps;

        /// <summary>시간 t(초)에 보일 프레임 번호.</summary>
        public static int FrameAt(float t, int count) => Mathf.Clamp(Mathf.FloorToInt(t * Fps), 0, Mathf.Max(0, count - 1));

        /// <summary>폭발 그림 10장(이름순). 중력장의 소용돌이(<see cref="GravityVortexVfx"/>)도 같은 그림을 쓴다.</summary>
        public static Sprite[] Frames()
        {
            if (frames == null || frames.Length == 0)
            {
                frames = Resources.LoadAll<Sprite>(Folder);
                System.Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
            }
            return frames;
        }

        public static GravityImpactVfx Spawn(Vector2 pos, float radius)
        {
            if (Frames().Length == 0) return null;
            var go = new GameObject("GravityImpact");
            go.transform.position = pos;
            RunTransient.Mark(go);
            go.transform.localScale = Vector3.one * (radius * 2f / frames[0].bounds.size.x);
            var v = go.AddComponent<GravityImpactVfx>();
            v.sr = go.AddComponent<SpriteRenderer>();
            v.sr.sortingOrder = SortingOrder;
            v.sr.sprite = frames[0];
            return v;
        }

        /// <summary>지금 보이는 그림(테스트용).</summary>
        public Sprite Current => sr != null ? sr.sprite : null;

        void Update()
        {
            age += Time.deltaTime; // 히트스톱(timeScale 0) 동안엔 같이 멈춘다
            if (age >= Life)
            {
                Destroy(gameObject);
                return;
            }
            sr.sprite = frames[FrameAt(age, frames.Length)];
        }

        /// <summary>캡처용 — 시간을 직접 정해 그 프레임을 보인다.</summary>
        public void ShowAt(float t)
        {
            age = t;
            sr.sprite = frames[FrameAt(t, frames.Length)];
        }
    }
}

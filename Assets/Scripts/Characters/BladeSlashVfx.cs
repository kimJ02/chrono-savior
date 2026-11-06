using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 섬영 기본 공격(회전베기 Z)에 겹쳐 그리는 베기 효과 — 2026-10-25 사용자가 받은 "Pixel Art Animations - Slashes"의
    /// Slash 1 · color4(9프레임, 128px)를 섬영에 적용. 공격 판정은 그대로, 그림만 얹는다.
    ///
    /// 섬영은 팀원 파일(`BladeCombat`)이라 안 건드리고 밖에서 읽는다: 회전베기를 휘두르면 `cdTimer`가 쿨다운 값으로 도로 올라가니
    /// 그 오름을 한 번의 휘두름으로 본다(`PlayerAfterimages`가 속도를 밖에서 읽는 것과 같은 방식). `PlayerRig`가 붙인다.
    /// 그림은 캐릭터를 따라다니고, 바라보는 쪽에 나오며, 휘두를 때마다 위아래를 번갈아 뒤집는다.
    /// </summary>
    public class BladeSlashVfx : MonoBehaviour, IRunResettable
    {
        /// <summary>한 번 재생하는 시간(초) — 쿨다운 0.13초보다 조금 길어 연타하면 겹쳐 이어진다.</summary>
        public const float Life = 0.2f;
        /// <summary>캐릭터 중심에서 바라보는 쪽으로 나가는 거리(유닛).</summary>
        public const float ForwardOffset = 0.45f;
        /// <summary>그림 크기 배율 — 128px 프레임 = 1유닛, 회전베기 반경 1.05의 지름에 맞춘다.</summary>
        public const float Scale = 2f;

        struct Play { public SpriteRenderer sr; public float t; public int facing; }
        readonly List<Play> playing = new List<Play>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        [Tooltip("베기 애니메이션 프레임(순서대로) — 지금은 Assets/Resources/Art/Figma/Vfx/BladeSlash 9장. 외주 효과로 바꿀 땐 이 칸만(프레임 수는 자유).")]
        [SerializeField] Sprite[] frames;

        /// <summary>씬 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources` 폴더에서 이름순으로 읽었다).</summary>
        public Sprite[] Frames { get => frames; set => frames = value; }

        BladeCombat blade;
        SpriteRenderer body;
        CharacterMover2D mover;
        float lastCd;
        bool flip;

        public int Count => playing.Count;

        /// <summary>시간 t(초)에 보일 프레임 번호.</summary>
        public static int FrameAt(float t, int count) => Mathf.Clamp(Mathf.FloorToInt(t / Life * count), 0, Mathf.Max(0, count - 1));

        void Awake()
        {
            blade = GetComponent<BladeCombat>();
            body = GetComponent<SpriteRenderer>();
            mover = GetComponent<CharacterMover2D>();
        }

        void Update()
        {
            float dt = Time.deltaTime; // 히트스톱·일시정지 땐 0 — 같이 멈춘다
            // 로비·타이틀로 나가면 남은 베기 그림을 지운다(시간이 멈춰 그 자리에 굳어 남는다 — 잔상과 같은 문제).
            if (playing.Count > 0 && (GameState.Current == GameScene.Lobby || GameState.Current == GameScene.Title)) ResetForRun();
            Age(dt);
            if (blade == null) return;
            float cd = blade.SpinCooldownLeft; // 베면 쿨다운이 다시 찬다 — 그 순간을 본다
            bool swung = cd > lastCd + 0.0001f;
            lastCd = cd;
            if (swung && dt > 0f && GameState.Current == GameScene.Run && ProfileService.Current.character == CharacterId.Blade)
                Spawn();
        }

        /// <summary>지금 자리에서 베기 효과 하나를 시작한다.</summary>
        public void Spawn()
        {
            if (frames == null || frames.Length == 0 || body == null) return; // 씬엔 늘 있다(씬 구성 검증이 본다)
            var sr = pool.Count > 0 ? pool.Pop() : new GameObject("BladeSlash").AddComponent<SpriteRenderer>();
            sr.gameObject.SetActive(true);
            sr.sortingLayerID = body.sortingLayerID;
            sr.sortingOrder = body.sortingOrder + 1;
            sr.transform.localScale = Vector3.one * Scale;
            flip = !flip;
            sr.flipY = flip;
            playing.Add(new Play { sr = sr, t = 0f, facing = mover != null ? mover.Facing : 1 });
            Place(playing.Count - 1);
        }

        void Place(int i)
        {
            var p = playing[i];
            p.sr.transform.position = transform.position + new Vector3(p.facing * ForwardOffset, 0f, 0f);
            p.sr.flipX = p.facing < 0;
            p.sr.sprite = frames[FrameAt(p.t, frames.Length)];
        }

        void Age(float dt)
        {
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                var p = playing[i];
                p.t += dt;
                if (p.t >= Life)
                {
                    p.sr.gameObject.SetActive(false);
                    pool.Push(p.sr);
                    playing.RemoveAt(i);
                    continue;
                }
                playing[i] = p;
                Place(i);
            }
        }

        public void ResetForRun()
        {
            foreach (var p in playing) { p.sr.gameObject.SetActive(false); pool.Push(p.sr); }
            playing.Clear();
            lastCd = 0f;
        }

        void OnDestroy()
        {
            foreach (var p in playing) if (p.sr != null) Destroy(p.sr.gameObject);
            foreach (var sr in pool) if (sr != null) Destroy(sr.gameObject);
            playing.Clear();
            pool.Clear();
        }
    }
}

using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 마법사 차지 표시 — 그림만이다(차지 수치·판정은 <see cref="MageAttack"/>).
    ///
    /// 1. **완충 깜빡임**(2026-10-25 사용자 "두 번 말고 풀차징 되면 한 번만"): 차지가 다 차는 순간 몸 모양이 한 번 하얗게 번쩍인다
    ///    (몹 피격 플래시와 같은 셰이더·세기·감쇠). 문턱은 원본 `bowFire`의 **완충 99%**(`full = chargeK >= 0.99`, project_test.html:1944·:2047).
    /// 2. **손 오브**(2026-11-16, 사용자 "차지 애니메이션 폴더 보면 이펙트가 또 따로있음" — `Character_Mage_Attack_OrbEffect_01~11.png`가
    ///    안 쓰이고 있던 걸 지적받아 추가): 몸 그림과 **같은 캔버스**(70×48, PPU 128, 피벗 0.5/0.546875 — Body·KiEffect 프레임들과 동일,
    ///    `.meta` 대조로 확인)에 그려진 손의 빛나는 구슬이 11장에 걸쳐 커진다 — 차지율(<see cref="MageAttack.ChargeRatio"/>)에 11등분해
    ///    그대로 맞춰(루프 아님, 0%=1번 프레임·100%=11번 프레임) 그린다. 같은 캔버스라 변환을 보정할 필요 없이 몸과 같은 부모(Player 루트)에
    ///    통째로 겹친다.
    /// 3. **오브 일렁임**(2026-11-16, 사용자 정정 — "KiEffect_01~06는 발사순간이 아니라 차지 9장쯤부터 오브 근처에 일렁이듯
    ///    반복되는 이펙트" — 처음엔 Attack 발사 순간용으로 잘못 짐작했었다): `Character_Mage_Attack_KiEffect_01~06.png`를
    ///    오브가 <see cref="KiEffectStartOrbFrame"/>번째 프레임(9번째, 0-based 8)에 들어선 뒤부터 2번과 같은 자리에 겹쳐 반복 재생한다.
    ///    차지율과 매핑하는 2번과 달리 이건 시간 기반 루프(<see cref="KiEffectFps"/>)다 — 그림 자체가 "일렁임"이라 정지된 프레임이 아니라
    ///    계속 움직여야 자연스럽다.
    ///
    /// **오라는 뺐다**(2026-11-16 사용자 "원래 차지할때 캐릭터뒤에 오라같은거는 없애줄래 필요없다" — 2026-11-07 VFX 스프린트에서
    /// 넣었던 외주 VFX 팩 기반 `scifi_charge_up_001` 오라(`VfxKeys.MageChargeSmall`/`…Violet`)를 통째로 제거. 테스트
    /// `ChargeAura_*`도 같이 지웠다. 되살릴 땐 `MageChargeVfxTests.cs`의 git 히스토리 참고.
    ///
    /// 원본엔 전부 없다(원본의 차지 표시는 버프 줄 "차지 N%"뿐) — 사용자·팀 결정으로 넣은 것. 예전 이름 `MageChargeFlash`(깜빡임만 할 때).
    /// </summary>
    [RequireComponent(typeof(MageAttack))]
    public class MageChargeVfx : MonoBehaviour
    {
        /// <summary>깜빡이는 차지 문턱(차지율) — 원본 완충 하나.</summary>
        public static readonly float[] Stages = { 0.99f };
        /// <summary>한 번 번쩍임이 사라지는 시간(초) — 몹 피격 플래시(원본 `e.flash -= dt * 6`, 약 0.17초)와 같게.</summary>
        public const float Duration = 1f / 6f;
        /// <summary>번쩍임 세기 — 몹 피격 플래시(원본 `flash × 0.75`, :4799)와 같게.</summary>
        public const float Strength = 0.75f;

        /// <summary>오브가 이 프레임(0-based, 11장 중 9번째=인덱스 8)에 들어서야 일렁임이 같이 보이기 시작한다 — 사용자 "오브 9장쯤부터".</summary>
        public const int KiEffectStartOrbFrame = 8;
        /// <summary>일렁임 6장이 도는 속도(시간 기반 루프 — 차지율과 무관).</summary>
        public const float KiEffectFps = 10f;

        [Tooltip("효과 머티리얼(Assets/Materials/SpriteFlash — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색으로 칠하는 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material flashMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources`에서 셰이더를 읽어 실행 중에 머티리얼을 만들었다).</summary>
        public Material FlashMaterial { get => flashMaterial; set => flashMaterial = value; }

        MageAttack mage;
        SpriteRenderer body, flashSr, orbSr, kiSr;
        int stage;
        float flash;
        float kiTimer;

        /// <summary>손 오브 11장(캐시, 캐릭터 공용 — Resources 밖에선 못 읽으니 그림이 아예 없으면 전부 null).</summary>
        static Sprite[] orbFrames;
        static Sprite[] OrbFrames()
        {
            if (orbFrames == null)
            {
                orbFrames = new Sprite[11];
                for (int i = 0; i < orbFrames.Length; i++)
                    orbFrames[i] = Resources.Load<Sprite>($"Art/Characters/Mage/Character_Mage_Attack_OrbEffect_{i + 1:00}");
            }
            return orbFrames;
        }

        /// <summary>오브 일렁임 6장(캐시).</summary>
        static Sprite[] kiFrames;
        static Sprite[] KiFrames()
        {
            if (kiFrames == null)
            {
                kiFrames = new Sprite[6];
                for (int i = 0; i < kiFrames.Length; i++)
                    kiFrames[i] = Resources.Load<Sprite>($"Art/Characters/Mage/Character_Mage_Attack_KiEffect_{i + 1:00}");
            }
            return kiFrames;
        }

        /// <summary>차지율 k에서 몇 단계인지 — 0 = 아직, 1 = 완충.</summary>
        public static int StageAt(float k)
        {
            int s = 0;
            foreach (float t in Stages) if (k >= t) s++;
            return s;
        }

        /// <summary>지금 번쩍임 세기 0~1(테스트용).</summary>
        public float FlashAmount => flash;
        /// <summary>손 오브가 보이는가(테스트용).</summary>
        public bool OrbVisible => orbSr != null && orbSr.enabled;
        /// <summary>오브 일렁임이 보이는가(테스트용).</summary>
        public bool KiVisible => kiSr != null && kiSr.enabled;

        void Awake()
        {
            mage = GetComponent<MageAttack>();
            body = GetComponent<SpriteRenderer>();
        }

        // MageAttack.Update가 차지율을 올린 뒤에 읽도록 LateUpdate에서.
        void LateUpdate() => Refresh(Time.deltaTime); // 히트스톱·일시정지 땐 0 — 번쩍임·일렁임도 같이 멈춘다

        /// <summary>손 오브·오브 일렁임을 켜고 끄고, 완충이면 번쩍이고, 번쩍임을 줄여 그린다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            bool charging = mage != null && mage.enabled && mage.IsCharging;
            float ratio = charging ? mage.ChargeRatio : 0f;
            int orbIdx = UpdateOrb(charging, ratio);
            UpdateKi(charging, orbIdx, dt);

            int now = charging ? StageAt(mage.ChargeRatio) : 0;
            if (now > stage) flash = 1f;
            else if (flash > 0f) flash = Mathf.Max(0f, flash - dt / Duration);
            stage = now;
            DrawFlash();
        }

        /// <summary>손 오브 — 차지율을 11장에 그대로 매핑(루프 없음, 0%=1번째·100%=11번째). 지금 그린 프레임 인덱스를 돌려준다(안 그렸으면 -1).</summary>
        int UpdateOrb(bool charging, float ratio)
        {
            if (!charging)
            {
                if (orbSr != null) orbSr.enabled = false;
                return -1;
            }
            if (orbSr == null)
            {
                var go = new GameObject("ChargeOrb", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false); // 몸과 같은 캔버스·피벗이라 변환 그대로 겹친다(보정 불필요)
                orbSr = go.GetComponent<SpriteRenderer>();
                if (body != null) orbSr.sortingLayerID = body.sortingLayerID;
                orbSr.sortingOrder = (body != null ? body.sortingOrder : 0) + 1; // 몸 바로 앞 — 완충 번쩍임·베기 효과와 같은 자리
            }
            var frames = OrbFrames();
            int idx = Mathf.Clamp(Mathf.RoundToInt(ratio * (frames.Length - 1)), 0, frames.Length - 1);
            var sprite = frames[idx];
            orbSr.enabled = sprite != null;
            if (sprite == null) return -1; // 그림이 없으면(이론상 늘 있어야 하지만) 숨긴다 — Resources는 VFX 팩과 달리 git에 들어있다
            orbSr.sprite = sprite;
            if (body != null) { orbSr.flipX = body.flipX; orbSr.flipY = body.flipY; }
            return idx;
        }

        /// <summary>오브 일렁임 — 오브가 <see cref="KiEffectStartOrbFrame"/> 이상일 때만, 시간 기반으로 6장을 돈다.</summary>
        void UpdateKi(bool charging, int orbIdx, float dt)
        {
            bool show = charging && orbIdx >= KiEffectStartOrbFrame;
            if (!show)
            {
                if (kiSr != null) kiSr.enabled = false;
                kiTimer = 0f; // 다음에 다시 켜질 때 처음부터
                return;
            }
            if (kiSr == null)
            {
                var go = new GameObject("ChargeKi", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false); // 오브와 같은 캔버스·자리
                kiSr = go.GetComponent<SpriteRenderer>();
                if (body != null) kiSr.sortingLayerID = body.sortingLayerID;
                kiSr.sortingOrder = (body != null ? body.sortingOrder : 0) + 1;
            }
            kiTimer += dt;
            var frames = KiFrames();
            int idx = Mathf.FloorToInt(kiTimer * KiEffectFps) % frames.Length;
            var sprite = frames[idx];
            kiSr.enabled = sprite != null;
            if (sprite == null) return;
            kiSr.sprite = sprite;
            if (body != null) { kiSr.flipX = body.flipX; kiSr.flipY = body.flipY; }
        }

        void DrawFlash()
        {
            if (body == null) return;
            if (flashSr == null)
            {
                if (flash <= 0f) return;
                if (flashMaterial == null) return; // 씬엔 늘 있다(씬 구성 검증이 본다)
                var go = new GameObject("ChargeFlash");
                go.transform.SetParent(transform, false);
                flashSr = go.AddComponent<SpriteRenderer>();
                flashSr.sharedMaterial = flashMaterial;
            }
            flashSr.enabled = flash > 0f && body.sprite != null;
            if (!flashSr.enabled) return;
            flashSr.sprite = body.sprite;
            flashSr.flipX = body.flipX;
            flashSr.flipY = body.flipY;
            flashSr.sortingLayerID = body.sortingLayerID;
            flashSr.sortingOrder = body.sortingOrder + 1;
            flashSr.color = new Color(1f, 1f, 1f, flash * Strength);
        }
    }
}

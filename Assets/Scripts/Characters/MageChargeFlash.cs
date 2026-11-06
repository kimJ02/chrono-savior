using UnityEngine;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 마법사 완충 깜빡임 — 2026-10-25 사용자 "차지 단계마다 깜빡이게" → 같은 날 "두 번 말고 풀차징 되면 한 번만".
    /// 차지가 다 차는 순간 캐릭터 몸 모양이 한 번 하얗게 번쩍인다(몹 피격 플래시와 같은 셰이더·같은 세기·같은 감쇠).
    ///
    /// 문턱은 원본 `bowFire`의 **완충 99%**(`full = chargeK >= 0.99` — 폭발력 최대·중첩 3), project_test.html:1944·:2047.
    /// 강화탄(50%)에선 안 번쩍인다.
    /// 원본엔 이 깜빡임이 없다(원본의 차지 표시는 버프 줄 "차지 N%"뿐) — 사용자 결정으로 넣은 것.
    /// </summary>
    [RequireComponent(typeof(MageAttack))]
    public class MageChargeFlash : MonoBehaviour
    {
        /// <summary>깜빡이는 차지 문턱(차지율) — 원본 완충 하나.</summary>
        public static readonly float[] Stages = { 0.99f };
        /// <summary>한 번 번쩍임이 사라지는 시간(초) — 몹 피격 플래시(원본 `e.flash -= dt * 6`, 약 0.17초)와 같게.</summary>
        public const float Duration = 1f / 6f;
        /// <summary>번쩍임 세기 — 몹 피격 플래시(원본 `flash × 0.75`, :4799)와 같게.</summary>
        public const float Strength = 0.75f;

        [Tooltip("효과 머티리얼(Assets/Materials/SpriteFlash — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색으로 칠하는 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material flashMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources`에서 셰이더를 읽어 실행 중에 머티리얼을 만들었다).</summary>
        public Material FlashMaterial { get => flashMaterial; set => flashMaterial = value; }

        MageAttack mage;
        SpriteRenderer body, flashSr;
        int stage;
        float flash;

        /// <summary>차지율 k에서 몇 단계인지 — 0 = 아직, 1 = 완충.</summary>
        public static int StageAt(float k)
        {
            int s = 0;
            foreach (float t in Stages) if (k >= t) s++;
            return s;
        }

        /// <summary>지금 번쩍임 세기 0~1(테스트용).</summary>
        public float FlashAmount => flash;

        void Awake()
        {
            mage = GetComponent<MageAttack>();
            body = GetComponent<SpriteRenderer>();
        }

        // MageAttack.Update가 차지율을 올린 뒤에 읽도록 LateUpdate에서.
        void LateUpdate() => Refresh(Time.deltaTime); // 히트스톱·일시정지 땐 0 — 번쩍임도 같이 멈춘다

        /// <summary>차지 단계가 올랐으면 번쩍이고, 번쩍임을 줄여 그린다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            int now = mage != null && mage.IsCharging ? StageAt(mage.ChargeRatio) : 0;
            if (now > stage) flash = 1f;
            else if (flash > 0f) flash = Mathf.Max(0f, flash - dt / Duration);
            stage = now;
            Draw();
        }

        void Draw()
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

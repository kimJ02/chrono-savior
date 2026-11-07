using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 마법사 차지 표시 — 그림만이다(차지 수치·판정은 <see cref="MageAttack"/>).
    ///
    /// 1. **차지 오라**(2026-11-07 VFX 스프린트, 팀원 문서 '이펙트와 연출' — "이펙트 크기 고정"): 차지하는 동안 몸 중심에 기가팩
    ///    `scifi_charge_up_001`(키 <see cref="VfxKeys.MageChargeLarge"/>, 96px × 1 = 0.75유닛)이 되풀이된다. 크기는 차지율과 상관없이 고정.
    ///    색은 갈래마다 — 빌드 없음은 그림 그대로(노랑), 폭발은 주황, 중력은 보라로 틴트한다(팩엔 노랑뿐). 그림이 없으면(팩을 안 가져온 클론) 안 보인다.
    /// 2. **완충 깜빡임**(2026-10-25 사용자 "두 번 말고 풀차징 되면 한 번만"): 차지가 다 차는 순간 몸 모양이 한 번 하얗게 번쩍인다
    ///    (몹 피격 플래시와 같은 셰이더·세기·감쇠). 문턱은 원본 `bowFire`의 **완충 99%**(`full = chargeK >= 0.99`, project_test.html:1944·:2047).
    ///
    /// 원본엔 둘 다 없다(원본의 차지 표시는 버프 줄 "차지 N%"뿐) — 사용자·팀 결정으로 넣은 것. 예전 이름 `MageChargeFlash`(깜빡임만 할 때).
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

        /// <summary>차지 오라 재생 속도(팩 기본 15fps).</summary>
        public const float AuraFps = 15f;
        /// <summary>오라는 몸 **뒤**(몸 정렬 − 1, 잔상과 같은 자리) — 그림 가운데가 꽉 찬 빛이라 앞에 두면 얼굴을 가린다(캡처로 확인).</summary>
        public static int AuraSortingOrder(SpriteRenderer body) => (body != null ? body.sortingOrder : 0) - 1;
        /// <summary>갈래별 오라 색(그림이 노랑·흰색이라 곱하면 그 색 쪽으로 간다).</summary>
        public static readonly Color FireTint = new Color(1f, 0.55f, 0.3f), GravityTint = new Color(0.8f, 0.5f, 1f);

        [Tooltip("효과 머티리얼(Assets/Materials/SpriteFlash — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색으로 칠하는 셰이더). 외주 효과로 바꿀 땐 이 칸만.")]
        [SerializeField] Material flashMaterial;

        /// <summary>씬 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources`에서 셰이더를 읽어 실행 중에 머티리얼을 만들었다).</summary>
        public Material FlashMaterial { get => flashMaterial; set => flashMaterial = value; }

        MageAttack mage;
        SpriteRenderer body, flashSr;
        VfxPlayer aura;
        int stage;
        float flash;

        /// <summary>차지율 k에서 몇 단계인지 — 0 = 아직, 1 = 완충.</summary>
        public static int StageAt(float k)
        {
            int s = 0;
            foreach (float t in Stages) if (k >= t) s++;
            return s;
        }

        /// <summary>갈래별 오라 색.</summary>
        public static Color AuraTint(MageBranch branch) =>
            branch == MageBranch.Explosion ? FireTint : branch == MageBranch.Gravity ? GravityTint : Color.white;

        /// <summary>지금 번쩍임 세기 0~1(테스트용).</summary>
        public float FlashAmount => flash;
        /// <summary>차지 오라가 보이는가(테스트용).</summary>
        public bool AuraVisible => aura != null && aura.gameObject.activeSelf;

        void Awake()
        {
            mage = GetComponent<MageAttack>();
            body = GetComponent<SpriteRenderer>();
        }

        // MageAttack.Update가 차지율을 올린 뒤에 읽도록 LateUpdate에서.
        void LateUpdate() => Refresh(Time.deltaTime); // 히트스톱·일시정지 땐 0 — 번쩍임도 같이 멈춘다

        /// <summary>차지 오라를 켜고 끄고, 완충이면 번쩍이고, 번쩍임을 줄여 그린다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            bool charging = mage != null && mage.enabled && mage.IsCharging;
            UpdateAura(charging);

            int now = charging ? StageAt(mage.ChargeRatio) : 0;
            if (now > stage) flash = 1f;
            else if (flash > 0f) flash = Mathf.Max(0f, flash - dt / Duration);
            stage = now;
            DrawFlash();
        }

        void UpdateAura(bool charging)
        {
            if (!charging)
            {
                if (aura != null) aura.gameObject.SetActive(false);
                return;
            }
            if (aura != null && aura.gameObject.activeSelf) return; // 차지하는 동안 계속 돈다
            var frames = VfxFrames.Get(VfxKeys.MageChargeLarge);
            if (frames.Length == 0) return; // 팩을 안 가져왔으면 그리지 않는다
            if (aura == null)
            {
                var go = new GameObject("ChargeAura", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false); // 몸 중심
                var sr = go.GetComponent<SpriteRenderer>();
                if (body != null) sr.sortingLayerID = body.sortingLayerID;
                sr.sortingOrder = AuraSortingOrder(body);
                aura = go.AddComponent<VfxPlayer>();
            }
            // 플레이어는 캐릭터 그림에 맞춰 배율이 1이 아니다(씬 0.8 등) — 오라는 월드 배율 1(도트 배율 1)이 되게 되돌린다.
            var s = transform.lossyScale;
            aura.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
            aura.gameObject.SetActive(true);
            aura.Play(frames, AuraFps, true, 0f, AuraTint(ProfileService.Current.mageBranch)); // 차지를 새로 시작할 때마다 처음부터
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

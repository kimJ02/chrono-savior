using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 반경 R 안의 Enemy 태그 대상에게 즉시 피해를 주는 범용 근접 공격.
    /// **현재 씬에서는 쓰지 않는다** — 마법사는 `MageAttack`(차지샷)을 쓴다. 나중에 근접 캐릭터
    /// (섬영 등)를 붙일 때 참고하려고 남겨둔 코드다(docs/sprints/01-combat-core.md 3번 참고).
    ///
    /// 스프린트 2에서 `Destroy()` 직접 호출을 `IDamageable.TakeDamage()`로 교체했다 —
    /// 예고한 대로 실제로 바뀐 건 호출 한 줄뿐이다.
    /// </summary>
    public class PlayerAttack : MonoBehaviour
    {
    public float range = 1.5f;
    public float cooldown = 0.4f;
    [Tooltip("기본 피해량. 원본에 이 무기에 대응하는 항목이 없어 마법사 기본값(baseAtk 10 × bow 0.9)과 맞춰뒀다.")]
    public float damage = 9f;
    public LayerMask enemyMask = ~0; // 기본값: 전체 레이어. Enemy 전용 레이어를 쓰기 전까지는 태그로 한 번 더 거른다.

    // 공격이 눈에 보이게 하는 최소한의 표시. 스프린트 1 스펙(docs/sprints/01-combat-core.md)은 "연출 없음"이었지만,
    // 아예 아무 표시가 없으면 Z를 눌렀을 때 뭐가 됐는지 알 길이 없어서(사용자 피드백) 추가.
    // 정식 이펙트가 아니라 판정 반경을 잠깐 보여주는 링 하나뿐이다.
    public float flashDuration = 0.12f;
    LineRenderer flashRing;
    float flashTimer;

    float cdTimer;

    void Awake()
    {
        flashRing = gameObject.AddComponent<LineRenderer>();
        flashRing.useWorldSpace = false;
        flashRing.loop = true;
        flashRing.widthMultiplier = 0.05f;
        flashRing.material = new Material(Shader.Find("Sprites/Default"));
        flashRing.startColor = flashRing.endColor = new Color(1f, 0.25f, 0.2f, 0.9f);
        flashRing.sortingOrder = 5;

        const int seg = 24;
        flashRing.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = i / (float)seg * Mathf.PI * 2f;
            flashRing.SetPosition(i, new Vector3(Mathf.Cos(a) * range, Mathf.Sin(a) * range, 0f));
        }
        flashRing.enabled = false;
    }

    void Update()
    {
        if (flashTimer > 0f)
        {
            flashTimer -= Time.deltaTime;
            if (flashTimer <= 0f) flashRing.enabled = false;
        }

        cdTimer -= Time.deltaTime;
        if (cdTimer > 0f) return;

        // 원본(project_test.html) KEYMAP: KeyZ = 기본 공격
        if (GameInput.AttackDown)
        {
            Attack();
            cdTimer = cooldown;
        }
    }

    void Attack()
    {
        flashRing.enabled = true;
        flashTimer = flashDuration;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, range, enemyMask);
        foreach (var col in hits)
        {
            if (!col.CompareTag("Enemy")) continue;
            // 원본은 스폰 직후(spawnInvuln>0) 모든 피해 판정을 건너뛴다(project_test.html:1659).
            // Combat 도메인은 Enemies를 직접 참조하면 안 되므로(asmdef 계층 규칙) 구체 타입
            // 대신 Core의 ISpawnProtectable 인터페이스로만 상태를 묻는다.
            var protectable = col.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) continue;

            // 치명타·±10% 난수는 적중할 때마다 새로 굴린다(원본 dealDamage와 동일).
            var target = col.GetComponent<IDamageable>();
            if (target != null && !target.IsDead)
                target.TakeDamage(DamageCalculator.Roll(damage), gameObject);
        }
    }

    // 씬 뷰에서 판정 반경을 눈으로 확인하기 위한 디버그용 표시. 게임 화면(빌드)엔 안 나온다.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, range);
    }
    }
}

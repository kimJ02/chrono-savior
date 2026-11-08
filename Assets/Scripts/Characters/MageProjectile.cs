using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Characters
{
/// <summary>
/// MageAttack이 쏘는 마법탄 하나. 원본 `projectiles` 배열의 화살 오브젝트(kind:'arrow',
/// project_test.html:1964-1975)와 그 명중/충돌 처리(`updateProjectiles`, project_test.html:3601-3693)를
/// 옮겼다.
///
/// 판정 크기: 원본은 `rectsOverlap(w.x-20*size, w.y-15*size, 40*size, 30*size, ...)` — 40×30px
/// (=0.4×0.3유닛, 차지 0이면) 직사각형이고 `size=1+chargeK*0.9(+티어5 폭발보너스)`로 차지할수록 커진다.
///
/// **지형과는 탄 종류마다 다르게 부딪힌다**(원본 :3624~:3648 — 2026-10-03 원본대로 바로잡음):
/// - 중력탄: 좌우 벽·천장·**바닥**에 닿으면 그 자리에서 터진다. **발판은 지나간다** — 원본에 발판 판정이 없다.
/// - 폭발탄: 좌우 벽·천장, 그리고 발판·바닥(원본 `groundYBelow`)에 닿으면 터진다.
/// - 갈래 없는 탄(0차): 지형과 부딪히지 않는다 — 수명이 다하거나 맵 밖으로 멀리 나가야 사라진다.
/// 예전엔 셋 다 지형 콜라이더(Ground 레이어)에 닿는 순간 터지거나 사라져서, 중력탄이 발판 위에서 터지고
/// 0차 탄이 발판에 막혔다. 판정은 콜라이더가 아니라 원본처럼 좌표(<see cref="FieldLayout.SurfaceBelow"/>)로 잰다.
/// </summary>
public class MageProjectile : MonoBehaviour
{
    // 원본 hitRect 절반 크기(20px, 15px → 0.2, 0.15유닛). sizeMul을 곱해서 실제 콜라이더 크기를 만든다.
    const float BaseHalfWidth = 0.2f;
    const float BaseHalfHeight = 0.15f;

    // ── 원본 지형·경계 수치(÷100) ──
    const float WallInset = 0.24f;            // 원본 `w.x < 24` / `w.x > mapW - 24`
    const float GroundAnchorHeight = 0.12f;   // 원본 중력탄 `w.y >= groundY - 12`
    const float SurfaceImpactHeight = 0.10f;  // 원본 폭발탄 `w.y >= gy - 10`
    const float SurfaceLookahead = 0.08f;     // 원본 `fromY = w.y - |vy|*dt - 8`
    const float RemoveMargin = 0.80f;         // 원본 `w.x < -80 || w.x > mapW + 80 || w.y > groundY + 80`
    const float RemoveAboveCeiling = 1.44f;   // 원본 `w.y < -80` — 천장(64px)보다 144px 위

    float damage;
    int pierceLeft;
    float life;
    bool infinitePierce;
    bool charged;

    // 원본 `explosive`/`gravityOrb` 분기(project_test.html:3624-3691)에 필요한 상태 전부.
    bool explosive;
    float explosionPower;
    bool gravityOrb;
    float gravityCharge;
    // 폭발/중력 중 실제로 선택된 갈래의 티어. 한 발사체는 둘 중 하나만 해당하므로(gravityOrb와
    // explosive는 상호 배타) 필드 하나로 충분하다.
    int tier;

    // 넉백 방향(원본 `kbDir: sign(w.vx)`, project_test.html:3679 — 시전자 위치가 아니라 투사체
    // 자신의 이동 방향)과, 폭발 효과가 "플레이어 기준 기본 넉백"을 계산하려고 필요한 시전자 정보.
    float hitKnockbackDirSign;
    Vector2 casterPos;
    int casterFacing;

    // 구성은 프리팹(Assets/Prefabs/Combat/MageBolt)에 있다(2026-11-05 생성 책임 2-3) — 판정 상자·리지드바디·그림 자식. 크기·속도·색은 쏠 때 넣는다.
    [SerializeField] Rigidbody2D rb;
    [SerializeField] BoxCollider2D box;
    [SerializeField] Transform visualTf;
    [SerializeField] SpriteRenderer visualRenderer;
    readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

    /// <summary>마탄 하나를 쏜다 — 쏘는 쪽(<see cref="MageAttack"/>)이 들고 있는 프리팹을 세우고 이번 발의 값을 넣는다. 프리팹이 없으면 오류.</summary>
    public static MageProjectile Spawn(MageProjectile prefab, Vector3 pos, Vector2 velocity, float damage, int pierce, float life, float sizeMul,
        Color color, bool infinitePierce, bool charged, bool explosive, float explosionPower,
        bool gravityOrb, float gravityCharge, int tier, Vector2 casterPos, int casterFacing)
    {
        if (prefab == null) { Debug.LogError("[MageProjectile] 마탄 프리팹이 없다 — MageAttack의 boltPrefab을 확인"); return null; }
        var proj = Instantiate(prefab, pos, Quaternion.identity); // RunTransient가 붙어 있어 런이 끝나면 사라진다(원본 `projectiles = []`, :4318)
        proj.name = prefab.name; // "(Clone)" 없이 — 계층 창·디버그에서 종류 이름 그대로
        proj.ShowArt(gravityOrb ? VfxKeys.MageGravityOrbSmall : explosive ? VfxKeys.MageFireProjectile : null, sizeMul,
                     gravityOrb ? new Color(0.78f, 0.42f, 1f) : color, velocity);
        proj.box.size = new Vector2(BaseHalfWidth * 2f * sizeMul, BaseHalfHeight * 2f * sizeMul);
        proj.rb.linearVelocity = velocity;
        proj.damage = damage;
        proj.pierceLeft = pierce;
        proj.life = life;
        proj.infinitePierce = infinitePierce;
        proj.charged = charged;
        proj.explosive = explosive;
        proj.explosionPower = explosionPower;
        proj.gravityOrb = gravityOrb;
        proj.gravityCharge = gravityCharge;
        proj.tier = tier;
        proj.casterPos = casterPos;
        proj.casterFacing = casterFacing;
        // 원본 `kbDir: Math.sign(w.vx)` — 위·아래로 똑바로 쏜 탄은 0이라 **옆으로 밀지 않는다**(예전엔 바라보는 쪽으로 밀었다).
        proj.hitKnockbackDirSign = Mathf.Approximately(velocity.x, 0f) ? 0f : Mathf.Sign(velocity.x);
        return proj;
    }

    /// <summary>탄 그림 재생 속도(팩 기본).</summary>
    public const float ArtFps = 15f;

    /// <summary>
    /// 탄 그림(2026-11-07 VFX 스프린트): 폭발 갈래는 `mage/fire_projectile`(Projectiles Pack 4 energy_blast red — 문서 예시의 혜성 모양, 2026-11-08),
    /// 중력 갈래는 `mage/gravity_orb_small`(dark_matter violet)을 되풀이한다. 배율은 **모든 그림 공통 도트 배율**(<see cref="DotScale"/>, 2026-11-09)
    /// — 판정 상자는 그대로다(차지할수록 판정은 커지지만 그림은 같다). 혜성 그림은 오른쪽을 향해 있어 **그림만** 날아가는 방향으로 돌린다(판정 상자는 안 돈다 — 원본도 축 정렬 사각형).
    /// 빌드 없음(0차)이거나 그림이 없으면(팩을 안 가져온 클론) 예전처럼 원 그림을 색칠한다.
    /// </summary>
    void ShowArt(string key, float sizeMul, Color fallbackColor, Vector2 velocity)
    {
        float hitWidth = BaseHalfWidth * 2f * sizeMul;
        var frames = key != null ? VfxFrames.Get(key) : System.Array.Empty<Sprite>();
        if (frames.Length == 0)
        {
            visualTf.localScale = Vector3.one * hitWidth;
            visualRenderer.color = fallbackColor;
            return;
        }
        visualTf.localScale = Vector3.one * DotScale.Value; // 모든 그림 같은 도트 크기(2026-11-09) — 판정 폭과 따로
        if (key == VfxKeys.MageFireProjectile && velocity.sqrMagnitude > 1e-6f)
            visualTf.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        visualRenderer.gameObject.AddComponent<VfxPlayer>().Play(frames, ArtFps, true, 0f, Color.white);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector2 pos = transform.position;

        if (gravityOrb && TryAnchorGravityOrb(ref pos))
        {
            MageSkillEffects.DetonateGravityOrb(pos, tier, gravityCharge, casterFacing);
            Destroy(gameObject);
            return;
        }
        if (explosive && TryImpactExplosive(ref pos, dt))
        {
            MageSkillEffects.SpawnFireExplosion(pos, tier, explosionPower, casterPos, casterFacing);
            Destroy(gameObject);
            return;
        }

        if (!infinitePierce)
        {
            life -= dt;
            if (life <= 0f)
            {
                // 원본 `!w.inf && w.t >= w.life` (project_test.html:3688) — 중력탄만 수명이 다하면 자동 폭발한다.
                if (gravityOrb) MageSkillEffects.DetonateGravityOrb(pos, tier, gravityCharge, casterFacing);
                Destroy(gameObject);
                return;
            }
        }

        // 원본 :3691 — 맵 밖으로 멀리 나가면 지운다(벽에서 안 멈추는 0차 탄과 무한 관통 탄 몫).
        if (pos.x < FieldBounds.MinX - RemoveMargin || pos.x > FieldBounds.MaxX + RemoveMargin
            || pos.y < FieldBounds.GroundY - RemoveMargin || pos.y > FieldLayout.ProjectileCeilingY + RemoveAboveCeiling)
            Destroy(gameObject);
    }

    /// <summary>원본 중력탄 고정(:3624~:3634) — 좌우 벽·천장·바닥이면 그 자리로 붙여 세우고 true. 발판은 안 본다.</summary>
    static bool TryAnchorGravityOrb(ref Vector2 pos)
    {
        bool anchor = ClampToWalls(ref pos);
        if (pos.y > FieldLayout.ProjectileCeilingY) { pos.y = FieldLayout.ProjectileCeilingY; anchor = true; }
        else if (pos.y <= FieldBounds.GroundY + GroundAnchorHeight) { pos.y = FieldBounds.GroundY + GroundAnchorHeight; anchor = true; }
        return anchor;
    }

    /// <summary>
    /// 원본 폭발탄 착탄(:3636~:3648) — 좌우 벽·천장, 그리고 "지금 높이(한 프레임 이동분 + 0.08 위까지) 이하의 가장 가까운
    /// 발판·바닥"보다 0.10 위까지 내려오면 터진다. 위로 쏜 탄도 발판 바로 밑에 오면 이 여유분 때문에 걸리고,
    /// 터지는 자리는 원본처럼 발판 위(0.10)로 맞춘다.
    /// </summary>
    bool TryImpactExplosive(ref Vector2 pos, float dt)
    {
        bool impact = ClampToWalls(ref pos);
        if (pos.y > FieldLayout.ProjectileCeilingY) { pos.y = FieldLayout.ProjectileCeilingY; impact = true; }
        float vy = rb != null ? rb.linearVelocity.y : 0f;
        float fromY = Mathf.Min(FieldLayout.ProjectileCeilingY, pos.y + Mathf.Abs(vy) * dt + SurfaceLookahead);
        float gy = FieldLayout.SurfaceBelow(pos.x, fromY);
        if (pos.y <= gy + SurfaceImpactHeight) { pos.y = gy + SurfaceImpactHeight; impact = true; }
        return impact;
    }

    /// <summary>원본 `if (w.x < 24) { w.x = 24; … } else if (w.x > mapW - 24) { … }` — 좌우 벽에 닿았으면 붙이고 true.</summary>
    static bool ClampToWalls(ref Vector2 pos)
    {
        if (pos.x < FieldBounds.MinX + WallInset) { pos.x = FieldBounds.MinX + WallInset; return true; }
        if (pos.x > FieldBounds.MaxX - WallInset) { pos.x = FieldBounds.MaxX - WallInset; return true; }
        return false;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // 지형(발판·바닥 콜라이더)은 여기서 안 본다 — 탄 종류별 규칙은 Update가 좌표로 잰다(클래스 주석 참고).
        if (!other.CompareTag("Enemy")) return;
        // 원본은 `if (e.spawnInvuln > 0) continue`로 스폰 직후 무적 대상을 hitSet에 아예 안 넣는다
        // — pierce도 안 깎이고, 무적이 풀린 뒤 다시 판정에 걸릴 수 있다. 그대로 이식.
        var protectable = other.GetComponent<ISpawnProtectable>();
        if (protectable != null && protectable.IsSpawnProtected) return;
        // 죽은 적은 건너뛴다 — 관통을 안 쓰고 중력탄도 안 터진다(원본 `if (e.dead || e.spawnInvuln > 0) continue`, 2026-11-07).
        var target = other.GetComponent<IDamageable>();
        if (target == null || target.IsDead) return;
        if (!alreadyHit.Add(other)) return;

        if (gravityOrb)
        {
            // 원본(project_test.html:3673-3677) — 중력탄은 직격 피해가 없다. 명중 즉시 폭발+중력점 생성.
            MageSkillEffects.DetonateGravityOrb(transform.position, tier, gravityCharge, casterFacing);
            Destroy(gameObject);
            return;
        }

        // 치명타·±10% 난수는 적중할 때마다 새로 굴린다(원본 dealDamage, 관통 시 각 대상 별도 판정).
        float critChance = PlayerStatCalculator.ComputeCritChance(ProfileService.Current);
        float dmg = DamageCalculator.Roll(damage, critChance, out _);
        // 원본 kbDir: sign(w.vx), kb: 140 (project_test.html:3679) — 시전자 위치가 아니라 투사체 진행 방향.
        target.TakeDamageWithKnockback(dmg, null, hitKnockbackDirSign, 1.40f);

        if (explosive)
        {
            int stacks = charged ? 2 : 1; // 원본 `const stacks = w.charged ? 2 : 1`(:3678)
            other.GetComponent<IElementAfflictable>()?.ApplyBurn(stacks);

            // 원본(project_test.html:3680) — 충전됐거나 3티어 이상이면 관통 여부와 무관하게 착탄마다 폭발.
            if (charged || tier >= 3)
                MageSkillEffects.SpawnFireExplosion(transform.position, tier, explosionPower, casterPos, casterFacing);
        }

        // 원본 `w.pierceLeft--; if (w.pierceLeft <= 0) remove = true;`(project_test.html:3683-3684).
        pierceLeft--;
        if (pierceLeft <= 0) Destroy(gameObject);
    }
}
}

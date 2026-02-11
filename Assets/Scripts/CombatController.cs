using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] AnimationManager[] animationManagers;
    [SerializeField] AudioSource audioSource;
    [SerializeField] Footsteps.FirstPersonController controller;
     [Header("Movement Slowdown")]
[SerializeField] float attackSpeedMultiplier = 0.4f;
[SerializeField] float blockSpeedMultiplier = 0.2f;

    [Header("Attack Settings")]
    [SerializeField] float attackCooldown = 0.8f;
    [SerializeField] int comboLength = 3;

    [Header("Sounds")]
    [SerializeField] AudioClip[] punchAirSounds;
    [SerializeField] AudioClip[] punchHitSounds;

    float nextAttackTime;
    float currentAttackIndex = 0f;

    void Update()
    {
        HandleBlock();
        HandleAttack();
    }

    void HandleBlock()
{
    bool blockInput = Input.GetMouseButton(1); // click derecho

    foreach (var manager in animationManagers)
        manager.SetBlock(blockInput);

    if (controller)
        controller.SetCombatSpeedMultiplier(
            blockInput ? blockSpeedMultiplier : 1f
        );
}

void HandleAttack()
{
    if (!Input.GetMouseButtonDown(0)) return;
    if (Time.time < nextAttackTime) return;
    if (IsAnyBlocking() || IsAnyAttacking()) return;

    foreach (var manager in animationManagers)
        manager.PlayAttack(currentAttackIndex);

    if (controller)
        controller.SetCombatSpeedMultiplier(attackSpeedMultiplier);

    currentAttackIndex++;

    if (currentAttackIndex >= comboLength)
        currentAttackIndex = 0f;

    nextAttackTime = Time.time + attackCooldown;
}


    bool IsAnyAttacking()
    {
        foreach (var manager in animationManagers)
            if (manager.IsAttacking)
                return true;

        return false;
    }

    bool IsAnyBlocking()
    {
        foreach (var manager in animationManagers)
            if (manager.IsBlocking)
                return true;

        return false;
    }

    // ───────── Animation Events ─────────

    public void PlayPunchAirSound()
    {
        if (punchAirSounds.Length == 0 || !audioSource) return;
        audioSource.PlayOneShot(
            punchAirSounds[Random.Range(0, punchAirSounds.Length)]
        );
    }

    public void PlayPunchHitSound()
    {
        if (punchHitSounds.Length == 0 || !audioSource) return;
        audioSource.PlayOneShot(
            punchHitSounds[Random.Range(0, punchHitSounds.Length)]
        );
    }
}

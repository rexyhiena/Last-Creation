using UnityEngine;

public class AnimationManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] Animator animator;
    [SerializeField] CombatController combatController;
    [SerializeField] Footsteps.FirstPersonController controller;

    public bool IsAttacking { get; private set; }
    public bool IsBlocking { get; private set; }

    void FixedUpdate()
    {
        HandleLocomotion();
    }

    // ───────── LOCOMOTION ─────────

    void HandleLocomotion()
    {
        if (!controller || !animator) return;

        Vector3 velocity = controller.velocity;
        velocity.y = 0f;

        animator.SetFloat("Speed", velocity.magnitude);
        animator.SetBool("Grounded", controller.isGrounded);

        if (controller.isJumping)
        {
            animator.SetTrigger("Jump");
        }
    }

    // ───────── COMBAT ─────────

    public void PlayAttack(float index)
    {
        if (IsAttacking) return;

        IsBlocking = false;
        animator.SetBool("Block", false);

        IsAttacking = true;
        animator.SetFloat("AttackIndex", index);
        animator.SetTrigger("Attack");
    }

    public void SetBlock(bool value)
    {
        if (IsAttacking) value = false;

        IsBlocking = value;
        animator.SetBool("Block", IsBlocking);
    }

    // ───────── ANIMATION EVENTS ─────────

    public void OnPunchAir()
    {
        combatController?.PlayPunchAirSound();
    }

    public void OnPunchHit()
    {
        combatController?.PlayPunchHitSound();
    }

    public void EndAttack()
    {
        IsAttacking = false;
    }
}

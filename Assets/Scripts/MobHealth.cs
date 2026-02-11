using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Sistema de vida universal para mobs con soporte completo de tipos de daño y efectos
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class MobHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;
    [SerializeField] private bool isInvulnerable = false;
    [SerializeField] private float invulnerabilityTime = 0.5f; // Tiempo de invulnerabilidad tras recibir daño
    
    [Header("Damage Resistances (0 = vulnerable, 1 = immune)")]
    [Range(0f, 1f)] public float meleeResistance = 0f;
    [Range(0f, 1f)] public float bulletResistance = 0f;
    [Range(0f, 1f)] public float explosionResistance = 0f;
    [Range(0f, 1f)] public float fireResistance = 0f;
    [Range(0f, 1f)] public float electricResistance = 0f;
    [Range(0f, 1f)] public float causticResistance = 0f;
    [Range(0f, 1f)] public float radioactiveResistance = 0f;
    
    [Header("Blood & Gore (Optional)")]
    [SerializeField] private bool enableBlood = true;
    [SerializeField] private GameObject bloodParticlePrefab;
    [SerializeField] private int bloodParticleCount = 10;
    [SerializeField] private bool enableBleeding = true;
    [SerializeField] private float bleedingDamagePerTick = 2f;
    [SerializeField] private float bleedingTickRate = 1f;
    
    [Header("Status Effects")]
    [SerializeField] private bool canBurn = true;
    [SerializeField] private bool canBeStunned = true;
    [SerializeField] private bool canBleed = true;
    [SerializeField] private GameObject fireEffectPrefab;
    [SerializeField] private GameObject electricEffectPrefab;
    
    [Header("UI")]
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private GameObject healthBarPrefab;
    [SerializeField] private Vector3 healthBarOffset = new Vector3(0, 2, 0);
    [SerializeField] private bool healthBarFollowsCamera = true;
    
    [Header("Animations (Optional)")]
    [SerializeField] private Animator animator;
    [SerializeField] private string damageAnimationTrigger = "TakeDamage";
    [SerializeField] private string deathAnimationTrigger = "Death";
    [SerializeField] private bool hasAnimations = false;
    
    [Header("AI & Death")]
    [SerializeField] private MonoBehaviour aiScript; // Referencia al script de IA
    [SerializeField] private float corpseLifetime = 5f;
    [SerializeField] private bool disableCollisionOnDeath = true;
    [SerializeField] private bool ragdollOnDeath = false;
    
    // Estado interno
    private bool isDead = false;
    private bool isBurning = false;
    private bool isStunned = false;
    private bool isBleeding = false;
    private float lastDamageTime = 0f;
    
    // UI
    private GameObject healthBarInstance;
    private Slider healthBarSlider;
    private Canvas healthBarCanvas;
    
    // Efectos activos
    private GameObject activeFireEffect;
    private GameObject activeElectricEffect;
    private Dictionary<DamageType, float> activeStatusEffects = new Dictionary<DamageType, float>();
    
    // Componentes
    private Rigidbody rb;
    private Collider[] colliders;
    
    // Eventos
    public System.Action<DamageInfo> OnDamageTaken;
    public System.Action OnDeath;
    public System.Action<float> OnHeal;
    public System.Action<DamageType> OnStatusEffectApplied;
    
    private void Awake()
    {
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody>();
        colliders = GetComponentsInChildren<Collider>();
        
        if (animator == null)
            animator = GetComponent<Animator>();
        
        if (animator != null)
            hasAnimations = true;
    }
    
    private void Start()
    {
        if (showHealthBar)
        {
            CreateHealthBar();
        }
    }
    
    private void Update()
    {
        if (isDead) return;
        
        // Actualizar barra de vida
        if (healthBarInstance != null && healthBarFollowsCamera)
        {
            healthBarInstance.transform.LookAt(Camera.main.transform);
            healthBarInstance.transform.Rotate(0, 180, 0);
        }
    }
    
    #region Health Bar
    
    private void CreateHealthBar()
    {
        if (healthBarPrefab != null)
        {
            healthBarInstance = Instantiate(healthBarPrefab, transform);
            healthBarInstance.transform.localPosition = healthBarOffset;
        }
        else
        {
            // Crear barra de vida por defecto
            GameObject barObj = new GameObject("HealthBar");
            barObj.transform.SetParent(transform);
            barObj.transform.localPosition = healthBarOffset;
            
            healthBarCanvas = barObj.AddComponent<Canvas>();
            healthBarCanvas.renderMode = RenderMode.WorldSpace;
            
            RectTransform canvasRect = healthBarCanvas.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(1, 0.2f);
            
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(barObj.transform);
            
            healthBarSlider = sliderObj.AddComponent<Slider>();
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = Vector2.zero;
            sliderRect.anchorMax = Vector2.one;
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;
            
            // Background
            GameObject bg = new GameObject("Background");
            bg.transform.SetParent(sliderObj.transform);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = Color.red;
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            
            // Fill
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(sliderObj.transform);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = Color.green;
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            
            healthBarSlider.fillRect = fillRect;
            healthBarSlider.maxValue = 1f;
            healthBarSlider.value = 1f;
            
            healthBarInstance = barObj;
        }
        
        if (healthBarSlider == null)
            healthBarSlider = healthBarInstance.GetComponentInChildren<Slider>();
        
        UpdateHealthBar();
    }
    
    private void UpdateHealthBar()
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.value = currentHealth / maxHealth;
            
            // Cambiar color según salud
            Image fillImage = healthBarSlider.fillRect.GetComponent<Image>();
            if (fillImage != null)
            {
                float healthPercent = currentHealth / maxHealth;
                if (healthPercent > 0.5f)
                    fillImage.color = Color.Lerp(Color.yellow, Color.green, (healthPercent - 0.5f) * 2f);
                else
                    fillImage.color = Color.Lerp(Color.red, Color.yellow, healthPercent * 2f);
            }
        }
    }
    
    #endregion
    
    #region Damage System
    
    /// <summary>
    /// Aplica daño al mob
    /// </summary>
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (isDead || isInvulnerable) return;
        
        // Calcular daño final con resistencias
        float resistance = GetResistance(damageInfo.type);
        float finalDamage = damageInfo.amount * (1f - resistance);
        
        // Aplicar multiplicador del tipo de daño
        finalDamage *= DamageConfig.GetDamageMultiplier(damageInfo.type);
        
        if (finalDamage <= 0f) return;
        
        // Aplicar daño
        currentHealth -= finalDamage;
        currentHealth = Mathf.Max(0f, currentHealth);
        lastDamageTime = Time.time;
        
        // Actualizar UI
        UpdateHealthBar();
        
        // Efectos visuales
        if (enableBlood && bloodParticlePrefab != null)
        {
            SpawnBloodParticles(damageInfo.position);
        }
        
        // Animación de daño
        if (hasAnimations && !string.IsNullOrEmpty(damageAnimationTrigger))
        {
            animator.SetTrigger(damageAnimationTrigger);
        }
        
        // Aplicar efectos de estado
        ApplyStatusEffect(damageInfo);
        
        // Knockback
        ApplyKnockback(damageInfo);
        
        // Eventos
        OnDamageTaken?.Invoke(damageInfo);
        
        // Invulnerabilidad temporal
        if (invulnerabilityTime > 0f)
        {
            StartCoroutine(InvulnerabilityCoroutine());
        }
        
        // Verificar muerte
        if (currentHealth <= 0f)
        {
            Die(damageInfo);
        }
    }
    
    private float GetResistance(DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Melee:       return meleeResistance;
            case DamageType.Bullet:      return bulletResistance;
            case DamageType.Explosion:   return explosionResistance;
            case DamageType.Fire:        return fireResistance;
            case DamageType.Electric:    return electricResistance;
            case DamageType.Caustic:     return causticResistance;
            case DamageType.Radioactive: return radioactiveResistance;
            default:                     return 0f;
        }
    }
    
    private void ApplyKnockback(DamageInfo damageInfo)
    {
        if (rb != null && damageInfo.direction != Vector3.zero)
        {
            float knockbackForce = 5f;
            
            // Explosiones tienen más knockback
            if (damageInfo.type == DamageType.Explosion)
                knockbackForce = 15f;
            
            rb.AddForce(damageInfo.direction.normalized * knockbackForce, ForceMode.Impulse);
        }
    }
    
    private IEnumerator InvulnerabilityCoroutine()
    {
        isInvulnerable = true;
        yield return new WaitForSeconds(invulnerabilityTime);
        isInvulnerable = false;
    }
    
    #endregion
    
    #region Status Effects
    
    private void ApplyStatusEffect(DamageInfo damageInfo)
    {
        switch (damageInfo.type)
        {
            case DamageType.Fire:
                if (canBurn && !isBurning)
                    StartCoroutine(BurningCoroutine());
                break;
                
            case DamageType.Electric:
                if (canBeStunned && !isStunned)
                    StartCoroutine(StunCoroutine());
                break;
                
            case DamageType.Caustic:
                if (canBleed && !isBleeding && enableBleeding)
                    StartCoroutine(BleedingCoroutine());
                break;
                
            case DamageType.Bullet:
            case DamageType.Melee:
                if (canBleed && !isBleeding && enableBleeding)
                    StartCoroutine(BleedingCoroutine());
                break;
        }
    }
    
    /// <summary>
    /// Corrutina de quemado (DoT)
    /// </summary>
    private IEnumerator BurningCoroutine()
    {
        isBurning = true;
        OnStatusEffectApplied?.Invoke(DamageType.Fire);
        
        // Spawn fire effect
        if (fireEffectPrefab != null)
        {
            activeFireEffect = Instantiate(fireEffectPrefab, transform);
            activeFireEffect.transform.localPosition = Vector3.zero;
        }
        
        float elapsed = 0f;
        float duration = DamageConfig.FIRE_DURATION;
        
        while (elapsed < duration && !isDead)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
            
            // Aplicar daño de fuego
            DamageInfo fireDamage = new DamageInfo(DamageType.Fire, DamageConfig.FIRE_DAMAGE_PER_TICK);
            TakeDamage(fireDamage);
        }
        
        isBurning = false;
        
        if (activeFireEffect != null)
            Destroy(activeFireEffect);
    }
    
    /// <summary>
    /// Corrutina de parálisis eléctrica
    /// </summary>
    private IEnumerator StunCoroutine()
    {
        isStunned = true;
        OnStatusEffectApplied?.Invoke(DamageType.Electric);
        
        // Desactivar IA temporalmente
        if (aiScript != null)
            aiScript.enabled = false;
        
        // Spawn electric effect
        if (electricEffectPrefab != null)
        {
            activeElectricEffect = Instantiate(electricEffectPrefab, transform);
            activeElectricEffect.transform.localPosition = Vector3.zero;
        }
        
        // Detener movimiento
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        yield return new WaitForSeconds(DamageConfig.ELECTRIC_STUN_DURATION);
        
        isStunned = false;
        
        // Reactivar IA
        if (aiScript != null && !isDead)
            aiScript.enabled = true;
        
        if (activeElectricEffect != null)
            Destroy(activeElectricEffect);
    }
    
    /// <summary>
    /// Corrutina de sangrado (DoT)
    /// </summary>
    private IEnumerator BleedingCoroutine()
    {
        isBleeding = true;
        OnStatusEffectApplied?.Invoke(DamageType.Generic);
        
        float elapsed = 0f;
        float duration = 5f; // Duración del sangrado
        
        while (elapsed < duration && !isDead)
        {
            yield return new WaitForSeconds(bleedingTickRate);
            elapsed += bleedingTickRate;
            
            // Aplicar daño de sangrado
            DamageInfo bleedDamage = new DamageInfo(DamageType.Generic, bleedingDamagePerTick);
            TakeDamage(bleedDamage);
            
            // Spawn blood particles
            if (enableBlood && bloodParticlePrefab != null)
            {
                SpawnBloodParticles(transform.position + Vector3.up * 0.5f);
            }
        }
        
        isBleeding = false;
    }
    
    #endregion
    
    #region Healing
    
    /// <summary>
    /// Cura al mob
    /// </summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        
        float oldHealth = currentHealth;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        
        float actualHealing = currentHealth - oldHealth;
        
        if (actualHealing > 0f)
        {
            UpdateHealthBar();
            OnHeal?.Invoke(actualHealing);
            
            // Mostrar efecto de curación (opcional)
            ShowHealEffect();
        }
    }
    
    /// <summary>
    /// Cura al mob un porcentaje de su vida máxima
    /// </summary>
    public void HealPercent(float percent)
    {
        Heal(maxHealth * percent);
    }
    
    /// <summary>
    /// Cura al mob completamente
    /// </summary>
    public void HealFull()
    {
        Heal(maxHealth);
    }
    
    private void ShowHealEffect()
    {
        // TODO: Implementar efecto visual de curación (partículas verdes, etc)
    }
    
    #endregion
    
    #region Death
    
    private void Die(DamageInfo killingBlow)
    {
        if (isDead) return;
        
        isDead = true;
        
        // Animación de muerte
        if (hasAnimations && !string.IsNullOrEmpty(deathAnimationTrigger))
        {
            animator.SetTrigger(deathAnimationTrigger);
        }
        
        // Desactivar IA
        if (aiScript != null)
        {
            aiScript.enabled = false;
        }
        
        // Desactivar colisiones
        if (disableCollisionOnDeath)
        {
            foreach (var col in colliders)
            {
                if (col != null)
                    col.enabled = false;
            }
        }
        
        // Ragdoll
        if (ragdollOnDeath)
        {
            EnableRagdoll();
        }
        
        // Ocultar barra de vida
        if (healthBarInstance != null)
        {
            healthBarInstance.SetActive(false);
        }
        
        // Evento de muerte
        OnDeath?.Invoke();
        
        // Destruir cadáver después de un tiempo
        if (corpseLifetime > 0f)
        {
            Destroy(gameObject, corpseLifetime);
        }
    }
    
    private void EnableRagdoll()
    {
        // Activar física en todos los rigidbodies
        Rigidbody[] allRigidbodies = GetComponentsInChildren<Rigidbody>();
        foreach (var rbody in allRigidbodies)
        {
            rbody.isKinematic = false;
            rbody.useGravity = true;
        }
        
        // Desactivar animator
        if (animator != null)
        {
            animator.enabled = false;
        }
    }
    
    #endregion
    
    #region Visual Effects
    
    private void SpawnBloodParticles(Vector3 position)
    {
        if (bloodParticlePrefab != null)
        {
            GameObject blood = Instantiate(bloodParticlePrefab, position, Quaternion.identity);
            Destroy(blood, 2f);
        }
        else
        {
            // Crear partículas de sangre por defecto
            for (int i = 0; i < bloodParticleCount; i++)
            {
                GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                particle.transform.position = position;
                particle.transform.localScale = Vector3.one * 0.1f;
                
                Renderer renderer = particle.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = new Color(0.8f, 0f, 0f); // Rojo sangre
                }
                
                Rigidbody particleRb = particle.AddComponent<Rigidbody>();
                particleRb.mass = 0.01f;
                particleRb.AddForce(Random.onUnitSphere * Random.Range(2f, 5f), ForceMode.Impulse);
                
                Destroy(particle, Random.Range(1f, 2f));
            }
        }
    }
    
    #endregion
    
    #region Public Getters
    
    public float GetCurrentHealth() => currentHealth;
    public float GetMaxHealth() => maxHealth;
    public float GetHealthPercent() => currentHealth / maxHealth;
    public bool IsDead() => isDead;
    public bool IsBurning() => isBurning;
    public bool IsStunned() => isStunned;
    public bool IsBleeding() => isBleeding;
    public bool IsInvulnerable() => isInvulnerable;
    
    #endregion
    
    #region Debug
    
    private void OnDrawGizmosSelected()
    {
        // Mostrar esfera de vida en el editor
        Gizmos.color = Color.Lerp(Color.red, Color.green, currentHealth / maxHealth);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 2, 0.5f);
    }
    
    #endregion
}

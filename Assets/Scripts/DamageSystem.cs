using UnityEngine;

/// <summary>
/// Tipos de daño disponibles en el juego
/// </summary>
public enum DamageType
{
    Generic = 0,      // Daño genérico (default)
    Melee = 1,        // Golpes, espadas, hachas
    Bullet = 2,       // Proyectiles balísticos
    Explosion = 3,    // TNT, creepers, granadas
    Fire = 4,         // Lava, antorchas, fuego
    Electric = 5,     // Rayos, cables eléctricos
    Caustic = 6,      // Ácido, veneno, corrosión
    Radioactive = 7,  // Radiación, desechos nucleares
    Sonic = 8         // Gritos del screamer o sirenas 
}

/// <summary>
/// Información sobre un evento de daño
/// </summary>
[System.Serializable]
public struct DamageInfo
{
    public DamageType type;
    public float amount;
    public Vector3 position;      // Punto de impacto
    public Vector3 direction;     // Dirección del daño
    public GameObject source;     // Quien causó el daño
    public float radius;          // Radio de efecto (para explosiones, sónico, etc)
    
    public DamageInfo(DamageType type, float amount)
    {
        this.type = type;
        this.amount = amount;
        this.position = Vector3.zero;
        this.direction = Vector3.zero;
        this.source = null;
        this.radius = 0f;
    }
    
    public DamageInfo(DamageType type, float amount, Vector3 position, Vector3 direction, float radius = 0f)
    {
        this.type = type;
        this.amount = amount;
        this.position = position;
        this.direction = direction;
        this.source = null;
        this.radius = radius;
    }
}

/// <summary>
/// Extensiones y utilidades para tipos de daño
/// </summary>
public static class DamageTypeExtensions
{
    /// <summary>
    /// Color visual asociado a cada tipo de daño
    /// </summary>
    public static Color GetColor(this DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Generic:     return Color.white;
            case DamageType.Melee:       return new Color(0.8f, 0.8f, 0.8f); // Gris claro
            case DamageType.Bullet:      return new Color(1f, 0.9f, 0.3f);   // Amarillo
            case DamageType.Explosion:   return new Color(1f, 0.5f, 0f);     // Naranja
            case DamageType.Fire:        return new Color(1f, 0.3f, 0f);     // Rojo-naranja
            case DamageType.Electric:    return new Color(0.3f, 0.8f, 1f);   // Azul eléctrico
            case DamageType.Caustic:     return new Color(0.5f, 1f, 0f);     // Verde ácido
            case DamageType.Radioactive: return new Color(0f, 1f, 0f);       // Verde radioactivo
            case DamageType.Sonic:       return new Color(0.7f, 0.3f, 1f);   // Púrpura/Vibrante
            default:                     return Color.white;
        }
    }
    
    /// <summary>
    /// Nombre legible del tipo de daño
    /// </summary>
    public static string GetDisplayName(this DamageType damageType)
    {
        switch (damageType)
        {
            case DamageType.Generic:     return "Generic";
            case DamageType.Melee:       return "Melee";
            case DamageType.Bullet:      return "Bullet";
            case DamageType.Explosion:   return "Explosion";
            case DamageType.Fire:        return "Fire";
            case DamageType.Electric:    return "Electric";
            case DamageType.Caustic:     return "Caustic";
            case DamageType.Radioactive: return "Radioactive";
            case DamageType.Sonic:       return "Sonic";
            default:                     return "Unknown";
        }
    }
    
    /// <summary>
    /// Si este tipo de daño causa efectos de área
    /// </summary>
    public static bool IsAreaOfEffect(this DamageType damageType)
    {
        return damageType == DamageType.Explosion || 
               damageType == DamageType.Fire || 
               damageType == DamageType.Radioactive ||
               damageType == DamageType.Electric ||
               damageType == DamageType.Sonic;
    }
    
    /// <summary>
    /// Si este tipo de daño puede destruir bloques
    /// </summary>
    public static bool CanDestroyBlocks(this DamageType damageType)
    {
        return damageType == DamageType.Explosion || 
               damageType == DamageType.Melee ||
               damageType == DamageType.Sonic; // El daño sónico puede romper cristales o estructuras débiles
    }
    
    /// <summary>
    /// Si este tipo de daño causa efectos de estado (DoT, etc)
    /// </summary>
    public static bool CausesStatusEffect(this DamageType damageType)
    {
        return damageType == DamageType.Fire || 
               damageType == DamageType.Caustic || 
               damageType == DamageType.Radioactive ||
               damageType == DamageType.Sonic; // Sónico puede causar desorientación o aturdimiento
    }
}

/// <summary>
/// Constantes de configuración de daño
/// </summary>
public static class DamageConfig
{
    // Multiplicadores de daño por tipo
    public const float MELEE_MULTIPLIER = 1.0f;
    public const float BULLET_MULTIPLIER = 0.8f;
    public const float EXPLOSION_MULTIPLIER = 2.0f;
    public const float FIRE_MULTIPLIER = 0.5f;
    public const float ELECTRIC_MULTIPLIER = 1.5f;
    public const float CAUSTIC_MULTIPLIER = 0.7f;
    public const float RADIOACTIVE_MULTIPLIER = 0.3f;
    public const float SONIC_MULTIPLIER = 1.2f;
    
    // Duración de efectos de estado (segundos)
    public const float FIRE_DURATION = 5f;
    public const float CAUSTIC_DURATION = 8f;
    public const float RADIOACTIVE_DURATION = 10f;
    public const float ELECTRIC_STUN_DURATION = 1f;
    public const float SONIC_CONFUSION_DURATION = 3f;
    
    // Daño por tick (DoT)
    public const float FIRE_DAMAGE_PER_TICK = 1f;
    public const float CAUSTIC_DAMAGE_PER_TICK = 0.5f;
    public const float RADIOACTIVE_DAMAGE_PER_TICK = 0.3f;
    
    // Radios de efecto
    public const float EXPLOSION_BASE_RADIUS = 5f;
    public const float ELECTRIC_CHAIN_RADIUS = 3f;
    public const float RADIOACTIVE_RADIUS = 4f;
    public const float SONIC_BASE_RADIUS = 6f;
    
    /// <summary>
    /// Obtiene el multiplicador de daño según el tipo
    /// </summary>
    public static float GetDamageMultiplier(DamageType type)
    {
        switch (type)
        {
            case DamageType.Melee:       return MELEE_MULTIPLIER;
            case DamageType.Bullet:      return BULLET_MULTIPLIER;
            case DamageType.Explosion:   return EXPLOSION_MULTIPLIER;
            case DamageType.Fire:        return FIRE_MULTIPLIER;
            case DamageType.Electric:    return ELECTRIC_MULTIPLIER;
            case DamageType.Caustic:     return CAUSTIC_MULTIPLIER;
            case DamageType.Radioactive: return RADIOACTIVE_MULTIPLIER;
            case DamageType.Sonic:       return SONIC_MULTIPLIER;
            default:                     return 1.0f;
        }
    }
}

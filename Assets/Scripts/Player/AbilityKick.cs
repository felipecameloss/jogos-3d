using UnityEngine;

/// <summary>
/// Dá o tranco do modelo de inseto (InsectPlayerVisual.Kick) sempre que uma habilidade
/// é usada. Ex.: a Formiga reaproveita a agulha como mordida e avança a cabeça.
/// </summary>
public class AbilityKick : MonoBehaviour
{
    PlayerAbility ability;
    InsectPlayerVisual visual;
    float distance, pitch;
    int lastUses;

    public void Init(PlayerAbility watched, InsectPlayerVisual modelVisual, float kickDistance, float kickPitch)
    {
        ability = watched;
        visual = modelVisual;
        distance = kickDistance;
        pitch = kickPitch;
        lastUses = watched != null ? watched.Uses : 0;
    }

    void Update()
    {
        if (ability == null || visual == null || ability.Uses == lastUses) return;
        lastUses = ability.Uses;
        visual.Kick(distance, pitch);
    }
}

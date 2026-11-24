using UnityEngine;

public enum PlayerCharacterId
{
    /// <summary>O personagem original: estilingue, agulha, dash e formigas aliadas.</summary>
    Explorer,
    /// <summary>Rápida e frágil, comanda formigas aliadas.</summary>
    Ant,
    /// <summary>Tanque de corpo a corpo, com kit próprio.</summary>
    Beetle,
}

/// <summary>Dados de um personagem jogável: textos da tela de seleção e stats base.</summary>
public sealed class PlayerCharacterInfo
{
    public PlayerCharacterId Id;
    public string DisplayName;
    public string Tagline;
    /// <summary>Linhas curtas com o kit (tela de seleção).</summary>
    public string[] Kit;
    public Color Color;
    /// <summary>Nome do modelo em Resources (Assets/Art/Models/Characters/Resources/, FBX do Blender). Null = mantém o modelo da cena.</summary>
    public string ModelResource;
    /// <summary>Comprimento do modelo no jogo, em metros (a escala sai disso).</summary>
    public float ModelLength;

    // Stats base (os itens multiplicam por cima)
    public float MaxHealth = 100f;
    public float Armor;
    public float Damage = 12f;
    public float AttackSpeed = 1f;
    public float WalkSpeed = 8f;
    public float SprintSpeed = 13.5f;
    /// <summary>Altura do estilingue/ponto de onde saem os ataques, em metros acima dos pés.</summary>
    public float MuzzleHeight = 1.45f;
}

/// <summary>
/// Personagens jogáveis e o escolhido para a run. Só o menu muda a escolha (Select):
/// abrir a cena Main direto no Editor (e os testes) joga com o Explorador.
/// A última escolha do menu fica no PlayerPrefs para vir marcada da próxima vez.
/// </summary>
public static class PlayerCharacters
{
    const string PrefKey = "run.character";

    public static readonly PlayerCharacterInfo Explorer = new()
    {
        Id = PlayerCharacterId.Explorer,
        DisplayName = "Explorador",
        Tagline = "Equilibrado, ataca de longe",
        Kit = new[]
        {
            "Vida 100 · velocidade normal",
            "M1 Estilingue de espinhos",
            "M2 Agulha · Shift Dash",
            "R Formigas aliadas",
        },
        Color = new Color(0.62f, 0.8f, 0.35f),
        ModelResource = null,
    };

    public static readonly PlayerCharacterInfo Ant = new()
    {
        Id = PlayerCharacterId.Ant,
        DisplayName = "Formiga",
        Tagline = "Rápida e frágil, comanda a colônia",
        Kit = new[]
        {
            "Vida 70 · 20% mais rápida",
            "M1 Estilingue · M2 Mordida",
            "Shift Dash (recarga curta)",
            "R Chama 5 formigas aliadas",
            "Passiva: a cada 5 abates, uma formiga se junta",
        },
        Color = new Color(0.25f, 0.75f, 0.95f),
        ModelResource = "PlayerAnt",
        ModelLength = 1.7f,
        MaxHealth = 70f,
        Damage = 11f,
        AttackSpeed = 1.1f,
        WalkSpeed = 9.6f,
        SprintSpeed = 16.2f,
        MuzzleHeight = 0.85f,
    };

    public static readonly PlayerCharacterInfo Beetle = new()
    {
        Id = PlayerCharacterId.Beetle,
        DisplayName = "Besouro",
        Tagline = "Tanque de corpo a corpo",
        Kit = new[]
        {
            "Vida 180 · armadura 25 · mais lento",
            "M1 Chifrada (corpo a corpo, 3º golpe forte)",
            "M2 Investida · Shift Carapaça",
            "R Terremoto (área + lentidão)",
        },
        Color = new Color(0.95f, 0.72f, 0.2f),
        ModelResource = "PlayerBeetle",
        ModelLength = 1.9f,
        MaxHealth = 180f,
        Armor = 25f,
        Damage = 14f,
        AttackSpeed = 1f,
        WalkSpeed = 6.8f,
        SprintSpeed = 10.8f,
        MuzzleHeight = 0.9f,
    };

    public static readonly PlayerCharacterInfo[] All = { Explorer, Ant, Beetle };

    /// <summary>Personagem da run atual.</summary>
    public static PlayerCharacterId Selected { get; private set; } = PlayerCharacterId.Explorer;
    public static PlayerCharacterInfo SelectedInfo => Get(Selected);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Selected = PlayerCharacterId.Explorer;

    public static PlayerCharacterInfo Get(PlayerCharacterId id)
    {
        foreach (var c in All) if (c.Id == id) return c;
        return Explorer;
    }

    /// <summary>Escolhe o personagem da próxima run (menu) e lembra a escolha.</summary>
    public static void Select(PlayerCharacterId id)
    {
        Selected = id;
        PlayerPrefs.SetInt(PrefKey, (int)id);
        PlayerPrefs.Save();
    }

    /// <summary>Última escolha feita no menu (para vir marcada).</summary>
    public static PlayerCharacterId LastChoice()
    {
        int v = PlayerPrefs.GetInt(PrefKey, 0);
        return System.Enum.IsDefined(typeof(PlayerCharacterId), v) ? (PlayerCharacterId)v : PlayerCharacterId.Explorer;
    }
}

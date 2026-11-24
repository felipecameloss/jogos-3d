using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Diário de descobertas: itens que o player já pegou e inimigos que já viu, entre sessões.
/// Salvo como JSON no PlayerPrefs (chave <see cref="PrefsKey"/>). Ids: nome do asset do item
/// (GotinhaDeMel) e nome da classe do inimigo (WorkerAnt). O que aparece na tela (nomes,
/// ícones, ordem) vem do <see cref="DiscoveryCatalog"/>.
/// </summary>
public static class DiscoveryJournal
{
    public const string PrefsKey = "journal.v1";

    [Serializable]
    class SaveData
    {
        public List<string> items = new();
        public List<string> enemies = new();
    }

    static readonly HashSet<string> items = new(StringComparer.Ordinal);
    static readonly HashSet<string> enemies = new(StringComparer.Ordinal);
    static bool loaded;

    /// <summary>Algo foi descoberto (ou o diário foi recarregado/apagado).</summary>
    public static event Action Changed;

    // O projeto entra no Play Mode sem recarregar o domínio: relê o salvo e solta quem escutava antes
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        loaded = false;
        Changed = null;
    }

    public static int ItemCount { get { EnsureLoaded(); return items.Count; } }
    public static int EnemyCount { get { EnsureLoaded(); return enemies.Count; } }

    /// <summary>Id do item no diário: nome do asset (sem o "(Clone)" de cópias feitas em código).</summary>
    public static string ItemId(ItemData item) => item == null ? null : item.name.Replace("(Clone)", "").Trim();

    public static bool IsItemDiscovered(ItemData item) => Contains(items, ItemId(item));
    public static bool IsEnemyDiscovered(string enemyId) => Contains(enemies, enemyId);

    /// <summary>Registra o item. Devolve true se era novo.</summary>
    public static bool DiscoverItem(ItemData item) => Add(items, ItemId(item));

    /// <summary>Registra o inimigo (id = nome da classe). Devolve true se era novo.</summary>
    public static bool DiscoverEnemy(string enemyId) => Add(enemies, enemyId);

    /// <summary>Relê do PlayerPrefs (salvo inválido vira diário vazio, sem travar o jogo).</summary>
    public static void Load()
    {
        items.Clear();
        enemies.Clear();
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var data = JsonUtility.FromJson<SaveData>(json);
                if (data?.items != null) items.UnionWith(data.items);
                if (data?.enemies != null) enemies.UnionWith(data.enemies);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DiscoveryJournal] diário salvo inválido, começando do zero: {e.Message}");
            }
        }
        items.Remove(null); items.Remove("");
        enemies.Remove(null); enemies.Remove("");
        loaded = true;
        Changed?.Invoke();
    }

    /// <summary>Apaga tudo (testes e depuração; não há botão no jogo).</summary>
    public static void Clear()
    {
        EnsureLoaded();
        items.Clear();
        enemies.Clear();
        Save();
        Changed?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (!loaded) Load();
    }

    static bool Contains(HashSet<string> set, string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureLoaded();
        return set.Contains(id);
    }

    static bool Add(HashSet<string> set, string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        EnsureLoaded();
        if (!set.Add(id)) return false;
        Save();
        Changed?.Invoke();
        return true;
    }

    static void Save()
    {
        var data = new SaveData { items = new List<string>(items), enemies = new List<string>(enemies) };
        data.items.Sort(StringComparer.Ordinal);
        data.enemies.Sort(StringComparer.Ordinal);
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}

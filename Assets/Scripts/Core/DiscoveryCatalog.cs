using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// O que o Diário de descobertas mostra, na ordem da tela: os itens do banco de itens e os
/// inimigos listados aqui (nome, descrição, retrato). Crie em Assets > Create > Jogo > Catálogo do diário.
/// Inimigo novo: adicione uma entrada com o nome da classe em `id` e o retrato de
/// Assets/Art/Icons/Enemies (Tools/Blender/build_enemy_icons.py).
/// </summary>
[CreateAssetMenu(menuName = "Jogo/Catálogo do diário", fileName = "DiscoveryCatalog")]
public class DiscoveryCatalog : ScriptableObject
{
    [Serializable]
    public class EnemyEntry
    {
        [Tooltip("Nome da classe do inimigo (WorkerAnt, Spider...): é o id salvo no diário.")]
        public string id;
        public string displayName;
        [TextArea(1, 3)] public string description;
        public Sprite icon;
        public bool isBoss;
    }

    [SerializeField] ItemDatabase itemDatabase;
    [SerializeField] List<EnemyEntry> enemies = new();

    public IReadOnlyList<ItemData> Items => itemDatabase != null ? itemDatabase.Items : Array.Empty<ItemData>();
    public IReadOnlyList<EnemyEntry> Enemies => enemies;
}

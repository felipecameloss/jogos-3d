# jogos-3d

Jogo roguelite de ação em terceira pessoa desenvolvido em **Unity `6000.6.5f1`**, ambientado em um quintal em escala miniaturizada: explore o jardim, colete migalhas, abra baús e santuários, enfrente enxames de insetos e elites, derrote o chefe louva-a-deus e alcance o cano de saída para avançar de fase.

---

## Stack e Pacotes Principais

- **Engine**: Unity `6000.6.5f1`
- **Renderização**: Universal Render Pipeline (`com.unity.render-pipelines.universal` `17.6.0`)
- **Câmera**: Cinemachine (`com.unity.cinemachine` `6.6.0`)
- **Controles**: Input System (`com.unity.inputsystem` `1.20.0`)
- **Navegação**: AI Navigation (`com.unity.ai.navigation` `2.0.15`)
- **Modelos 3D**: glTFast (`com.unity.cloud.gltfast` `6.21.0`)
- **UI**: UI Toolkit (`MainMenu`, `Hud`, `PauseMenu`, `SettingsScreen`, `DiscoveryJournalScreen`, `VictoryScreen`, `EndRunScreen`)

---

## Controles

Configurados em [`Assets/InputSystem_Actions.inputactions`](Assets/InputSystem_Actions.inputactions) e [`PlayerMovement.cs`](Assets/Scripts/Player/PlayerMovement.cs):

| Ação | Tecla / Botão |
| :--- | :--- |
| Mover (corre por padrão) | `W` `A` `S` `D` |
| Modo foco tático (andar devagar, zoom, mira e tiro mais rápido/forte) | `Left Ctrl` |
| Pular | `Space` |
| Dash | `Left Shift` |
| Tiro principal (Estilingue de Espinhos) | `M1` (Botão esquerdo do mouse) |
| Agulha (`NeedleThrust`) | `M2` (Botão direito do mouse) |
| Invocar Formigas Aliadas (`SummonAllies`) | `R` |
| Usar Equipamento Ativo (`EquipmentSlot`) | `Q` |
| Trocar ombro da câmera (`DynamicCameraFeel`) | `V` |
| Zoom da câmera (`DynamicCameraFeel`) | `Scroll do mouse` |
| Interagir (baús, santuários, equipamentos, cano de saída) | `E` |
| Pausar | `Esc` |

---

## Sistemas do Jogo

- **Loop de Run e Dificuldade**: [`RunManager.cs`](Assets/Scripts/Core/RunManager.cs) mantém tempo, kills, fase atual e transição entre fases (levando itens, equipamento, cooldown restante, migalhas e proporção de vida); [`DifficultyManager.cs`](Assets/Scripts/Core/DifficultyManager.cs) escala a dificuldade ao longo do tempo; [`DayNightCycle.cs`](Assets/Scripts/Core/DayNightCycle.cs) alterna iluminação dia/pôr do sol/noite (`AmbientMode.Trilight`).
- **Inimigos e Chefe**:
  - Comuns: Formiga Operária (`WorkerAnt`), Formiga Soldado (`SoldierAnt`), Pulgão (`Aphid`), Besouro-Bombardeiro (`BombardierBeetle`, com projétil `AcidBlob` e poça `AcidPool`), Cupim (`Termite`) e Aranha (`Spider`: pula no player com aviso de círculo vermelho e dano ao pousar; cospe teia `WebShot` que vira `WebPatch` no chão e prende o player por um instante via `PlayerMovement.Root`, sem andar/pular/dash).
  - Variantes Elite: [`EnemyElite.cs`](Assets/Scripts/Enemies/EnemyElite.cs) (Gigante e Flamejante, com bônus de velocidade e recompensa ampliada de migalhas).
  - Chefe: Louva-a-Deus (`MantisBoss`, spawnado por [`BossSpawner.cs`](Assets/Scripts/Enemies/BossSpawner.cs) no evento do cano de saída `ExitPipe`).
- **Itens, Equipamentos, Economia e Santuários**:
  - Moeda (`Crumb` / `PlayerWallet` / `CrumbDropper`), baús (`ChestSmall`, `ChestLarge`) e santuários (`Shrine`: **Tampinha da sorte** e **Santuário de sangue**, distribuídos por `ShrineSpawner`).
  - Raridades em [`ItemRarity.cs`](Assets/Scripts/Items/ItemRarity.cs): `Comum`, `Incomum`, `Lendario`, `Amaldicoado` e `Equipamento`, gerenciadas por [`ItemDatabase.cs`](Assets/Scripts/Items/ItemDatabase.cs) com efeitos passivos em `Assets/Scripts/Items/Effects/` (incluindo itens de movimento como Perna de Gafanhoto e Asa de Mosca, e itens amaldiçoados como Café Derramado e Cola Branca). Item ao matar `MushroomOnKillItem` / [`EsporoDeCogumelo.asset`](Assets/ScriptableObjects/Items/EsporoDeCogumelo.asset) (Incomum): 20% de chance por stack de brotar um cogumelo ([`HealingMushroom.cs`](Assets/Scripts/Items/HealingMushroom.cs), malha low-poly feita em código) onde o inimigo morreu; ele cura 3% da vida máxima por segundo (+1,5% por stack extra) quem ficar dentro do círculo rosa de 3,5 m (+0,5 m por stack extra) por 6s, com no máximo 3 cogumelos ao mesmo tempo (o mais velho murcha). Itens ao acertar do issue #18 (ambos Incomum): `ChainLightningOnHitItem` / [`FioEletrico.asset`](Assets/ScriptableObjects/Items/FioEletrico.asset) (*Fio elétrico desencapado*): 15% de chance por stack de um raio (`ItemVfx.Lightning`) que acerta o alvo e pula para até 3 inimigos próximos (+1 por stack extra, 10 m por pulo), 80% do dano do player em cada; `PollenOnHitItem` / [`Polen.asset`](Assets/ScriptableObjects/Items/Polen.asset): 12% de chance por stack de soltar uma nuvem de pólen ([`PollenCloud.cs`](Assets/Scripts/Items/PollenCloud.cs), 3,5 m, 2,5s, máx. 4) que deixa confuso quem está dentro por 2s (+0,5s por stack extra): `StatusEffects.ApplyConfusion` faz o inimigo cancelar o ataque, andar sem rumo e ficar amarelado (`EnemyBase.IsConfused`); chefes são imunes.
  - Ícones dos itens em `Assets/Art/Icons/` (256x256, fundo transparente), gerados por `Tools/Blender/build_item_icons.py` (folha de contato em `Tools/Blender/previews/ItemIcons.png`), importados como Sprite por [`IconImportSettings.cs`](Assets/Editor/IconImportSettings.cs) e ligados em `ItemData.icon` pelo menu `Jogo/Art/Ligar ícones nos itens` ([`AssignItemIcons.cs`](Assets/Editor/AssignItemIcons.cs)); a HUD mostra o ícone no lugar da sigla.
  - Equipamentos ativos em `Assets/Scripts/Items/Equipment/` (`EquipmentData`, `EquipmentSlot`; `MagnifierEquipment` / [`Lupa.asset`](Assets/ScriptableObjects/Items/Lupa.asset): raio de sol em linha; `MintEquipment` / [`PastilhaDeMenta.asset`](Assets/ScriptableObjects/Items/PastilhaDeMenta.asset): cura 45% da vida na hora, recarga 45s, não gasta com vida cheia).
- **Diário de descobertas** (menu principal > *Diário de descobertas*): [`DiscoveryJournal.cs`](Assets/Scripts/Core/DiscoveryJournal.cs) registra os itens já pegos (`Inventory.Add`) e os inimigos já vistos (a até `EnemyBase.DiscoverDistance` = 30 m do player, ou mortos por ele de longe) e salva como JSON no PlayerPrefs (chave `journal.v1`; ids = nome do asset do item e nome da classe do inimigo). A tela [`DiscoveryJournalScreen.cs`](Assets/Scripts/UI/DiscoveryJournalScreen.cs) (`Assets/UI/DiscoveryJournal.uxml`) mostra em cards o que está no [`DiscoveryCatalog.asset`](Assets/ScriptableObjects/DiscoveryCatalog.asset) (itens do `ItemDatabase` + inimigos com nome, descrição e retrato): descoberto com ícone, nome e descrição; ainda oculto como silhueta preta e "???". Retratos dos inimigos em `Assets/Art/Icons/Enemies/`, gerados por `Tools/Blender/build_enemy_icons.py` (folha em `Tools/Blender/previews/EnemyIcons.png`). Inimigo novo: entrada no catálogo com o nome da classe em `id`.
- **Câmera, Ambiente, Perigos e Áudio**:
  - Câmera dinâmica em 3ª pessoa ([`DynamicCameraFeel.cs`](Assets/Scripts/Feel/DynamicCameraFeel.cs)): FOV dinâmico, *Dutch tilt*, ombro alternável, zoom por scroll, modo foco tático com `Ctrl` (zoom over-the-shoulder + retículo de precisão e vinheta no `Hud`), enquadramento no chefe e órbita ao fim da run.
  - Ambiente & Arte: [`BackyardAtmosphere.cs`](Assets/Scripts/Feel/BackyardAtmosphere.cs) (chão procedural com normal map, horizonte além da cerca, vegetação, poças animadas, nuvens/estrelas, pólen/vaga-lumes e DoF), modelos 3D `.glb` em `Assets/Art/Models/` gerados via Blender em `Tools/Blender/` e vinculados no Editor por [`ArtMeshBinder.cs`](Assets/Editor/ArtMeshBinder.cs) (cupim, formiga aliada, santuários, pickups, vaso, tijolo, cerca e lata em pé).
  - Props do quintal (`Tools/Blender/build_props.py`, FBX em `Assets/Art/Models/Props/`): pedras, folha caída, talo de grama, lata amassada e tampinhas. Ligados por [`PropsBinder.cs`](Assets/Editor/PropsBinder.cs) (menu `Jogo/Art/Aplicar props do quintal`): os baús viram tampinhas (`ChestSmall` vermelha, `ChestLarge` dourada; `Chest.lid` = peça `Lid`), e na cena `Main` rochas, latas deitadas, rampas/pontes de folha, pedrinhas e grama da decoração trocam só a malha (versões normalizadas em `Props/Normalized/`; colisores e NavMesh intactos). O `BackyardAtmosphere` espalha folhas caídas e pedras sem colisão (`fallenLeafPrefab`, `rockDecorPrefabs`).
  - Insetos low-poly (direção de arte no [`CLAUDE.md`](CLAUDE.md)): formiga operária, formiga-soldado, pulgão, besouro-bombardeiro, louva-a-deus e aranha usam FBX gerados por `Tools/Blender/build_*.py` (base em [`ant_common.py`](Tools/Blender/ant_common.py)). Ficam em `Assets/Art/Models/Characters/`, em peças separadas com pivô na articulação, ligados aos prefabs por [`InsectModelBinder.cs`](Assets/Editor/InsectModelBinder.cs) (menu `Jogo/Art/Aplicar modelos FBX dos insetos`) e animados por código por [`ProceduralInsectAnimator.cs`](Assets/Scripts/Enemies/ProceduralInsectAnimator.cs) (pernas em tripé pela velocidade do NavMeshAgent, bob do corpo, antenas, abdômen e squash + flash branco ao levar dano; funciona com qualquer modelo na convenção de nomes do `CLAUDE.md`). Comparação dos modelos: `Tools/Blender/previews/Lineup.png` (`render_lineup.py`).
  - Perigos e feedback: pisão humano (`HumanStomp` / `StompDirector`), sensação de combate (`GameFeel`, `HitFlash`, `HitStop`, `DamageNumbers`) e áudio procedural para música dia/noite e SFX (`GameAudio`).

---

## Estrutura do Projeto

```text
Assets/
├── Art/                 # Materiais, modelos 3D (.glb, .fbx dos insetos e dos props) e texturas
├── Editor/              # ArtMeshBinder.cs, InsectModelBinder.cs, PropsBinder.cs, ícones (IconImportSettings.cs, AssignItemIcons.cs), BuildWindows.cs e testes (DiscoveryJournalSmokeTest.cs, EquipmentSmokeTest.cs, InsectModelsSmokeTest.cs, ItemIconsSmokeTest.cs, LightningPollenSmokeTest.cs, MushroomSmokeTest.cs, SpiderSmokeTest.cs)
├── Prefabs/             # Inimigos, itens/baús/santuários, player, projéteis e hazards
├── Scenes/              # MainMenu.unity e Main.unity (além do NavMesh-Backyard)
├── ScriptableObjects/   # Itens, Lupa.asset, ItemDatabase.asset e DiscoveryCatalog.asset (diário)
├── Scripts/
│   ├── Audio/           # GameAudio (música dinâmica dia/noite e SFX)
│   ├── Combat/          # Vida, dano, projéteis, status effects e estilingue
│   ├── Core/            # RunManager, DifficultyManager, DayNightCycle, Settings, Pool, DiscoveryJournal/DiscoveryCatalog
│   ├── Enemies/         # Base, spawners, elites, inimigos comuns e MantisBoss
│   ├── Feel/            # DynamicCameraFeel, BackyardAtmosphere, HitFlash, HitStop, DamageNumbers, GameFeel
│   ├── Hazards/         # HumanStomp e StompDirector
│   ├── Items/           # Inventário, baús, santuários, migalhas, carteira, Effects/ e Equipment/
│   ├── Level/           # ExitPipe
│   ├── Player/          # Movimento, animação e Abilities/
│   └── UI/              # HUD, menus de pausa, configurações, diário de descobertas, vitória e fim de run
└── UI/                  # UXML/USS de HUD e menus (UI Toolkit)
Tools/
└── Blender/             # Scripts procedurais (.py) que geram os modelos .glb e os .fbx dos insetos, ícones dos itens e retratos dos inimigos (+ previews/)
```

---

## Como Abrir e Gerar Build

1. **Pré-requisitos**:
   - **Unity `6000.6.5f1`**
   - **Git LFS** habilitado antes de clonar/puxar (`.gitattributes` rastreia modelos `.glb`/`.fbx`, imagens e áudios):
     ```bash
     git lfs install
     git lfs pull
     ```
2. **Cenas no Build**:
   - `Assets/Scenes/MainMenu.unity`
   - `Assets/Scenes/Main.unity`
3. **Build para Windows**:
   - Pelo Editor do Unity: menu **Jogo > Build Windows** (`Builds/Windows/jogos-3d.exe`).
   - Via CLI (`Unity.exe`):
     ```bash
     Unity.exe -batchmode -quit -projectPath . -executeMethod BuildWindows.Build
     ```

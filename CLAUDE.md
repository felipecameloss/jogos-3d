# Jogo 3D roguelike — insetos no quintal (Unity)

## Direção de arte
- Low-poly estilizado, cores chapadas e saturadas, sem texturas (só materiais de cor)
- Silhuetas claras e exageradas: cabeça e olhos grandes, visual meio fofo
- Cada inimigo com uma cor dominante: formiga operária vermelha, formiga-soldado vermelho-escuro,
  besouro-bombardeiro verde metálico, aranha roxa, pulgão verde-claro, louva-a-deus verde-limão
- Máximo de ~1500 triângulos por personagem

## Regras dos modelos (Blender)
- Gerar via scripts Python em `Tools/Blender/`, rodando `blender --background --python <script>`
- Peças separadas e nomeadas como objetos filhos (Body, Head, Leg_L1, Leg_R1...) com pivô
  na articulação, porque a animação é feita por código no Unity (sem rig)
- Escala: 1 unidade = 1 metro; formiga operária ~1m de comprimento
- Frente do modelo apontando para +Y no Blender, para chegar como +Z no Unity
- Exportar FBX para `Assets/Art/Models/` usando `to_unity_space()` / `export_fbx()` de
  `Tools/Blender/ant_common.py` — NÃO usar "Apply Transform" (`bake_space_transform`): ele quebra
  com hierarquia (gira as peças filhas e erra posições). A conversão manual faz a raiz e todas as
  peças chegarem no Unity sem rotação/escala, com eixos do Unity (X direita, Y cima, Z frente)
- Insetos usam `Tools/Blender/ant_common.py` (`build_ant(cfg)`): cada modelo é um script curto
  que só muda o dicionário de configuração (cores, proporções, mandíbulas...)
- Sempre renderizar um preview PNG (3/4 de frente) em `Tools/Blender/previews/` e conferir
  visualmente antes de dar por pronto

## Unity
- Scripts em `Assets/Scripts/`, itens como ScriptableObjects, object pooling para inimigos e projéteis

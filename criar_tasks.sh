#!/usr/bin/env bash
# Cria labels, milestone e issues do jogo 3D, e adiciona tudo ao kanban (GitHub Projects).
#
# Uso:
#   gh auth refresh -s project            # uma vez, para dar permissão de Projects ao gh
#   ./criar_tasks.sh <NUMERO_DO_PROJECT>  # ex.: ./criar_tasks.sh 1
#
# O número do project aparece na URL: github.com/users/felipecameloss/projects/<NUMERO>
# Sem número, o script só cria as issues (sem adicionar ao kanban).

set -euo pipefail

REPO="felipecameloss/jogos-3d"
OWNER="felipecameloss"
PROJECT="${1:-}"
MILESTONE="Entrega 25/11"

echo "==> Labels"
label() { gh api "repos/$REPO/labels" -f name="$1" -f color="$2" -f description="$3" >/dev/null 2>&1 \
  || gh api -X PATCH "repos/$REPO/labels/$1" -f color="$2" -f description="$3" >/dev/null; }
label "mvp"          "d73a4a" "Essencial para a entrega"
label "extra"        "a2eeef" "Se der tempo"
label "core"         "5319e7" "Base: player, câmera, estrutura"
label "combate"      "e99695" "Habilidades, dano, hit"
label "inimigos"     "fbca04" "IA, spawn, chefe"
label "itens"        "0e8a16" "Itens, moeda, baús"
label "level-design" "c5def5" "Mapa, perigos, ambiente"
label "ui"           "1d76db" "Interface e telas"
label "polish"       "f9d0c4" "Juice, som, efeitos"

echo "==> Milestone"
gh api "repos/$REPO/milestones" -f title="$MILESTONE" -f due_on="2026-11-25T23:59:00Z" >/dev/null 2>&1 \
  || echo "    (milestone já existe)"
MS_NUM=$(gh api "repos/$REPO/milestones" --jq ".[] | select(.title==\"$MILESTONE\") | .number")

# Títulos já existentes (para poder rodar o script de novo sem duplicar)
EXISTING=$(gh issue list -R "$REPO" --state all --limit 500 --json title --jq '.[].title')

issue() {
  local title="$1" labels="$2" body="$3"
  local url
  local args=() l
  if grep -qxF "$title" <<< "$EXISTING"; then
    echo "    (já existe) $title"
    return
  fi
  IFS=',' read -ra ls <<< "$labels"
  for l in "${ls[@]}"; do args+=(-f "labels[]=$l"); done
  url=$(gh api "repos/$REPO/issues" -f title="$title" -f body="$body" -F milestone="$MS_NUM" "${args[@]}" --jq .html_url)
  echo "    $url"
  if [[ -n "$PROJECT" ]]; then
    # o board pode ter auto-add ligado; nesse caso o item já está lá
    gh project item-add "$PROJECT" --owner "$OWNER" --url "$url" >/dev/null 2>&1 || true
  fi
}

echo "==> Issues"

# ---------- Dias 1–2: base ----------
issue "Setup do projeto Unity" "mvp,core" \
"- [ ] Criar projeto 3D (URP) e subir no repo
- [ ] .gitignore de Unity + Git LFS para assets grandes
- [ ] Estrutura de pastas (Scripts, Prefabs, ScriptableObjects, Scenes, Art, Audio)
- [ ] Instalar Cinemachine, Input System e AI Navigation"

issue "Movimento do jogador em terceira pessoa" "mvp,core" \
"- [ ] CharacterController: andar, correr, pular
- [ ] Gravidade e detecção de chão
- [ ] Câmera Cinemachine orbitando o player
- [ ] Player gira para onde a câmera mira"

issue "Sistema de vida e dano" "mvp,combate" \
"- [ ] Componente Health reutilizável (player e inimigos)
- [ ] Interface IDamageable
- [ ] Eventos OnDamaged / OnDeath
- [ ] Morte do player -> fim da run"

issue "Habilidade primária: estilingue de espinhos" "mvp,combate" \
"- [ ] Projétil disparado na direção da mira
- [ ] Cadência baseada em attack speed (stat)
- [ ] Object pooling para os projéteis"

issue "Inimigo base: formiga operária" "mvp,inimigos" \
"- [ ] NavMesh no mapa de teste
- [ ] Perseguir o player e atacar corpo a corpo
- [ ] Usar Health/IDamageable
- [ ] Prefab genérico de inimigo para herdar depois"

# ---------- Dias 3–4: loop roguelike ----------
issue "Spawner com dificuldade por tempo (ciclo dia/noite)" "mvp,inimigos" \
"- [ ] Timer da run e coeficiente de dificuldade crescente
- [ ] Taxa e quantidade de spawn escalam com a dificuldade
- [ ] Vida/dano dos inimigos escalam com a dificuldade
- [ ] Luz direcional (sol) gira e escurece com o tempo
- [ ] Object pooling dos inimigos"

issue "Moeda (migalhas) e drop de inimigos" "mvp,itens" \
"- [ ] Inimigo derruba migalhas ao morrer
- [ ] Migalhas atraídas pelo player quando perto
- [ ] Contador de moeda no player"

issue "Sistema de itens empilháveis (ScriptableObjects)" "mvp,itens" \
"- [ ] ItemData (nome, descrição, raridade, ícone)
- [ ] Inventário com contagem de stacks
- [ ] Stats do player recalculados a partir dos itens
- [ ] Hooks de efeito: ao acertar, ao matar, por segundo"

issue "Itens: primeiro lote (8–10 itens)" "mvp,itens" \
"Comuns:
- [ ] Grão de açúcar (+attack speed)
- [ ] Gota de orvalho (regen)
- [ ] Semente de dente-de-leão (pulo duplo/planar)
- [ ] Casca de semente (+armadura)
Incomuns:
- [ ] Clipe de papel (ricochete)
- [ ] Pedaço de chiclete (lentidão)
- [ ] Cabeça de fósforo (chance de queimar)
Lendários:
- [ ] Caco de lente (raio de sol concentrado)"

issue "Baús (tampinhas/cápsulas) para comprar itens" "mvp,itens" \
"- [ ] Interagir para abrir gastando migalhas
- [ ] Custo escala com a dificuldade
- [ ] Sorteio por raridade com pesos
- [ ] Spawn aleatório de baús pelo mapa"

# ---------- Dia 5: objetivo e variedade ----------
issue "Objetivo da fase: ativar a saída com onda de inimigos" "mvp,level-design" \
"- [ ] Objeto de saída (buraco/cano) que o player ativa
- [ ] Carregamento enquanto o player fica na área
- [ ] Spawn intensificado durante a ativação
- [ ] Ao terminar: vitória da fase / próxima fase"

issue "Inimigo: formiga-soldado (tanque)" "mvp,inimigos" \
"- [ ] Lenta, muita vida, ataque forte com wind-up visível"

issue "Inimigo: besouro-bombardeiro (ranged)" "mvp,inimigos" \
"- [ ] Mantém distância e atira ácido
- [ ] Poça de ácido que causa dano por segundo"

issue "Habilidades secundária, utilidade e especial" "mvp,combate" \
"- [ ] Secundária: estocada com agulha (dano alto, cooldown)
- [ ] Utilidade: dash
- [ ] Especial: chamar formigas aliadas por X segundos
- [ ] Cooldowns exibidos na HUD"

issue "Inimigos elite" "extra,inimigos" \
"- [ ] Chance de spawnar elite conforme a dificuldade
- [ ] Variantes: gigante (escala + vida) e em chamas (deixa fogo)
- [ ] Drop maior de migalhas"

# ---------- Dia 6: chefe e mapa ----------
issue "Mapa do quintal (gramado)" "mvp,level-design" \
"- [ ] Terreno com grama gigante, pedras, latas e folhas como plataformas
- [ ] Verticalidade: pontos altos alcançáveis
- [ ] Bake do NavMesh
- [ ] Iluminação e fog para dar escala"

issue "Chefe: louva-a-deus" "mvp,inimigos" \
"- [ ] Spawn ao ativar a saída (ou no fim da onda)
- [ ] 2–3 ataques com aviso visual
- [ ] Barra de vida do chefe na HUD
- [ ] Drop de item lendário"

issue "Perigo ambiental: pé humano pisando" "mvp,level-design" \
"- [ ] Sombra cresce no chão como aviso
- [ ] Pisão mata/causa dano em tudo na área (inimigos também)
- [ ] Frequência sobe com a dificuldade"

issue "Perigo ambiental: aspersor / cortador de grama" "extra,level-design" \
"- [ ] Aspersor: zonas de água que empurram/desaceleram
- [ ] Cortador: evento que atravessa o mapa"

issue "Inimigo: aranha (pulo + teia)" "extra,inimigos" \
"- [ ] Salta no player
- [ ] Teia que imobiliza por um instante"

# ---------- Dias 7–8: UI e polish ----------
issue "HUD" "mvp,ui" \
"- [ ] Vida, migalhas, timer e nível de dificuldade
- [ ] Ícones das habilidades com cooldown
- [ ] Barra de itens coletados com stacks
- [ ] Popup ao pegar item (nome + descrição curta)"

issue "Menus e telas" "mvp,ui" \
"- [ ] Menu principal
- [ ] Pause
- [ ] Tela de fim de run (tempo, itens, kills) com reiniciar"

issue "Game feel / juice" "mvp,polish" \
"- [ ] Números de dano
- [ ] Screen shake (Cinemachine Impulse)
- [ ] Hit stop curto
- [ ] Flash branco no inimigo ao ser atingido
- [ ] Partículas de morte"

issue "Áudio" "mvp,polish" \
"- [ ] Música ambiente (dia/noite)
- [ ] SFX: tiro, impacto, morte, coleta, baú, pisão"

issue "Fase do formigueiro (túneis escuros)" "extra,level-design" \
"- [ ] Corredores estreitos, pouca luz
- [ ] Vaga-lume como lanterna do player"

issue "Playtest, balanceamento e build final" "mvp,polish" \
"- [ ] Rodar runs completas e ajustar curva de dificuldade
- [ ] Corrigir bugs encontrados
- [ ] Build Windows e teste fora do editor
- [ ] Gravar vídeo/prints se a entrega pedir"

echo "==> Pronto!"

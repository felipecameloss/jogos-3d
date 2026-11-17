#!/usr/bin/env bash
# Cria as tasks extras (banco de ideias) com label de dificuldade e adiciona ao kanban.
#
# Uso:
#   ./criar_tasks_extras.sh <NUMERO_DO_PROJECT>   # ex.: ./criar_tasks_extras.sh 4  (banbanban)
#
# Filtrar no board: label:facil  |  label:media  |  label:dificil  |  label:proximo-lote

set -euo pipefail

REPO="felipecameloss/jogos-3d"
OWNER="felipecameloss"
PROJECT="${1:-}"
MILESTONE="Entrega 25/11"

echo "==> Labels"
label() { gh api "repos/$REPO/labels" -f name="$1" -f color="$2" -f description="$3" >/dev/null 2>&1 \
  || gh api -X PATCH "repos/$REPO/labels/$1" -f color="$2" -f description="$3" >/dev/null; }
label "extra"        "a2eeef" "Se der tempo"
label "core"         "5319e7" "Base: player, câmera, estrutura"
label "inimigos"     "fbca04" "IA, spawn, chefe"
label "itens"        "0e8a16" "Itens, moeda, baús"
label "level-design" "c5def5" "Mapa, perigos, ambiente"
label "ui"           "1d76db" "Interface e telas"
label "facil"        "8ae234" "Dificuldade: barato de fazer"
label "media"        "f5d90a" "Dificuldade: custo médio"
label "dificil"      "b60205" "Dificuldade: caro de fazer"
label "proximo-lote" "0052cc" "Prioridade do próximo lote de extras"

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

# =====================================================================
# 💚 FÁCEIS
# =====================================================================
issue "Itens ao acertar: ferrão de abelha e grafite de lápis" "extra,facil,itens,proximo-lote" \
"- [ ] Ferrão de abelha: chance de envenenar (dano ao longo do tempo)
- [ ] Grafite de lápis: dano extra contra inimigos com vida cheia"

issue "Itens ao matar: casca de amendoim e gotinha de mel" "extra,facil,itens,proximo-lote" \
"- [ ] Casca de amendoim: inimigo explode ao morrer (dano em área)
- [ ] Gotinha de mel: chance de dropar uma cura"

issue "Itens de defesa: tampa de caneta e casco de tatuzinho" "extra,facil,itens,proximo-lote" \
"- [ ] Tampa de caneta: escudo que recarrega se ficar X s sem levar dano
- [ ] Casco de tatuzinho: reduz dano levado"

issue "Itens de movimento: perna de gafanhoto e asa de mosca" "extra,facil,itens,proximo-lote" \
"- [ ] Perna de gafanhoto: pulo mais alto
- [ ] Asa de mosca: mais velocidade"

issue "Itens amaldiçoados: café derramado e cola branca" "extra,facil,itens" \
"Bônus forte com penalidade:
- [ ] Café derramado: +50% attack speed, -30% vida máxima
- [ ] Cola branca: inimigos ficam lentos, você também
- [ ] Visual/raridade própria de item amaldiçoado"

issue "Equipamento ativo: pastilha de menta" "extra,facil,itens" \
"Depende do sistema de equipamento ativo.
- [ ] Cura instantânea ao usar"

issue "Inimigo: pulgão" "extra,facil,inimigos,proximo-lote" \
"Reaproveita a IA da formiga.
- [ ] Inofensivo, mas solta mais migalhas (alvo de prioridade)
- [ ] Foge do player"

issue "Inimigo: cupim" "extra,facil,inimigos,proximo-lote" \
"Reaproveita a IA da formiga.
- [ ] Aparece em grupo saindo do chão (animação/partícula de terra)"

issue "Santuários: tampinha da sorte e santuário de sangue" "extra,facil,itens,proximo-lote" \
"- [ ] Tampinha da sorte: paga migalhas, chance de ganhar item
- [ ] Santuário de sangue: troca vida por migalhas
- [ ] Spawn aleatório pelo mapa (igual aos baús)"

issue "Aliados compráveis: joaninhas" "extra,facil,itens" \
"- [ ] Comprar com migalhas em um ponto do mapa
- [ ] Orbitam o player e atiram no inimigo mais próximo"

issue "Modificadores de run" "extra,facil,core" \
"Escolhidos antes de começar a run:
- [ ] Inimigos 2x mais rápidos
- [ ] Só itens amaldiçoados
- [ ] Chefe em toda fase
- [ ] Toggle no menu principal"

issue "Diário de descobertas" "extra,facil,ui" \
"- [ ] Registra itens e inimigos já vistos (salvo em PlayerPrefs/JSON)
- [ ] Tela no menu com os descobertos e os ainda ocultos (silhueta/?)"

# =====================================================================
# 💛 MÉDIAS
# =====================================================================
issue "Itens ao acertar: fio elétrico e pólen" "extra,media,itens" \
"- [ ] Fio elétrico desencapado: chance de raio que pula entre inimigos
- [ ] Pólen: acerto solta nuvem que deixa inimigos confusos"

issue "Item ao matar: cogumelo" "extra,media,itens" \
"- [ ] Brota um cogumelo onde o inimigo morreu
- [ ] Cura quem ficar perto por alguns segundos"

issue "Item de defesa: espinho de rosa" "extra,media,itens" \
"- [ ] Devolve parte do dano a quem bate em você"

issue "Item de movimento: bolha de sabão" "extra,media,itens" \
"- [ ] Cai devagar e flutua no ar (segurar pulo)"

issue "Sistema de equipamento ativo + lupa" "extra,media,itens,proximo-lote" \
"Como o Equipment do RoR: 1 por vez, com cooldown.
- [ ] Slot de equipamento, tecla de uso e cooldown na HUD
- [ ] Trocar equipamento ao pegar outro (dropa o antigo)
- [ ] Lupa: raio de sol que queima em linha"

issue "Equipamento ativo: spray de inseticida" "extra,media,itens" \
"- [ ] Cone de dano
- [ ] Deixa nuvem que também machuca o player se ficar nela"

issue "Equipamento ativo: apito de feromônio" "extra,media,itens" \
"- [ ] Formigas do mapa brigam entre si por X segundos"

issue "Inimigo: vaga-lume" "extra,media,inimigos" \
"- [ ] Só aparece à noite (ciclo dia/noite)
- [ ] Pisca e cega a tela por um instante"

issue "Inimigo: lesma" "extra,media,inimigos" \
"- [ ] Lenta
- [ ] Deixa rastro que faz o player escorregar"

issue "Inimigo: gafanhoto" "extra,media,inimigos" \
"- [ ] Salta de longe no player (com aviso)
- [ ] Bate forte"

issue "Inimigo: centopeia" "extra,media,inimigos" \
"- [ ] Corpo segmentado seguindo a cabeça
- [ ] Cada segmento com vida própria"

issue "Fase 2: horta" "extra,media,level-design,proximo-lote" \
"Reaproveitar o terreno do gramado trocando cores, luz e props.
- [ ] Tomates e abóboras gigantes como obstáculos
- [ ] Perigo: regador
- [ ] Tabela de inimigos própria da fase"

issue "Fase: embaixo do deck" "extra,media,level-design" \
"- [ ] Escuro, tábuas formando corredores
- [ ] Teias pelo caminho (lentidão)"

issue "Fase: poça d'água" "extra,media,level-design" \
"- [ ] Folhas boiando como plataformas
- [ ] Cair na água deixa o player lento"

issue "Chefe: aranha-rainha" "extra,media,inimigos,proximo-lote" \
"Pode reaproveitar a aranha comum como aranhinha.
- [ ] Prende o player em teia
- [ ] Invoca aranhinhas
- [ ] Barra de vida de chefe e drop lendário"

issue "Evento aleatório: chuva" "extra,media,level-design" \
"- [ ] Gotas gigantes caem com sombra de aviso no chão
- [ ] Reaproveitar lógica do pisão humano"

issue "Evento aleatório: criança com lupa" "extra,media,level-design" \
"- [ ] Raio de sol que segue o player por alguns segundos"

issue "Evento aleatório: gato" "extra,media,level-design" \
"- [ ] Gato atravessa o mapa
- [ ] Inimigos perto fogem assustados"

issue "Personagem jogável: formiga + tela de seleção" "extra,media,core" \
"- [ ] Tela de seleção de personagem no menu
- [ ] Formiga: rápida, frágil, comanda aliados"

issue "Personagem jogável: besouro" "extra,media,core" \
"- [ ] Tanque, ataque corpo a corpo
- [ ] Kit de habilidades próprio"

# =====================================================================
# ❤️ DIFÍCEIS
# =====================================================================
issue "Inimigo: vespa" "extra,dificil,inimigos" \
"- [ ] IA voadora (sem NavMesh)
- [ ] Atira ferrão e foge"

issue "Chefe: rainha formiga" "extra,dificil,inimigos" \
"Fica no fundo da fase do formigueiro.
- [ ] Parada, invoca operárias sem parar
- [ ] Luta de priorizar alvos"

issue "Fase final: cozinha" "extra,dificil,level-design" \
"- [ ] Dentro da casa: balcão, pia, migalhas por todo lado
- [ ] Verticalidade (subir no balcão)"

issue "Chefe final: o dedetizador" "extra,dificil,inimigos" \
"Humano gigante, chefe final da apresentação.
- [ ] Joga nuvens de veneno
- [ ] Pisa (reaproveitar o pisão)
- [ ] Player sobe nele para atacar pontos fracos"

echo "==> Pronto!"

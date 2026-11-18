# Diretrizes para Agentes (`AGENTS.md`)

Instruções de trabalho para qualquer agente atuando neste repositório (`jogos-3d`).

## 1. Manter o `README.md` Sempre Atualizado

- **Regra obrigatória**: sempre que adicionar, alterar ou remover funcionalidades, controles, inimigos, itens, santuários, cenas, dependências (`Packages/manifest.json`), estrutura de pastas ou fluxo de build, **atualize o [`README.md`](README.md) antes de concluir a tarefa**.
- Ao atualizar o `README.md`, cite apenas caminhos, símbolos, teclas e versões verificados diretamente nos arquivos atuais do repositório — nunca por memória ou suposição.

## 2. Convenções de Código e Assets Unity

- **Scripts C# (`Assets/Scripts/`, `Assets/Editor/`)**:
  - Nomes de classes, métodos e propriedades em inglês; textos voltados ao jogador e comentários de documentação/docstrings de sistema em português, mantendo comentários existentes que não estejam ligados à mudança.
  - Scripts de Editor (`using UnityEditor`) devem ficar exclusivamente em `Assets/Editor/` ou protegidos por `#if UNITY_EDITOR`.
- **Arquivos `.meta`, Prefabs e Cenas (`.unity` / `.prefab` / `.asset`)**:
  - Todo arquivo ou pasta criado dentro de `Assets/` deve possuir seu `.meta` válido com `guid` hexadecimal único de 32 caracteres.
  - Ao editar arquivos YAML do Unity manualmente, preservar `fileID`s locais e referências externas existentes e validar integridade referencial antes de finalizar.
- **Git LFS**:
  - `.gitattributes` coloca `.glb`, `.gltf`, `.fbx`, `.png`, `.wav`, `.mp3`, `.ogg`, etc. no Git LFS. Nunca commitar ponteiros LFS quebrados (~131 bytes) no lugar de binários; sempre verificar integridade quando o ambiente não possuir `git-lfs`.
- **Higiene do Repositório**:
  - Nunca commitar pastas geradas (`Library/`, `Temp/`, `Obj/`, `Builds/`, `.vs/`, `.idea/`) nem arquivos locais de ambiente ignorados em `.git/info/exclude` (como `GEMINI.md` ou `.agent-notes/`).

## 3. Verificação Antes de Concluir

- Se não houver Unity Editor disponível no ambiente de execução (ex.: Linux/WSL), **deixar explícito** quais validações estáticas/estruturais foram executadas (JSON, YAML, GUIDs, diff) e que compilação C# e Play Mode ainda precisam ser verificados no Unity Editor.

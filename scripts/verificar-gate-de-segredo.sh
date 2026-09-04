#!/usr/bin/env bash
#
# Prova que o gate de segredo REPROVA (#47).
#
# Um gate que nunca reprovou nao e gate: ele pode estar quebrado ha meses e o
# sintoma e o mesmo de estar funcionando — tudo verde. Este script fecha essa
# duvida em dez segundos, e existe para ser rodado de novo quando a versao do
# gitleaks mudar.
#
# Faz duas perguntas, e as duas precisam de resposta certa:
#   1. com segredo plantado, ele acusa?   (exit 1 esperado)
#   2. neste repositorio, ele passa?      (exit 0 esperado, sem falso positivo)
#
# ATENCAO ao testar a mao: NAO use `AKIAIOSFODNN7EXAMPLE`. Essa e a chave de
# exemplo da documentacao da AWS e o gitleaks a IGNORA de proposito. Quem testar
# com ela conclui que o gate esta quebrado — foi o que aconteceu na primeira
# tentativa desta verificacao.

set -uo pipefail

VERSAO="${GITLEAKS_VERSAO:-v8.30.1}"   # a mesma do .github/workflows/segredo.yml
IMAGEM="ghcr.io/gitleaks/gitleaks:$VERSAO"
REPO="$(git rev-parse --show-toplevel)"

varrer() { docker run --rm -v "$1:/repo" "$IMAGEM" detect --source /repo --redact --no-banner; }

echo "== 1. com segredo plantado, o gate precisa REPROVAR =="
PLANTADO="$(mktemp -d)"
trap 'rm -rf "$PLANTADO"' EXIT

git init -q "$PLANTADO"
# Os segredos sao GERADOS aqui, e nao escritos no arquivo — senao este proprio
# script vira um vazamento e o gate reprova o repositorio que ele deveria
# proteger. Foi exatamente o que aconteceu na primeira versao: a varredura
# acusou o PR que provava a varredura.
#
# Aleatorios, com o FORMATO que as regras reconhecem: prefixo e comprimento sao
# o que o gitleaks casa, e nenhum destes valores existe em lugar nenhum.
chave_aws="AKIA$(tr -dc 'A-Z0-9' < /dev/urandom | head -c 16)"
token_gh="ghp_$(tr -dc 'A-Za-z0-9' < /dev/urandom | head -c 36)"

cat > "$PLANTADO/vazamento.py" <<EOF
AWS_ACCESS_KEY_ID = "$chave_aws"
GITHUB_TOKEN = "$token_gh"
EOF
git -C "$PLANTADO" add -A
git -C "$PLANTADO" -c user.email=teste@local -c user.name=teste commit -qm "segredo plantado"

if varrer "$PLANTADO" > /dev/null 2>&1; then
  echo "FALHOU: o gate NAO acusou o segredo plantado. Ele nao esta protegendo nada." >&2
  exit 1
fi
echo "ok - acusou, como esperado"

echo
echo "== 2. neste repositorio, o gate precisa PASSAR =="
if ! varrer "$REPO" > /dev/null 2>&1; then
  echo "FALHOU: o gate acusou algo aqui. Olhe o achado:" >&2
  varrer "$REPO" >&2
  exit 1
fi
echo "ok - passou, e o .env.example nao virou falso positivo"

echo
echo "Gate de segredo verificado nos dois sentidos, com gitleaks $VERSAO."

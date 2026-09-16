#!/usr/bin/env bash
#
# Carga de 30s nos endpoints que recebem trafego (#54).
#
# A IA fica com FakeProvider de proposito: com provedor real o teste mediria a
# latencia da API do fornecedor, e nao este codigo. Aqui nao ha provedor nenhum
# configurado — e esse e o ponto.
#
# Uso:
#   dotnet run --project src/Copiloto.Api --urls http://localhost:5290 -c Release
#   ./scripts/stress.sh [url]
#
# Release, e nao Debug: medir o binario com otimizacao desligada produz um
# numero que nao existe em lugar nenhum.

set -euo pipefail

URL="${1:-http://localhost:5290}"
SAIDA="${SAIDA:-$(mktemp -d)}"
CORPO="$SAIDA/corpo.json"

cat > "$CORPO" <<'JSON'
{"providerMessageId":"wamid.carga","de":"+55 11 98888-1111","para":"+55 11 3333-4444","texto":"qual o valor do kg?","enviadaEm":"2026-09-04T13:00:00Z"}
JSON

if ! curl -sf -m 2 "$URL/saude" > /dev/null; then
  echo "A aplicacao nao respondeu em $URL/saude. Suba antes de medir." >&2
  exit 1
fi

medir() {  # nome conexoes [extras...]
  local nome="$1" conexoes="$2"; shift 2
  npx --yes autocannon@8 -c "$conexoes" -d 30 -j "$@" > "$SAIDA/$nome.json" 2>/dev/null
  python3 - "$SAIDA/$nome.json" "$nome" "$conexoes" <<'PY'
import json, sys
d = json.load(open(sys.argv[1])); l = d["latency"]
print(f"{sys.argv[2]:<10} c={sys.argv[3]:<4} "
      f"RPS={d['requests']['average']:>10.0f}  "
      f"p50={l['p50']:>3}ms p90={l.get('p90', '-'):>3}ms p99={l['p99']:>3}ms max={l['max']:>4}ms  "
      f"erros={d['errors']} timeouts={d['timeouts']} non2xx={d['non2xx']}")
PY
}

echo "Medindo $URL — 30s por cenario, resultados em $SAIDA"
medir saude 10 "$URL/saude"
medir saude 100 "$URL/saude"
medir webhook 10 -m POST -H "Content-Type=application/json" -i "$CORPO" "$URL/webhook/whatsapp"
medir webhook 100 -m POST -H "Content-Type=application/json" -i "$CORPO" "$URL/webhook/whatsapp"

echo
echo "Lembre de reportar a MAQUINA junto do numero: RPS sem maquina nao compara com nada."

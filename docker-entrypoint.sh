#!/bin/sh
set -e

# Se as credenciais do Infisical estiverem presentes, o container busca os
# segredos sozinho (Machine Identity / Universal Auth) e injeta no app.
# Caso contrario, roda o app direto com o ambiente que ja estiver setado
# (mantem a imagem portatil para rodar standalone/local).
if [ -n "${INFISICAL_CLIENT_ID:-}" ] && [ -n "${INFISICAL_CLIENT_SECRET:-}" ]; then
  echo "[entrypoint] Buscando segredos no Infisical (${INFISICAL_ENV:-prod} ${INFISICAL_PATH:-/})..."
  INFISICAL_TOKEN="$(infisical login \
    --method=universal-auth \
    --client-id="$INFISICAL_CLIENT_ID" \
    --client-secret="$INFISICAL_CLIENT_SECRET" \
    --domain="${INFISICAL_API_URL:-https://app.infisical.com}" \
    --plain --silent)"
  export INFISICAL_TOKEN

  exec infisical run \
    --domain="${INFISICAL_API_URL:-https://app.infisical.com}" \
    --projectId="$INFISICAL_PROJECT_ID" \
    --env="${INFISICAL_ENV:-prod}" \
    --path="${INFISICAL_PATH:-/}" \
    -- dotnet VirtualGameCard.Api.dll
else
  echo "[entrypoint] Sem credenciais Infisical; rodando com o ambiente atual."
  exec dotnet VirtualGameCard.Api.dll
fi

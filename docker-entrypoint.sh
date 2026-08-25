#!/bin/sh
set -e

# Se houver credenciais do Infisical, o container busca os segredos sozinho
# (Machine Identity / Universal Auth) via API e injeta no app. Caso contrario,
# roda o app com o ambiente atual (mantem a imagem portatil/standalone).
if [ -n "${INFISICAL_CLIENT_ID:-}" ] && [ -n "${INFISICAL_CLIENT_SECRET:-}" ]; then
  API="${INFISICAL_API_URL:-https://app.infisical.com}"
  ENV_SLUG="${INFISICAL_ENV:-prod}"
  SECRET_PATH="${INFISICAL_PATH:-/}"

  echo "[entrypoint] Autenticando no Infisical..."
  TOKEN=$(curl -sf -X POST "$API/api/v1/auth/universal-auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"clientId\":\"$INFISICAL_CLIENT_ID\",\"clientSecret\":\"$INFISICAL_CLIENT_SECRET\"}" \
    | jq -r '.accessToken // empty')

  if [ -z "$TOKEN" ]; then
    echo "[entrypoint] ERRO: login falhou (verifique Client ID/Secret e o INFISICAL_API_URL)."
    exit 1
  fi

  echo "[entrypoint] Buscando segredos ($ENV_SLUG $SECRET_PATH)..."
  if ! RESP=$(curl -sf -G "$API/api/v3/secrets/raw" \
      -H "Authorization: Bearer $TOKEN" \
      --data-urlencode "workspaceId=$INFISICAL_PROJECT_ID" \
      --data-urlencode "environment=$ENV_SLUG" \
      --data-urlencode "secretPath=$SECRET_PATH" \
      --data-urlencode "expandSecretReferences=true"); then
    echo "[entrypoint] ERRO: nao consegui ler os segredos (403? a identidade tem acesso READ ao projeto/ambiente '$ENV_SLUG'?)."
    exit 1
  fi

  COUNT=$(echo "$RESP" | jq '.secrets | length')
  echo "[entrypoint] $COUNT segredos carregados do Infisical."

  # exporta cada segredo com aspas seguras (@sh)
  eval "$(echo "$RESP" | jq -r '.secrets[] | "export " + .secretKey + "=" + (.secretValue | @sh)')"

  exec dotnet VirtualGameCard.Api.dll
else
  echo "[entrypoint] Sem credenciais Infisical; rodando com o ambiente atual."
  exec dotnet VirtualGameCard.Api.dll
fi

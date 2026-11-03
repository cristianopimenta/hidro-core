#!/usr/bin/env python3
import base64
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone

API_PORT = int(os.environ.get("HML_API_PORT", "18090"))
WEB_PORT = int(os.environ.get("HML_WEB_PORT", "18091"))
KC_PORT = int(os.environ.get("HML_KEYCLOAK_PORT", "18080"))
API = f"http://127.0.0.1:{API_PORT}"
WEB = f"http://127.0.0.1:{WEB_PORT}"
REALM = "hidro-homologacao"
CLIENT = "hidrocore-homologacao-cli"
ONCA_ID = "11111111-1111-4111-8111-111111111111"
SANTA_ID = "22222222-2222-4222-8222-222222222222"
ONCA_USINA_SEED = "11111111-1111-4111-8111-000000000001"
SANTA_USINA_SEED = "22222222-2222-4222-8222-000000000001"
ONCA_EQUIPAMENTO_SEED = "11111111-1111-4111-8111-000000000003"
SANTA_EQUIPAMENTO_SEED = "22222222-2222-4222-8222-000000000003"
ONCA_GRANDEZA_SEED = "11111111-1111-4111-8111-000000000004"
SANTA_GRANDEZA_SEED = "22222222-2222-4222-8222-000000000004"
USERS = {
    "onca.teste": ("Onca-Test-Only-2026", ONCA_ID, "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
    "santa.teste": ("Santa-Test-Only-2026", SANTA_ID, "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
    "onca.operador": ("Operador-Test-Only-2026", ONCA_ID, None),
}

def request(url, host=None, token=None, forwarded=None, data=None, form_encoded=False):
    headers = {}
    if host:
        headers["Host"] = host
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if forwarded:
        headers["X-Forwarded-Host"] = forwarded
    body = None
    if data is not None:
        if form_encoded:
            body = urllib.parse.urlencode(data).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
        elif isinstance(data, dict):
            body = json.dumps(data).encode()
            headers["Content-Type"] = "application/json"
        else:
            body = urllib.parse.urlencode(data).encode()
            headers["Content-Type"] = "application/x-www-form-urlencoded"
    req = urllib.request.Request(url, data=body, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            return response.status, response.read()
    except urllib.error.HTTPError as error:
        return error.code, error.read()

def check(condition, message):
    if not condition:
        raise RuntimeError(message)

def decode_payload(jwt):
    chunk = jwt.split(".")[1]
    chunk += "=" * ((4 - len(chunk) % 4) % 4)
    return json.loads(base64.urlsafe_b64decode(chunk))

def wait_for_keycloak():
    url = f"http://127.0.0.1:{KC_PORT}/realms/{REALM}/.well-known/openid-configuration"
    deadline = time.time() + 180
    while time.time() < deadline:
        try:
            status, body = request(url)
            if status == 200:
                config = json.loads(body)
                check(config["issuer"] == f"http://keycloak:8080/realms/{REALM}", f"Issuer inesperado: {config['issuer']}")
                check(config["jwks_uri"] == f"http://keycloak:8080/realms/{REALM}/protocol/openid-connect/certs", "JWKS não aponta ao serviço Keycloak interno.")
                print("PASS: discovery OIDC, issuer e endpoint JWKS locais.", flush=True)
                return
        except (OSError, json.JSONDecodeError, KeyError):
            pass
        time.sleep(2)
    raise RuntimeError("Keycloak/OIDC não ficou pronto em 180 segundos.")

def token_for(username, client=CLIENT, expect_api_audience=True):
    password, expected_tenant, expected_sub = USERS[username]
    url = f"http://127.0.0.1:{KC_PORT}/realms/{REALM}/protocol/openid-connect/token"
    status, body = request(url, data={
        "grant_type": "password", "client_id": client, "username": username,
        "password": password, "scope": "openid",
    }, form_encoded=True)
    check(status == 200, f"Keycloak token endpoint retornou {status}: {body[:300]!r}")
    result = json.loads(body)
    claims = decode_payload(result["access_token"])
    check(claims.get("iss") == f"http://keycloak:8080/realms/{REALM}", "JWT issuer claim diverge do Authority da API.")
    check(bool(claims.get("sub")) and (expected_sub is None or claims.get("sub") == expected_sub), "JWT sub inesperado.")
    check(claims.get("tenant_id") == expected_tenant, "JWT tenant_id inesperado.")
    aud = claims.get("aud", [])
    if isinstance(aud, str):
        aud = [aud]
    check(("hidrocore-api" in aud) == expect_api_audience, f"Audience JWT inesperada: {aud}")
    check(claims.get("exp", 0) > time.time(), "JWT já expirado.")
    return result["access_token"], claims

def json_request(method, path, host, token=None, payload=None):
    headers = {"Host": host}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = json.dumps(payload).encode() if payload is not None else None
    if data is not None:
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(API + path, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=10) as response:
            body = response.read()
            return response.status, json.loads(body) if body else None
    except urllib.error.HTTPError as error:
        body = error.read()
        try:
            body = json.loads(body) if body else None
        except json.JSONDecodeError:
            pass
        return error.code, body

def exercise_api_crud(onca_token, santa_token, operator_token):
    code = uuid.uuid4().hex[:8].upper()
    usina_payload = {"nome": "Usina API Homologação", "codigo": f"API-{code}", "tenantId": SANTA_ID}
    status, usina = json_request("POST", "/api/usinas", "onca.hidro.test", onca_token, usina_payload)
    check(status == 201 and usina["codigo"] == usina_payload["codigo"] and "tenantId" not in usina, f"POST usinas inválido: {status}, {usina}")
    usina_id = usina["id"]
    status, body = json_request("POST", "/api/usinas", "onca.hidro.test", onca_token, usina_payload)
    check(status == 409, f"Código duplicado deveria ser 409, recebeu {status}.")
    status, usinas = json_request("GET", "/api/usinas", "onca.hidro.test", onca_token)
    check(status == 200 and any(item["id"] == usina_id for item in usinas), "Usina criada não aparece na leitura do tenant.")
    status, body = json_request("POST", "/api/usinas", "onca.hidro.test", operator_token, {"nome": "Sem permissão", "codigo": f"OP-{code}"})
    check(status == 403, f"Operador não deveria administrar usinas; recebeu {status}.")
    status, _ = json_request("GET", f"/api/usinas/{usina_id}", "santamaria.hidro.test", santa_token)
    check(status == 404, f"Santa Maria deveria receber 404 para recurso Onça; recebeu {status}.")
    status, _ = json_request("GET", f"/api/usinas/{usina_id}", "santamaria.hidro.test", onca_token)
    check(status == 403, f"JWT Onça no domínio Santa deveria receber 403; recebeu {status}.")

    status, updated = json_request("PUT", f"/api/usinas/{usina_id}", "onca.hidro.test", onca_token,
                                   {"nome": "Usina API Atualizada", "codigo": f"API-{code}-U"})
    check(status == 200 and updated["nome"] == "Usina API Atualizada", f"PUT usina falhou: {status}.")

    station_payload = {"usinaId": usina_id, "nome": "Estação API", "codigo": f"EST-{code}"}
    status, station = json_request("POST", "/api/estacoes", "onca.hidro.test", onca_token, station_payload)
    check(status == 201, f"POST estação falhou: {status}, {station}")
    station_id = station["id"]
    status, _ = json_request("POST", "/api/estacoes", "onca.hidro.test", onca_token,
                             {"usinaId": SANTA_USINA_SEED, "nome": "Referência cruzada", "codigo": f"CROSS-{code}"})
    check(status == 404, f"Estação não pode referenciar usina de outro tenant; recebeu {status}.")
    status, _ = json_request("POST", "/api/estacoes", "onca.hidro.test", operator_token,
                             {"usinaId": usina_id, "nome": "Sem permissão", "codigo": f"OP-EST-{code}"})
    check(status == 403, "Operador não deveria administrar estações.")
    status, stations = json_request("GET", f"/api/estacoes?usinaId={usina_id}", "onca.hidro.test", onca_token)
    check(status == 200 and any(item["id"] == station_id for item in stations), "Filtro/listagem de estações falhou.")
    status, station = json_request("PUT", f"/api/estacoes/{station_id}", "onca.hidro.test", onca_token,
                                   {"usinaId": usina_id, "nome": "Estação API Atualizada", "codigo": f"EST-{code}-U"})
    check(status == 200 and station["nome"] == "Estação API Atualizada", "PUT estação falhou.")
    status, _ = json_request("PUT", f"/api/estacoes/{station_id}", "onca.hidro.test", onca_token,
                             {"usinaId": SANTA_USINA_SEED, "nome": "Referência cruzada", "codigo": f"CROSS-{code}"})
    check(status == 404, "PUT estação não pode vincular usina de outro tenant.")

    equipment_payload = {"estacaoId": station_id, "nome": "Sensor API", "codigo": f"EQ-{code}"}
    status, equipment = json_request("POST", "/api/equipamentos", "onca.hidro.test", onca_token, equipment_payload)
    check(status == 201, f"POST equipamento falhou: {status}, {equipment}")
    equipment_id = equipment["id"]
    status, _ = json_request("POST", "/api/equipamentos", "onca.hidro.test", operator_token,
                             {"estacaoId": station_id, "nome": "Sem permissão", "codigo": f"OP-EQ-{code}"})
    check(status == 403, "Operador não deveria administrar equipamentos.")
    status, equipment_list = json_request("GET", f"/api/equipamentos?estacaoId={station_id}", "onca.hidro.test", onca_token)
    check(status == 200 and any(item["id"] == equipment_id for item in equipment_list), "Filtro/listagem de equipamentos falhou.")
    status, equipment = json_request("PUT", f"/api/equipamentos/{equipment_id}", "onca.hidro.test", onca_token,
                                     {"estacaoId": station_id, "nome": "Sensor API Atualizado", "codigo": f"EQ-{code}-U"})
    check(status == 200 and equipment["nome"] == "Sensor API Atualizado", "PUT equipamento falhou.")

    status, grandezas = json_request("GET", "/api/grandezas", "onca.hidro.test", onca_token)
    check(status == 200 and any(item["id"] == ONCA_GRANDEZA_SEED for item in grandezas), "Grandezas do tenant não listadas.")
    instant = datetime.now(timezone.utc).replace(microsecond=(datetime.now(timezone.utc).microsecond // 1000) * 1000)
    reading_payload = {"equipamentoId": equipment_id, "grandezaId": ONCA_GRANDEZA_SEED, "instante": instant.isoformat(), "valor": 12.345678}
    status, reading = json_request("POST", "/api/leituras", "onca.hidro.test", operator_token, reading_payload)
    check(status == 201, f"Operador não gravou leitura permitida: {status}, {reading}")
    reading_id = reading["id"]
    status, reading_detail = json_request("GET", f"/api/leituras/{reading_id}", "onca.hidro.test", operator_token)
    check(status == 200 and reading_detail["id"] == reading_id, "GET leitura por id falhou.")
    status, _ = json_request("POST", "/api/leituras", "onca.hidro.test", operator_token, reading_payload)
    check(status == 409, f"Leitura duplicada deveria ser 409; recebeu {status}.")
    second_reading_payload = {**reading_payload, "instante": (instant + timedelta(seconds=1)).isoformat(), "valor": 13.456789}
    status, second_reading = json_request("POST", "/api/leituras", "onca.hidro.test", operator_token, second_reading_payload)
    check(status == 201, f"Segunda leitura necessária para paginação falhou: {status}.")
    status, _ = json_request("POST", "/api/leituras", "onca.hidro.test", operator_token,
                             {**reading_payload, "valor": 0.0000001, "instante": (instant + timedelta(seconds=2)).isoformat()})
    check(status == 400, f"Precisão inválida deveria ser 400; recebeu {status}.")
    status, _ = json_request("POST", "/api/leituras", "onca.hidro.test", onca_token,
                             {**reading_payload, "equipamentoId": SANTA_EQUIPAMENTO_SEED, "instante": (instant + timedelta(seconds=2)).isoformat()})
    check(status == 404, f"Leitura não pode referenciar equipamento de outro tenant; recebeu {status}.")
    start = urllib.parse.quote((instant - timedelta(seconds=1)).isoformat())
    end = urllib.parse.quote((instant + timedelta(seconds=3)).isoformat())
    path = f"/api/leituras?estacaoId={station_id}&grandezaId={ONCA_GRANDEZA_SEED}&inicio={start}&fim={end}&page=1&pageSize=1"
    status, page1 = json_request("GET", path, "onca.hidro.test", operator_token)
    check(status == 200 and page1["totalCount"] == 2 and page1["pageSize"] == 1 and len(page1["items"]) == 1,
          f"Filtros/paginação de leituras falharam: {status}, {page1}")
    status, page2 = json_request("GET", path.replace("page=1", "page=2"), "onca.hidro.test", operator_token)
    check(status == 200 and page2["totalCount"] == 2 and len(page2["items"]) == 1 and page2["items"][0]["id"] != page1["items"][0]["id"],
          "Paginação de leituras não avançou para o segundo item.")
    status, _ = json_request("GET", "/api/leituras?page=0", "onca.hidro.test", operator_token)
    check(status == 400, f"page=0 deveria retornar 400; recebeu {status}.")
    status, _ = json_request("GET", f"/api/leituras?inicio={end}&fim={start}", "onca.hidro.test", operator_token)
    check(status == 400, f"intervalo invertido deveria retornar 400; recebeu {status}.")
    status, _ = json_request("GET", f"/api/leituras?estacaoId={station_id}", "santamaria.hidro.test", santa_token)
    check(status == 404, f"Filtro de estação cross-tenant deveria retornar 404; recebeu {status}.")

    # Referenced records must not be deleted while children/readings exist.
    status, _ = json_request("DELETE", f"/api/equipamentos/{equipment_id}", "onca.hidro.test", onca_token)
    check(status == 409, f"Equipamento com leitura deveria retornar 409; recebeu {status}.")
    status, _ = json_request("DELETE", f"/api/estacoes/{station_id}", "onca.hidro.test", onca_token)
    check(status == 409, f"Estação com equipamento deveria retornar 409; recebeu {status}.")
    status, _ = json_request("DELETE", f"/api/usinas/{usina_id}", "onca.hidro.test", onca_token)
    check(status == 409, f"Usina com estação deveria retornar 409; recebeu {status}.")

    # Exercise successful deletes on unreferenced resources, children first.
    status, disposable = json_request("POST", "/api/usinas", "onca.hidro.test", onca_token,
                                      {"nome": "Descartável", "codigo": f"DEL-{code}"})
    check(status == 201, "Falha criando usina descartável.")
    status, disposable_station = json_request("POST", "/api/estacoes", "onca.hidro.test", onca_token,
                                              {"usinaId": disposable["id"], "nome": "Descartável", "codigo": f"DEL-EST-{code}"})
    check(status == 201, "Falha criando estação descartável.")
    status, disposable_equipment = json_request("POST", "/api/equipamentos", "onca.hidro.test", onca_token,
                                                {"estacaoId": disposable_station["id"], "nome": "Descartável", "codigo": f"DEL-EQ-{code}"})
    check(status == 201, "Falha criando equipamento descartável.")
    for path_to_delete in [f"/api/equipamentos/{disposable_equipment['id']}", f"/api/estacoes/{disposable_station['id']}", f"/api/usinas/{disposable['id']}"]:
        status, _ = json_request("DELETE", path_to_delete, "onca.hidro.test", onca_token)
        check(status == 204, f"DELETE {path_to_delete} deveria retornar 204; recebeu {status}.")
    print("PASS: CRUD usinas/estações/equipamentos, leitura autorizada, validação, filtros/paginação e bloqueios de referência/cross-tenant.", flush=True)

def check_api_and_web():
    wait_for_keycloak()
    onca_token, _ = token_for("onca.teste")
    santa_token, _ = token_for("santa.teste")
    operator_token, _ = token_for("onca.operador")

    status, _ = request(f"{API}/api/tenant", host="onca.hidro.test")
    check(status == 401, f"API sem JWT esperava 401, recebeu {status}.")
    for host, expected, token in [("onca.hidro.test", ONCA_ID, onca_token), ("santamaria.hidro.test", SANTA_ID, santa_token)]:
        status, body = request(f"{API}/api/tenant", host=host, token=token)
        check(status == 200 and json.loads(body)["tenantId"] == expected, f"Resolução incorreta para {host}: HTTP {status}.")
        status, body = request(f"{API}/api/branding", host=host, token=token)
        expected_name = "Cafeeira da Onça" if expected == ONCA_ID else "Santa Maria"
        check(status == 200 and json.loads(body)["nome"] == expected_name, f"Branding incorreto para {host}.")

    bad_aud_token, _ = token_for("onca.teste", client="hidrocore-sem-audience-cli", expect_api_audience=False)
    status, _ = request(f"{API}/api/tenant", host="onca.hidro.test", token=bad_aud_token)
    check(status == 401, f"JWT com audience errada deveria retornar 401, recebeu {status}.")
    pieces = onca_token.rsplit(".", 1)
    bad_signature = pieces[0] + "." + ("A" if pieces[1][0] != "A" else "B") + pieces[1][1:]
    status, _ = request(f"{API}/api/tenant", host="onca.hidro.test", token=bad_signature)
    check(status == 401, f"JWT com assinatura alterada deveria retornar 401, recebeu {status}.")
    for host, token in [("santamaria.hidro.test", onca_token), ("onca.hidro.test", santa_token)]:
        status, _ = request(f"{API}/api/tenant", host=host, token=token)
        check(status == 403, f"Acesso JWT cruzado {host} esperava 403, recebeu {status}.")
    print("PASS: JWT/issuer/sub/tenant_id/audience, assinatura adulterada, 401/403 e associação de operador.", flush=True)

    for host, forwarded in [("unknown.hidro.test", None), ("forjado.hidro.test", "onca.hidro.test")]:
        status, _ = request(f"{API}/api/tenant", host=host, token=onca_token, forwarded=forwarded)
        check(status == 404, f"Domínio desconhecido/forjado esperava 404, recebeu {status}.")
    status, body = request(f"{WEB}/", host="onca.hidro.test")
    check(status == 200 and b"Hidro-Core" in body, f"Web Onça esperava 200, recebeu {status}.")
    status, _ = request(f"{WEB}/", host="unknown.hidro.test")
    check(status == 404, f"Web deveria bloquear domínio desconhecido; recebeu {status}.")
    print("PASS: hostname validado, forwarded-host não confiável e Web por domínio.", flush=True)
    exercise_api_crud(onca_token, santa_token, operator_token)
    return onca_token

def prepare_expiration(path):
    token, claims = token_for("onca.teste")
    status, _ = request(f"{API}/api/tenant", host="onca.hidro.test", token=token)
    check(status == 200, f"JWT de expiração não foi aceito antes de expirar: HTTP {status}.")
    with open(path, "w", encoding="utf-8") as output:
        json.dump({"token": token, "exp": claims["exp"]}, output)
    os.chmod(path, 0o600)
    print(f"PASS: JWT válido antes do exp; token efêmero armazenado em arquivo local protegido (exp={claims['exp']}).", flush=True)

def finish_expiration(path):
    with open(path, encoding="utf-8") as source:
        entry = json.load(source)
    deadline = entry["exp"] + 305
    wait = max(0, deadline - time.time())
    while wait > 0:
        chunk = min(wait, 30)
        time.sleep(chunk)
        wait -= chunk
        print(f"Aguardando expiração JWT e clock skew padrão; faltam aproximadamente {max(0, int(wait))}s.", flush=True)
    status, _ = request(f"{API}/api/tenant", host="onca.hidro.test", token=entry["token"])
    check(status == 401, f"JWT expirado deveria ser rejeitado com 401; API respondeu {status}.")
    print("PASS: API rejeitou JWT expirado após o clock skew.", flush=True)

if len(sys.argv) == 2 and sys.argv[1] == "--check":
    check_api_and_web()
elif len(sys.argv) == 3 and sys.argv[1] == "--prepare-expiration":
    wait_for_keycloak(); prepare_expiration(sys.argv[2])
elif len(sys.argv) == 3 and sys.argv[1] == "--finish-expiration":
    finish_expiration(sys.argv[2])
else:
    raise SystemExit("uso: run_http_tests.py --check | --prepare-expiration ARQUIVO | --finish-expiration ARQUIVO")

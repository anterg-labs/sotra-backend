"""Configura el acceso local por la IP del host sin versionar IP, secretos ni ampliar callbacks a cualquier dominio."""
from pathlib import Path
import json
import os
import urllib.request
import urllib.parse

raiz = Path(__file__).resolve().parent.parent
frontend = raiz.parent / 'sotra-frontend'

def leer_variables(ruta):
    return dict(linea.split('=', 1) for linea in ruta.read_text(encoding='utf-8-sig').splitlines()
                if '=' in linea and not linea.lstrip().startswith('#'))

def guardar_variables(ruta, cambios):
    texto = ruta.read_bytes().decode('utf-8-sig')
    salto = '\r\n' if '\r\n' in texto else '\n'
    lineas = texto.splitlines()
    for nombre, valor in cambios.items():
        indice = next((i for i, linea in enumerate(lineas) if linea.startswith(nombre + '=')), None)
        if indice is None: lineas.append(nombre + '=' + valor)
        else: lineas[indice] = nombre + '=' + valor
    ruta.write_bytes((salto.join(lineas) + salto).encode('utf-8'))

variables = leer_variables(raiz / '.env')
ip = json.loads((frontend / '.ip-local.json').read_text(encoding='utf-8'))['ipLocal']
if not ip or len(ip.split('.')) != 4 or any(not p.isdigit() or not 0 <= int(p) <= 255 for p in ip.split('.')):
    raise RuntimeError('Primero actualizá la IP desde el frontend con npm run qr:actualizar-ip.')
puerto = variables.get('KC_PORT', '9080')
base = 'http://localhost:' + puerto
publica = 'http://' + ip + ':' + puerto
origen = 'http://' + ip

def pedir(ruta, metodo='GET', datos=None, token=None):
    cabeceras = {}
    if token: cabeceras['Authorization'] = 'Bearer ' + token
    cuerpo = None
    if datos is not None:
        cuerpo = json.dumps(datos).encode('utf-8'); cabeceras['Content-Type'] = 'application/json'
    solicitud = urllib.request.Request(base + ruta, data=cuerpo, headers=cabeceras, method=metodo)
    with urllib.request.urlopen(solicitud, timeout=30) as respuesta:
        contenido = respuesta.read()
        return json.loads(contenido) if contenido else None

credenciales = urllib.parse.urlencode({
    'grant_type': 'password', 'client_id': 'admin-cli',
    'username': variables['KC_BOOTSTRAP_ADMIN_USERNAME'].strip('"\''),
    'password': variables['KC_BOOTSTRAP_ADMIN_PASSWORD'].strip('"\''),
}).encode('utf-8')
solicitud = urllib.request.Request(base + '/realms/master/protocol/openid-connect/token', data=credenciales)
with urllib.request.urlopen(solicitud, timeout=30) as respuesta:
    token = json.load(respuesta)['access_token']
clientes = pedir('/admin/realms/sotra/clients?clientId=sotra-frontend', token=token)
if len(clientes) != 1: raise RuntimeError('No se encontró un único cliente sotra-frontend.')
ruta = '/admin/realms/sotra/clients/' + clientes[0]['id']
cliente = pedir(ruta, token=token)
respaldo = { 'redirectUris': cliente.get('redirectUris', []), 'webOrigins': cliente.get('webOrigins', []),
    'postLogout': cliente.get('attributes', {}).get('post.logout.redirect.uris') }
(raiz / '.acceso-local-anterior.json').write_text(json.dumps(respaldo, indent=2), encoding='utf-8')
for campo, valor in [('redirectUris', origen + '/*'), ('webOrigins', origen)]:
    valores = cliente.setdefault(campo, [])
    if valor not in valores: valores.append(valor)
atributos = cliente.setdefault('attributes', {})
salidas = atributos.get('post.logout.redirect.uris', '').split('##')
if origen + '/*' not in salidas: salidas.append(origen + '/*')
atributos['post.logout.redirect.uris'] = '##'.join(v for v in salidas if v)
pedir(ruta, 'PUT', cliente, token)
guardar_variables(raiz / '.env', { 'KEYCLOAK_URL_PUBLICA': publica, 'KEYCLOAK_EMISOR': publica + '/realms/sotra' })
guardar_variables(frontend / '.env.dev', { 'AUTH_URL': '', 'KEYCLOAK_EMISOR': publica + '/realms/sotra' })
print('Configuración local preparada: emisor público en la IP de red, callbacks explícitos y origen del frontend inferido del pedido. No se modificaron usuarios ni roles.')

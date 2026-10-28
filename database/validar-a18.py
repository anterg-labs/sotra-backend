"""Valida init e incremental A18 en una base descartable y exporta el dump sin datos personales ni binarios."""
from pathlib import Path
import subprocess
import time
import uuid

raiz = Path(__file__).resolve().parent.parent
contenedor = "sotra-a18-prueba-" + uuid.uuid4().hex[:12]

def docker(*argumentos, entrada=None):
    resultado = subprocess.run(["docker", *argumentos], input=entrada, capture_output=True, check=True)
    return resultado.stdout

def sql(texto):
    return docker("exec", "-i", contenedor, "psql", "-U", "postgres", "-d", "sotra", "-v", "ON_ERROR_STOP=1", "-tA", entrada=texto.encode("utf-8")).decode("utf-8").strip()

try:
    docker("run", "--rm", "-d", "--name", contenedor, "-e", "POSTGRES_PASSWORD=clave_solo_base_descartable", "-e", "POSTGRES_DB=sotra", "pgvector/pgvector:pg18-trixie")
    for intento in range(40):
        listo = subprocess.run(["docker", "exec", contenedor, "pg_isready", "-U", "postgres", "-d", "sotra"], capture_output=True)
        if listo.returncode == 0:
            break
        time.sleep(1)
    else:
        raise RuntimeError("La base descartable no inició.")
    sql((raiz / "database/init/01-init.sql").read_text(encoding="utf-8-sig"))
    assert sql("SELECT count(*) FROM operativo.umbral_etapa_ot") == "4"
    nuevo = sql("SELECT etapa || ':' || COALESCE(umbral_horas::text, 'null') FROM operativo.umbral_etapa_ot ORDER BY etapa")
    sql("DROP TABLE operativo.umbral_etapa_ot")
    cambio = (raiz / "database/cambios/2026-10-27-umbrales-etapas.sql").read_text(encoding="utf-8-sig")
    sql(cambio)
    assert sql("SELECT etapa || ':' || COALESCE(umbral_horas::text, 'null') FROM operativo.umbral_etapa_ot ORDER BY etapa") == nuevo
    sql("UPDATE operativo.umbral_etapa_ot SET umbral_horas = 17 WHERE etapa = 'pausa'")
    sql(cambio)
    assert sql("SELECT umbral_horas FROM operativo.umbral_etapa_ot WHERE etapa = 'pausa'") == "17"
    sql("UPDATE operativo.umbral_etapa_ot SET umbral_horas = NULL")
    for valor in ["-1", "'NaN'::float8", "'Infinity'::float8"]:
        try:
            sql(f"UPDATE operativo.umbral_etapa_ot SET umbral_horas = {valor} WHERE etapa = 'pausa'")
        except subprocess.CalledProcessError:
            pass
        else:
            raise AssertionError("La restricción aceptó un valor inválido.")
    # Los datos salen exclusivamente del init, nunca de la base local. Anonimiza las personas de sus ejemplos.
    sql("""
        UPDATE operativo.usuario SET nombre = 'Usuario de ejemplo ' || id_usuario, apellido = 'Ficticio',
            mail = 'usuario' || id_usuario || '@example.test', telefono = '09' || lpad(id_usuario::text, 7, '0'), id_keycloak = NULL;
        UPDATE operativo.cliente SET razon_social = 'Empresa de ejemplo ' || id_cliente, nombre = 'Contacto de ejemplo ' || id_cliente,
            correo = 'cliente' || id_cliente || '@example.test', telefono = '09' || lpad(id_cliente::text, 7, '0'), direccion = 'Dirección ficticia', observaciones = '';
        UPDATE operativo.evidencia_ot SET archivo = NULL;
        """)
    volcado = docker("exec", contenedor, "pg_dump", "-U", "postgres", "-d", "sotra", "--schema=operativo", "--no-owner", "--no-privileges")
    assert b"CREATE SCHEMA vectorial" not in volcado
    (raiz / "sotra-dump.sql").write_bytes(volcado)
    print("A18: init e incremental equivalentes; segunda aplicación preserva configuración; restricciones aprobadas; dump regenerado desde base descartable.")
finally:
    if contenedor.startswith("sotra-a18-prueba-"):
        subprocess.run(["docker", "rm", "-f", contenedor], capture_output=True)

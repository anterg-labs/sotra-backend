"""Comprueba init e incremental en una base descartable y regenera el dump sanitizado."""
from pathlib import Path
import subprocess
import time
import uuid

raiz = Path(__file__).resolve().parent.parent
contenedor = "sotra-errores-prueba-" + uuid.uuid4().hex[:12]

def docker(*argumentos, entrada=None):
    resultado = subprocess.run(["docker", *argumentos], input=entrada, capture_output=True, check=True)
    return resultado.stdout

def sql(texto):
    return docker("exec", "-i", contenedor, "psql", "-U", "postgres", "-d", "sotra", "-v", "ON_ERROR_STOP=1", "-tA", entrada=texto.encode("utf-8")).decode("utf-8").strip()

try:
    docker("run", "--rm", "-d", "--name", contenedor, "-e", "POSTGRES_PASSWORD=clave_solo_base_descartable", "-e", "POSTGRES_DB=sotra", "pgvector/pgvector:pg18-trixie")
    for _ in range(40):
        if subprocess.run(["docker", "exec", contenedor, "pg_isready", "-U", "postgres", "-d", "sotra"], capture_output=True).returncode == 0:
            break
        time.sleep(1)
    else:
        raise RuntimeError("La base descartable no inició.")
    sql((raiz / "database/init/01-init.sql").read_text(encoding="utf-8-sig"))
    assert sql("SELECT count(*) FROM operativo.registro_error") == "0"
    sql("DROP TABLE operativo.registro_error")
    cambio = (raiz / "database/cambios/2026-10-27-registro-errores.sql").read_text(encoding="utf-8-sig")
    sql(cambio)
    sql(cambio)
    assert sql("SELECT count(*) FROM operativo.registro_error") == "0"
    assert sql("SELECT count(*) FROM operativo.visibilidad_reporte") == "0"
    sql("DROP TABLE operativo.visibilidad_reporte")
    cambio_visibilidad = (raiz / "database/cambios/2026-10-27-visibilidad-reportes.sql").read_text(encoding="utf-8-sig")
    sql(cambio_visibilidad)
    sql("INSERT INTO operativo.visibilidad_reporte (codigo, visible) VALUES ('resumen_estado', FALSE)")
    sql(cambio_visibilidad)
    assert sql("SELECT visible FROM operativo.visibilidad_reporte WHERE codigo = 'resumen_estado'") == "f"
    sql("TRUNCATE operativo.visibilidad_reporte")
    sql("INSERT INTO operativo.registro_error (origen, categoria, codigo) VALUES ('api', 'prueba', 'ERROR_PRUEBA')")
    assert sql("SELECT codigo FROM operativo.registro_error") == "ERROR_PRUEBA"
    sql("TRUNCATE operativo.registro_error RESTART IDENTITY")
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
    print("Init e incrementales de errores y visibilidad equivalentes; repetición idempotente; dump sanitizado regenerado.")
finally:
    if contenedor.startswith("sotra-errores-prueba-"):
        subprocess.run(["docker", "rm", "-f", contenedor], capture_output=True)

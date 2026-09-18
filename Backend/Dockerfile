# Backend/Dockerfile — Fase 1.5 del Plan Maestro ("Contenerización").
#
# Empaqueta el backend FastAPI (server.py) para que corra igual en cualquier
# máquina, sin depender de que cada desarrollador tenga Python/pip
# configurados igual localmente (el objetivo explícito de 1.5 en
# Docs/04_Plan_Maestro_Migracion.md: "facilitando el despliegue y la
# consistencia entre desarrolladores").
#
# También es el primer paso concreto hacia el "visor inalámbrico": este
# contenedor publica el puerto 8000 en 0.0.0.0 (no solo localhost), así que
# cualquier dispositivo en la misma red Wi-Fi — incluido el visor VR
# corriendo la app de Unity — puede llegar a él usando la IP local de la
# máquina que corre el contenedor, sin cambiar una sola línea de código de
# server.py. Ver Docs/02_Backend_Python.md para el comando exacto de
# `docker run` y cómo apuntar Unity (backendUrl en SimulationClient /
# SSEStreamReader) a esa IP en vez de "localhost".
#
# Construir (desde Backend/):    docker build -t virtual-lab-backend .
# Correr:                        docker run --rm -p 8000:8000 virtual-lab-backend
# (o, equivalente:               docker compose up --build   — ver docker-compose.yml)

FROM python:3.10-slim

# PYTHONUNBUFFERED: los prints de utils.emit_progress() (usados por
# Backend/main.py en modo CLI, y como fallback en simulator.py cuando nadie
# registra un callback de progreso — ver simulator.py::_emit_progress) deben
# salir sin buffer para verse en tiempo real vía `docker logs`.
# PYTHONDONTWRITEBYTECODE: evita que se acumulen *.pyc en la imagen.
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1

WORKDIR /app

# Copiar solo requirements.txt primero: Docker cachea esta capa mientras no
# cambien las dependencias, así que editar simulator.py/server.py durante
# desarrollo no obliga a reinstalar fastapi/uvicorn en cada build.
COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY . .

# Usuario sin privilegios: este backend no necesita root para nada de lo que
# hace (no escribe fuera de /app, no abre puertos < 1024).
RUN useradd --create-home --shell /bin/bash appuser \
    && chown -R appuser:appuser /app
USER appuser

EXPOSE 8000

# --host 0.0.0.0 (no 127.0.0.1) para que el puerto publicado sea alcanzable
# desde fuera del contenedor — incluido desde el visor VR por Wi-Fi.
# --reload (el que usa el bloque `if __name__ == "__main__"` de server.py,
# pensado para desarrollo local) se omite a propósito: en un contenedor no
# hay un árbol de archivos "en vivo" que valga la pena vigilar para recargar.
CMD ["uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]

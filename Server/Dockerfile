FROM python:3.13-slim

ENV PYTHONUNBUFFERED=1 \
    PIP_NO_CACHE_DIR=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=0

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    git \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN pip install uv

COPY . /app

WORKDIR /app/Server

RUN uv sync --frozen --no-dev

EXPOSE 8080

ENV PYTHONPATH=/app/Server/src

ENTRYPOINT ["uv", "run", "unity-mcp"]
CMD []

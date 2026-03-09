FROM mcr.microsoft.com/dotnet/sdk:10.0 AS brain-build
WORKDIR /src

COPY brain/ ./brain/
RUN dotnet restore ./brain/src/Web/Web.csproj
RUN dotnet publish ./brain/src/Web/Web.csproj -c Release -o /out/brain

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip python3-venv \
    && rm -rf /var/lib/apt/lists/*

COPY aiworker/requirements.txt /app/aiworker/requirements.txt
RUN python3 -m venv /opt/venv \
    && /opt/venv/bin/pip install --no-cache-dir --upgrade pip \
    && /opt/venv/bin/pip install --no-cache-dir -r /app/aiworker/requirements.txt

COPY aiworker/ /app/aiworker/
COPY --from=brain-build /out/brain/ /app/brain/
COPY scripts/start-brain-ai.sh /app/start-brain-ai.sh

RUN chmod +x /app/start-brain-ai.sh

ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
ENV WEBSITES_PORT=8080
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV External__AIWorkerBaseUrl=http://127.0.0.1:8001
ENV PYTHONUNBUFFERED=1
ENV PATH="/opt/venv/bin:${PATH}"

EXPOSE 8080

ENTRYPOINT ["/app/start-brain-ai.sh"]
# ============================================================================
# UMURA — imagem de produção. Contexto do build = esta pasta (src/).
# Multi-stage: SDK só no build; a imagem final é o runtime ASP.NET (menor).
# Hospedagem ainda não decidida: a imagem roda em qualquer host que fale Docker.
# ============================================================================

# ───── build ─────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# csproj primeiro → o RESTORE fica cacheado entre builds (só invalida se as
# dependências mudarem, não a cada mudança de código).
COPY Agenda_Shows.UI/Agenda_Shows.UI.csproj Agenda_Shows.UI/
COPY Agenda_Shows.Services/Agenda_Shows.Services.csproj Agenda_Shows.Services/
COPY Agenda_Shows.Repositories/Agenda_Shows.Repositories.csproj Agenda_Shows.Repositories/
RUN dotnet restore Agenda_Shows.UI/Agenda_Shows.UI.csproj

COPY . .
# Versão calculada pelo pipeline (--build-arg Version=X.Y.Z); build local fica em 1.0.0.
# O ARG fica DEPOIS do restore de propósito: ARG antes de um RUN entra na chave de
# cache dele, e cada versão nova invalidaria o cache do restore.
ARG Version=1.0.0
RUN dotnet publish Agenda_Shows.UI -c Release -o /app/publish /p:UseAppHost=false /p:Version=$Version

# ───── runtime ─────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# FORWARDEDHEADERS: o TLS termina no proxy do host — sem honrar o X-Forwarded-Proto,
# o redirect pra HTTPS entraria em loop e os cookies Secure não sairiam.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true

# Não-root: a imagem aspnet já define o usuário "app" ($APP_UID).
# 8080 é porta não-privilegiada, então roda sem root em qualquer host.
USER $APP_UID
EXPOSE 8080

# Hosts tipo Railway/Render injetam a porta em $PORT (shell-form de propósito pra
# expandir a var); sem $PORT cai no 8080 (casa com EXPOSE e com probes de K8s).
CMD ["/bin/sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet Agenda_Shows.UI.dll"]

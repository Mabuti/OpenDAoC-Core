#!/bin/sh
set -euo pipefail

# Defaults for UID/GID
APP_UID="${UID:-1000}"
APP_GID="${GID:-1000}"

# Require root
if [ "$(id -u)" != "0" ]; then
  >&2 echo "ERROR: Not running as root. Please run as root and pass UID and GID."
  exit 120
fi

# Determine (or create) the runtime group name that has APP_GID
RUN_GROUP="appgroup"
EXISTING_GROUP_LINE="$(getent group "${APP_GID}" || true)"
if [ -n "${EXISTING_GROUP_LINE}" ]; then
  # Reuse the existing group with that GID
  RUN_GROUP="$(echo "${EXISTING_GROUP_LINE}" | cut -d: -f1)"
else
  # Create our group with requested GID (don't delete any existing groups)
  addgroup -S -g "${APP_GID}" "${RUN_GROUP}" 2>/dev/null || true
fi

# (Re)create the user; ensure PRIMARY group is *not* RUN_GROUP
# We use shadow's usermod (installed in the Dockerfile) to set primary=root.
deluser appuser 2>/dev/null || true
adduser -S -D -H -s /sbin/nologin -u "${APP_UID}" appuser
usermod -g root appuser 2>/dev/null || true
addgroup appuser "${RUN_GROUP}" 2>/dev/null || true

# ----- Generate serverconfig.xml from env -----
rm -f /app/config/serverconfig.xml
cat << EOF > /app/config/serverconfig.xml
<?xml version="1.0" encoding="utf-8"?>
<root>
  <Server>
    <Port>${SERVER_PORT}</Port>
    <IP>${SERVER_IP}</IP>
    <RegionIP>${REGION_IP}</RegionIP>
    <RegionPort>${REGION_PORT}</RegionPort>
    <UdpIP>${UDP_IP}</UdpIP>
    <UdpPort>${UDP_PORT}</UdpPort>
    <EnableUPnP>${ENABLE_UPNP}</EnableUPnP>
    <DetectRegionIP>${DETECT_REGION_IP}</DetectRegionIP>
    <ServerName>${SERVER_NAME}</ServerName>
    <ServerNameShort>${SERVER_NAME_SHORT}</ServerNameShort>
    <LogConfigFile>${LOG_CONFIG_FILE}</LogConfigFile>
    <ScriptCompilationTarget>${SCRIPT_COMPILATION_TARGET}</ScriptCompilationTarget>
    <ScriptAssemblies>${SCRIPT_ASSEMBLIES}</ScriptAssemblies>
    <EnableCompilation>${ENABLE_COMPILATION}</EnableCompilation>
    <AutoAccountCreation>${AUTO_ACCOUNT_CREATION}</AutoAccountCreation>
    <GameType>${GAME_TYPE}</GameType>
    <CheatLoggerName>${CHEAT_LOGGER_NAME}</CheatLoggerName>
    <GMActionLoggerName>${GM_ACTION_LOGGER_NAME}</GMActionLoggerName>
    <InvalidNamesFile>${INVALID_NAMES_FILE}</InvalidNamesFile>
    <DBType>${DB_TYPE}</DBType>
    <DBConnectionString>${DB_CONNECTION_STRING}</DBConnectionString>
    <DBAutosave>${DB_AUTOSAVE}</DBAutosave>
    <DBAutosaveInterval>${DB_AUTOSAVE_INTERVAL}</DBAutosaveInterval>
    <CpuUse>${CPU_USE}</CpuUse>
  </Server>
</root>
EOF

# Ownership (use the resolved group name)
chown -R appuser:"${RUN_GROUP}" /app || true

# Run as unprivileged user
exec su-exec appuser:"${RUN_GROUP}" sh -c 'cd /app && dotnet CoreServer.dll'

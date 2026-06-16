#!/usr/bin/env bash
set -euo pipefail

# Local PostgreSQL helper for Linux or MSYS2 Bash.
# Defaults match SubscriptionManager.api/appsettings.Development.json examples.

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"

PG_BIN="${PG_BIN:-}"
PGDATA="${PGDATA:-$REPO_ROOT/.local/postgres-data}"
PGLOG="${PGLOG:-$REPO_ROOT/.local/postgres.log}"
PGHOST="${PGHOST:-localhost}"
PGPORT="${PGPORT:-5432}"
PGSUPERUSER="${PGSUPERUSER:-postgres}"
APP_DB="${APP_DB:-subtrack}"
APP_USER="${APP_USER:-subtrack}"
APP_PASSWORD="${APP_PASSWORD:-localpass}"

usage() {
  cat <<EOF
Usage: $(basename "$0") <command>

Commands:
  up         Initialize, start, and create/update app role/database
  init       Initialize the local PostgreSQL data directory
  start      Start PostgreSQL
  setup      Create/update app role and database
  ready      Wait until PostgreSQL accepts connections
  stop       Stop PostgreSQL
  restart    Restart PostgreSQL
  status     Show PostgreSQL status
  connstr    Print the API DefaultConnection value
  help       Show this help

Environment overrides:
  PG_BIN        PostgreSQL bin directory, e.g. /usr/lib/postgresql/16/bin
  PGDATA        Data directory. Default: $REPO_ROOT/.local/postgres-data
  PGLOG         Log file. Default: $REPO_ROOT/.local/postgres.log
  PGPORT        Port. Default: 5432
  PGSUPERUSER   Superuser for setup. Default: postgres
  APP_DB        App database. Default: subtrack
  APP_USER      App database user. Default: subtrack
  APP_PASSWORD  App database password. Default: localpass

Example:
  bash scripts/local-postgres.sh up
  PG_BIN="/usr/lib/postgresql/16/bin" bash scripts/local-postgres.sh up
  PG_BIN="/c/Program Files/PostgreSQL/16/bin" bash scripts/local-postgres.sh up
  bash scripts/local-postgres.sh connstr
EOF
}

find_pg_bin() {
  if [[ -n "$PG_BIN" ]]; then
    return
  fi

  local pg_ctl_path
  if pg_ctl_path="$(command -v pg_ctl 2>/dev/null)"; then
    PG_BIN="$(cd -- "$(dirname -- "$pg_ctl_path")" && pwd)"
    return
  fi

  local candidates=(
    "/c/Program Files/PostgreSQL/18/bin" \
    "/c/Program Files/PostgreSQL/17/bin" \
    "/c/Program Files/PostgreSQL/16/bin" \
    "/c/Program Files/PostgreSQL/15/bin" \
    "/ucrt64/bin" \
    "/mingw64/bin" \
    "/usr/lib/postgresql/18/bin" \
    "/usr/lib/postgresql/17/bin" \
    "/usr/lib/postgresql/16/bin" \
    "/usr/lib/postgresql/15/bin" \
    "/usr/lib/postgresql/14/bin" \
    "/usr/lib/postgresql/13/bin" \
    "/usr/lib/postgresql/12/bin" \
    "/usr/pgsql-18/bin" \
    "/usr/pgsql-17/bin" \
    "/usr/pgsql-16/bin" \
    "/usr/pgsql-15/bin" \
    "/usr/pgsql-14/bin" \
    "/usr/pgsql-13/bin" \
    "/usr/pgsql-12/bin" \
    "/usr/local/pgsql/bin" \
    "/usr/local/bin" \
    "/usr/bin"
  )

  local candidate
  for candidate in "${candidates[@]}"; do
    if [[ -x "$candidate/pg_ctl.exe" || -x "$candidate/pg_ctl" ]]; then
      PG_BIN="$candidate"
      return
    fi
  done

  echo "PostgreSQL binaries were not found." >&2
  echo "Set PG_BIN, for example:" >&2
  echo '  PG_BIN="/usr/lib/postgresql/16/bin" ./scripts/local-postgres.sh init' >&2
  echo '  PG_BIN="/c/Program Files/PostgreSQL/16/bin" ./scripts/local-postgres.sh init' >&2
  exit 1
}

pg_exe() {
  local name="$1"
  if [[ -x "$PG_BIN/$name.exe" ]]; then
    printf '%s\n' "$PG_BIN/$name.exe"
  elif [[ -x "$PG_BIN/$name" ]]; then
    printf '%s\n' "$PG_BIN/$name"
  else
    echo "Required PostgreSQL binary not found: $PG_BIN/$name(.exe)" >&2
    exit 1
  fi
}

run_pg_ctl() {
  "$(pg_exe pg_ctl)" -D "$PGDATA" "$@"
}

init_db() {
  mkdir -p "$(dirname -- "$PGDATA")" "$(dirname -- "$PGLOG")"

  if [[ -f "$PGDATA/PG_VERSION" ]]; then
    echo "PostgreSQL data directory already exists: $PGDATA"
    return
  fi

  "$(pg_exe initdb)" -D "$PGDATA" -U "$PGSUPERUSER" -A scram-sha-256 -W
}

start_db() {
  mkdir -p "$(dirname -- "$PGLOG")"

  if run_pg_ctl status >/dev/null 2>&1; then
    echo "PostgreSQL is already running."
    return
  fi

  run_pg_ctl -l "$PGLOG" -o "-p $PGPORT" start
}

ready_db() {
  "$(pg_exe pg_isready)" -h "$PGHOST" -p "$PGPORT" -U "$PGSUPERUSER"
}

wait_ready() {
  local attempts=30
  local i

  for ((i = 1; i <= attempts; i++)); do
    if ready_db >/dev/null 2>&1; then
      echo "PostgreSQL is ready on $PGHOST:$PGPORT."
      return
    fi
    sleep 1
  done

  echo "PostgreSQL did not become ready within ${attempts}s." >&2
  exit 1
}

psql_super() {
  "$(pg_exe psql)" -h "$PGHOST" -p "$PGPORT" -U "$PGSUPERUSER" -d postgres "$@"
}

sql_literal() {
  local value="$1"
  value="${value//\'/\'\'}"
  printf "'%s'" "$value"
}

setup_db() {
  wait_ready

  local app_user_lit app_password_lit app_db_lit
  app_user_lit="$(sql_literal "$APP_USER")"
  app_password_lit="$(sql_literal "$APP_PASSWORD")"
  app_db_lit="$(sql_literal "$APP_DB")"

  psql_super -v ON_ERROR_STOP=1 -c "DO \$\$
DECLARE
  app_user text := $app_user_lit;
  app_password text := $app_password_lit;
BEGIN
  IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = app_user) THEN
    EXECUTE format('CREATE ROLE %I LOGIN PASSWORD %L', app_user, app_password);
  ELSE
    EXECUTE format('ALTER ROLE %I WITH LOGIN PASSWORD %L', app_user, app_password);
  END IF;
END
\$\$;"

  if psql_super -tAc "SELECT 1 FROM pg_database WHERE datname = $app_db_lit" | grep -q 1; then
    echo "Database already exists: $APP_DB"
  else
    "$(pg_exe createdb)" -h "$PGHOST" -p "$PGPORT" -U "$PGSUPERUSER" -O "$APP_USER" "$APP_DB"
  fi
}

connstr() {
  echo "Host=$PGHOST;Port=$PGPORT;Database=$APP_DB;Username=$APP_USER;Password=$APP_PASSWORD"
}

main() {
  local command="${1:-help}"

  case "$command" in
    connstr)
      connstr
      return
      ;;
    help|-h|--help)
      usage
      return
      ;;
  esac

  find_pg_bin

  case "$command" in
    up)
      init_db
      start_db
      setup_db
      ;;
    init)
      init_db
      ;;
    start)
      start_db
      ;;
    setup)
      setup_db
      ;;
    ready)
      wait_ready
      ;;
    stop)
      run_pg_ctl stop
      ;;
    restart)
      run_pg_ctl restart
      ;;
    status)
      run_pg_ctl status
      ;;
    *)
      echo "Unknown command: $command" >&2
      usage >&2
      exit 1
      ;;
  esac
}

main "$@"

@echo off
setlocal EnableExtensions DisableDelayedExpansion

rem Local PostgreSQL helper for Windows Command Prompt.
rem Defaults match SubscriptionManager.api/appsettings.Development.json examples.

set "COMMAND=%~1"
if not defined COMMAND set "COMMAND=help"

set "SCRIPT_DIR=%~dp0"
for %%I in ("%SCRIPT_DIR%..") do set "REPO_ROOT=%%~fI"

if not defined PG_BIN set "PG_BIN="
if not defined PGDATA set "PGDATA=%REPO_ROOT%\.local\postgres-data"
if not defined PGLOG set "PGLOG=%REPO_ROOT%\.local\postgres.log"
if not defined PGHOST set "PGHOST=localhost"
if not defined PGPORT set "PGPORT=5432"
if not defined PGSUPERUSER set "PGSUPERUSER=postgres"
if not defined APP_DB set "APP_DB=subtrack"
if not defined APP_USER set "APP_USER=subtrack"
if not defined APP_PASSWORD set "APP_PASSWORD=localpass"

call :ConvertMsysPath PG_BIN
call :ConvertMsysPath PGDATA
call :ConvertMsysPath PGLOG

if /i "%COMMAND%"=="connstr" call :ConnStr & exit /b
if /i "%COMMAND%"=="help" call :Usage & exit /b 0
if /i "%COMMAND%"=="-h" call :Usage & exit /b 0
if /i "%COMMAND%"=="--help" call :Usage & exit /b 0

call :FindPgBin || exit /b 1

if /i "%COMMAND%"=="up" (
    call :InitDb || exit /b 1
    call :StartDb || exit /b 1
    call :SetupDb
    exit /b
)

if /i "%COMMAND%"=="init" call :InitDb & exit /b
if /i "%COMMAND%"=="start" call :StartDb & exit /b
if /i "%COMMAND%"=="setup" call :SetupDb & exit /b
if /i "%COMMAND%"=="ready" call :WaitReady & exit /b
if /i "%COMMAND%"=="stop" call :PgCtl stop & exit /b
if /i "%COMMAND%"=="restart" call :PgCtl -o "-p %PGPORT%" restart & exit /b
if /i "%COMMAND%"=="status" call :StatusDb & exit /b

echo Unknown command: %COMMAND% 1>&2
call :Usage 1>&2
exit /b 1

:Usage
echo Usage: scripts\local-postgres-windows.bat ^<command^>
echo.
echo Commands:
echo   up         Initialize, start, and create/update app role/database
echo   init       Initialize the local PostgreSQL data directory
echo   start      Start PostgreSQL
echo   setup      Create/update app role and database
echo   ready      Wait until PostgreSQL accepts connections
echo   stop       Stop PostgreSQL
echo   restart    Restart PostgreSQL
echo   status     Show PostgreSQL status
echo   connstr    Print the API DefaultConnection value
echo   help       Show this help
echo.
echo Environment overrides:
echo   PG_BIN        PostgreSQL bin directory, e.g. C:\Program Files\PostgreSQL\16\bin
echo   PGDATA        Data directory. Default: %PGDATA%
echo   PGLOG         Log file. Default: %PGLOG%
echo   PGPORT        Port. Default: 5432
echo   PGSUPERUSER   Superuser for setup. Default: postgres
echo   APP_DB        App database. Default: subtrack
echo   APP_USER      App database user. Default: subtrack
echo   APP_PASSWORD  App database password. Default: localpass
echo.
echo Examples:
echo   set "PG_BIN=C:\Program Files\PostgreSQL\16\bin"
echo   scripts\local-postgres-windows.bat up
echo   scripts\local-postgres-windows.bat connstr
exit /b 0

:ConvertMsysPath
set "_var=%~1"
call set "_value=%%%_var%%%"
if not defined _value exit /b 0
set "_converted=%_value:/=\%"
set "%_var%=%_converted%"
if not "%_converted:~0,1%"=="\" exit /b 0
if not "%_converted:~2,1%"=="\" exit /b 0
set "_drive=%_converted:~1,1%"
set "_tail=%_converted:~2%"
set "%_var%=%_drive%:%_tail%"
exit /b 0

:FindPgBin
if defined PG_BIN exit /b 0

for %%C in (pg_ctl.exe pg_ctl) do (
    for /f "delims=" %%P in ('where %%C 2^>nul') do (
        for %%D in ("%%~dpP.") do set "PG_BIN=%%~fD"
        exit /b 0
    )
)

for %%R in ("%ProgramFiles%" "%ProgramFiles(x86)%") do (
    if not "%%~R"=="" (
        for %%V in (18 17 16 15) do (
            if exist "%%~R\PostgreSQL\%%V\bin\pg_ctl.exe" (
                set "PG_BIN=%%~R\PostgreSQL\%%V\bin"
                exit /b 0
            )
        )
    )
)

for %%D in ("C:\msys64\ucrt64\bin" "C:\msys64\mingw64\bin") do (
    if exist "%%~D\pg_ctl.exe" (
        set "PG_BIN=%%~D"
        exit /b 0
    )
)

echo PostgreSQL binaries were not found. 1>&2
echo Set PG_BIN, for example: 1>&2
echo   set "PG_BIN=C:\Program Files\PostgreSQL\16\bin" 1>&2
echo   scripts\local-postgres-windows.bat init 1>&2
exit /b 1

:GetPgExe
set "PG_EXE="
if exist "%PG_BIN%\%~1.exe" (
    set "PG_EXE=%PG_BIN%\%~1.exe"
    exit /b 0
)
if exist "%PG_BIN%\%~1" (
    set "PG_EXE=%PG_BIN%\%~1"
    exit /b 0
)
echo Required PostgreSQL binary not found: %PG_BIN%\%~1(.exe) 1>&2
exit /b 1

:EnsureParentDirectory
for %%D in ("%~1") do (
    if not exist "%%~dpD" mkdir "%%~dpD"
)
exit /b

:InitDb
call :EnsureParentDirectory "%PGDATA%" || exit /b 1
call :EnsureParentDirectory "%PGLOG%" || exit /b 1
if exist "%PGDATA%\PG_VERSION" (
    echo PostgreSQL data directory already exists: %PGDATA%
    exit /b 0
)
call :GetPgExe initdb || exit /b 1
"%PG_EXE%" -D "%PGDATA%" -U "%PGSUPERUSER%" -A scram-sha-256 -W
exit /b %ERRORLEVEL%

:TestPgRunning
call :GetPgExe pg_ctl || exit /b 1
"%PG_EXE%" -D "%PGDATA%" status >nul 2>nul
exit /b %ERRORLEVEL%

:StartDb
call :EnsureParentDirectory "%PGLOG%" || exit /b 1
call :TestPgRunning
if not errorlevel 1 (
    echo PostgreSQL is already running.
    exit /b 0
)
call :GetPgExe pg_ctl || exit /b 1
"%PG_EXE%" -D "%PGDATA%" -l "%PGLOG%" -o "-p %PGPORT%" start
exit /b %ERRORLEVEL%

:TestReady
call :GetPgExe pg_isready || exit /b 1
"%PG_EXE%" -h "%PGHOST%" -p "%PGPORT%" -U "%PGSUPERUSER%" >nul 2>nul
exit /b %ERRORLEVEL%

:WaitReady
for /l %%I in (1,1,30) do (
    call :TestReady
    if not errorlevel 1 (
        echo PostgreSQL is ready on %PGHOST%:%PGPORT%.
        exit /b 0
    )
    timeout /t 1 /nobreak >nul
)
echo PostgreSQL did not become ready within 30s. 1>&2
exit /b 1

:SqlLiteral
set "_source=%~1"
set "_target=%~2"
call set "_literal_value=%%%_source%%%"
set "_literal_value=%_literal_value:'=''%"
set "%_target%='%_literal_value%'"
exit /b 0

:SetupDb
call :WaitReady || exit /b 1
call :SqlLiteral APP_USER APP_USER_LIT
call :SqlLiteral APP_PASSWORD APP_PASSWORD_LIT
call :SqlLiteral APP_DB APP_DB_LIT

set "ROLE_SQL=DO $$ DECLARE app_user text := %APP_USER_LIT%; app_password text := %APP_PASSWORD_LIT%; BEGIN IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = app_user) THEN EXECUTE format('CREATE ROLE %%I LOGIN PASSWORD %%L', app_user, app_password); ELSE EXECUTE format('ALTER ROLE %%I WITH LOGIN PASSWORD %%L', app_user, app_password); END IF; END $$;"

call :GetPgExe psql || exit /b 1
"%PG_EXE%" -h "%PGHOST%" -p "%PGPORT%" -U "%PGSUPERUSER%" -d postgres -v ON_ERROR_STOP=1 -c "%ROLE_SQL%"
if errorlevel 1 exit /b %ERRORLEVEL%

set "DB_EXISTS="
for /f "delims=" %%A in ('"%PG_EXE%" -h "%PGHOST%" -p "%PGPORT%" -U "%PGSUPERUSER%" -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = %APP_DB_LIT%"') do set "DB_EXISTS=%%A"

if "%DB_EXISTS%"=="1" (
    echo Database already exists: %APP_DB%
    exit /b 0
)

call :GetPgExe createdb || exit /b 1
"%PG_EXE%" -h "%PGHOST%" -p "%PGPORT%" -U "%PGSUPERUSER%" -O "%APP_USER%" "%APP_DB%"
exit /b %ERRORLEVEL%

:PgCtl
call :GetPgExe pg_ctl || exit /b 1
"%PG_EXE%" -D "%PGDATA%" %*
exit /b %ERRORLEVEL%

:StatusDb
call :GetPgExe pg_ctl || exit /b 1
"%PG_EXE%" -D "%PGDATA%" status
exit /b %ERRORLEVEL%

:ConnStr
echo Host=%PGHOST%;Port=%PGPORT%;Database=%APP_DB%;Username=%APP_USER%;Password=%APP_PASSWORD%
exit /b 0

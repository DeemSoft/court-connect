# CourtConnect

Aplicación de práctica para organizar partidos de baloncesto callejero: canchas, plazas e inscripciones de jugadores. En las siguientes partes del reto se añadirán torneos y pagos simulados.

## Estado

El dominio, la persistencia con EF Core, la API y la web están implementados. La API usa ASP.NET Core Identity con roles `Organizer` y `Player`; cada jugador queda enlazado a su cuenta y solo puede gestionar sus propias inscripciones.

## Desarrollo

Se necesita el SDK de .NET 10. Para compilar y ejecutar las pruebas:

```powershell
dotnet build CourtConnect.slnx
dotnet test CourtConnect.slnx
```

Las pruebas de integración arrancan su propio SQL Server temporal con Testcontainers, así que Docker Desktop debe estar en ejecución. Para recoger cobertura excluyendo código generado:

```powershell
dotnet test CourtConnect.slnx --settings coverlet.runsettings --collect:"XPlat Code Coverage"
```

`global.json` selecciona el SDK 10.0.403 y permite avanzar a otra versión de la misma línea 10.0 cuando sea necesario.

## Ejecutar la aplicación con Docker Compose

Se necesita Docker Desktop en ejecución. Crea el fichero local de variables de entorno a partir de la plantilla y sustituye los dos valores por contraseñas fuertes, únicas y locales:

```powershell
Copy-Item .env.example .env
```

`.env` contiene la contraseña de SQL Server y la contraseña de las cuentas de desarrollo; está excluido de Git. `.env.example` solo contiene marcadores y no debe usarse sin reemplazarlos. No compartas ni subas tu `.env`.

El puerto SQL publicado por defecto es `1433`. Si ya está ocupado, cambia `MSSQL_HOST_PORT` en `.env` (por ejemplo, a `1434`); para conectarte desde el host usa entonces ese mismo puerto.

Construye las dos imágenes y arranca SQL Server, la API y la web:

```powershell
docker compose up --build
```

Abre `http://localhost:8080`. La cuenta organizadora de desarrollo es `organizer@example.com`; las cuentas de jugador son `maria@example.com`, `pau@example.com`, `irene@example.com`, `leo@example.com` y `sara@example.com`. Todas usan el valor local de `IDENTITY_DEVELOPMENT_PASSWORD` de `.env`. La API está disponible en `http://localhost:5258`.

Compose mantiene los datos de SQL Server en el volumen `courtconnect-data`, espera a que SQL Server supere su comprobación de salud antes de iniciar la API y espera a que la API responda antes de iniciar la web. Para detener los servicios conserva los datos con `docker compose down`; `docker compose down -v` también elimina el volumen y la base de datos.

En este entorno local la API usa `Development`: aplica las migraciones al arrancar y crea las cuentas de prueba. Esto evita un paso manual para el ejercicio, pero no es apropiado para producción: si varias réplicas arrancan a la vez pueden competir al aplicar las mismas migraciones. En producción, ejecútalas como un paso de despliegue único y controlado, y no uses las cuentas ni las contraseñas de desarrollo.

## Base de datos para desarrollo local sin Compose completo

También se puede usar solo SQL Server en Docker, después de crear el `.env` descrito arriba:

```powershell
docker compose up -d sqlserver
```

Configura la cadena de conexión mediante User Secrets en el proyecto `CourtConnect.Api` (usa el mismo valor local definido para `MSSQL_SA_PASSWORD`):

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=CourtConnect;User Id=sa;Password=<contraseña-local>;TrustServerCertificate=True;Encrypt=False" --project src/CourtConnect.Api
```

Configura además `Identity:DevelopmentPassword` con `dotnet user-secrets` si quieres crear las cuentas de prueba al arrancar la API localmente.

## API local

Con SQL Server iniciado y la cadena de conexión configurada, arranca la API con:

```powershell
dotnet run --project src/CourtConnect.Api
```

En Development, la API aplica las migraciones al arrancar. Para crear las cuentas locales de prueba, configura una contraseña solo en User Secrets:

```powershell
dotnet user-secrets set "Identity:DevelopmentPassword" "<contraseña-fuerte-local>" --project src/CourtConnect.Api
```

La cuenta organizadora de prueba es `organizer@example.com`. También se crean cuentas de jugador para los jugadores semilla (`maria@example.com`, `pau@example.com`, `irene@example.com`, `leo@example.com` y `sara@example.com`); todas usan la contraseña local configurada arriba. Si no se configura esa clave, la API arranca igualmente, pero no crea las cuentas de prueba.

Al iniciar sesión como organizador, el panel interactivo `/organizer` lista y permite ordenar los propios partidos. La página `/organizer/games/new` crea partidos; desde el detalle se pueden abrir o cerrar y consultar las inscripciones mostrando únicamente apodo y nombre. La pantalla `/organizer/courts` permite crear, editar y eliminar pistas; no se puede eliminar una pista que tenga partidos asociados.

El documento OpenAPI está disponible en `http://localhost:5258/openapi/v1.json`. Las peticiones de ejemplo, incluido el flujo de registro y autenticación, están en [`src/CourtConnect.Api/CourtConnect.Api.http`](src/CourtConnect.Api/CourtConnect.Api.http). La cancelación de una inscripción ajena devuelve `404`, igual que una inscripción inexistente, para no revelar si existe.

## Web local

Con la API y SQL Server en ejecución, inicia la aplicación Blazor con:

```powershell
dotnet run --project src/CourtConnect.Ui.Web
```

La URL base de la API se configura en `src/CourtConnect.Ui.Web/appsettings.json` (`CourtConnectApi:BaseUrl`).

La CI construye las imágenes de API y web en cada Pull Request y cada push a `main`; no las publica en un registro.

La web permite crear una cuenta de jugador, iniciar sesión y cerrar sesión. La sesión se mantiene con una cookie protegida; el token de Identity permanece en el servidor y se reenvía a la API mediante un `DelegatingHandler`.

Los jugadores también tienen la pestaña «Inscripciones», donde pueden consultar sus partidos activos y darse de baja si faltan más de 72 horas para el inicio.

## Dominio

Consulta [`docs/domain.md`](docs/domain.md).

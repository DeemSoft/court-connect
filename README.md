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

## Base de datos local

Se puede arrancar SQL Server en Docker con:

```powershell
docker compose up -d sqlserver
```

La cadena de conexión se configura con `dotnet user-secrets` en el proyecto `CourtConnect.Api`:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=CourtConnect;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False" --project src/CourtConnect.Api
```

No se guarda ninguna contraseña en el repositorio.

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

Al iniciar sesión como organizador, el panel `/organizer` permite crear partidos en las pistas existentes, editar los propios y abrirlos o cerrarlos.

El documento OpenAPI está disponible en `http://localhost:5258/openapi/v1.json`. Las peticiones de ejemplo, incluido el flujo de registro y autenticación, están en [`src/CourtConnect.Api/CourtConnect.Api.http`](src/CourtConnect.Api/CourtConnect.Api.http). La cancelación de una inscripción ajena devuelve `404`, igual que una inscripción inexistente, para no revelar si existe.

## Web local

Con la API y SQL Server en ejecución, inicia la aplicación Blazor con:

```powershell
dotnet run --project src/CourtConnect.Ui.Web
```

La URL base de la API se configura en `src/CourtConnect.Ui.Web/appsettings.json` (`CourtConnectApi:BaseUrl`).

La web permite crear una cuenta de jugador, iniciar sesión y cerrar sesión. La sesión se mantiene con una cookie protegida; el token de Identity permanece en el servidor y se reenvía a la API mediante un `DelegatingHandler`.

Los jugadores también tienen la pestaña «Inscripciones», donde pueden consultar sus partidos activos.

## Dominio

Consulta [`docs/domain.md`](docs/domain.md).

# CourtConnect

Aplicación de práctica para organizar partidos de baloncesto callejero: canchas, plazas e inscripciones de jugadores. En las siguientes partes del reto se añadirán torneos y pagos simulados.

## Estado

El dominio, la persistencia con EF Core y los endpoints REST principales están implementados. La API todavía no incluye autenticación.

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

En Development, el documento OpenAPI está disponible en `http://localhost:5258/openapi/v1.json`. Las peticiones de ejemplo y algunos casos de error están en [`src/CourtConnect.Api/CourtConnect.Api.http`](src/CourtConnect.Api/CourtConnect.Api.http).

## Web local

Con la API y SQL Server en ejecución, inicia la aplicación Blazor con:

```powershell
dotnet run --project src/CourtConnect.Ui.Web
```

La URL base de la API se configura en `src/CourtConnect.Ui.Web/appsettings.json` (`CourtConnectApi:BaseUrl`).

## Dominio

Consulta [`docs/domain.md`](docs/domain.md).

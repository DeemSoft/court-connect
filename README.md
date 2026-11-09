# CourtConnect

Aplicación de práctica para organizar partidos de baloncesto callejero: canchas, plazas e inscripciones de jugadores. En las siguientes partes del reto se añadirán torneos y pagos simulados.

## Estado

El dominio ya está implementado y el ejercicio 2 añade la capa de infraestructura con EF Core y la API de arranque para la base de datos.

## Desarrollo

Se necesita el SDK de .NET 10. Para compilar y ejecutar las pruebas:

```powershell
dotnet build CourtConnect.slnx
dotnet test CourtConnect.slnx
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

## Dominio

Consulta [`docs/domain.md`](docs/domain.md).

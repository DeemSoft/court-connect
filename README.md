# CourtConnect

Aplicación de práctica para organizar partidos de baloncesto callejero: canchas, plazas e inscripciones de jugadores. En las siguientes partes del reto se añadirán torneos y pagos simulados.

## Estado

El modelo de dominio y sus pruebas unitarias están en desarrollo. Todavía no hay base de datos ni API.

## Desarrollo

Se necesita el SDK de .NET 10. Para compilar y ejecutar las pruebas:

```powershell
dotnet build CourtConnect.slnx
dotnet test CourtConnect.slnx
```

`global.json` selecciona el SDK 10.0.403 y permite avanzar a otra versión de la misma línea 10.0 cuando sea necesario.

## Dominio

Consulta [`docs/domain.md`](docs/domain.md).

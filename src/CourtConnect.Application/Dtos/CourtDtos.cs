using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

/// <summary>Datos necesarios para crear una pista.</summary>
public sealed record CreateCourtRequest(
    [property: Required]
    [property: MaxLength(100)]
    string Name,
    [property: Required]
    [property: MaxLength(200)]
    string Location);

/// <summary>Datos para actualizar una pista existente.</summary>
public sealed record UpdateCourtRequest(
    [property: Required]
    [property: MaxLength(100)]
    string Name,
    [property: Required]
    [property: MaxLength(200)]
    string Location);

/// <summary>Representa una pista devuelta por la API.</summary>
public sealed record CourtDto(Guid Id, string Name, string Location);

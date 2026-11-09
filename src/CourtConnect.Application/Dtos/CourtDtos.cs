using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

public sealed record CreateCourtRequest(
    [property: Required]
    [property: MaxLength(100)]
    string Name,
    [property: Required]
    [property: MaxLength(200)]
    string Location);

public sealed record CourtDto(Guid Id, string Name, string Location);

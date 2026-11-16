using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

public sealed record CreatePlayerRequest(
    [property: Required]
    [property: MaxLength(50)]
    string Nickname,
    [property: Required]
    [property: MaxLength(150)]
    string FullName,
    [property: Required] DateOnly? DateOfBirth,
    [property: Required]
    [property: EmailAddress]
    [property: MaxLength(200)]
    string Email);

public sealed record PlayerDto(Guid Id, string Nickname, string FullName, DateOnly DateOfBirth, string Email);

public sealed record PlayerOptionDto(Guid Id, string Nickname);

using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

/// <summary>Creates a player profile linked to the authenticated Identity account.</summary>
public sealed record CreatePlayerProfileRequest(
    [property: Required]
    [property: MaxLength(50)]
    string Nickname,
    [property: Required]
    [property: MaxLength(150)]
    string FullName,
    [property: Required] DateOnly? DateOfBirth);

public sealed record PlayerDto(Guid Id, string Nickname, string FullName, DateOnly DateOfBirth, string Email);

public sealed record PlayerOptionDto(Guid Id, string Nickname);

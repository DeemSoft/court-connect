using System.ComponentModel.DataAnnotations;

namespace DeemSoft.CourtConnect.Application.Dtos;

public sealed record RegistrationDto(
    Guid Id,
    Guid GameId,
    Guid PlayerId,
    DateTimeOffset CreatedAt,
    decimal Amount,
    string Status);

public sealed record PlayerRegistrationDto(RegistrationDto Registration, GameDto Game);

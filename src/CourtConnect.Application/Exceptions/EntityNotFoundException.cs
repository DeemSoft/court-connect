namespace DeemSoft.CourtConnect.Application.Exceptions;

public sealed class EntityNotFoundException(string entityName, Guid id)
    : Exception($"{entityName} with id '{id}' was not found.");

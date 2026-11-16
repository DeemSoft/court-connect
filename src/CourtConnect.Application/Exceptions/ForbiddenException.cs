namespace DeemSoft.CourtConnect.Application.Exceptions;

public sealed class ForbiddenException(string message) : Exception(message);

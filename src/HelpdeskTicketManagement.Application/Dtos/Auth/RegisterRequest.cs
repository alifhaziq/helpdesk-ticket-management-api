namespace HelpdeskTicketManagement.Application.Dtos.Auth;

public sealed record RegisterRequest(string FullName, string Email, string Password);

namespace Notes.Interface.DTO;

public record HttpResult(int StatusCode, object? Content = null);
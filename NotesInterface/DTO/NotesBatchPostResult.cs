using Microsoft.AspNetCore.Http;

namespace Notes.Interface.DTO;

public record NotesBatchPostResult(IResult[] Results);
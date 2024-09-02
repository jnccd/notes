using NotesServer;
using NotesServer.Notes;

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureWebhost();
builder.RegisterServices();

var app = builder.Build();
app.RegisterMiddlewares();
app.RegisterNotesEndpoints(app.Services);
app.Run();
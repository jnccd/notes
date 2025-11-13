using NotesServer;

DotNetEnv.Env.Load("../.env", new(setEnvVars: true));

var builder = WebApplication.CreateBuilder(args);
builder.ConfigureWebhost();
builder.RegisterServices();

var app = builder.Build();
app.RegisterMiddlewares();
app.ConfigureWebApp();
app.RegisterNotesEndpoints(app.Services);
app.Run();

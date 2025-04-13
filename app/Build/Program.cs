using Build.Commands;

var builder = CoconaApp.CreateBuilder();
var app = builder.Build();
app.AddCommands<CheckRidsCommand>();
app.Run();
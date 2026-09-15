using Microsoft.AspNetCore.Mvc;

var app = WebApplication.Create();
app.MapGet("/q", ([FromQuery] IntOrBool v) => v.Value?.ToString());
app.Run();

public union IntOrBool(int, bool);

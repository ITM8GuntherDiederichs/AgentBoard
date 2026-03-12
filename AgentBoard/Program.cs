using AgentBoard.Api;
using AgentBoard.Components;
using AgentBoard.Data;
using AgentBoard.Hubs;
using AgentBoard.Services;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<TodoService>();
builder.Services.AddScoped<ProjectService>();
builder.Services.AddScoped<FeatureRequestService>();
builder.Services.AddHostedService<ClaimExpiryService>();

builder.Services.AddMudServices();

var app = builder.Build();

// Run EF migrations on startup (dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Status code pages only for non-API routes — API endpoints return RFC-compliant status codes directly.
app.UseWhen(
    ctx => !ctx.Request.Path.StartsWithSegments("/api"),
    branch => branch.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true));
app.UseHttpsRedirection();

// UseStaticFiles serves _content/{LibraryName}/ paths for Razor Class Library packages
// (e.g. _content/MudBlazor/MudBlazor.min.css). MapStaticAssets() alone does not intercept
// these paths in development — it only handles fingerprinted app-level assets.
app.UseStaticFiles();

app.UseAntiforgery();

app.MapTodoEndpoints();
app.MapProjectEndpoints();
app.MapFeatureRequestEndpoints();

app.MapHub<AgentBoardHub>("/hubs/agentboard");

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

public partial class Program { }

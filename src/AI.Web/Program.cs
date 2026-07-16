using cCoder.AI;
using cCoder.AI.Models.Configurations;
using AI.Web.Services.Diagnostics;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IAgentRunHistoryService, AgentRunHistoryService>();
builder.Services.AddAI((_, configuration) =>
{
    builder.Configuration.GetSection(AIConfiguration.SectionName).Bind(configuration);
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Acceptance"))
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program;

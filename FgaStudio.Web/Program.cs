using FgaStudio.Web.Configuration;
using FgaStudio.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.Configure<FgaStudioSettings>(
    builder.Configuration.GetSection(FgaStudioSettings.Section));
builder.Services.AddSingleton<ConnectionManager>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

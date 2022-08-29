using FubarDev.FtpServer;
using FubarDev.FtpServer.FileSystem.DotNet;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.Configure<DotNetFileSystemOptions>(opt => opt.RootPath = Path.Combine("wwwroot", "drive"));
builder.Services.AddFtpServer(builder => builder
    .UseDotNetFileSystem()
    .EnableAnonymousAuthentication());
builder.Services.Configure<FtpServerOptions>(opt => opt.ServerAddress = "localhost");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment()) {
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

var ftpHost = app.Services.GetRequiredService<IFtpServerHost>();
await ftpHost.StartAsync();

app.Run();
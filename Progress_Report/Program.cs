using Progress_RepositoryLayer.IRepository;
using Progress_RepositoryLayer.Repository;
using Progress_ServiceLayer.ICustomServices;
using Progress_ServiceLayer.CustomServices;
using Microsoft.EntityFrameworkCore;
using Progress_DomainLayer.Data;
using Progress_DomainLayer.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<Progress_DomainLayer.Data.SQLiteDBContext>(options =>
options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.
builder.Services.AddControllersWithViews();

#region Service Injected 
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ICustomService<Mark>, MarkService>();
#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

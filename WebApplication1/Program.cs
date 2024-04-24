using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddSqlServerDbContext<AppDbContext>("db");

var app = builder.Build();

app.MapGet("/", async (AppDbContext db) =>
{
    await db.Database.EnsureCreatedAsync();

    var product = new Product { Name = Guid.NewGuid().ToString("d") };
    db.Products.Add(product);
    await db.SaveChangesAsync();

    return await db.Products.ToListAsync();
});

app.Run();

class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products { get; set; }
}

class Product 
{
    public int Id { get; set; }
    public string Name { get; set; } = default!;
}
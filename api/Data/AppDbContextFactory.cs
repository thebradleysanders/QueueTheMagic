using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace XlightsQueue.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> {
    public AppDbContext CreateDbContext(string[] args) {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=qtm.db")
            .Options;
        return new AppDbContext(options);
    }
}

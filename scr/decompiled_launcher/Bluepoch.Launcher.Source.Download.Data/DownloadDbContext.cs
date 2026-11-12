using System.Threading.Tasks;
using Bluepoch.Launcher.Source.Download.Files;
using Microsoft.EntityFrameworkCore;

namespace Bluepoch.Launcher.Source.Download.Data;

internal class DownloadDbContext : DbContext
{
	private string DbPath;

	public DbSet<MultiFilesInfo> multiFilesInfo { get; set; }

	public DownloadDbContext(string dbPath)
	{
		DbPath = dbPath;
	}

	protected override void OnConfiguring(DbContextOptionsBuilder options)
	{
		options.UseSqlite("Data Source=" + DbPath);
	}

	public void ClearAllData()
	{
		Database.ExecuteSqlRaw("DELETE FROM multiFilesInfo");
		Database.ExecuteSqlRaw("DELETE FROM sqlite_sequence WHERE name='multiFilesInfo'");
	}

	public async Task ClearAllDataAsync()
	{
		await Database.ExecuteSqlRawAsync("DELETE FROM multiFilesInfo");
		await Database.ExecuteSqlRawAsync("DELETE FROM sqlite_sequence WHERE name='multiFilesInfo'");
	}
}

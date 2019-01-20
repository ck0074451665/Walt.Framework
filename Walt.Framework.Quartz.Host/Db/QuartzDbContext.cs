
using System.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MySql.Data.EntityFrameworkCore;

namespace Walt.Framework.Quartz
{
    public class  QuartzDbContext:DbContext
    {
         public QuartzDbContext(DbContextOptions<QuartzDbContext> options)
            : base(options)
        { }

         protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }

         protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                modelBuilder.Entity<QuartzTask>(q=>{
                    q.HasKey(HelloJob=>HelloJob.TaskID);
                });
        }

        public DbSet<QuartzTask> QuartzTask{get;set;}
  
    }
}
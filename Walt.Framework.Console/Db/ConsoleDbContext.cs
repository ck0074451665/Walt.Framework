
using System.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using MySql.Data.EntityFrameworkCore;

namespace Walt.Framework.Console
{
    public class  ConsoleDbContext:DbContext
    {
         public ConsoleDbContext(DbContextOptions<ConsoleDbContext> options)
            : base(options)
        { }

         protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {

        }

         protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
                // modelBuilder.Entity<QuartzTask>(q=>{
                //     q.HasKey(HelloJob=>HelloJob.TaskID);
                // });
        }

        //public DbSet<QuartzTask> QuartzTask{get;set;}
  
    }
}
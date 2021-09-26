using Microsoft.EntityFrameworkCore;
using System;
using System.Reflection;

namespace mPUObserver
{
    public class MPUDataContext : DbContext
    {
        public DbSet<Force> Forces { get; set; }
        public string fileName { get; set; } 
        public MPUDataContext(string fileName) : base()
        {
            this.fileName = fileName;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite(string.Format("Filename={0}", fileName), options =>
            {
                options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName);
            });
            base.OnConfiguring(optionsBuilder);
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Map table names
            modelBuilder.Entity<Force>();
            base.OnModelCreating(modelBuilder);
        }
    }

    /// <summary>
    /// Blog entity
    /// </summary>
    [Index(nameof(time))]
    public class Force
    {
        public int ForceId { get; set; }
        public long time { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public double rX { get; set; }
        public double rY { get; set; }
        public double rZ { get; set; }
        public double rW { get; set; }
        public double Speed { get; set; }
        public double RPM { get; set; }
        public long SampleElapseTime { get; set; }
    }
}

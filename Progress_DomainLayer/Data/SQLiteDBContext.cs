using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Progress_DomainLayer.Models;

namespace Progress_DomainLayer.Data
{
    public class SQLiteDBContext: DbContext
    {
        public SQLiteDBContext(DbContextOptions<SQLiteDBContext> options) : base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
        public DbSet<Mark> Marks { get; set; }
    }
}

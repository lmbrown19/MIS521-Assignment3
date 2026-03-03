using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Assignment_3.Models;

namespace Assignment_3.Data
{
    public class Assignment_3Context : DbContext
    {
        public Assignment_3Context (DbContextOptions<Assignment_3Context> options)
            : base(options)
        {
        }

        public DbSet<Assignment_3.Models.Movie> Movie { get; set; } = default!;
        public DbSet<Assignment_3.Models.Actor> Actor { get; set; } = default!;
        public DbSet<Assignment_3.Models.MovieActors> MovieActors { get; set; } = default!;
    }
}

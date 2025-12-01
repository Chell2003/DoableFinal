using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DoableFinal.Migrations
{
    [DbContext(typeof(DoableFinal.Data.ApplicationDbContext))]
    [Migration("20251201120000_AddImagePathToHomePageSections")]
    partial class AddImagePathToHomePageSections
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.0")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            modelBuilder.Entity("DoableFinal.Models.HomePageSection", b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("SectionKey").IsRequired().HasMaxLength(100);
                b.Property<string>("DisplayName").IsRequired().HasMaxLength(500);
                b.Property<string>("Content").IsRequired();
                b.Property<string>("IconClass").IsRequired(false);
                b.Property<int?>("SectionOrder").IsRequired(false);
                b.Property<DateTime>("CreatedAt");
                b.Property<DateTime?>("UpdatedAt").IsRequired(false);
                b.Property<string>("UpdatedBy").IsRequired(false);
                b.Property<string>("ImagePath").IsRequired(false);
                b.HasKey("Id");
                b.ToTable("HomePageSections");
            });
        }
    }
}

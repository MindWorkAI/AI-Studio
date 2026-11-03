#nullable disable

using AIStudio.Tools.Databases.IndexStore;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace AIStudio.Tools.Databases.ProvenanceStore.Migrations;

// EF Core writes this file and declares the type as partial. Dropping the keyword would only
// last until the next migration is added, so the inspection is silenced instead.
// ReSharper disable once PartialTypeWithSinglePart
[DbContext(typeof(ProvenanceStoreDbContext))]
partial class ProvenanceStoreDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder.HasAnnotation("ProductVersion", "9.0.18");
        var utcDateTimeOffsetConverter = new IndexStoreDateTimeOffsetConverter();

        modelBuilder.Entity("AIStudio.Tools.Databases.ProvenanceStore.ProvenanceEntity", entity =>
        {
            entity.Property<string>("ChatId")
                .HasColumnType("TEXT")
                .HasColumnName("chat_id");

            entity.Property<string>("Id")
                .HasColumnType("TEXT")
                .HasColumnName("id");

            entity.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("content");

            entity.Property<DateTimeOffset>("CreatedAtUtc")
                .HasConversion(utcDateTimeOffsetConverter)
                .HasColumnType("TEXT")
                .HasColumnName("created_at_utc");

            entity.Property<string>("Kind")
                .IsRequired()
                .HasColumnType("TEXT")
                .HasColumnName("kind");

            entity.Property<string>("SurroundingContent")
                .HasColumnType("TEXT")
                .HasColumnName("surrounding_content");

            entity.HasKey("ChatId", "Id");

            entity.HasIndex("Id")
                .HasDatabaseName("idx_provenance_id");

            entity.ToTable("provenance");
        });
#pragma warning restore 612, 618
    }
}
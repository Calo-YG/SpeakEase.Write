using SpeakEase.Write.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations.Users;

internal sealed class UserPreferenceEntityConfiguration : IEntityTypeConfiguration<UserPreferenceEntity>
{
    public void Configure(EntityTypeBuilder<UserPreferenceEntity> builder)
    {
        builder.ToTable("user_preferences");
        builder.ConfigureBaseEntity();
        builder.Property(x => x.UserId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.DefaultGenre).HasMaxLength(64);
        builder.Property(x => x.NarrativeStyle).HasMaxLength(64);
        builder.Property(x => x.WritingStyle).HasMaxLength(64);
        builder.Property(x => x.EditorPreferenceJson).HasColumnType("text");
        builder.Property(x => x.PromptPreferences).HasColumnType("text");
        builder.Property(x => x.PromptPreferences).ConfigureStringDictionaryProperty<UserPreferenceEntity>();
    }
}

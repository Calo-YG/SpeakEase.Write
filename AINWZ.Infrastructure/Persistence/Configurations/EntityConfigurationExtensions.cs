using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SpeakEase.Write.Domain;

namespace SpeakEase.Write.Infrastructure.Persistence.Configurations;

/// <summary>
/// 通用实体配置扩展�?/// </summary>
internal static class EntityConfigurationExtensions
{
    public static void ConfigureBaseEntity<TEntity>(this EntityTypeBuilder<TEntity> builder) where TEntity : Entity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasMaxLength(64);
        builder.Property(x => x.CreateBy).HasMaxLength(64);
        builder.Property(x => x.UpdateBy).HasMaxLength(64);
    }

    public static void ConfigureStringListProperty<TEntity>(this PropertyBuilder<List<string>> propertyBuilder)
    {
        propertyBuilder.HasConversion(JsonValueConverterFactory.CreateStringListConverter());
        propertyBuilder.Metadata.SetValueComparer(JsonValueConverterFactory.CreateStringListComparer());
    }

    public static void ConfigureStringDictionaryProperty<TEntity>(this PropertyBuilder<Dictionary<string, string>> propertyBuilder)
    {
        propertyBuilder.HasConversion(JsonValueConverterFactory.CreateStringDictionaryConverter());
        propertyBuilder.Metadata.SetValueComparer(JsonValueConverterFactory.CreateStringDictionaryComparer());
    }
}

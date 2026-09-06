using Microsoft.EntityFrameworkCore;
using SpeakEase.Write.Domain.Entities.Works;

namespace AINWZ.Tests.Performance;

public sealed class HotQueryTests
{
    [Fact]
    public void WorkModel_HasOwnerAndUpdateIndex()
    {
        using var db = AINWZ.Tests.AI.TestDb.Create();
        var entityType = db.Model.FindEntityType(typeof(WorkEntity));

        Assert.NotNull(entityType);
        Assert.Contains(entityType.GetIndexes(), index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(WorkEntity.UserId), nameof(WorkEntity.UpdateAt)]));
    }
}

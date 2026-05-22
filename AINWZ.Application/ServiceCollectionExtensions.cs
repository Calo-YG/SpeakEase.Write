using Microsoft.Extensions.DependencyInjection;

using SpeakEase.Write.Application.Applications;
using SpeakEase.Write.Application.Contracts.AI;
using SpeakEase.Write.Application.Contracts.Auth;
using SpeakEase.Write.Application.Contracts.Creation;
using SpeakEase.Write.Application.Contracts.Dashboard;
using SpeakEase.Write.Application.Contracts.References;
using SpeakEase.Write.Application.Contracts.Story;
using SpeakEase.Write.Application.Contracts.Tags;
using SpeakEase.Write.Application.Contracts.Users;
using SpeakEase.Write.Application.Contracts.Version;
using SpeakEase.Write.Application.Contracts.Works;
using SpeakEase.Write.Application.Novel.Export;

namespace SpeakEase.Write.Application;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<ILLMCallLogApplication, LLMCallLogApplication>();
        services.AddScoped<IAuthApplication, AuthApplication>();
        services.AddScoped<IUserApplication, UserApplication>();
        services.AddScoped<IModelApplication, ModelApplication>();
        services.AddScoped<IUserModelConfigApplication, UserModelConfigApplication>();
        services.AddScoped<IWorkApplication, WorkApplication>();
        services.AddScoped<IChapterApplication, ChapterApplication>();
        services.AddScoped<ICharacterApplication, CharacterApplication>();
        services.AddScoped<IOutlineApplication, OutlineApplication>();
        services.AddScoped<IVolumeApplication, VolumeApplication>();
        services.AddScoped<IForeshadowingApplication, ForeshadowingApplication>();
        services.AddScoped<ITimelineApplication, TimelineApplication>();
        services.AddScoped<IWorldApplication, WorldApplication>();
        services.AddScoped<ICharacterRelationshipApplication, CharacterRelationshipApplication>();
        services.AddScoped<ICharacterGraphApplication, CharacterGraphApplication>();
        services.AddScoped<ICharacterArcApplication, CharacterArcApplication>();
        services.AddScoped<IInspirationApplication, InspirationApplication>();
        services.AddScoped<IReferenceApplication, ReferenceApplication>();
        services.AddScoped<ITagApplication, TagApplication>();
        services.AddScoped<IDashboardApplication, DashboardApplication>();
        services.AddScoped<IAgentApplication, AgentApplication>();
        services.AddScoped<ICreationSessionManager, CreationSessionManager>();
        services.AddScoped<IChapterVersionManager, ChapterVersionManager>();
        services.AddScoped<IAdoptionManager, AdoptionManager>();
        services.AddScoped<IAutoSaveApplication, AutoSaveApplication>();
        services.AddScoped<ExportService>();

        return services;
    }
}

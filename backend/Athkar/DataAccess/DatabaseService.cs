using Microsoft.EntityFrameworkCore;
using Athkar.Areas.Domain.Audit;
using Athkar.Areas.Domain.Configuration;
using Athkar.Areas.Domain.Content;
using Athkar.Areas.Domain.Devices;
using Athkar.Areas.Domain.Localization;
using Athkar.Areas.Domain.Logging;
using Athkar.Areas.Domain.Notifications;
using Athkar.Areas.Domain.Quran;
using Athkar.Areas.Domain.Radio;
using Athkar.Areas.Domain.Recitations;
using Athkar.Areas.Domain.Reminders;
using Athkar.Areas.Domain.Staff;
using Athkar.Areas.Domain.Support;
using Athkar.Areas.Domain.Widgets;

namespace Athkar.DataAccess;

public class DatabaseService : DbContext
{
    public DatabaseService(DbContextOptions<DatabaseService> options) : base(options) { }

    // ── Staff (the only accounts that exist) ──
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<UserLogin> UserLogins => Set<UserLogin>();

    // ── Readers, as anonymous installs ──
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceNotification> DeviceNotifications => Set<DeviceNotification>();

    // ── Content ──
    public DbSet<AthkarCategory> Categories => Set<AthkarCategory>();
    public DbSet<CategoryTranslation> CategoryTranslations => Set<CategoryTranslation>();
    public DbSet<Dhikr> Adhkar => Set<Dhikr>();
    public DbSet<DhikrTranslation> DhikrTranslations => Set<DhikrTranslation>();
    public DbSet<RadioStation> RadioStations => Set<RadioStation>();
    public DbSet<RadioStationTranslation> RadioStationTranslations => Set<RadioStationTranslation>();
    public DbSet<Reciter> Reciters => Set<Reciter>();
    public DbSet<ReciterTranslation> ReciterTranslations => Set<ReciterTranslation>();
    public DbSet<Recitation> Recitations => Set<Recitation>();
    public DbSet<RecitationTranslation> RecitationTranslations => Set<RecitationTranslation>();

    // ── Localisation ──
    public DbSet<AppLanguage> Languages => Set<AppLanguage>();
    public DbSet<UiString> UiStrings => Set<UiString>();

    // ── Reminders and push ──
    public DbSet<ReminderCampaign> ReminderCampaigns => Set<ReminderCampaign>();
    public DbSet<ReminderCampaignTranslation> ReminderCampaignTranslations => Set<ReminderCampaignTranslation>();
    public DbSet<PushDispatch> PushDispatches => Set<PushDispatch>();
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<BroadcastTranslation> BroadcastTranslations => Set<BroadcastTranslation>();

    // ── Qur'an packages ──
    public DbSet<QuranPackage> QuranPackages => Set<QuranPackage>();

    // ── Home-screen widgets ──
    public DbSet<WidgetSettings> WidgetSettings => Set<WidgetSettings>();
    public DbSet<WidgetCatalogItem> WidgetCatalogItems => Set<WidgetCatalogItem>();
    public DbSet<WidgetCatalogItemTranslation> WidgetCatalogItemTranslations =>
        Set<WidgetCatalogItemTranslation>();

    // ── Platform ──
    public DbSet<AppConfiguration> AppConfigurations => Set<AppConfiguration>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<FaqTranslation> FaqTranslations => Set<FaqTranslation>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiLog> ApiLogs => Set<ApiLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DatabaseService).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

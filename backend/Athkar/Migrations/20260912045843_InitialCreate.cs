using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Athkar.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Method = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Path = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    StatusCode = table.Column<int>(type: "int", nullable: false),
                    ErrorCode = table.Column<int>(type: "int", nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    DeviceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppConfigurations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PrimaryColor = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    DefaultCalculationMethod = table.Column<int>(type: "int", nullable: false),
                    DefaultMadhab = table.Column<int>(type: "int", nullable: false),
                    MorningOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    EveningOffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    ContentVersion = table.Column<int>(type: "int", nullable: false),
                    MinimumAppBuild = table.Column<int>(type: "int", nullable: false),
                    ReviewingScholar = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewingScholarCredential = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupportEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SupportWebsite = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    PrivacyPolicyUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AndroidPackageName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IosAppStoreId = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AthkarCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Rhythm = table.Column<int>(type: "int", nullable: false),
                    Anchor = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AthkarCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Broadcasts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    TargetLanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    TargetPlatform = table.Column<int>(type: "int", nullable: true),
                    TargetDeviceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AndroidChannelId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    SentCount = table.Column<int>(type: "int", nullable: false),
                    FailedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Broadcasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<int>(type: "int", nullable: false),
                    PushToken = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    NotificationsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AppVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    CountryCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SyncedContentVersion = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FaqItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Languages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    NativeName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EnglishName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsRtl = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Languages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuranPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Version = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Script = table.Column<int>(type: "int", nullable: false),
                    HasWaqfAnnotations = table.Column<bool>(type: "bit", nullable: false),
                    ReleaseNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DownloadCount = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuranPackages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Adhkar",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    ArabicText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    SearchText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    RepeatCount = table.Column<int>(type: "int", nullable: false),
                    SourceBook = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Grade = table.Column<int>(type: "int", nullable: true),
                    GradedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Adhkar", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Adhkar_AthkarCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AthkarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CategoryTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CategoryTranslations_AthkarCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AthkarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReminderCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Key = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: true),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Delivery = table.Column<int>(type: "int", nullable: false),
                    LocalTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    Anchor = table.Column<int>(type: "int", nullable: false),
                    OffsetMinutes = table.Column<int>(type: "int", nullable: false),
                    Days = table.Column<int>(type: "int", nullable: false),
                    Audience = table.Column<int>(type: "int", nullable: false),
                    TargetLanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    TargetPlatform = table.Column<int>(type: "int", nullable: true),
                    AndroidChannelId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    IsUserAdjustable = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderCampaigns_AthkarCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AthkarCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BroadcastTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BroadcastId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BroadcastTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BroadcastTranslations_Broadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "Broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DeviceNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Route = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceNotifications_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FaqTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FaqItemId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaqTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaqTranslations_FaqItems_FaqItemId",
                        column: x => x.FaqItemId,
                        principalTable: "FaqItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UiStrings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LanguageId = table.Column<int>(type: "int", nullable: false),
                    Key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiStrings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UiStrings_Languages_LanguageId",
                        column: x => x.LanguageId,
                        principalTable: "Languages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Action = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Feedbacks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    DhikrId = table.Column<int>(type: "int", nullable: true),
                    ContactEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Reply = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    RepliedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RepliedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Feedbacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Feedbacks_Users_RepliedBy",
                        column: x => x.RepliedBy,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UserLogins",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    SessionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RefreshToken = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    RefreshExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLogins", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLogins_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    Role = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DhikrTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DhikrId = table.Column<int>(type: "int", nullable: false),
                    Translation = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Transliteration = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Virtue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DhikrTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DhikrTranslations_Adhkar_DhikrId",
                        column: x => x.DhikrId,
                        principalTable: "Adhkar",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PushDispatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: true),
                    BroadcastId = table.Column<int>(type: "int", nullable: true),
                    DeviceId = table.Column<int>(type: "int", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Attempts = table.Column<int>(type: "int", nullable: false),
                    MessageId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PushDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PushDispatches_Broadcasts_BroadcastId",
                        column: x => x.BroadcastId,
                        principalTable: "Broadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PushDispatches_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PushDispatches_ReminderCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ReminderCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReminderCampaignTranslations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CampaignId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ModificationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LanguageCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReminderCampaignTranslations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReminderCampaignTranslations_ReminderCampaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "ReminderCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Adhkar_CategoryId_SortOrder",
                table: "Adhkar",
                columns: new[] { "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Adhkar_SearchText",
                table: "Adhkar",
                column: "SearchText");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_CreationDate",
                table: "ApiLogs",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_ApiLogs_StatusCode",
                table: "ApiLogs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_AthkarCategories_Key",
                table: "AthkarCategories",
                column: "Key",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_AthkarCategories_SortOrder",
                table: "AthkarCategories",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreationDate",
                table: "AuditLogs",
                column: "CreationDate");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityName_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Broadcasts_Status_ScheduledAtUtc",
                table: "Broadcasts",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BroadcastTranslations_BroadcastId_LanguageCode",
                table: "BroadcastTranslations",
                columns: new[] { "BroadcastId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryTranslations_CategoryId_LanguageCode",
                table: "CategoryTranslations",
                columns: new[] { "CategoryId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceNotifications_DeviceId_CreationDate",
                table: "DeviceNotifications",
                columns: new[] { "DeviceId", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DeviceKey",
                table: "Devices",
                column: "DeviceKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_LastSeenAt",
                table: "Devices",
                column: "LastSeenAt");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_NotificationsEnabled_LanguageCode",
                table: "Devices",
                columns: new[] { "NotificationsEnabled", "LanguageCode" },
                filter: "[PushToken] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_DhikrTranslations_DhikrId_LanguageCode",
                table: "DhikrTranslations",
                columns: new[] { "DhikrId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_FaqItems_Category_SortOrder",
                table: "FaqItems",
                columns: new[] { "Category", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_FaqTranslations_FaqItemId_LanguageCode",
                table: "FaqTranslations",
                columns: new[] { "FaqItemId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_DeviceId",
                table: "Feedbacks",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_RepliedBy",
                table: "Feedbacks",
                column: "RepliedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Feedbacks_Status_CreationDate",
                table: "Feedbacks",
                columns: new[] { "Status", "CreationDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Languages_Code",
                table: "Languages",
                column: "Code",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_BroadcastId",
                table: "PushDispatches",
                column: "BroadcastId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_CampaignId_DeviceId_ScheduledAtUtc",
                table: "PushDispatches",
                columns: new[] { "CampaignId", "DeviceId", "ScheduledAtUtc" },
                unique: true,
                filter: "[CampaignId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_DeviceId",
                table: "PushDispatches",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_PushDispatches_Status_ScheduledAtUtc",
                table: "PushDispatches",
                columns: new[] { "Status", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_QuranPackages_Version",
                table: "QuranPackages",
                column: "Version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReminderCampaigns_CategoryId",
                table: "ReminderCampaigns",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderCampaigns_Key",
                table: "ReminderCampaigns",
                column: "Key",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderCampaignTranslations_CampaignId_LanguageCode",
                table: "ReminderCampaignTranslations",
                columns: new[] { "CampaignId", "LanguageCode" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UiStrings_LanguageId_Key",
                table: "UiStrings",
                columns: new[] { "LanguageId", "Key" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_RefreshToken",
                table: "UserLogins",
                column: "RefreshToken");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_SessionKey",
                table: "UserLogins",
                column: "SessionKey");

            migrationBuilder.CreateIndex(
                name: "IX_UserLogins_UserId",
                table: "UserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_Role",
                table: "UserRoles",
                columns: new[] { "UserId", "Role" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiLogs");

            migrationBuilder.DropTable(
                name: "AppConfigurations");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BroadcastTranslations");

            migrationBuilder.DropTable(
                name: "CategoryTranslations");

            migrationBuilder.DropTable(
                name: "DeviceNotifications");

            migrationBuilder.DropTable(
                name: "DhikrTranslations");

            migrationBuilder.DropTable(
                name: "FaqTranslations");

            migrationBuilder.DropTable(
                name: "Feedbacks");

            migrationBuilder.DropTable(
                name: "PushDispatches");

            migrationBuilder.DropTable(
                name: "QuranPackages");

            migrationBuilder.DropTable(
                name: "ReminderCampaignTranslations");

            migrationBuilder.DropTable(
                name: "UiStrings");

            migrationBuilder.DropTable(
                name: "UserLogins");

            migrationBuilder.DropTable(
                name: "UserRoles");

            migrationBuilder.DropTable(
                name: "Adhkar");

            migrationBuilder.DropTable(
                name: "FaqItems");

            migrationBuilder.DropTable(
                name: "Broadcasts");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "ReminderCampaigns");

            migrationBuilder.DropTable(
                name: "Languages");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "AthkarCategories");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DecisionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EngineState = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Mode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Cause = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    WaterfallRisk = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TelegramState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RailPermissionA = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RailPermissionB = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Entry = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Tp = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Grams = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RotationCapThisSession = table.Column<int>(type: "int", nullable: false),
                    ForceWhereToTrade = table.Column<bool>(type: "bit", nullable: false),
                    SnapshotHash = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DecisionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HazardWindows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsBlocked = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EndUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HazardWindows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitialCashAed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CashAed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GoldGrams = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LedgerPositions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Grams = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mt5BuyPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ShopBuyPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    DebitAed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Mt5BuyTime = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Mt5SellPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ShopSellPrice = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    CreditAed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetProfitAed = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ClosedSession = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OpenedSession = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LedgerPositions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MacroCacheStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MacroBias = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InstitutionalBias = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CbFlowFlag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PositioningFlag = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LastRefreshedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MacroCacheStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketSnapshots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Atr = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Adr = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Ma20 = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Session = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RiskProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Level = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MaxDrawdownPercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RiskProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RuntimeTimelineEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CycleId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    TradeId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuntimeTimelineEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SessionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Session = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramChannels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReliabilityFlags = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    WinRateRolling = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    ImpactScore = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    ConflictScore = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    LastActiveTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramChannels", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TelegramSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ChannelKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    ConsensusState = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PanicSuspected = table.Column<bool>(type: "bit", nullable: false),
                    ServerTimeUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RawMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OutcomeTag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TelegramSignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rail = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Entry = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    ExpiryUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaxLifeSeconds = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradeSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Rail = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Entry = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    TakeProfit = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    PendingExpirationUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MaxLifeSeconds = table.Column<int>(type: "int", nullable: false),
                    Confidence = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeSignals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TradingViewAlertLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timeframe = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Signal = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ConfirmationTag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Bias = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RiskTag = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(8,4)", precision: 8, scale: 4, nullable: false),
                    Volatility = table.Column<decimal>(type: "decimal(18,5)", precision: 18, scale: 5, nullable: false),
                    Timestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradingViewAlertLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketSnapshotTimeframes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Timeframe = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Open = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    High = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Low = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    Close = table.Column<decimal>(type: "decimal(10,6)", precision: 10, scale: 6, nullable: false),
                    MarketSnapshotId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketSnapshotTimeframes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarketSnapshotTimeframes_MarketSnapshots_MarketSnapshotId",
                        column: x => x.MarketSnapshotId,
                        principalTable: "MarketSnapshots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "MacroCacheStates",
                columns: new[] { "Id", "CbFlowFlag", "InstitutionalBias", "LastRefreshedUtc", "MacroBias", "PositioningFlag", "Source" },
                values: new object[] { new Guid("55555555-5555-5555-5555-555555555555"), "UNKNOWN", "UNKNOWN", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "UNKNOWN", "UNKNOWN", "SEED" });

            migrationBuilder.InsertData(
                table: "RiskProfiles",
                columns: new[] { "Id", "IsActive", "Level", "MaxDrawdownPercent", "Name" },
                values: new object[,]
                {
                    { new Guid("33333333-3333-3333-3333-333333333333"), true, "Medium", 5m, "Balanced" },
                    { new Guid("44444444-4444-4444-4444-444444444444"), false, "Low", 2m, "Conservative" }
                });

            migrationBuilder.InsertData(
                table: "StrategyProfiles",
                columns: new[] { "Id", "Description", "IsActive", "Name" },
                values: new object[,]
                {
                    { new Guid("11111111-1111-1111-1111-111111111111"), "Baseline production strategy profile.", true, "Standard" },
                    { new Guid("22222222-2222-2222-2222-222222222222"), "War expansion harvest profile with stricter kill-switch behavior.", false, "WarPremium" }
                });

            migrationBuilder.InsertData(
                table: "TelegramChannels",
                columns: new[] { "Id", "ChannelKey", "ConflictScore", "ImpactScore", "LastActiveTimeUtc", "Name", "ReliabilityFlags", "Type", "Weight", "WinRateRolling" },
                values: new object[,]
                {
                    { new Guid("66666666-6666-6666-6666-666666666661"), "@core_gold_1", 0m, 0m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Core Gold 1", "unknown", "NEWS", 1.50m, 0m },
                    { new Guid("66666666-6666-6666-6666-666666666662"), "@core_gold_2", 0m, 0m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Core Gold 2", "unknown", "INTRADAY", 1.25m, 0m }
                });

            migrationBuilder.CreateIndex(
                name: "IX_DecisionLogs_CreatedAtUtc",
                table: "DecisionLogs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_HazardWindows_IsActive_IsBlocked_StartUtc_EndUtc",
                table: "HazardWindows",
                columns: new[] { "IsActive", "IsBlocked", "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerAccounts_UpdatedAtUtc",
                table: "LedgerAccounts",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerPositions_IsClosed_Mt5BuyTime",
                table: "LedgerPositions",
                columns: new[] { "IsClosed", "Mt5BuyTime" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerPositions_TradeId",
                table: "LedgerPositions",
                column: "TradeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketSnapshotTimeframes_MarketSnapshotId",
                table: "MarketSnapshotTimeframes",
                column: "MarketSnapshotId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeTimelineEvents_CreatedAtUtc",
                table: "RuntimeTimelineEvents",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeTimelineEvents_CycleId",
                table: "RuntimeTimelineEvents",
                column: "CycleId");

            migrationBuilder.CreateIndex(
                name: "IX_RuntimeTimelineEvents_TradeId",
                table: "RuntimeTimelineEvents",
                column: "TradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TelegramChannels_ChannelKey",
                table: "TelegramChannels",
                column: "ChannelKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramSignals_ServerTimeUtc",
                table: "TelegramSignals",
                column: "ServerTimeUtc");

            migrationBuilder.CreateIndex(
                name: "IX_TradingViewAlertLogs_Timestamp",
                table: "TradingViewAlertLogs",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DecisionLogs");

            migrationBuilder.DropTable(
                name: "HazardWindows");

            migrationBuilder.DropTable(
                name: "LedgerAccounts");

            migrationBuilder.DropTable(
                name: "LedgerPositions");

            migrationBuilder.DropTable(
                name: "MacroCacheStates");

            migrationBuilder.DropTable(
                name: "MarketSnapshotTimeframes");

            migrationBuilder.DropTable(
                name: "RiskProfiles");

            migrationBuilder.DropTable(
                name: "RuntimeTimelineEvents");

            migrationBuilder.DropTable(
                name: "SessionStates");

            migrationBuilder.DropTable(
                name: "StrategyProfiles");

            migrationBuilder.DropTable(
                name: "TelegramChannels");

            migrationBuilder.DropTable(
                name: "TelegramSignals");

            migrationBuilder.DropTable(
                name: "Trades");

            migrationBuilder.DropTable(
                name: "TradeSignals");

            migrationBuilder.DropTable(
                name: "TradingViewAlertLogs");

            migrationBuilder.DropTable(
                name: "MarketSnapshots");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Yashdeep.Persistence.Cloud.Migrations
{
    /// <inheritdoc />
    public partial class InitialCloudFoundationalSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Enable PostgreSQL Row-Level Security (RLS) on tenant-isolated tables
            migrationBuilder.Sql("ALTER TABLE organizations ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE branches ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE outlets ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE terminals ENABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE devices ENABLE ROW LEVEL SECURITY;");

            // Create PostgreSQL RLS policies matching current setting 'app.current_tenant_id'
            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON organizations
                FOR ALL
                USING (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");

            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON branches
                FOR ALL
                USING (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");

            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON outlets
                FOR ALL
                USING (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");

            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON terminals
                FOR ALL
                USING (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");

            migrationBuilder.Sql(@"
                CREATE POLICY tenant_isolation_policy ON devices
                FOR ALL
                USING (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid)
                WITH CHECK (""TenantId"" = NULLIF(current_setting('app.current_tenant_id', true), '')::uuid);
            ");
            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TradeName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    GSTIN = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ExciseLicenseNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContactEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.TenantId);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalEntityName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TaxRegistrationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    StateExciseLicenseNo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.OrganizationId);
                    table.UniqueConstraint("AK_organizations_TenantId_OrganizationId", x => new { x.TenantId, x.OrganizationId });
                    table.ForeignKey(
                        name: "FK_organizations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AddressLine1 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    AddressLine2 = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.BranchId);
                    table.UniqueConstraint("AK_branches_TenantId_OrganizationId_BranchId", x => new { x.TenantId, x.OrganizationId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_branches_organizations_TenantId_OrganizationId",
                        columns: x => new { x.TenantId, x.OrganizationId },
                        principalTable: "organizations",
                        principalColumns: new[] { "TenantId", "OrganizationId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "outlets",
                columns: table => new
                {
                    OutletId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outlets", x => x.OutletId);
                    table.UniqueConstraint("AK_outlets_TenantId_OrganizationId_BranchId_OutletId", x => new { x.TenantId, x.OrganizationId, x.BranchId, x.OutletId });
                    table.ForeignKey(
                        name: "FK_outlets_branches_TenantId_OrganizationId_BranchId",
                        columns: x => new { x.TenantId, x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "OrganizationId", "BranchId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_outlets_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "terminals",
                columns: table => new
                {
                    TerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IPAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_terminals", x => x.TerminalId);
                    table.UniqueConstraint("AK_terminals_TenantId_OrganizationId_BranchId_TerminalId", x => new { x.TenantId, x.OrganizationId, x.BranchId, x.TerminalId });
                    table.ForeignKey(
                        name: "FK_terminals_branches_TenantId_OrganizationId_BranchId",
                        columns: x => new { x.TenantId, x.OrganizationId, x.BranchId },
                        principalTable: "branches",
                        principalColumns: new[] { "TenantId", "OrganizationId", "BranchId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_terminals_outlets_OutletId",
                        column: x => x.OutletId,
                        principalTable: "outlets",
                        principalColumn: "OutletId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_terminals_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutletId = table.Column<Guid>(type: "uuid", nullable: true),
                    TerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeviceIdentifier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HardwareFingerprint = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    PublicEd25519Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastSyncAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.DeviceId);
                    table.ForeignKey(
                        name: "FK_devices_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_devices_terminals_TenantId_OrganizationId_BranchId_Terminal~",
                        columns: x => new { x.TenantId, x.OrganizationId, x.BranchId, x.TerminalId },
                        principalTable: "terminals",
                        principalColumns: new[] { "TenantId", "OrganizationId", "BranchId", "TerminalId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_OrganizationId_Code",
                table: "branches",
                columns: new[] { "TenantId", "OrganizationId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId_DeviceIdentifier",
                table: "devices",
                columns: new[] { "TenantId", "DeviceIdentifier" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId_HardwareFingerprint",
                table: "devices",
                columns: new[] { "TenantId", "HardwareFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_devices_TenantId_OrganizationId_BranchId_TerminalId",
                table: "devices",
                columns: new[] { "TenantId", "OrganizationId", "BranchId", "TerminalId" });

            migrationBuilder.CreateIndex(
                name: "IX_organizations_TenantId_Code",
                table: "organizations",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_outlets_TenantId_BranchId_Code",
                table: "outlets",
                columns: new[] { "TenantId", "BranchId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tenants_ContactEmail",
                table: "tenants",
                column: "ContactEmail");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_GSTIN",
                table: "tenants",
                column: "GSTIN");

            migrationBuilder.CreateIndex(
                name: "IX_tenants_TradeName",
                table: "tenants",
                column: "TradeName");

            migrationBuilder.CreateIndex(
                name: "IX_terminals_OutletId",
                table: "terminals",
                column: "OutletId");

            migrationBuilder.CreateIndex(
                name: "IX_terminals_TenantId_BranchId_Code",
                table: "terminals",
                columns: new[] { "TenantId", "BranchId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation_policy ON devices;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation_policy ON terminals;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation_policy ON outlets;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation_policy ON branches;");
            migrationBuilder.Sql("DROP POLICY IF EXISTS tenant_isolation_policy ON organizations;");

            migrationBuilder.Sql("ALTER TABLE devices DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE terminals DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE outlets DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE branches DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.Sql("ALTER TABLE organizations DISABLE ROW LEVEL SECURITY;");
            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "terminals");

            migrationBuilder.DropTable(
                name: "outlets");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "organizations");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}

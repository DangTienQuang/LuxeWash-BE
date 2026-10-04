using AutoWashPro.BLL.Services;
using AutoWashPro.BLL.DTOs;
using AutoWashPro.BLL.Extensions;
using AutoWashPro.DAL.Entities;
using AutoWashPro.DAL.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var checks = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    Console.WriteLine("PASS: " + description);
    checks++;
}
User Person(string role = "Staff") => new() { Role = role, Status = "Active", PasswordHash = "test", PhoneNumber = "0777777777" };
var legacy = Person();
legacy.EmployeeProfile = new EmployeeProfile { FullName = " Nguyễn Duy Anh " };
Check(PersonnelNames.DisplayName(legacy) == "Nguyễn Duy Anh", "Legacy employee-only name fallback");
legacy.StaffProfile = new StaffProfile { FullName = "  " };
Check(PersonnelNames.DisplayName(legacy) == "Nguyễn Duy Anh", "Whitespace role name fallback");
PersonnelNames.EnsureProfiles(legacy);
Check(legacy.StaffProfile.FullName == "Nguyễn Duy Anh", "Missing role name is filled from employee");
legacy.StaffProfile.FullName = "Role name";
PersonnelNames.EnsureProfiles(legacy);
Check(legacy.EmployeeProfile.FullName == " Nguyễn Duy Anh ", "Existing conflicting name is not silently overwritten");
PersonnelNames.Rename(legacy, " Tên mới ");
Check(legacy.StaffProfile.FullName == "Tên mới" && legacy.EmployeeProfile.FullName == "Tên mới", "Explicit rename synchronizes profiles");
var manager = Person("Manager");
manager.EmployeeProfile = new EmployeeProfile { FullName = "Quản lý" };
PersonnelNames.EnsureProfiles(manager);
Check(manager.ManagerProfile?.FullName == "Quản lý", "Legacy manager gets role profile");
var roleOnly = Person();
roleOnly.StaffProfile = new StaffProfile { FullName = "Nhân viên" };
PersonnelNames.EnsureProfiles(roleOnly);
Check(roleOnly.EmployeeProfile?.FullName == "Nhân viên" && roleOnly.EmployeeProfile.BranchId == null, "Role-only employee gets profile without inventing branch");
Check(PersonnelNames.DisplayName(Person()) == "0777777777", "Phone used only when no usable name exists");

// Optional read-only checks against the configured database; no server startup or migration.
if (args.Contains("--database"))
{
    var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
    var configuration = new ConfigurationBuilder().SetBasePath(Path.Combine(root, "API"))
        .AddJsonFile("appsettings.json").AddJsonFile("appsettings.Development.json", optional: true)
        .AddEnvironmentVariables().Build();
    var services = new ServiceCollection();
    services.AddDatabaseInfrastructure(configuration);
    using var provider = services.BuildServiceProvider();
    using var scope = provider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AutoWashDbContext>();
    var user = await db.Users.AsNoTracking().Include(u => u.EmployeeProfile).Include(u => u.StaffProfile)
        .Include(u => u.ManagerProfile).FirstOrDefaultAsync(u => u.UserId == 32);
    if (user != null)
    {
        var expected = PersonnelNames.DisplayName(user);
        var users = new UserService(db);
        var found = await users.GetAllCustomersAsync(1, 100, expected, null, user.Role);
        Check(found.Items.Any(u => u.UserId == user.UserId && u.FullName == expected), "User #32 found by displayed name before pagination");
        var hr = new StaffManagementService(db);
        var staff = await hr.GetStaffsAsync(expected, user.Role, null);
        Check(staff.Any(u => u.UserId == user.UserId && u.FullName == expected), "Personnel search agrees with user name");
        var assignments = await hr.GetMyShiftAssignmentsAsync(user.UserId, null, null);
        Check(assignments.All(a => a.StaffName == expected), "All existing assignments use same name");
        var profile = await users.GetProfileAsync(user.UserId);
        Check(profile.FullName == expected, "Profile and assignment names agree");
        Console.WriteLine($"Read-only database check: {assignments.Count} assignments inspected.");
    }
    else Console.WriteLine("Database has no user #32; targeted checks skipped.");
    if (args.Contains("--transaction-tests"))
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var branchId = await db.Branches.Select(b => b.BranchId).FirstAsync();
            string TestPhone() => "09" + Random.Shared.Next(10000000, 99999999);
            var employees = new EmployeeService(db);
            var hr = new StaffManagementService(db);
            var users = new UserService(db);
            foreach (var role in new[] { "Staff", "Manager" })
            {
                var created = await employees.CreateEmployeeAsync(new CreateEmployeeDTO
                {
                    PhoneNumber = TestPhone(), Password = "Regression123", FullName = " Hồ sơ thử " + role,
                    Role = role, BranchId = branchId
                });
                db.ChangeTracker.Clear();
                var saved = await db.Users.Include(u => u.EmployeeProfile).Include(u => u.StaffProfile)
                    .Include(u => u.ManagerProfile).SingleAsync(u => u.UserId == created.UserId);
                Check(saved.EmployeeProfile?.BranchId == branchId &&
                    (role == "Staff" ? saved.StaffProfile?.FullName : saved.ManagerProfile?.FullName) == created.FullName,
                    role + " employee creation persists both profiles and branch");
                await hr.UpdateStaffAsync(created.UserId, new UpdateStaffDTO { FullName = "Tên mới " + role });
                db.ChangeTracker.Clear();
                saved = await db.Users.Include(u => u.EmployeeProfile).Include(u => u.StaffProfile)
                    .Include(u => u.ManagerProfile).SingleAsync(u => u.UserId == created.UserId);
                Check(saved.EmployeeProfile?.FullName == "Tên mới " + role &&
                    (role == "Staff" ? saved.StaffProfile?.FullName : saved.ManagerProfile?.FullName) == "Tên mới " + role,
                    role + " admin update persists synchronized names");
                await users.UpdateProfileAsync(created.UserId, new UpdateUserProfileDTO { FullName = "Tự đổi tên " + role });
                db.ChangeTracker.Clear();
                var matches = await users.GetAllCustomersAsync(1, 10, "Tự đổi tên " + role, null, role);
                Check(matches.Items.Any(u => u.UserId == created.UserId && u.FullName == "Tự đổi tên " + role),
                    role + " self-profile update remains searchable");
            }
            var roleCreated = await hr.CreateStaffAsync(new CreateStaffDTO
            {
                PhoneNumber = TestPhone(), Password = "Regression123", FullName = "Tạo bằng API Staff"
            });
            db.ChangeTracker.Clear();
            var roleSaved = await db.Users.Include(u => u.EmployeeProfile).Include(u => u.StaffProfile)
                .SingleAsync(u => u.UserId == roleCreated.UserId);
            Check(roleSaved.EmployeeProfile?.FullName == roleSaved.StaffProfile?.FullName,
                "Role creation API persists matching employee profile");
            var legacyFixture = Person();
            legacyFixture.PhoneNumber = TestPhone();
            legacyFixture.EmployeeProfile = new EmployeeProfile { FullName = "Tên legacy được giữ", BranchId = branchId };
            db.Users.Add(legacyFixture);
            await db.SaveChangesAsync();
            await hr.UpdateStaffAsync(legacyFixture.UserId, new UpdateStaffDTO { Position = "Nhân viên" });
            db.ChangeTracker.Clear();
            var repaired = await db.Users.Include(u => u.EmployeeProfile).Include(u => u.StaffProfile)
                .SingleAsync(u => u.UserId == legacyFixture.UserId);
            Check(repaired.StaffProfile?.FullName == "Tên legacy được giữ" && repaired.EmployeeProfile?.BranchId == branchId,
                "Editing legacy employee without name preserves name and branch");
        }
        finally
        {
            await transaction.RollbackAsync();
            Console.WriteLine("All fixture writes rolled back.");
        }
    }
}
Console.WriteLine($"{checks} regression checks passed.");

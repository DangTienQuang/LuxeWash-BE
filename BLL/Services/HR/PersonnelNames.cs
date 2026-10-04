using AutoWashPro.BLL.Constants;
using AutoWashPro.DAL.Entities;

namespace AutoWashPro.BLL.Services;

/// <summary>One name policy for both legacy employee and role-specific profiles.</summary>
internal static class PersonnelNames
{
    public static string DisplayName(User user)
    {
        string? primary = user.Role switch
        {
            UserRoles.Staff => user.StaffProfile?.FullName,
            UserRoles.Manager => user.ManagerProfile?.FullName,
            UserRoles.Customer => user.CustomerProfile?.FullName,
            _ => user.BusinessProfile?.CompanyName ?? user.CustomerProfile?.FullName
        };
        return FirstName(primary, user.EmployeeProfile?.FullName, user.PhoneNumber);
    }

    private static string FirstName(params string?[] names) =>
        names.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))?.Trim() ?? "N/A";

    // Only fill missing profiles; never replace an existing name unless explicitly requested.
    public static void EnsureProfiles(User user)
    {
        if (user.Role != UserRoles.Staff && user.Role != UserRoles.Manager) return;
        var name = DisplayName(user);
        user.EmployeeProfile ??= new EmployeeProfile { FullName = name };
        if (string.IsNullOrWhiteSpace(user.EmployeeProfile.FullName)) user.EmployeeProfile.FullName = name;
        if (user.Role == UserRoles.Staff)
        {
            user.StaffProfile ??= new StaffProfile { FullName = name };
            if (string.IsNullOrWhiteSpace(user.StaffProfile.FullName)) user.StaffProfile.FullName = name;
        }
        else
        {
            user.ManagerProfile ??= new ManagerProfile { FullName = name };
            if (string.IsNullOrWhiteSpace(user.ManagerProfile.FullName)) user.ManagerProfile.FullName = name;
        }
    }

    public static void Rename(User user, string name)
    {
        EnsureProfiles(user);
        name = name.Trim();
        if (user.EmployeeProfile != null) user.EmployeeProfile.FullName = name;
        if (user.StaffProfile != null) user.StaffProfile.FullName = name;
        if (user.ManagerProfile != null) user.ManagerProfile.FullName = name;
    }
}

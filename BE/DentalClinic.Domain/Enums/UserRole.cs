namespace DentalClinic.Domain.Enums;

public static class UserRole
{
    public const string Patient = "Patient";
    public const string Receptionist = "Receptionist";
    public const string Dentist = "Dentist";
    public const string DepartmentManager = "DepartmentManager";
    public const string SystemAdministrator = "SystemAdministrator";

    public static readonly string[] All = { Patient, Receptionist, Dentist, DepartmentManager, SystemAdministrator };

    public static bool IsValid(string role) => All.Contains(role);
}

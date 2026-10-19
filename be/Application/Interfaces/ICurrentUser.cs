namespace NX_MTD.Application.Interfaces;

public interface ICurrentUser
{
    int AccountId { get; }
    int EmployeeId { get; }
    string Role { get; }
    int? TeamId { get; }
}
